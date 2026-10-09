using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace SnesGFX
{
    public static class HdmaEngine
    {
        public struct HdmaEntry
        {
            public int Scanlines;
            public Color Color;
        }

        public static List<HdmaEntry> RipGradient(Bitmap bmp, bool grayscale, bool luminosity, bool negative, bool sepia)
        {
            List<HdmaEntry> entries = new List<HdmaEntry>();
            if (bmp == null) return entries;

            int height = bmp.Height;
            Color lastColor = Color.Empty;
            int count = 0;

            for (int y = 0; y < height; y++)
            {
                Color c = bmp.GetPixel(0, y);
                
                // Apply filters
                if (negative) c = Color.FromArgb(255 - c.R, 255 - c.G, 255 - c.B);
                if (grayscale)
                {
                    int gray = luminosity ? (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114) : (c.R + c.G + c.B) / 3;
                    c = Color.FromArgb(gray, gray, gray);
                }
                if (sepia)
                {
                    int r = (int)(c.R * 0.393 + c.G * 0.769 + c.B * 0.189);
                    int g = (int)(c.R * 0.349 + c.G * 0.686 + c.B * 0.168);
                    int b = (int)(c.R * 0.272 + c.G * 0.534 + c.B * 0.131);
                    c = Color.FromArgb(Math.Min(255, r), Math.Min(255, g), Math.Min(255, b));
                }

                if (c == lastColor && count < 0x7F)
                {
                    count++;
                }
                else
                {
                    if (count > 0)
                    {
                        entries.Add(new HdmaEntry { Scanlines = count, Color = lastColor });
                    }
                    lastColor = c;
                    count = 1;
                }
            }

            if (count > 0)
            {
                entries.Add(new HdmaEntry { Scanlines = count, Color = lastColor });
            }

            return entries;
        }

        public static ushort ToSnesColor(Color c)
        {
            int r = c.R >> 3;
            int g = c.G >> 3;
            int b = c.B >> 3;
            return (ushort)(r | (g << 5) | (b << 10));
        }

        public static string GenerateColdataTable(List<HdmaEntry> entries, bool compress)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(".TableColdata");

            foreach (var entry in entries)
            {
                int r = entry.Color.R >> 3;
                int g = entry.Color.G >> 3;
                int b = entry.Color.B >> 3;

                // Format: bgrccccc
                // We generate 3 bytes if we want to set all planes
                // But the screenshot shows 2? 
                // db $scanlines, $red_val, $green_val, $blue_val
                // Red: 0x20 | r, Green: 0x40 | g, Blue: 0x80 | b
                sb.AppendLine(string.Format("    db ${0:X2}, ${1:X2}, ${2:X2}, ${3:X2}", 
                    entry.Scanlines, 
                    0x20 | r, 
                    0x40 | g, 
                    0x80 | b));
            }
            sb.AppendLine("    db $00");
            return sb.ToString();
        }

        public static string GeneratePaletteTable(List<HdmaEntry> entries, int destination, bool compress)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(".TablePalette");

            foreach (var entry in entries)
            {
                ushort snesColor = ToSnesColor(entry.Color);
                // Format: db $scanlines : dw $palette_index, $snes_color
                sb.AppendLine(string.Format("    db ${0:X2} : dw ${1:X4}, ${2:X4}", 
                    entry.Scanlines, 
                    destination, 
                    snesColor));
            }
            sb.AppendLine("    db $00");
            return sb.ToString();
        }
    }
}
