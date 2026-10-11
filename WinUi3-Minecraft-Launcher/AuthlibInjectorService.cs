using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace WinUi3_Minecraft_Launcher
{
    public static class AuthlibInjectorService
    {
        // BMCLAPI 镜像
        private const string DownloadUrl = "https://bmclapi2.bangbang93.com/mirrors/authlib-injector/artifact/latest.json";

        private static readonly HttpClient Http = new HttpClient();

        public static string CacheDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WML", "authlib");

        public static string JarPath => Path.Combine(CacheDir, "authlib-injector.jar");

        public static async Task<string> EnsureJarAsync(Action<string> log)
        {
            if (File.Exists(JarPath))
            {
                log($"[Authlib] 已存在: {JarPath}");
                return JarPath;
            }

            Directory.CreateDirectory(CacheDir);

            log("[Authlib] 正在获取下载地址...");
            var metaJson = await Http.GetStringAsync(DownloadUrl);
            using var doc = System.Text.Json.JsonDocument.Parse(metaJson);
            var downloadUrl = doc.RootElement.GetProperty("download_url").GetString()
                ?? throw new Exception("获取 authlib-injector 下载地址失败");

            log($"[Authlib] 正在下载: {downloadUrl}");
            var bytes = await Http.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(JarPath, bytes);

            log($"[Authlib] ✅ 下载完成 ({bytes.Length / 1024} KB)");
            return JarPath;
        }
    }
}