using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace serverApplication
{
    public partial class splash_screen : Form
    {
        int width { get; set; }
        int height { get; set; }
        public bool IsShown { get; set; }

        public splash_screen()
        {
            InitializeComponent();
            width = Screen.PrimaryScreen.WorkingArea.Width;
            height = Screen.PrimaryScreen.WorkingArea.Height / 4;
        }

        private void splash_screen_Load(object sender, EventArgs e)
        {
            //this.Opacity = 0;
            this.IsShown = false;
            this.Left = 0;
            this.Top = Screen.PrimaryScreen.WorkingArea.Height - height;
            this.Height = height;
            this.Width = width;
        }

        public void ShowSplash()
        {
            if (this.IsShown)
                return; // Cancel if already shown
            timer1.Start();

        }

        public void HideSplash()
        {
            if (!this.IsShown)
                return; // Cancel if already hidden
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (IsShown)
                if (this.Opacity > 0)
                    this.Opacity -= 0.001;
                else
                    timer1.Stop();

            if (!IsShown)
                if (this.Opacity < 70)
                    this.Opacity += 0.001;
                else
                    timer1.Stop();
        }
    }
}
