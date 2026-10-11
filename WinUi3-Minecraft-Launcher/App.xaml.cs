using Microsoft.UI.Xaml;

namespace WinUi3_Minecraft_Launcher
{
    public partial class App : Application
    {
        public static Window? MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}