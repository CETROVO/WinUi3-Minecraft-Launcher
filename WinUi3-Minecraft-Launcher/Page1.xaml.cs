using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using CmlLib.Core;
using Windows.Storage.Pickers;

namespace WinUi3_Minecraft_Launcher
{
    public sealed partial class Page1 : Page
    {
        public ObservableCollection<GameVersion> Versions { get; set; } = new();
        public ObservableCollection<MinecraftFolder> Folders { get; set; } = new();

        private string? _currentMinecraftPath;
        private bool _isLoaded = false;

        public Page1()
        {
            this.InitializeComponent();
            VersionListView.ItemsSource = Versions;
            FolderListView.ItemsSource = Folders;

            this.Loaded += Page1_Loaded;
        }

        private void ShowVersionEmptyHint(string text)
        {
            Versions.Clear();
            VersionListView.Visibility = Visibility.Collapsed;
            VersionEmptyHint.Text = text;
            VersionEmptyHint.Visibility = Visibility.Visible;
        }

        private void HideVersionEmptyHint()
        {
            VersionListView.Visibility = Visibility.Visible;
            VersionEmptyHint.Visibility = Visibility.Collapsed;
        }

        private void ShowFolderEmptyHint(string text)
        {
            Folders.Clear();
            FolderListView.Visibility = Visibility.Collapsed;
            FolderEmptyHint.Text = text;
            FolderEmptyHint.Visibility = Visibility.Visible;
        }

        private void HideFolderEmptyHint()
        {
            FolderListView.Visibility = Visibility.Visible;
            FolderEmptyHint.Visibility = Visibility.Collapsed;
        }

        private async void Page1_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isLoaded) return;
            _isLoaded = true;

            var cfg = ConfigService.Load();

            foreach (var entry in cfg.Folders)
            {
                if (!Directory.Exists(entry.Path)) continue;
                Folders.Add(new MinecraftFolder
                {
                    Path = entry.Path,
                    DisplayName = string.IsNullOrEmpty(entry.DisplayName) ? Path.GetFileName(entry.Path) : entry.DisplayName,
                    IsActive = false
                });
            }

