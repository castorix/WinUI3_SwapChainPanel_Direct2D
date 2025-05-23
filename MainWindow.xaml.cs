// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

using System.ComponentModel;
using System.Runtime.InteropServices;
using GlobalStructures;
using static GlobalStructures.GlobalTools;
using DXGI;
using static DXGI.DXGITools;
using Direct2D;
using static Direct2D.D2DTools;
using WIC;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WinUI3_SwapChainPanel_Direct2D
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        [ComImport, Guid("63aad0b8-7c24-40ff-85a8-640d944cc325"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface ISwapChainPanelNative
        {
            [PreserveSig]
            HRESULT SetSwapChain(IDXGISwapChain swapChain);
        }

        [DllImport("User32.dll", SetLastError = true)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool QueryPerformanceFrequency(out LARGE_INTEGER lpFrequency);

        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint GetDpiForWindow(IntPtr hwnd);


        ID2D1Factory m_pD2DFactory = null;
        ID2D1Factory1 m_pD2DFactory1 = null;
        IWICImagingFactory m_pWICImagingFactory = null;

        IntPtr m_pD3D11DevicePtr = IntPtr.Zero; //Used in CreateSwapChain
        ID3D11DeviceContext m_pD3D11DeviceContext = null; // Released in Clean : not used
        IDXGIDevice1 m_pDXGIDevice = null; // Released in Clean

        ID2D1Device m_pD2DDevice = null; // Released in CreateDeviceContext
        ID2D1DeviceContext m_pD2DDeviceContext = null; // Released in Clean
        ID2D1DeviceContext3 m_pD2DDeviceContext3 = null;

        ID2D1Bitmap m_pD2DBitmapBackground = null;
        ID2D1Bitmap m_pD2DBitmap = null;
        ID2D1Bitmap m_pD2DBitmap1 = null;

        ID2D1Bitmap1 m_pD2DTargetBitmap = null;
        IDXGISwapChain1 m_pDXGISwapChain1 = null;
        ID2D1SolidColorBrush m_pMainBrush = null;

        private CSprite spriteBird = null;

        private IntPtr hWndMain = IntPtr.Zero;
        private Microsoft.UI.Windowing.AppWindow _apw;

        private LARGE_INTEGER liFreq;

        private Random rand = null;
        private Random randColor = null;
        private int nNbButterflyInitial = 2;
        private List<CSprite> CSprites = new List<CSprite>();
        private bool g_bSpritesCreated = false;

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Test SwapChainPanel with Direct2D & ID2D1SpriteBatch";

            hWndMain = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId myWndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWndMain);
            _apw = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(myWndId);

            m_pWICImagingFactory = (IWICImagingFactory)Activator.CreateInstance(Type.GetTypeFromCLSID(WICTools.CLSID_WICImagingFactory));

            liFreq = new LARGE_INTEGER();
            QueryPerformanceFrequency(out liFreq);

            this.Closed += MainWindow_Closed;
            //D2DPanel1.SizeChanged += D2DPanel1_SizeChanged;

            rand = new Random();
            randColor = new Random();

            HRESULT hr = CreateD2D1Factory();
            if (hr == HRESULT.S_OK)
            {
                hr = CreateDeviceContext();
                hr = CreateDeviceResources();
                hr = CreateSwapChain(IntPtr.Zero);
                if (hr == HRESULT.S_OK)
                {
                    hr = ConfigureSwapChain();
                    ISwapChainPanelNative panelNative = WinRT.CastExtensions.As<ISwapChainPanelNative>(scpD2D);
                    hr = panelNative.SetSwapChain(m_pDXGISwapChain1);
                }
                scpD2D.SizeChanged += scpD2D_SizeChanged;
                CompositionTarget.Rendering += CompositionTarget_Rendering;
                       
                if (!g_bSpritesCreated)
                    CreateSprites();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private float _SpriteSpeed = 1.0f;

        private float SpriteSpeed
        {
            get => _SpriteSpeed;
            set
            {
                _SpriteSpeed = value;
                if (spriteBird != null)
                {
                    spriteBird.StepX = (spriteBird.StepX < 0) ? -SpriteSpeed : SpriteSpeed;
                    spriteBird.StepY = (spriteBird.StepY < 0) ? -SpriteSpeed : SpriteSpeed;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpriteSpeed)));
            }
        }
        public double GetSpeed(float? x) => _SpriteSpeed;
        public float? SetSpeed(double x) => SpriteSpeed = (float)x;

        // To avoid "Only a single ContentDialog can be open at any time.'
        bool bDialog = false;
        private void myButton_Click(object sender, RoutedEventArgs e)
        {
            if (!bDialog)
            {
                Click();
            }
        }

        private async void Click()
        {
            //bRender = false;

            StackPanel sp = new StackPanel();
            // https://www.unicode.org/emoji/charts/full-emoji-list.html
            FontIcon fi = new FontIcon()
            {
                FontFamily = new FontFamily("Segoe UI Emoji"),
                Glyph = "\U0001F439",
                FontSize = 50
            };
            sp.Children.Add(fi);
            TextBlock tb = new TextBlock();
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.Text = "You clicked on the Button !";
            sp.Children.Add(tb);
            ContentDialog cd = new ContentDialog()
            {
                Title = "Information",
                Content = sp,
                CloseButtonText = "Ok"
            };
            cd.XamlRoot = this.Content.XamlRoot;
            cd.Closed += Cd_Closed;
            bDialog = true;
            var res = await cd.ShowAsync();          
            //bRender = true;
        }

        private void Cd_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            bDialog = false;
        }

        private void buttonButterfly_Click(object sender, RoutedEventArgs e)
        {
            AddButterfly(m_pD2DBitmap1, 13, 7, 87);
        }

        private void AddButterfly(ID2D1Bitmap pBitmap, int nXSprite, int nYSprite, int nCountSprite)
        {
            if (pBitmap != null)
            {
                m_pD2DDeviceContext.GetSize(out D2D1_SIZE_F size);
                float nClientWidth = (float)size.width;
                float nClientHeight = (float)size.height;

                float nScale = rand.NextSingle() * 1;
                D2D1_MATRIX_3X2_F scale = new D2D1_MATRIX_3X2_F();
                scale._11 = nScale;
                scale._22 = nScale;
                Array colors = ColorF.Enum.GetValues(typeof(ColorF.Enum));
                ColorF.Enum randomColor;
                randomColor = (ColorF.Enum)colors.GetValue(randColor.Next(colors.Length));
                CSprite s = new CSprite(m_pD2DDeviceContext3, pBitmap, (uint)nXSprite, (uint)nYSprite, (uint)nCountSprite, rand.NextSingle() * 5, rand.NextSingle() * 5, new ColorF(randomColor), scale);
                CSprites.Add(s);

                pBitmap.GetSize(out D2D1_SIZE_F bmpSize);
                float nWidth = bmpSize.width / nXSprite;
                float nHeight = bmpSize.width / nYSprite;
                if (scale._11 != 0)
                {
                    //nWidth *= scale._11;
                    nClientWidth *= 1 / scale._11;
                }
                if (scale._22 != 0)
                {
                    //nHeight *= scale._22;
                    nClientHeight *= 1 / scale._22;
                }

                float nX = rand.NextSingle() * nClientWidth;
                float nY = rand.NextSingle() * nClientHeight;
                if (nX + nWidth >= nClientWidth)
                    nX = nClientWidth - nWidth;
                if (nX <= 0)
                    nX = 0;
                if (nY + nHeight >= nClientHeight)
                    nY = nClientHeight - nHeight;
                if (nY <= 0)
                    nY = 0;
                s.X = nX;
                s.Y = nY;
            }
        }

        private void scpD2D_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //string sMessage = "NewSize = " + string.Format("{0}, {1}", e.NewSize.Width, e.NewSize.Height);
            //System.Diagnostics.Debug.WriteLine(sMessage);
            Resize(e.NewSize);           
        }

        private bool bRender = true;
        private ulong nLastTime = 0, nTotalTime = 0;
        private uint nNbTotalFrames = 0, nLastNbFrames = 0;
        private void CompositionTarget_Rendering(object sender, object e)
        {
            HRESULT hr = HRESULT.S_OK;
            if (bRender)
            {
                Render();
                if (m_pDXGISwapChain1 != null)
                {
                    DXGI_FRAME_STATISTICS fs = new DXGI_FRAME_STATISTICS();
                    hr = m_pDXGISwapChain1.GetFrameStatistics(out fs);
                    // 0x887A000B DXGI_ERROR_FRAME_STATISTICS_DISJOINT            
                    if (hr == HRESULT.S_OK)
                    {
                        ulong nCurrentTime = (ulong)fs.SyncQPCTime.QuadPart;
                        nNbTotalFrames += fs.PresentCount - nLastNbFrames;
                        if (nLastTime != 0)
                        {
                            nTotalTime += (nCurrentTime - nLastTime);
                            double nSeconds = nTotalTime / (ulong)liFreq.QuadPart;
                            if (nSeconds >= 1)
                            {
                                tbFPS.Text = nNbTotalFrames.ToString() + " FPS";
                                nNbTotalFrames = 0;
                                nTotalTime = 0;
                            }
                        }
                        nLastNbFrames = fs.PresentCount;
                        nLastTime = nCurrentTime;
                    }
                }
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            Clean();
        }

        public HRESULT CreateD2D1Factory()
        {
            HRESULT hr = HRESULT.S_OK;
            D2D1_FACTORY_OPTIONS options = new D2D1_FACTORY_OPTIONS();

            // Needs "Enable native code Debugging"
#if DEBUG
            options.debugLevel = D2D1_DEBUG_LEVEL.D2D1_DEBUG_LEVEL_INFORMATION;
#endif

            hr = D2DTools.D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, ref D2DTools.CLSID_D2D1Factory, ref options, out m_pD2DFactory);
            m_pD2DFactory1 = (ID2D1Factory1)m_pD2DFactory;
            return hr;
        }

        HRESULT Render()
        {
            HRESULT hr = HRESULT.S_OK;
            if (m_pD2DDeviceContext != null)
            {
                m_pD2DDeviceContext.BeginDraw();
                m_pD2DDeviceContext.GetSize(out D2D1_SIZE_F size);

                if (m_pD2DBitmapBackground != null)
                {
                    m_pD2DBitmapBackground.GetSize(out D2D1_SIZE_F sizeBmpBackground);
                    D2D1_RECT_F destRectBackground = new D2D1_RECT_F(0.0f, 0.0f, size.width, size.height);
                    D2D1_RECT_F sourceRectBackground = new D2D1_RECT_F(0.0f, 0.0f, sizeBmpBackground.width, sizeBmpBackground.height);
                    m_pD2DDeviceContext.DrawBitmap(m_pD2DBitmapBackground, ref destRectBackground, 1.0f, D2D1_BITMAP_INTERPOLATION_MODE.D2D1_BITMAP_INTERPOLATION_MODE_LINEAR, ref sourceRectBackground);
                }
                else
                {
                    m_pD2DDeviceContext.Clear(new ColorF(ColorF.Enum.Black));
                }
                if (m_pD2DDeviceContext3 != null)
                {
                    m_pD2DDeviceContext3.GetAntialiasMode(out D2D1_ANTIALIAS_MODE nOldAntialiasMode);
                    m_pD2DDeviceContext3.SetAntialiasMode(D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);

                    foreach (CSprite s in CSprites)
                    {
                        s.X += ((rand.NextSingle()) * s.StepX);
                        s.Y += ((rand.NextSingle()) * s.StepY);
                        s.Move(m_pD2DDeviceContext3, 0, true);
                        s.Draw(m_pD2DDeviceContext3, s.CurrentIndex, 1, true);
                        s.CurrentIndex++;
                    }
                    if (spriteBird != null)
                    {
                        spriteBird.X += spriteBird.StepX;
                        spriteBird.Y += spriteBird.StepY;
                        spriteBird.Move(m_pD2DDeviceContext3, spriteBird.HORIZONTALFLIP_RIGHT, true);
                        spriteBird.Draw(m_pD2DDeviceContext3, spriteBird.CurrentIndex, 1, true);
                        spriteBird.CurrentIndex++;
                    }
                    m_pD2DDeviceContext3.SetAntialiasMode(nOldAntialiasMode);
                }

                ulong tag1, tag2 = 0;
                hr = m_pD2DDeviceContext.EndDraw(out tag1, out tag2);

                if ((uint)hr == D2DTools.D2DERR_RECREATE_TARGET)
                {
                    m_pD2DDeviceContext.SetTarget(null);
                    SafeRelease(ref m_pD2DDeviceContext);
                    hr = CreateDeviceContext();
                    CleanDeviceResources();
                    hr = CreateDeviceResources();
                    hr = CreateSwapChain(IntPtr.Zero);
                    hr = ConfigureSwapChain();
                }
                hr = m_pDXGISwapChain1.Present(1, 0);
            }
            return (hr);
        }

        HRESULT Resize(Size sz)
        {
            HRESULT hr = HRESULT.S_OK;

            if (m_pDXGISwapChain1 != null)
            {
                if (m_pD2DDeviceContext != null)
                    m_pD2DDeviceContext.SetTarget(null);

                if (m_pD2DTargetBitmap != null)
                    SafeRelease(ref m_pD2DTargetBitmap);

                // 0, 0 => HRESULT: 0x80070057 (E_INVALIDARG) if not CreateSwapChainForHwnd
                //hr = m_pDXGISwapChain1.ResizeBuffers(
                // 2,
                // 0,
                // 0,
                // DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                // 0
                // );
                if (sz.Width != 0 && sz.Height != 0)
                {
                    hr = m_pDXGISwapChain1.ResizeBuffers(
                      2,
                      (uint)sz.Width,
                      (uint)sz.Height,
                      DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                      0
                      );                    
                }
                ConfigureSwapChain();
            }
            return (hr);
        }

        public HRESULT CreateDeviceContext()
        {
            HRESULT hr = HRESULT.S_OK;
            uint creationFlags = (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT;

            // Needs "Enable native code Debugging"
#if DEBUG
            creationFlags |= (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_DEBUG;
#endif

            int[] aD3D_FEATURE_LEVEL = new int[] { (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_1, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0,
                (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_1, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_10_0, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_3,
                (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_2, (int)D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_9_1};

            D3D_FEATURE_LEVEL featureLevel;
            hr = D2DTools.D3D11CreateDevice(null,    // specify null to use the default adapter
                D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
                IntPtr.Zero,
                creationFlags,              // optionally set debug and Direct2D compatibility flags
                                            //pD3D_FEATURE_LEVEL,              // list of feature levels this app can support
                aD3D_FEATURE_LEVEL,
                //(uint)Marshal.SizeOf(aD3D_FEATURE_LEVEL),   // number of possible feature levels
                (uint)aD3D_FEATURE_LEVEL.Length,
                D2DTools.D3D11_SDK_VERSION,
                out m_pD3D11DevicePtr,                    // returns the Direct3D device created
                out featureLevel,            // returns feature level of device created
                                             //out pD3D11DeviceContextPtr                    // returns the device immediate context
                out m_pD3D11DeviceContext
            );
            if (hr == HRESULT.S_OK)
            {
                //m_pD3D11DeviceContext = Marshal.GetObjectForIUnknown(pD3D11DeviceContextPtr) as ID3D11DeviceContext;             

                m_pDXGIDevice = Marshal.GetObjectForIUnknown(m_pD3D11DevicePtr) as IDXGIDevice1;
                if (m_pD2DFactory1 != null)
                {
                    hr = m_pD2DFactory1.CreateDevice(m_pDXGIDevice, out m_pD2DDevice);
                    if (hr == HRESULT.S_OK)
                    {
                        hr = m_pD2DDevice.CreateDeviceContext(D2D1_DEVICE_CONTEXT_OPTIONS.D2D1_DEVICE_CONTEXT_OPTIONS_NONE, out m_pD2DDeviceContext);
                        SafeRelease(ref m_pD2DDevice);
                    }
                }                
                // Marshal.Release(m_pD3D11DevicePtr);
            }
            return hr;
        }

        HRESULT CreateSwapChain(IntPtr hWnd)
        {
            HRESULT hr = HRESULT.S_OK;
            DXGI_SWAP_CHAIN_DESC1 swapChainDesc = new DXGI_SWAP_CHAIN_DESC1();
            swapChainDesc.Width = 1;
            swapChainDesc.Height = 1;
            swapChainDesc.Format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM; // this is the most common swapchain format
            swapChainDesc.Stereo = false;
            swapChainDesc.SampleDesc.Count = 1;                // don't use multi-sampling
            swapChainDesc.SampleDesc.Quality = 0;
            swapChainDesc.BufferUsage = D2DTools.DXGI_USAGE_RENDER_TARGET_OUTPUT;
            swapChainDesc.BufferCount = 2;                     // use double buffering to enable flip
            swapChainDesc.Scaling = (hWnd != IntPtr.Zero) ? DXGI_SCALING.DXGI_SCALING_NONE : DXGI_SCALING.DXGI_SCALING_STRETCH;
            swapChainDesc.SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL; // all apps must use this SwapEffect       
            swapChainDesc.Flags = 0;

            IDXGIAdapter pDXGIAdapter;
            hr = m_pDXGIDevice.GetAdapter(out pDXGIAdapter);
            if (hr == HRESULT.S_OK)
            {
                IntPtr pDXGIFactory2Ptr;
                hr = pDXGIAdapter.GetParent(typeof(IDXGIFactory2).GUID, out pDXGIFactory2Ptr);
                if (hr == HRESULT.S_OK)
                {
                    IDXGIFactory2 pDXGIFactory2 = Marshal.GetObjectForIUnknown(pDXGIFactory2Ptr) as IDXGIFactory2;
                    if (hWnd != IntPtr.Zero)
                        hr = pDXGIFactory2.CreateSwapChainForHwnd(m_pD3D11DevicePtr, hWnd, ref swapChainDesc, IntPtr.Zero, null, out m_pDXGISwapChain1);
                    else
                        hr = pDXGIFactory2.CreateSwapChainForComposition(m_pD3D11DevicePtr, ref swapChainDesc, null, out m_pDXGISwapChain1);

                    hr = m_pDXGIDevice.SetMaximumFrameLatency(1);
                    SafeRelease(ref pDXGIFactory2);
                    Marshal.Release(pDXGIFactory2Ptr);
                }
                SafeRelease(ref pDXGIAdapter);
            }
            return hr;
        }

        HRESULT ConfigureSwapChain()
        {
            HRESULT hr = HRESULT.S_OK;

            //IntPtr pD3D11Texture2DPtr = IntPtr.Zero;
            //hr = m_pDXGISwapChain1.GetBuffer(0, typeof(ID3D11Texture2D).GUID, ref pD3D11Texture2DPtr);
            //m_pD3D11Texture2D = Marshal.GetObjectForIUnknown(pD3D11Texture2DPtr) as ID3D11Texture2D;

            D2D1_BITMAP_PROPERTIES1 bitmapProperties = new D2D1_BITMAP_PROPERTIES1();
            bitmapProperties.bitmapOptions = D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_TARGET | D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_CANNOT_DRAW;
            bitmapProperties.pixelFormat = D2DTools.PixelFormat(DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_IGNORE);
            //float nDpiX, nDpiY = 0.0f;
            //m_pD2DContext.GetDpi(out nDpiX, out nDpiY);
            uint nDPI = GetDpiForWindow(hWndMain);
            bitmapProperties.dpiX = nDPI;
            bitmapProperties.dpiY = nDPI;

            IntPtr pDXGISurfacePtr = IntPtr.Zero;
            hr = m_pDXGISwapChain1.GetBuffer(0, typeof(IDXGISurface).GUID, out pDXGISurfacePtr);
            if (hr == HRESULT.S_OK)
            {
                IDXGISurface pDXGISurface = Marshal.GetObjectForIUnknown(pDXGISurfacePtr) as IDXGISurface;
                hr = m_pD2DDeviceContext.CreateBitmapFromDxgiSurface(pDXGISurface, ref bitmapProperties, out m_pD2DTargetBitmap);
                if (hr == HRESULT.S_OK)
                {
                    m_pD2DDeviceContext.SetTarget(m_pD2DTargetBitmap);
                }
                SafeRelease(ref pDXGISurface);
                Marshal.Release(pDXGISurfacePtr);
            }
            return hr;
        }

        HRESULT CreateDeviceResources()
        {
            HRESULT hr = HRESULT.S_OK;
            if (m_pD2DDeviceContext != null)
            {
                if (m_pMainBrush == null)
                    hr = m_pD2DDeviceContext.CreateSolidColorBrush(new ColorF(ColorF.Enum.Red), BrushProperties(), out m_pMainBrush);
                if (m_pD2DBitmapBackground == null)
                    hr = CreateD2DBitmapFromURL("https://i.ibb.co/2MtgC8C/clouds-country-daylight-371633.jpg", out m_pD2DBitmapBackground);
                if (m_pD2DBitmap == null)
                    hr = CreateD2DBitmapFromURL("https://i.ibb.co/QCBKBjD/Flying-bird.png", out m_pD2DBitmap);
                if (m_pD2DBitmap1 == null)
                    hr = CreateD2DBitmapFromURL("https://i.ibb.co/VgVp09Y/butterfly-sprite-sheet-blue.png", out m_pD2DBitmap1);

                if (m_pD2DDeviceContext3 == null)
                    m_pD2DDeviceContext3 = (ID2D1DeviceContext3)m_pD2DDeviceContext;
            }
            return hr;
        }

        private void CreateSprites()
        {
            if (m_pD2DBitmap != null)
            {
                if (spriteBird == null)
                    spriteBird = new CSprite(m_pD2DDeviceContext3, m_pD2DBitmap, 5, 4);
            }

            if (CSprites.Count == 0)
            {
                if (m_pD2DBitmap1 != null)
                {
                    for (int i = 0; i < nNbButterflyInitial; i++)
                    {
                        AddButterfly(m_pD2DBitmap1, 13, 7, 87);
                    }
                }
            }
            if (spriteBird != null && CSprites.Count > 0)
                g_bSpritesCreated = true;
        }

        private HRESULT CreateD2DBitmapFromURL(string sURL, out ID2D1Bitmap pD2DBitmap)
        {
            HRESULT hr = HRESULT.S_OK;
            pD2DBitmap = null;
            byte[] bytes = null;
            try
            {
                System.Net.ServicePointManager.Expect100Continue = true;
                System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
                System.Net.HttpWebRequest webRequest = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(sURL);
                webRequest.AllowWriteStreamBuffering = true;
                using (System.Net.WebResponse webResponse = webRequest.GetResponse())
                {
                    System.IO.Stream stream = webResponse.GetResponseStream();
                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
                    {
                        stream.CopyTo(ms);
                        bytes = ms.ToArray();
                    }
                }
            }
            catch (Exception)
            {
                return HRESULT.E_FAIL;
            }

            IWICStream wicStream = null;
            hr = (HRESULT)m_pWICImagingFactory.CreateStream(out wicStream);
            if (hr == HRESULT.S_OK)
            {
                hr = (HRESULT)wicStream.InitializeFromMemory(bytes, bytes.Length);
                if (hr == HRESULT.S_OK)
                {
                    IWICBitmapDecoder pDecoder = null;
                    hr = (HRESULT)m_pWICImagingFactory.CreateDecoderFromStream(wicStream, Guid.Empty, WICDecodeOptions.WICDecodeMetadataCacheOnDemand, out pDecoder);
                    if (hr == HRESULT.S_OK)
                    {
                        IWICBitmapFrameDecode pFrame = null;
                        hr = (HRESULT)pDecoder.GetFrame(0, out pFrame);
                        if (hr == HRESULT.S_OK)
                        {
                            IWICFormatConverter pConvertedSourceBitmap = null;
                            hr = (HRESULT)m_pWICImagingFactory.CreateFormatConverter(out pConvertedSourceBitmap);
                            if (hr == HRESULT.S_OK)
                            {
                                hr = (HRESULT)pConvertedSourceBitmap.Initialize(
                                    (IWICBitmapSource)pFrame,        // Input bitmap to convert
                                    WICTools.GUID_WICPixelFormat32bppPBGRA,   // Destination pixel format
                                    WICBitmapDitherType.WICBitmapDitherTypeNone,         // Specified dither pattern
                                    null,                            // Specify a particular palette 
                                    0,                             // Alpha threshold
                                    WICBitmapPaletteType.WICBitmapPaletteTypeCustom       // Palette translation type
                                    );
                                if (hr == HRESULT.S_OK)
                                {
                                    D2D1_BITMAP_PROPERTIES bitmapproperties = new D2D1_BITMAP_PROPERTIES();
                                    hr = m_pD2DDeviceContext.CreateBitmapFromWicBitmap(pConvertedSourceBitmap, ref bitmapproperties, out pD2DBitmap);
                                }
                                SafeRelease(ref pConvertedSourceBitmap);
                            }
                            SafeRelease(ref pFrame);
                        }
                        SafeRelease(ref pDecoder);
                    }
                }
            }
            return hr;
        }

        void CleanDeviceResources()
        {
            SafeRelease(ref m_pD2DBitmap);
            SafeRelease(ref m_pD2DBitmap1);
            SafeRelease(ref m_pD2DBitmapBackground);
            SafeRelease(ref m_pMainBrush);

            if (spriteBird != null)
                spriteBird.Dispose();

             foreach (CSprite s in CSprites)
                s.Dispose(); 
        }

        void Clean()
        {            
            SafeRelease(ref m_pD2DDeviceContext);
            SafeRelease(ref m_pD2DDeviceContext3);

            CleanDeviceResources();

            SafeRelease(ref m_pD2DTargetBitmap);
            SafeRelease(ref m_pDXGISwapChain1);
           
            SafeRelease(ref m_pD3D11DeviceContext);
            if (m_pD3D11DevicePtr != IntPtr.Zero)
                Marshal.Release(m_pD3D11DevicePtr);
            SafeRelease(ref m_pDXGIDevice);

            SafeRelease(ref m_pWICImagingFactory);
            SafeRelease(ref m_pD2DFactory1);
            SafeRelease(ref m_pD2DFactory);
        }
    }
}
