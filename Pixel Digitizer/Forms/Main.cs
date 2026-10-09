using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using SnesGFX.Properties;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Xml.Serialization;
using SnesGFX;
using SnesGFX.SNES;

namespace SnesGFX.Forms
{
    /// <summary>
    /// Classe do Form principal
    /// </summary>
    public partial class Main : Form
    {
        /// <summary>
        /// Inicializadora do Windows Forms. Requer a execução do Program.Main();
        /// </summary>
        public Main()
        {
            InitializeComponent();
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            this.comboBox3.Enabled = false;
			for (int i = 0x3FF; i >= 0; --i)
			{
				this.domainUpDown1.Items.Add(i.ToString("X"));
			}
			for (int i = 0xFF; i >= 0; --i)
			{
				this.domainUpDown2.Items.Add(i.ToString("X2"));
			}
            this.AllowDrop = true;
            this.DragEnter += new DragEventHandler(Main_DragEnter);
            this.DragDrop += new DragEventHandler(Main_DragDrop);

            LoadSettings();
            Global_DetectCodecs();
            InitializeQuantizerControls();
            InitializeMassRotateControls();
            InitializeBPPConverterControls();
            InitializeMode7MapperControls();
            InitializeHDMARipperControls();
            ApplyLivePreviewUI();
            InitializeSidebar();
            ApplyUnifiedDashboard();

            ApplyDarkMode();
            SetupTooltips();
            SwitchView(-1); // Start with Dashboard
        }

        private PictureBox previewBox;
        private Panel previewPanel;
        private System.Windows.Forms.Timer debounceTimer;

        private Panel pnlLeft;
        private Panel pnlMiddle;
        private Panel pnlNav;
        private List<Button> navButtons = new List<Button>();
        private int currentView = -1; // -1 = Dashboard, >=0 = TabControl index

        private void ApplyUnifiedDashboard()
        {
            // 1. Hide TabControl
            tabControl1.Visible = false;

            // 2. Adjust Main Window
            this.MinimumSize = new Size(1100, 600);
            this.Size = new Size(1100, 600);
            this.Text = "PiXeL Digitizer - Unified Conversion Dashboard";

            // 3. Create Panels
            pnlLeft = new Panel();
            pnlLeft.Location = new Point(130, 10);
            pnlLeft.Size = new Size(220, this.ClientSize.Height - 20);
            pnlLeft.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            pnlLeft.BackColor = Color.FromArgb(35, 35, 38);
            pnlLeft.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(pnlLeft);

            pnlMiddle = new Panel();
            pnlMiddle.Location = new Point(360, 10);
            pnlMiddle.Size = new Size(260, this.ClientSize.Height - 20);
            pnlMiddle.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            pnlMiddle.BackColor = Color.FromArgb(35, 35, 38);
            pnlMiddle.BorderStyle = BorderStyle.FixedSingle;
            pnlMiddle.AutoScroll = true;
            this.Controls.Add(pnlMiddle);

            // Move existing preview panel
            previewPanel.Location = new Point(630, 10);
            previewPanel.Size = new Size(this.ClientSize.Width - 640, this.ClientSize.Height - 20);

            // 4. Populate Left Panel (Input / Export)
            Label lblInput = new Label() { Text = "INPUT & CONVERSION", ForeColor = Color.White, Font = new Font(this.Font, FontStyle.Bold), Location = new Point(10, 10), AutoSize = true };
            pnlLeft.Controls.Add(lblInput);

            label1.Location = new Point(10, 40); // "Image file:"
            pnlLeft.Controls.Add(label1);

            textBox1.Location = new Point(10, 60);
            textBox1.Size = new Size(150, 20);
            pnlLeft.Controls.Add(textBox1);

            button1.Location = new Point(165, 59); // "..."
            button1.Size = new Size(40, 22);
            pnlLeft.Controls.Add(button1);

            label2.Location = new Point(10, 90); // "Format/Codec:"
            pnlLeft.Controls.Add(label2);

            comboBox1.Location = new Point(10, 110);
            comboBox1.Size = new Size(195, 21);
            pnlLeft.Controls.Add(comboBox1);

            // Add Resize options
            checkBox3.Location = new Point(10, 140);
            checkBox3.Text = "Resize Image (%)";
            pnlLeft.Controls.Add(checkBox3);

            textBox2.Location = new Point(130, 138);
            textBox2.Size = new Size(75, 20);
            pnlLeft.Controls.Add(textBox2);

            button2.Location = new Point(10, 180);
            button2.Size = new Size(195, 45);
            button2.Text = "Process and Export";
            button2.Font = new Font(this.Font, FontStyle.Bold);
            button2.BackColor = Color.FromArgb(0, 122, 204);
            button2.ForeColor = Color.White;
            button2.FlatStyle = FlatStyle.Flat;
            pnlLeft.Controls.Add(button2);

            if (pbQuantize != null)
            {
                pbQuantize.Location = new Point(10, 235);
                pbQuantize.Size = new Size(195, 15);
                pnlLeft.Controls.Add(pbQuantize);
            }

            // 5. Populate Middle Panel (Settings)
            Label lblSettings = new Label() { Text = "CONVERSION SETTINGS", ForeColor = Color.White, Font = new Font(this.Font, FontStyle.Bold), Location = new Point(10, 10), AutoSize = true };
            pnlMiddle.Controls.Add(lblSettings);

            // Find dynamically created controls
            CheckBox chkArrange = null;
            Button btnSortSettings = null;
            CheckBox chkCgadsub = null;
            Label lblPalettes = null;
            Label lblCpp = null;
            foreach (Control c in tabPage4.Controls)
            {
                if (c is Label && c.Text == "Palettes:") lblPalettes = (Label)c;
                if (c is Label && c.Text == "Colors per palette:") lblCpp = (Label)c;
                if (c is CheckBox && c.Text == "Sort Palette") chkArrange = (CheckBox)c;
                if (c is Button && c.Text == "Sort Settings...") btnSortSettings = (Button)c;
                if (c is CheckBox && c.Text.Contains("CGADSUB")) chkCgadsub = (CheckBox)c;
            }

            int yOffset = 40;

            // Palette Settings Group
            GroupBox gbPalette = new GroupBox() { Text = "Palette", ForeColor = Color.LightGray, Location = new Point(10, yOffset), Size = new Size(220, 140) };
            pnlMiddle.Controls.Add(gbPalette);
            
            if (lblPalettes != null)
            {
                lblPalettes.Location = new Point(10, 20);
                gbPalette.Controls.Add(lblPalettes);
            }
            if (numPalettesUD != null)
            {
                numPalettesUD.Location = new Point(120, 18);
                gbPalette.Controls.Add(numPalettesUD);
            }

            if (lblCpp != null)
            {
                lblCpp.Location = new Point(10, 45);
                gbPalette.Controls.Add(lblCpp);
            }
            if (colorsPerPaletteUD != null)
            {
                colorsPerPaletteUD.Location = new Point(120, 43);
                gbPalette.Controls.Add(colorsPerPaletteUD);
            }

            if (chkArrange != null)
            {
                chkArrange.Location = new Point(10, 75);
                gbPalette.Controls.Add(chkArrange);
            }
            if (btnSortSettings != null)
            {
                btnSortSettings.Location = new Point(120, 72);
                btnSortSettings.Size = new Size(90, 23);
                gbPalette.Controls.Add(btnSortSettings);
            }

            checkBox7.Location = new Point(10, 105);
            checkBox7.Text = "Handle Transparency";
            gbPalette.Controls.Add(checkBox7);

            yOffset += 150;

            // Tilemap Settings Group
            GroupBox gbTilemap = new GroupBox() { Text = "Tilemap", ForeColor = Color.LightGray, Location = new Point(10, yOffset), Size = new Size(220, 130) };
            pnlMiddle.Controls.Add(gbTilemap);

            label4.Location = new Point(10, 20);
            gbTilemap.Controls.Add(label4);
            comboBox4.Location = new Point(10, 40);
            comboBox4.Size = new Size(200, 21);
            gbTilemap.Controls.Add(comboBox4);

            checkBox13.Location = new Point(10, 70);
            gbTilemap.Controls.Add(checkBox13);

            checkBox9.Location = new Point(10, 95);
            checkBox9.Text = "Allow Tile Flipping (Opt.)";
            gbTilemap.Controls.Add(checkBox9);

            yOffset += 140;

            // Advanced Settings Group
            GroupBox gbAdv = new GroupBox() { Text = "Advanced", ForeColor = Color.LightGray, Location = new Point(10, yOffset), Size = new Size(220, 160) };
            pnlMiddle.Controls.Add(gbAdv);

            if (chkCgadsub != null)
            {
                chkCgadsub.Location = new Point(10, 20);
                gbAdv.Controls.Add(chkCgadsub);
            }

            checkBox5.Location = new Point(10, 45); // Optimize image
            gbAdv.Controls.Add(checkBox5);

            checkBox2.Location = new Point(10, 70); // Remove duplicate tiles
            gbAdv.Controls.Add(checkBox2);

            checkBox11.Location = new Point(10, 95); // Offset tiles
            gbAdv.Controls.Add(checkBox11);
            domainUpDown1.Location = new Point(120, 93);
            domainUpDown1.Size = new Size(80, 20);
            gbAdv.Controls.Add(domainUpDown1);

            checkBox12.Location = new Point(10, 120); // Offset palette
            gbAdv.Controls.Add(checkBox12);
            domainUpDown2.Location = new Point(120, 118);
            domainUpDown2.Size = new Size(80, 20);
            gbAdv.Controls.Add(domainUpDown2);

            if (btnQuantize != null) btnQuantize.Visible = false;

            // Set color schemes
            foreach (Control c in pnlLeft.Controls) UpdateControlColors(c);
            foreach (Control c in pnlMiddle.Controls) UpdateControlColors(c);
            
            // Adjust TabControl for legacy views
            tabControl1.Location = new Point(130, 10);
            tabControl1.Size = new Size(this.ClientSize.Width - 140, this.ClientSize.Height - 20);
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        }

        private void InitializeSidebar()
        {
            pnlNav = new Panel();
            pnlNav.Location = new Point(0, 0);
            pnlNav.Size = new Size(120, this.ClientSize.Height);
            pnlNav.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            pnlNav.BackColor = Color.FromArgb(45, 45, 48);
            this.Controls.Add(pnlNav);

            AddNavButton("Digitizer", -1, 10);
            AddNavButton("HDMA Ripper", 7, 60);
            AddNavButton("Mode 7", 4, 100);
            AddNavButton("Mass Conv", 2, 140);
            AddNavButton("8BPP Conv", 3, 180);
            AddNavButton("Profiles", 5, 230);
            AddNavButton("Options", 6, 270);
        }

        private void AddNavButton(string text, int viewIdx, int y)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Location = new Point(5, y);
            btn.Size = new Size(110, 35);
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.ForeColor = Color.White;
            btn.TextAlign = ContentAlignment.MiddleLeft;
            btn.Padding = new Padding(10, 0, 0, 0);
            btn.Click += (s, e) => SwitchView(viewIdx);
            pnlNav.Controls.Add(btn);
            navButtons.Add(btn);
        }

        private void SwitchView(int viewIdx)
        {
            currentView = viewIdx;
            
            bool isDashboard = (viewIdx == -1);
            pnlLeft.Visible = isDashboard;
            pnlMiddle.Visible = isDashboard;
            previewPanel.Visible = isDashboard;
            tabControl1.Visible = !isDashboard;

            if (!isDashboard)
            {
                // TabControl indices (based on InitializeComponent order/indices)
                // Graphics Ripper = 0, Profiles = 1, Options = 2, Quantize = 3, Mass = 4, Mode 7 = 5, BPP = 6, HDMA = 7
                // Wait, let's verify the actual indices from designer.
                tabControl1.SelectedIndex = viewIdx;
            }

            // Update button styles
            foreach (var btn in navButtons)
            {
                bool active = false;
                if (isDashboard && btn.Text == "Digitizer") active = true;
                else if (!isDashboard && btn.Text != "Digitizer")
                {
                    // Match button text to tab text (simplified)
                    if (btn.Text == "HDMA Ripper" && viewIdx == 7) active = true;
                    if (btn.Text == "Mode 7" && viewIdx == 5) active = true;
                    if (btn.Text == "Mass Conv" && viewIdx == 2) active = true;
                    if (btn.Text == "8BPP Conv" && viewIdx == 4) active = true;
                    if (btn.Text == "Profiles" && viewIdx == 6) active = true;
                    if (btn.Text == "Options" && viewIdx == 0) active = true;
                }
                btn.BackColor = active ? Color.FromArgb(0, 122, 204) : Color.Transparent;
            }
        }

        private void ApplyDarkMode()
        {
            this.BackColor = Color.FromArgb(30, 30, 30);
            this.ForeColor = Color.White;

            foreach (Control c in this.Controls)
            {
                UpdateControlColors(c);
            }
        }

