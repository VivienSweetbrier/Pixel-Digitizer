using System;
using System.Collections.Generic;

namespace SnesGFX.SNES
{
    // ======================== ENUMS ========================

    public enum ColorZeroBehaviour
    {
        Unique = 0,
        Shared = 1,
        TransparentFromTransparent = 2,
        TransparentFromColor = 3
    }

    public enum DitherMode
    {
        Off = 0,
        Fast = 1,
        Slow = 2
    }

    public enum DitherPatternType
    {
        Diagonal4 = 0,
        Horizontal4 = 1,
        Vertical4 = 2,
        Diagonal2 = 3,
        Horizontal2 = 4,
        Vertical2 = 5
    }

    // ======================== OPTIONS ========================

    public class QuantizerOptions
    {
        public int TileWidth = 8;
        public int TileHeight = 8;
        public int NumPalettes = 8;
        public int ColorsPerPalette = 4;
        public int BitsPerChannel = 5;
        public double FractionOfPixels = 0.1;
        public ColorZeroBehaviour ColorZeroBehaviour = ColorZeroBehaviour.Unique;
        public double[] ColorZeroValue = { 0, 0, 0 };
        public DitherMode Dither = DitherMode.Off;
        public double DitherWeight = 0.5;
        public DitherPatternType DitherPattern = DitherPatternType.Diagonal4;
    }

    // ======================== RESULT ========================

    /// <summary>
    /// Output of the tile palette quantizer.
    /// Palettes[p][c] = { r, g, b } in 0-255 range.
    /// ColorIndexes[y * Width + x] = packed index (paletteIndex * ColorsPerPalette + colorIndex [+ 1 if transparent mode]).
    /// </summary>
    public class QuantizedResult
    {
        public double[][][] Palettes;
        public int Width;
        public int Height;
        public byte[] ColorIndexes;
    }

    // ======================== QUANTIZER ========================

    /// <summary>
    /// Native C# port of the tiledpalettequant quantizer-core.js.
    /// Performs tile-aware palette quantization for SNES graphics.
    /// </summary>
    public class TilePaletteQuantizer
    {
        // -- Internal data types --
        private class QTile
        {
            public List<double[]> Colors = new List<double[]>();
            public List<int> Counts = new List<int>();
            public List<QPixel> Pixels = new List<QPixel>();
        }

        private class QPixel
        {
            public QTile Tile;
            public double[] Color;
            public int X, Y;
        }

        private struct DitherCandidate
        {
            public int ColorIndex;
            public double ColorDistance;
            public double[] ComparedColor;
            public double Brightness;
        }

        // Fisher-Yates shuffle that visits every index once per epoch before reshuffling.
        private class RandomShuffle
        {
            private readonly int[] _values;
            private int _idx;
            private readonly Random _rng;

            public RandomShuffle(int n, Random rng)
            {
                _rng = rng;
                _values = new int[n];
                for (int i = 0; i < n; i++) _values[i] = i;
                _idx = n - 1; // first Next() triggers shuffle
            }

            private void Shuffle()
            {
                for (int i = 0; i < _values.Length; i++)
                {
                    int j = i + _rng.Next(_values.Length - i);
                    int tmp = _values[i]; _values[i] = _values[j]; _values[j] = tmp;
                }
            }

            public int Next()
            {
                _idx++;
                if (_idx >= _values.Length) { Shuffle(); _idx = 0; }
                return _values[_idx];
            }
        }

        // -- Constants --
        private static readonly double[] AlphaValues = { 0, 255, 85, 36.42857, 17, 8.22581, 4.04762, 2.00787, 1 };
        private static readonly double[] BrightnessScale = { 0.299, 0.587, 0.114 };

        private static readonly Dictionary<DitherPatternType, int[,]> DitherPatterns =
            new Dictionary<DitherPatternType, int[,]>
        {
            { DitherPatternType.Diagonal4,   new int[,] { {0,2}, {3,1} } },
            { DitherPatternType.Horizontal4, new int[,] { {0,3}, {1,2} } },
            { DitherPatternType.Vertical4,   new int[,] { {0,1}, {3,2} } },
            { DitherPatternType.Diagonal2,   new int[,] { {0,1}, {1,0} } },
            { DitherPatternType.Horizontal2, new int[,] { {0,1}, {0,1} } },
            { DitherPatternType.Vertical2,   new int[,] { {0,0}, {1,1} } },
        };

        // -- Fields --
        private readonly QuantizerOptions _opts;
        private readonly int[,] _ditherPattern;
        private readonly int _ditherPixels;
        private readonly Random _rng = new Random();

        // -- Constructor --
        public TilePaletteQuantizer(QuantizerOptions options)
        {
            _opts = options;
            _ditherPattern = DitherPatterns[options.DitherPattern];
            bool is2 = options.DitherPattern == DitherPatternType.Diagonal2 ||
                        options.DitherPattern == DitherPatternType.Horizontal2 ||
                        options.DitherPattern == DitherPatternType.Vertical2;
            _ditherPixels = is2 ? 2 : 4;
        }

