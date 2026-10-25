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

                // 🚨 窗口居中魔法（把尺寸从 1000x700 改大）
                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var workArea = displayArea.WorkArea;
                    int width = 1450;  // 改大
                    int height = 850;  // 改大
                    int x = (workArea.Width - width) / 2;
                    int y = (workArea.Height - height) / 2;
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

        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LauncherState.CurrentMinecraftPath) || string.IsNullOrEmpty(LauncherState.CurrentVersionName)) { await ShowMessageAsync("提示", "请先选择版本！"); return; }
            ProgressOverlay.Visibility = Visibility.Visible; LaunchProgressBar.Value = 0; CancelLaunchButton.IsEnabled = true; LaunchButton.IsEnabled = false;
            _launchCts = new CancellationTokenSource(); var token = _launchCts.Token;
            var progress = new Progress<LaunchProgress>(p => { ProgressTitleText.Text = p.Title; ProgressDetailText.Text = p.Detail; LaunchProgressBar.IsIndeterminate = p.IsIndeterminate; if (!p.IsIndeterminate) LaunchProgressBar.Value = p.Percentage; });
            try
            {
                var process = await GameLauncher.LaunchAsync(LauncherState.CurrentMinecraftPath!, LauncherState.CurrentVersionName!, LauncherState.MaxRamMb, msg => { }, progress, token);
                ProgressOverlay.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException) { ProgressOverlay.Visibility = Visibility.Collapsed; await ShowMessageAsync("已取消", "启动已取消。"); }
            catch (Exception ex) { ProgressOverlay.Visibility = Visibility.Collapsed; await ShowMessageAsync("启动失败", $"{ex.Message}"); }
            finally { LaunchButton.IsEnabled = true; _launchCts?.Dispose(); _launchCts = null; }
        }

        private void CancelLaunch_Click(object sender, RoutedEventArgs e) { CancelLaunchButton.IsEnabled = false; ProgressTitleText.Text = "正在取消..."; _launchCts?.Cancel(); }

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var javaBox = new TextBox { Header = "Java 路径", Text = LauncherState.JavaPath ?? "" };
            var ramBox = new NumberBox { Header = "最大内存 (MB)", Value = LauncherState.MaxRamMb, Minimum = 0, Maximum = 32768 };
            var autoIsolationToggle = new ToggleSwitch { Header = "智能版本隔离", IsOn = LauncherState.AutoIsolation };
            var manualIsolationToggle = new ToggleSwitch { Header = "版本隔离模式", IsOn = LauncherState.VersionIsolation, IsEnabled = !LauncherState.AutoIsolation };
            autoIsolationToggle.Toggled += (s, ee) => { manualIsolationToggle.IsEnabled = !autoIsolationToggle.IsOn; };

            var pickBtn = new Button { Content = "浏览..." };
            pickBtn.Click += async (s, ee) =>
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker(); picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder; picker.FileTypeFilter.Add(".exe");
                var hwnd = WindowNative.GetWindowHandle(this); InitializeWithWindow.Initialize(picker, hwnd);
                var file = await picker.PickSingleFileAsync(); if (file != null) javaBox.Text = file.Path;
            };

            var panel = new StackPanel { Spacing = 12, MinWidth = 480 };
            panel.Children.Add(javaBox); panel.Children.Add(pickBtn); panel.Children.Add(ramBox); panel.Children.Add(autoIsolationToggle); panel.Children.Add(manualIsolationToggle);

            var dialog = new ContentDialog { Title = "设置", Content = panel, PrimaryButtonText = "保存", CloseButtonText = "取消", XamlRoot = (this.Content as FrameworkElement)?.XamlRoot };
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                LauncherState.JavaPath = string.IsNullOrWhiteSpace(javaBox.Text) ? null : javaBox.Text.Trim();
                LauncherState.MaxRamMb = (int)ramBox.Value; LauncherState.AutoMemory = LauncherState.MaxRamMb <= 0;
                LauncherState.AutoIsolation = autoIsolationToggle.IsOn; LauncherState.VersionIsolation = manualIsolationToggle.IsOn;
                var cfg = ConfigService.Load(); cfg.JavaPath = LauncherState.JavaPath; cfg.MaxRamMb = LauncherState.MaxRamMb; cfg.AutoMemory = LauncherState.AutoMemory; cfg.AutoIsolation = LauncherState.AutoIsolation; cfg.VersionIsolation = LauncherState.VersionIsolation; ConfigService.Save(cfg);
            }
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
                }
            }
        }
    }
}