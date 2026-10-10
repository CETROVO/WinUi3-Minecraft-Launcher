using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.UI;
using WinRT.Interop;

namespace WinUi3_Minecraft_Launcher
{
    public sealed partial class MainWindow : Window
    {
        private AppWindow? _appWindow;
        private bool _isClosed = false;

        public MainWindow()
        {
            this.InitializeComponent();

            if (MicaController.IsSupported())
                this.SystemBackdrop = new DesktopAcrylicBackdrop();

            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);

            if (_appWindow != null)
            {
                _appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                _appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            }

            if (this.Content is FrameworkElement rootElement)
                rootElement.ActualThemeChanged += (s, e) => UpdateTitleBarTheme();

            this.Activated += (s, e) => UpdateTitleBarTheme();
            this.Closed += (s, e) => { _isClosed = true; _appWindow = null; };

            UpdateTitleBarTheme();
            UpdateAccountDisplay();
            ContentFrame.Navigate(typeof(Page1));
        }

        private void UpdateAccountDisplay()
        {
            AccountNameText.Text = AccountService.Current.Username;
            AccountTypeText.Text = AccountService.GetTypeDisplayName();
        }

        private async void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            var typeCombo = new ComboBox
            {
                Header = "账户类型",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedIndex = (int)AccountService.Current.Type
            };
            typeCombo.Items.Add("离线账户");
            typeCombo.Items.Add("微软正版");
            typeCombo.Items.Add("第三方认证");

            var usernameBox = new TextBox
            {
                Header = "用户名",
                Text = AccountService.Current.Username,
                PlaceholderText = "游戏内显示的名字"
            };

            var authServerBox = new TextBox
            {
                Header = "认证服务器 URL",
                Text = AccountService.Current.AuthServerUrl,
                PlaceholderText = "例: https://littleskin.cn/api/yggdrasil",
                Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed
            };

            var microsoftHint = new TextBlock
            {
                Text = "⚠️ 微软正版登录需要 Azure 应用 OAuth2 流程，后续单独实现。目前先支持离线/第三方。",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Visibility = AccountService.Current.Type == AccountType.Microsoft ? Visibility.Visible : Visibility.Collapsed
            };

            var panel = new StackPanel { Spacing = 12, MinWidth = 360 };
            panel.Children.Add(typeCombo);
            panel.Children.Add(usernameBox);
            panel.Children.Add(authServerBox);
            panel.Children.Add(microsoftHint);

            typeCombo.SelectionChanged += (s, ee) =>
            {
                authServerBox.Visibility = typeCombo.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
                microsoftHint.Visibility = typeCombo.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
                usernameBox.Visibility = typeCombo.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
            };

            var dialog = new ContentDialog
            {
                Title = "账户管理",
                Content = panel,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = (this.Content as FrameworkElement)?.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                AccountService.Current.Type = (AccountType)typeCombo.SelectedIndex;
                AccountService.Current.Username = string.IsNullOrWhiteSpace(usernameBox.Text) ? "Steve" : usernameBox.Text.Trim();
                AccountService.Current.AuthServerUrl = authServerBox.Text.Trim();

                if (AccountService.Current.Type == AccountType.Offline)
                {
                    AccountService.Current.AccessToken = "";
                    AccountService.Current.Uuid = "";
                }

                UpdateAccountDisplay();
            }
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LauncherState.CurrentMinecraftPath) ||
                string.IsNullOrEmpty(LauncherState.CurrentVersionName))
            {
                await ShowMessageAsync("提示", "请先在「管理版本」里选择一个版本。");
                return;
            }

            try
            {
                LaunchButton.IsEnabled = false;
                var process = await GameLauncher.LaunchAsync(
                    LauncherState.CurrentMinecraftPath!,
                    LauncherState.CurrentVersionName!,
                    LauncherState.MaxRamMb,
                    msg => System.Diagnostics.Debug.WriteLine($"[WML] {msg}"));

                await ShowMessageAsync("启动成功", $"游戏进程已启动，PID: {process.Id}");
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("启动失败", $"{ex.GetType().Name}\n{ex.Message}");
            }
            finally
            {
                LaunchButton.IsEnabled = true;
            }
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var javaBox = new TextBox
            {
                Header = "Java 路径 (javaw.exe)",
                Text = LauncherState.JavaPath ?? "",
                PlaceholderText = @"例: C:\Program Files\Eclipse Adoptium\jdk-21\bin\javaw.exe"
            };

            var ramBox = new NumberBox
            {
                Header = "最大内存 (MB)",
                Value = LauncherState.MaxRamMb,
                Minimum = 512,
                Maximum = 32768,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };

            var pickBtn = new Button { Content = "浏览..." };
            pickBtn.Click += async (s, ee) =>
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
                picker.FileTypeFilter.Add(".exe");
                var hwnd = WindowNative.GetWindowHandle(this);
                InitializeWithWindow.Initialize(picker, hwnd);
                var file = await picker.PickSingleFileAsync();
                if (file != null) javaBox.Text = file.Path;
            };

            var panel = new StackPanel { Spacing = 12, MinWidth = 450 };
            panel.Children.Add(javaBox);
            panel.Children.Add(pickBtn);
            panel.Children.Add(ramBox);

            var dialog = new ContentDialog
            {
                Title = "设置",
                Content = panel,
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = (this.Content as FrameworkElement)?.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                LauncherState.JavaPath = string.IsNullOrWhiteSpace(javaBox.Text) ? null : javaBox.Text.Trim();
                LauncherState.MaxRamMb = (int)ramBox.Value;
            }
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "确定",
                XamlRoot = (this.Content as FrameworkElement)?.XamlRoot
            };
            try { await dialog.ShowAsync(); } catch { }
        }

        private void UpdateTitleBarTheme()
        {
            if (_isClosed || _appWindow == null) return;
            try
            {
                var isDarkTheme = false;
                if (this.Content is FrameworkElement rootElement)
                    isDarkTheme = rootElement.ActualTheme == ElementTheme.Dark;
                else
                    isDarkTheme = Application.Current.RequestedTheme == ApplicationTheme.Dark;

                if (isDarkTheme)
                {
                    _appWindow.TitleBar.ButtonForegroundColor = Colors.White;
                    _appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;
                    _appWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 60, 60, 60);
                    _appWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
                    _appWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 90, 90, 90);
                    _appWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
                }
                else
                {
                    _appWindow.TitleBar.ButtonForegroundColor = Colors.Black;
                    _appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;
                    _appWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 230, 230, 230);
                    _appWindow.TitleBar.ButtonHoverForegroundColor = Colors.Black;
                    _appWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 200, 200, 200);
                    _appWindow.TitleBar.ButtonPressedForegroundColor = Colors.Black;
                }
            }
            catch { }
        }

        private void NavButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string pageName)
            {
                switch (pageName)
                {
                    case "Page1": ContentFrame.Navigate(typeof(Page1)); break;
                    case "Page2": ContentFrame.Navigate(typeof(Page2)); break;
                    case "Page3": ContentFrame.Navigate(typeof(Page3)); break;
                }
            }
        }

        private void ManageVersionButton_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(Page1));
        }
    }
}