        // ======================== COLOR MATH ========================

        private static double[] CloneColor(double[] c)
        {
            return new double[] { c[0], c[1], c[2] };
        }

        private static void CopyColor(double[] dest, double[] src)
        {
            dest[0] = src[0]; dest[1] = src[1]; dest[2] = src[2];
        }

        private static void AddColor(double[] c1, double[] c2)
        {
            c1[0] += c2[0]; c1[1] += c2[1]; c1[2] += c2[2];
        }

        private static void SubtractColor(double[] c1, double[] c2)
        {
            c1[0] -= c2[0]; c1[1] -= c2[1]; c1[2] -= c2[2];
        }

        private static void ScaleColor(double[] c, double s)
        {
            c[0] *= s; c[1] *= s; c[2] *= s;
        }

        private static void ClampColor(double[] c, double min, double max)
        {
            c[0] = Math.Max(min, Math.Min(max, c[0]));
            c[1] = Math.Max(min, Math.Min(max, c[1]));
            c[2] = Math.Max(min, Math.Min(max, c[2]));
        }

        private static double ToNbit(double value, int n)
        {
            double alpha = AlphaValues[n];
            return Math.Round(Math.Round(value / alpha, MidpointRounding.AwayFromZero) * alpha,
                              MidpointRounding.AwayFromZero);
        }

        private static void ToNbitColor(double[] c, int n)
        {
            c[0] = ToNbit(c[0], n);
            c[1] = ToNbit(c[1], n);
            c[2] = ToNbit(c[2], n);
        }

        private static double ToLinear(double x) { return x * x; }

        private static void ToLinearColor(double[] c)
        {
            c[0] = ToLinear(c[0]); c[1] = ToLinear(c[1]); c[2] = ToLinear(c[2]);
        }

        private static double ToSrgb(double x) { return Math.Sqrt(x); }

        private static void ToSrgbColor(double[] c)
        {
            c[0] = ToSrgb(c[0]); c[1] = ToSrgb(c[1]); c[2] = ToSrgb(c[2]);
        }

        private static double Brightness(double[] c)
        {
            double sum = 0;
            for (int i = 0; i < 3; i++) sum += BrightnessScale[i] * ToLinear(c[i]);
            return sum;
        }

        private static void MoveColorCloser(double[] c, double[] target, double alpha)
        {
            double inv = 1.0 - alpha;
            c[0] = inv * c[0] + alpha * target[0];
            c[1] = inv * c[1] + alpha * target[1];
            c[2] = inv * c[2] + alpha * target[2];
        }

        /// <summary>
        /// Perceptually-weighted color distance: 2R² + 4G² + B².
        /// </summary>
        private static double ColorDistance(double[] a, double[] b)
        {
            double dr = a[0] - b[0], dg = a[1] - b[1], db = a[2] - b[2];
            return 2 * dr * dr + 4 * dg * dg + db * db;
        }

        private static bool EqualColors(double[] a, double[] b)
        {
            return a[0] == b[0] && a[1] == b[1] && a[2] == b[2];
        }

        // ======================== UTILITY ========================

        private static int MaxIndex(double[] values)
        {
            int m = 0;
            for (int i = 1; i < values.Length; i++)
                if (values[i] > values[m]) m = i;
            return m;
        }

        private static int MinIndex(double[] values)
        {
            int m = 0;
            for (int i = 1; i < values.Length; i++)
                if (values[i] < values[m]) m = i;
            return m;
        }

        private static void ReverseRange(int[] a, int left, int right)
        {
            while (left < right)
            {
                int tmp = a[left]; a[left] = a[right]; a[right] = tmp;
                left++; right--;
            }
        }

        private int SafeNext(int max)
        {
            return max <= 0 ? 0 : _rng.Next(max);
        }

        // ======================== CLOSEST COLOR / PALETTE ========================

        private static void GetClosestColor(List<double[]> palette, double[] color,
            out int bestIndex, out double bestDist)
        {
            bestIndex = palette.Count - 1;
            bestDist = ColorDistance(palette[bestIndex], color);
            for (int i = palette.Count - 2; i >= 0; i--)
            {
                double d = ColorDistance(palette[i], color);
                if (d < bestDist) { bestIndex = i; bestDist = d; }
            }
        }

