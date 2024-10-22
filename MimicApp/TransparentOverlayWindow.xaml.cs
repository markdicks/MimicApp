using System.Windows;

namespace YourNamespace // Replace with your actual namespace
{
    public partial class TransparentOverlayWindow : Window
    {
        private Window _mainWindow; // Store reference to the main window

        public TransparentOverlayWindow(Window mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow; // Store the reference
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the window to full screen
            this.WindowState = WindowState.Maximized;
            this.Topmost = true; // Keep it in front
        }

        // Method to bring the main window to the front
        public void BringMainWindowToFront()
        {
            _mainWindow?.Activate();
        }
    }
}
