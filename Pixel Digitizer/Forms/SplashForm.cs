using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;

namespace SnesGFX.Forms
{
    public class SplashForm : Form
    {
        private Timer closeTimer;

        public SplashForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ShowInTaskbar = false;
            
            // Try load image
            try
            {
                string imgPath = Path.Combine(Application.StartupPath, "splash.png");
                if (File.Exists(imgPath))
                {
                    Image img = Image.FromFile(imgPath);
                    this.BackgroundImage = img;
                    this.BackgroundImageLayout = ImageLayout.Zoom;
                    this.Width = img.Width;
                    this.Height = img.Height;
                }
                else
                {
                    this.Width = 600;
                    this.Height = 400;
                    this.BackColor = Color.Black;
                    
                    Label lbl = new Label();
                    lbl.Text = "SHIN NEO MIMBEMBO ULTRASYSTEM GRAFI-X";
                    lbl.ForeColor = Color.White;
                    lbl.AutoSize = true;
                    lbl.Font = new Font("Arial", 16, FontStyle.Bold);
                    lbl.Location = new Point(50, 180);
                    this.Controls.Add(lbl);
                }
            }
            catch { }

            closeTimer = new Timer();
            closeTimer.Interval = 2500; // 2.5 seconds
            closeTimer.Tick += CloseTimer_Tick;
            closeTimer.Start();
        }

        private void CloseTimer_Tick(object sender, EventArgs e)
        {
            closeTimer.Stop();
            this.Close();
        }
    }
}
