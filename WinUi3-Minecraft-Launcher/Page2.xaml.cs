using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace WinUi3_Minecraft_Launcher
{
    public sealed partial class Page2 : Page
    {
        private List<RemoteVersion> _allVersions = new();
        private bool _isDataLoaded = false;

        public Page2()
        {
            this.InitializeComponent();
            this.Loaded += Page2_Loaded;
        }

        private async void Page2_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isDataLoaded)
            {
                // 初始化左侧分类列表
                DownloadCategoryList.ItemsSource = new List<string>
                {
                    "游戏",
                    "模组",
                    "资源包",
                    "整合包",
                    "光影包"
                };
                DownloadCategoryList.SelectedIndex = 0;

                await LoadVersionsAsync();
            }
        }

        // 左侧分类切换逻辑
        private void DownloadCategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DownloadCategoryList.SelectedItem is not string category) return;

            GamePanel.Visibility = Visibility.Collapsed;
            ModPanel.Visibility = Visibility.Collapsed;
            ResourcePackPanel.Visibility = Visibility.Collapsed;
            ModpackPanel.Visibility = Visibility.Collapsed;
            ShaderpackPanel.Visibility = Visibility.Collapsed;

            switch (category)
            {
                case "游戏":
                    GamePanel.Visibility = Visibility.Visible;
                    break;
                case "模组":
                    ModPanel.Visibility = Visibility.Visible;
                    break;
                case "资源包":
                    ResourcePackPanel.Visibility = Visibility.Visible;
                    break;
                case "整合包":
                    ModpackPanel.Visibility = Visibility.Visible;
                    break;
                case "光影包":
                    ShaderpackPanel.Visibility = Visibility.Visible;
                    break;
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _isDataLoaded = false;
            await LoadVersionsAsync();
        }

        private async Task LoadVersionsAsync()
        {
            LoadingPanel.Visibility = Visibility.Visible;
            VersionListView.Visibility = Visibility.Collapsed;

            try
            {
                _allVersions = await DownloadService.GetVersionManifestAsync();
                _isDataLoaded = true;

                LoadingPanel.Visibility = Visibility.Collapsed;
                VersionListView.Visibility = Visibility.Visible;
                ReleaseRadio.IsChecked = true;
            }
            catch (Exception ex)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                await ShowDialogAsync("网络错误", $"获取版本列表失败：\n{ex.Message}");
            }
        }

        private void VersionType_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isDataLoaded || VersionListView == null) return;

            try
            {
                IEnumerable<RemoteVersion> filtered;
                bool isAprilFools = false;

                if (ReleaseRadio.IsChecked == true)
                {
                    filtered = _allVersions.Where(v => v.Type == "release");
                }
                else if (SnapshotRadio.IsChecked == true)
                {
                    filtered = _allVersions.Where(v => v.Type == "snapshot" && !IsAprilFools(v.Id));
                }
                else
                {
                    filtered = _allVersions.Where(v => IsAprilFools(v.Id));
                    isAprilFools = true;
                }

                var list = filtered.OrderByDescending(v => v.ReleaseTime).ToList();

                foreach (var item in list)
                {
                    item.DotColor = isAprilFools
                        ? new SolidColorBrush(Colors.Purple)
                        : (item.Type == "release"
                            ? new SolidColorBrush(Colors.DodgerBlue)
                            : new SolidColorBrush(Colors.OrangeRed));
                }

                VersionListView.ItemsSource = list;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"分类过滤异常: {ex.Message}");
            }
        }

        private bool IsAprilFools(string versionId)
        {
            if (string.IsNullOrEmpty(versionId)) return false;

            if (versionId.Equals("25w14craftmine", StringComparison.OrdinalIgnoreCase) ||
                versionId.Equals("26w14a", StringComparison.OrdinalIgnoreCase))
                return true;

            var keywords = new[]
            {
                "combat", "3D Shareware", "infinite", "oneblockatatime",
                "_a_or_b", "potato", "15w14a", "1.RV", "love-and-hugs"
            };
            return keywords.Any(k => versionId.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private void VersionListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RemoteVersion selectedVersion)
            {
                this.Frame.Navigate(typeof(Page5), selectedVersion.Id);
            }
        }

        private async Task ShowDialogAsync(string title, string message)
        {
            if (this.XamlRoot == null) return;
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot
            };
            try { await dialog.ShowAsync(); } catch { }
        }
    }
}