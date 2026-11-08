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

            // 🚨 核心：实时读取 Page3 设置里的 "版本列表源" (0,1,2,3)
            int source = ConfigService.Load().VersionListSource;

            if (source == 3) // 仅镜像
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                json = await client.GetStringAsync(mirrorUrl);
            }
            else if (source == 2) // 仅官方
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                json = await client.GetStringAsync(officialUrl);
            }
            else if (source == 1) // 优先镜像，慢换官方
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    json = await client.GetStringAsync(mirrorUrl);
                }
                catch
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    json = await client.GetStringAsync(officialUrl);
                }
            }
            else // 0: 优先官方，慢换镜像 (默认)
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    json = await client.GetStringAsync(officialUrl);
                }
                catch
                {
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