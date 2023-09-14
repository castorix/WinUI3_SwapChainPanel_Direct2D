using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// [Build] check "Unsafe code"
using Direct2D;
using System.Runtime.InteropServices;
using GlobalStructures;
using static GlobalStructures.GlobalTools;

namespace WinUI3_SwapChainPanel_Direct2D
{
    internal class CSprite : IDisposable
    {
        private ID2D1Bitmap m_pBitmap = null;
        private D2D1_RECT_U[] m_pRectSource;
        private D2D1_RECT_F[] m_pRectDest;
        private D2D1_COLOR_F_STRUCT[] m_pColors;
        private D2D1_MATRIX_3X2_F_STRUCT[] m_pTransforms;
        private ID2D1SpriteBatch m_pSpriteBatch = null;
        private uint m_nNbImagesX = 1;
        private uint m_nNbImagesY = 1;
        private uint m_nNbImages = 1;

        public int HORIZONTALFLIP_RIGHT = 1;
        public int HORIZONTALFLIP_LEFT = 2;

        public int Width = 0;
        public int Height = 0;

        private uint m_nCurrentIndex = 0;
        public uint CurrentIndex
        {
            get => m_nCurrentIndex;
            set
            {
                m_nCurrentIndex = value;
                if (m_nCurrentIndex >= m_nNbImages)
                    m_nCurrentIndex = 0;
            }
        }

        private float m_nStepX = 1;
        public float StepX
        {
            get => m_nStepX;
            set => m_nStepX = value;
        }

        private float m_nStepY = 1;
        public float StepY
        {
            get => m_nStepY;
            set => m_nStepY = value;
        }

        public float X
        {
            get => m_pRectDest[0].left;
            set => m_pRectDest[0].left = value;
        }

        public float Y
        {
            get => m_pRectDest[0].top;
            set => m_pRectDest[0].top = value;
        }

        public CSprite(ID2D1DeviceContext3 pDC, ID2D1Bitmap pBitmap, uint nNbImagesX, uint nNbImagesY, uint nNbImages = 0, float nStepX = 1, float nStepY = 1, D2D1_COLOR_F color = null, D2D1_MATRIX_3X2_F matrix = null)
        {
            HRESULT hr = HRESULT.S_OK;
            m_pBitmap = pBitmap;
            m_nNbImagesX = nNbImagesX;
            m_nNbImagesY = nNbImagesY;
            m_nNbImages = (nNbImages == 0) ? m_nNbImagesX * m_nNbImagesY : nNbImages;
            hr = pDC.CreateSpriteBatch(out m_pSpriteBatch);
            D2D1_SIZE_F bmpSize = pBitmap.GetSize();

            // Only first pRectDest used in the test...
            m_pRectSource = new D2D1_RECT_U[m_nNbImagesX * m_nNbImagesY];
            m_pRectDest = new D2D1_RECT_F[m_nNbImagesX * m_nNbImagesY];
            m_pTransforms = new D2D1_MATRIX_3X2_F_STRUCT[m_nNbImagesX * m_nNbImagesY];
            for (uint m = 0; m < m_nNbImagesX * m_nNbImagesY; m++)
            {
                if (matrix != null)
                {
                    m_pTransforms[m]._11 = matrix._11;
                    m_pTransforms[m]._12 = matrix._12;
                    m_pTransforms[m]._21 = matrix._21;
                    m_pTransforms[m]._22 = matrix._22;
                    m_pTransforms[m]._31 = matrix._31;
                    m_pTransforms[m]._32 = matrix._32;
                }
            }

            StepX = nStepX;
            StepY = nStepY;
            float nWidth = bmpSize.width / m_nNbImagesX;
            float nHeight = bmpSize.height / m_nNbImagesY;
            Width = (int)nWidth;
            Height = (int)nHeight;
            int n = 0;
            for (uint j = 0; j < m_nNbImagesY; j++)
            {
                for (uint i = 0; i < m_nNbImagesX; i++)
                {
                    m_pRectSource[n] = new D2D1_RECT_U((uint)(i * bmpSize.width / m_nNbImagesX), (uint)(j * bmpSize.height / m_nNbImagesY), (uint)((i * bmpSize.width / m_nNbImagesX) + (uint)bmpSize.width / m_nNbImagesX), (uint)((j * bmpSize.height / m_nNbImagesY) + (uint)bmpSize.height / m_nNbImagesY));
                    m_pRectDest[n] = new D2D1_RECT_F((float)(i * nWidth), (float)(j * nHeight), (float)((i * nWidth) + (float)nWidth), (uint)((j * nHeight) + (float)nHeight));
                    n++;
                }
            }
            m_pColors = new D2D1_COLOR_F_STRUCT[m_nNbImagesX * m_nNbImagesY];
            for (uint c = 0; c < m_nNbImagesX * m_nNbImagesY; c++)
            {               
                if (color != null)
                {
                    m_pColors[c].a = color.a;
                    m_pColors[c].r = color.r;
                    m_pColors[c].g = color.g;
                    m_pColors[c].b = color.b;
                }
            }
            hr = m_pSpriteBatch.AddSprites(m_nNbImagesX * m_nNbImagesY, m_pRectDest, m_pRectSource, color == null ? null : m_pColors, matrix == null ? null : m_pTransforms, 0, (uint)Marshal.SizeOf(typeof(D2D1_RECT_U)), 0, (uint)Marshal.SizeOf(typeof(D2D1_MATRIX_3X2_F)));
        }

