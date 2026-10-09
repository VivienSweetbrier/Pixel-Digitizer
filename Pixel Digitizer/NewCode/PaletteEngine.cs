using System;
using System.Drawing;
using System.IO;
using System.Collections.Generic;

namespace SnesGFX
{
    public static class PaletteEngine
    {
        public static Color[] LoadPalette(string path)
        {
            if (!File.Exists(path)) return null;
            byte[] data = File.ReadAllBytes(path);

            // Simple heuristics for palette formats
            if (data.Length == 768) // Raw RGB or JASC-PAL without header
            {
                return FromRawRGB(data);
            }
            if (data.Length > 768 && System.Text.Encoding.ASCII.GetString(data, 0, 8) == "JASC-PAL")
            {
                return FromJascPal(path);
            }
            if (data.Length == 512) // SNES RGB15 raw
            {
                return FromSnesRGB15(data);
            }

            return null;
        }

        private static Color[] FromRawRGB(byte[] data)
        {
            Color[] pal = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                if (i * 3 + 2 < data.Length)
                    pal[i] = Color.FromArgb(data[i * 3], data[i * 3 + 1], data[i * 3 + 2]);
                else
                    pal[i] = Color.Black;
            }
            return pal;
        }

        private static Color[] FromSnesRGB15(byte[] data)
        {
            Color[] pal = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                int c = BitConverter.ToUInt16(data, i * 2);
                int r = (c & 0x1F) << 3;
                int g = ((c >> 5) & 0x1F) << 3;
                int b = ((c >> 10) & 0x1F) << 3;
                pal[i] = Color.FromArgb(r, g, b);
            }
            return pal;
        }

        private static Color[] FromJascPal(string path)
        {
            try
            {
                string[] lines = File.ReadAllLines(path);
                if (lines.Length < 4) return null;
                int count = int.Parse(lines[3]);
                Color[] pal = new Color[256];
                for (int i = 0; i < 256; i++) pal[i] = Color.Black;
                
                for (int i = 0; i < count && i + 4 < lines.Length; i++)
                {
                    string[] parts = lines[i + 4].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        pal[i] = Color.FromArgb(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));
                    }
                }
                return pal;
            }
            catch { return null; }
        }
    }
}
