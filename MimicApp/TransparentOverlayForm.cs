using System;
using System.Windows.Forms;

namespace MimicApp
{
    public partial class TransparentOverlayForm : Form
    {
        public TransparentOverlayForm()
        {
            //InitializeComponent();
            InitializeOverlay();
        }

        private void InitializeOverlay()
        {
            // Set form properties
            this.FormBorderStyle = FormBorderStyle.None; // No border
            this.BackColor = System.Drawing.Color.Black; // Background color
            this.Opacity = 0.5; // Transparency
            this.WindowState = FormWindowState.Maximized; // Full screen
            this.TopMost = true; // Always on top
            this.ShowInTaskbar = false; // Hide from taskbar
            this.StartPosition = FormStartPosition.CenterScreen; // Center on screen
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Set the form to block input
            var exStyle = User32.GetWindowLong(this.Handle, User32.GWL_EXSTYLE);
            User32.SetWindowLong(this.Handle, User32.GWL_EXSTYLE, exStyle | User32.WS_EX_LAYERED);
        }
    }

    internal static class User32
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_LAYERED = 0x00080000;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}