        private void GetClosestColorDither(List<double[]> palette, QPixel pixel,
            out int bestIndex, out double bestDist, out double[] comparedColor)
        {
            double[] error = { 0, 0, 0 };
            double[] linearPixel = CloneColor(pixel.Color);
            ToLinearColor(linearPixel);
            var candidates = new List<DitherCandidate>(_ditherPixels);
            double[] c = new double[3], err = new double[3], reducedColor = new double[3];

            for (int i = 0; i < _ditherPixels; i++)
            {
                CopyColor(c, linearPixel);
                CopyColor(err, error);
                ScaleColor(err, _opts.DitherWeight);
                AddColor(c, err);
                ClampColor(c, 0, 255.0 * 255.0);
                ToSrgbColor(c);

                int minCI; double minDist;
                GetClosestColor(palette, c, out minCI, out minDist);
                double[] minColor = palette[minCI];
                candidates.Add(new DitherCandidate
                {
                    ColorIndex = minCI,
                    ColorDistance = minDist,
                    ComparedColor = new double[] { c[0], c[1], c[2] },
                    Brightness = Brightness(minColor)
                });

                CopyColor(reducedColor, minColor);
                ToNbitColor(reducedColor, _opts.BitsPerChannel);
                ToLinearColor(reducedColor);
                AddColor(error, linearPixel);
                SubtractColor(error, reducedColor);
            }

            candidates.Sort((a, b) => a.Brightness.CompareTo(b.Brightness));
            int idx = _ditherPattern[pixel.X & 1, pixel.Y & 1];
            bestIndex = candidates[idx].ColorIndex;
            bestDist = candidates[idx].ColorDistance;
            comparedColor = candidates[idx].ComparedColor;
        }

        private static double PaletteDistance(List<double[]> palette, QTile tile)
        {
            double sum = 0;
            for (int i = 0; i < tile.Colors.Count; i++)
            {
                int bi; double bd;
                GetClosestColor(palette, tile.Colors[i], out bi, out bd);
                sum += tile.Counts[i] * bd;
            }
            return sum;
        }

        private double PaletteDistanceDither(List<double[]> palette, QTile tile)
        {
            double sum = 0;
            foreach (var px in tile.Pixels)
            {
                int bi; double bd; double[] cc;
                GetClosestColorDither(palette, px, out bi, out bd, out cc);
                sum += bd;
            }
            return sum;
        }

        private int GetClosestPaletteIndex(List<List<double[]>> palettes, QTile tile)
        {
            double minD = PaletteDistance(palettes[0], tile);
            int minI = 0;
            for (int i = 1; i < palettes.Count; i++)
            {
                double d = PaletteDistance(palettes[i], tile);
                if (d < minD) { minD = d; minI = i; }
            }
            return minI;
        }

        private void ClosestPaletteDistance(List<List<double[]>> palettes, QTile tile,
            out int bestIndex, out double bestDist)
        {
            bestDist = PaletteDistance(palettes[0], tile);
            bestIndex = 0;
            for (int i = 1; i < palettes.Count; i++)
            {
                double d = PaletteDistance(palettes[i], tile);
                if (d < bestDist) { bestDist = d; bestIndex = i; }
            }
        }

        private int GetClosestPaletteIndexDither(List<List<double[]>> palettes, QTile tile)
        {
            double minD = PaletteDistanceDither(palettes[0], tile);
            int minI = 0;
            for (int i = 1; i < palettes.Count; i++)
            {
                double d = PaletteDistanceDither(palettes[i], tile);
                if (d < minD) { minD = d; minI = i; }
            }
            return minI;
        }

        private void ClosestPaletteDistanceDither(List<List<double[]>> palettes, QTile tile,
            out int bestIndex, out double bestDist)
        {
            bestDist = PaletteDistanceDither(palettes[0], tile);
            bestIndex = 0;
            for (int i = 1; i < palettes.Count; i++)
            {
                double d = PaletteDistanceDither(palettes[i], tile);
                if (d < bestDist) { bestDist = d; bestIndex = i; }
            }
        }

        // ======================== TILE EXTRACTION ========================

