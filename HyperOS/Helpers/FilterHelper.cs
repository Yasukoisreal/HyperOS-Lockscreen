using System;
using System.Windows.Media.Imaging;

namespace HyperOS.Helpers
{
    public static class FilterHelper
    {
        /// <summary>
        /// Applies a fast Box Blur to the pixel array. Multiple iterations simulate Gaussian Blur.
        /// </summary>
        public static int[] ApplyBoxBlur(int[] srcPixels, int w, int h, int radius, int iterations = 3)
        {
            if (radius < 1) return srcPixels;
            
            int[] destPixels = new int[w * h];
            
            // Clone first
            Array.Copy(srcPixels, destPixels, srcPixels.Length);

            int[] temp = new int[w * h];

            for (int i = 0; i < iterations; i++)
            {
                BlurHorizontal(destPixels, temp, w, h, radius);
                BlurVertical(temp, destPixels, w, h, radius);
            }

            return destPixels;
        }

        private static void BlurHorizontal(int[] source, int[] dest, int w, int h, int radius)
        {
            float inv = 1.0f / (radius * 2 + 1);
            for (int y = 0; y < h; y++)
            {
                int outIndex = y * w;
                int inIndex = outIndex;
                
                int r = 0, g = 0, b = 0;
                
                // Initialize the window
                for (int i = -radius; i <= radius; i++)
                {
                    int x = Math.Max(0, Math.Min(i, w - 1));
                    int p = source[inIndex + x];
                    r += (p >> 16) & 0xFF;
                    g += (p >> 8) & 0xFF;
                    b += p & 0xFF;
                }
                
                for (int x = 0; x < w; x++)
                {
                    dest[outIndex++] = unchecked((int)0xFF000000) | (((int)(r * inv)) << 16) | (((int)(g * inv)) << 8) | ((int)(b * inv));
                    
                    int nextX = Math.Min(x + radius + 1, w - 1);
                    int prevX = Math.Max(x - radius, 0);
                    
                    int pNext = source[inIndex + nextX];
                    int pPrev = source[inIndex + prevX];
                    
                    r += ((pNext >> 16) & 0xFF) - ((pPrev >> 16) & 0xFF);
                    g += ((pNext >> 8) & 0xFF) - ((pPrev >> 8) & 0xFF);
                    b += (pNext & 0xFF) - (pPrev & 0xFF);
                }
            }
        }

        private static void BlurVertical(int[] source, int[] dest, int w, int h, int radius)
        {
            float inv = 1.0f / (radius * 2 + 1);
            for (int x = 0; x < w; x++)
            {
                int outIndex = x;
                int inIndex = x;
                
                int r = 0, g = 0, b = 0;
                
                for (int i = -radius; i <= radius; i++)
                {
                    int y = Math.Max(0, Math.Min(i, h - 1));
                    int p = source[inIndex + y * w];
                    r += (p >> 16) & 0xFF;
                    g += (p >> 8) & 0xFF;
                    b += p & 0xFF;
                }
                
                for (int y = 0; y < h; y++)
                {
                    dest[outIndex] = unchecked((int)0xFF000000) | (((int)(r * inv)) << 16) | (((int)(g * inv)) << 8) | ((int)(b * inv));
                    outIndex += w;
                    
                    int nextY = Math.Min(y + radius + 1, h - 1);
                    int prevY = Math.Max(y - radius, 0);
                    
                    int pNext = source[inIndex + nextY * w];
                    int pPrev = source[inIndex + prevY * w];
                    
                    r += ((pNext >> 16) & 0xFF) - ((pPrev >> 16) & 0xFF);
                    g += ((pNext >> 8) & 0xFF) - ((pPrev >> 8) & 0xFF);
                    b += (pNext & 0xFF) - (pPrev & 0xFF);
                }
            }
        }

        /// <summary>
        /// Simulates fluted (ribbed) glass using cylindrical refraction and 3D edge lighting.
        /// </summary>
        public static int[] ApplyRibbedFilter(int[] srcPixels, int w, int h, int stripWidth = 0)
        {
            // Auto-calculate strip width to have about 12 strips across the screen
            if (stripWidth <= 0) stripWidth = Math.Max(20, w / 12);
            
            int[] d = new int[w * h];
            
            // Precompute wave values for each column to save time
            int[] dx = new int[w];
            float[] light = new float[w];
            for (int x = 0; x < w; x++)
            {
                int stripIndex = x / stripWidth;
                int localX = x % stripWidth;
                
                // Normalized position within the strip: -1.0 to 1.0
                float nx = (localX - (stripWidth - 1) / 2.0f) / ((stripWidth - 1) / 2.0f);
                
                // Medium Refraction: simulate a standard cylindrical lens
                float center = stripIndex * stripWidth + stripWidth / 2.0f;
                float sampleOffset = Math.Sign(nx) * (float)Math.Pow(Math.Abs(nx), 0.8) * (stripWidth * 0.85f);
                dx[x] = (int)(center + sampleOffset) - x;
                
                // 3D Lighting (Highlights and Shadows)
                if (localX == 0) light[x] = 1.4f;
                else if (localX == 1) light[x] = 1.2f;
                else if (localX == stripWidth - 2) light[x] = 0.8f;
                else if (localX == stripWidth - 1) light[x] = 0.6f;
                else light[x] = 1.0f - nx * 0.15f; // Stronger gradient across the cylinder
            }

            for (int y = 0; y < h; y++)
            {
                int yOffset = y * w;
                for (int x = 0; x < w; x++)
                {
                    int sourceX = x + dx[x];
                    
                    // Mirror wrap for out-of-bounds to prevent edge bleeding
                    if (sourceX < 0) sourceX = -sourceX;
                    if (sourceX >= w) sourceX = w - 1 - (sourceX - w + 1);
                    // Double check clamp
                    if (sourceX < 0) sourceX = 0;
                    if (sourceX >= w) sourceX = w - 1;
                    
                    int p = srcPixels[yOffset + sourceX];
                    float l = light[x];
                    
                    int r = Math.Min(255, (int)(((p >> 16) & 0xFF) * l));
                    int g = Math.Min(255, (int)(((p >> 8) & 0xFF) * l));
                    int b = Math.Min(255, (int)((p & 0xFF) * l));
                    
                    d[yOffset + x] = unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;
                }
            }
            return d;
        }
    }
}
