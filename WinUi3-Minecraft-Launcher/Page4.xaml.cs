using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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

        public Page4()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _versionName = e.Parameter as string ?? LauncherState.CurrentVersionName ?? "";
            _minecraftPath = LauncherState.CurrentMinecraftPath ?? "";
            _versionDir = Path.Combine(_minecraftPath, "versions", _versionName);

            LoadVersionInfo();
        }

        private void LoadVersionInfo()
        {
            VersionTitleText.Text = _versionName;
            InfoVersionIdText.Text = _versionName;
            InfoPathText.Text = _versionDir;

            try
            {
                var jsonPath = Path.Combine(_versionDir, $"{_versionName}.json");
                if (!File.Exists(jsonPath))
                {
                    VersionSubtitleText.Text = "⚠️ 版本 json 不存在";
                    return;
                }

                var json = JsonDocument.Parse(File.ReadAllText(jsonPath));
                var root = json.RootElement;

                string gameVersion = _versionName;
                string loaderInfo = "原版 (Vanilla)";

                if (root.TryGetProperty("id", out var idProp))
                    gameVersion = idProp.GetString() ?? _versionName;

                if (root.TryGetProperty("libraries", out var libs))
                {
                    foreach (var lib in libs.EnumerateArray())
                    {
                        if (lib.TryGetProperty("name", out var nameProp))
                        {
                            var name = nameProp.GetString() ?? "";
                            if (name.StartsWith("net.fabricmc:fabric-loader", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = name.Split(':');
                                loaderInfo = parts.Length >= 3 ? $"Fabric {parts[2]}" : "Fabric";
                                break;
                            }
                            else if (name.StartsWith("net.minecraftforge:forge", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = name.Split(':');
                                loaderInfo = parts.Length >= 3 ? $"Forge {parts[2]}" : "Forge";
                                break;
                            }
                            else if (name.StartsWith("net.neoforged:neoforge", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = name.Split(':');
                                loaderInfo = parts.Length >= 3 ? $"NeoForge {parts[2]}" : "NeoForge";
                                break;
                            }
                        }
                    }
                }

                InfoGameVersionText.Text = gameVersion;
                InfoLoaderText.Text = loaderInfo;
                VersionSubtitleText.Text = $"{gameVersion} · {loaderInfo}";

                if (root.TryGetProperty("javaVersion", out var jv) &&
                    jv.TryGetProperty("majorVersion", out var mv))
                {
                    InfoJavaText.Text = $"Java {mv.GetInt32()}";
                }
                else
                {
                    InfoJavaText.Text = "未指定";
                }
            }
            catch (Exception ex)
            {
                VersionSubtitleText.Text = $"解析失败: {ex.Message}";
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack)
                this.Frame.GoBack();
            else
                this.Frame.Navigate(typeof(Page1));
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            LauncherState.CurrentVersionName = _versionName;
            LauncherState.CurrentMinecraftPath = _minecraftPath;

            if (App.MainWindow is MainWindow mw)
            {
                mw.TriggerLaunch();
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not string tag) return;

            string path;
            if (tag == "versions")
                path = _versionDir;
            else
                path = Path.Combine(_versionDir, tag);

            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                _ = ShowDialogAsync("打开失败", ex.Message);
            }
        }

        private async void RenameVersion_Click(object sender, RoutedEventArgs e)
        {
            if (this.XamlRoot == null) return;

            var input = new TextBox { Text = _versionName };
            var dialog = new ContentDialog
            {
                Title = "重命名版本（仅显示名）",
                Content = input,
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
            };

            try
            {
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    var newName = input.Text?.Trim();
                    if (string.IsNullOrEmpty(newName)) return;

                    VersionTitleText.Text = newName;
                }
            }
            catch { }
        }

        private async void DeleteVersion_Click(object sender, RoutedEventArgs e)
        {
            if (this.XamlRoot == null) return;

            var confirm = new ContentDialog
            {
                Title = "确认删除版本？",
                Content = new TextBlock
                {
                    Text = $"将永久删除以下文件夹：\n\n{_versionDir}\n\n此操作不可恢复！",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "永久删除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            try
            {
                if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                {
                    try
                    {
                        if (Directory.Exists(_versionDir))
                            Directory.Delete(_versionDir, true);

                        if (LauncherState.CurrentVersionName == _versionName)
                        {
                            LauncherState.CurrentVersionName = null;
                        }

                        if (this.Frame.CanGoBack)
                            this.Frame.GoBack();
                        else
                            this.Frame.Navigate(typeof(Page1));
                    }
                    catch (Exception ex)
                    {
                        await ShowDialogAsync("删除失败", ex.Message);
                    }
                }
            }
            catch { }
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