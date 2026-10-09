using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using SnesGFX.SNES;

namespace SnesGFX
{
    public static class GfxEngine
    {
        /// <summary>
        /// Renders raw SNES GFX data to a Bitmap for preview purposes.
        /// </summary>
        public static byte[] BitmapToIndices(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            byte[] indices = new byte[width * height];

            if (bmp.PixelFormat == PixelFormat.Format8bppIndexed)
            {
                var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
                System.Runtime.InteropServices.Marshal.Copy(data.Scan0, indices, 0, indices.Length);
                bmp.UnlockBits(data);
            }
            else
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        indices[y * width + x] = bmp.GetPixel(x, y).R;
                    }
                }
            }
            return indices;
        }

        public static Bitmap RenderGfx(byte[] gfxData, Color[] palette, int formatIndex, int width = 128)
        {
            if (gfxData == null || gfxData.Length == 0) return null;
            if (palette == null) palette = CreateDefaultPalette();

            try
            {
                // Decode SNES bitplanes to raw 8-bit indices
                byte[] rawIndices = SnesGFX.AvaiableFormats[formatIndex].Decode(gfxData);
                
                int height = (rawIndices.Length + width - 1) / width;
                if (height <= 0) height = 8;

                Bitmap bmp = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
                ColorPalette cp = bmp.Palette;
                for (int i = 0; i < 256; i++)
                {
                    if (i < palette.Length) cp.Entries[i] = palette[i];
                    else cp.Entries[i] = Color.Black;
                }
                bmp.Palette = cp;

                BitmapData data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
                
                // Copy indices, handling potential size mismatch
                int length = Math.Min(rawIndices.Length, data.Stride * height);
                Marshal.Copy(rawIndices, 0, data.Scan0, length);
                
                bmp.UnlockBits(data);
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private static Color[] CreateDefaultPalette()
        {
            Color[] pal = new Color[256];
            for (int i = 0; i < 256; i++) pal[i] = Color.FromArgb(i, i, i);
            return pal;
        }

        /// <summary>
        /// Performs "Smart" BPP conversion by re-quantizing the graphics using a reference palette.
        /// </summary>
        public static byte[] SmartConvertBpp(byte[] inputGfx, Color[] sourcePalette, int inFormatIndex, int outFormatIndex, QuantizerOptions options, out Color[] outPalette)
        {
            // 1. Decode to raw indices
            byte[] rawIndices = SnesGFX.AvaiableFormats[inFormatIndex].Decode(inputGfx);
            
            // 2. Map indices to RGB colors
            int width = 128; // Standard SNES GFX width
            int height = (rawIndices.Length + width - 1) / width;
            byte[] rgbaData = new byte[width * height * 4];
            
            for (int i = 0; i < rawIndices.Length; i++)
            {
                Color c = sourcePalette[rawIndices[i]];
                rgbaData[i * 4 + 0] = c.R;
                rgbaData[i * 4 + 1] = c.G;
                rgbaData[i * 4 + 2] = c.B;
                rgbaData[i * 4 + 3] = 255;
            }

            // 3. Run Quantizer
            var quantizer = new TilePaletteQuantizer(options);
            var result = quantizer.Quantize(rgbaData, width, height);

            // 4. Encode to target format
            byte[] quantizedIndices = result.ColorIndexes;
            byte[] finalGfx = SnesGFX.AvaiableFormats[outFormatIndex].Encode(quantizedIndices);

            // 5. Build output palette
            outPalette = ConvertQuantizedPalette(result.Palettes, options);

            return finalGfx;
        }

        private static Color[] ConvertQuantizedPalette(double[][][] palettes, QuantizerOptions opts)
        {
            Color[] final = new Color[256];
            for (int i = 0; i < 256; i++) final[i] = Color.Black;

            int colorsPerPal = opts.ColorsPerPalette;
            bool isTransparent = opts.ColorZeroBehaviour == ColorZeroBehaviour.TransparentFromTransparent || opts.ColorZeroBehaviour == ColorZeroBehaviour.TransparentFromColor;

            for (int p = 0; p < palettes.Length; p++)
            {
                if (palettes[p] == null) continue;
                for (int c = 0; c < palettes[p].Length; c++)
                {
                    int targetIdx = p * colorsPerPal + (isTransparent ? c + 1 : c);
                    if (targetIdx >= 256) break;
                    
                    final[targetIdx] = Color.FromArgb(
                        (int)palettes[p][c][0],
                        (int)palettes[p][c][1],
                        (int)palettes[p][c][2]
                    );
                }
            }
            return final;
        }
    }
}
