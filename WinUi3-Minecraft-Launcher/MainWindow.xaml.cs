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

                    int width = state.WindowWidth > 0 ? state.WindowWidth : (int)(workArea.Width * 0.75);
                    int height = state.WindowHeight > 0 ? state.WindowHeight : (int)(workArea.Height * 0.75);
                    width = Math.Clamp(width, 1024, workArea.Width);
                    height = Math.Clamp(height, 680, workArea.Height);

                    int x = workArea.X + (workArea.Width - width) / 2;
                    int y = workArea.Y + (workArea.Height - height) / 2;

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
            var typeCombo = new ComboBox { Header = "账户类型", HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = (int)AccountService.Current.Type };
            typeCombo.Items.Add("离线账户"); typeCombo.Items.Add("微软正版"); typeCombo.Items.Add("第三方认证");

            var usernameBox = new TextBox { Header = "用户名", Text = AccountService.Current.Username, Visibility = AccountService.Current.Type == AccountType.Offline ? Visibility.Visible : Visibility.Collapsed };
            var authServerBox = new TextBox { Header = "认证服务器 URL", Text = string.IsNullOrEmpty(AccountService.Current.AuthServerUrl) ? "https://littleskin.cn/api/yggdrasil" : AccountService.Current.AuthServerUrl, Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed };
            var emailBox = new TextBox { Header = "邮箱 / 账号", Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed };
            var passwordBox = new PasswordBox { Header = "密码", Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed };
            var thirdPartyLoginBtn = new Button { Content = "🔐 登录第三方皮肤站", HorizontalAlignment = HorizontalAlignment.Stretch, Height = 40, Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed };

            var profileSelectorPanel = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
            var profileCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
            var confirmProfileBtn = new Button { Content = "✅ 确认选择此角色", HorizontalAlignment = HorizontalAlignment.Stretch, Height = 36 };
            profileSelectorPanel.Children.Add(new TextBlock { Text = "选择角色：" }); profileSelectorPanel.Children.Add(profileCombo); profileSelectorPanel.Children.Add(confirmProfileBtn);

            var thirdPartyStatusText = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed, Text = "输入账号密码后登录。" };
            var msHint = new TextBlock { Text = "⚠️ 微软正版暂未配置。", FontSize = 12, Visibility = AccountService.Current.Type == AccountType.Microsoft ? Visibility.Visible : Visibility.Collapsed };

            var panel = new StackPanel { Spacing = 12, MinWidth = 440 };
            panel.Children.Add(typeCombo); panel.Children.Add(usernameBox); panel.Children.Add(authServerBox); panel.Children.Add(emailBox); panel.Children.Add(passwordBox); panel.Children.Add(thirdPartyLoginBtn); panel.Children.Add(profileSelectorPanel); panel.Children.Add(thirdPartyStatusText); panel.Children.Add(msHint);

            string pendingApiRoot = "", pendingAccessToken = "", pendingClientToken = "";
            List<ThirdPartyProfile> pendingProfiles = new();

            thirdPartyLoginBtn.Click += async (s, ee) =>
            {
                var apiRoot = authServerBox.Text.Trim(); var email = emailBox.Text.Trim(); var pwd = passwordBox.Password;
                if (string.IsNullOrEmpty(apiRoot) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd)) { thirdPartyStatusText.Text = "❌ 请填写完整。"; return; }
                thirdPartyLoginBtn.IsEnabled = false; profileSelectorPanel.Visibility = Visibility.Collapsed; thirdPartyStatusText.Text = "⏳ 正在登录...";
                try
                {
                    var (result, profiles) = await ThirdPartyAuthService.LoginAsync(apiRoot, email, pwd, log => { });
                    if (result != null && !string.IsNullOrEmpty(result.Username))
                    {
                        AccountService.Current.Type = AccountType.ThirdParty; AccountService.Current.Username = result.Username; AccountService.Current.Uuid = result.Uuid; AccountService.Current.AccessToken = result.AccessToken; AccountService.Current.AuthServerUrl = apiRoot;
                        SaveAccountConfig(); thirdPartyStatusText.Text = $"✅ 登录成功：{result.Username}"; return;
                    }
                    if (result != null && profiles != null && profiles.Count > 0)
                    {
                        pendingApiRoot = apiRoot; pendingAccessToken = result.AccessToken; pendingClientToken = result.ClientToken; pendingProfiles = profiles;
                        profileCombo.Items.Clear(); foreach (var p in profiles) profileCombo.Items.Add($"{p.Name} ({p.Id.Substring(0, 8)}...)");
                        profileCombo.SelectedIndex = 0; profileSelectorPanel.Visibility = Visibility.Visible; thirdPartyStatusText.Text = "⏳ 请选择角色。";
                    }
                }
                catch (Exception ex) { thirdPartyStatusText.Text = $"❌ 登录失败：{ex.Message}"; }
                finally { thirdPartyLoginBtn.IsEnabled = true; }
            };

            confirmProfileBtn.Click += async (s, ee) =>
            {
                if (pendingProfiles.Count == 0 || profileCombo.SelectedIndex < 0) return;
                confirmProfileBtn.IsEnabled = false; thirdPartyStatusText.Text = "⏳ 获取令牌...";
                try
                {
                    var chosen = pendingProfiles[profileCombo.SelectedIndex];
                    var finalResult = await ThirdPartyAuthService.SelectProfileAsync(pendingApiRoot, pendingAccessToken, pendingClientToken, chosen.Id, chosen.Name, log => { });
                    AccountService.Current.Type = AccountType.ThirdParty; AccountService.Current.Username = finalResult.Username; AccountService.Current.Uuid = finalResult.Uuid; AccountService.Current.AccessToken = finalResult.AccessToken; AccountService.Current.AuthServerUrl = pendingApiRoot;
                    SaveAccountConfig(); thirdPartyStatusText.Text = $"✅ 登录成功：{finalResult.Username}"; profileSelectorPanel.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex) { thirdPartyStatusText.Text = $"❌ 失败：{ex.Message}"; }
                finally { confirmProfileBtn.IsEnabled = true; }
            };

            typeCombo.SelectionChanged += (s, ee) =>
            {
                int idx = typeCombo.SelectedIndex;
                usernameBox.Visibility = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
                authServerBox.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
                emailBox.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
                passwordBox.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
                thirdPartyLoginBtn.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
                thirdPartyStatusText.Visibility = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
                msHint.Visibility = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
                if (idx != 2) profileSelectorPanel.Visibility = Visibility.Collapsed;
            };

            var dialog = new ContentDialog { Title = "账户管理", Content = new ScrollViewer { Content = panel, MaxHeight = 600 }, PrimaryButtonText = "保存", CloseButtonText = "取消", XamlRoot = (this.Content as FrameworkElement)?.XamlRoot };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var newType = (AccountType)typeCombo.SelectedIndex;
                if (newType == AccountType.Offline)
                {
                    AccountService.Current.Type = AccountType.Offline; AccountService.Current.Username = string.IsNullOrWhiteSpace(usernameBox.Text) ? "Steve" : usernameBox.Text.Trim();
                    AccountService.Current.AccessToken = ""; AccountService.Current.Uuid = ""; AccountService.Current.AuthServerUrl = "";
                }
                else if (newType == AccountType.ThirdParty)
                {
                    AccountService.Current.Type = AccountType.ThirdParty; AccountService.Current.AuthServerUrl = authServerBox.Text.Trim();
                }
                SaveAccountConfig();
            }
        }

        private int GetRequiredJavaMajorVersion(string minecraftPath, string versionName)
        {
            int requiredMajor = 8;
            try
            {
                var jsonPath = Path.Combine(minecraftPath, "versions", versionName, versionName + ".json");
                if (File.Exists(jsonPath))
                {
                    var json = JsonDocument.Parse(File.ReadAllText(jsonPath));
                    if (json.RootElement.TryGetProperty("javaVersion", out var jv) && jv.TryGetProperty("majorVersion", out var mv))
                    {
                        return mv.GetInt32();
                    }
                }

                var match = Regex.Match(versionName, @"1\.(\d+)");
                if (match.Success)
                {
                    int minor = int.Parse(match.Groups[1].Value);
                    if (minor >= 21) requiredMajor = 21;
                    else if (minor >= 18) requiredMajor = 17;
                    else if (minor >= 17) requiredMajor = 16;
                }
            }
            catch { }
            return requiredMajor;
        }

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LauncherState.CurrentMinecraftPath) || string.IsNullOrEmpty(LauncherState.CurrentVersionName))
            {
                await ShowMessageAsync("提示", "请先选择版本！");
                return;
            }

            int requiredJava = GetRequiredJavaMajorVersion(LauncherState.CurrentMinecraftPath, LauncherState.CurrentVersionName);
            var javaPath = JavaFinder.FindJavaByMajorVersion(requiredJava);

            if (string.IsNullOrEmpty(javaPath))
            {
                var dialog = new ContentDialog
                {
                    Title = "缺少 Java 运行环境",
                    Content = new TextBlock { Text = $"检测到当前版本 (需要 Java {requiredJava}) 的 Java 运行环境缺失或版本不匹配。\n\n这意味着您将无法正常游玩 Minecraft Java Edition。\n\n是否前往下载页安装？", TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = "前往安装",
                    CloseButtonText = "取消",
                    XamlRoot = (this.Content as FrameworkElement)?.XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    ContentFrame.Navigate(typeof(Page2), "Java_Force");
                }
                return;
            }

            ProgressOverlay.Visibility = Visibility.Visible;
            LaunchProgressBar.Value = 0;
            CancelLaunchButton.IsEnabled = true;
            LaunchButton.IsEnabled = false;
            _launchCts = new CancellationTokenSource();
            var token = _launchCts.Token;
            var progress = new Progress<LaunchProgress>(p => { ProgressTitleText.Text = p.Title; ProgressDetailText.Text = p.Detail; LaunchProgressBar.IsIndeterminate = p.IsIndeterminate; if (!p.IsIndeterminate) LaunchProgressBar.Value = p.Percentage; });

            try
            {
                LauncherState.JavaPath = javaPath;
                var config = ConfigService.Load();

                var process = await GameLauncher.LaunchAsync(LauncherState.CurrentMinecraftPath!, LauncherState.CurrentVersionName!, LauncherState.MaxRamMb, config, msg => { }, progress, token);
                ProgressOverlay.Visibility = Visibility.Collapsed;

                if (config.LauncherVisibility == 1)
                {
                    _appWindow?.Hide();
                }
            }
            catch (OperationCanceledException)
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                await ShowMessageAsync("已取消", "启动已取消。");
            }
            catch (Exception ex)
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;

                // 🚨 智能识别 Invalid session
                if (ex.Message.Contains("Invalid session") || ex.Message.Contains("Invalid token") || ex.Message.Contains("AuthenticationException") || ex.Message.Contains("AccessToken 为空"))
                {
                    var authDialog = new ContentDialog
                    {
                        Title = "登录已失效",
                        Content = new TextBlock { Text = "您的账户登录状态已过期或无效（Invalid session）。\n\n这通常是因为长时间未登录或 Token 被刷新导致。请点击下方按钮重新登录账户。", TextWrapping = TextWrapping.Wrap },
                        PrimaryButtonText = "重新登录",
                        CloseButtonText = "取消",
                        XamlRoot = (this.Content as FrameworkElement)?.XamlRoot
                    };

                    if (await authDialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        AccountButton_Click(null, null);
                    }
                }
                else
                {
                    await ShowMessageAsync("启动失败", $"{ex.Message}");
                }
            }
            finally
            {
                LaunchButton.IsEnabled = true;
                _launchCts?.Dispose();
                _launchCts = null;
            }
        }

        private void CancelLaunch_Click(object sender, RoutedEventArgs e) { CancelLaunchButton.IsEnabled = false; ProgressTitleText.Text = "正在取消..."; _launchCts?.Cancel(); }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(Page3));
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
                }
                else
                {
                    _appWindow.TitleBar.ButtonForegroundColor = Colors.Black; _appWindow.TitleBar.ButtonInactiveForegroundColor = Colors.Gray;
                    _appWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 230, 230, 230); _appWindow.TitleBar.ButtonHoverForegroundColor = Colors.Black;
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
                    case "Page6": ContentFrame.Navigate(typeof(Page6)); break; // 🚨 完美导航到更多页
                }
            }
        }
    }
}