        private void ApplyLivePreviewUI()
        {
            this.MinimumSize = new Size(950, 400);
            this.Width = 950;
            
            previewPanel = new Panel();
            previewPanel.Location = new Point(480, 40);
            previewPanel.Size = new Size(440, this.ClientSize.Height - 50);
            previewPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            previewPanel.AutoScroll = true;
            previewPanel.BackColor = Color.FromArgb(20, 20, 20);
            previewPanel.BorderStyle = BorderStyle.FixedSingle;

            // Preview controls
            Panel pnlPreviewControls = new Panel() { Dock = DockStyle.Top, Height = 30, BackColor = Color.FromArgb(45, 45, 48) };
            Label lblZoom = new Label() { Text = "Zoom:", ForeColor = Color.White, Location = new Point(5, 7), AutoSize = true };
            ComboBox cbZoom = new ComboBox() { Location = new Point(50, 4), Size = new Size(60, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            cbZoom.Items.AddRange(new object[] { "1x", "2x", "4x", "8x" });
            cbZoom.SelectedIndex = 1; // 2x
            cbZoom.SelectedIndexChanged += (s, e) => {
                previewZoom = 1 << cbZoom.SelectedIndex;
                UpdatePreviewImage();
            };

            chkGrid = new CheckBox() { Text = "Grid", ForeColor = Color.White, Location = new Point(130, 6), AutoSize = true };
            chkGrid.CheckedChanged += (s, e) => UpdatePreviewImage(); // TODO: Implement grid rendering

            pnlPreviewControls.Controls.Add(lblZoom);
            pnlPreviewControls.Controls.Add(cbZoom);
            pnlPreviewControls.Controls.Add(chkGrid);
            previewPanel.Controls.Add(pnlPreviewControls);

            previewBox = new PictureBox();
            previewBox.SizeMode = PictureBoxSizeMode.AutoSize;
            previewBox.Location = new Point(0, 30);

            previewPanel.Controls.Add(previewBox);
            this.Controls.Add(previewPanel);

            // Hide redundant elements from Ripper tab
            button5.Visible = false; // Old Preview button
            if (checkBox4 != null) checkBox4.Visible = false; // Arrange Colors (moved to Quant tab)
            if (checkBox10 != null) checkBox10.Visible = false; // CGADSUB (moved to Quant tab)

            debounceTimer = new System.Windows.Forms.Timer();
            debounceTimer.Interval = 250;
            debounceTimer.Tick += (s, e) => {
                debounceTimer.Stop();
                if (textBox1.Tag != null && !isProcessing) {
                    Ripper_ProcessSystem(true);
                }
            };

            // Hook up live preview events
            numPalettesUD.ValueChanged += (s, e) => UpdateLivePreview();
            colorsPerPaletteUD.ValueChanged += (s, e) => UpdateLivePreview();
            checkBox4.CheckedChanged += (s, e) => UpdateLivePreview();
            checkBox9.CheckedChanged += (s, e) => UpdateLivePreview();
            comboBox4.SelectedIndexChanged += (s, e) => UpdateLivePreview();
            comboBox5.SelectedIndexChanged += (s, e) => UpdateLivePreview();

            // Setup Profiles Tab
            tabPage2.BackColor = this.BackColor;

            Button btnSaveProfile = new Button();
            btnSaveProfile.Text = "Save Current Profile";
            btnSaveProfile.Size = new Size(180, 40);
            btnSaveProfile.Location = new Point(20, 20);
            btnSaveProfile.Click += (s, e) => SaveProfile();
            btnSaveProfile.BackColor = Color.FromArgb(45, 45, 48);
            btnSaveProfile.ForeColor = Color.White;
            btnSaveProfile.FlatStyle = FlatStyle.Flat;
            tabPage2.Controls.Add(btnSaveProfile);

            Button btnLoadProfile = new Button();
            btnLoadProfile.Text = "Load Existing Profile";
            btnLoadProfile.Size = new Size(180, 40);
            btnLoadProfile.Location = new Point(20, 70);
            btnLoadProfile.Click += (s, e) => LoadProfile();
            btnLoadProfile.BackColor = Color.FromArgb(45, 45, 48);
            btnLoadProfile.ForeColor = Color.White;
            btnLoadProfile.FlatStyle = FlatStyle.Flat;
            tabPage2.Controls.Add(btnLoadProfile);

            Label lblProfileHint = new Label();
            lblProfileHint.Text = "Profiles store your Quantization, Palette, and Codec settings.";
            lblProfileHint.Location = new Point(20, 130);
            lblProfileHint.AutoSize = true;
            lblProfileHint.ForeColor = Color.Gray;
            tabPage2.Controls.Add(lblProfileHint);
        }

        private int previewZoom = 2;
        private byte[] lastProcessedGfx;
        private Color[] lastProcessedPalette;
        private int lastProcessedFormat;
        private bool isProcessing = false;
        private CheckBox chkGrid;

        private void UpdateLivePreview()
        {
            if (isProcessing) return;

            string[] files = textBox1.Tag as string[];
            if (files != null && files.Length == 1)
            {
                // Restart debounce timer to prevent lag when dragging sliders
                debounceTimer.Stop();
                debounceTimer.Start();
            }
            else if (lastProcessedGfx != null)
            {
                UpdatePreviewImage();
            }
        }

        private void UpdatePreviewImage()
        {
            if (lastProcessedGfx == null) return;

            Bitmap bmp = GfxEngine.RenderGfx(lastProcessedGfx, lastProcessedPalette, lastProcessedFormat);
            if (bmp != null)
            {
                if (previewZoom > 1)
                {
                    Bitmap scaled = new Bitmap(bmp.Width * previewZoom, bmp.Height * previewZoom, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(scaled))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(bmp, 0, 0, scaled.Width, scaled.Height);

                        // Draw grid
                        if (chkGrid.Checked)
                        {
                            using (Pen p = new Pen(Color.FromArgb(128, 255, 255, 255)))
                            {
                                p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
                                for (int x = 0; x <= bmp.Width; x += 8)
                                    g.DrawLine(p, x * previewZoom, 0, x * previewZoom, scaled.Height);
                                for (int y = 0; y <= bmp.Height; y += 8)
                                    g.DrawLine(p, 0, y * previewZoom, scaled.Width, y * previewZoom);
                            }
                        }
                    }
                    previewBox.Image = scaled;
                }
                else
                {
                    previewBox.Image = bmp;
                }
            }
        }

        public class SettingsProfile
        {
            public bool OptimizeImage { get; set; }
            public bool AllowTransparency { get; set; }
            public bool Rescale { get; set; }
            public bool ArrangeColors { get; set; }
            public bool Cgadsub { get; set; }
            public bool RemoveDuplicates { get; set; }
            public bool IgnoreFlipped { get; set; }
            public int CodecType { get; set; }
            public int OutputType { get; set; }
            public int SplitType { get; set; }
            public int PaletteOutput { get; set; }
            public decimal NumPalettes { get; set; }
            public decimal ColorsPerPalette { get; set; }
        }

        private void SaveProfile()
        {
            var p = new SettingsProfile();
            p.OptimizeImage = checkBox5.Checked;
            p.AllowTransparency = checkBox7.Checked;
            p.Rescale = checkBox3.Checked;
            p.ArrangeColors = checkBox4.Checked;
            p.Cgadsub = checkBox10.Checked;
            p.RemoveDuplicates = checkBox2.Checked;
            p.IgnoreFlipped = checkBox9.Checked;
            p.CodecType = comboBox1.SelectedIndex;
            p.OutputType = comboBox4.SelectedIndex;
            p.SplitType = comboBox3.SelectedIndex;
            p.PaletteOutput = comboBox5.SelectedIndex;
            p.NumPalettes = numPalettesUD.Value;
            p.ColorsPerPalette = colorsPerPaletteUD.Value;

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "XML Profile (*.xml)|*.xml";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                using (var w = new StreamWriter(sfd.FileName))
                {
                    XmlSerializer x = new XmlSerializer(typeof(SettingsProfile));
                    x.Serialize(w, p);
                }
            }
        }

