using Microsoft.UI.Xaml;

namespace WinUi3_Minecraft_Launcher
{
    public partial class App : Application
    {
        // 全局暴露主窗口，供 Page1 调用 FolderPicker 使用
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