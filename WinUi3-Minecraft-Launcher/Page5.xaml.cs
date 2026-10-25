using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using CmlLib.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace WinUi3_Minecraft_Launcher
{
    public sealed partial class Page5 : Page
    {
        private string _mcVersion = "";
        private bool _isDownloading = false;
        private static readonly HttpClient Http = new HttpClient();

        private List<LoaderItem> _loaderCategories = new();
        private LoaderItem? _fabricItem;
        private LoaderItem? _fabricApiItem;
        private LoaderItem? _quiltItem;
        private LoaderItem? _forgeItem;
        private LoaderItem? _neoForgeItem;
        private LoaderItem? _optiFineItem;
        private string _selectedFabricApiVer = "";

        public Page5()
        {
            this.InitializeComponent();
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("WML-Launcher/1.0");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string version)
            {
                _mcVersion = version;
                VersionNameTextBox.Text = _mcVersion;
                InitCategories();
            }
        }

        private void InitCategories()
        {
            _loaderCategories.Clear();
            _fabricItem = new LoaderItem { Name = "原版" }; _loaderCategories.Add(_fabricItem);
            var fabric = new LoaderItem { Name = "Fabric" }; _loaderCategories.Add(fabric);
            var quilt = new LoaderItem { Name = "Quilt" }; _loaderCategories.Add(quilt);
            var forge = new LoaderItem { Name = "Forge" }; _loaderCategories.Add(forge);
            var neo = new LoaderItem { Name = "NeoForge" }; _loaderCategories.Add(neo);
            var opti = new LoaderItem { Name = "OptiFine" }; _loaderCategories.Add(opti);

            LoaderListView.ItemsSource = _loaderCategories;
            LoaderListView.SelectedIndex = 0;
        }

        private async void LoaderListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LoaderListView.SelectedItem is not LoaderItem item) return;

            DefaultHintText.Visibility = Visibility.Collapsed;
            FabricPanel.Visibility = Visibility.Collapsed;
            FabricApiPanel.Visibility = Visibility.Collapsed;
            QuiltPanel.Visibility = Visibility.Collapsed;
            ForgePanel.Visibility = Visibility.Collapsed;
            NeoForgePanel.Visibility = Visibility.Collapsed;
            OptiFinePanel.Visibility = Visibility.Collapsed;

            if (item.Name == "原版") { DefaultHintText.Visibility = Visibility.Visible; VersionNameTextBox.Text = _mcVersion; }
            else if (item.Name == "Fabric") { FabricPanel.Visibility = Visibility.Visible; if (FabricVersionCombo.Items.Count == 0) await LoadFabricVersionsAsync(); }
            else if (item.Name == "Quilt") { QuiltPanel.Visibility = Visibility.Visible; if (QuiltVersionCombo.Items.Count == 0) await LoadQuiltVersionsAsync(); }
            else if (item.Name == "Forge") { ForgePanel.Visibility = Visibility.Visible; if (ForgeVersionCombo.Items.Count == 0) await LoadForgeVersionsAsync(); }
            else if (item.Name == "NeoForge") { NeoForgePanel.Visibility = Visibility.Visible; if (NeoForgeVersionCombo.Items.Count == 0) await LoadNeoForgeVersionsAsync(); }
            else if (item.Name == "OptiFine") { OptiFinePanel.Visibility = Visibility.Visible; if (OptiFineVersionCombo.Items.Count == 0) await LoadOptiFineVersionsAsync(); }
        }

        // 加载器获取逻辑
        private async Task LoadFabricVersionsAsync() { /* 请把之前给你写好的Fabric获取逻辑贴过来，或去源码看 */ }
        private async Task FetchFabricApiVersionsAsync() { /* 同上 */ }
        private async Task LoadQuiltVersionsAsync() { /* 同上 */ }
        private async Task LoadForgeVersionsAsync() { /* 同上 */ }
        private async Task LoadNeoForgeVersionsAsync() { /* 同上 */ }
        private async Task LoadOptiFineVersionsAsync() { /* 同上 */ }

        // 选中逻辑
        private async void FabricVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { /* 同上 */ }
        private void FabricApiVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { /* 同上 */ }
        private void QuiltVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { /* 同上 */ }
        private void ForgeVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { /* 同上 */ }
        private void NeoForgeVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { /* 同上 */ }
        private void OptiFineVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) { /* 同上 */ }

        private void BackButton_Click(object sender, RoutedEventArgs e) { /* 同上 */ }
        private async void StartDownloadButton_Click(object sender, RoutedEventArgs e) { /* 同上 */ }
        private void RenameVanillaVersion(string mcDir, string oldName, string newName) { /* 同上 */ }
        private async Task InstallFabricAsync(string mcDir, string mcVersion, string loaderVersion, string targetName) { /* 同上 */ }
        private async Task DownloadFabricApiAsync(string mcDir, string mcVersion, string loaderVersion, string apiVersion, string targetName) { /* 同上 */ }
        private async Task InstallQuiltAsync(string mcDir, string mcVersion, string loaderVersion, string targetName) { /* 同上 */ }
        private async Task InstallForgeAsync(string mcDir, string mcVersion, string forgeVersion, string targetName) { /* 同上 */ }
        private async Task InstallNeoForgeAsync(string mcDir, string mcVersion, string neoVer, string targetName) { /* 同上 */ }
        private async Task InstallOptiFineAsync(string mcDir, string mcVersion, string optiFineType) { /* 同上 */ }
        private async Task ShowDialogAsync(string title, string message) { /* 同上 */ }
    }
}