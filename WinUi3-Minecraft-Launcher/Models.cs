using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;

namespace WinUi3_Minecraft_Launcher
{
    public class GameVersion : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;
        private bool _isActive;

        public string Id { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveVisibility)); }
        }
        public Visibility ActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class MinecraftFolder : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;
        private bool _isActive;

        public string Path { get; set; } = string.Empty;
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(); }
        }
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActiveVisibility)); }
        }
        public Visibility ActiveVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LaunchProgress
    {
        public string Title { get; set; } = "正在启动...";
        public string Detail { get; set; } = "";
        public double Percentage { get; set; } = 0;
        public bool IsIndeterminate { get; set; } = false;
    }

    // 🚨 持久化配置
    public class FolderEntry
    {
        public string Path { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    public class AppConfig
    {
        public List<FolderEntry> Folders { get; set; } = new();
        public string? ActiveFolderPath { get; set; }
        public string? ActiveVersionName { get; set; }
        public string? JavaPath { get; set; }
        public int MaxRamMb { get; set; } = 0;
        public bool AutoMemory { get; set; } = true;
        public bool VersionIsolation { get; set; } = true;
        public bool AutoIsolation { get; set; } = true;
    }
}