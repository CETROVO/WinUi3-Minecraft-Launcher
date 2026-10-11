using System;
using System.IO;
using System.Text.Json;

namespace WinUi3_Minecraft_Launcher
{
    public static class ConfigService
    {
        private static string ConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WML");

        private static string ConfigPath => Path.Combine(ConfigDir, "config.json");

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true
        };

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath)) return new AppConfig();
                var json = File.ReadAllText(ConfigPath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts);
                return cfg ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }

        public static void Save(AppConfig config)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                var json = JsonSerializer.Serialize(config, JsonOpts);
                File.WriteAllText(ConfigPath, json);
            }
            catch { /* 忽略写入失败 */ }
        }
    }
}