            if (Folders.Any())
            {
                HideFolderEmptyHint();

                var target = Folders.FirstOrDefault(f =>
                    string.Equals(f.Path, cfg.ActiveFolderPath, StringComparison.OrdinalIgnoreCase))
                    ?? Folders.First();

                foreach (var f in Folders) f.IsActive = false;
                target.IsActive = true;
                _currentMinecraftPath = target.Path;

                await LoadRealVersionsAsync(cfg.ActiveVersionName);
            }
            else
            {
                ShowFolderEmptyHint("📂 还没有添加任何 .minecraft 文件夹，点击上方「添加文件夹」开始");
                ShowVersionEmptyHint("👋 添加文件夹后，这里会显示已安装的游戏版本");
            }
        }

        private void SaveConfig()
        {
            var cfg = ConfigService.Load();
            cfg.Folders = Folders.Select(f => new FolderEntry
            {
                Path = f.Path,
                DisplayName = f.DisplayName
            }).ToList();

            cfg.ActiveFolderPath = Folders.FirstOrDefault(f => f.IsActive)?.Path;
            cfg.ActiveVersionName = LauncherState.CurrentVersionName;

            cfg.JavaPath = LauncherState.JavaPath;
            cfg.MaxRamMb = LauncherState.MaxRamMb;
            cfg.AutoMemory = LauncherState.AutoMemory;
            cfg.VersionIsolation = LauncherState.VersionIsolation;
            cfg.AutoIsolation = LauncherState.AutoIsolation;

            ConfigService.Save(cfg);
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FolderPicker();
                picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                picker.FileTypeFilter.Add("*");
                picker.CommitButtonText = "选择此文件夹";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var folder = await picker.PickSingleFolderAsync();
                if (folder == null) return;

                if (Folders.Any(f => string.Equals(f.Path, folder.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    await ShowDialogAsync("提示", "这个文件夹已经在列表里了。");
                    return;
                }

                var newFolder = new MinecraftFolder
                {
                    DisplayName = folder.Name,
                    Path = folder.Path,
                    IsActive = false
                };
                Folders.Add(newFolder);
                HideFolderEmptyHint();

                foreach (var f in Folders) f.IsActive = false;
                newFolder.IsActive = true;
                _currentMinecraftPath = folder.Path;

                await LoadRealVersionsAsync();
                SaveConfig();
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("错误", ex.Message);
            }
        }

        private async Task LoadRealVersionsAsync(string? restoreActiveVersion = null)
        {
            Versions.Clear();
            LauncherState.CurrentMinecraftPath = _currentMinecraftPath;
            LauncherState.CurrentVersionName = null;

            if (string.IsNullOrEmpty(_currentMinecraftPath))
            {
                ShowVersionEmptyHint("请先选择一个 .minecraft 文件夹");
                return;
            }

            try
            {
                if (!Directory.Exists(_currentMinecraftPath))
                {
                    ShowVersionEmptyHint($"⚠️ 目录不存在：{_currentMinecraftPath}");
                    return;
                }

                var versionsDir = Path.Combine(_currentMinecraftPath, "versions");
                if (!Directory.Exists(versionsDir))
                {
                    ShowVersionEmptyHint("📭 此目录下没有 versions 文件夹");
                    return;
                }

                var minecraftPath = new MinecraftPath(_currentMinecraftPath);
                var launcher = new MinecraftLauncher(minecraftPath);
                var realVersions = await launcher.GetAllVersionsAsync();

                var validVersions = realVersions.Where(v =>
                {
                    var vDir = Path.Combine(versionsDir, v.Name);
                    var jsonFile = Path.Combine(vDir, v.Name + ".json");
                    var jarFile = Path.Combine(vDir, v.Name + ".jar");
                    return File.Exists(jsonFile) && File.Exists(jarFile);
                }).ToList();

                if (!validVersions.Any())
                {
                    ShowVersionEmptyHint("📭 没有找到任何完整的已安装版本");
                    return;
                }

                string? activeName = restoreActiveVersion;
                if (string.IsNullOrEmpty(activeName) || !validVersions.Any(v => v.Name == activeName))
                {
                    activeName = validVersions.First().Name;
                }

                HideVersionEmptyHint();
                foreach (var v in validVersions)
                {
                    bool active = (v.Name == activeName);
                    Versions.Add(new GameVersion
                    {
                        Id = v.Name,
                        DisplayName = v.Name,
                        Path = Path.Combine(versionsDir, v.Name),
                        IsActive = active
                    });

                    if (active)
                    {
                        LauncherState.CurrentVersionName = v.Name;
                        LauncherState.CurrentMinecraftPath = _currentMinecraftPath;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                ShowVersionEmptyHint("🚫 权限被拒绝，请尝试重新选择文件夹");
            }
            catch (Exception ex)
            {
                ShowVersionEmptyHint($"❌ 读取失败：{ex.Message}");
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

        private void ActivateVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GameVersion ver)
            {
                foreach (var v in Versions) v.IsActive = false;
                ver.IsActive = true;
                LauncherState.CurrentVersionName = ver.Id;
                LauncherState.CurrentMinecraftPath = _currentMinecraftPath;
                SaveConfig();
            }
        }

        private void VersionDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GameVersion ver)
            {
                this.Frame?.Navigate(typeof(Page4), ver.Id);
            }
        }

        private async void AddVersion_Click(object sender, RoutedEventArgs e)
        {
            await Task.CompletedTask;
        }

        private async void SwitchFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MinecraftFolder folder)
            {
                foreach (var f in Folders) f.IsActive = false;
                folder.IsActive = true;
                _currentMinecraftPath = folder.Path;
                await LoadRealVersionsAsync();
                SaveConfig();
            }
        }

        private async void RenameFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MinecraftFolder folder)
            {
                if (this.XamlRoot == null) return;
                var dialog = new ContentDialog
                {
                    Title = "重命名文件夹",
                    Content = new TextBox { Text = folder.DisplayName },
                    PrimaryButtonText = "确定",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };
                try
                {
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        folder.DisplayName = ((TextBox)dialog.Content).Text;
                        SaveConfig();
                    }
                }
                catch { }
            }
        }

        private async void DeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not MinecraftFolder folder) return;

            if (this.XamlRoot == null) return;
            var confirm = new ContentDialog
            {
                Title = "从列表中移除？",
                Content = new TextBlock
                {
                    Text = $"将把以下文件夹从启动器列表中移除：\n\n{folder.Path}\n\n⚠️ 不会删除你磁盘上的任何文件。",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "移除",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            ContentDialogResult result;
            try { result = await confirm.ShowAsync(); } catch { return; }
            if (result != ContentDialogResult.Primary) return;

            Folders.Remove(folder);
            if (!Folders.Any())
                ShowFolderEmptyHint("📂 还没有添加任何 .minecraft 文件夹，点击上方「添加文件夹」开始");

            if (folder.IsActive)
            {
                _currentMinecraftPath = null;
                LauncherState.CurrentMinecraftPath = null;
                LauncherState.CurrentVersionName = null;

                if (Folders.Any())
                {
                    var next = Folders.First();
                    next.IsActive = true;
                    _currentMinecraftPath = next.Path;
                    await LoadRealVersionsAsync();
                }
                else
                {
                    ShowVersionEmptyHint("👋 添加文件夹后，这里会显示已安装的游戏版本");
                }
            }
            SaveConfig();
        }
    }
}