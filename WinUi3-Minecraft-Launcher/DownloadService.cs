using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinUi3_Minecraft_Launcher
{
    public static class DownloadService
    {
        public static async Task<List<RemoteVersion>> GetVersionManifestAsync()
        {
            string officialUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
            string mirrorUrl = "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json";

            string json = "";

            if (LauncherState.CurrentSource == DownloadSource.Mirror)
            {
                System.Diagnostics.Debug.WriteLine("[源] 强制使用镜像源");
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                json = await client.GetStringAsync(mirrorUrl);
            }
            else if (LauncherState.CurrentSource == DownloadSource.Official)
            {
                System.Diagnostics.Debug.WriteLine("[源] 强制使用官方源");
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                json = await client.GetStringAsync(officialUrl);
            }
            else // Auto
            {
                // 🚨 自动模式：优先官方源，5秒超时切镜像
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    json = await client.GetStringAsync(officialUrl);
                    System.Diagnostics.Debug.WriteLine("[源] 自动模式 - 官方源连接成功");
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("[源] 自动模式 - 官方源超时，切换至 BMCLAPI 镜像");
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    json = await client.GetStringAsync(mirrorUrl);
                }
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var versions = new List<RemoteVersion>();
            foreach (var v in root.GetProperty("versions").EnumerateArray())
            {
                var type = v.GetProperty("type").GetString() ?? "";
                if (type != "release" && type != "snapshot") continue;

                versions.Add(new RemoteVersion
                {
                    Id = v.GetProperty("id").GetString() ?? "",
                    Type = type,
                    Url = v.GetProperty("url").GetString() ?? "",
                    ReleaseTime = v.GetProperty("releaseTime").GetString() ?? ""
                });
            }
            return versions;
        }
    }
}