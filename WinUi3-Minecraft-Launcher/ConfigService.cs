using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WinUi3_Minecraft_Launcher
{
    public static class ConfigService
    {
        private static readonly string ConfigDir = @"C:\WML\WML";
        private static readonly string ConfigPath = Path.Combine(ConfigDir, "Config.ini");
        private static readonly string StatePath = Path.Combine(ConfigDir, "Config.json");

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

        public static AppConfig Load()
        {
            var config = new AppConfig();
            if (!File.Exists(ConfigPath)) return config;

            try
            {
                var lines = File.ReadAllLines(ConfigPath);
                foreach (var line in lines)
                {
                    var l = line.Trim();
                    if (string.IsNullOrEmpty(l) || l.StartsWith("[") || !l.Contains('=')) continue;

                    var parts = l.Split('=', 2);
                    var key = parts[0].Trim();
                    var val = parts[1].Trim();

                    switch (key)
                    {
                        case "LauncherSystemUpdate": config.LauncherSystemUpdate = string.IsNullOrEmpty(val) ? 1 : (val == ":1" ? 1 : 2); break;
                        case "LauncherSystemPosts": config.LauncherSystemPosts = string.IsNullOrEmpty(val) ? 2 : (val == ":1" ? 1 : 2); break;
                        case "UpdateChannel": config.UpdateChannel = int.Parse(val); break;

                        case "Folders": config.Folders = JsonSerializer.Deserialize<List<FolderEntry>>(val) ?? new(); break;
                        case "ActiveFolderPath": config.ActiveFolderPath = val; break;
                        case "ActiveVersionName": config.ActiveVersionName = val; break;
                        case "IsolationMode": config.IsolationMode = int.Parse(val); break;
                        case "LauncherVisibility": config.LauncherVisibility = int.Parse(val); break;
                        case "ProcessPriority": config.ProcessPriority = int.Parse(val); break;
                        case "WindowSize": config.WindowSize = int.Parse(val); break;
                        case "AutoMemory": config.AutoMemory = bool.Parse(val); break;
                        case "MaxRamMb": config.MaxRamMb = int.Parse(val); break;
                        case "JvmArgs": config.JvmArgs = val; break;
                        case "GameArgs": config.GameArgs = val; break;
                        case "PreLaunchCommand": config.PreLaunchCommand = val; break;
                        case "JavaPath": config.JavaPath = string.IsNullOrEmpty(val) ? null : val; break;
                        case "MemoryAlgorithm": config.MemoryAlgorithm = int.Parse(val); break;
                        case "DisableJavaLaunchWrapper": config.DisableJavaLaunchWrapper = bool.Parse(val); break;
                        case "DisableLWJGLUnsafeAgent": config.DisableLWJGLUnsafeAgent = bool.Parse(val); break;
                        case "UseHighPerformanceGpu": config.UseHighPerformanceGpu = bool.Parse(val); break;
                        case "FileDownloadSource": config.FileDownloadSource = int.Parse(val); break;
                        case "VersionListSource": config.VersionListSource = int.Parse(val); break;
                        case "CommunitySource": config.CommunitySource = int.Parse(val); break;
                        case "FileNameFormat": config.FileNameFormat = int.Parse(val); break;
                        case "ModManageStyle": config.ModManageStyle = int.Parse(val); break;
                        case "ShowReleaseUpdate": config.ShowReleaseUpdate = bool.Parse(val); break;
                        case "ShowSnapshotUpdate": config.ShowSnapshotUpdate = bool.Parse(val); break;
                        case "AutoSetChinese": config.AutoSetChinese = bool.Parse(val); break;
                        case "CacheFolder": config.CacheFolder = val; break;
                        case "AllowAnonData": config.AllowAnonData = bool.Parse(val); break;
                        case "AccountType": config.AccountType = Enum.Parse<AccountType>(val); break;
                        case "AccountUsername": config.AccountUsername = val; break;
                        case "AccountUuid": config.AccountUuid = val; break;
                        case "AccountAccessToken": config.AccountAccessToken = val; break;
                        case "AccountAuthServerUrl": config.AccountAuthServerUrl = val; break;
                    }
                }
            }
            catch { }

            return config;
        }

        public static void Save(AppConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);

                var lines = new List<string>
                {
                    "[Launcher]",
                    $"LauncherSystemUpdate={(config.LauncherSystemUpdate == 1 ? ":1" : ":2")}",
                    $"LauncherSystemPosts={(config.LauncherSystemPosts == 1 ? ":1" : ":2")}",
                    $"UpdateChannel={config.UpdateChannel}",
                    "",
                    "[Settings]",
                    $"Folders={JsonSerializer.Serialize(config.Folders)}",
                    $"ActiveFolderPath={config.ActiveFolderPath}",
                    $"ActiveVersionName={config.ActiveVersionName}",
                    $"IsolationMode={config.IsolationMode}",
                    $"LauncherVisibility={config.LauncherVisibility}",
                    $"ProcessPriority={config.ProcessPriority}",
                    $"WindowSize={config.WindowSize}",
                    $"AutoMemory={config.AutoMemory}",
                    $"MaxRamMb={config.MaxRamMb}",
                    $"JvmArgs={config.JvmArgs}",
                    $"GameArgs={config.GameArgs}",
                    $"PreLaunchCommand={config.PreLaunchCommand}",
                    $"JavaPath={config.JavaPath}",
                    $"MemoryAlgorithm={config.MemoryAlgorithm}",
                    $"DisableJavaLaunchWrapper={config.DisableJavaLaunchWrapper}",
                    $"DisableLWJGLUnsafeAgent={config.DisableLWJGLUnsafeAgent}",
                    $"UseHighPerformanceGpu={config.UseHighPerformanceGpu}",
                    $"FileDownloadSource={config.FileDownloadSource}",
                    $"VersionListSource={config.VersionListSource}",
                    $"CommunitySource={config.CommunitySource}",
                    $"FileNameFormat={config.FileNameFormat}",
                    $"ModManageStyle={config.ModManageStyle}",
                    $"ShowReleaseUpdate={config.ShowReleaseUpdate}",
                    $"ShowSnapshotUpdate={config.ShowSnapshotUpdate}",
                    $"AutoSetChinese={config.AutoSetChinese}",
                    $"CacheFolder={config.CacheFolder}",
                    $"AllowAnonData={config.AllowAnonData}",
                    "",
                    "[Account]",
                    $"AccountType={config.AccountType}",
                    $"AccountUsername={config.AccountUsername}",
                    $"AccountUuid={config.AccountUuid}",
                    $"AccountAccessToken={config.AccountAccessToken}",
                    $"AccountAuthServerUrl={config.AccountAuthServerUrl}"
                };

                File.WriteAllLines(ConfigPath, lines);
            }
            catch { }
        }

        public static AppState LoadState()
        {
            try
            {
                if (!File.Exists(StatePath)) return new AppState();
                var json = File.ReadAllText(StatePath);
                var state = JsonSerializer.Deserialize<AppState>(json, JsonOpts) ?? new AppState();

                // 🚨 窗口位置不再恢复，强制归零
                state.WindowX = 0;
                state.WindowY = 0;

                return state;
            }
            catch
            {
                return new AppState();
            }
        }

        public static void SaveState(AppState state)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);

                // 🚨 窗口位置不再持久化
                state.WindowX = 0;
                state.WindowY = 0;

                var json = JsonSerializer.Serialize(state, JsonOpts);
                File.WriteAllText(StatePath, json);
            }
            catch { }
        }
    }
}