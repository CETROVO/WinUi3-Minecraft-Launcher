using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace WinUi3_Minecraft_Launcher
{
    public sealed partial class Page3 : Page
    {
        private AppConfig _config = new();
        private List<string> _javaList = new();
        private bool _isLoaded = false;
        private DispatcherTimer _memoryTimer;
        private bool _isUpdatingMemoryUI = false;
        private static readonly HttpClient Http = new HttpClient();

        public Page3()
        {
            this.InitializeComponent();
            _memoryTimer = new DispatcherTimer();
            _memoryTimer.Interval = TimeSpan.FromSeconds(1);
            _memoryTimer.Tick += (s, e) => UpdateMemoryBars();

            Http.DefaultRequestHeaders.UserAgent.ParseAdd("WML-Launcher/1.0");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _config = ConfigService.Load();
            LoadConfigToUI();
            _isLoaded = true;

            SettingsNavList.ItemsSource = new List<string> { "启动", "更新", "其他" };
            SettingsNavList.SelectedIndex = 0;

            _memoryTimer.Start();

            // 进入页面瞬间，先显示初始化文案
            UpdateStatusText.Text = "正在检查更新...";
            UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray);

            // 如果开启了自动检查，切到更新页时自动触发一次
            if (_config.LauncherSystemUpdate == 1)
            {
                _ = CheckUpdateInternalAsync(false);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _memoryTimer.Stop();
        }

        private void SettingsNavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingsNavList.SelectedItem is string category)
            {
                StartupPanel.Visibility = category == "启动" ? Visibility.Visible : Visibility.Collapsed;
                UpdatePanel.Visibility = category == "更新" ? Visibility.Visible : Visibility.Collapsed;
                OtherPanel.Visibility = category == "其他" ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void LoadConfigToUI()
        {
            _isUpdatingMemoryUI = true;

            // --- 启动面板 ---
            IsolationComboBox.SelectedIndex = _config.IsolationMode;
            LauncherVisibilityComboBox.SelectedIndex = _config.LauncherVisibility;
            ProcessPriorityComboBox.SelectedIndex = _config.ProcessPriority;
            WindowSizeComboBox.SelectedIndex = _config.WindowSize;
            if (_config.AutoMemory) AutoMemoryRadio.IsChecked = true; else CustomMemoryRadio.IsChecked = true;
            MemorySlider.Value = _config.MaxRamMb;
            MemorySlider.IsEnabled = !_config.AutoMemory;
            MemorySliderText.Text = $"{_config.MaxRamMb} MB";
            UpdateMemoryBars();
            JvmArgsTextBox.Text = _config.JvmArgs;
            GameArgsTextBox.Text = _config.GameArgs;
            PreLaunchCommandTextBox.Text = _config.PreLaunchCommand;
            MemoryAlgorithmComboBox.SelectedIndex = _config.MemoryAlgorithm;
            DisableLaunchWrapperCheckBox.IsChecked = _config.DisableJavaLaunchWrapper;
            DisableUnsafeAgentCheckBox.IsChecked = _config.DisableLWJGLUnsafeAgent;
            UseHighPerformanceGpuCheckBox.IsChecked = _config.UseHighPerformanceGpu;
            RefreshJavaList();

            // --- 更新面板 ---
            EnableAutoUpdateToggle.IsOn = _config.LauncherSystemUpdate == 1;
            UpdateChannelComboBox.SelectedIndex = _config.UpdateChannel;

            // --- 其他面板 ---
            FileSourceComboBox.SelectedIndex = _config.FileDownloadSource;
            VersionListSourceComboBox.SelectedIndex = _config.VersionListSource;
            CommunitySourceComboBox.SelectedIndex = _config.CommunitySource;
            FileNameFormatComboBox.SelectedIndex = _config.FileNameFormat;
            ModManageStyleComboBox.SelectedIndex = _config.ModManageStyle;
            ShowReleaseUpdateCheckBox.IsChecked = _config.ShowReleaseUpdate;
            ShowSnapshotUpdateCheckBox.IsChecked = _config.ShowSnapshotUpdate;
            AutoSetChineseCheckBox.IsChecked = _config.AutoSetChinese;
            LauncherAnnouncementComboBox.SelectedIndex = (_config.LauncherSystemPosts == 1) ? 1 : 0;
            CacheFolderTextBox.Text = _config.CacheFolder;
            AllowAnonDataCheckBox.IsChecked = _config.AllowAnonData;

            _isUpdatingMemoryUI = false;
        }

        private void SaveConfig()
        {
            if (!_isLoaded) return;
            _config.AutoIsolation = _config.IsolationMode == 1;
            _config.VersionIsolation = _config.IsolationMode == 0;
            ConfigService.Save(_config);
            LauncherState.JavaPath = _config.JavaPath;
            LauncherState.MaxRamMb = _config.MaxRamMb;
            LauncherState.AutoMemory = _config.AutoMemory;
            LauncherState.AutoIsolation = _config.AutoIsolation;
            LauncherState.VersionIsolation = _config.VersionIsolation;
        }

        private void UpdateMemoryBars()
        {
            if (_isUpdatingMemoryUI) return;

            double totalGb = MemoryAllocator.GetTotalPhysicalMemoryGB();
            double availGb = MemoryAllocator.GetAvailablePhysicalMemoryGB();
            double usedGb = totalGb - availGb;
            if (usedGb < 0) usedGb = 0;

            double allocatedGb;
            if (_config.AutoMemory)
            {
                allocatedGb = totalGb - 1.0 - usedGb;
                if (allocatedGb < 1.0) allocatedGb = 1.0;
                if (allocatedGb > 8.0) allocatedGb = 8.0;
                MemorySliderText.Text = $"{(int)(allocatedGb * 1024)} MB (自动)";
            }
            else
            {
                allocatedGb = _config.MaxRamMb / 1024.0;
            }

            UsedMemoryText.Text = $"{usedGb:F1} GB / {totalGb:F1} GB";
            AllocatedMemoryText.Text = $"{allocatedGb:F1} GB";

            double totalWidth = 600;
            double usedRatio = usedGb / totalGb;
            double allocRatio = allocatedGb / totalGb;

            UsedMemoryBar.Width = Math.Max(0, Math.Min(totalWidth, usedRatio * totalWidth));
            AllocatedMemoryBar.Margin = new Thickness(UsedMemoryBar.Width, 0, 0, 0);
            AllocatedMemoryBar.Width = Math.Max(0, Math.Min(totalWidth - UsedMemoryBar.Width, allocRatio * totalWidth));
        }

        private void RefreshJavaList()
        {
            _javaList = JavaFinder.FindAllJavas();
            JavaListComboBox.Items.Clear();
            JavaListComboBox.Items.Add(new ComboBoxItem { Content = "让 WML 自动选择合适的 Java", Tag = "auto" });
            if (_javaList.Count == 0)
            {
                JavaListComboBox.Items.Add(new ComboBoxItem { Content = "未找到 Java，请前往下载", Tag = "none" });
                JavaListComboBox.SelectedIndex = 1;
            }
            else
            {
                foreach (var java in _javaList)
                {
                    int ver = JavaFinder.GetJavaMajorVersion(java);
                    JavaListComboBox.Items.Add(new ComboBoxItem { Content = $"Java {ver} - {java}", Tag = java });
                }
                if (string.IsNullOrEmpty(_config.JavaPath)) JavaListComboBox.SelectedIndex = 0;
                else
                {
                    int index = _javaList.FindIndex(j => j.Equals(_config.JavaPath, StringComparison.OrdinalIgnoreCase));
                    JavaListComboBox.SelectedIndex = index >= 0 ? index + 1 : 0;
                }
            }
        }

        // ================= 更新逻辑 =================
        private async void UpdateChannelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            int newIndex = UpdateChannelComboBox.SelectedIndex;

            if (newIndex == 1 && _config.UpdateChannel != 1)
            {
                var warning = new ContentDialog
                {
                    Title = "⚠️ 切换到测试版",
                    Content = new TextBlock { Text = "Beta 版包含最新功能，但可能极不稳定。\n\n您确定要切换到 Beta 版通道吗？", TextWrapping = TextWrapping.Wrap },
                    PrimaryButtonText = "确定切换",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                if (await warning.ShowAsync() != ContentDialogResult.Primary)
                {
                    UpdateChannelComboBox.SelectedIndex = _config.UpdateChannel;
                    return;
                }
            }

            _config.UpdateChannel = newIndex;
            SaveConfig();

            // 通道改变后自动重新检查
            _ = CheckUpdateInternalAsync(false);
        }

        private void EnableAutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            _config.LauncherSystemUpdate = EnableAutoUpdateToggle.IsOn ? 1 : 2;
            SaveConfig();
        }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            await CheckUpdateInternalAsync(true);
        }

        // 🚨 核心：更新检查逻辑
        private async Task CheckUpdateInternalAsync(bool isManual)
        {
            CheckUpdateButton.IsEnabled = false;

            UpdateStatusText.Text = "正在检查更新...";
            UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray);
            ReleaseNotesScrollViewer.Visibility = Visibility.Collapsed;
            ReleaseNotesText.Text = "";
            UpdateStatusBorder.Visibility = Visibility.Visible;

            try
            {
                // 利用 csproj 的 Version 标签获取当前版本（不会变成 1.0.0）
                string currentVersion = Assembly.GetExecutingAssembly()
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion ?? "0.2.0";

                // 如果带有 commit hash 后缀，截断它
                if (currentVersion.Contains("+")) currentVersion = currentVersion.Split('+')[0];

                bool isBeta = _config.UpdateChannel == 1;
                string apiUrl = isBeta
                    ? "https://api.github.com/repos/CETROVO/WinUi3-Minecraft-Launcher/releases"
                    : "https://api.github.com/repos/CETROVO/WinUi3-Minecraft-Launcher/releases/latest";

                var res = await Http.GetStringAsync(apiUrl);
                using var doc = JsonDocument.Parse(res);

                JsonElement latestRelease = default;
                if (isBeta)
                {
                    bool found = false;
                    foreach (var release in doc.RootElement.EnumerateArray())
                    {
                        if (release.GetProperty("prerelease").GetBoolean())
                        {
                            latestRelease = release;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                    {
                        UpdateStatusText.Text = "当前通道下没有可用的 Beta 版本";
                        UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGray);
                        return;
                    }
                }
                else
                {
                    latestRelease = doc.RootElement;
                }

                string latestTag = latestRelease.GetProperty("tag_name").GetString() ?? "";
                string releaseBody = latestRelease.GetProperty("body").GetString() ?? "暂无更新说明。";
                string downloadUrl = "";

                var assets = latestRelease.GetProperty("assets");
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe"))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }

                // 🚨 终极判断：不显示本地版本，只展示结果
                string cleanLatestTag = latestTag.Replace("v", "");
                if (cleanLatestTag != currentVersion && cleanLatestTag != $"{currentVersion}.0" && !latestTag.Contains(currentVersion))
                {
                    // 有更新
                    UpdateStatusText.Text = $"有可用的更新：WML {latestTag}";
                    UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
                    ReleaseNotesText.Text = releaseBody;
                    ReleaseNotesScrollViewer.Visibility = Visibility.Visible;

                    // 如果是手动点击的，弹窗询问；如果是自动检查的，就不弹窗打扰（玩家自己点更新按钮）
                    if (isManual)
                    {
                        var dialog = new ContentDialog
                        {
                            Title = "发现新版本",
                            Content = new TextBlock { Text = $"最新版本：{latestTag}\n\n是否立即下载并更新？", TextWrapping = TextWrapping.Wrap },
                            PrimaryButtonText = "立即更新",
                            CloseButtonText = "稍后",
                            XamlRoot = this.XamlRoot
                        };

                        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            if (string.IsNullOrEmpty(downloadUrl))
                            {
                                await ShowMsg("更新失败", "未找到安装包直链，请前往 GitHub Releases 手动下载。");
                                return;
                            }
                            await DownloadAndInstallUpdateAsync(downloadUrl, latestTag);
                        }
                    }
                }
                else
                {
                    // 无更新
                    UpdateStatusText.Text = "您当前使用的是最新版本";
                    UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = "检查更新失败，请检查网络连接";
                UpdateStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Red);
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
            finally
            {
                CheckUpdateButton.IsEnabled = true;
            }
        }

        private async Task DownloadAndInstallUpdateAsync(string url, string version)
        {
            var progressDialog = new ContentDialog
            {
                Title = $"正在下载更新 ({version})",
                Content = new ProgressBar { IsIndeterminate = true, Width = 300 },
                XamlRoot = this.XamlRoot
            };

            _ = progressDialog.ShowAsync();

            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), $"WML_Setup_{version}.exe");
                var bytes = await Http.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(tempPath, bytes);

                progressDialog.Hide();

                Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true });
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                progressDialog.Hide();
                await ShowMsg("下载失败", $"下载更新包失败：{ex.Message}");
            }
        }

        private async Task ShowMsg(string title, string content)
        {
            var dialog = new ContentDialog { Title = title, Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap }, CloseButtonText = "确定", XamlRoot = this.XamlRoot };
            try { await dialog.ShowAsync(); } catch { }
        }

        // ================= 其他原有事件 =================
        private void RefreshJavaList_Click(object sender, RoutedEventArgs e) => RefreshJavaList();
        private void IsolationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.IsolationMode = IsolationComboBox.SelectedIndex; SaveConfig(); }
        private void LauncherVisibilityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.LauncherVisibility = LauncherVisibilityComboBox.SelectedIndex; SaveConfig(); }
        private void ProcessPriorityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.ProcessPriority = ProcessPriorityComboBox.SelectedIndex; SaveConfig(); }
        private void WindowSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.WindowSize = WindowSizeComboBox.SelectedIndex; SaveConfig(); }
        private void MemoryMode_Checked(object sender, RoutedEventArgs e) { if (!_isLoaded) return; _config.AutoMemory = AutoMemoryRadio.IsChecked == true; MemorySlider.IsEnabled = !_config.AutoMemory; if (!_config.AutoMemory) MemorySliderText.Text = $"{_config.MaxRamMb} MB"; UpdateMemoryBars(); SaveConfig(); }
        private void MemorySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e) { if (!_isLoaded || _isUpdatingMemoryUI) return; _config.MaxRamMb = (int)e.NewValue; MemorySliderText.Text = $"{_config.MaxRamMb} MB"; UpdateMemoryBars(); SaveConfig(); }
        private void JvmArgsTextBox_TextChanged(object sender, TextChangedEventArgs e) { _config.JvmArgs = JvmArgsTextBox.Text; SaveConfig(); }
        private void GameArgsTextBox_TextChanged(object sender, TextChangedEventArgs e) { _config.GameArgs = GameArgsTextBox.Text; SaveConfig(); }
        private void PreLaunchCommandTextBox_TextChanged(object sender, TextChangedEventArgs e) { _config.PreLaunchCommand = PreLaunchCommandTextBox.Text; SaveConfig(); }
        private void JavaListComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (!_isLoaded) return; if (JavaListComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag) { _config.JavaPath = (tag == "auto" || tag == "none") ? null : tag; SaveConfig(); } }
        private void MemoryAlgorithmComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.MemoryAlgorithm = MemoryAlgorithmComboBox.SelectedIndex; SaveConfig(); }
        private void DisableLaunchWrapperCheckBox_Changed(object sender, RoutedEventArgs e) { _config.DisableJavaLaunchWrapper = DisableLaunchWrapperCheckBox.IsChecked == true; SaveConfig(); }
        private void DisableUnsafeAgentCheckBox_Changed(object sender, RoutedEventArgs e) { _config.DisableLWJGLUnsafeAgent = DisableUnsafeAgentCheckBox.IsChecked == true; SaveConfig(); }
        private void UseHighPerformanceGpuCheckBox_Changed(object sender, RoutedEventArgs e) { _config.UseHighPerformanceGpu = UseHighPerformanceGpuCheckBox.IsChecked == true; SaveConfig(); }
        private void FileSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.FileDownloadSource = FileSourceComboBox.SelectedIndex; SaveConfig(); }
        private void VersionListSourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.VersionListSource = VersionListSourceComboBox.SelectedIndex; SaveConfig(); }
        private void CommunitySourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.CommunitySource = CommunitySourceComboBox.SelectedIndex; SaveConfig(); }
        private void FileNameFormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.FileNameFormat = FileNameFormatComboBox.SelectedIndex; SaveConfig(); }
        private void ModManageStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.ModManageStyle = ModManageStyleComboBox.SelectedIndex; SaveConfig(); }
        private void ShowReleaseUpdateCheckBox_Changed(object sender, RoutedEventArgs e) { _config.ShowReleaseUpdate = ShowReleaseUpdateCheckBox.IsChecked == true; SaveConfig(); }
        private void ShowSnapshotUpdateCheckBox_Changed(object sender, RoutedEventArgs e) { _config.ShowSnapshotUpdate = ShowSnapshotUpdateCheckBox.IsChecked == true; SaveConfig(); }
        private void AutoSetChineseCheckBox_Changed(object sender, RoutedEventArgs e) { _config.AutoSetChinese = AutoSetChineseCheckBox.IsChecked == true; SaveConfig(); }
        private void LauncherAnnouncementComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.LauncherSystemPosts = LauncherAnnouncementComboBox.SelectedIndex == 1 ? 1 : 2; SaveConfig(); }
        private void CacheFolderTextBox_TextChanged(object sender, TextChangedEventArgs e) { _config.CacheFolder = CacheFolderTextBox.Text; SaveConfig(); }
        private void AllowAnonDataCheckBox_Changed(object sender, RoutedEventArgs e) { _config.AllowAnonData = AllowAnonDataCheckBox.IsChecked == true; SaveConfig(); }
        private async void ExportSettings_Click(object sender, RoutedEventArgs e) { await ShowMsg("导出设置", "等待接入文件保存对话框逻辑..."); }
        private void ImportSettings_Click(object sender, RoutedEventArgs e) { _ = ShowMsg("导入设置", "等待接入文件读取对话框逻辑..."); }
    }
}