        private void LoadProfile()
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "XML Profile (*.xml)|*.xml";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                using (var r = new StreamReader(ofd.FileName))
                {
                    XmlSerializer x = new XmlSerializer(typeof(SettingsProfile));
                    var p = (SettingsProfile)x.Deserialize(r);

                    checkBox5.Checked = p.OptimizeImage;
                    checkBox7.Checked = p.AllowTransparency;
                    checkBox3.Checked = p.Rescale;
                    checkBox4.Checked = p.ArrangeColors;
                    checkBox10.Checked = p.Cgadsub;
                    checkBox2.Checked = p.RemoveDuplicates;
                    checkBox9.Checked = p.IgnoreFlipped;

                    if (p.CodecType >= 0 && p.CodecType < comboBox1.Items.Count) comboBox1.SelectedIndex = p.CodecType;
                    if (p.OutputType >= 0 && p.OutputType < comboBox4.Items.Count) comboBox4.SelectedIndex = p.OutputType;
                    if (p.SplitType >= 0 && p.SplitType < comboBox3.Items.Count && checkBox6.Checked) comboBox3.SelectedIndex = p.SplitType;
                    if (p.PaletteOutput >= 0 && p.PaletteOutput < comboBox5.Items.Count) comboBox5.SelectedIndex = p.PaletteOutput;

                    try { numPalettesUD.Value = p.NumPalettes; } catch {}
                    try { colorsPerPaletteUD.Value = p.ColorsPerPalette; } catch {}
                }
                UpdateLivePreview();
            }
        }

        private void UpdateControlColors(Control c)
        {
            c.ForeColor = Color.White;

            if (c is GroupBox || c is CheckBox || c is Label || c is TabControl || c is TabPage || c is RadioButton)
            {
                c.BackColor = Color.FromArgb(30, 30, 30);
            }
            else if (c is Button || c is ComboBox || c is TextBox || c is TextBoxBase || c is DomainUpDown || c is NumericUpDown || c is ProgressBar)
            {
                if (c == this.button2)
                    c.BackColor = Color.FromArgb(0, 122, 204);
                else
                    c.BackColor = Color.FromArgb(45, 45, 48);
            }
            
            foreach (Control child in c.Controls)
            {
                UpdateControlColors(child);
            }
        }

        private void SetupTooltips()
        {
            ToolTip tt = new ToolTip();
            tt.SetToolTip(checkBox1, "Use a more precise (but slower) color matching algorithm during reduction.");
            tt.SetToolTip(checkBox4, "Sort the colors by Hue, Brightness or Saturation before generating the final palette.");
            tt.SetToolTip(checkBox3, "Allows changing the width/height resolution of the source image before processing.");
            tt.SetToolTip(checkBox2, "If checked, any identical 8x8 tiles will only be mapped once, saving SNES VRAM space.");
            tt.SetToolTip(checkBox5, "Checks for completely blank/transparent tiles and excludes them from the export.");
            tt.SetToolTip(checkBox9, "Tries to deduplicate by checking if a tile is a horizontal or vertical flip of another tile.");
            tt.SetToolTip(checkBox10, "Enables CGADSUB transparency settings when importing back into SMW.");
            tt.SetToolTip(checkBox11, "Offset the final palette indexes. Example: offset 2 shifts from palette 0 to palette 2.");
        }

        private void Main_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void Main_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                if (files.Length == 1 && System.IO.Directory.Exists(files[0]))
                {
                    textBox1.Text = files[0];
                    textBox1.Tag = System.IO.Directory.GetFiles(files[0], "*.*", System.IO.SearchOption.TopDirectoryOnly)
                        .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                }
                else
                {
                    textBox1.Text = files[0];
                    if (files.Length > 1) textBox1.Text = files.Length + " files selected";
                    textBox1.Tag = files;
                }
                EnableOrDisable(true);
                textBox1.SelectionStart = textBox1.Text.Length;
                textBox1.ScrollToCaret();

                UpdateLivePreview();
            }
        }



        private System.Windows.Forms.NumericUpDown numPalettesUD;
        private System.Windows.Forms.Label numPalettesLbl;
        private System.Windows.Forms.NumericUpDown colorsPerPaletteUD;
        private System.Windows.Forms.Label colorsPerPaletteLbl;

        private System.Windows.Forms.NumericUpDown nudTileWidth;
        private System.Windows.Forms.NumericUpDown nudTileHeight;
        private System.Windows.Forms.NumericUpDown nudBitsPerChannel;
        private System.Windows.Forms.NumericUpDown nudFractionOfPixels;
        private System.Windows.Forms.NumericUpDown nudDitherWeight;

        private System.Windows.Forms.RadioButton rbZeroUnique;
        private System.Windows.Forms.RadioButton rbZeroShared;
        private System.Windows.Forms.RadioButton rbZeroTransparentPixels;
        private System.Windows.Forms.RadioButton rbZeroTransparentColor;

        private System.Windows.Forms.RadioButton rbDitherDisabled;
        private System.Windows.Forms.RadioButton rbDitherFast;
        private System.Windows.Forms.RadioButton rbDitherSlow;

        private TextBox txtM7Input;
        private TextBox txtM7OutputGfx;
        private TextBox txtM7OutputMap;

        private PictureBox pbHdmaPreview;
        private CheckBox chkHdmaGray;
        private RadioButton rbHdmaAvg;
        private RadioButton rbHdmaLum;
        private CheckBox chkHdmaNeg;
        private CheckBox chkHdmaSepia;
        private CheckBox chkHdmaCompress;
        private TextBox txtHdmaDest;
        private TextBox txtHdmaOutColdata;
        private TextBox txtHdmaOutPalette;
        private Bitmap hdmaSourceBmp;

        private System.Windows.Forms.RadioButton rbPatternDiag4;
        private System.Windows.Forms.RadioButton rbPatternDiag2;
        private System.Windows.Forms.RadioButton rbPatternHoriz4;
        private System.Windows.Forms.RadioButton rbPatternHoriz2;
        private System.Windows.Forms.RadioButton rbPatternVert4;
        private System.Windows.Forms.RadioButton rbPatternVert2;

        private System.Windows.Forms.ProgressBar pbQuantize;
        private System.Windows.Forms.Button btnQuantize;

        private System.Windows.Forms.RadioButton rbMassRotate;
        private System.Windows.Forms.RadioButton rbMassScale;
        private System.Windows.Forms.NumericUpDown nudMassFrames;
        private System.Windows.Forms.NumericUpDown nudMassSize;
        private System.Windows.Forms.NumericUpDown nudMassAmount;
        private System.Windows.Forms.TextBox txtMassInput;
        private System.Windows.Forms.TextBox txtMassOutput;
        private System.Windows.Forms.ComboBox cbMassFormat;
        private System.Windows.Forms.Label lblMassAmount;

        private System.Windows.Forms.ComboBox cbBPPIn;
        private System.Windows.Forms.ComboBox cbBPPOut;
        private System.Windows.Forms.NumericUpDown nudBPPOffset;
        private System.Windows.Forms.TextBox txtBPPInput;
        private System.Windows.Forms.TextBox txtBPPOutput;
        private System.Windows.Forms.CheckBox chkBPPSmart;
        private System.Windows.Forms.TextBox txtBPPPalette;
        private System.Windows.Forms.Button btnBPPPalette;

        private void InitializeQuantizerControls()
        {
            // numPalettesUD
            this.numPalettesUD = new System.Windows.Forms.NumericUpDown();
            this.numPalettesUD.Location = new System.Drawing.Point(120, 70);
            this.numPalettesUD.Maximum = 256;
            this.numPalettesUD.Minimum = 1;
            this.numPalettesUD.Value = Options.NumPalettes;
            this.numPalettesUD.ValueChanged += (s, e) => Options.NumPalettes = (int)numPalettesUD.Value;
            tabPage4.Controls.Add(numPalettesUD);

            Label lblPalettes = new Label() { Text = "Palettes:", Location = new Point(10, 72), AutoSize = true };
            tabPage4.Controls.Add(lblPalettes);

            // colorsPerPaletteUD
            this.colorsPerPaletteUD = new System.Windows.Forms.NumericUpDown();
            this.colorsPerPaletteUD.Location = new System.Drawing.Point(120, 100);
            this.colorsPerPaletteUD.Maximum = 256;
            this.colorsPerPaletteUD.Minimum = 2;
            this.colorsPerPaletteUD.Value = Options.ColorsPerPalette;
            this.colorsPerPaletteUD.ValueChanged += (s, e) => Options.ColorsPerPalette = (int)colorsPerPaletteUD.Value;
            tabPage4.Controls.Add(colorsPerPaletteUD);

            Label lblCpp = new Label() { Text = "Colors per palette:", Location = new Point(10, 102), AutoSize = true };
            tabPage4.Controls.Add(lblCpp);

            // Tile Width/Height
            this.nudTileWidth = new NumericUpDown() { Location = new Point(120, 10), Value = Options.TileWidth, Minimum = 1, Maximum = 128 };
            this.nudTileWidth.ValueChanged += (s, e) => Options.TileWidth = (int)nudTileWidth.Value;
            tabPage4.Controls.Add(nudTileWidth);
            tabPage4.Controls.Add(new Label() { Text = "Tile width:", Location = new Point(10, 12), AutoSize = true });

            this.nudTileHeight = new NumericUpDown() { Location = new Point(120, 40), Value = Options.TileHeight, Minimum = 1, Maximum = 128 };
            this.nudTileHeight.ValueChanged += (s, e) => Options.TileHeight = (int)nudTileHeight.Value;
            tabPage4.Controls.Add(nudTileHeight);
            tabPage4.Controls.Add(new Label() { Text = "Tile height:", Location = new Point(10, 42), AutoSize = true });

            // Bits per channel
            this.nudBitsPerChannel = new NumericUpDown() { Location = new Point(120, 130), Value = Options.BitsPerChannel, Minimum = 1, Maximum = 8 };
            this.nudBitsPerChannel.ValueChanged += (s, e) => Options.BitsPerChannel = (int)nudBitsPerChannel.Value;
            tabPage4.Controls.Add(nudBitsPerChannel);
            tabPage4.Controls.Add(new Label() { Text = "Bits per channel:", Location = new Point(10, 132), AutoSize = true });

            // Fraction of pixels
            this.nudFractionOfPixels = new NumericUpDown() { Location = new Point(120, 160), Value = (decimal)Options.FractionOfPixels, Minimum = 0.001M, Maximum = 1.0M, DecimalPlaces = 3, Increment = 0.01M };
            this.nudFractionOfPixels.ValueChanged += (s, e) => Options.FractionOfPixels = (double)nudFractionOfPixels.Value;
            tabPage4.Controls.Add(nudFractionOfPixels);
            tabPage4.Controls.Add(new Label() { Text = "Fraction of pixels:", Location = new Point(10, 162), AutoSize = true });

            // Color Zero Behaviour
            GroupBox gbZero = new GroupBox() { Text = "Color index zero behaviour", Location = new Point(10, 190), Size = new Size(450, 120) };
            tabPage4.Controls.Add(gbZero);

            rbZeroUnique = new RadioButton() { Text = "unique", Location = new Point(10, 20), Checked = Options.ColorZeroBehaviour == 0 };
            rbZeroShared = new RadioButton() { Text = "shared color:", Location = new Point(10, 45), Checked = Options.ColorZeroBehaviour == 1 };
            rbZeroTransparentPixels = new RadioButton() { Text = "transparent, from transparent pixels", Location = new Point(10, 70), Checked = Options.ColorZeroBehaviour == 2 };
            rbZeroTransparentColor = new RadioButton() { Text = "transparent color:", Location = new Point(10, 95), Checked = Options.ColorZeroBehaviour == 3 };

            rbZeroUnique.CheckedChanged += (s, e) => { if (rbZeroUnique.Checked) Options.ColorZeroBehaviour = 0; };
            rbZeroShared.CheckedChanged += (s, e) => { if (rbZeroShared.Checked) Options.ColorZeroBehaviour = 1; };
            rbZeroTransparentPixels.CheckedChanged += (s, e) => { if (rbZeroTransparentPixels.Checked) Options.ColorZeroBehaviour = 2; };
            rbZeroTransparentColor.CheckedChanged += (s, e) => { if (rbZeroTransparentColor.Checked) Options.ColorZeroBehaviour = 3; };

            gbZero.Controls.Add(rbZeroUnique);
            gbZero.Controls.Add(rbZeroShared);
            gbZero.Controls.Add(rbZeroTransparentPixels);
            gbZero.Controls.Add(rbZeroTransparentColor);

            // Dithering
            GroupBox gbDither = new GroupBox() { Text = "Dithering", Location = new Point(10, 320), Size = new Size(450, 80) };
            tabPage4.Controls.Add(gbDither);

            rbDitherDisabled = new RadioButton() { Text = "disabled", Location = new Point(10, 20), Checked = Options.DitherMode == 0 };
            rbDitherFast = new RadioButton() { Text = "fast", Location = new Point(100, 20), Checked = Options.DitherMode == 1 };
            rbDitherSlow = new RadioButton() { Text = "slow", Location = new Point(190, 20), Checked = Options.DitherMode == 2 };

            rbDitherDisabled.CheckedChanged += (s, e) => { if (rbDitherDisabled.Checked) Options.DitherMode = 0; };
            rbDitherFast.CheckedChanged += (s, e) => { if (rbDitherFast.Checked) Options.DitherMode = 1; };
            rbDitherSlow.CheckedChanged += (s, e) => { if (rbDitherSlow.Checked) Options.DitherMode = 2; };

            gbDither.Controls.Add(rbDitherDisabled);
            gbDither.Controls.Add(rbDitherFast);
            gbDither.Controls.Add(rbDitherSlow);

            nudDitherWeight = new NumericUpDown() { Location = new Point(120, 50), Value = (decimal)Options.DitherWeight, Minimum = 0.0M, Maximum = 1.0M, DecimalPlaces = 2, Increment = 0.05M };
            nudDitherWeight.ValueChanged += (s, e) => Options.DitherWeight = (double)nudDitherWeight.Value;
            gbDither.Controls.Add(nudDitherWeight);
            gbDither.Controls.Add(new Label() { Text = "Dither weight:", Location = new Point(10, 52), AutoSize = true });

            // Dither Pattern
            GroupBox gbPattern = new GroupBox() { Text = "Dither pattern", Location = new Point(10, 410), Size = new Size(450, 100) };
            tabPage4.Controls.Add(gbPattern);

            rbPatternDiag4 = new RadioButton() { Text = "diagonal4", Location = new Point(10, 20), Checked = Options.DitherPattern == 0 };
            rbPatternDiag2 = new RadioButton() { Text = "diagonal2", Location = new Point(10, 45), Checked = Options.DitherPattern == 3 };
            rbPatternHoriz4 = new RadioButton() { Text = "horizontal4", Location = new Point(150, 20), Checked = Options.DitherPattern == 1 };
            rbPatternHoriz2 = new RadioButton() { Text = "horizontal2", Location = new Point(150, 45), Checked = Options.DitherPattern == 4 };
            rbPatternVert4 = new RadioButton() { Text = "vertical4", Location = new Point(290, 20), Checked = Options.DitherPattern == 2 };
            rbPatternVert2 = new RadioButton() { Text = "vertical2", Location = new Point(290, 45), Checked = Options.DitherPattern == 5 };

            rbPatternDiag4.CheckedChanged += (s, e) => { if (rbPatternDiag4.Checked) Options.DitherPattern = 0; };
            rbPatternHoriz4.CheckedChanged += (s, e) => { if (rbPatternHoriz4.Checked) Options.DitherPattern = 1; };
            rbPatternVert4.CheckedChanged += (s, e) => { if (rbPatternVert4.Checked) Options.DitherPattern = 2; };
            rbPatternDiag2.CheckedChanged += (s, e) => { if (rbPatternDiag2.Checked) Options.DitherPattern = 3; };
            rbPatternHoriz2.CheckedChanged += (s, e) => { if (rbPatternHoriz2.Checked) Options.DitherPattern = 4; };
            rbPatternVert2.CheckedChanged += (s, e) => { if (rbPatternVert2.Checked) Options.DitherPattern = 5; };

            gbPattern.Controls.Add(rbPatternDiag4);
            gbPattern.Controls.Add(rbPatternDiag2);
            gbPattern.Controls.Add(rbPatternHoriz4);
            gbPattern.Controls.Add(rbPatternHoriz2);
            gbPattern.Controls.Add(rbPatternVert4);
            gbPattern.Controls.Add(rbPatternVert2);

            // Quantize Button
            this.btnQuantize = new Button() { Text = "Quantize", Location = new Point(10, 520), Size = new Size(100, 30) };
            this.btnQuantize.Click += (s, e) => Ripper_ProcessSystem();
            tabPage4.Controls.Add(btnQuantize);

            this.pbQuantize = new ProgressBar() { Location = new Point(120, 525), Size = new Size(330, 20) };
            tabPage4.Controls.Add(pbQuantize);

            // Add Sort Palette and CGADSUB to Quantization tab
            CheckBox chkArrange = new CheckBox() { Text = "Sort Palette", Location = new Point(10, 410), AutoSize = true, Checked = Options.OrderPalette };
            chkArrange.CheckedChanged += (s, e) => { Options.OrderPalette = chkArrange.Checked; checkBox4.Checked = chkArrange.Checked; UpdateLivePreview(); };
            tabPage4.Controls.Add(chkArrange);

            Button btnSortSettings = new Button() { Text = "Sort Settings...", Location = new Point(130, 407), Size = new Size(110, 24) };
            btnSortSettings.Click += (s, e) => {
                using (var form = new ColorSortSettings())
                {
                    form.ShowDialog();
                    UpdateLivePreview();
                }
            };
            tabPage4.Controls.Add(btnSortSettings);

            CheckBox chkCgadsub = new CheckBox() { Text = "CGADSUB (Color Math)", Location = new Point(150, 410), AutoSize = true, Checked = Options.CGADSUB };
            chkCgadsub.CheckedChanged += (s, e) => { Options.CGADSUB = chkCgadsub.Checked; checkBox10.Checked = chkCgadsub.Checked; UpdateLivePreview(); };
            tabPage4.Controls.Add(chkCgadsub);
        }

        private void InitializeMassRotateControls()
        {
            // Operation Group
            GroupBox gbOp = new GroupBox() { Text = "Operation", Location = new Point(10, 10), Size = new Size(200, 50) };
            rbMassRotate = new RadioButton() { Text = "Rotate", Location = new Point(10, 20), Checked = true };
            rbMassScale = new RadioButton() { Text = "Scale", Location = new Point(100, 20) };
            gbOp.Controls.Add(rbMassRotate);
            gbOp.Controls.Add(rbMassScale);
            tabPage5.Controls.Add(gbOp);

            lblMassAmount = new Label() { Text = "Total Degrees:", Location = new Point(10, 72), AutoSize = true };
            tabPage5.Controls.Add(lblMassAmount);
            nudMassAmount = new NumericUpDown() { Location = new Point(120, 70), Maximum = 36000, Minimum = -36000, Value = 360 };
            tabPage5.Controls.Add(nudMassAmount);

            rbMassRotate.CheckedChanged += (s, e) => { if (rbMassRotate.Checked) lblMassAmount.Text = "Total Degrees:"; };
            rbMassScale.CheckedChanged += (s, e) => { if (rbMassScale.Checked) lblMassAmount.Text = "New Size:"; };

            tabPage5.Controls.Add(new Label() { Text = "Frames:", Location = new Point(10, 102), AutoSize = true });
            nudMassFrames = new NumericUpDown() { Location = new Point(120, 100), Maximum = 1024, Minimum = 1, Value = 16 };
            tabPage5.Controls.Add(nudMassFrames);

            tabPage5.Controls.Add(new Label() { Text = "Original Size:", Location = new Point(10, 132), AutoSize = true });
            nudMassSize = new NumericUpDown() { Location = new Point(120, 130), Maximum = 128, Minimum = 8, Value = 32, Increment = 8 };
            tabPage5.Controls.Add(nudMassSize);

            tabPage5.Controls.Add(new Label() { Text = "Format:", Location = new Point(10, 162), AutoSize = true });
            cbMassFormat = new ComboBox() { Location = new Point(120, 160), DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (IBitformat format in SnesGFX.AvaiableFormats) cbMassFormat.Items.Add(format.Name);
            cbMassFormat.SelectedIndex = 0;
            tabPage5.Controls.Add(cbMassFormat);

            tabPage5.Controls.Add(new Label() { Text = "Input File:", Location = new Point(10, 202), AutoSize = true });
            txtMassInput = new TextBox() { Location = new Point(120, 200), Size = new Size(240, 20) };
            tabPage5.Controls.Add(txtMassInput);
            Button btnBrowseInput = new Button() { Text = "...", Location = new Point(370, 198), Size = new Size(30, 24) };
            btnBrowseInput.Click += (s, e) => {
                OpenFileDialog ofd = new OpenFileDialog() { Filter = "SNES GFX (*.bin;*.clr;*.gfx;*.pc7)|*.bin;*.clr;*.gfx;*.pc7|All files (*.*)|*.*" };
                if (ofd.ShowDialog() == DialogResult.OK) txtMassInput.Text = ofd.FileName;
            };
            tabPage5.Controls.Add(btnBrowseInput);

            tabPage5.Controls.Add(new Label() { Text = "Output File:", Location = new Point(10, 232), AutoSize = true });
            txtMassOutput = new TextBox() { Location = new Point(120, 230), Size = new Size(240, 20) };
            tabPage5.Controls.Add(txtMassOutput);
            Button btnBrowseOutput = new Button() { Text = "...", Location = new Point(370, 228), Size = new Size(30, 24) };
            btnBrowseOutput.Click += (s, e) => {
                SaveFileDialog sfd = new SaveFileDialog() { Filter = "SNES GFX (*.bin;*.clr;*.gfx;*.pc7)|*.bin;*.clr;*.gfx;*.pc7|All files (*.*)|*.*" };
                if (sfd.ShowDialog() == DialogResult.OK) txtMassOutput.Text = sfd.FileName;
            };
            tabPage5.Controls.Add(btnBrowseOutput);

            Button btnMassProcess = new Button() { Text = "Process MassRotate", Location = new Point(10, 270), Size = new Size(150, 40) };
            btnMassProcess.Click += (s, e) => ProcessMassRotate();
            tabPage5.Controls.Add(btnMassProcess);
        }

        private void ProcessMassRotate()
        {
            if (string.IsNullOrEmpty(txtMassInput.Text) || !File.Exists(txtMassInput.Text))
            {
                MessageBox.Show("Please select a valid input file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrEmpty(txtMassOutput.Text))
            {
                MessageBox.Show("Please select an output file path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                byte[] input = File.ReadAllBytes(txtMassInput.Text);
                byte[] output;
                int format = cbMassFormat.SelectedIndex;
                int frames = (int)nudMassFrames.Value;
                int size = (int)nudMassSize.Value;
                float amount = (float)nudMassAmount.Value;

                if (rbMassScale.Checked)
                {
                    output = MassScaleSNES(input, amount, frames, size, size / 2, size / 2, format);
                }
                else
                {
                    output = MassRotateSNES(input, amount, frames, size, format);
                }

                File.WriteAllBytes(txtMassOutput.Text, output);
                MessageBox.Show("Mass processing complete!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error processing: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private unsafe byte[] MassScaleSNES(byte[] input, float max, int frames, int snesWidth, int centerX, int centerY, int format)
        {
            int mmax = (int)frames;
            if (mmax % snesWidth != 0) mmax += snesWidth - mmax % snesWidth;
            float dt = (max - snesWidth) / (float)(frames - 1);
            int size = snesWidth;

            byte[] rawBmpData = SnesGFX.AvaiableFormats[format].Decode(input);

            using (Bitmap bmp = new Bitmap(snesWidth, snesWidth, System.Drawing.Imaging.PixelFormat.Format8bppIndexed))
            {
                var palette = bmp.Palette;
                for (int i = 0; i < 256; ++i) palette.Entries[i] = Color.FromArgb(i, i, i);
                bmp.Palette = palette;
                var bmpData = bmp.LockBits(new Rectangle(0, 0, snesWidth, snesWidth), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);

                for (int x = 0; x < snesWidth; ++x)
                {
                    for (int y = 0; y < snesWidth; ++y)
                    {
                        int off = y * snesWidth + x;
                        int off2 = y * 128 + x;
                        if (off2 < rawBmpData.Length)
                            *(((byte*)bmpData.Scan0) + off) = rawBmpData[off2];
                    }
                }
                bmp.UnlockBits(bmpData);

                using (Bitmap data = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (Bitmap output = new Bitmap(128, Math.Max(8, (size * mmax) / (128 / size)), System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(output))
                    using (Graphics original = Graphics.FromImage(data))
                    {
                        original.SmoothingMode = g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        original.InterpolationMode = g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        original.PixelOffsetMode = g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                        float rotation = snesWidth;
                        int z = 0;
                        int itemsPerRow = 128 / size;

                        for (int row = 0; z < frames; row++)
                        {
                            for (int col = 0; col < itemsPerRow && z < frames; col++)
                            {
                                original.Clear(Color.Transparent);
                                original.ResetTransform();
                                original.TranslateTransform(centerX, centerY);
                                original.ScaleTransform(rotation / (float)snesWidth, rotation / (float)snesWidth);
                                original.TranslateTransform(-centerX, -centerY);
                                original.DrawImage(bmp, 0, 0, size, size);

                                g.DrawImage(data, size * col, row * size, size, size);
                                z++;
                                if (frames > 1) rotation = snesWidth + dt * z;
                            }
                        }
                    }

                    var bmpDataOut = output.LockBits(new Rectangle(0, 0, output.Width, output.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var pixelData = new byte[output.Width * output.Height * 4];
                    System.Runtime.InteropServices.Marshal.Copy(bmpDataOut.Scan0, pixelData, 0, pixelData.Length);
                    var correctData = new byte[output.Width * output.Height];
                    for (int i = 0; i < pixelData.Length; i += 4) correctData[i >> 2] = pixelData[i + 1];
                    byte[] outputData = SnesGFX.AvaiableFormats[format].Encode(correctData);
                    output.UnlockBits(bmpDataOut);
                    return outputData;
                }
            }
        }

        private unsafe byte[] MassRotateSNES(byte[] db, float max, int frames, int snesWidth, int format)
        {
            int mmax = (int)frames;
            int size = snesWidth;
            float dt = max / (float)frames;
            byte[] rawBmpData = SnesGFX.AvaiableFormats[format].Decode(db);

            using (Bitmap bmp = new Bitmap(snesWidth, snesWidth, System.Drawing.Imaging.PixelFormat.Format8bppIndexed))
            {
                var palette = bmp.Palette;
                for (int i = 0; i < 256; ++i) palette.Entries[i] = Color.FromArgb(i, i, i);
                bmp.Palette = palette;
                var bmpData = bmp.LockBits(new Rectangle(0, 0, snesWidth, snesWidth), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);

                for (int x = 0; x < snesWidth; ++x)
                {
                    for (int y = 0; y < snesWidth; ++y)
                    {
                        int off = y * snesWidth + x;
                        int off2 = y * 128 + x;
                        if (off2 < rawBmpData.Length)
                            *(((byte*)bmpData.Scan0) + off) = rawBmpData[off2];
                    }
                }
                bmp.UnlockBits(bmpData);

                int itemsPerRow = 128 / size;
                using (Bitmap data = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (Bitmap output = new Bitmap(128, Math.Max(8, (size * mmax) / itemsPerRow), System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(output))
                    using (Graphics original = Graphics.FromImage(data))
                    {
                        original.SmoothingMode = g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        original.InterpolationMode = g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        original.PixelOffsetMode = g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                        float rotation = 0;
                        float center = (float)size / 2f;
                        int z = 0;

                        for (int row = 0; z < frames; row++)
                        {
                            for (int col = 0; col < itemsPerRow && z < frames; col++)
                            {
                                original.Clear(Color.Transparent);
                                original.ResetTransform();
                                original.TranslateTransform(center, center);
                                original.RotateTransform(rotation);
                                original.TranslateTransform(-center, -center);
                                original.DrawImage(bmp, 0, 0, size, size);
                                g.DrawImage(data, size * col, row * size, size, size);

                                z++;
                                rotation = dt * z;
                            }
                        }
                    }

                    var bmpDataOut = output.LockBits(new Rectangle(0, 0, output.Width, output.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    var pixelData = new byte[output.Width * output.Height * 4];
                    System.Runtime.InteropServices.Marshal.Copy(bmpDataOut.Scan0, pixelData, 0, pixelData.Length);
                    var correctData = new byte[Math.Max(8192, output.Width * output.Height)];
                    for (int i = 0; i < pixelData.Length; i += 4) correctData[i >> 2] = pixelData[i + 1];
                    byte[] outputData = SnesGFX.AvaiableFormats[format].Encode(correctData);
                    output.UnlockBits(bmpDataOut);
                    return outputData;
                }
            }
        }

        private void InitializeBPPConverterControls()
        {
            // Input settings
            GroupBox gbInput = new GroupBox() { Text = "Input Settings", Location = new Point(10, 10), Size = new Size(210, 110) };
            gbInput.Controls.Add(new Label() { Text = "BPP Format:", Location = new Point(10, 25), AutoSize = true });
            cbBPPIn = new ComboBox() { Location = new Point(100, 22), Size = new Size(100, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            cbBPPIn.Items.AddRange(new object[] { "2 BPP", "3 BPP", "4 BPP", "7 (Linear)", "8 BPP" });
            cbBPPIn.SelectedIndex = 2; // Default 4 BPP
            gbInput.Controls.Add(cbBPPIn);

            gbInput.Controls.Add(new Label() { Text = "File:", Location = new Point(10, 55), AutoSize = true });
            txtBPPInput = new TextBox() { Location = new Point(10, 75), Size = new Size(160, 20) };
            gbInput.Controls.Add(txtBPPInput);
            Button btnBrowseIn = new Button() { Text = "...", Location = new Point(175, 73), Size = new Size(30, 24) };
            btnBrowseIn.Click += (s, e) => {
                OpenFileDialog ofd = new OpenFileDialog() { Filter = "SNES GFX (*.bin;*.clr;*.gfx;*.pc7)|*.bin;*.clr;*.gfx;*.pc7|All files (*.*)|*.*" };
                if (ofd.ShowDialog() == DialogResult.OK) txtBPPInput.Text = ofd.FileName;
            };
            gbInput.Controls.Add(btnBrowseIn);
            tabPage7.Controls.Add(gbInput);

            // Output settings
            GroupBox gbOutput = new GroupBox() { Text = "Output Settings", Location = new Point(230, 10), Size = new Size(210, 110) };
            gbOutput.Controls.Add(new Label() { Text = "BPP Format:", Location = new Point(10, 25), AutoSize = true });
            cbBPPOut = new ComboBox() { Location = new Point(100, 22), Size = new Size(100, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            cbBPPOut.Items.AddRange(new object[] { "2 BPP", "3 BPP", "4 BPP", "7 (Linear)", "8 BPP" });
            cbBPPOut.SelectedIndex = 0; // Default 2 BPP
            gbOutput.Controls.Add(cbBPPOut);

            gbOutput.Controls.Add(new Label() { Text = "File:", Location = new Point(10, 55), AutoSize = true });
            txtBPPOutput = new TextBox() { Location = new Point(10, 75), Size = new Size(160, 20) };
            gbOutput.Controls.Add(txtBPPOutput);
            Button btnBrowseOut = new Button() { Text = "...", Location = new Point(175, 73), Size = new Size(30, 24) };
            btnBrowseOut.Click += (s, e) => {
                SaveFileDialog sfd = new SaveFileDialog() { Filter = "SNES GFX (*.bin;*.clr;*.gfx;*.pc7)|*.bin;*.clr;*.gfx;*.pc7|All files (*.*)|*.*" };
                if (sfd.ShowDialog() == DialogResult.OK) txtBPPOutput.Text = sfd.FileName;
            };
            gbOutput.Controls.Add(btnBrowseOut);
            tabPage7.Controls.Add(gbOutput);

            // Options
            GroupBox gbOptions = new GroupBox() { Text = "Options", Location = new Point(10, 130), Size = new Size(430, 70) };
            gbOptions.Controls.Add(new Label() { Text = "Color Offset:", Location = new Point(10, 25), AutoSize = true });
            nudBPPOffset = new NumericUpDown() { Location = new Point(100, 22), Maximum = 255, Minimum = 0, Value = 0 };
            gbOptions.Controls.Add(nudBPPOffset);

            chkBPPSmart = new CheckBox() { Text = "Smart Quantization (Requires Palette)", Location = new Point(220, 22), AutoSize = true };
            gbOptions.Controls.Add(chkBPPSmart);
            
            gbOptions.Controls.Add(new Label() { Text = "Ref Palette:", Location = new Point(10, 48), AutoSize = true });
            txtBPPPalette = new TextBox() { Location = new Point(100, 45), Size = new Size(280, 20), Enabled = false };
            gbOptions.Controls.Add(txtBPPPalette);
            btnBPPPalette = new Button() { Text = "...", Location = new Point(385, 43), Size = new Size(30, 24), Enabled = false };
            btnBPPPalette.Click += (s, e) => {
                OpenFileDialog ofd = new OpenFileDialog() { Filter = "Palette Files (*.pal;*.clr;*.tpl)|*.pal;*.clr;*.tpl|All files (*.*)|*.*" };
                if (ofd.ShowDialog() == DialogResult.OK) txtBPPPalette.Text = ofd.FileName;
            };
            gbOptions.Controls.Add(btnBPPPalette);

            chkBPPSmart.CheckedChanged += (s, e) => {
                txtBPPPalette.Enabled = btnBPPPalette.Enabled = chkBPPSmart.Checked;
                nudBPPOffset.Enabled = !chkBPPSmart.Checked;
            };

            tabPage7.Controls.Add(gbOptions);

            // Convert button
            Button btnConvert = new Button() { Text = "Convert Bit-Depth", Location = new Point(10, 210), Size = new Size(430, 50), Font = new Font("Segoe UI", 12, FontStyle.Bold) };
            btnConvert.Click += (s, e) => ProcessBPPConversion();
            tabPage7.Controls.Add(btnConvert);
        }

        private void ProcessBPPConversion()
        {
            if (string.IsNullOrEmpty(txtBPPInput.Text) || !File.Exists(txtBPPInput.Text))
            {
                MessageBox.Show("Please select a valid input file.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (string.IsNullOrEmpty(txtBPPOutput.Text))
            {
                MessageBox.Show("Please select an output file path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                int inBppIdx = cbBPPIn.SelectedIndex;
                int outBppIdx = cbBPPOut.SelectedIndex;
                int inBpp = GetBppValue(inBppIdx);
                int outBpp = GetBppValue(outBppIdx);
                int offset = (int)nudBPPOffset.Value;

                byte[] resultGfx;
                Color[] resultPalette = null;

                if (chkBPPSmart.Checked)
                {
                    if (string.IsNullOrEmpty(txtBPPPalette.Text) || !File.Exists(txtBPPPalette.Text))
                    {
                        MessageBox.Show("Smart conversion requires a reference palette.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // Load source palette
                    Color[] srcPal = PaletteEngine.LoadPalette(txtBPPPalette.Text);
                    if (srcPal == null)
                    {
                        MessageBox.Show("Could not load reference palette.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    QuantizerOptions qOpts = new QuantizerOptions
                    {
                        TileWidth = 8,
                        TileHeight = 8,
                        NumPalettes = (int)numPalettesUD.Value,
                        ColorsPerPalette = (int)colorsPerPaletteUD.Value,
                        BitsPerChannel = (int)nudBitsPerChannel.Value,
                        FractionOfPixels = 0.1,
                        ColorZeroBehaviour = (ColorZeroBehaviour)Options.ColorZeroBehaviour,
                        Dither = (DitherMode)Options.DitherMode,
                        DitherWeight = (double)nudDitherWeight.Value
                    };

                    byte[] inputGfx = File.ReadAllBytes(txtBPPInput.Text);
                    // Map indices to actual codec indices
                    int inFormatIdx = GetCodecIndexForBpp(inBpp);
                    int outFormatIdx = GetCodecIndexForBpp(outBpp);

                    resultGfx = GfxEngine.SmartConvertBpp(inputGfx, srcPal, inFormatIdx, outFormatIdx, qOpts, out resultPalette);
                    
                    // Save new palette too
                    string palOut = Path.ChangeExtension(txtBPPOutput.Text, ".pal");
                    File.WriteAllBytes(palOut, Program.PaletteToMw3(resultPalette, 256));
                }
                else
                {
                    // Legacy bit-shuffling logic
                    resultGfx = ProcessLegacyBPP(txtBPPInput.Text, inBpp, outBpp, offset);
                }

                File.WriteAllBytes(txtBPPOutput.Text, resultGfx);
                
                // Update preview
                lastProcessedGfx = resultGfx;
                lastProcessedPalette = resultPalette;
                lastProcessedFormat = GetCodecIndexForBpp(outBpp);
                UpdateLivePreview();

                MessageBox.Show("Conversion complete!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error converting BPP: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeMode7MapperControls()
        {
            tabPage6.Controls.Clear();

            GroupBox gbInput = new GroupBox() { Text = "Input Image (1024x1024 recommended)", Location = new Point(10, 10), Size = new Size(450, 60) };
            txtM7Input = new TextBox() { Location = new Point(10, 25), Size = new Size(350, 20) };
            Button btnBrowse = new Button() { Text = "...", Location = new Point(370, 23), Size = new Size(70, 24) };
            btnBrowse.Click += (s, e) => {
                OpenFileDialog ofd = new OpenFileDialog() { Filter = "Images|*.bmp;*.png;*.jpg" };
                if (ofd.ShowDialog() == DialogResult.OK) txtM7Input.Text = ofd.FileName;
            };
            gbInput.Controls.Add(txtM7Input);
            gbInput.Controls.Add(btnBrowse);
            tabPage6.Controls.Add(gbInput);

            GroupBox gbOutput = new GroupBox() { Text = "Outputs", Location = new Point(10, 80), Size = new Size(450, 100) };
            gbOutput.Controls.Add(new Label() { Text = "GFX (8bpp):", Location = new Point(10, 25), AutoSize = true });
            txtM7OutputGfx = new TextBox() { Location = new Point(100, 22), Size = new Size(260, 20) };
            gbOutput.Controls.Add(txtM7OutputGfx);

            gbOutput.Controls.Add(new Label() { Text = "Tilemap:", Location = new Point(10, 55), AutoSize = true });
            txtM7OutputMap = new TextBox() { Location = new Point(100, 52), Size = new Size(260, 20) };
            gbOutput.Controls.Add(txtM7OutputMap);

            tabPage6.Controls.Add(gbOutput);

            Button btnProcess = new Button() { Text = "Generate Mode 7 Map", Location = new Point(10, 200), Size = new Size(450, 40) };
            btnProcess.Click += (s, e) => ProcessMode7Map();
            tabPage6.Controls.Add(btnProcess);
        }

        private void ProcessMode7Map()
        {
            try
            {
                if (!File.Exists(txtM7Input.Text)) return;
                using (Bitmap bmp = new Bitmap(txtM7Input.Text))
                {
                    if (bmp.Width != 1024 || bmp.Height != 1024)
                    {
                        if (MessageBox.Show("Image is not 1024x1024. Continue anyway?", "Warning", MessageBoxButtons.YesNo) == DialogResult.No) return;
                    }

                    // For Mode 7, we usually want 8bpp linear data
                    QuantizerOptions qOpts = new QuantizerOptions
                    {
                        TileWidth = 8,
                        TileHeight = 8,
                        NumPalettes = 1,
                        ColorsPerPalette = 256,
                        BitsPerChannel = (int)nudBitsPerChannel.Value,
                        FractionOfPixels = 0.1,
                        ColorZeroBehaviour = ColorZeroBehaviour.Unique,
                        Dither = (DitherMode)Options.DitherMode,
                        DitherWeight = (double)nudDitherWeight.Value
                    };

                    Color[] pal;
                    byte[] gfx = GfxEngine.SmartConvertBpp(GfxEngine.BitmapToIndices(bmp), null, 9, 9, qOpts, out pal);

                    // Generate a linear 128x128 tilemap (each byte is a tile index)
                    byte[] map = new byte[128 * 128];
                    for (int i = 0; i < map.Length; i++) map[i] = (byte)(i & 0xFF);

                    if (!string.IsNullOrEmpty(txtM7OutputGfx.Text)) File.WriteAllBytes(txtM7OutputGfx.Text, gfx);
                    if (!string.IsNullOrEmpty(txtM7OutputMap.Text)) File.WriteAllBytes(txtM7OutputMap.Text, map);

                    // Update preview
                    lastProcessedGfx = gfx;
                    lastProcessedPalette = pal;
                    lastProcessedFormat = 9; // Mode 7
                    UpdateLivePreview();

                    MessageBox.Show("Mode 7 Mapping complete!", "Success");
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void InitializeHDMARipperControls()
        {
            tabPage8.Controls.Clear();

            // Left: Preview
            pbHdmaPreview = new PictureBox() { Location = new Point(10, 10), Size = new Size(150, 450), BorderStyle = BorderStyle.FixedSingle, BackColor = Color.Black };
            tabPage8.Controls.Add(pbHdmaPreview);
            tabPage8.Controls.Add(new Label() { Text = "PC RGB   in SNES", Location = new Point(10, 465), AutoSize = true });

            // Middle: Options
            GroupBox gbHdmaOptions = new GroupBox() { Text = "Options", Location = new Point(170, 10), Size = new Size(200, 200) };
            chkHdmaGray = new CheckBox() { Text = "Turn into grayscale", Location = new Point(10, 20), AutoSize = true };
            rbHdmaAvg = new RadioButton() { Text = "Average", Location = new Point(30, 40), AutoSize = true };
            rbHdmaLum = new RadioButton() { Text = "Luminosity", Location = new Point(30, 60), AutoSize = true, Checked = true };
            chkHdmaNeg = new CheckBox() { Text = "Negative colors", Location = new Point(10, 85), AutoSize = true };
            chkHdmaSepia = new CheckBox() { Text = "Sepia", Location = new Point(10, 105), AutoSize = true };
            chkHdmaCompress = new CheckBox() { Text = "Compress Output", Location = new Point(10, 130), AutoSize = true, Checked = true };
            
            gbHdmaOptions.Controls.Add(chkHdmaGray);
            gbHdmaOptions.Controls.Add(rbHdmaAvg);
            gbHdmaOptions.Controls.Add(rbHdmaLum);
            gbHdmaOptions.Controls.Add(chkHdmaNeg);
            gbHdmaOptions.Controls.Add(chkHdmaSepia);
            gbHdmaOptions.Controls.Add(chkHdmaCompress);

            Label lblDest = new Label() { Text = "Color destination ($2121):", Location = new Point(10, 155), AutoSize = true };
            txtHdmaDest = new TextBox() { Text = "00", Location = new Point(10, 175), Size = new Size(50, 20) };
            Button btnApply = new Button() { Text = "Apply", Location = new Point(70, 173), Size = new Size(70, 24) };
            btnApply.Click += (s, e) => ProcessHDMARipping();
            
            gbHdmaOptions.Controls.Add(lblDest);
            gbHdmaOptions.Controls.Add(txtHdmaDest);
            gbHdmaOptions.Controls.Add(btnApply);
            tabPage8.Controls.Add(gbHdmaOptions);

            Button btnLoadHdma = new Button() { Text = "Load Image", Location = new Point(170, 220), Size = new Size(200, 40) };
            btnLoadHdma.Click += (s, e) => {
                OpenFileDialog ofd = new OpenFileDialog() { Filter = "Images|*.bmp;*.png;*.jpg" };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    hdmaSourceBmp = new Bitmap(ofd.FileName);
                    ProcessHDMARipping();
                }
            };
            tabPage8.Controls.Add(btnLoadHdma);

            // Right: Outputs
            tabPage8.Controls.Add(new Label() { Text = "For register $2132", Location = new Point(380, 10), AutoSize = true });
            txtHdmaOutColdata = new TextBox() { Location = new Point(380, 30), Size = new Size(400, 200), Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9) };
            tabPage8.Controls.Add(txtHdmaOutColdata);

            tabPage8.Controls.Add(new Label() { Text = "For register $2121 and $2122", Location = new Point(380, 240), AutoSize = true });
            txtHdmaOutPalette = new TextBox() { Location = new Point(380, 260), Size = new Size(400, 200), Multiline = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9) };
            tabPage8.Controls.Add(txtHdmaOutPalette);
        }

        private void ProcessHDMARipping()
        {
            if (hdmaSourceBmp == null) return;

            int dest = 0;
            try { dest = int.Parse(txtHdmaDest.Text, System.Globalization.NumberStyles.HexNumber); } catch { }

            var entries = HdmaEngine.RipGradient(hdmaSourceBmp, chkHdmaGray.Checked, rbHdmaLum.Checked, chkHdmaNeg.Checked, chkHdmaSepia.Checked);
            
            txtHdmaOutColdata.Text = HdmaEngine.GenerateColdataTable(entries, chkHdmaCompress.Checked);
            txtHdmaOutPalette.Text = HdmaEngine.GeneratePaletteTable(entries, dest, chkHdmaCompress.Checked);

            // Draw preview
            Bitmap preview = new Bitmap(pbHdmaPreview.Width, pbHdmaPreview.Height);
            using (Graphics g = Graphics.FromImage(preview))
            {
                int y = 0;
                int totalLines = entries.Sum(e => e.Scanlines);
                float scale = (float)preview.Height / totalLines;

                foreach (var entry in entries)
                {
                    int h = (int)(entry.Scanlines * scale);
                    if (h < 1) h = 1;
                    
                    // Original color
                    using (Brush b = new SolidBrush(entry.Color))
                    {
                        g.FillRectangle(b, 0, y, preview.Width / 2, h);
                    }

                    // SNES color
                    ushort snes = HdmaEngine.ToSnesColor(entry.Color);
                    int r = (snes & 0x1F) << 3;
                    int gr = ((snes >> 5) & 0x1F) << 3;
                    int bl = ((snes >> 10) & 0x1F) << 3;
                    using (Brush b = new SolidBrush(Color.FromArgb(r, gr, bl)))
                    {
                        g.FillRectangle(b, preview.Width / 2, y, preview.Width / 2, h);
                    }

                    y += h;
                }
            }
            pbHdmaPreview.Image = preview;
        }

        private int GetBppValue(int index)
        {
            switch (index)
            {
                case 0: return 2;
                case 1: return 3;
                case 2: return 4;
                case 3: return 7;
                case 4: return 8;
                default: return 4;
            }
        }

        private int GetCodecIndexForBpp(int bpp)
        {
            switch (bpp)
            {
                case 2: return 0; // SNES 2BPP
                case 3: return 1; // SNES 3BPP
                case 4: return 2; // SNES 4BPP
                case 8: return 3; // SNES 8BPP
                case 7: return 9; // Mode 7
                default: return 2;
            }
        }

        private byte[] ProcessLegacyBPP(string inputPath, int inBpp, int outBpp, int offset)
        {
            using (FileStream fsIn = new FileStream(inputPath, FileMode.Open, FileAccess.Read))
            using (MemoryStream msOut = new MemoryStream())
            {
                byte[] intile = new byte[64];
                byte[] decodedtile = new byte[64];
                byte[] outtile = new byte[64];

                int inSize = (inBpp == 7) ? 64 : (inBpp * 8);

                while (fsIn.Position < fsIn.Length)
                {
                    if (fsIn.Read(intile, 0, inSize) < inSize) break;

                    // Decode
                    if (inBpp == 2)
                    {
                        for (int xx = 0; xx < 8; xx++)
                            for (int yy = 0; yy < 8; yy++)
                                decodedtile[(yy * 8) + 7 - xx] = (byte)(((intile[yy << 1] >> xx) & 1) + (((intile[(yy << 1) | 1] >> xx) & 1) << 1));
                    }
                    else if (inBpp == 3)
                    {
                        for (int xx = 0; xx < 8; xx++)
                            for (int yy = 0; yy < 8; yy++)
                                decodedtile[(yy * 8) + 7 - xx] = (byte)(((intile[yy << 1] >> xx) & 1) + (((intile[(yy << 1) | 1] >> xx) & 1) << 1) + (((intile[yy | 16] >> xx) & 1) << 2));
                    }
                    else if (inBpp == 4)
                    {
                        for (int xx = 0; xx < 8; xx++)
                            for (int yy = 0; yy < 8; yy++)
                                decodedtile[(yy * 8) + 7 - xx] = (byte)(((intile[yy << 1] >> xx) & 1) + (((intile[(yy << 1) | 1] >> xx) & 1) << 1) + (((intile[(yy << 1) | 16] >> xx) & 1) << 2) + (((intile[(yy << 1) | 17] >> xx) & 1) << 3));
                    }
                    else if (inBpp == 8)
                    {
                        for (int xx = 0; xx < 8; xx++)
                            for (int yy = 0; yy < 8; yy++)
                                decodedtile[(yy * 8) + 7 - xx] = (byte)(((intile[yy << 1] >> xx) & 1) + (((intile[(yy << 1) | 1] >> xx) & 1) << 1) + (((intile[(yy << 1) | 16] >> xx) & 1) << 2) + (((intile[(yy << 1) | 17] >> xx) & 1) << 3) + (((intile[(yy << 1) | 32] >> xx) & 1) << 4) + (((intile[(yy << 1) | 33] >> xx) & 1) << 5) + (((intile[(yy << 1) | 48] >> xx) & 1) << 6) + (((intile[(yy << 1) | 49] >> xx) & 1) << 7));
                    }
                    else if (inBpp == 7)
                    {
                        Array.Copy(intile, decodedtile, 64);
                    }

                    // Apply offset
                    for (int i = 0; i < 64; i++) decodedtile[i] = (byte)((decodedtile[i] + offset) & 0xFF);

                    // Encode
                    if (outBpp == 2)
                    {
                        for (int xx = 0; xx < 8; xx++)
                        {
                            outtile[xx << 1] = (byte)(((decodedtile[xx << 3] & 1) << 7) | ((decodedtile[(xx << 3) | 1] & 1) << 6) | ((decodedtile[(xx << 3) | 2] & 1) << 5) | ((decodedtile[(xx << 3) | 3] & 1) << 4) | ((decodedtile[(xx << 3) | 4] & 1) << 3) | ((decodedtile[(xx << 3) | 5] & 1) << 2) | ((decodedtile[(xx << 3) | 6] & 1) << 1) | (decodedtile[(xx << 3) | 7] & 1));
                            outtile[(xx << 1) | 1] = (byte)(((decodedtile[xx << 3] & 2) << 6) | ((decodedtile[(xx << 3) | 1] & 2) << 5) | ((decodedtile[(xx << 3) | 2] & 2) << 4) | ((decodedtile[(xx << 3) | 3] & 2) << 3) | ((decodedtile[(xx << 3) | 4] & 2) << 2) | ((decodedtile[(xx << 3) | 5] & 2) << 1) | (decodedtile[(xx << 3) | 6] & 2) | ((decodedtile[(xx << 3) | 7] & 2) >> 1));
                        }
                        msOut.Write(outtile, 0, 16);
                    }
                    else if (outBpp == 3)
                    {
                        for (int xx = 0; xx < 8; xx++)
                        {
                            outtile[xx << 1] = (byte)(((decodedtile[xx << 3] & 1) << 7) | ((decodedtile[(xx << 3) | 1] & 1) << 6) | ((decodedtile[(xx << 3) | 2] & 1) << 5) | ((decodedtile[(xx << 3) | 3] & 1) << 4) | ((decodedtile[(xx << 3) | 4] & 1) << 3) | ((decodedtile[(xx << 3) | 5] & 1) << 2) | ((decodedtile[(xx << 3) | 6] & 1) << 1) | (decodedtile[(xx << 3) | 7] & 1));
                            outtile[(xx << 1) | 1] = (byte)(((decodedtile[xx << 3] & 2) << 6) | ((decodedtile[(xx << 3) | 1] & 2) << 5) | ((decodedtile[(xx << 3) | 2] & 2) << 4) | ((decodedtile[(xx << 3) | 3] & 2) << 3) | ((decodedtile[(xx << 3) | 4] & 2) << 2) | ((decodedtile[(xx << 3) | 5] & 2) << 1) | (decodedtile[(xx << 3) | 6] & 2) | ((decodedtile[(xx << 3) | 7] & 2) >> 1));
                            outtile[xx | 16] = (byte)(((decodedtile[xx << 3] & 4) << 5) | ((decodedtile[(xx << 3) | 1] & 4) << 4) | ((decodedtile[(xx << 3) | 2] & 4) << 3) | ((decodedtile[(xx << 3) | 3] & 4) << 2) | ((decodedtile[(xx << 3) | 4] & 4) << 1) | (decodedtile[(xx << 3) | 5] & 4) | ((decodedtile[(xx << 3) | 6] & 4) >> 1) | ((decodedtile[(xx << 3) | 7] & 4) >> 2));
                        }
                        msOut.Write(outtile, 0, 24);
                    }
                    else if (outBpp == 4)
                    {
                        for (int xx = 0; xx < 8; xx++)
                        {
                            outtile[xx << 1] = (byte)(((decodedtile[xx << 3] & 1) << 7) | ((decodedtile[(xx << 3) | 1] & 1) << 6) | ((decodedtile[(xx << 3) | 2] & 1) << 5) | ((decodedtile[(xx << 3) | 3] & 1) << 4) | ((decodedtile[(xx << 3) | 4] & 1) << 3) | ((decodedtile[(xx << 3) | 5] & 1) << 2) | ((decodedtile[(xx << 3) | 6] & 1) << 1) | (decodedtile[(xx << 3) | 7] & 1));
                            outtile[(xx << 1) | 1] = (byte)(((decodedtile[xx << 3] & 2) << 6) | ((decodedtile[(xx << 3) | 1] & 2) << 5) | ((decodedtile[(xx << 3) | 2] & 2) << 4) | ((decodedtile[(xx << 3) | 3] & 2) << 3) | ((decodedtile[(xx << 3) | 4] & 2) << 2) | ((decodedtile[(xx << 3) | 5] & 2) << 1) | (decodedtile[(xx << 3) | 6] & 2) | ((decodedtile[(xx << 3) | 7] & 2) >> 1));
                            outtile[(xx << 1) | 16] = (byte)(((decodedtile[xx << 3] & 4) << 5) | ((decodedtile[(xx << 3) | 1] & 4) << 4) | ((decodedtile[(xx << 3) | 2] & 4) << 3) | ((decodedtile[(xx << 3) | 3] & 4) << 2) | ((decodedtile[(xx << 3) | 4] & 4) << 1) | (decodedtile[(xx << 3) | 5] & 4) | ((decodedtile[(xx << 3) | 6] & 4) >> 1) | ((decodedtile[(xx << 3) | 7] & 4) >> 2));
                            outtile[(xx << 1) | 17] = (byte)(((decodedtile[xx << 3] & 8) << 4) | ((decodedtile[(xx << 3) | 1] & 8) << 3) | ((decodedtile[(xx << 3) | 2] & 8) << 2) | ((decodedtile[(xx << 3) | 3] & 8) << 1) | (decodedtile[(xx << 3) | 4] & 8) | ((decodedtile[(xx << 3) | 5] & 8) >> 1) | ((decodedtile[(xx << 3) | 6] & 8) >> 2) | ((decodedtile[(xx << 3) | 7] & 8) >> 3));
                        }
                        msOut.Write(outtile, 0, 32);
                    }
                    else if (outBpp == 8)
                    {
                        for (int xx = 0; xx < 8; xx++)
                        {
                            outtile[xx << 1] = (byte)(((decodedtile[xx << 3] & 1) << 7) | ((decodedtile[(xx << 3) | 1] & 1) << 6) | ((decodedtile[(xx << 3) | 2] & 1) << 5) | ((decodedtile[(xx << 3) | 3] & 1) << 4) | ((decodedtile[(xx << 3) | 4] & 1) << 3) | ((decodedtile[(xx << 3) | 5] & 1) << 2) | ((decodedtile[(xx << 3) | 6] & 1) << 1) | (decodedtile[(xx << 3) | 7] & 1));
                            outtile[(xx << 1) | 1] = (byte)(((decodedtile[xx << 3] & 2) << 6) | ((decodedtile[(xx << 3) | 1] & 2) << 5) | ((decodedtile[(xx << 3) | 2] & 2) << 4) | ((decodedtile[(xx << 3) | 3] & 2) << 3) | ((decodedtile[(xx << 3) | 4] & 2) << 2) | ((decodedtile[(xx << 3) | 5] & 2) << 1) | (decodedtile[(xx << 3) | 6] & 2) | ((decodedtile[(xx << 3) | 7] & 2) >> 1));
                            outtile[(xx << 1) | 16] = (byte)(((decodedtile[xx << 3] & 4) << 5) | ((decodedtile[(xx << 3) | 1] & 4) << 4) | ((decodedtile[(xx << 3) | 2] & 4) << 3) | ((decodedtile[(xx << 3) | 3] & 4) << 2) | ((decodedtile[(xx << 3) | 4] & 4) << 1) | (decodedtile[(xx << 3) | 5] & 4) | ((decodedtile[(xx << 3) | 6] & 4) >> 1) | ((decodedtile[(xx << 3) | 7] & 4) >> 2));
                            outtile[(xx << 1) | 17] = (byte)(((decodedtile[xx << 3] & 8) << 4) | ((decodedtile[(xx << 3) | 1] & 8) << 3) | ((decodedtile[(xx << 3) | 2] & 8) << 2) | ((decodedtile[(xx << 3) | 3] & 8) << 1) | (decodedtile[(xx << 3) | 4] & 8) | ((decodedtile[(xx << 3) | 5] & 8) >> 1) | ((decodedtile[(xx << 3) | 6] & 8) >> 2) | ((decodedtile[(xx << 3) | 7] & 8) >> 3));
                            outtile[(xx << 1) | 32] = (byte)(((decodedtile[xx << 3] & 16) << 3) | ((decodedtile[(xx << 3) | 1] & 16) << 2) | ((decodedtile[(xx << 3) | 2] & 16) << 1) | (decodedtile[(xx << 3) | 3] & 16) | ((decodedtile[(xx << 3) | 4] & 16) >> 1) | ((decodedtile[(xx << 3) | 5] & 16) >> 2) | ((decodedtile[(xx << 3) | 6] & 16) >> 3) | ((decodedtile[(xx << 3) | 7] & 16) >> 4));
                            outtile[(xx << 1) | 33] = (byte)(((decodedtile[xx << 3] & 32) << 2) | ((decodedtile[(xx << 3) | 1] & 32) << 1) | (decodedtile[(xx << 3) | 2] & 32) | ((decodedtile[(xx << 3) | 3] & 32) >> 1) | ((decodedtile[(xx << 3) | 4] & 32) >> 2) | ((decodedtile[(xx << 3) | 5] & 32) >> 3) | ((decodedtile[(xx << 3) | 6] & 32) >> 4) | ((decodedtile[(xx << 3) | 7] & 32) >> 5));
                            outtile[(xx << 1) | 48] = (byte)(((decodedtile[xx << 3] & 64) << 1) | (decodedtile[(xx << 3) | 1] & 64) | ((decodedtile[(xx << 3) | 2] & 64) >> 1) | ((decodedtile[(xx << 3) | 3] & 64) >> 2) | ((decodedtile[(xx << 3) | 4] & 64) >> 3) | ((decodedtile[(xx << 3) | 5] & 64) >> 4) | ((decodedtile[(xx << 3) | 6] & 64) >> 5) | ((decodedtile[(xx << 3) | 7] & 64) >> 6));
                            outtile[(xx << 1) | 49] = (byte)((decodedtile[xx << 3] & 128) | ((decodedtile[(xx << 3) | 1] & 128) >> 1) | ((decodedtile[(xx << 3) | 2] & 128) >> 2) | ((decodedtile[(xx << 3) | 3] & 128) >> 3) | ((decodedtile[(xx << 3) | 4] & 128) >> 4) | ((decodedtile[(xx << 3) | 5] & 128) >> 5) | ((decodedtile[(xx << 3) | 6] & 128) >> 6) | ((decodedtile[(xx << 3) | 7] & 128) >> 7));
                        }
                        msOut.Write(outtile, 0, 64);
                    }
                    else if (outBpp == 7)
                    {
                        msOut.Write(decodedtile, 0, 64);
                    }
                }
                return msOut.ToArray();
            }
        }

        /// <summary>
        /// Rotina de detectar os codecs.
        /// </summary>
        private void Global_DetectCodecs()
        {
            this.comboBox1.BeginUpdate();

            foreach (IBitformat format in SnesGFX.AvaiableFormats)
            {
                this.comboBox1.Items.Add(format.Name);
            }

            this.comboBox1.EndUpdate();
            this.comboBox1.SelectedIndex = Settings.Default.Codec;
        }

        /// <summary>
        /// Detecta as quantidades de cores que um codec suporta.
        /// </summary>
        private void Ripper_DetectColors()
        {
            int maxColors = SnesGFX.AvaiableFormats[comboBox1.SelectedIndex].Colors;

            this.comboBox2.BeginUpdate();
            this.comboBox2.Items.Clear();

            for (int x = 2; x <= maxColors; x++)
                this.comboBox2.Items.Add(x);

            this.comboBox2.SelectedIndex = this.comboBox2.Items.Count - 1;
            this.comboBox2.EndUpdate();
            
            if (this.colorsPerPaletteUD != null)
            {
                this.colorsPerPaletteUD.Value = new decimal(new int[] { maxColors, 0, 0, 0 });
            }
        }



        private void EnableOrDisable(bool option)
        {
            button5.Enabled = button2.Enabled = button3.Enabled = option;
            
            string[] files = textBox1.Tag as string[];
            if (files != null && files.Length > 1)
            {
                button2.Text = "Process All";
                button5.Enabled = false; // Disable preview for batch mode
            }
            else
            {
                button2.Text = "Save";
            }
        }

        /// <summary>
        /// Rotina de conversão de Imagens para SNES Graphics
        /// </summary>
		private void Ripper_ProcessSystem_Cleanup(bool previewFlag, string reportText)
		{
			if (this.InvokeRequired)
			{
				this.Invoke(new Action(() => Ripper_ProcessSystem_Cleanup(previewFlag, reportText)));
				return;
			}

			if (!previewFlag)
			{
				Application.UseWaitCursor = false;
				EnableOrDisable(true);
			}
			isProcessing = false;

			if (reportText.Length > 0)
				File.WriteAllText("convertlog.txt", reportText);
		}

		private void Ripper_ProcessSystem(bool preview = false)
		{
			if (!init || isProcessing) return;

			string[] files = textBox1.Tag as string[];
			if (files == null) return;

			if (!preview)
			{
				EnableOrDisable(false);
				Application.UseWaitCursor = true;
			}
			isProcessing = true;

			// Capture UI State
			bool chk3 = checkBox3.Checked;
			string txt2 = textBox2.Text;
			int cb2Index = comboBox2.SelectedIndex;
			int cb1Index = comboBox1.SelectedIndex;
			bool isSingleFile = files.Length == 1;
			bool previewFlag = preview;

			ThreadPool.QueueUserWorkItem((state) =>
			{
				StringBuilder report = new StringBuilder();
				try
				{
					foreach (var file in files)
					{
						string name = Regex.Replace(file.Replace(@"\", @"\\"), "[.]\\w{3,4}", "", Program.ropts);

						if (!Options.SaveOnImageFolder && name.LastIndexOf("\\") != -1)
						{
							name = name.Substring(name.LastIndexOf("\\") + 1);
						}

						Bitmap b = null;

						using (FileStream fs = new FileStream(file, FileMode.Open))
						{
							b = new Bitmap(fs);
							fs.Close();

							Program.bitmapInfo = b.Size;
						}

						double scale = (!chk3) ? 1 : (Int32.Parse(txt2) / 100D);
                    
					byte[] result;
					byte[] palette;
					Color[] memPalette;
					string extension;
					byte[] mwl = null;

					Program.BitmapToRAWSNES(b, cb2Index + 2, scale,
						out result, out palette, out extension, out memPalette);

                    // Update preview data
                    lastProcessedGfx = result;
                    lastProcessedPalette = memPalette;
                    lastProcessedFormat = cb1Index;
                    
                    if (this.InvokeRequired)
                        this.Invoke(new Action(() => UpdatePreviewImage()));
                    else
                        UpdatePreviewImage();

					b.Dispose();

					IBitformat codec = SnesGFX.AvaiableFormats[cb1Index];
					byte[] outputTilemap = null;

					if (Options.RemoveDuplicateTiles)
					{
                        int baseSize = 8;

						int[] tilemap;
						bool[] flipx, flipy;
						int theWidth = codec.FixedWidth == 0 ? Program.bitmapInfo.Width :
							codec.FixedWidth;

						int colorsPerPal = Options.ColorsPerPalette;
						SNES.Tile.ColorsPerPalette = colorsPerPal;
						SNES.Tile.CrossPaletteDedup = Options.CrossPaletteDedup;
						SNES.Tile[] tiles = SNES.Tile.FromBitmap(result, theWidth, baseSize, colorsPerPal);
						SNES.Tile[] aTiles = SNES.Tile.RemoveRepeatedBlocks(tiles, Options.RemoveFlippedTiles,
							out tilemap, out flipx, out flipy);
						int nTiles = aTiles.Length;
						// This is needed to some lines don't get cut-off.
						if (nTiles % 0x10 != 0) nTiles += 0x10 - nTiles % 0x10;
						SNES.Tile[] rTiles = new SNES.Tile[nTiles];
						aTiles.CopyTo(rTiles, 0);
						for (int i = aTiles.Length; i < nTiles; ++i) { rTiles[i] = new SNES.Tile(new byte[baseSize* baseSize], baseSize); }
						result = SNES.Tile.ConvertToBitmap(rTiles, theWidth);

						int x_flip_count = 0;
						int y_flip_count = 0;
						int xy_flip_count = 0;

						for (int i = 0; i < flipx.Length; ++i)
						{
							if (flipx[i] && flipy[i])
							{
								++xy_flip_count;
								continue;
							}
							if (flipx[i])
							{
								++x_flip_count;
								continue;
							}
							if (flipy[i])
							{
								++y_flip_count;
								continue;
							}
						}

						string msg =
	@"Total Tiles Before Removing Duplicates:		0x{0:X4}
Total Tiles After Removing Duplicates:		0x{1:X4}

Total X Flipped Tiles:				0x{3:X4}
Total Y Flipped Tiles:				0x{4:X4}
Total X+Y Flipped Tiles:			0x{5:X4}
Total Duplicated Tiles:			0x{6:X4}
Total Removed:				0x{7:X4}

Ratio:					{2:F}%";

						msg = string.Format(CultureInfo.InvariantCulture,
					msg, tiles.Length, rTiles.Length, rTiles.Length / (double)tiles.Length * 100,
					x_flip_count, y_flip_count, xy_flip_count, tiles.Length - rTiles.Length -
					x_flip_count - y_flip_count - xy_flip_count, tiles.Length - rTiles.Length);

						report.AppendLine(name);
						report.AppendLine(msg);
						report.AppendLine();

						if (!previewFlag && isSingleFile)
						{
							this.Invoke(new Action(() => {
								MessageBox.Show(msg, "Anti-Tile Duplicate Results",
									MessageBoxButtons.OK, MessageBoxIcon.Information);
							}));
						}

						for (int i = 0; i < tilemap.Length; ++i)
						{
							tilemap[i] = (tilemap[i] + Options.OffsetTile) & 0x3FF;
						}

						/*if (rTiles.Length > 1024 && Options.TilemapOutput != 3)//Options.GenerateMap16)
						{
							MessageBox.Show("There are more than 1024 (0x400) tiles, thus " +
								"it's impossible to generate a SNES tilemap.", "Error",
								MessageBoxButtons.OK, MessageBoxIcon.Error);
							goto End;
						}*/
						if (Options.TilemapOutput == 0)
						{
							int size = tilemap.Length * 2;
							if (size <= 0x780)
							{
								if (size % 0x780 != 0) size += 0x780 - size % 0x780;
							}
							else
							{
								if (size % 0x800 != 0) size += 0x800 - size % 0x800;
							}
							outputTilemap = new byte[size];

							int w = Program.bitmapInfo.Width;
							int h = Program.bitmapInfo.Height;
							int h2 = h;
							if (h2 % 8 != 0) h2 += 8 - (h2 % 8);
							if (w % 128 != 0) w += 128 - w % 128; // align stack width

							for (int y = 0; y < h; y += 8)
							{
								for (int x = 0; x < w; x += 8)
								{
									if (x >= Program.bitmapInfo.Width) continue; // Skip padding tiles

									// destination: standard SNES tilemap mapping (32x32 screen blocks)
									int dest = (x >> 3 & 31) | ((y >> 3 & 31) << 5) | ((x >> 8 & 1) << 10) | ((y >> 8 & 1) << 11);
									dest <<= 1;

									if (dest >= size) continue;

									// calculate the source index in the stacked layout (reformatted 128px wide columns)
									int fakeY = (y >> 3) + (x >> 7) * (h2 >> 3);
									int source = (x >> 3 & 15) | (fakeY << 4);

									if (source >= tilemap.Length) continue;

									int item = tilemap[source] & 0x3FF;
									item |= (tiles[source].PaletteIndex << 10);
									item += flipy[source] ? 0x8000 : 0;
									item += flipx[source] ? 0x4000 : 0;

									outputTilemap[dest] = (byte)(item & 0xFF);
									outputTilemap[dest + 1] = (byte)(item >> 8 & 0xFF);
								}
							}
						}
						else if (Options.TilemapOutput == 1)
						{
							int w = Program.bitmapInfo.Width;
							int h = Program.bitmapInfo.Height;
							int h2 = h;
							if (h2 % 8 != 0) h2 += 8 - (h2 % 8);
                            
							int mwlWidth = w;
							if (mwlWidth % 16 != 0) mwlWidth += 16 - (mwlWidth % 16);
							int mwlHeight = h;
							if (mwlHeight % 16 != 0) mwlHeight += 16 - (mwlHeight % 16);

							List<int[]> m16opt = new List<int[]>();
							int[] m16index = new int[(mwlWidth / 16) * (mwlHeight / 16)];

							for (int my = 0; my < mwlHeight / 16; my++)
							{
								for (int mx = 0; mx < mwlWidth / 16; mx++)
								{
									int[] group = new int[4];
                                    
									for (int subY = 0; subY < 2; subY++)
									{
										for (int subX = 0; subX < 2; subX++)
										{
											int x = mx * 16 + subX * 8;
											int y = my * 16 + subY * 8;

											if (x >= w || y >= h) continue;

											int fakeY = (y >> 3) + (x >> 7) * (h2 >> 3);
											int source = (x >> 3 & 15) | (fakeY << 4);

											if (source >= tilemap.Length) continue;

											int item = tilemap[source] & 0x3FF;
											item |= (tiles[source].PaletteIndex << 10);
											item += flipy[source] ? 0x8000 : 0;
											item += flipx[source] ? 0x4000 : 0;
											group[subX * 2 + subY] = item;
										}
									}

									int idx = m16opt.FindIndex(p => p[0] == group[0] &&
										p[1] == group[1] && p[2] == group[2] && p[3] == group[3]);

									if (idx != -1)
									{
										m16index[my * (mwlWidth / 16) + mx] = idx;
									}
									else
									{
										m16index[my * (mwlWidth / 16) + mx] = m16opt.Count;
										m16opt.Add(group);
									}
								}
							}

							int size = m16opt.Count * 8;
							if (size % 0x800 != 0)
							{
								size += 0x800 - size % 0x800;
							}
							outputTilemap = new byte[size];

							for (int i = 0; i < m16opt.Count; ++i)
							{
								outputTilemap[(i << 3) + 0] = (byte)(m16opt[i][0]);
								outputTilemap[(i << 3) + 1] = (byte)(m16opt[i][0] >> 8);
								outputTilemap[(i << 3) + 2] = (byte)(m16opt[i][1]);
								outputTilemap[(i << 3) + 3] = (byte)(m16opt[i][1] >> 8);
								outputTilemap[(i << 3) + 4] = (byte)(m16opt[i][2]);
								outputTilemap[(i << 3) + 5] = (byte)(m16opt[i][2] >> 8);
								outputTilemap[(i << 3) + 6] = (byte)(m16opt[i][3]);
								outputTilemap[(i << 3) + 7] = (byte)(m16opt[i][3] >> 8);
							}

							// assuming the map16 will get stored at page 0x42...

							//FuSoYa:
							//"Base mwl file attached.  This file is already set to use a custom
							//palette and custom BG.  Insert the BG tilemap at offset 0xD6 (0x800
							//bytes, 16 bit values little endian).  The upper 4 bits in the byte at
							//offset 0xCE should be set to the Map16 bank you want to use (0-3),
							//while the lower 4 bits should not be changed.  Offset 0x8E8 is the
							//palette table (SNES format, 16 bit values little endian, 0x202 bytes
							//as there's an extra entry at the end for the back area color)."

							/*if (rTiles.Length > 0x300)
							{
								MessageBox.Show("There are more than 768 (0x300) tiles. It means it won't fit even if you " +
									"use all LM slots (FG1-3; BG1-3). Conversion aborted.", "Error",
									MessageBoxButtons.OK, MessageBoxIcon.Error);
								goto End;
							}*/

							mwl = Resources._base;
							if (Options.MwlLayer1)
							{
								mwl[0xCA] = 0x01; // Level Type: Standard (Horizontal)
								mwl[0xD3] = 0x01; // Direct Map: Layer 1
							}

							/*if (mwlWidth > 512 || mwlHeight > 512)
							{
								MessageBox.Show("The output image (" + mwlWidth + "x" + mwlHeight + ") is larger than 512x512! Conversion aborted.",
									"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
								goto End;
							}*/

							for (int y = 0; y < mwlHeight; y += 16)
							{
								for (int x = 0; x < mwlWidth; x += 16)
								{
									// destination: there are 0x400 blocks, each one of 16x16
									// format is, well:
									// XYyyyyxxxx

									int dest = (x >> 4 & 15) | ((y >> 4 & 15) << 4) | ((x >> 8 & 1) << 9) | ((y >> 8 & 1) << 8);
									dest <<= 1;
									dest += 0xD6;

									int mx = x / 16;
									int my = y / 16;
									int source = my * (mwlWidth / 16) + mx;

									if (source >= m16index.Length) continue;

									int m16 = 0x0200 + m16index[source];
									mwl[dest] = (byte)m16;
									mwl[dest + 1] = (byte)(m16 >> 8);
								}
							}

							byte[] mw3 = Program.PaletteToMw3(memPalette, 256);
							mw3[512] = mw3[0];
							mw3[513] = mw3[1];
							mw3[0] = 0;
							mw3[1] = 0;
							mw3.CopyTo(mwl, 0x8E8);
						}
						else if (Options.TilemapOutput == 2)
						{

							// we need to deinterleave the gfx "blocks" or it will look weird on LM
							int w = Program.bitmapInfo.Width;
							int h = Program.bitmapInfo.Height;
							int h2 = h;
							if (h2 % 8 != 0) h2 += 8 - (h2 % 8);
							if (w % 128 != 0) w += 128 - w % 128;
							if (h % 256 != 0) h += 256 - h % 256;

							/*if (w > 512 || h > 512)
							{
								MessageBox.Show("The output image (" + w + "x" + h + ") is larger than 512x512! Conversion aborted.",
									"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
								goto End;
							}*/

							//brrrrrrrrrrrrrrrrrrrrrrrrrr!
							if (w <= 256 && h <= 256)
							{
								//outputTilemap = new byte[32 * 32 * 2];
								// attention TO DO
								outputTilemap = new byte[0x780];
							}
							else if (w > 256 && h <= 256)
							{
								outputTilemap = new byte[64 * 32 * 2];
							}
							else
							{
								outputTilemap = new byte[64 * 64 * 2];
							}

							for (int y = 0; y < h; y += 8)
							{
								for (int x = 0; x < w; x += 8)
								{
									// destination: there are 0x1000 blocks, each one of 8x8
									// format is, well:
									// XYyy yyyx xxxx

									int dest = (x >> 3 & 31) | ((y >> 3 & 31) << 5) | ((x >> 8 & 1) << 10) | ((y >> 8 & 1) << 11);
									dest <<= 1;


									// pp pppp xxxx

									if (y >> 3 >= h2 >> 3) continue;

									int fakeY = (y >> 3) + (x >> 7) * (h2 >> 3);
									int source = (x >> 3 & 15) | (fakeY << 4);

									if (source >= tilemap.Length) continue;

									int item = tilemap[source] & 0x3FF;
									item |= (tiles[source].PaletteIndex << 10);
									item += flipy[source] ? 0x8000 : 0;
									item += flipx[source] ? 0x4000 : 0;

									outputTilemap[dest] = (byte)(item & 0xFF);
									outputTilemap[dest + 1] = (byte)(item >> 8 & 0xFF);
								}
							}

						}
						else if (Options.TilemapOutput == 3 || Options.TilemapOutput == 4)
						{
							// mode 7

							int w = Program.bitmapInfo.Width;
							int h = Program.bitmapInfo.Height;
							int h2 = h;
							if (w % 128 != 0) w += 128 - w % 128;
							if (h % 256 != 0) h += 256 - h % 256;

							/*if (w > 1024 || h > 1024)
							{
								MessageBox.Show("The output image (" + w + "x" + h + ") is larger than 1024x1024! Conversion aborted.",
									"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
								goto End;
							}*/

							/*if (rTiles.Length > 0x100)
							{
								MessageBox.Show("There are more than 256 (0x100) tiles. It won't fit on a Mode 7 Character Data, " +
									"which is limited to 128x128. Conversion aborted.", "Error",
									MessageBoxButtons.OK, MessageBoxIcon.Error);
								goto End;
							}*/

							outputTilemap = new byte[128 * 128];

							for (int y = 0; y < h; y += 8)
							{
								for (int x = 0; x < w; x += 8)
								{
									// destination: there are 0x1000 blocks, each one of 8x8
									// format is, well:
									// XYyy yyyx xxxx

									// format:
									// yy yyyy yxxx xxxx

									int dest = (x >> 3 & 1023) | ((y >> 3 & 1023) << 7);

									// pp pppp xxxx

									if (y >> 3 >= h2 >> 3) continue;

									int fakeY = (y >> 3) + (x >> 7) * (h2 >> 3);
									int source = (x >> 3 & 15) | (fakeY << 4);

									if (source >= tilemap.Length) continue;

									if (flipy[source] || flipx[source])
									{
										this.Invoke(new Action(() => {
											MessageBox.Show("Mode 7 can't flip tiles! Please disable \"Remove Flipped Tiles\"" +
												"option and try again. Conversion aborted.", "Error",
												MessageBoxButtons.OK, MessageBoxIcon.Error);
										}));
										return;
									}

									outputTilemap[dest] = (byte)(tilemap[source] & 0xFF);
								}
							}
						}

					}

					byte[] output = codec.Encode(result);

					if (!preview)
					{
						int error_count = 0;
						while (true)
						{
							try
							{
								if (!Options.SplitOutput)
								{
									if (Options.TilemapOutput == 4)
									{
										// kk interleave mode
										byte[] final = new byte[0x8000];

										for (int x = 0, y = 0; x < 0x8000; x += 2, ++y)
										{
											final[x + 0] = outputTilemap[y];
											if (y < output.Length)
											{
												final[x + 1] = output[y];
											}
											else
											{
												final[x + 1] = 0;
											}
										}

										File.WriteAllBytes(name + ".bin", final);
									}
									else
									{
										File.WriteAllBytes(name + ".bin", output);
									}
								}
								else
								{
									if (Options.TilemapOutput == 4)
									{
										this.Invoke(new Action(() => {
											MessageBox.Show("Can't split output with Mode 7 Interleaved Tilemap!", "Error",
												MessageBoxButtons.OK, MessageBoxIcon.Error);
										}));
										return;
									}

									int maxSize = 1 << (Options.SplitOutputIndex + 9);
									int size = output.Length;
									int index = 0;
									int part = 0;

									while (index < size)
									{
										byte[] area = new byte[maxSize];
										if (index + maxSize < size)
										{
											Array.Copy(output, index, area, 0, maxSize);
										}
										else
										{
											Array.Copy(output, index, area, 0, size - index);
										}
										File.WriteAllBytes(name + "_part_" + part.ToString("X2") + ".bin", area);
										index += maxSize;
										++part;
									}
								}

								if (palette.Length != 0)
								{
									File.WriteAllBytes(name + extension, palette);
								}

								if (outputTilemap != null && Options.TilemapOutput != 4)
								{
									File.WriteAllBytes(name + "_map16.bin", outputTilemap);
								}

								if (mwl != null)
								{
									File.WriteAllBytes(name + ".mwl", mwl);
								}
								break;
							}

							catch (Exception e)
							{
								if (error_count++ >= 6)
								{
									this.Invoke(new Action(() => {
										MessageBox.Show(e.Message, "Error",
											MessageBoxButtons.OK, MessageBoxIcon.Error);
									}));
									return;
								}
							}
						}
					}
					else
					{
						Bitmap graphics = Program.ConvertCustomToBitmap(output,
							memPalette, codec.BitsPerPixel);

						this.Invoke(new Action(() => {
							if (previewBox != null)
							{
								if (previewBox.Image != null) previewBox.Image.Dispose();
								previewBox.Image = graphics;
							}
							else if (Program.view == null || Program.view.IsDisposed)
							{
								Program.view = new Preview(graphics, new Size(256, 256));
								Program.view.Owner = this;
								Program.view.Show();
							}
							else
								Program.view.CurrrentImage = graphics;
						}));
					}
				}
			}
			catch (NullReferenceException)
			{
				this.Invoke(new Action(() => {
					MessageBox.Show("This image has too many colors to fit on configuration. " +
						"Please enable \"optimize image\" option to reduce the number of colors.",
						"Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}));
			}
			catch (Exception e)
			{
				this.Invoke(new Action(() => {
					MessageBox.Show(e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}));
			}
			finally
			{
				Ripper_ProcessSystem_Cleanup(previewFlag, report.ToString());
			}
		}); // End of ThreadPool.QueueUserWorkItem
	}

		/// <summary>
		/// Mostra um OpenFileDialog
		/// </summary>
		/// <param name="sender">meh</param>
		/// <param name="e">meh</param>
		private void ShowDialog1(object sender, EventArgs e)
        {
            if (this.openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
					foreach (var file in openFileDialog1.FileNames)
					{
						using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read))
						{
							new Bitmap(fs).Dispose();
						}
					}
                }

                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                button5.Enabled = button2.Enabled = button3.Enabled = true;
                textBox1.Tag = openFileDialog1.FileNames;
                textBox1.Text = openFileDialog1.FileName.Substring(openFileDialog1.FileName.LastIndexOf("\\") + 1);
            }
        }

        private void Ripper_ChangeCodec(object sender, EventArgs e)
        {
            Settings.Default.Codec = comboBox1.SelectedIndex;
            Settings.Default.Save();
            Ripper_DetectColors();
        }

        bool init = false;

        private void Main_Load(object sender, EventArgs e)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            init = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Ripper_ProcessSystem();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Ripper_ProcessSystem(true);
        }

        private void codecToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            Ripper_ChangeCodec(sender, e);
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!Char.IsNumber(e.KeyChar) && !Char.IsControl(e.KeyChar))
                e.Handled = true;
        }

        /// <summary>
        /// Hi Quality Change
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            Options.HiQuality = checkBox1.Checked;
        }

        /// <summary>
        /// Order Color Changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            button6.Enabled = Options.OrderPalette = checkBox4.Checked;
        }

        /// <summary>
        /// ScaleImage Changed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Ripper_ScaleOptions_CheckedStateChanged(object sender, EventArgs e)
        {
            Options.ScaleOption = checkBox3.Checked;
            textBox2.Enabled = checkBox3.Checked;
            textBox2.Text = "100";
        }

        /// <summary>
        /// Load Settings
        /// </summary>
        private void LoadSettings()
        {
            checkBox4.Checked = Options.OrderPalette;
            checkBox1.Checked = Options.HiQuality;
            checkBox3.Checked = Options.ScaleOption;
            checkBox2.Checked = Options.RemoveDuplicateTiles;
            checkBox5.Checked = Options.OptimizeImage;
            checkBox6.Checked = Options.SplitOutput;
            checkBox8.Checked = Options.SaveOnImageFolder;
            checkBox7.Checked = Options.AllowTransparency;
			checkBox10.Checked = Options.CGADSUB;
            checkBoxSplash.Checked = Options.ShowSplash;
            checkBoxSplash.CheckedChanged += (s, e) => { Options.ShowSplash = checkBoxSplash.Checked; };
            checkBox13.Checked = Options.MwlLayer1;

            comboBox4.SelectedIndex = Options.TilemapOutput;
            comboBox5.SelectedIndex = Options.PaletteOutput;

            checkBox9.Checked = Options.RemoveFlippedTiles;

			int a = Options.OffsetTile;
			checkBox11.Checked = a != 0;
			domainUpDown1.SelectedIndex = a ^ 0x3FF;
			Options.OffsetTile = a;
			if (!checkBox11.Checked) { domainUpDown1.Enabled = false; domainUpDown1.SelectedIndex = 0x00 ^ 0x3FF; }

			a = Options.OffsetPalette;
			checkBox12.Checked = a != 0;
			domainUpDown2.SelectedIndex = a ^ 0xFF;
			Options.OffsetPalette = a;
			if (!checkBox12.Checked) { domainUpDown2.Enabled = false; domainUpDown2.SelectedIndex = 0x00 ^ 0xFF; }
        }

		private unsafe void button3_Click(object sender, EventArgs e)
		{
			if (textBox1.Tag == null)
			{
				goto error;
			}

			StringBuilder report = new StringBuilder();

			foreach (var file in (string[])textBox1.Tag)
			{
				if (file == "") goto error;
				if (!File.Exists(file)) goto error;

				byte[] colorBuffer = new byte[16777216];

				try
				{
					using (Bitmap bitmap = new Bitmap(file))
					{
						using (Bitmap pixel3 = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format32bppArgb))
						{
							using (Graphics g = Graphics.FromImage(pixel3))
							{
								g.DrawImage(bitmap, 0, 0, bitmap.Width, bitmap.Height);
							}

							BitmapData data = pixel3.LockBits(
								new Rectangle(0, 0, bitmap.Width, bitmap.Height),
								ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

							int bitmapsize = Math.Abs(data.Stride * data.Height) / 4;
							uint* ptr = (uint*)data.Scan0;

							int* i_ptr = stackalloc int[1];
							int i = *i_ptr;

							// 16mb buffer for color detect
							Array.Clear(colorBuffer, 0, colorBuffer.Length);

							int colorcounter = 0;

							fixed (byte* p = colorBuffer)
							{
								while (i < bitmapsize)
									p[ptr[i++] & 0xffffff] = 1;

								pixel3.UnlockBits(data);
								data = null;

								i = 0;

								for (; i < 16777216; i++)
									if (p[i] == 1)
										colorcounter++;
							}

							Array.Clear(colorBuffer, 0, colorBuffer.Length);

							int result1 = colorcounter;

							Bitmap tmp = new Bitmap(pixel3);
							double scale = (!checkBox3.Checked) ? 1 : (Int32.Parse(textBox2.Text) / 100D);

							Program.PrepareBitmap(ref tmp, scale);
							if (Options.CGADSUB) Program.CGADSUB(ref tmp);
							if (Options.HiQuality) Program.Snes15BitRgb(ref tmp);

							data = tmp.LockBits(
		new Rectangle(0, 0, tmp.Width, tmp.Height),
		ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

							bitmapsize = Math.Abs(data.Stride * data.Height) / 4;
							ptr = (uint*)data.Scan0;

							i = *i_ptr;
							
							colorcounter = 0;

							fixed (byte* p = colorBuffer)
							{
								while (i < bitmapsize)
									p[ptr[i++] & 0xffffff] = 1;

								tmp.UnlockBits(data);
								data = null;

								i = 0;

								for (; i < 16777216; i++)
									if (p[i] == 1)
										colorcounter++;
							}

							colorBuffer = null;

							tmp.Dispose();
							tmp = null;

							string result = String.Format("Total colors:\t{0}\r\nAfter filter:\t{1}\r\n",
								result1, colorcounter);

							report.AppendLine(Path.GetFileName(file));
							report.AppendLine(result);
						}
					}
				}
				catch
				{
					goto error;
				}
			}


			File.WriteAllText("colorlog.txt", report.ToString());
			MessageBox.Show(report.ToString(), "Result", MessageBoxButtons.OK,
				MessageBoxIcon.Information);

			return;

		error:
			MessageBox.Show("Please load a proper file before converting!",
				"Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			return;
		}

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            Options.RemoveDuplicateTiles = checkBox2.Checked;
            if (!Options.RemoveDuplicateTiles)
            {
                comboBox4.SelectedIndex = 5;
                checkBox9.Checked = false;
                checkBox9.Enabled = false;
            }
            else
            {
                checkBox9.Enabled = true;
                checkBox9.Checked = true;
            }
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            Options.OptimizeImage = checkBox5.Checked;
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            comboBox3.Enabled = checkBox6.Checked;
            if (comboBox3.Enabled)
            {
                comboBox3.SelectedIndex = 3;
                Options.SplitOutputIndex = 3;
            }
            Options.SplitOutput = comboBox3.Enabled;
        }

        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            Options.SaveOnImageFolder = checkBox8.Checked;
        }

        private void comboBox3_SelectedIndexChanged(object sender, EventArgs e)
        {
            Options.SplitOutputIndex = comboBox3.SelectedIndex;
        }

        private void comboBox4_SelectedIndexChanged(object sender, EventArgs e)
        {
            Options.TilemapOutput = comboBox4.SelectedIndex;

            if (Options.TilemapOutput != 5)
            {
                checkBox2.Checked = true;
            }
			if (Options.TilemapOutput == 3 || Options.TilemapOutput == 4)
			{
				checkBox9.Checked = false;
			}
			else
			{
				checkBox9.Checked = true;
			}
        }

        private void comboBox5_SelectedIndexChanged(object sender, EventArgs e)
        {
            Options.PaletteOutput = comboBox5.SelectedIndex;
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            button4.Enabled = Options.AllowTransparency = checkBox7.Checked;
        }

        TransparencySettings opac;
        ColorSortSettings csettings;
		TranslucencySettings cgadsub;

        private void button4_Click(object sender, EventArgs e)
        {
            if (opac == null)
            {
                opac = new TransparencySettings();
                opac.Owner = this;
                opac.Show();
            }
            else if (opac.IsDisposed)
            {
                opac = new TransparencySettings();
                opac.Owner = this;
                opac.Show();
            }
            else
            {
                opac.Focus();
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (csettings == null)
            {
                csettings = new ColorSortSettings();
                csettings.Owner = this;
                csettings.Show();
            }
            else if (csettings.IsDisposed)
            {
                csettings = new ColorSortSettings();
                csettings.Owner = this;
                csettings.Show();
            }
            else
            {
                csettings.Focus();
            }
        }

        private void checkBox9_CheckedChanged(object sender, EventArgs e)
        {
            Options.RemoveFlippedTiles = checkBox9.Checked;
        }

		private void checkBox10_CheckedChanged(object sender, EventArgs e)
		{
			button7.Enabled = Options.CGADSUB = checkBox10.Checked;
		}

		private void button7_Click(object sender, EventArgs e)
		{
			if (cgadsub == null)
			{
				cgadsub = new TranslucencySettings();
				cgadsub.Owner = this;
				cgadsub.Show();
			}
			else if (cgadsub.IsDisposed)
			{
				cgadsub = new TranslucencySettings();
				cgadsub.Owner = this;
				cgadsub.Show();
			}
			else
			{
				cgadsub.Focus();
			}
		}

		private void domainUpDown1_SelectedItemChanged(object sender, EventArgs e)
		{
			try
			{
				ushort value = Convert.ToUInt16(domainUpDown1.Text, 16);
				if (value > 0x3FF) throw new Exception();
				//domainUpDown1.SelectedIndex = value ^ 0x3FF;
				domainUpDown1.BackColor = SystemColors.Window;
				Options.OffsetTile = value;
			}
			catch
			{
				domainUpDown1.BackColor = Color.Red;
			}
		}

		private void checkBox11_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox11.Checked)
			{
				domainUpDown1.Enabled = true;
			}
			else
			{
				domainUpDown1.Enabled = false;
				domainUpDown1.SelectedIndex = 0x00 ^ 0x3FF;
				Options.OffsetTile = 0;
			}
		}

		private void checkBox12_CheckedChanged(object sender, EventArgs e)
		{
			if (checkBox12.Checked)
			{
				domainUpDown2.Enabled = true;
			}
			else
			{
				domainUpDown2.Enabled = false;
				domainUpDown2.SelectedIndex = 0x00 ^ 0xFF;
				Options.OffsetPalette = 0;
			}
		}

		private void domainUpDown2_SelectedItemChanged(object sender, EventArgs e)
		{
			try
			{
				byte value = Convert.ToByte(domainUpDown2.Text, 16);
				//domainUpDown2.SelectedIndex = value ^ 0xFF;
				domainUpDown2.BackColor = SystemColors.Window;
				Options.OffsetPalette = value;
			}
			catch
			{
				domainUpDown2.BackColor = Color.Red;
			}
		}
		private void checkBox13_CheckedChanged(object sender, EventArgs e)
		{
			Options.MwlLayer1 = checkBox13.Checked;
		}
	}
}
