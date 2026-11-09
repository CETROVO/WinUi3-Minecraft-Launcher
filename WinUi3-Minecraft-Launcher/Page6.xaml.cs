using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;

namespace WinUi3_Minecraft_Launcher
{
    public sealed partial class Page6 : Page
    {
        public Page6()
        {
            this.InitializeComponent();
        }

        private void OpenUrl_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string url)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"打开链接失败: {ex.Message}");
                }
            }
        }
    }
}