using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
using WinRT.Interop;

namespace WinUi3_Minecraft_Launcher
{
    public sealed partial class MainWindow : Window
    {
        private AppWindow? _appWindow;
        private bool _isClosed = false;
        private CancellationTokenSource? _launchCts;
        private static readonly HttpClient Http = new HttpClient();

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
            this.Closed += (s, e) =>
            {
                _isClosed = true;
                _appWindow = null;
                _launchCts?.Cancel();
                _launchCts?.Dispose();

                SaveAccountConfig();
            };

            LoadConfigIntoState();
            UpdateVersionDisplay();

            AccountService.Current.PropertyChanged += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    UpdateAccountDisplay();
                    _ = LoadAvatarAsync();
                });
            };

            UpdateTitleBarTheme();
            UpdateAccountDisplay();
            _ = LoadAvatarAsync();
            ContentFrame.Navigate(typeof(Page1));
        }

        private void LoadConfigIntoState()
        {
            try
            {
                var cfg = ConfigService.Load();
                LauncherState.JavaPath = cfg.JavaPath;
                LauncherState.MaxRamMb = cfg.MaxRamMb;
                LauncherState.AutoMemory = cfg.AutoMemory;
                LauncherState.VersionIsolation = cfg.VersionIsolation;
                LauncherState.AutoIsolation = cfg.AutoIsolation;
                LauncherState.CurrentVersionName = cfg.ActiveVersionName;
                LauncherState.CurrentMinecraftPath = cfg.ActiveFolderPath;

                AccountService.Current.Type = cfg.AccountType;
                AccountService.Current.Username = string.IsNullOrEmpty(cfg.AccountUsername) ? "Steve" : cfg.AccountUsername;
                AccountService.Current.Uuid = cfg.AccountUuid;
                AccountService.Current.AccessToken = cfg.AccountAccessToken;
                AccountService.Current.AuthServerUrl = cfg.AccountAuthServerUrl;
            }
            catch { }
        }

        public void SaveAccountConfig()
        {
            try
            {
                var cfg = ConfigService.Load();
                cfg.AccountType = AccountService.Current.Type;
                cfg.AccountUsername = AccountService.Current.Username;
                cfg.AccountUuid = AccountService.Current.Uuid;
                cfg.AccountAccessToken = AccountService.Current.AccessToken;
                cfg.AccountAuthServerUrl = AccountService.Current.AuthServerUrl;
                ConfigService.Save(cfg);
            }
            catch { }
        }

        public void UpdateVersionDisplay()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (string.IsNullOrEmpty(LauncherState.CurrentVersionName))
                {
                    CurrentVersionText.Text = "未选择版本";
                }
                else
                {
                    CurrentVersionText.Text = LauncherState.CurrentVersionName;
                }
            });
        }

        private void UpdateAccountDisplay()
        {
            AccountNameText.Text = AccountService.Current.Username;
            AccountTypeText.Text = AccountService.GetTypeDisplayName();
        }

        private async Task LoadAvatarAsync()
        {
            var username = AccountService.Current.Username;
            var uuid = AccountService.Current.Uuid?.Replace("-", "");
            var authServerUrl = AccountService.Current.AuthServerUrl;

            if (AccountService.Current.Type == AccountType.Offline || (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(uuid)))
            {
                ShowDefaultAvatar();
                return;
            }

            string avatarUrl = "";
            long timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

            if (!string.IsNullOrEmpty(authServerUrl) && authServerUrl.Contains("littleskin.cn"))
            {
                avatarUrl = $"https://littleskin.cn/avatar/player/{username}?t={timestamp}";
            }
            else if (!string.IsNullOrEmpty(uuid))
            {
                var baseUrl = string.IsNullOrEmpty(authServerUrl) ? "https://crafatar.com" : authServerUrl.Replace("/api/yggdrasil", "");
                if (baseUrl.Contains("crafatar.com"))
                    avatarUrl = $"https://crafatar.com/avatars/{uuid}?size=160&overlay&t={timestamp}";
                else
                    avatarUrl = $"{baseUrl}/avatar/player/{username}?t={timestamp}";
            }
            else
            {
                ShowDefaultAvatar();
                return;
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, avatarUrl);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.Referrer = new Uri("https://littleskin.cn/");
                request.Headers.Accept.ParseAdd("image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");

                var response = await Http.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();

                if (bytes.Length > 0)
                {
                    var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    await stream.WriteAsync(bytes.AsBuffer());
                    stream.Seek(0);

                    var bitmap = new BitmapImage();
                    bitmap.DecodePixelWidth = 80;
                    bitmap.DecodePixelHeight = 80;

                    await bitmap.SetSourceAsync(stream);

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        AvatarImage.Source = bitmap;
                        AvatarImage.Visibility = Visibility.Visible;
                        DefaultAvatarIcon.Visibility = Visibility.Collapsed;
                    });
                    return;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Avatar] 下载失败: {ex.Message}");
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                var fallbackBitmap = new BitmapImage();
                fallbackBitmap.DecodePixelWidth = 80;
                fallbackBitmap.DecodePixelHeight = 80;
                fallbackBitmap.UriSource = new Uri(avatarUrl);

                fallbackBitmap.ImageOpened += (s, e) =>
                {
                    AvatarImage.Source = fallbackBitmap;
                    AvatarImage.Visibility = Visibility.Visible;
                    DefaultAvatarIcon.Visibility = Visibility.Collapsed;
                };
                fallbackBitmap.ImageFailed += (s, e) =>
                {
                    ShowDefaultAvatar();
                };
            });
        }

        private void ShowDefaultAvatar()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                AvatarImage.Visibility = Visibility.Collapsed;
                AvatarImage.Source = null;
                DefaultAvatarIcon.Visibility = Visibility.Visible;
            });
        }

        public void TriggerLaunch()
        {
            LaunchButton_Click(this, new RoutedEventArgs());
        }

        private void ManageVersionButton_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(Page1));
        }

        private async void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            // （省略账户管理逻辑，保持你之前本地的代码不动即可）
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            // （省略启动逻辑，保持你之前本地的代码不动即可）
        }

        private void CancelLaunch_Click(object sender, RoutedEventArgs e)
        {
            CancelLaunchButton.IsEnabled = false;
            ProgressTitleText.Text = "正在取消...";
            ProgressDetailText.Text = "正在终止游戏进程...";
            _launchCts?.Cancel();
        }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // （省略设置逻辑，保持你之前本地的代码不动即可）
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog { Title = title, Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, CloseButtonText = "确定", XamlRoot = (this.Content as FrameworkElement)?.XamlRoot };
            try { await dialog.ShowAsync(); } catch { }
        }

        private void UpdateTitleBarTheme()
        {
            if (_isClosed || _appWindow == null) return;
            try
            {
                var isDarkTheme = this.Content is FrameworkElement rootElement && rootElement.ActualTheme == ElementTheme.Dark;
                if (isDarkTheme)
                {
                    _appWindow.TitleBar.ButtonForegroundColor = Colors.White; _appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;
                    _appWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 60, 60, 60); _appWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
                    _appWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 90, 90, 90); _appWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
                }
                else
                {
                    _appWindow.TitleBar.ButtonForegroundColor = Colors.Black; _appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;
                    _appWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 230, 230, 230); _appWindow.TitleBar.ButtonHoverForegroundColor = Colors.Black;
                    _appWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 200, 200, 200); _appWindow.TitleBar.ButtonPressedForegroundColor = Colors.Black;
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
    }
}