using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        public Page1()
        {
            this.InitializeComponent();
            VersionListView.ItemsSource = Versions;
            FolderListView.ItemsSource = Folders;

            this.Loaded += Page1_Loaded;
        }

        private void Page1_Loaded(object sender, RoutedEventArgs e)
        {
            if (Folders.Count == 0)
            {
                Versions.Clear();
                Versions.Add(new GameVersion
                {
                    Id = "hint",
                    DisplayName = "👋 请点击右侧的「添加文件夹」按钮，选择你的 .minecraft 目录",
                    Path = "",
                    IsActive = false
                });
            }
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

                foreach (var f in Folders) f.IsActive = false;
                newFolder.IsActive = true;
                _currentMinecraftPath = folder.Path;

                await LoadRealVersionsAsync();
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("错误", ex.Message);
            }
        }

        private async Task LoadRealVersionsAsync()
        {
            Versions.Clear();
            LauncherState.CurrentMinecraftPath = _currentMinecraftPath;
            LauncherState.CurrentVersionName = null;

            if (string.IsNullOrEmpty(_currentMinecraftPath))
            {
                Versions.Add(new GameVersion { Id = "none", DisplayName = "请先选择一个 .minecraft 文件夹", Path = "", IsActive = false });
                return;
            }

            try
            {
                if (!Directory.Exists(_currentMinecraftPath))
                {
                    Versions.Add(new GameVersion
                    {
                        Id = "error",
                        DisplayName = $"⚠️ 目录不存在: {_currentMinecraftPath}",
                        Path = _currentMinecraftPath,
                        IsActive = false
                    });
                    return;
                }

                var versionsDir = Path.Combine(_currentMinecraftPath, "versions");
                if (!Directory.Exists(versionsDir))
                {
                    Versions.Add(new GameVersion
                    {
                        Id = "empty",
                        DisplayName = "📭 此目录下没有 versions 文件夹",
                        Path = _currentMinecraftPath,
                        IsActive = false
                    });
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
                    Versions.Add(new GameVersion
                    {
                        Id = "empty",
                        DisplayName = "📭 没有找到任何完整的已安装版本（缺少 .json 或 .jar）",
                        Path = _currentMinecraftPath,
                        IsActive = false
                    });
                    return;
                }

                bool firstActive = true;
                foreach (var v in validVersions)
                {
                    Versions.Add(new GameVersion
                    {
                        Id = v.Name,
                        DisplayName = v.Name,
                        Path = Path.Combine(versionsDir, v.Name),
                        IsActive = firstActive
                    });

                    if (firstActive)
                    {
                        LauncherState.CurrentVersionName = v.Name;
                        LauncherState.CurrentMinecraftPath = _currentMinecraftPath;
                    }
                    firstActive = false;
                }
            }
            catch (UnauthorizedAccessException)
            {
                Versions.Add(new GameVersion
                {
                    Id = "denied",
                    DisplayName = "🚫 权限被拒绝，请尝试重新选择文件夹",
                    Path = _currentMinecraftPath,
                    IsActive = false
                });
            }
            catch (Exception ex)
            {
                Versions.Add(new GameVersion
                {
                    Id = "error",
                    DisplayName = $"❌ 读取失败: {ex.Message}",
                    Path = _currentMinecraftPath,
                    IsActive = false
                });
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

        // ================= 版本操作 =================
        private void ActivateVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GameVersion ver)
            {
                foreach (var v in Versions) v.IsActive = false;
                ver.IsActive = true;

                LauncherState.CurrentVersionName = ver.Id;
                LauncherState.CurrentMinecraftPath = _currentMinecraftPath;
            }
        }

        private async void RenameVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GameVersion ver)
            {
                if (this.XamlRoot == null) return;
                var dialog = new ContentDialog
                {
                    Title = "重命名版本（仅显示名）",
                    Content = new TextBox { Text = ver.DisplayName },
                    PrimaryButtonText = "确定",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.XamlRoot
                };
                try
                {
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                        ver.DisplayName = ((TextBox)dialog.Content).Text;
                }
                catch { }
            }
        }

        private void DeleteVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GameVersion ver)
            {
                Versions.Remove(ver);
                if (LauncherState.CurrentVersionName == ver.Id)
                    LauncherState.CurrentVersionName = null;
            }
        }

        private async void AddVersion_Click(object sender, RoutedEventArgs e)
        {
            await Task.CompletedTask;
        }

        // ================= 文件夹操作 =================
        private async void SwitchFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MinecraftFolder folder)
            {
                foreach (var f in Folders) f.IsActive = false;
                folder.IsActive = true;
                _currentMinecraftPath = folder.Path;
                await LoadRealVersionsAsync();
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
                        folder.DisplayName = ((TextBox)dialog.Content).Text;
                }
                catch { }
            }
        }

        private void DeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MinecraftFolder folder)
            {
                Folders.Remove(folder);
                if (folder.IsActive)
                {
                    _currentMinecraftPath = null;
                    LauncherState.CurrentMinecraftPath = null;
                    LauncherState.CurrentVersionName = null;
                    Versions.Clear();
                    Versions.Add(new GameVersion
                    {
                        Id = "hint",
                        DisplayName = "👋 请点击下方的「添加文件夹」按钮，选择你的 .minecraft 目录",
                        Path = "",
                        IsActive = false
                    });
                }
            }
        }
    }
}