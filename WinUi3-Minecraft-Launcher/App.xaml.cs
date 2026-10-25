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
            // 🚨 启动词典服务（挂载定时器、拉取更新）
            _ = TranslationService.InitializeAsync();

            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}