using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
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
using System.Threading;
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
        private bool _isJavaListLoaded = false;
        private string _currentProjectType = "mod";

        // 🚨 核心锁区标记
        private bool _isRegionRestricted = false;

        private InstalledVersionItem? _targetInstance = null;
        private readonly HttpClient Http = new HttpClient();
        private CancellationTokenSource? _javaDownloadCts;

        public Page2()
        {
            this.InitializeComponent();
            Http.Timeout = TimeSpan.FromSeconds(60);
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            this.Loaded += Page2_Loaded;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is string param && param == "Java_Force")
            {
                if (!_isDataLoaded)
                {
                    DownloadCategoryList.ItemsSource = new List<string> { "游戏", "Java", "模组", "资源包", "整合包", "光影包" };
                    await CheckLegalRestrictionsAsync();
                    await LoadVersionsAsync();
                }

                DownloadCategoryList.SelectedIndex = 1;
                await ShowDialogAsync("缺少 Java 运行环境", "检测到当前版本所需的 Java 缺失或版本不匹配。\n\n这意味着您将无法正常游玩 Minecraft Java Edition。\n\n请在此页面下载并安装。");
            }
        }

        private async void Page2_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isDataLoaded && (DownloadCategoryList.ItemsSource == null))
            {
                DownloadCategoryList.ItemsSource = new List<string> { "游戏", "Java", "模组", "资源包", "整合包", "光影包" };
                DownloadCategoryList.SelectedIndex = 0;

                // 🚨 加载时校验，命中则直接全灰
                await CheckLegalRestrictionsAsync();

                // 正常拉取版本列表（数据依然加载，但 UI 会变灰）
                await LoadVersionsAsync();
            }
        }

        // 🚨 核心校验逻辑：命中直接全灰封死
        private async Task CheckLegalRestrictionsAsync()
        {
            bool isRestricted = false;

            // 1. 检测系统区域
            try
            {
                var region = Windows.System.UserProfile.GlobalizationPreferences.HomeGeographicRegion;
                if (region == "CN" || System.Globalization.CultureInfo.CurrentCulture.Name == "zh-CN")
                {
                    isRestricted = true;
                }
            }
            catch { }

            // 2. 检测出口 IP
            if (!isRestricted)
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                    var json = await client.GetStringAsync("http://ip-api.com/json/?fields=countryCode");
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("countryCode", out var cc) && cc.GetString() == "CN")
                    {
                        isRestricted = true;
                    }
                }
                catch { }
            }

            // 3. 如果命中，直接封死并全灰
            if (isRestricted)
            {
                _isRegionRestricted = true;
                DispatcherQueue.TryEnqueue(() =>
                {
                    // 显示顶部的红字警告
                    GameBanWarning.Visibility = Visibility.Visible;

                    // 🚨 核心：让整个内容区变灰，并且禁用一切点击（单选、刷新、列表统统点不了）
                    GameDownloadContent.IsEnabled = false;
                    GameDownloadContent.Opacity = 0.5;
                });
            }
        }

        private async void DownloadCategoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DownloadCategoryList.SelectedItem is not string category) return;

            GamePanel.Visibility = Visibility.Collapsed;
            SearchPanel.Visibility = Visibility.Collapsed;
            JavaPanel.Visibility = Visibility.Collapsed;

            if (category == "游戏")
            {
                GamePanel.Visibility = Visibility.Visible;
            }
            else if (category == "Java")
            {
                JavaPanel.Visibility = Visibility.Visible;
                if (!_isJavaListLoaded)
                {
                    await LoadJavaVersionsAsync();
                }
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

                if (_currentProjectType == "modpack")
                {
                    InstanceSelectionPanel.Visibility = Visibility.Collapsed;
                    _targetInstance = null;
                }
                else
                {
                    InstanceSelectionPanel.Visibility = Visibility.Visible;
                    LoadTargetInstances();
                }

                _ = LoadRecommendedModsAsync();
            }
        }

        // ================= Java 下载逻辑（不受影响） =================
        private async Task LoadJavaVersionsAsync()
        {
            try
            {
                JavaVersionCombo.IsEnabled = false;
                var response = await Http.GetStringAsync("https://api.adoptium.net/v3/info/available_releases");
                using var doc = JsonDocument.Parse(response);

                var availableReleases = doc.RootElement.GetProperty("available_releases").EnumerateArray().Select(x => x.GetInt32()).ToList();
                var ltsReleases = doc.RootElement.GetProperty("available_lts_releases").EnumerateArray().Select(x => x.GetInt32()).ToList();

                availableReleases.Sort((a, b) => b.CompareTo(a));

                JavaVersionCombo.Items.Clear();
                foreach (var ver in availableReleases)
                {
                    string display = $"Java {ver}";
                    if (ltsReleases.Contains(ver)) display += " (LTS 长期支持版)";
                    JavaVersionCombo.Items.Add(new ComboBoxItem { Content = display, Tag = ver.ToString() });
                }

                var defaultItem = JavaVersionCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(x => (string)x.Tag == "21");
                JavaVersionCombo.SelectedItem = defaultItem ?? JavaVersionCombo.Items[0];

                _isJavaListLoaded = true;
            }
            catch (Exception)
            {
                JavaVersionCombo.Items.Clear();
                JavaVersionCombo.Items.Add(new ComboBoxItem { Content = "Java 21 (LTS 长期支持版)", Tag = "21" });
                JavaVersionCombo.Items.Add(new ComboBoxItem { Content = "Java 17 (LTS 长期支持版)", Tag = "17" });
                JavaVersionCombo.Items.Add(new ComboBoxItem { Content = "Java 8 (LTS 长期支持版)", Tag = "8" });
                JavaVersionCombo.SelectedIndex = 0;
            }
            finally
            {
                JavaVersionCombo.IsEnabled = true;
            }
        }

        private async Task<(string url, string fileName)> GetDownloadUrlAsync(string javaVer, string imageType)
        {
            try
            {
                string oracleUrl = $"https://download.oracle.com/java/{javaVer}/latest/jdk-{javaVer}_windows-x64_bin.zip";
                string oracleFileName = $"jdk-{javaVer}_windows-x64_bin.zip";

                using var request = new HttpRequestMessage(HttpMethod.Head, oracleUrl);
                using var response = await Http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return (oracleUrl, oracleFileName);
                }
            }
            catch { }

            string os = "windows";
            string arch = "x64";
            string adoptiumUrl = $"https://api.adoptium.net/v3/binary/latest/{javaVer}/ga/{os}/{arch}/{imageType}/hotspot/normal/eclipse";
            string adoptiumFileName = $"OpenJDK{javaVer}U-{imageType}_x64_{os}.zip";

            return (adoptiumUrl, adoptiumFileName);
        }

        private void CancelJavaButton_Click(object sender, RoutedEventArgs e)
        {
            _javaDownloadCts?.Cancel();
        }

        private async void DownloadJavaButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;

            var selectedItem = JavaVersionCombo.SelectedItem as ComboBoxItem;
            if (selectedItem == null || selectedItem.Tag == null) return;
            string javaVer = selectedItem.Tag.ToString()!;

            string imageType = JreRadio.IsChecked == true ? "jre" : "jdk";

            btn.IsEnabled = false;
            DownloadJavaButton.Visibility = Visibility.Collapsed;
            CancelJavaButton.Visibility = Visibility.Visible;
            JavaDownloadProgress.Visibility = Visibility.Visible;
            JavaDownloadSpeedText.Visibility = Visibility.Visible;
            JavaStatusText.Visibility = Visibility.Visible;
            JavaDownloadProgress.Value = 0;
            JavaDownloadSpeedText.Text = "0 KB/s";

            _javaDownloadCts = new CancellationTokenSource();
            var token = _javaDownloadCts.Token;

            try
            {
                string extractDir = $@"C:\WML\Java\{javaVer}-{imageType}";
                Directory.CreateDirectory(Path.GetDirectoryName(extractDir)!);
                Directory.CreateDirectory(extractDir);

                JavaStatusText.Text = $"正在获取 Java {javaVer} 下载链接...\n路径将保存在: {extractDir}";

                var (downloadUrl, fileName) = await GetDownloadUrlAsync(javaVer, imageType);

                if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(fileName))
                    throw new Exception("未能获取到有效的下载链接。");

                JavaStatusText.Text = $"正在下载 Java {javaVer} ({imageType.ToUpper()})...";

                var response = await Http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync(token);

                string tempDir = Path.Combine(Path.GetTempPath(), "WML_Java_Download");
                Directory.CreateDirectory(tempDir);
                string zipPath = Path.Combine(tempDir, fileName);

                using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;

                    long lastRead = 0;
                    var stopwatch = Stopwatch.StartNew();
                    long lastUpdateTime = 0;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                        totalRead += bytesRead;

                        if (stopwatch.ElapsedMilliseconds - lastUpdateTime > 500)
                        {
                            long bytesSinceLastUpdate = totalRead - lastRead;
                            double secondsSinceLastUpdate = (stopwatch.ElapsedMilliseconds - lastUpdateTime) / 1000.0;
                            double speedBps = bytesSinceLastUpdate / secondsSinceLastUpdate;

                            string speedStr = speedBps > 1024 * 1024
                                ? $"{speedBps / 1024 / 1024:F2} MB/s"
                                : $"{speedBps / 1024:F1} KB/s";

                            double percentage = totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 0;

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                if (totalBytes > 0) JavaDownloadProgress.Value = percentage;
                                JavaStatusText.Text = $"正在下载... {percentage:F1}%";
                                JavaDownloadSpeedText.Text = speedStr;
                            });

                            lastRead = totalRead;
                            lastUpdateTime = stopwatch.ElapsedMilliseconds;
                        }
                    }
                }

                JavaStatusText.Text = "正在解压 Java 运行环境...";
                JavaDownloadSpeedText.Visibility = Visibility.Collapsed;
                JavaDownloadProgress.IsIndeterminate = true;

                if (Directory.Exists(extractDir))
                {
                    try { Directory.Delete(extractDir, true); } catch { }
                }
                Directory.CreateDirectory(extractDir);

                ZipFile.ExtractToDirectory(zipPath, extractDir, true);
                try { File.Delete(zipPath); } catch { }

                JavaStatusText.Text = "✅ Java 安装完成！";
                JavaDownloadProgress.Value = 100;
                JavaDownloadProgress.IsIndeterminate = false;

                var result = await ShowDialogAsync("安装成功", $"Java {javaVer} 已经成功安装！\n\n完整路径: {extractDir}\n\n是否打开该文件夹查看？", true);
                if (result == ContentDialogResult.Primary)
                {
                    Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"\"{extractDir}\"", UseShellExecute = true });
                }
            }
            catch (OperationCanceledException)
            {
                JavaStatusText.Text = "❌ 下载已取消。";
                JavaDownloadProgress.Visibility = Visibility.Collapsed;
                JavaDownloadSpeedText.Visibility = Visibility.Collapsed;

                try
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "WML_Java_Download");
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                }
                catch { }
            }
            catch (Exception ex)
            {
                JavaStatusText.Text = $"❌ 下载或解压失败：{ex.Message}";
                JavaDownloadProgress.Visibility = Visibility.Collapsed;
                JavaDownloadSpeedText.Visibility = Visibility.Collapsed;
                await ShowDialogAsync("下载失败", ex.Message);
            }
            finally
            {
                _javaDownloadCts?.Dispose();
                _javaDownloadCts = null;

                btn.IsEnabled = true;
                CancelJavaButton.Visibility = Visibility.Collapsed;
                DownloadJavaButton.Visibility = Visibility.Visible;
            }
        }

        // ================= 社区资源搜索与下载 =================
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
            if (_targetInstance != null && DownloadCategoryList.SelectedIndex > 0 && string.IsNullOrEmpty(SearchBox.Text))
            {
                _ = LoadRecommendedModsAsync();
            }
        }

        private string GetModrinthApiUrl(string path, bool useMirror)
        {
            string baseUrl = useMirror ? "https://mod.mcimirror.top/modrinth" : "https://api.modrinth.com";
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
                var cfg = ConfigService.Load();
                int commSrc = cfg.CommunitySource;
                int fileSrc = cfg.FileDownloadSource;

                if (fileSrc == 3 || commSrc == 1) useMirror = true;
                else if (fileSrc == 2 || commSrc == 0) useMirror = false;
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

        private async Task<byte[]> DownloadFileBytesWithRetryAsync(string url, string fileName)
        {
            return await ExecuteWithRetryAsync(async (useMirror) =>
            {
                var realUrl = GetModrinthCdnUrl(url, useMirror);
                using var response = await Http.GetAsync(realUrl, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length < 500 && fileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
                    throw new Exception("下载的文件过小，可能已损坏或未同步至镜像。");
                return bytes;
            }, $"下载 {fileName}");
        }

        private async Task LoadRecommendedModsAsync()
        {
            if (_currentProjectType == "modpack" && _targetInstance == null) { }
            else if (_currentProjectType != "modpack" && _targetInstance == null) return;

            SearchLoadingPanel.Visibility = Visibility.Visible;
            SearchResultList.Visibility = Visibility.Collapsed;
            SearchResultList.ItemsSource = null;

            try
            {
                var facetList = new List<string> { $"[\"project_type:{_currentProjectType}\"]" };
                if (_currentProjectType != "modpack" && _targetInstance != null)
                {
                    if (_currentProjectType == "mod" && _targetInstance.Loader != "vanilla") facetList.Add($"[\"categories:{_targetInstance.Loader}\"]");
                    if (!string.IsNullOrEmpty(_targetInstance.McVersion)) facetList.Add($"[\"versions:{_targetInstance.McVersion}\"]");
                }

                string facets = $"[{string.Join(",", facetList)}]";
                string path = $"/v2/search?query=&facets={Uri.EscapeDataString(facets)}&index=downloads&limit=20";

                var response = await ExecuteWithRetryAsync((useMirror) => Http.GetStringAsync(GetModrinthApiUrl(path, useMirror)), "获取推荐", true);

                using var doc = JsonDocument.Parse(response);
                var hits = doc.RootElement.GetProperty("hits");
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
            catch { }
            finally
            {
                SearchLoadingPanel.Visibility = Visibility.Collapsed;
                SearchResultList.Visibility = Visibility.Visible;
            }
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e) => await PerformSearchAsync();

        private async void SearchBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter) await PerformSearchAsync();
        }

        private async Task PerformSearchAsync()
        {
            string originalQuery = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(originalQuery)) { await LoadRecommendedModsAsync(); return; }
            if (_currentProjectType != "modpack" && _targetInstance == null) { await ShowDialogAsync("提示", "请先在上方选择要安装到的版本实例！"); return; }

            SearchLoadingPanel.Visibility = Visibility.Visible;
            SearchResultList.Visibility = Visibility.Collapsed;
            SearchResultList.ItemsSource = null;
            DownloadStatusBorder.Visibility = Visibility.Collapsed;

            try
            {
                string searchQuery = originalQuery;
                var cfg = ConfigService.Load();
                bool forceStartMirror = (cfg.FileDownloadSource == 1 || cfg.FileDownloadSource == 3 || cfg.CommunitySource == 1);

                var facetList = new List<string> { $"[\"project_type:{_currentProjectType}\"]" };
                if (_currentProjectType != "modpack" && _targetInstance != null)
                {
                    if (_currentProjectType == "mod" && _targetInstance.Loader != "vanilla") facetList.Add($"[\"categories:{_targetInstance.Loader}\"]");
                    if (!string.IsNullOrEmpty(_targetInstance.McVersion)) facetList.Add($"[\"versions:{_targetInstance.McVersion}\"]");
                }

                string facets = $"[{string.Join(",", facetList)}]";
                string path = $"/v2/search?query={Uri.EscapeDataString(searchQuery)}&facets={Uri.EscapeDataString(facets)}&limit=20";

                var response = await ExecuteWithRetryAsync((useMirror) => Http.GetStringAsync(GetModrinthApiUrl(path, useMirror)), "搜索列表", forceStartMirror);

                using var doc = JsonDocument.Parse(response);
                var hits = doc.RootElement.GetProperty("hits");
                if (hits.GetArrayLength() == 0) { await ShowDialogAsync("无结果", $"没有找到与“{originalQuery}”相关的资源。\n请尝试使用英文关键词搜索。"); return; }

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
            catch (Exception ex) { await ShowDialogAsync("搜索失败", $"请求 Modrinth 失败。\n{ex.Message}"); }
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
                    var cfg = ConfigService.Load();
                    bool useMirror = (cfg.FileDownloadSource == 1 || cfg.FileDownloadSource == 3 || cfg.CommunitySource == 1);
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
            if (_currentProjectType != "modpack" && _targetInstance == null) { await ShowDialogAsync("提示", "请先选择目标实例！"); return; }

            btn.IsEnabled = false;
            btn.Content = "解析中...";
            DownloadStatusBorder.Visibility = Visibility.Collapsed;

            try
            {
                string versionPath = _currentProjectType == "modpack"
                    ? Path.Combine(LauncherState.CurrentMinecraftPath!, "modpacks")
                    : Path.Combine(LauncherState.CurrentMinecraftPath!, "versions", _targetInstance!.Name);

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
            catch (Exception ex) { await ShowDialogAsync("下载失败", $"发生错误：{ex.Message}"); }
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

            var compatibleVersions = new List<JsonElement>();
            foreach (var ver in doc.RootElement.EnumerateArray())
            {
                var gameVers = ver.GetProperty("game_versions").EnumerateArray().Select(x => x.GetString()).ToList();
                var loaders = ver.GetProperty("loaders").EnumerateArray().Select(x => x.GetString().ToLower()).ToList();
                bool loaderMatch = loaders.Contains(_targetInstance!.Loader) || (_targetInstance.Loader == "quilt" && loaders.Contains("fabric")) || loaders.Count == 0;
                bool versionMatch = gameVers.Contains(_targetInstance.McVersion) || gameVers.Any(v => v.StartsWith(_targetInstance.McVersion) || _targetInstance.McVersion.StartsWith(v));
                if (versionMatch && loaderMatch) compatibleVersions.Add(ver);
            }

            if (compatibleVersions.Count == 0)
            {
                if (depth == 0) throw new Exception($"未找到兼容 {_targetInstance.McVersion}-{_targetInstance.Loader} 的版本。");
                else report.AppendLine($"⚠️ 未找到前置 {projectTitle} 兼容当前实例的版本。");
                return;
            }

            var releaseVersion = compatibleVersions.FirstOrDefault(v => v.GetProperty("version_type").GetString() == "release");
            var targetVerObj = releaseVersion.ValueKind != JsonValueKind.Undefined ? releaseVersion : compatibleVersions[0];

            string targetVersionNumber = targetVerObj.GetProperty("version_number").GetString() ?? "";
            string versionType = targetVerObj.GetProperty("version_type").GetString() ?? "release";

            string modsDir = Path.Combine(versionPath, "mods");
            Directory.CreateDirectory(modsDir);

            string? localVersion = FindLocalModVersion(modsDir, projectId, actualSlug, projectTitle);
            bool shouldDownload = true;

            if (localVersion != null)
            {
                int cmp = VersionComparer.Compare(targetVersionNumber, localVersion);
                if (cmp <= 0) { shouldDownload = false; report.AppendLine($"⏩ {projectTitle} (本地已是最新: {localVersion})"); }
                else report.AppendLine($"🔄 {projectTitle} ({localVersion} ➔ {targetVersionNumber} {versionType})");
            }
            else report.AppendLine($"✅ {projectTitle} (新安装 {targetVersionNumber} {versionType})");

            if (shouldDownload)
            {
                var files = targetVerObj.GetProperty("files");
                if (files.GetArrayLength() > 0)
                {
                    var mainFile = files[0];
                    string downloadUrl = mainFile.GetProperty("url").GetString() ?? "";
                    string fileName = mainFile.GetProperty("filename").GetString() ?? "";

                    var cfg = ConfigService.Load();
                    string saveFileName = fileName;
                    if (cfg.FileNameFormat == 0 && projectTitle != actualSlug && !string.IsNullOrEmpty(projectTitle))
                    {
                        saveFileName = $"[{projectTitle}] {fileName}";
                    }
                    string savePath = Path.Combine(modsDir, saveFileName);

                    if (localVersion != null)
                    {
                        var oldFiles = Directory.GetFiles(modsDir, "*.jar").Where(f => f.Contains(actualSlug, StringComparison.OrdinalIgnoreCase) || f.Contains(projectTitle.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                        foreach (var old in oldFiles) { try { File.Delete(old); } catch { await Task.Delay(500); try { File.Delete(old); } catch { } } }
                    }

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
                        await DownloadModWithDependenciesAsync(depProjectId, "前置", versionPath, report, processed, depth + 1);
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
                        }
                        catch (Exception ex) { report.AppendLine($"⚠️ 解析 VersionId 前置失败: {ex.Message}"); }
                    }
                }
            }
        }

        private string? FindLocalModVersion(string modsDir, string modrinthProjectId, string actualSlug, string projectTitle)
        {
            foreach (var jar in Directory.GetFiles(modsDir, "*.jar"))
            {
                string fileName = Path.GetFileName(jar).ToLower();
                bool nameMatch = (!string.IsNullOrEmpty(actualSlug) && fileName.Contains(actualSlug.ToLower())) || fileName.Contains(projectTitle.ToLower().Replace(" ", ""));
                try
                {
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
                        bool idMatch = (!string.IsNullOrEmpty(actualSlug) && id.Contains(actualSlug.ToLower())) || (!string.IsNullOrEmpty(projectTitle) && id.Contains(projectTitle.ToLower().Replace(" ", ""))) || id == "sodium" && modrinthProjectId == "AANobbMI";
                        if (nameMatch || idMatch) return root.TryGetProperty("version", out var vP) ? vP.GetString() ?? "" : null;
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
                if (_currentProjectType == "modpack" || gameVers.Contains(_targetInstance!.McVersion)) { matchedVer = ver; break; }
            }

            if (matchedVer == null) throw new Exception($"未找到兼容 {_targetInstance?.McVersion} 的文件版本。");

            var files = matchedVer.Value.GetProperty("files");
            if (files.GetArrayLength() == 0) throw new Exception("版本中无有效文件。");
            var mainFile = files[0];
            string downloadUrl = mainFile.GetProperty("url").GetString() ?? "";
            string fileName = mainFile.GetProperty("filename").GetString() ?? "";

            string subFolder = _currentProjectType switch { "shader" => "shaderpacks", "resourcepack" => "resourcepacks", "modpack" => "", _ => "mods" };
            string baseDir = string.IsNullOrEmpty(subFolder) ? versionPath : Path.Combine(versionPath, subFolder);
            Directory.CreateDirectory(baseDir);

            string savePath = Path.Combine(baseDir, fileName);
            var fileBytes = await DownloadFileBytesWithRetryAsync(downloadUrl, fileName);
            await File.WriteAllBytesAsync(savePath, fileBytes);
            await ShowDialogAsync("下载成功", $"文件已保存至：\n{savePath}", true);
        }

        // ================= 原版版本列表过滤 =================
        private void VersionType_Checked(object sender, RoutedEventArgs e) { if (!_isDataLoaded || VersionListView == null) return; FilterAndDisplay(); }

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
                foreach (var item in list) item.DotColor = isAprilFoolsMode ? new SolidColorBrush(Colors.Purple) : (item.Type == "release" ? new SolidColorBrush(Colors.DodgerBlue) : new SolidColorBrush(Colors.OrangeRed));
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

        private async void RefreshButton_Click(object sender, RoutedEventArgs e) { _isDataLoaded = false; await LoadVersionsAsync(); }

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
            catch (Exception ex) { LoadingPanel.Visibility = Visibility.Collapsed; await ShowDialogAsync("网络错误", $"获取版本列表失败：\n{ex.Message}"); }
        }

        private async void VersionListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            // 🚨 物理锁区：如果被限制，绝对不响应点击
            if (_isRegionRestricted) return;

            if (e.ClickedItem is RemoteVersion selectedVersion)
                this.Frame.Navigate(typeof(Page5), selectedVersion.Id);
        }

        private async Task<ContentDialogResult> ShowDialogAsync(string title, string message, bool askToOpen = false)
        {
            if (this.XamlRoot == null) return ContentDialogResult.None;
            var dialog = new ContentDialog { Title = title, Content = new ScrollViewer { Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, MaxHeight = 400 }, XamlRoot = this.XamlRoot };
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
                if (Version.TryParse(cleanV1, out var ver1) && Version.TryParse(cleanV2, out var ver2)) return ver1.CompareTo(ver2);
            }
            catch { }
            return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase);
        }
    }
}