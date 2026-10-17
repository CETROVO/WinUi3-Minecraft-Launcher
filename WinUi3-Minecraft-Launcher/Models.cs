using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;

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

        public AccountType AccountType { get; set; } = AccountType.Offline;
        public string AccountUsername { get; set; } = "Steve";
        public string AccountUuid { get; set; } = "";
        public string AccountAccessToken { get; set; } = "";
        public string AccountAuthServerUrl { get; set; } = "";
    }

    public class LoaderVersion : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isStable;

        public string Version { get; set; } = "";
        public bool IsStable
        {
            get => _isStable;
            set { _isStable = value; OnPropertyChanged(); }
        }
        public string TypeText => IsStable ? "稳定版" : "测试版";

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedVisibility)); }
        }
        public Visibility SelectedVisibility => IsSelected ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class LoaderItem : INotifyPropertyChanged
    {
        private string _statusText = "可以添加";
        private bool _isCompatible = true;
        private bool _isSelected = false;
        private bool _isExpanded = false;
        private Visibility _showRemoveButton = Visibility.Collapsed;
        private Visibility _showArrow = Visibility.Visible;

        public string Name { get; set; } = "";
        public string SelectedVersion { get; set; } = "";
        public ObservableCollection<LoaderVersion> Versions { get; set; } = new();

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsCompatible
        {
            get => _isCompatible;
            set { _isCompatible = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
                ShowRemoveButton = value ? Visibility.Visible : Visibility.Collapsed;
                ShowArrow = value ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        public Visibility ShowRemoveButton
        {
            get => _showRemoveButton;
            set { _showRemoveButton = value; OnPropertyChanged(); }
        }

        public Visibility ShowArrow
        {
            get => _showArrow;
            set { _showArrow = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RemoteVersion
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Url { get; set; } = "";
        public string ReleaseTime { get; set; } = "";

        // 🚨 允许修改圆点颜色
        public SolidColorBrush DotColor { get; set; } = new SolidColorBrush(Colors.DodgerBlue);
    }

    public class BoolToVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}