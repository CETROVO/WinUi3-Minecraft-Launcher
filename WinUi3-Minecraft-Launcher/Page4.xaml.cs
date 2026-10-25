using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace WinUi3_Minecraft_Launcher
{
    public sealed partial class Page4 : Page
    {
        private string _versionName = "";
        private string _minecraftPath = "";
        private string _versionDir = "";

        private readonly HttpClient Http = new HttpClient();

        private List<string> _mainCategories = new() { "设置", "模组" };
        public ObservableCollection<ModItem> Mods { get; set; } = new();

        public Page4()
        {
            this.InitializeComponent();
            Http.Timeout = TimeSpan.FromSeconds(15);
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("WML-Launcher/1.0");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _versionName = e.Parameter as string ?? LauncherState.CurrentVersionName ?? "";
            _minecraftPath = LauncherState.CurrentMinecraftPath ?? "";
            _versionDir = Path.Combine(_minecraftPath, "versions", _versionName);

            MainCategoryList.ItemsSource = _mainCategories;
            MainCategoryList.SelectedIndex = 0;

            LoadVersionInfo();
        }

        private void MainCategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainCategoryList.SelectedItem is not string category) return;

            SetupPanel.Visibility = Visibility.Collapsed;
            ModsPanel.Visibility = Visibility.Collapsed;

            if (category == "设置")
            {
                SetupPanel.Visibility = Visibility.Visible;
            }
            else if (category == "模组")
            {
                ModsPanel.Visibility = Visibility.Visible;
                _ = LoadInstalledModsAsync();
            }
        }

        private void LoadVersionInfo()
        {
            VersionTitleText.Text = _versionName;
            InfoVersionIdText.Text = _versionName;
            InfoPathText.Text = _versionDir;

            string gameVersion = "";
            string loaderInfo = "原版 (Vanilla)";
            string javaVersion = "未指定";

            var mcMatch = Regex.Match(_versionName, @"(1\.\d+(\.\d+)?)");
            if (mcMatch.Success) gameVersion = mcMatch.Value;

            try
            {
                var jsonPath = Path.Combine(_versionDir, $"{_versionName}.json");
                if (File.Exists(jsonPath))
                {
                    var json = JsonDocument.Parse(File.ReadAllText(jsonPath));
                    var root = json.RootElement;

                    if (string.IsNullOrEmpty(gameVersion))
                    {
                        if (root.TryGetProperty("inheritsFrom", out var inheritProp) && inheritProp.ValueKind == JsonValueKind.String)
                        {
                            gameVersion = inheritProp.GetString() ?? "";
                        }
                    }

                    if (root.TryGetProperty("libraries", out var libs))
                    {
                        foreach (var lib in libs.EnumerateArray())
                        {
                            if (lib.TryGetProperty("name", out var nameProp))
                            {
                                var libName = nameProp.GetString() ?? "";
                                if (libName.StartsWith("net.fabricmc:fabric-loader", StringComparison.OrdinalIgnoreCase))
                                {
                                    var parts = libName.Split(':');
                                    loaderInfo = parts.Length >= 3 ? $"Fabric {parts[2]}" : "Fabric";
                                }
                                else if (libName.StartsWith("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase))
                                {
                                    var parts = libName.Split(':');
                                    loaderInfo = parts.Length >= 3 ? $"Forge {parts[2]}" : "Forge";
                                }
                                else if (libName.StartsWith("net.neoforged:neoforge", StringComparison.OrdinalIgnoreCase))
                                {
                                    var parts = libName.Split(':');
                                    loaderInfo = parts.Length >= 3 ? $"NeoForge {parts[2]}" : "NeoForge";
                                }
                            }
                        }
                    }

                    if (root.TryGetProperty("javaVersion", out var jv) && jv.TryGetProperty("majorVersion", out var mv))
                    {
                        javaVersion = $"Java {mv.GetInt32()}";
                    }
                }
            }
            catch { }

            if (string.IsNullOrEmpty(gameVersion)) gameVersion = _versionName;

            InfoGameVersionText.Text = gameVersion;
            InfoLoaderText.Text = loaderInfo;
            VersionSubtitleText.Text = $"{gameVersion} · {loaderInfo}";
            InfoJavaText.Text = javaVersion;
        }

        private async Task LoadInstalledModsAsync()
        {
            Mods.Clear();
            var modsPath = Path.Combine(_versionDir, "mods");

            if (!Directory.Exists(modsPath))
            {
                modsPath = Path.Combine(_minecraftPath, "mods");
                if (!Directory.Exists(modsPath)) return;
            }

            var jarFiles = Directory.GetFiles(modsPath, "*.jar");
            foreach (var jar in jarFiles)
            {
                var modInfo = await ParseModInfoAsync(jar);
                if (modInfo != null) Mods.Add(modInfo);
            }

            ModListView.ItemsSource = Mods;
        }

        private async Task<ModItem?> ParseModInfoAsync(string jarPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(jarPath);
                string modId = "", name = "", version = "";
                string? iconPath = null;

                var fabricJson = archive.GetEntry("fabric.mod.json");
                if (fabricJson != null)
                {
                    using var stream = fabricJson.Open();
                    using var reader = new StreamReader(stream);
                    using var doc = JsonDocument.Parse(reader.ReadToEnd());
                    var root = doc.RootElement;

                    modId = root.TryGetProperty("id", out var idP) ? idP.GetString() ?? "" : "";
                    name = root.TryGetProperty("name", out var nP) ? nP.GetString() ?? modId : modId;
                    version = root.TryGetProperty("version", out var vP) ? vP.GetString() ?? "unknown" : "unknown";

                    if (root.TryGetProperty("icon", out var iconP))
                    {
                        if (iconP.ValueKind == JsonValueKind.String)
                            iconPath = iconP.GetString();
                        else if (iconP.ValueKind == JsonValueKind.Object)
                            iconPath = iconP.EnumerateObject().FirstOrDefault().Value.GetString();
                    }
                }
                else
                {
                    var tomlEntry = archive.GetEntry("META-INF/mods.toml");
                    if (tomlEntry != null)
                    {
                        using var stream = tomlEntry.Open();
                        using var reader = new StreamReader(stream);
                        var toml = reader.ReadToEnd();
                        var lines = toml.Split('\n');
                        foreach (var line in lines)
                        {
                            var l = line.Trim();
                            if (l.StartsWith("modId=")) modId = l.Split('=')[1].Trim().Trim('"', '\'');
                            if (l.StartsWith("version=")) version = l.Split('=')[1].Trim().Trim('"', '\'');
                            if (l.StartsWith("logoFile=")) iconPath = l.Split('=')[1].Trim().Trim('"', '\'');
                        }
                        if (string.IsNullOrEmpty(modId)) return null;
                        name = modId;
                    }
                    else return null;
                }

                Microsoft.UI.Xaml.Media.ImageSource? imageSource = null;
                if (!string.IsNullOrEmpty(iconPath))
                {
                    var iconEntry = archive.GetEntry(iconPath);
                    if (iconEntry != null)
                    {
                        using var stream = iconEntry.Open();
                        using var ms = new MemoryStream();
                        await stream.CopyToAsync(ms);
                        ms.Seek(0, SeekOrigin.Begin);

                        var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                        bitmap.DecodePixelWidth = 80;
                        await bitmap.SetSourceAsync(ms.AsRandomAccessStream());
                        imageSource = bitmap;
                    }
                }

                return new ModItem { FilePath = jarPath, ModId = modId, Name = name, CurrentVersion = version, ImageSource = imageSource };
            }
            catch { }
            return null;
        }

        private async void CheckModsUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            if (Mods.Count == 0)
            {
                await ShowDialogAsync("提示", "当前列表没有模组，请先点击左侧的“模组”加载列表。");
                return;
            }

            btn.IsEnabled = false;
            btn.Content = "正在检查...";

            int checkCount = 0;
            int foundUpdateCount = 0;
            int errorCount = 0;

            foreach (var mod in Mods)
            {
                mod.UpdateStatus = "查询中...";
                mod.UpdateBtnVisibility = Visibility.Collapsed;
                mod.LatestDownloadUrl = "";
                mod.LatestFileName = "";

                await Task.Delay(20);

                try
                {
                    string query = Uri.EscapeDataString(mod.ModId);
                    string url = $"https://api.modrinth.com/v2/search?query={query}&limit=1";
                    var res = await Http.GetStringAsync(url);

                    using var doc = JsonDocument.Parse(res);
                    var hits = doc.RootElement.GetProperty("hits");

                    if (hits.GetArrayLength() > 0)
                    {
                        var projectId = hits[0].GetProperty("project_id").GetString();

                        string gameVer = InfoGameVersionText.Text;
                        if (gameVer == "未知" || string.IsNullOrEmpty(gameVer)) gameVer = "";

                        string verUrl = $"https://api.modrinth.com/v2/project/{projectId}/version?game_versions=%5B%22{gameVer}%22%5D";
                        var verRes = await Http.GetStringAsync(verUrl);
                        using var verDoc = JsonDocument.Parse(verRes);

                        if (verDoc.RootElement.GetArrayLength() > 0)
                        {
                            var latestVersionObj = verDoc.RootElement[0];
                            var latestVer = latestVersionObj.GetProperty("version_number").GetString();

                            if (latestVer != mod.CurrentVersion)
                            {
                                // 🚨 提取下载链接
                                var files = latestVersionObj.GetProperty("files");
                                string downloadUrl = "";
                                string fileName = "";

                                if (files.GetArrayLength() > 0)
                                {
                                    downloadUrl = files[0].GetProperty("url").GetString() ?? "";
                                    fileName = files[0].GetProperty("filename").GetString() ?? "";
                                }

                                if (!string.IsNullOrEmpty(downloadUrl))
                                {
                                    mod.LatestVersion = latestVer;
                                    mod.LatestDownloadUrl = downloadUrl;
                                    mod.LatestFileName = fileName;
                                    mod.UpdateStatus = $"可更新至 {latestVer}";
                                    mod.UpdateBtnVisibility = Visibility.Visible;
                                    foundUpdateCount++;
                                }
                                else
                                {
                                    mod.UpdateStatus = "无下载链接";
                                }
                            }
                            else
                            {
                                mod.UpdateStatus = "已是最新";
                            }
                        }
                        else
                        {
                            mod.UpdateStatus = "无当前版本";
                        }
                    }
                    else
                    {
                        mod.UpdateStatus = "未收录";
                    }
                }
                catch (Exception ex)
                {
                    mod.UpdateStatus = "网络错误";
                    System.Diagnostics.Debug.WriteLine($"[Update] {mod.ModId} 失败: {ex.Message}");
                    errorCount++;
                }

                checkCount++;
            }

            btn.Content = "检查更新";
            btn.IsEnabled = true;

            await ShowDialogAsync("检查完毕", $"共检查了 {checkCount} 个模组。\n\n✅ 发现 {foundUpdateCount} 个可更新。\n❌ 失败 {errorCount} 个。");
        }

        // 🚨 核心更新：下载并替换
        private async void UpdateMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not ModItem mod) return;

            if (string.IsNullOrEmpty(mod.LatestDownloadUrl))
            {
                await ShowDialogAsync("提示", "下载链接无效，请重新点击上方【检查更新】。");
                return;
            }

            btn.IsEnabled = false;
            btn.Content = "更新中...";
            mod.UpdateStatus = "下载中...";

            try
            {
                // 1. 下载新文件
                var bytes = await Http.GetByteArrayAsync(mod.LatestDownloadUrl);

                // 2. 确定保存路径
                string dir = Path.GetDirectoryName(mod.FilePath) ?? "";
                string newFilePath = Path.Combine(dir, string.IsNullOrEmpty(mod.LatestFileName) ? Path.GetFileName(mod.FilePath) : mod.LatestFileName);

                // 3. 如果文件名不同，先删旧文件，再存新文件
                if (mod.FilePath != newFilePath && File.Exists(mod.FilePath))
                {
                    File.Delete(mod.FilePath);
                }

                await File.WriteAllBytesAsync(newFilePath, bytes);

                // 4. 更新 UI 和数据
                mod.FilePath = newFilePath;
                mod.CurrentVersion = mod.LatestVersion;
                mod.UpdateStatus = "已是最新";
                mod.UpdateBtnVisibility = Visibility.Collapsed;

                btn.Content = "更新";
                btn.IsEnabled = true;

                await ShowDialogAsync("更新成功", $"模组 {mod.Name} 已更新至 {mod.LatestVersion}！\n\n建议重启游戏加载。");
            }
            catch (Exception ex)
            {
                mod.UpdateStatus = "更新失败";
                btn.Content = "重试";
                btn.IsEnabled = true;
                await ShowDialogAsync("更新失败", $"下载或替换文件失败：\n{ex.Message}\n\n如果是网络问题，请检查代理。如果文件被占用，请关闭游戏。");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack) this.Frame.GoBack();
            else this.Frame.Navigate(typeof(Page1));
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            LauncherState.CurrentVersionName = _versionName;
            LauncherState.CurrentMinecraftPath = _minecraftPath;
            if (App.MainWindow is MainWindow mw) mw.TriggerLaunch();
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;
            string path = tag == "versions" ? _versionDir : Path.Combine(_versionDir, tag);
            try
            {
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });
            }
            catch (Exception ex) { _ = ShowDialogAsync("打开失败", ex.Message); }
        }

        private async void RenameVersion_Click(object sender, RoutedEventArgs e)
        {
            if (this.XamlRoot == null) return;
            var input = new TextBox { Text = _versionName };
            var dialog = new ContentDialog { Title = "重命名显示名", Content = input, PrimaryButtonText = "确定", CloseButtonText = "取消", XamlRoot = this.XamlRoot };
            try
            {
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    var newName = input.Text?.Trim();
                    if (!string.IsNullOrEmpty(newName)) VersionTitleText.Text = newName;
                }
            }
            catch { }
        }

        private async void DeleteVersion_Click(object sender, RoutedEventArgs e)
        {
            if (this.XamlRoot == null) return;
            var confirm = new ContentDialog { Title = "确认删除版本？", Content = new TextBlock { Text = $"将永久删除以下文件夹：\n\n{_versionDir}\n\n此操作不可恢复！", TextWrapping = TextWrapping.Wrap }, PrimaryButtonText = "永久删除", CloseButtonText = "取消", DefaultButton = ContentDialogButton.Close, XamlRoot = this.XamlRoot };
            try
            {
                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                {
                    try
                    {
                        if (Directory.Exists(_versionDir)) Directory.Delete(_versionDir, true);
                        if (LauncherState.CurrentVersionName == _versionName) LauncherState.CurrentVersionName = null;
                        BackButton_Click(null, null);
                    }
                    catch (Exception ex) { await ShowDialogAsync("删除失败", ex.Message); }
                }
            }
            catch { }
        }

        private async Task ShowDialogAsync(string title, string message)
        {
            if (this.XamlRoot == null) return;
            var dialog = new ContentDialog { Title = title, Content = message, CloseButtonText = "确定", XamlRoot = this.XamlRoot };
            try { await dialog.ShowAsync(); } catch { }
        }
    }
}