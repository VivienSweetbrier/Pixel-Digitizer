using SnesGFX.Properties;

namespace SnesGFX
{
    public static class Options
    {
        private static void SaveConfig()
        {
            try
            {
                Settings.Default.Save();
            }
            catch
            {
            }
        }


		/// <summary>
		/// Flag que determina se a imagem fica no modo transparência.
		/// </summary>
		public static bool CGADSUB
		{
			get
			{
				return Settings.Default.CGADSUB;
			}

			set
			{
				Settings.Default.CGADSUB = value;
				SaveConfig();
			}
		}

        /// <summary>
        /// Flag que determina se a imagem deve ter transparência ou não
        /// </summary>
        public static bool AllowTransparency
        {
            get
            {
                return Settings.Default.Transparency;
            }

            set
            {
                Settings.Default.Transparency = value;
                SaveConfig();
            }
        }

        /// <summary>
        /// Flag que determina se a palette vai ser ordenada ou não.
        /// </summary>
        public static bool OrderPalette
        {
            get
            {
                return Settings.Default.OrderPalette;
            }

            set
            {
                Settings.Default.OrderPalette = value;
                SaveConfig();
            }
        }

        /// <summary>
        /// Flag que determina se vai usar configurações de qualidados ou de pobre.
        /// </summary>
        public static bool HiQuality
        {
            get
            {
                return Settings.Default.HiQuality;
            }

            set
            {
                Settings.Default.HiQuality = value;
                SaveConfig();
            }
        }

        /// <summary>
        /// Flag que determina se a imagem será redmensionada ou não.
        /// </summary>
        public static bool ScaleOption
        {
            get
            {
                return Settings.Default.ScaleOption;
            }

            set
            {
                Settings.Default.ScaleOption = value;
                SaveConfig();
            }
        }

        public static bool RemoveDuplicateTiles
        {
            get
            {
                return Settings.Default.NoDuplicate;
            }

            set
            {
                Settings.Default.NoDuplicate = value;
                SaveConfig();
            }
        }

        public static bool RemoveFlippedTiles
        {
            get
            {
                return Settings.Default.NoFlips;
            }

            set
            {
                Settings.Default.NoFlips = value;
                SaveConfig();
            }
        }


        public static bool OptimizeImage
        {
            get
            {
                return Settings.Default.OptimizeImage;
            }

            set
            {
                Settings.Default.OptimizeImage = value;
                SaveConfig();
            }
        }

        public static bool SaveOnImageFolder
        {
            get
            {
                return Settings.Default.SaveImageFolder;
            }

            set
            {
                Settings.Default.SaveImageFolder = value;
                SaveConfig();
            }
        }

        public static bool SplitOutput
        {
            get
            {
                return Settings.Default.Split;
            }

            set
            {
                Settings.Default.Split = value;
                SaveConfig();
            }
        }

        public static int SplitOutputIndex
        {
            get
            {
                return Settings.Default.SplitItem;
            }

            set
            {
                Settings.Default.SplitItem = value;
                SaveConfig();
            }
        }

        public static int TilemapOutput
        {
            get
            {
                return Settings.Default.TilemapOutput;
            }
            set
            {
                Settings.Default.TilemapOutput = value;
                SaveConfig();
            }
        }

        public static int PaletteOutput
        {
            get
            {
                return Settings.Default.PaletteOutput;
            }
            set
            {
                Settings.Default.PaletteOutput = value;
                SaveConfig();
            }
        }

		public static int OffsetTile
		{
			get
			{
				return Settings.Default.OffsetTile;
			}
			set
			{
				Settings.Default.OffsetTile = value;
				SaveConfig();
			}
		}

		public static int OffsetPalette
		{
			get
			{
				return Settings.Default.OffsetPalette;
			}
			set
			{
				Settings.Default.OffsetPalette = value;
				SaveConfig();
			}
		}

        private static bool _crossPaletteDedup = false;
		public static bool CrossPaletteDedup
        {
            get { return _crossPaletteDedup; }
            set { _crossPaletteDedup = value; }
        }

        private static int _numPalettes = 8;
        public static int NumPalettes
        {
            get { return _numPalettes; }
            set { _numPalettes = value; }
        }

        private static int _colorsPerPalette = 4;
        public static int ColorsPerPalette
        {
            get { return _colorsPerPalette; }
            set { _colorsPerPalette = value; }
        }

        private static bool _usePaletteQuant = true;
        public static bool UsePaletteQuant
        {
            get { return _usePaletteQuant; }
            set { _usePaletteQuant = value; }
        }

        private static string _nodePath = "node";
        public static string NodePath
        {
            get { return _nodePath; }
            set { _nodePath = value; }
        }

        public static bool ShowSplash
        {
            get { return Settings.Default.ShowSplash; }
            set { Settings.Default.ShowSplash = value; SaveConfig(); }
        }

        public static int TileWidth
        {
            get { return Settings.Default.TileWidth; }
            set { Settings.Default.TileWidth = value; SaveConfig(); }
        }

        public static int TileHeight
        {
            get { return Settings.Default.TileHeight; }
            set { Settings.Default.TileHeight = value; SaveConfig(); }
        }

        public static int BitsPerChannel
        {
            get { return Settings.Default.BitsPerChannel; }
            set { Settings.Default.BitsPerChannel = value; SaveConfig(); }
        }

        public static double FractionOfPixels
        {
            get { return Settings.Default.FractionOfPixels; }
            set { Settings.Default.FractionOfPixels = value; SaveConfig(); }
        }

        public static int ColorZeroBehaviour
        {
            get { return Settings.Default.ColorZeroBehaviour; }
            set { Settings.Default.ColorZeroBehaviour = value; SaveConfig(); }
        }

        public static int DitherMode
        {
            get { return Settings.Default.DitherMode; }
            set { Settings.Default.DitherMode = value; SaveConfig(); }
        }

        public static double DitherWeight
        {
            get { return Settings.Default.DitherWeight; }
            set { Settings.Default.DitherWeight = value; SaveConfig(); }
        }

        public static int DitherPattern
        {
            get { return Settings.Default.DitherPattern; }
            set { Settings.Default.DitherPattern = value; SaveConfig(); }
        }

        public static bool MwlLayer1
        {
            get { return Settings.Default.MwlLayer1; }
            set { Settings.Default.MwlLayer1 = value; SaveConfig(); }
        }

    }
}
