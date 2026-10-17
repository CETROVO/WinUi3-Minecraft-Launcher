using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        public ObservableCollection<LoaderItem> Categories { get; set; } = new();

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
            Categories.Clear();
            _fabricItem = null; _fabricApiItem = null; _quiltItem = null; _forgeItem = null; _neoForgeItem = null; _optiFineItem = null;

            Categories.Add(new LoaderItem { Name = "原版 (Vanilla)" });
            _fabricItem = new LoaderItem { Name = "Fabric" }; Categories.Add(_fabricItem);
            _quiltItem = new LoaderItem { Name = "Quilt" }; Categories.Add(_quiltItem);
            _forgeItem = new LoaderItem { Name = "Forge" }; Categories.Add(_forgeItem);
            _neoForgeItem = new LoaderItem { Name = "NeoForge" }; Categories.Add(_neoForgeItem);
            _optiFineItem = new LoaderItem { Name = "OptiFine" }; Categories.Add(_optiFineItem);

            CategoryListView.ItemsSource = Categories;
            CategoryListView.SelectedIndex = 0;
        }

        private async void CategoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryListView.SelectedItem is not LoaderItem item) return;

            DefaultHintText.Visibility = Visibility.Collapsed;
            FabricPanel.Visibility = Visibility.Collapsed;
            FabricApiPanel.Visibility = Visibility.Collapsed;
            QuiltPanel.Visibility = Visibility.Collapsed;
            ForgePanel.Visibility = Visibility.Collapsed;
            NeoForgePanel.Visibility = Visibility.Collapsed;
            OptiFinePanel.Visibility = Visibility.Collapsed;

            if (item.Name.StartsWith("原版")) { DefaultHintText.Visibility = Visibility.Visible; VersionNameTextBox.Text = _mcVersion; }
            else if (item.Name == "Fabric") { FabricPanel.Visibility = Visibility.Visible; if (FabricVersionCombo.Items.Count == 0) await LoadFabricVersionsAsync(); }
            else if (item.Name == "Fabric API") { FabricApiPanel.Visibility = Visibility.Visible; if (FabricApiVersionCombo.Items.Count == 0) await FetchFabricApiVersionsAsync(); }
            else if (item.Name == "Quilt") { QuiltPanel.Visibility = Visibility.Visible; if (QuiltVersionCombo.Items.Count == 0) await LoadQuiltVersionsAsync(); }
            else if (item.Name == "Forge") { ForgePanel.Visibility = Visibility.Visible; if (ForgeVersionCombo.Items.Count == 0) await LoadForgeVersionsAsync(); }
            else if (item.Name == "NeoForge") { NeoForgePanel.Visibility = Visibility.Visible; if (NeoForgeVersionCombo.Items.Count == 0) await LoadNeoForgeVersionsAsync(); }
            else if (item.Name == "OptiFine") { OptiFinePanel.Visibility = Visibility.Visible; if (OptiFineVersionCombo.Items.Count == 0) await LoadOptiFineVersionsAsync(); }
        }

        // =============== 版本获取逻辑 ===============

        private async Task LoadFabricVersionsAsync()
        {
            try
            {
                string url = $"https://meta.fabricmc.net/v2/versions/loader/{_mcVersion}";
                var json = await Http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var items = doc.RootElement.EnumerateArray().Select(v => v.GetProperty("loader").GetProperty("version").GetString()).ToList();
                FabricVersionCombo.ItemsSource = items;
                if (items.Count > 0) FabricVersionCombo.SelectedIndex = 0;
            }
            catch { FabricVersionCombo.ItemsSource = new List<string> { "获取失败" }; }
        }

        private async Task FetchFabricApiVersionsAsync()
        {
            try
            {
                string apiUrl = $"https://api.modrinth.com/v2/project/fabric-api/version?game_versions=%5B%22{_mcVersion}%22%5D&loaders=%5B%22fabric%22%5D";
                var response = await Http.GetStringAsync(apiUrl);
                using var doc = JsonDocument.Parse(response);
                var items = doc.RootElement.EnumerateArray().Select(v => v.GetProperty("version_number").GetString()).ToList();
                FabricApiVersionCombo.ItemsSource = items;
                if (items.Count > 0) FabricApiVersionCombo.SelectedIndex = 0;
            }
            catch { FabricApiVersionCombo.ItemsSource = new List<string> { "获取失败" }; }
        }

        private async Task LoadQuiltVersionsAsync()
        {
            try
            {
                string url = $"https://meta.quiltmc.org/v3/versions/loader/{_mcVersion}";
                var json = await Http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var items = doc.RootElement.EnumerateArray().Select(v => v.GetProperty("loader").GetProperty("version").GetString()).ToList();
                QuiltVersionCombo.ItemsSource = items;
                if (items.Count > 0) QuiltVersionCombo.SelectedIndex = 0;
            }
            catch { QuiltVersionCombo.ItemsSource = new List<string> { "获取失败" }; }
        }

        private async Task LoadForgeVersionsAsync()
        {
            try
            {
                string url = $"https://bmclapi2.bangbang93.com/forge/minecraft/{_mcVersion}";
                var json = await Http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var items = doc.RootElement.EnumerateArray().Select(v => v.GetProperty("version").GetString()).ToList();
                items = items.OrderByDescending(v => v).ToList();
                ForgeVersionCombo.ItemsSource = items;
                if (items.Count > 0) ForgeVersionCombo.SelectedIndex = 0;
            }
            catch { ForgeVersionCombo.ItemsSource = new List<string> { "获取失败" }; }
        }

        private async Task LoadNeoForgeVersionsAsync()
        {
            try
            {
                string url = _mcVersion == "1.20.1"
                    ? "https://maven.neoforged.net/releases/net/neoforged/forge/maven-metadata.xml"
                    : "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";

                var xml = await Http.GetStringAsync(url);
                var doc = XDocument.Parse(xml);
                var versions = doc.Descendants("version").Select(x => x.Value).ToList();

                string prefix = _mcVersion == "1.20.1" ? "47." : _mcVersion.Replace("1.", "");
                var filtered = versions.Where(v => v.StartsWith(prefix)).ToList();
                filtered.Reverse();

                NeoForgeVersionCombo.ItemsSource = filtered;
                if (filtered.Count > 0) NeoForgeVersionCombo.SelectedIndex = 0;
            }
            catch { NeoForgeVersionCombo.ItemsSource = new List<string> { "获取失败" }; }
        }

        private async Task LoadOptiFineVersionsAsync()
        {
            try
            {
                string url = $"https://bmclapi2.bangbang93.com/optifine/{_mcVersion}";
                var json = await Http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var items = doc.RootElement.EnumerateArray().Select(v => v.GetProperty("type").GetString()).ToList();
                OptiFineVersionCombo.ItemsSource = items;
                if (items.Count > 0) OptiFineVersionCombo.SelectedIndex = 0;
            }
            catch { OptiFineVersionCombo.ItemsSource = new List<string> { "获取失败" }; }
        }

        // =============== 选中联动逻辑 ===============

        private async void FabricVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FabricVersionCombo.SelectedItem is not string ver) return;
            _fabricItem.SelectedVersion = ver;
            VersionNameTextBox.Text = $"{_mcVersion}-Fabric_{ver}";

            if (_fabricApiItem == null)
            {
                _fabricApiItem = new LoaderItem { Name = "Fabric API" };
                Categories.Insert(Categories.IndexOf(_fabricItem) + 1, _fabricApiItem);
            }
            if (FabricApiVersionCombo.Items.Count == 0) await FetchFabricApiVersionsAsync();
        }

        private void FabricApiVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FabricApiVersionCombo.SelectedItem is not string ver) return;
            _selectedFabricApiVer = ver;
            if (_fabricApiItem != null) _fabricApiItem.SelectedVersion = ver;
            if (FabricApiToggle.IsOn)
                VersionNameTextBox.Text = $"{_mcVersion}-Fabric_{_fabricItem?.SelectedVersion}+API_{ver}";
        }

        private void FabricApiToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (FabricApiVersionCombo != null) FabricApiVersionCombo.IsEnabled = FabricApiToggle.IsOn;
            if (_fabricItem != null && !string.IsNullOrEmpty(_fabricItem.SelectedVersion))
            {
                if (FabricApiToggle.IsOn && !string.IsNullOrEmpty(_selectedFabricApiVer))
                    VersionNameTextBox.Text = $"{_mcVersion}-Fabric_{_fabricItem.SelectedVersion}+API_{_selectedFabricApiVer}";
                else
                    VersionNameTextBox.Text = $"{_mcVersion}-Fabric_{_fabricItem.SelectedVersion}";
            }
        }

        private void QuiltVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuiltVersionCombo.SelectedItem is not string ver) return;
            _quiltItem.SelectedVersion = ver;
            VersionNameTextBox.Text = $"{_mcVersion}-Quilt_{ver}";
        }

        private void ForgeVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ForgeVersionCombo.SelectedItem is not string ver) return;
            _forgeItem.SelectedVersion = ver;
            VersionNameTextBox.Text = $"{_mcVersion}-Forge_{ver}";
        }

        private void NeoForgeVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NeoForgeVersionCombo.SelectedItem is not string ver) return;
            _neoForgeItem.SelectedVersion = ver;
            VersionNameTextBox.Text = $"{_mcVersion}-NeoForge_{ver}";
        }

        private void OptiFineVersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (OptiFineVersionCombo.SelectedItem is not string ver) return;
            _optiFineItem.SelectedVersion = ver;
            VersionNameTextBox.Text = $"{_mcVersion}-OptiFine_{ver}";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack) this.Frame.GoBack();
            else this.Frame.Navigate(typeof(Page2));
        }

        // =============== 下载和安装核心逻辑 ===============

        private async void StartDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading) return;

            string targetName = VersionNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetName)) { await ShowDialogAsync("提示", "实例名称不能为空！"); return; }

            // 🚨 过滤 Windows 不允许的文件夹字符
            if (targetName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                await ShowDialogAsync("提示", "实例名称包含非法字符，请重新输入！");
                return;
            }

            _isDownloading = true;
            StartDownloadButton.IsEnabled = false;
            StartButtonText.Text = "正在下载原版...";

            try
            {
                var minecraftPath = new MinecraftPath(LauncherState.CurrentMinecraftPath);
                var launcher = new MinecraftLauncher(minecraftPath);

                // 下载原版
                await launcher.InstallAsync(_mcVersion);

                // 分发安装
                if (_fabricItem != null && !string.IsNullOrEmpty(_fabricItem.SelectedVersion))
                {
                    StartButtonText.Text = "安装 Fabric...";
                    await InstallFabricAsync(minecraftPath.BasePath, _mcVersion, _fabricItem.SelectedVersion, targetName);
                    if (FabricApiToggle.IsOn && !string.IsNullOrEmpty(_selectedFabricApiVer))
                        await DownloadFabricApiAsync(minecraftPath.BasePath, _mcVersion, _fabricItem.SelectedVersion, _selectedFabricApiVer, targetName);
                }
                else if (_quiltItem != null && !string.IsNullOrEmpty(_quiltItem.SelectedVersion))
                {
                    StartButtonText.Text = "安装 Quilt...";
                    await InstallQuiltAsync(minecraftPath.BasePath, _mcVersion, _quiltItem.SelectedVersion, targetName);
                }
                else if (_forgeItem != null && !string.IsNullOrEmpty(_forgeItem.SelectedVersion))
                {
                    StartButtonText.Text = "下载并安装 Forge...";
                    await InstallForgeAsync(minecraftPath.BasePath, _mcVersion, _forgeItem.SelectedVersion, targetName);
                }
                else if (_neoForgeItem != null && !string.IsNullOrEmpty(_neoForgeItem.SelectedVersion))
                {
                    StartButtonText.Text = "下载并安装 NeoForge...";
                    await InstallNeoForgeAsync(minecraftPath.BasePath, _mcVersion, _neoForgeItem.SelectedVersion, targetName);
                }
                else if (_optiFineItem != null && !string.IsNullOrEmpty(_optiFineItem.SelectedVersion))
                {
                    StartButtonText.Text = "下载 OptiFine 安装器...";
                    await InstallOptiFineAsync(minecraftPath.BasePath, _mcVersion, _optiFineItem.SelectedVersion);
                    await ShowDialogAsync("提示", "OptiFine 安装器已下载并启动。\n\n由于 OptiFine 官方限制，无法完全静默安装。请在弹出的窗口中点击 Install 完成安装。");
                }
                else
                {
                    if (targetName != _mcVersion) RenameVanillaVersion(minecraftPath.BasePath, _mcVersion, targetName);
                }

                LauncherState.CurrentVersionName = targetName;
                LauncherState.CurrentMinecraftPath = minecraftPath.BasePath;
                if (App.MainWindow is MainWindow mw) mw.UpdateVersionDisplay();

                if (_optiFineItem == null || string.IsNullOrEmpty(_optiFineItem.SelectedVersion))
                    await ShowDialogAsync("成功", $"实例 {targetName} 安装完成！");

                BackButton_Click(null, null);
            }
            catch (Win32Exception ex)
            {
                // 🚨 专治 Process.Start() 找不到 Java 导致底层的直接崩溃
                await ShowDialogAsync("系统错误", $"启动安装进程失败，可能是 Java 路径不正确或权限不足。\n详细信息：{ex.Message}");
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("失败", $"安装失败: {ex.Message}");
            }
            finally
            {
                _isDownloading = false;
                StartDownloadButton.IsEnabled = true;
                StartButtonText.Text = "开始下载";
            }
        }

        // =============== 具体安装实现 ===============

        private void RenameVanillaVersion(string mcDir, string oldName, string newName)
        {
            string oldDir = Path.Combine(mcDir, "versions", oldName);
            string newDir = Path.Combine(mcDir, "versions", newName);

            if (Directory.Exists(newDir)) throw new Exception($"版本 {newName} 已存在！");
            Directory.Move(oldDir, newDir);

            string oldJar = Path.Combine(newDir, $"{oldName}.jar");
            string newJar = Path.Combine(newDir, $"{newName}.jar");
            if (File.Exists(oldJar)) File.Move(oldJar, newJar);

            string oldJson = Path.Combine(newDir, $"{oldName}.json");
            string newJson = Path.Combine(newDir, $"{newName}.json");
            if (File.Exists(oldJson))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(oldJson));
                dict["id"] = newName;
                File.WriteAllText(oldJson, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
                File.Move(oldJson, newJson);
            }
        }

        private async Task InstallFabricAsync(string mcDir, string mcVersion, string loaderVersion, string targetName)
        {
            string profileUrl = $"https://meta.fabricmc.net/v2/versions/loader/{mcVersion}/{loaderVersion}/profile/json";
            var profileJson = await Http.GetStringAsync(profileUrl);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(profileJson);
            dict["id"] = targetName;

            string fabricDir = Path.Combine(mcDir, "versions", targetName);
            if (Directory.Exists(fabricDir)) Directory.Delete(fabricDir, true);
            Directory.CreateDirectory(fabricDir);

            string fabricJsonPath = Path.Combine(fabricDir, $"{targetName}.json");
            await File.WriteAllTextAsync(fabricJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));

            string vanillaJar = Path.Combine(mcDir, "versions", mcVersion, $"{mcVersion}.jar");
            string fabricJar = Path.Combine(fabricDir, $"{targetName}.jar");
            if (File.Exists(vanillaJar)) File.Copy(vanillaJar, fabricJar, true);
        }

        private async Task DownloadFabricApiAsync(string mcDir, string mcVersion, string loaderVersion, string apiVersion, string targetName)
        {
            string apiUrl = $"https://api.modrinth.com/v2/project/fabric-api/version?game_versions=%5B%22{mcVersion}%22%5D&loaders=%5B%22fabric%22%5D";
            var response = await Http.GetStringAsync(apiUrl);
            using var doc = JsonDocument.Parse(response);

            foreach (var v in doc.RootElement.EnumerateArray())
            {
                if (v.GetProperty("version_number").GetString() == apiVersion)
                {
                    var files = v.GetProperty("files");
                    string downloadUrl = files[0].GetProperty("url").GetString();
                    string fileName = files[0].GetProperty("filename").GetString();

                    string modsDir = Path.Combine(mcDir, "versions", targetName, "mods");
                    Directory.CreateDirectory(modsDir);

                    var bytes = await Http.GetByteArrayAsync(downloadUrl);
                    await File.WriteAllBytesAsync(Path.Combine(modsDir, fileName), bytes);
                    break;
                }
            }
        }

        private async Task InstallQuiltAsync(string mcDir, string mcVersion, string loaderVersion, string targetName)
        {
            string profileUrl = $"https://meta.quiltmc.org/v3/versions/loader/{mcVersion}/{loaderVersion}/profile/json";
            var profileJson = await Http.GetStringAsync(profileUrl);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(profileJson);
            dict["id"] = targetName;

            string quiltDir = Path.Combine(mcDir, "versions", targetName);
            if (Directory.Exists(quiltDir)) Directory.Delete(quiltDir, true);
            Directory.CreateDirectory(quiltDir);

            string quiltJsonPath = Path.Combine(quiltDir, $"{targetName}.json");
            await File.WriteAllTextAsync(quiltJsonPath, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));

            string vanillaJar = Path.Combine(mcDir, "versions", mcVersion, $"{mcVersion}.jar");
            string quiltJar = Path.Combine(quiltDir, $"{targetName}.jar");
            if (File.Exists(vanillaJar)) File.Copy(vanillaJar, quiltJar, true);
        }

        private async Task InstallForgeAsync(string mcDir, string mcVersion, string forgeVersion, string targetName)
        {
            string installerUrl = $"https://bmclapi2.bangbang93.com/forge/download?mcversion={mcVersion}&version={forgeVersion}&category=installer&format=jar";
            string installerPath = Path.Combine(Path.GetTempPath(), $"forge-installer-{mcVersion}-{forgeVersion}.jar");

            var bytes = await Http.GetByteArrayAsync(installerUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);

            string javaExe = JavaFinder.FindJava();
            if (string.IsNullOrEmpty(javaExe)) throw new Exception("找不到 Java，无法运行 Forge 安装器。");

            StartButtonText.Text = "正在静默运行 Forge 安装器...";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = $"-jar \"{installerPath}\" --installClient \"{mcDir}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0) throw new Exception($"Forge 安装失败，退出码: {process.ExitCode}");
            try { File.Delete(installerPath); } catch { }

            string defaultForgeDir = Path.Combine(mcDir, "versions", $"{mcVersion}-forge-{forgeVersion}");
            if (targetName != $"{mcVersion}-forge-{forgeVersion}")
            {
                string newDir = Path.Combine(mcDir, "versions", targetName);
                if (Directory.Exists(newDir)) Directory.Delete(newDir, true);
                Directory.Move(defaultForgeDir, newDir);

                string oldJar = Path.Combine(newDir, $"{mcVersion}-forge-{forgeVersion}.jar");
                string newJar = Path.Combine(newDir, $"{targetName}.jar");
                if (File.Exists(oldJar)) File.Move(oldJar, newJar);

                string oldJson = Path.Combine(newDir, $"{mcVersion}-forge-{forgeVersion}.json");
                string newJson = Path.Combine(newDir, $"{targetName}.json");
                if (File.Exists(oldJson))
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(oldJson));
                    dict["id"] = targetName;
                    File.WriteAllText(oldJson, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
                    File.Move(oldJson, newJson);
                }
            }
        }

        private async Task InstallNeoForgeAsync(string mcDir, string mcVersion, string neoVer, string targetName)
        {
            string installerUrl = mcVersion == "1.20.1"
                ? $"https://maven.neoforged.net/releases/net/neoforged/forge/{neoVer}/forge-{neoVer}-installer.jar"
                : $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{neoVer}/neoforge-{neoVer}-installer.jar";

            string installerPath = Path.Combine(Path.GetTempPath(), $"neoforge-installer-{neoVer}.jar");
            var bytes = await Http.GetByteArrayAsync(installerUrl);
            await File.WriteAllBytesAsync(installerPath, bytes);

            string javaExe = JavaFinder.FindJava();
            if (string.IsNullOrEmpty(javaExe)) throw new Exception("找不到 Java，无法运行安装器。");

            StartButtonText.Text = "正在运行 NeoForge 安装器...";
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = $"-jar \"{installerPath}\" --installClient \"{mcDir}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0) throw new Exception($"NeoForge 安装失败，退出码: {process.ExitCode}");
            try { File.Delete(installerPath); } catch { }

            string defaultDirName = mcVersion == "1.20.1" ? $"{mcVersion}-forge-{neoVer}" : $"neoforge-{neoVer}";
            string defaultDir = Path.Combine(mcDir, "versions", defaultDirName);

            if (targetName != defaultDirName && Directory.Exists(defaultDir))
            {
                string newDir = Path.Combine(mcDir, "versions", targetName);
                if (Directory.Exists(newDir)) Directory.Delete(newDir, true);
                Directory.Move(defaultDir, newDir);

                string oldJar = Path.Combine(newDir, $"{defaultDirName}.jar");
                string newJar = Path.Combine(newDir, $"{targetName}.jar");
                if (File.Exists(oldJar)) File.Move(oldJar, newJar);

                string oldJson = Path.Combine(newDir, $"{defaultDirName}.json");
                string newJson = Path.Combine(newDir, $"{targetName}.json");
                if (File.Exists(oldJson))
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(oldJson));
                    dict["id"] = targetName;
                    File.WriteAllText(oldJson, JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
                    File.Move(oldJson, newJson);
                }
            }
        }

        private async Task InstallOptiFineAsync(string mcDir, string mcVersion, string optiFineType)
        {
            string url = $"https://bmclapi2.bangbang93.com/optifine/{mcVersion}/{optiFineType}";
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            string downloadUrl = doc.RootElement.GetProperty("url").GetString();

            string targetJar = Path.Combine(mcDir, $"OptiFine_{mcVersion}_{optiFineType}.jar");
            var bytes = await Http.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(targetJar, bytes);

            string javaExe = JavaFinder.FindJava();
            if (!string.IsNullOrEmpty(javaExe))
            {
                try
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo { FileName = javaExe, Arguments = $"-jar \"{targetJar}\"", UseShellExecute = true }
                    };
                    process.Start();
                }
                catch (Win32Exception ex)
                {
                    throw new Exception($"启动 OptiFine 安装器失败，请检查 Java 是否损坏。\n{ex.Message}");
                }
            }
            else
            {
                throw new Exception("找不到 Java，无法启动 OptiFine 安装器。");
            }
        }

        private async Task ShowDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog { Title = title, Content = message, CloseButtonText = "确定", XamlRoot = this.XamlRoot };
            try { await dialog.ShowAsync(); } catch { }
        }
    }
}