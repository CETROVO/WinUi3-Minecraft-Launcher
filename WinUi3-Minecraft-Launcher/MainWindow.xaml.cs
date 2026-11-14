using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };

        // 🚨 P/Invoke 调用系统 API 获取当前窗口的 DPI
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

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

                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    var state = ConfigService.LoadState();

                    // 🚨 获取当前 DPI 缩放比例（96 DPI 是 100%，192 是 200%）
                    uint dpi = GetDpiForWindow(hWnd);
                    double scaleFactor = dpi / 96.0;

                    // 最小的逻辑宽高 (1024x680) 转换成当前缩放下的物理像素
                    int minPhysicalWidth = (int)(1024 * scaleFactor);
                    int minPhysicalHeight = (int)(680 * scaleFactor);

                    // 默认大小使用工作区的 75%，但如果是第一次打开，且屏幕很大，可以稍微再大一点
                    int width = state.WindowWidth > 0 ? state.WindowWidth : (int)(workArea.Width * 0.75);
                    int height = state.WindowHeight > 0 ? state.WindowHeight : (int)(workArea.Height * 0.75);

                    // 使用换算后的物理像素进行限制
                    width = Math.Clamp(width, minPhysicalWidth, workArea.Width);
                    height = Math.Clamp(height, minPhysicalHeight, workArea.Height);

                    // 居中显示
                    int x = workArea.X + (workArea.Width - width) / 2;
                    int y = workArea.Y + (workArea.Height - height) / 2;

                    // 如果 state 有有效的坐标记录，且没有被甩到屏幕外，才恢复坐标
                    if (state.WindowX != 0 && state.WindowY != 0)
                    {
                        if (state.WindowX >= workArea.X && state.WindowX + width <= workArea.X + workArea.Width &&
                            state.WindowY >= workArea.Y && state.WindowY + height <= workArea.Y + workArea.Height)
                        {
                            x = state.WindowX;
                            y = state.WindowY;
                        }
                    }

                    _appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                    _appWindow.Move(new Windows.Graphics.PointInt32(x, y));
                }
            }

            if (this.Content is FrameworkElement rootElement)
                rootElement.ActualThemeChanged += (s, e) => UpdateTitleBarTheme();

            this.Activated += (s, e) => UpdateTitleBarTheme();

            this.Closed += (s, e) =>
            {
                _isClosed = true;
                if (_appWindow != null)
                {
                    var state = new AppState
                    {
                        WindowWidth = _appWindow.Size.Width,
                        WindowHeight = _appWindow.Size.Height,
                        WindowX = _appWindow.Position.X,
                        WindowY = _appWindow.Position.Y
                    };
                    ConfigService.SaveState(state);
                }
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

                LauncherState.FileDownloadSource = cfg.FileDownloadSource;
                LauncherState.VersionListSource = cfg.VersionListSource;

                AccountService.Current.Type = cfg.AccountType;
                AccountService.Current.Username = string.IsNullOrEmpty(cfg.AccountUsername) ? "Steve" : cfg.AccountUsername;
                AccountService.Current.Uuid = cfg.AccountUuid;
                AccountService.Current.AccessToken = cfg.AccountAccessToken;
                AccountService.Current.AuthServerUrl = cfg.AccountAuthServerUrl;
            }
            catch { }
        }

        // ... 剩下的 SaveAccountConfig, UpdateVersionDisplay, LoadAvatarAsync, AccountButton_Click, LaunchButton_Click 等代码保持原样 ...
        // 完整代码太长就不全贴了，你只需要把上面的构造函数部分覆盖即可。

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
                CurrentVersionText.Text = string.IsNullOrEmpty(LauncherState.CurrentVersionName) ? "未选择版本" : LauncherState.CurrentVersionName;
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
                avatarUrl = $"https://littleskin.cn/avatar/player/{username}?t={timestamp}";
            else if (!string.IsNullOrEmpty(uuid))
            {
                var baseUrl = string.IsNullOrEmpty(authServerUrl) ? "https://crafatar.com" : authServerUrl.Replace("/api/yggdrasil", "");
                if (baseUrl.Contains("crafatar.com"))
                    avatarUrl = $"https://crafatar.com/avatars/{uuid}?size=160&overlay&t={timestamp}";
                else
                    avatarUrl = $"{baseUrl}/avatar/player/{username}?t={timestamp}";
            }
            else { ShowDefaultAvatar(); return; }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, avatarUrl);
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.Referrer = new Uri("https://littleskin.cn/");
                var response = await Http.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length > 0)
                {
                    var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    await stream.WriteAsync(bytes.AsBuffer());
                    stream.Seek(0);
                    var bitmap = new BitmapImage { DecodePixelWidth = 80, DecodePixelHeight = 80 };
                    await bitmap.SetSourceAsync(stream);
                    DispatcherQueue.TryEnqueue(() => { AvatarImage.Source = bitmap; AvatarImage.Visibility = Visibility.Visible; DefaultAvatarIcon.Visibility = Visibility.Collapsed; });
                    return;
                }
            }
            catch { }

            DispatcherQueue.TryEnqueue(() => ShowDefaultAvatar());
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

        public void TriggerLaunch() => LaunchButton_Click(this, new RoutedEventArgs());
        private void ManageVersionButton_Click(object sender, RoutedEventArgs e) => ContentFrame.Navigate(typeof(Page1));

        private async void AccountButton_Click(object sender, RoutedEventArgs e)
        {
            // 保持你原来的账户弹窗代码不变
        }

        private int GetRequiredJavaMajorVersion(string minecraftPath, string versionName)
        {
            // 保持原样
            return 21;
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            // 保持原样
        }

        private void CancelLaunch_Click(object sender, RoutedEventArgs e)
        {
            // 保持原样
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(Page3));
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            // 保持原样
        }

        private void UpdateTitleBarTheme()
        {
            // 保持原样
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
                    case "Page6": ContentFrame.Navigate(typeof(Page6)); break;
                }
            }
        }
    }
}