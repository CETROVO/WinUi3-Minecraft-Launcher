using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
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
            };

            LoadConfigIntoState();

            // 订阅账户变化，自动刷新左上角 UI
            AccountService.Current.PropertyChanged += (s, e) =>
            {
                DispatcherQueue.TryEnqueue(() => UpdateAccountDisplay());
            };

            UpdateTitleBarTheme();
            UpdateAccountDisplay();
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
            }
            catch { }
        }

        private void UpdateAccountDisplay()
        {
            AccountNameText.Text = AccountService.Current.Username;
            AccountTypeText.Text = AccountService.GetTypeDisplayName();
        }

        public void TriggerLaunch()
        {
            LaunchButton_Click(this, new RoutedEventArgs());
        }

        // ================= 账户管理 =================
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
                PlaceholderText = "游戏内显示的名字",
                Visibility = AccountService.Current.Type == AccountType.Offline ? Visibility.Visible : Visibility.Collapsed
            };

            var authServerBox = new TextBox
            {
                Header = "认证服务器 URL",
                Text = string.IsNullOrEmpty(AccountService.Current.AuthServerUrl)
                    ? "https://littleskin.cn/api/yggdrasil"
                    : AccountService.Current.AuthServerUrl,
                PlaceholderText = "例: https://littleskin.cn/api/yggdrasil",
                Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed
            };

            var emailBox = new TextBox
            {
                Header = "邮箱 / 账号",
                PlaceholderText = "皮肤站账号",
                Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed
            };

            var passwordBox = new PasswordBox
            {
                Header = "密码",
                PlaceholderText = "皮肤站密码",
                Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed
            };

            var thirdPartyLoginBtn = new Button
            {
                Content = "🔐 登录第三方皮肤站",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 40,
                Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed
            };

            // 角色选择区（多角色时显示）
            var profileSelectorPanel = new StackPanel
            {
                Spacing = 8,
                Visibility = Visibility.Collapsed
            };
            var profileLabel = new TextBlock { Text = "选择要登录的角色：", FontSize = 13 };
            var profileCombo = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "选择角色..."
            };
            var confirmProfileBtn = new Button
            {
                Content = "✅ 确认选择此角色",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Height = 36
            };
            profileSelectorPanel.Children.Add(profileLabel);
            profileSelectorPanel.Children.Add(profileCombo);
            profileSelectorPanel.Children.Add(confirmProfileBtn);

            var thirdPartyStatusText = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Visibility = AccountService.Current.Type == AccountType.ThirdParty ? Visibility.Visible : Visibility.Collapsed,
                Text = (AccountService.Current.Type == AccountType.ThirdParty && !string.IsNullOrEmpty(AccountService.Current.AccessToken))
                    ? $"✅ 已登录：{AccountService.Current.Username}"
                    : "输入皮肤站账号密码后点击登录。"
            };

            var msHint = new TextBlock
            {
                Text = "⚠️ 微软正版登录需要 Azure 应用，暂未配置。",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Visibility = AccountService.Current.Type == AccountType.Microsoft ? Visibility.Visible : Visibility.Collapsed
            };

            var panel = new StackPanel { Spacing = 12, MinWidth = 440 };
            panel.Children.Add(typeCombo);
            panel.Children.Add(usernameBox);
            panel.Children.Add(authServerBox);
            panel.Children.Add(emailBox);
            panel.Children.Add(passwordBox);
            panel.Children.Add(thirdPartyLoginBtn);
            panel.Children.Add(profileSelectorPanel);
            panel.Children.Add(thirdPartyStatusText);
            panel.Children.Add(msHint);

            string pendingApiRoot = "";
            string pendingAccessToken = "";
            string pendingClientToken = "";
            List<ThirdPartyProfile> pendingProfiles = new();

            // 登录按钮
            thirdPartyLoginBtn.Click += async (s, ee) =>
            {
                var apiRoot = authServerBox.Text.Trim();
                var email = emailBox.Text.Trim();
                var pwd = passwordBox.Password;

                if (string.IsNullOrEmpty(apiRoot) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(pwd))
                {
                    thirdPartyStatusText.Text = "❌ 请填写完整的服务器 URL、账号和密码。";
                    return;
                }

                thirdPartyLoginBtn.IsEnabled = false;
                profileSelectorPanel.Visibility = Visibility.Collapsed;
                thirdPartyStatusText.Text = "⏳ 正在登录...";

                try
                {
                    var (result, profiles) = await ThirdPartyAuthService.LoginAsync(
                        apiRoot, email, pwd,
                        log => System.Diagnostics.Debug.WriteLine(log));

                    if (result != null && !string.IsNullOrEmpty(result.Username))
                    {
                        AccountService.Current.Type = AccountType.ThirdParty;
                        AccountService.Current.Username = result.Username;
                        AccountService.Current.Uuid = result.Uuid;
                        AccountService.Current.AccessToken = result.AccessToken;
                        AccountService.Current.AuthServerUrl = apiRoot;

                        thirdPartyStatusText.Text = $"✅ 登录成功：{result.Username}\nUUID: {result.Uuid}";
                        return;
                    }

                    if (result != null && profiles != null && profiles.Count > 0)
                    {
                        pendingApiRoot = apiRoot;
                        pendingAccessToken = result.AccessToken;
                        pendingClientToken = result.ClientToken;
                        pendingProfiles = profiles;

                        profileCombo.Items.Clear();
                        foreach (var p in profiles)
                            profileCombo.Items.Add($"{p.Name}  ({p.Id.Substring(0, Math.Min(8, p.Id.Length))}...)");
                        profileCombo.SelectedIndex = 0;

                        profileLabel.Text = $"检测到 {profiles.Count} 个角色，请选择要登录的角色：";
                        profileSelectorPanel.Visibility = Visibility.Visible;
                        thirdPartyStatusText.Text = "⏳ 请选择角色，然后点「确认选择此角色」。";
                    }
                }
                catch (Exception ex)
                {
                    thirdPartyStatusText.Text = $"❌ 登录失败：{ex.Message}";
                }
                finally
                {
                    thirdPartyLoginBtn.IsEnabled = true;
                }
            };

            // 确认角色
            confirmProfileBtn.Click += async (s, ee) =>
            {
                if (pendingProfiles.Count == 0 || profileCombo.SelectedIndex < 0)
                {
                    thirdPartyStatusText.Text = "❌ 请先选择一个角色。";
                    return;
                }

                confirmProfileBtn.IsEnabled = false;
                thirdPartyStatusText.Text = "⏳ 正在获取角色令牌...";

                try
                {
                    var chosen = pendingProfiles[profileCombo.SelectedIndex];

                    // 🚨 同时传 id 和 name，兼容 LittleSkin 严格校验
                    var finalResult = await ThirdPartyAuthService.SelectProfileAsync(
                        pendingApiRoot,
                        pendingAccessToken,
                        pendingClientToken,
                        chosen.Id,
                        chosen.Name,
                        log => System.Diagnostics.Debug.WriteLine(log));

                    AccountService.Current.Type = AccountType.ThirdParty;
                    AccountService.Current.Username = finalResult.Username;
                    AccountService.Current.Uuid = finalResult.Uuid;
                    AccountService.Current.AccessToken = finalResult.AccessToken;
                    AccountService.Current.AuthServerUrl = pendingApiRoot;

                    thirdPartyStatusText.Text = $"✅ 登录成功：{finalResult.Username}\nUUID: {finalResult.Uuid}";
                    profileSelectorPanel.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    thirdPartyStatusText.Text = $"❌ 选择角色失败：{ex.Message}";
                }
                finally
                {
                    confirmProfileBtn.IsEnabled = true;
                }
            };

            // 切换账户类型
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

            var dialog = new ContentDialog
            {
                Title = "账户管理",
                Content = new ScrollViewer
                {
                    Content = panel,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    MaxHeight = 600
                },
                PrimaryButtonText = "保存",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = (this.Content as FrameworkElement)?.XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                var newType = (AccountType)typeCombo.SelectedIndex;

                if (newType == AccountType.Offline)
                {
                    AccountService.Current.Type = AccountType.Offline;
                    AccountService.Current.Username = string.IsNullOrWhiteSpace(usernameBox.Text) ? "Steve" : usernameBox.Text.Trim();
                    AccountService.Current.AccessToken = "";
                    AccountService.Current.Uuid = "";
                    AccountService.Current.AuthServerUrl = "";
                }
                else if (newType == AccountType.Microsoft)
                {
                    AccountService.Current.Type = AccountType.Microsoft;
                }
                else if (newType == AccountType.ThirdParty)
                {
                    AccountService.Current.Type = AccountType.ThirdParty;
                    if (!string.IsNullOrWhiteSpace(authServerBox.Text))
                        AccountService.Current.AuthServerUrl = authServerBox.Text.Trim();
                }
            }
        }

        // ================= 启动游戏 =================
        private async void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LauncherState.CurrentMinecraftPath) ||
                string.IsNullOrEmpty(LauncherState.CurrentVersionName))
            {
                await ShowMessageAsync("提示", "请先在「管理版本」里选择一个版本。");
                return;
            }

            ProgressOverlay.Visibility = Visibility.Visible;
            ProgressTitleText.Text = "正在启动游戏...";
            ProgressDetailText.Text = "准备中...";
            LaunchProgressBar.IsIndeterminate = false;
            LaunchProgressBar.Value = 0;
            CancelLaunchButton.IsEnabled = true;

            LaunchButton.IsEnabled = false;

            _launchCts = new CancellationTokenSource();
            var token = _launchCts.Token;

            var progress = new Progress<LaunchProgress>(p =>
            {
                ProgressTitleText.Text = p.Title;
                ProgressDetailText.Text = p.Detail;
                LaunchProgressBar.IsIndeterminate = p.IsIndeterminate;
                if (!p.IsIndeterminate)
                    LaunchProgressBar.Value = p.Percentage;
            });

            try
            {
                var process = await GameLauncher.LaunchAsync(
                    LauncherState.CurrentMinecraftPath!,
                    LauncherState.CurrentVersionName!,
                    LauncherState.MaxRamMb,
                    msg => System.Diagnostics.Debug.WriteLine($"[WML] {msg}"),
                    progress,
                    token);

                ProgressOverlay.Visibility = Visibility.Collapsed;
            }
            catch (OperationCanceledException)
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                await ShowMessageAsync("已取消", "本次启动已被取消。");
            }
            catch (Exception ex)
            {
                ProgressOverlay.Visibility = Visibility.Collapsed;
                await ShowMessageAsync("启动失败", $"{ex.GetType().Name}\n{ex.Message}");
            }
            finally
            {
                LaunchButton.IsEnabled = true;
                _launchCts?.Dispose();
                _launchCts = null;
            }
        }

        private void CancelLaunch_Click(object sender, RoutedEventArgs e)
        {
            CancelLaunchButton.IsEnabled = false;
            ProgressTitleText.Text = "正在取消...";
            ProgressDetailText.Text = "正在终止游戏进程...";
            _launchCts?.Cancel();
        }

        // ================= 设置 =================
        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var javaBox = new TextBox
            {
                Header = "Java 路径 (javaw.exe) — 留空则自动检测",
                Text = LauncherState.JavaPath ?? "",
                PlaceholderText = @"例: C:\Program Files\Eclipse Adoptium\jdk-21\bin\javaw.exe"
            };

            var ramBox = new NumberBox
            {
                Header = "最大内存 (MB) — 填 0 表示自动分配",
                Value = LauncherState.MaxRamMb,
                Minimum = 0,
                Maximum = 32768,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact
            };

            var autoIsolationToggle = new ToggleSwitch
            {
                Header = "智能版本隔离",
                OnContent = "自动判断",
                OffContent = "手动指定",
                IsOn = LauncherState.AutoIsolation
            };

            var manualIsolationToggle = new ToggleSwitch
            {
                Header = "版本隔离模式",
                OnContent = "启用隔离 (模组放 versions/版本名/mods)",
                OffContent = "全局共享 (模组放 .minecraft/mods)",
                IsOn = LauncherState.VersionIsolation,
                IsEnabled = !LauncherState.AutoIsolation
            };

            autoIsolationToggle.Toggled += (s, ee) =>
            {
                manualIsolationToggle.IsEnabled = !autoIsolationToggle.IsOn;
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

            var panel = new StackPanel { Spacing = 12, MinWidth = 480 };
            panel.Children.Add(javaBox);
            panel.Children.Add(pickBtn);
            panel.Children.Add(ramBox);
            panel.Children.Add(autoIsolationToggle);
            panel.Children.Add(manualIsolationToggle);

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
                LauncherState.AutoMemory = LauncherState.MaxRamMb <= 0;
                LauncherState.AutoIsolation = autoIsolationToggle.IsOn;
                LauncherState.VersionIsolation = manualIsolationToggle.IsOn;

                var cfg = ConfigService.Load();
                cfg.JavaPath = LauncherState.JavaPath;
                cfg.MaxRamMb = LauncherState.MaxRamMb;
                cfg.AutoMemory = LauncherState.AutoMemory;
                cfg.AutoIsolation = LauncherState.AutoIsolation;
                cfg.VersionIsolation = LauncherState.VersionIsolation;
                ConfigService.Save(cfg);
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