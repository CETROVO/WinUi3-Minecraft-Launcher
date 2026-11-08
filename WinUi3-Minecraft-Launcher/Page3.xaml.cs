using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WinUi3_Minecraft_Launcher
{
    public sealed partial class Page3 : Page
    {
        private AppConfig _config = new();
        private List<string> _javaList = new();
        private bool _isLoaded = false;
        private DispatcherTimer _memoryTimer;
        private bool _isUpdatingMemoryUI = false;

        public Page3()
        {
            this.InitializeComponent();

            _memoryTimer = new DispatcherTimer();
            _memoryTimer.Interval = TimeSpan.FromSeconds(1);
            _memoryTimer.Tick += (s, e) => UpdateMemoryBars();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _config = ConfigService.Load();
            LoadConfigToUI();
            _isLoaded = true;

            SettingsNavList.ItemsSource = new List<string> { "启动", "其他" };
            SettingsNavList.SelectedIndex = 0;

            _memoryTimer.Start();
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

            if (_config.AutoMemory) AutoMemoryRadio.IsChecked = true;
            else CustomMemoryRadio.IsChecked = true;

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

            // --- 其他面板 ---
            FileSourceComboBox.SelectedIndex = _config.FileDownloadSource;
            VersionListSourceComboBox.SelectedIndex = _config.VersionListSource;
            CommunitySourceComboBox.SelectedIndex = _config.CommunitySource;
            FileNameFormatComboBox.SelectedIndex = _config.FileNameFormat;
            ModManageStyleComboBox.SelectedIndex = _config.ModManageStyle;

            ShowReleaseUpdateCheckBox.IsChecked = _config.ShowReleaseUpdate;
            ShowSnapshotUpdateCheckBox.IsChecked = _config.ShowSnapshotUpdate;
            AutoSetChineseCheckBox.IsChecked = _config.AutoSetChinese;

            // 🚨 修复了这里，用新的字段名
            LauncherUpdateComboBox.SelectedIndex = (_config.LauncherSystemUpdate == 1) ? 1 : 0;
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

                if (string.IsNullOrEmpty(_config.JavaPath))
                {
                    JavaListComboBox.SelectedIndex = 0;
                }
                else
                {
                    int index = _javaList.FindIndex(j => j.Equals(_config.JavaPath, StringComparison.OrdinalIgnoreCase));
                    JavaListComboBox.SelectedIndex = index >= 0 ? index + 1 : 0;
                }
            }
        }

        // ================= 事件绑定区 =================

        private void RefreshJavaList_Click(object sender, RoutedEventArgs e) => RefreshJavaList();

        private void IsolationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.IsolationMode = IsolationComboBox.SelectedIndex; SaveConfig(); }
        private void LauncherVisibilityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.LauncherVisibility = LauncherVisibilityComboBox.SelectedIndex; SaveConfig(); }
        private void ProcessPriorityComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.ProcessPriority = ProcessPriorityComboBox.SelectedIndex; SaveConfig(); }
        private void WindowSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { _config.WindowSize = WindowSizeComboBox.SelectedIndex; SaveConfig(); }
        private void MemoryMode_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            _config.AutoMemory = AutoMemoryRadio.IsChecked == true;
            MemorySlider.IsEnabled = !_config.AutoMemory;
            if (!_config.AutoMemory) MemorySliderText.Text = $"{_config.MaxRamMb} MB";
            UpdateMemoryBars(); SaveConfig();
        }
        private void MemorySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isLoaded || _isUpdatingMemoryUI) return;
            _config.MaxRamMb = (int)e.NewValue;
            MemorySliderText.Text = $"{_config.MaxRamMb} MB";
            UpdateMemoryBars(); SaveConfig();
        }
        private void JvmArgsTextBox_TextChanged(object sender, TextChangedEventArgs e) { _config.JvmArgs = JvmArgsTextBox.Text; SaveConfig(); }
        private void GameArgsTextBox_TextChanged(object sender, TextChangedEventArgs e) { _config.GameArgs = GameArgsTextBox.Text; SaveConfig(); }
        private void PreLaunchCommandTextBox_TextChanged(object sender, TextChangedEventArgs e) { _config.PreLaunchCommand = PreLaunchCommandTextBox.Text; SaveConfig(); }
        private void JavaListComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (JavaListComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                _config.JavaPath = (tag == "auto" || tag == "none") ? null : tag;
                SaveConfig();
            }
        }
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

        private void LauncherUpdateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _config.LauncherSystemUpdate = LauncherUpdateComboBox.SelectedIndex == 1 ? 1 : 2;
            SaveConfig();
        }
        private void LauncherAnnouncementComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _config.LauncherSystemPosts = LauncherAnnouncementComboBox.SelectedIndex == 1 ? 1 : 2;
            SaveConfig();
        }

        private void CacheFolderTextBox_TextChanged(object sender, TextChangedEventArgs e) { _config.CacheFolder = CacheFolderTextBox.Text; SaveConfig(); }
        private void AllowAnonDataCheckBox_Changed(object sender, RoutedEventArgs e) { _config.AllowAnonData = AllowAnonDataCheckBox.IsChecked == true; SaveConfig(); }

        private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog { Title = "检查更新", Content = "当前已是最新版本 (v0.1.3)", CloseButtonText = "确定", XamlRoot = this.XamlRoot };
            try { await dialog.ShowAsync(); } catch { }
        }
        private async void ExportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog { Title = "导出设置", Content = "等待接入文件保存对话框逻辑...", CloseButtonText = "确定", XamlRoot = this.XamlRoot };
            try { await dialog.ShowAsync(); } catch { }
        }
        private void ImportSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog { Title = "导入设置", Content = "等待接入文件读取对话框逻辑...", CloseButtonText = "确定", XamlRoot = this.XamlRoot };
            try { dialog.ShowAsync(); } catch { }
        }
    }
}