        private QTile ExtractTile(byte[] data, int width, int height, int startX, int startY)
        {
            var tile = new QTile();
            int endX = Math.Min(startX + _opts.TileWidth, width);
            int endY = Math.Min(startY + _opts.TileHeight, height);

            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    int idx = 4 * (x + width * y);
                    double[] color = { data[idx], data[idx + 1], data[idx + 2] };

                    if (_opts.ColorZeroBehaviour == ColorZeroBehaviour.TransparentFromTransparent
                        && data[idx + 3] < 255)
                        continue;
                    if (_opts.ColorZeroBehaviour == ColorZeroBehaviour.TransparentFromColor
                        && EqualColors(color, _opts.ColorZeroValue))
                        continue;

                    tile.Pixels.Add(new QPixel { Tile = tile, Color = color, X = x, Y = y });

                    int ci = -1;
                    for (int c = 0; c < tile.Colors.Count; c++)
                    {
                        if (EqualColors(tile.Colors[c], color)) { ci = c; break; }
                    }
                    if (ci >= 0) tile.Counts[ci]++;
                    else { tile.Colors.Add(color); tile.Counts.Add(1); }
                }
            }
            return tile;
        }

        private List<QTile> ExtractTiles(byte[] data, int width, int height)
        {
            var tiles = new List<QTile>();
            for (int y = 0; y < height; y += _opts.TileHeight)
                for (int x = 0; x < width; x += _opts.TileWidth)
                {
                    var tile = ExtractTile(data, width, height, x, y);
                    if (tile.Colors.Count > 0) tiles.Add(tile);
                }
            return tiles;
        }

        private static List<QPixel> ExtractAllPixels(List<QTile> tiles)
        {
            var pixels = new List<QPixel>();
            foreach (var tile in tiles)
                foreach (var px in tile.Pixels)
                    pixels.Add(new QPixel { Tile = px.Tile, Color = px.Color, X = px.X, Y = px.Y });
            return pixels;
        }

        // ======================== PALETTE BUILDING ========================

        private void MovePalettesCloser(List<List<double[]>> palettes, QPixel pixel, double alpha)
        {
            int sharedIdx = (_opts.ColorZeroBehaviour == ColorZeroBehaviour.Shared) ? 0 : -1;
            int pIdx;
            if (_opts.Dither == DitherMode.Slow)
                pIdx = GetClosestPaletteIndexDither(palettes, pixel.Tile);
            else
                pIdx = GetClosestPaletteIndex(palettes, pixel.Tile);

            int colorIdx;
            double[] compColor;
            if (_opts.Dither == DitherMode.Slow)
            {
                double dummy; double[] cc;
                GetClosestColorDither(palettes[pIdx], pixel, out colorIdx, out dummy, out cc);
                compColor = cc;
            }
            else
            {
                double dummy;
                GetClosestColor(palettes[pIdx], pixel.Color, out colorIdx, out dummy);
                compColor = pixel.Color;
            }

            if (colorIdx != sharedIdx)
                MoveColorCloser(palettes[pIdx][colorIdx], compColor, alpha);
        }

        private List<List<double[]>> ColorQuantize1Color(List<QTile> tiles, List<QPixel> pixels,
            RandomShuffle shuffle)
        {
            int iterations = (int)(_opts.FractionOfPixels * pixels.Count);
            double alpha = 0.3;
            if (_opts.Dither == DitherMode.Slow) { iterations /= 5; alpha = 0.1; }

            double[] avgColor = { 0, 0, 0 };
            foreach (var p in pixels) AddColor(avgColor, p.Color);
            ScaleColor(avgColor, 1.0 / pixels.Count);

            var palettes = new List<List<double[]>>();
            var firstPal = new List<double[]> { avgColor };
            if (_opts.ColorZeroBehaviour == ColorZeroBehaviour.Shared)
            {
                firstPal.Add(CloneColor(avgColor));
                firstPal[0] = CloneColor(_opts.ColorZeroValue);
            }
            palettes.Add(firstPal);

            int splitIndex = 0;
            for (int i = 2; i <= _opts.NumPalettes; i++)
            {
                var newPal = new List<double[]>();
                foreach (var c in palettes[splitIndex]) newPal.Add(CloneColor(c));
                palettes.Add(newPal);

                for (int iter = 0; iter < iterations; iter++)
                    MovePalettesCloser(palettes, pixels[shuffle.Next()], alpha);

                double[] pDistArr = new double[palettes.Count];
                foreach (var t in tiles)
                {
                    int idx; double dist;
                    ClosestPaletteDistance(palettes, t, out idx, out dist);
                    pDistArr[idx] += dist;
                }
                splitIndex = MaxIndex(pDistArr);
            }
            return palettes;
        }

        private void ExpandPalettesByOneColor(List<List<double[]>> palettes, List<QTile> tiles,
            List<QPixel> pixels, RandomShuffle shuffle)
        {
            int iterations = (int)(_opts.FractionOfPixels * pixels.Count);
            double alpha = 0.3;
            if (_opts.Dither == DitherMode.Slow) { iterations /= 5; alpha = 0.1; }

            int numColors = palettes[0].Count + 1;
            int[] splitIndexes = new int[palettes.Count];

            if (numColors > 2)
            {
                var totalColorDist = new double[palettes.Count][];
                for (int i = 0; i < palettes.Count; i++)
                    totalColorDist[i] = new double[numColors];

                foreach (var tile in tiles)
                {
                    int pIdx = GetClosestPaletteIndex(palettes, tile);
                    var pal = palettes[pIdx];
                    for (int i = 0; i < tile.Colors.Count; i++)
                    {
                        int cIdx; double dist;
                        GetClosestColor(pal, tile.Colors[i], out cIdx, out dist);
                        totalColorDist[pIdx][cIdx] += tile.Counts[i] * dist;
                    }
                }
                for (int i = 0; i < palettes.Count; i++)
                    splitIndexes[i] = MaxIndex(totalColorDist[i]);
            }

            for (int i = 0; i < palettes.Count; i++)
                palettes[i].Add(CloneColor(palettes[i][splitIndexes[i]]));

            for (int iter = 0; iter < iterations; iter++)
                MovePalettesCloser(palettes, pixels[shuffle.Next()], alpha);
        }

        private List<List<double[]>> ReplaceWeakestColors(List<List<double[]>> palettes,
            List<QTile> tiles, double minColorFactor, double minPaletteFactor, bool replacePalettes)
        {
            bool useSlow = _opts.Dither == DitherMode.Slow;
            int[] closestPalIdx = new int[tiles.Count];
            double[] totalPalMse = new double[palettes.Count];
            double[] removedPalMse = new double[palettes.Count];

            if (palettes.Count > 1)
            {
                for (int j = 0; j < tiles.Count; j++)
                {
                    int idx; double dist;
                    if (useSlow) ClosestPaletteDistanceDither(palettes, tiles[j], out idx, out dist);
                    else ClosestPaletteDistance(palettes, tiles[j], out idx, out dist);

                    totalPalMse[idx] += dist;
                    closestPalIdx[j] = idx;

                    var remaining = new List<List<double[]>>();
                    for (int i = 0; i < palettes.Count; i++)
                        if (i != idx) remaining.Add(palettes[i]);

                    if (remaining.Count > 0)
                    {
                        int idx2; double dist2;
                        if (useSlow) ClosestPaletteDistanceDither(remaining, tiles[j], out idx2, out dist2);
                        else ClosestPaletteDistance(remaining, tiles[j], out idx2, out dist2);
                        removedPalMse[idx] += dist2;
                    }
                }
            }

            var totalColorMse = new double[palettes.Count][];
            var secondColorMse = new double[palettes.Count][];
            for (int i = 0; i < palettes.Count; i++)
            {
                totalColorMse[i] = new double[palettes[i].Count];
                secondColorMse[i] = new double[palettes[i].Count];
            }

            for (int j = 0; j < tiles.Count; j++)
            {
                int pIdx = closestPalIdx[j];
                var pal = palettes[pIdx];
                var tile = tiles[j];

                if (useSlow)
                {
                    foreach (var px in tile.Pixels)
                    {
                        int cIdx; double dist; double[] cc;
                        GetClosestColorDither(pal, px, out cIdx, out dist, out cc);
                        totalColorMse[pIdx][cIdx] += dist;

                        var rem = new List<double[]>();
                        for (int i = 0; i < pal.Count; i++) if (i != cIdx) rem.Add(pal[i]);
                        int ci2; double d2;
                        GetClosestColorDither(rem, px, out ci2, out d2, out cc);
                        secondColorMse[pIdx][cIdx] += d2;
                    }
                }
                else
                {
                    for (int i = 0; i < tile.Colors.Count; i++)
                    {
                        int cIdx; double dist;
                        GetClosestColor(pal, tile.Colors[i], out cIdx, out dist);
                        totalColorMse[pIdx][cIdx] += dist * tile.Counts[i];

                        var rem = new List<double[]>();
                        for (int k = 0; k < pal.Count; k++) if (k != cIdx) rem.Add(pal[k]);
                        int ci2; double d2;
                        GetClosestColor(rem, tile.Colors[i], out ci2, out d2);
                        secondColorMse[pIdx][cIdx] += d2 * tile.Counts[i];
                    }
                }
            }

            int sharedIdx = (_opts.ColorZeroBehaviour == ColorZeroBehaviour.Shared) ? 0 : -1;
            var result = new List<List<double[]>>();

            for (int pIdx = 0; pIdx < palettes.Count; pIdx++)
            {
                int maxC = MaxIndex(totalColorMse[pIdx]);
                int minC = MinIndex(secondColorMse[pIdx]);
                bool replace = minC != maxC && minC != sharedIdx &&
                    secondColorMse[pIdx][minC] < minColorFactor * totalColorMse[pIdx][maxC];

                var newPal = new List<double[]>();
                for (int i = 0; i < palettes[pIdx].Count; i++)
                {
                    if (i == minC && replace)
                        newPal.Add(CloneColor(palettes[pIdx][maxC]));
                    else
                        newPal.Add(CloneColor(palettes[pIdx][i]));
                }
                result.Add(newPal);
            }

            if (replacePalettes && palettes.Count > 1)
            {
                int maxP = MaxIndex(totalPalMse);
                int minP = MinIndex(removedPalMse);
                if (minP != maxP && removedPalMse[minP] < minPaletteFactor * totalPalMse[maxP])
                {
                    result[minP] = new List<double[]>();
                    foreach (var c in result[maxP]) result[minP].Add(CloneColor(c));
                }
            }

            return result;
        }

        private List<List<double[]>> KMeans(List<List<double[]>> palettes, List<QTile> tiles)
        {
            var counts = new int[palettes.Count][];
            var sums = new double[palettes.Count][][];
            for (int i = 0; i < palettes.Count; i++)
            {
                counts[i] = new int[palettes[i].Count];
                sums[i] = new double[palettes[i].Count][];
                for (int j = 0; j < palettes[i].Count; j++)
                    sums[i][j] = new double[] { 0, 0, 0 };
            }

            foreach (var t in tiles)
            {
                int pIdx = (_opts.Dither == DitherMode.Slow) ?
                    GetClosestPaletteIndexDither(palettes, t) :
                    GetClosestPaletteIndex(palettes, t);

                if (_opts.Dither == DitherMode.Slow)
                {
                    foreach (var px in t.Pixels)
                    {
                        int cIdx; double bd; double[] cc;
                        GetClosestColorDither(palettes[pIdx], px, out cIdx, out bd, out cc);
                        counts[pIdx][cIdx]++;
                        AddColor(sums[pIdx][cIdx], px.Color);
                    }
                }
                else
                {
                    for (int i = 0; i < t.Colors.Count; i++)
                    {
                        int cIdx; double bd;
                        GetClosestColor(palettes[pIdx], t.Colors[i], out cIdx, out bd);
                        counts[pIdx][cIdx] += t.Counts[i];
                        double[] col = CloneColor(t.Colors[i]);
                        ScaleColor(col, t.Counts[i]);
                        AddColor(sums[pIdx][cIdx], col);
                    }
                }
            }

            int sharedIdx = (_opts.ColorZeroBehaviour == ColorZeroBehaviour.Shared) ? 0 : -1;
            var result = new List<List<double[]>>();
            for (int i = 0; i < palettes.Count; i++)
            {
                var newPal = new List<double[]>();
                for (int j = 0; j < palettes[i].Count; j++)
                {
                    if (counts[i][j] == 0 || j == sharedIdx)
                        newPal.Add(CloneColor(palettes[i][j]));
                    else
                    {
                        ScaleColor(sums[i][j], 1.0 / counts[i][j]);
                        newPal.Add(sums[i][j]);
                    }
                }
                result.Add(newPal);
            }
            return result;
        }

        private static List<List<double[]>> ReducePalettes(List<List<double[]>> palettes, int bits)
        {
            var result = new List<List<double[]>>();
            foreach (var pal in palettes)
            {
                var newPal = new List<double[]>();
                foreach (var c in pal)
                {
                    double[] col = CloneColor(c);
                    ToNbitColor(col, bits);
                    newPal.Add(col);
                }
                result.Add(newPal);
            }
            return result;
        }

        // ======================== PALETTE SORTING (TSP) ========================

        /// <summary>
        /// TSP-based palette and color ordering for visual coherence.
        /// </summary>
        private List<List<double[]>> SortPalettes(List<List<double[]>> palettes, int startIndex)
        {
            int numPal = palettes.Count;
            int numCol = palettes[0].Count;
            if (numCol == 2 && startIndex == 1) return palettes;

            // Distance matrix between palettes (1-indexed with sentinels at 0 and numPal+1)
            double[][] palDist = new double[numPal + 2][];
            for (int i = 0; i < numPal + 2; i++) palDist[i] = new double[numPal + 2];

            // Color index mapping: colIdx[p1][p2][k] = color in p2 that color k in p1 maps to
            int[][][] colIdx = new int[numPal][][];
            for (int i = 0; i < numPal; i++)
            {
                colIdx[i] = new int[numPal][];
                for (int j = 0; j < numPal; j++)
                {
                    colIdx[i][j] = new int[numCol];
                    for (int k = 0; k < numCol; k++) colIdx[i][j][k] = k;
                }
            }

            // Optimize color mappings between each pair of palettes
            for (int p1 = 0; p1 < numPal - 1; p1++)
            {
                for (int p2 = p1 + 1; p2 < numPal; p2++)
                {
                    int[] idx = colIdx[p1][p2];
                    for (int it = 0; it < 2000; it++)
                    {
                        int range1 = numCol - startIndex - 1;
                        int i1 = startIndex + SafeNext(range1);
                        int range2 = numCol - i1 - 1;
                        int i2 = i1 + 1 + SafeNext(range2);
                        if (i2 >= numCol) continue;

                        if (_rng.NextDouble() < 0.5) { int tmp = i1; i1 = i2; i2 = tmp; }

                        double d1 = ColorDistance(palettes[p1][i1], palettes[p2][idx[i2]])
                                   + ColorDistance(palettes[p1][i2], palettes[p2][idx[i1]]);
                        double d2 = ColorDistance(palettes[p1][i1], palettes[p2][idx[i1]])
                                   + ColorDistance(palettes[p1][i2], palettes[p2][idx[i2]]);
                        if (d1 < d2)
                        {
                            int tmp = idx[i1]; idx[i1] = idx[i2]; idx[i2] = tmp;
                        }
                    }
                    double sum = 0;
                    for (int i = 0; i < numCol; i++)
                        sum += ColorDistance(palettes[p1][i], palettes[p2][idx[i]]);
                    palDist[p1 + 1][p2 + 1] = palDist[p2 + 1][p1 + 1] = sum;
                }
            }

            // Compute inverse mappings (for p1 > p2)
            for (int p1 = 1; p1 < numPal; p1++)
                for (int p2 = 0; p2 < p1; p2++)
                    for (int i = 0; i < numCol; i++)
                        colIdx[p1][p2][i] = Array.IndexOf(colIdx[p2][p1], i);

            // TSP-optimize palette ordering
            int[] pIdxs = new int[numPal + 2];
            for (int i = 0; i < numPal + 2; i++) pIdxs[i] = i;

            if (numPal > 2)
            {
                for (int it = 0; it < 100000; it++)
                {
                    int i1 = Math.Max(1, _rng.Next(numPal));
                    int i2 = Math.Min(numPal, i1 + 1 + _rng.Next(numPal));
                    double d1 = palDist[pIdxs[i1 - 1]][pIdxs[i2]]
                              + palDist[pIdxs[i1]][pIdxs[i2 + 1]];
                    double d2 = palDist[pIdxs[i1 - 1]][pIdxs[i1]]
                              + palDist[pIdxs[i2]][pIdxs[i2 + 1]];
                    if (d1 < d2) ReverseRange(pIdxs, i1, i2);
                }
            }

            // TSP-optimize color ordering within the base palette
            int[][] pIdxRes = new int[numPal][];
            for (int i = 0; i < numPal; i++) pIdxRes[i] = new int[numCol];

            int[] p1IdxList = new int[numCol + 2];
            for (int i = 0; i < numCol + 2; i++) p1IdxList[i] = i;

            double[][] p1Dists = new double[numCol + 2][];
            for (int i = 0; i < numCol + 2; i++) p1Dists[i] = new double[numCol + 2];

            var baseP = palettes[pIdxs[1] - 1];
            for (int i = 1; i <= numCol; i++)
                for (int j = 1; j <= numCol; j++)
                    p1Dists[i][j] = ColorDistance(baseP[i - 1], baseP[j - 1]);

            if (numCol > 2)
            {
                for (int it = 0; it < 100000; it++)
                {
                    int i1 = Math.Max(1 + startIndex, _rng.Next(numCol));
                    int i2 = Math.Min(numCol, i1 + 1 + _rng.Next(numCol));
                    double d1 = p1Dists[p1IdxList[i1 - 1]][p1IdxList[i2]]
                              + p1Dists[p1IdxList[i1]][p1IdxList[i2 + 1]];
                    double d2 = p1Dists[p1IdxList[i1 - 1]][p1IdxList[i1]]
                              + p1Dists[p1IdxList[i2]][p1IdxList[i2 + 1]];
                    if (d1 < d2) ReverseRange(p1IdxList, i1, i2);
                }
            }

            // Build first palette's color ordering
            for (int i = 0; i < numCol; i++)
                pIdxRes[0][i] = p1IdxList[i + 1] - 1;

            // Propagate color ordering to remaining palettes via the color mappings.
            // JS: pIdxRes[i][j] = colIdx[pIdxs[i]-1][pIdxs[i+1]-1][pIdxRes[i-1][j]]
            // Maps from palette pIdxs[i]-1 to palette pIdxs[i+1]-1.
            for (int i = 1; i < numPal; i++)
                for (int j = 0; j < numCol; j++)
                    pIdxRes[i][j] = colIdx[pIdxs[i] - 1][pIdxs[i + 1] - 1][pIdxRes[i - 1][j]];

            // Assemble sorted palettes
            var sorted = new List<List<double[]>>();
            for (int i = 0; i < numPal; i++)
            {
                int srcPal = pIdxs[i + 1] - 1;
                var newPal = new List<double[]>();
                for (int j = 0; j < numCol; j++)
                    newPal.Add(palettes[srcPal][pIdxRes[i][j]]);
                sorted.Add(newPal);
            }
            return sorted;
        }

        // ======================== FINAL TILE QUANTIZATION ========================

        private byte[] QuantizeTilesMethod(List<List<double[]>> palettes, byte[] reducedData,
            int width, int height, bool useDither)
        {
            int adjustedIndex = (_opts.ColorZeroBehaviour == ColorZeroBehaviour.TransparentFromColor
                              || _opts.ColorZeroBehaviour == ColorZeroBehaviour.TransparentFromTransparent) ? 1 : 0;
            bool imageIsReduced = _opts.Dither != DitherMode.Off;

            var reducedPalettes = new List<List<double[]>>();
            foreach (var pal in palettes)
            {
                var rp = new List<double[]>();
                foreach (var c in pal)
                {
                    double[] col = CloneColor(c);
                    ToNbitColor(col, _opts.BitsPerChannel);
                    rp.Add(col);
                }
                reducedPalettes.Add(rp);
            }

            double[] transparentColor = CloneColor(_opts.ColorZeroValue);
            if (imageIsReduced) ToNbitColor(transparentColor, _opts.BitsPerChannel);

            byte[] colorIndexes = new byte[width * height];

            for (int startY = 0; startY < height; startY += _opts.TileHeight)
            {
                for (int startX = 0; startX < width; startX += _opts.TileWidth)
                {
                    var tile = ExtractTile(reducedData, width, height, startX, startY);
                    int closestPaletteIndex = 0;
                    if (tile.Colors.Count > 0)
                    {
                        closestPaletteIndex = useDither ?
                            GetClosestPaletteIndexDither(reducedPalettes, tile) :
                            GetClosestPaletteIndex(reducedPalettes, tile);
                    }

                    var palette = reducedPalettes[closestPaletteIndex];
                    int endX = Math.Min(startX + _opts.TileWidth, width);
                    int endY = Math.Min(startY + _opts.TileHeight, height);

                    for (int y = startY; y < endY; y++)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            int idx = 4 * (x + width * y);
                            int outIdx = x + width * y;
                            double[] color = { reducedData[idx], reducedData[idx + 1], reducedData[idx + 2] };

                            if ((_opts.ColorZeroBehaviour == ColorZeroBehaviour.TransparentFromTransparent
                                 && reducedData[idx + 3] < 255)
                                || (_opts.ColorZeroBehaviour == ColorZeroBehaviour.TransparentFromColor
                                    && EqualColors(color, transparentColor)))
                            {
                                colorIndexes[outIdx] = (byte)(closestPaletteIndex * _opts.ColorsPerPalette);
                            }
                            else
                            {
                                int cIdx;
                                if (useDither)
                                {
                                    double bd; double[] cc;
                                    var px = new QPixel { Color = color, X = x, Y = y };
                                    GetClosestColorDither(palette, px, out cIdx, out bd, out cc);
                                }
                                else
                                {
                                    double bd;
                                    GetClosestColor(palette, color, out cIdx, out bd);
                                }
                                colorIndexes[outIdx] = (byte)(closestPaletteIndex * _opts.ColorsPerPalette
                                                              + cIdx + adjustedIndex);
                            }
                        }
                    }
                }
            }
            return colorIndexes;
        }

        // ======================== MAIN ENTRY POINT ========================

        /// <summary>
        /// Quantizes an RGBA image into tile-aligned palettes for SNES hardware.
        /// </summary>
        /// <param name="rgbaData">Raw RGBA pixel data (4 bytes per pixel, row-major).</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <returns>Quantized palettes and per-pixel color index array.</returns>
        public QuantizedResult Quantize(byte[] rgbaData, int width, int height)
        {
            // 1. Reduce to N-bit per channel if not dithering
            bool ditherOn = _opts.Dither != DitherMode.Off;
            byte[] reduced = new byte[rgbaData.Length];
            for (int i = 0; i < rgbaData.Length; i++)
                reduced[i] = ditherOn ? rgbaData[i] : (byte)ToNbit(rgbaData[i], _opts.BitsPerChannel);

            // 2. Extract 8×8 tiles and all non-transparent pixels
            var tiles = ExtractTiles(reduced, width, height);
            var pixels = ExtractAllPixels(tiles);
            if (pixels.Count == 0)
            {
                // Fully transparent image — return empty result
                return new QuantizedResult
                {
                    Palettes = new double[_opts.NumPalettes][][],
                    Width = width,
                    Height = height,
                    ColorIndexes = new byte[width * height]
                };
            }
            var shuffle = new RandomShuffle(pixels.Count, _rng);

            // 3. Seed palettes with 1 color, split to NumPalettes
            var pals = ColorQuantize1Color(tiles, pixels, shuffle);

            // 4. Iteratively expand each palette to full color count
            int sC = 2 + (_opts.ColorZeroBehaviour == ColorZeroBehaviour.Shared ? 1 : 0);
            int eC = _opts.ColorsPerPalette
                   - ((_opts.ColorZeroBehaviour == ColorZeroBehaviour.TransparentFromColor
                    || _opts.ColorZeroBehaviour == ColorZeroBehaviour.TransparentFromTransparent) ? 1 : 0);
            for (int n = sC; n <= eC; n++)
                ExpandPalettesByOneColor(pals, tiles, pixels, shuffle);

            // 5. Refine palettes
            for (int i = 0; i < 10; i++)
            {
                pals = ReplaceWeakestColors(pals, tiles, 0.5, 0.5, true);
                int iters = (int)(pixels.Count * _opts.FractionOfPixels);
                for (int j = 0; j < iters; j++)
                    MovePalettesCloser(pals, pixels[shuffle.Next()], 0.3);
            }

            // 6. K-means convergence (when not dithering)
            if (!ditherOn)
            {
                pals = ReducePalettes(pals, _opts.BitsPerChannel);
                for (int i = 0; i < 3; i++) pals = KMeans(pals, tiles);
            }
            pals = ReducePalettes(pals, _opts.BitsPerChannel);

            // 7. Sort palettes for visual coherence, then brightness-sort each
            int sSort = (_opts.ColorZeroBehaviour != ColorZeroBehaviour.Unique) ? 1 : 0;
            var sorted = SortPalettes(pals, sSort);
            for (int i = 0; i < sorted.Count; i++)
                sorted[i].Sort((a, b) => Brightness(a).CompareTo(Brightness(b)));

            // 8. Produce final per-pixel index array
            byte[] indexes = QuantizeTilesMethod(sorted, reduced, width, height, ditherOn);

            // Package result
            double[][][] palArr = new double[sorted.Count][][];
            for (int i = 0; i < sorted.Count; i++)
                palArr[i] = sorted[i].ToArray();

            return new QuantizedResult
            {
                Palettes = palArr,
                Width = width,
                Height = height,
                ColorIndexes = indexes
            };
        }
    }
}