        public void Draw(ID2D1DeviceContext3 pDC, uint nSpriteIndex, uint nSpriteCount, bool bIncrement)
        {
            pDC.DrawSpriteBatch(m_pSpriteBatch, nSpriteIndex, nSpriteCount, m_pBitmap);
        }

        public void Move(ID2D1DeviceContext3 pDC, int nHorizontalFlip, bool bBounce)
        {
            HRESULT hr = HRESULT.S_OK;
            D2D1_SIZE_F size = pDC.GetSize();
            D2D1_SIZE_F bmpSize = m_pBitmap.GetSize();

            float nWidth = bmpSize.width / m_nNbImagesX;
            float nHeight = bmpSize.height / m_nNbImagesY;
            if (m_pTransforms[0]._11 != 0)
            {
                //nWidth *= 1/m_pTransforms[0]._11;
                size.width *= 1 / m_pTransforms[0]._11;
            }
            if (m_pTransforms[0]._22 != 0)
            {
                //nHeight *= 1/m_pTransforms[0]._22;
                size.height *= 1 / m_pTransforms[0]._22;
            }

            m_pRectDest[0].right = m_pRectDest[0].left + nWidth;
            m_pRectDest[0].bottom = m_pRectDest[0].top + nHeight;

            // Tests to bounce the sprite
            if (bBounce)
            {
                if (m_pRectDest[0].left >= size.width - nWidth)
                {
                    m_nStepX = -Math.Abs(m_nStepX);
                    m_pRectDest[0].left = size.width - nWidth;
                }
                if (m_pRectDest[0].top >= size.height - nHeight)
                {
                    m_nStepY = -Math.Abs(m_nStepY);
                    m_pRectDest[0].top = size.height - nHeight;
                }
                if (m_pRectDest[0].left <= 0)
                {
                    m_nStepX = Math.Abs(m_nStepX);
                    m_pRectDest[0].left = 0;
                }
                if (m_pRectDest[0].top <= 0)
                {
                    m_nStepY = Math.Abs(m_nStepY);
                    m_pRectDest[0].top = 0;
                }
            }

            if (nHorizontalFlip == HORIZONTALFLIP_RIGHT || nHorizontalFlip == HORIZONTALFLIP_LEFT)
            {
                if (m_nStepX >= 0 && nHorizontalFlip == HORIZONTALFLIP_RIGHT || m_nStepX < 0 && nHorizontalFlip == HORIZONTALFLIP_LEFT)
                {
                    int n = 0;
                    for (uint j = 0; j < m_nNbImagesY; j++)
                    {
                        for (uint i = 0; i < m_nNbImagesX; i++)
                        {
                            m_pRectSource[n] = new D2D1_RECT_U((uint)((i * bmpSize.width / m_nNbImagesX) + (uint)bmpSize.width / m_nNbImagesX), (uint)(j * bmpSize.height / m_nNbImagesY), (uint)(i * bmpSize.width / m_nNbImagesX), (uint)((j * bmpSize.height / m_nNbImagesY) + (uint)bmpSize.height / m_nNbImagesY));
                            n++;
                        }
                    }
                }
                else
                {
                    int n = 0;
                    for (uint j = 0; j < m_nNbImagesY; j++)
                    {
                        for (uint i = 0; i < m_nNbImagesX; i++)
                        {
                            m_pRectSource[n] = new D2D1_RECT_U((uint)(i * bmpSize.width / m_nNbImagesX), (uint)(j * bmpSize.height / m_nNbImagesY), (uint)((i * bmpSize.width / m_nNbImagesX) + (uint)bmpSize.width / m_nNbImagesX), (uint)((j * bmpSize.height / m_nNbImagesY) + (uint)bmpSize.height / m_nNbImagesY));
                            n++;
                        }
                    }
                }
            }
            hr = m_pSpriteBatch.SetSprites(0, m_nNbImagesX * m_nNbImagesY, m_pRectDest, m_pRectSource, null, null, 0, (uint)Marshal.SizeOf(typeof(D2D1_RECT_U)), 0, (uint)Marshal.SizeOf(typeof(D2D1_MATRIX_3X2_F)));
        }

        public void Dispose()
        {
            m_pSpriteBatch.Clear();            
            SafeRelease(ref m_pSpriteBatch);            
        }
    }
}
