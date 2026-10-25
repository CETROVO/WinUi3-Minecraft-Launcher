using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.UI;

namespace WinUi3_Minecraft_Launcher
{
    public class InstalledVersionItem
    {
        public string Name { get; set; } = "";
        public string McVersion { get; set; } = "";
        public string Loader { get; set; } = "";
        public override string ToString() => Name;
    }

    public sealed partial class Page2 : Page
    {
        private List<RemoteVersion> _allVersions = new();
        private bool _isDataLoaded = false;
        private string _currentProjectType = "mod";

        private InstalledVersionItem? _targetInstance = null;
        private readonly HttpClient Http = new HttpClient();

        public Page2()
        {
            this.InitializeComponent();
            Http.Timeout = TimeSpan.FromSeconds(15);
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("WML-Launcher/1.0");
            this.Loaded += Page2_Loaded;
        }

        private async void Page2_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isDataLoaded)
            {
                DownloadCategoryList.ItemsSource = new List<string> { "游戏", "模组", "资源包", "整合包", "光影包" };
                DownloadCategoryList.SelectedIndex = 0;
                SourceComboBox.SelectedIndex = (int)LauncherState.CurrentSource;
                await LoadVersionsAsync();
            }
        }

        private void DownloadCategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DownloadCategoryList.SelectedItem is not string category) return;

            GamePanel.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Collapsed;

            if (category == "游戏")
            {
                GamePanel.Visibility = Visibility.Visible;
            }
            else
            {
                SearchPanel.Visibility = Visibility.Visible;
                SearchResultList.ItemsSource = null;
                SearchBox.Text = "";

                _currentProjectType = category switch
                {
                    "模组" => "mod",
                    "资源包" => "resourcepack",
                    "整合包" => "modpack",
                    "光影包" => "shader",
                    _ => "mod"
                };
                LoadTargetInstances();
            }
        }

        private void LoadTargetInstances()
        {
            TargetInstanceComboBox.Items.Clear();
            _targetInstance = null;
            if (string.IsNullOrEmpty(LauncherState.CurrentMinecraftPath)) return;

            string versionsDir = Path.Combine(LauncherState.CurrentMinecraftPath, "versions");
            if (!Directory.Exists(versionsDir)) return;

            var dirs = Directory.GetDirectories(versionsDir);
            var items = new List<InstalledVersionItem>();

            foreach (var dir in dirs)
            {
                string name = Path.GetFileName(dir);
                if (!File.Exists(Path.Combine(dir, name + ".json"))) continue;

                string mcVer = Regex.Match(name, @"\d+\.\d+(\.\d+)?").Value;
                if (string.IsNullOrEmpty(mcVer)) continue;

                string loader = "vanilla";
                string nameLower = name.ToLower();
                if (nameLower.Contains("fabric")) loader = "fabric";
                else if (nameLower.Contains("forge")) loader = "forge";
                else if (nameLower.Contains("quilt")) loader = "quilt";

                items.Add(new InstalledVersionItem { Name = name, McVersion = mcVer, Loader = loader });
            }

            var current = items.FirstOrDefault(x => x.Name == LauncherState.CurrentVersionName) ?? items.FirstOrDefault();
            foreach (var item in items) TargetInstanceComboBox.Items.Add(item);

            if (current != null)
            {
                TargetInstanceComboBox.SelectedItem = current;
                _targetInstance = current;
            }
        }

        private void TargetInstanceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _targetInstance = TargetInstanceComboBox.SelectedItem as InstalledVersionItem;
        }

        private string GetModrinthApiUrl(string path, bool useMirror)
        {
            string baseUrl = useMirror
                ? "https://mod.mcimirror.top/modrinth"
                : "https://api.modrinth.com";
            return $"{baseUrl}{path}";
        }

        private string GetModrinthCdnUrl(string path, bool useMirror)
        {
            if (string.IsNullOrEmpty(path)) return "";
            if (useMirror && path.StartsWith("https://cdn.modrinth.com/"))
                return path.Replace("https://cdn.modrinth.com/", "https://mod.mcimirror.top/cdn/");
            return path;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<bool, Task<T>> action, string actionName, bool forceStartMirror = false)
        {
            int maxRetries = 5;

            for (int i = 0; i < maxRetries; i++)
            {
                bool useMirror = false;
                if (LauncherState.CurrentSource == DownloadSource.Mirror) useMirror = true;
                else if (LauncherState.CurrentSource == DownloadSource.Official) useMirror = false;
                else useMirror = forceStartMirror ? (i % 2 == 0) : (i % 2 != 0);

                try
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        DownloadStatusBorder.Visibility = Visibility.Visible;
                        string sourceStr = useMirror ? "镜像源" : "官方源";
                        DownloadStatusText.Text = $"正在请求 {actionName} ({sourceStr})，重试：{i}/{maxRetries}";
                    });

                    return await action(useMirror);
                }
                catch (Exception ex)
                {
                    if (i == maxRetries - 1)
                    {
                        DispatcherQueue.TryEnqueue(() => DownloadStatusBorder.Visibility = Visibility.Collapsed);
                        throw new Exception($"连续 {maxRetries} 次请求失败（当前源：{(useMirror ? "镜像" : "官方")}）：{ex.Message}");
                    }
                    await Task.Delay(1000);
                }
            }
            return default!;
        }

        // 🚨 新增：安全下载文件，防 404、防损坏
        private async Task<byte[]> DownloadFileBytesWithRetryAsync(string url, string fileName)
        {
            return await ExecuteWithRetryAsync(async (useMirror) =>
            {
                var realUrl = GetModrinthCdnUrl(url, useMirror);
                using var response = await Http.GetAsync(realUrl, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"HTTP {(int)response.StatusCode}");

                var bytes = await response.Content.ReadAsByteArrayAsync();

                // 简单校验：如果文件小于 500 字节，极大概率是错误页面或损坏
                if (bytes.Length < 500 && fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("下载的文件过小，可能已损坏或未同步至镜像。");

                return bytes;
            }, $"下载 {fileName}");
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e) => await PerformSearchAsync();

        private async void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) await PerformSearchAsync();
        }

        private async Task PerformSearchAsync()
        {
            string originalQuery = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(originalQuery)) return;
            if (_targetInstance == null) { await ShowDialogAsync("提示", "请先在上方选择要安装到的版本实例！"); return; }

            SearchLoadingPanel.Visibility = Visibility.Visible;
            SearchResultList.Visibility = Visibility.Collapsed;
            SearchResultList.ItemsSource = null;
            DownloadStatusBorder.Visibility = Visibility.Collapsed;

            try
            {
                string searchQuery = TranslationService.TranslateQuery(originalQuery);
                bool isStillChinese = Regex.IsMatch(searchQuery, @"[\u4e00-\u9fa5]");
                bool forceStartMirror = isStillChinese && LauncherState.CurrentSource == DownloadSource.Auto;

                var facetList = new List<string> { $"[\"project_type:{_currentProjectType}\"]" };

                if (_currentProjectType == "mod" || _currentProjectType == "modpack")
                {
                    if (_targetInstance.Loader != "vanilla")
                        facetList.Add($"[\"categories:{_targetInstance.Loader}\"]");
                }

                if (!string.IsNullOrEmpty(_targetInstance.McVersion))
                    facetList.Add($"[\"versions:{_targetInstance.McVersion}\"]");

                string facets = $"[{string.Join(",", facetList)}]";
                string path = $"/v2/search?query={Uri.EscapeDataString(searchQuery)}&facets={Uri.EscapeDataString(facets)}&limit=20";

                var response = await ExecuteWithRetryAsync((useMirror) => Http.GetStringAsync(GetModrinthApiUrl(path, useMirror)), "搜索列表", forceStartMirror);

                using var doc = JsonDocument.Parse(response);
                var hits = doc.RootElement.GetProperty("hits");

                if (hits.GetArrayLength() == 0)
                {
                    await ShowDialogAsync("无结果", $"没有找到与“{originalQuery}”相关的资源。");
                    return;
                }

                var projects = new List<ModrinthProject>();
                foreach (var hit in hits.EnumerateArray())
                {
                    projects.Add(new ModrinthProject
                    {
                        ProjectId = hit.GetProperty("project_id").GetString() ?? "",
                        Title = hit.GetProperty("title").GetString() ?? "",
                        Description = hit.GetProperty("description").GetString() ?? "",
                        IconUrl = hit.TryGetProperty("icon_url", out var icon) ? (icon.GetString() ?? "") : "",
                        Downloads = hit.GetProperty("downloads").GetInt32(),
                        ProjectType = _currentProjectType
                    });
                }

                SearchResultList.ItemsSource = projects;
                _ = LoadSearchIconsAsync(projects);
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("搜索失败", $"请求 Modrinth 失败。\n{ex.Message}");
            }
            finally
            {
                SearchLoadingPanel.Visibility = Visibility.Collapsed;
                SearchResultList.Visibility = Visibility.Visible;
                DownloadStatusBorder.Visibility = Visibility.Collapsed;
            }
        }

        private async Task LoadSearchIconsAsync(List<ModrinthProject> projects)
        {
            foreach (var p in projects)
            {
                if (string.IsNullOrEmpty(p.IconUrl)) continue;
                try
                {
                    bool useMirror = LauncherState.CurrentSource == DownloadSource.Mirror;
                    var cdnUrl = GetModrinthCdnUrl(p.IconUrl, useMirror);
                    var bytes = await Http.GetByteArrayAsync(cdnUrl);
                    this.DispatcherQueue.TryEnqueue(async () =>
                    {
                        try
                        {
                            var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                            await stream.WriteAsync(bytes.AsBuffer());
                            stream.Seek(0);
                            var bitmap = new BitmapImage { DecodePixelWidth = 100 };
                            await bitmap.SetSourceAsync(stream);
                            p.ImageSource = bitmap;
                        }
                        catch { }
                    });
                }
                catch { }
            }
        }

        private async void DownloadResource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not ModrinthProject project) return;
            if (_targetInstance == null) { await ShowDialogAsync("提示", "请先选择目标实例！"); return; }

            btn.IsEnabled = false;
            btn.Content = "解析中...";
            DownloadStatusBorder.Visibility = Visibility.Collapsed;

            try
            {
                string versionPath = Path.Combine(LauncherState.CurrentMinecraftPath, "versions", _targetInstance.Name);

                if (_currentProjectType == "mod")
                {
                    var processedMods = new HashSet<string>();
                    var report = new System.Text.StringBuilder();
                    report.AppendLine($"📦 开始安装 {project.Title} 及其前置...\n");

                    await DownloadModWithDependenciesAsync(project.ProjectId, project.Title, versionPath, report, processedMods, 0);

                    var result = await ShowDialogAsync("安装完成", report.ToString(), true);
                    if (result == ContentDialogResult.Primary)
                    {
                        string modsDir = Path.Combine(versionPath, "mods");
                        if (Directory.Exists(modsDir))
                            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{modsDir}\"", UseShellExecute = true });
                    }
                }
                else
                {
                    await DownloadSimpleResourceAsync(project.ProjectId, versionPath);
                }
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("下载失败", $"发生错误：{ex.Message}");
            }
            finally
            {
                btn.IsEnabled = true;
                btn.Content = "获取下载";
                DownloadStatusBorder.Visibility = Visibility.Collapsed;
            }
        }

        private async Task DownloadModWithDependenciesAsync(string projectId, string projectTitle, string versionPath, System.Text.StringBuilder report, HashSet<string> processed, int depth)
        {
            if (depth > 5) return;
            if (processed.Contains(projectId)) return;
            processed.Add(projectId);

            string actualSlug = "";
            string projectInfoPath = $"/v2/project/{projectId}";
            try
            {
                var infoRes = await ExecuteWithRetryAsync((useMirror) => Http.GetStringAsync(GetModrinthApiUrl(projectInfoPath, useMirror)), "获取模组信息");
                using var infoDoc = JsonDocument.Parse(infoRes);
                actualSlug = infoDoc.RootElement.GetProperty("slug").GetString() ?? "";
                if (string.IsNullOrEmpty(projectTitle) || projectTitle.Contains("前置"))
                    projectTitle = infoDoc.RootElement.GetProperty("title").GetString() ?? projectId;
            }
            catch { }

            string path = $"/v2/project/{projectId}/version";
            var verRes = await ExecuteWithRetryAsync((useMirror) => Http.GetStringAsync(GetModrinthApiUrl(path, useMirror)), "获取版本列表");
            using var doc = JsonDocument.Parse(verRes);

            // 🚨 智能版本匹配（支持 1.20.1 匹配 1.20，以及 Quilt 兼容 Fabric）
            var compatibleVersions = new List<JsonElement>();
            foreach (var ver in doc.RootElement.EnumerateArray())
            {
                var gameVers = ver.GetProperty("game_versions").EnumerateArray().Select(x => x.GetString()).ToList();
                var loaders = ver.GetProperty("loaders").EnumerateArray().Select(x => x.GetString().ToLower()).ToList();

                bool loaderMatch = loaders.Contains(_targetInstance!.Loader) ||
                                   (_targetInstance.Loader == "quilt" && loaders.Contains("fabric")) ||
                                   loaders.Count == 0;

                bool versionMatch = gameVers.Contains(_targetInstance.McVersion) ||
                                    gameVers.Any(v => v.StartsWith(_targetInstance.McVersion) || _targetInstance.McVersion.StartsWith(v));

                if (versionMatch && loaderMatch)
                {
                    compatibleVersions.Add(ver);
                }
            }

            if (compatibleVersions.Count == 0)
            {
                if (depth == 0) throw new Exception($"未找到兼容 {_targetInstance.McVersion}-{_targetInstance.Loader} 的版本。");
                else report.AppendLine($"⚠️ 未找到前置 {projectTitle} 兼容当前实例的版本。");
                return;
            }

            var targetVerObj = compatibleVersions[0]; // 取第一个（最新）
            string targetVersionNumber = targetVerObj.GetProperty("version_number").GetString() ?? "";
            string versionType = targetVerObj.GetProperty("version_type").GetString() ?? "release";

            string modsDir = Path.Combine(versionPath, "mods");
            Directory.CreateDirectory(modsDir);

            string? localVersion = FindLocalModVersion(modsDir, projectId, actualSlug, projectTitle);
            bool shouldDownload = true;

            if (localVersion != null)
            {
                int cmp = VersionComparer.Compare(targetVersionNumber, localVersion);
                if (cmp <= 0)
                {
                    shouldDownload = false;
                    report.AppendLine($"⏩ {projectTitle} (本地已是最新: {localVersion})");
                }
                else if (versionType != "release")
                {
                    shouldDownload = false;
                    report.AppendLine($"🛡️ {projectTitle} (远程最新 {targetVersionNumber} 为 {versionType}，已跳过)");
                }
                else
                {
                    report.AppendLine($"🔄 {projectTitle} ({localVersion} ➔ {targetVersionNumber} Release)");
                }
            }
            else
            {
                report.AppendLine($"✅ {projectTitle} (新安装 {targetVersionNumber})");
            }

            if (shouldDownload)
            {
                var files = targetVerObj.GetProperty("files");
                if (files.GetArrayLength() > 0)
                {
                    var mainFile = files[0];
                    string downloadUrl = mainFile.GetProperty("url").GetString() ?? "";
                    string fileName = mainFile.GetProperty("filename").GetString() ?? "";
                    string savePath = Path.Combine(modsDir, fileName);

                    // 🚨 安全替换逻辑：先删旧，带重试
                    if (localVersion != null)
                    {
                        var oldFiles = Directory.GetFiles(modsDir, "*.jar").Where(f =>
                            f.Contains(actualSlug, StringComparison.OrdinalIgnoreCase) ||
                            f.Contains(projectTitle.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

                        foreach (var old in oldFiles)
                        {
                            try { File.Delete(old); }
                            catch
                            {
                                await Task.Delay(500);
                                try { File.Delete(old); } catch { } // 再试一次，防杀软占用
                            }
                        }
                    }

                    // 🚨 下载到临时文件再重命名，防止下载一半中断损坏文件
                    var fileBytes = await DownloadFileBytesWithRetryAsync(downloadUrl, fileName);
                    string tmpPath = savePath + ".download";
                    await File.WriteAllBytesAsync(tmpPath, fileBytes);

                    if (File.Exists(savePath)) { try { File.Delete(savePath); } catch { } }
                    File.Move(tmpPath, savePath);
                }
            }

            if (targetVerObj.TryGetProperty("dependencies", out var deps))
            {
                foreach (var dep in deps.EnumerateArray())
                {
                    string depType = dep.TryGetProperty("dependency_type", out var dt) ? dt.GetString() : "";
                    if (depType != "required") continue;

                    string? depProjectId = dep.TryGetProperty("project_id", out var pid) ? pid.GetString() : null;
                    string? depVersionId = dep.TryGetProperty("version_id", out var vid) ? vid.GetString() : null;

                    if (!string.IsNullOrEmpty(depProjectId) && !processed.Contains(depProjectId))
                    {
                        await DownloadModWithDependenciesAsync(depProjectId, $"前置", versionPath, report, processed, depth + 1);
                    }
                    else if (string.IsNullOrEmpty(depProjectId) && !string.IsNullOrEmpty(depVersionId) && !processed.Contains(depVersionId))
                    {
                        processed.Add(depVersionId);
                        try
                        {
                            string vPath = $"/v2/version/{depVersionId}";
                            var vRes = await ExecuteWithRetryAsync((useMirror) => Http.GetStringAsync(GetModrinthApiUrl(vPath, useMirror)), "解析特定前置版本");
                            using var vDoc = JsonDocument.Parse(vRes);
                            var vObj = vDoc.RootElement;

                            string fName = vObj.GetProperty("files")[0].GetProperty("filename").GetString() ?? "";
                            string fUrl = vObj.GetProperty("files")[0].GetProperty("url").GetString() ?? "";
                            string projTitle = vObj.GetProperty("name").GetString() ?? "特殊前置";

                            string savePath = Path.Combine(modsDir, fName);

                            if (!File.Exists(savePath))
                            {
                                var bytes = await DownloadFileBytesWithRetryAsync(fUrl, fName);
                                await File.WriteAllBytesAsync(savePath, bytes);
                                report.AppendLine($"✅ {projTitle} (通过 VersionId 下载)");
                            }
                            else
                            {
                                report.AppendLine($"⏩ {projTitle} (已存在)");
                            }
                        }
                        catch (Exception ex)
                        {
                            report.AppendLine($"⚠️ 解析 VersionId 前置失败: {ex.Message}");
                        }
                    }
                }
            }
        }

        private string? FindLocalModVersion(string modsDir, string modrinthProjectId, string actualSlug, string projectTitle)
        {
            foreach (var jar in Directory.GetFiles(modsDir, "*.jar"))
            {
                string fileName = Path.GetFileName(jar).ToLower();
                bool nameMatch = (!string.IsNullOrEmpty(actualSlug) && fileName.Contains(actualSlug.ToLower()))
                              || fileName.Contains(projectTitle.ToLower().Replace(" ", ""));
                try
                {
                    // 🚨 使用 FileShare.ReadWrite 防止文件被占用时读取失败
                    using var fs = new FileStream(jar, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

                    var fabricJson = archive.GetEntry("fabric.mod.json");
                    if (fabricJson != null)
                    {
                        using var stream = fabricJson.Open();
                        using var reader = new StreamReader(stream);
                        using var doc = JsonDocument.Parse(reader.ReadToEnd());
                        var root = doc.RootElement;

                        string id = root.TryGetProperty("id", out var idP) ? (idP.GetString() ?? "").ToLower() : "";
                        bool idMatch = (!string.IsNullOrEmpty(actualSlug) && id.Contains(actualSlug.ToLower()))
                                    || (!string.IsNullOrEmpty(projectTitle) && id.Contains(projectTitle.ToLower().Replace(" ", "")))
                                    || id == "sodium" && modrinthProjectId == "AANobbMI";

                        if (nameMatch || idMatch)
                        {
                            return root.TryGetProperty("version", out var vP) ? vP.GetString() ?? "" : null;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private async Task DownloadSimpleResourceAsync(string projectId, string versionPath)
        {
            string path = $"/v2/project/{projectId}/version";
            var verRes = await ExecuteWithRetryAsync((useMirror) => Http.GetStringAsync(GetModrinthApiUrl(path, useMirror)), "获取资源版本");
            using var doc = JsonDocument.Parse(verRes);

            JsonElement? matchedVer = null;
            foreach (var ver in doc.RootElement.EnumerateArray())
            {
                var gameVers = ver.GetProperty("game_versions").EnumerateArray().Select(x => x.GetString()).ToList();
                if (gameVers.Contains(_targetInstance!.McVersion)) { matchedVer = ver; break; }
            }

            if (matchedVer == null) throw new Exception($"未找到兼容 {_targetInstance.McVersion} 的文件版本。");

            var files = matchedVer.Value.GetProperty("files");
            if (files.GetArrayLength() == 0) throw new Exception("版本中无有效文件。");

            var mainFile = files[0];
            string downloadUrl = mainFile.GetProperty("url").GetString() ?? "";
            string fileName = mainFile.GetProperty("filename").GetString() ?? "";

            string subFolder = _currentProjectType == "shader" ? "shaderpacks" : "resourcepacks";
            string baseDir = Path.Combine(versionPath, subFolder);
            Directory.CreateDirectory(baseDir);

            string savePath = Path.Combine(baseDir, fileName);
            var fileBytes = await DownloadFileBytesWithRetryAsync(downloadUrl, fileName);
            await File.WriteAllBytesAsync(savePath, fileBytes);

            await ShowDialogAsync("下载成功", $"已保存至：\n{savePath}", true);
        }

        // ... 原版游戏下载逻辑保持不变 ...
        private void VersionType_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isDataLoaded || VersionListView == null) return;
            FilterAndDisplay();
        }

        private void FilterAndDisplay()
        {
            if (!_isDataLoaded || VersionListView == null) return;
            try
            {
                IEnumerable<RemoteVersion> filtered;
                bool isAprilFoolsMode = false;
                if (ReleaseRadio.IsChecked == true) filtered = _allVersions.Where(v => v.Type == "release");
                else if (SnapshotRadio.IsChecked == true) filtered = _allVersions.Where(v => v.Type == "snapshot" && !IsAprilFools(v));
                else { filtered = _allVersions.Where(v => IsAprilFools(v)); isAprilFoolsMode = true; }

                var list = filtered.OrderByDescending(v => v.ReleaseTime).ToList();
                foreach (var item in list)
                    item.DotColor = isAprilFoolsMode ? new SolidColorBrush(Colors.Purple) : (item.Type == "release" ? new SolidColorBrush(Colors.DodgerBlue) : new SolidColorBrush(Colors.OrangeRed));
                VersionListView.ItemsSource = list;
            }
            catch { }
        }

        private bool IsAprilFools(RemoteVersion version)
        {
            if (string.IsNullOrEmpty(version.Id) || string.IsNullOrEmpty(version.ReleaseTime)) return false;
            if (version.Type == "release") return false;
            if (version.ReleaseTime.Contains("-04-01T")) return true;
            var keywords = new[] { "3D Shareware", "_a_or_b", "potato", "oneblockatatime", "infinite", "1.RV", "love-and-hugs" };
            return keywords.Any(k => version.Id.Contains(k, StringComparison.OrdinalIgnoreCase));
        }

        private async void SourceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SourceComboBox.SelectedIndex < 0) return;
            LauncherState.CurrentSource = (DownloadSource)SourceComboBox.SelectedIndex;
            if (_isDataLoaded) await LoadVersionsAsync();
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
                if (ReleaseRadio.IsChecked != true) ReleaseRadio.IsChecked = true;
                else FilterAndDisplay();
            }
            catch (Exception ex)
            {
                LoadingPanel.Visibility = Visibility.Collapsed;
                await ShowDialogAsync("网络错误", $"获取版本列表失败：\n{ex.Message}");
            }
        }

        private void VersionListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is RemoteVersion selectedVersion)
                this.Frame.Navigate(typeof(Page5), selectedVersion.Id);
        }

        private async Task<ContentDialogResult> ShowDialogAsync(string title, string message, bool askToOpen = false)
        {
            if (this.XamlRoot == null) return ContentDialogResult.None;
            var dialog = new ContentDialog
            {
                Title = title,
                Content = new ScrollViewer { Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, MaxHeight = 400 },
                XamlRoot = this.XamlRoot
            };
            if (askToOpen) { dialog.PrimaryButtonText = "打开文件夹"; dialog.CloseButtonText = "关闭"; }
            else dialog.CloseButtonText = "确定";
            try { return await dialog.ShowAsync(); } catch { return ContentDialogResult.None; }
        }
    }

    public static class VersionComparer
    {
        public static int Compare(string v1, string v2)
        {
            if (v1 == v2) return 0;
            try
            {
                var cleanV1 = Regex.Match(v1, @"^\d+(\.\d+)*").Value;
                var cleanV2 = Regex.Match(v2, @"^\d+(\.\d+)*").Value;
                if (Version.TryParse(cleanV1, out var ver1) && Version.TryParse(cleanV2, out var ver2))
                    return ver1.CompareTo(ver2);
            }
            catch { }
            return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
        }
    }
}