using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinUi3_Minecraft_Launcher
{
    public static class DownloadService
    {
        private static readonly HttpClient Http = new HttpClient();

        public static async Task<List<RemoteVersion>> GetVersionManifestAsync()
        {
            // 使用 BMCLAPI 国内镜像
            const string url = "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json";
            var json = await Http.GetStringAsync(url);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var versions = new List<RemoteVersion>();
            foreach (var v in root.GetProperty("versions").EnumerateArray())
            {
                var type = v.GetProperty("type").GetString() ?? "";
                // 过滤掉远古版本，只保留 release 和 snapshot
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