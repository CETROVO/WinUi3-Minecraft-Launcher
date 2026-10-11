using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinUi3_Minecraft_Launcher
{
    public class ThirdPartyProfile
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class ThirdPartyAuthResult
    {
        public string Username { get; set; } = "";
        public string Uuid { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string ClientToken { get; set; } = "";
    }

    public static class ThirdPartyAuthService
    {
        private static readonly HttpClient Http = new HttpClient();

        public static async Task<(ThirdPartyAuthResult? result, List<ThirdPartyProfile>? profiles)> LoginAsync(
            string apiRoot,
            string username,
            string password,
            Action<string> log)
        {
            apiRoot = apiRoot.TrimEnd('/');
            log($"[3rd] 认证服务器: {apiRoot}");

            var clientToken = Guid.NewGuid().ToString("N");

            var payload = new
            {
                username = username,
                password = password,
                clientToken = clientToken,
                requestUser = true,
                agent = new { name = "Minecraft", version = 1 }
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var url = $"{apiRoot}/authserver/authenticate";
            log($"[3rd] POST {url}");

            var resp = await Http.PostAsync(url, content);
            var json = await resp.Content.ReadAsStringAsync();

            log($"[3rd] 状态码: {(int)resp.StatusCode}");

            if (!resp.IsSuccessStatusCode)
            {
                try
                {
                    using var errDoc = JsonDocument.Parse(json);
                    if (errDoc.RootElement.TryGetProperty("errorMessage", out var em))
                        throw new Exception(em.GetString() ?? "登录失败");
                }
                catch (JsonException) { }
                throw new Exception($"HTTP {(int)resp.StatusCode}: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var accessToken = root.GetProperty("accessToken").GetString() ?? "";

            // 唯一角色
            if (root.TryGetProperty("selectedProfile", out var sp) && sp.ValueKind == JsonValueKind.Object)
            {
                var name = sp.GetProperty("name").GetString() ?? username;
                var uuid = sp.GetProperty("id").GetString() ?? "";
                log($"[3rd] ✅ 登录成功（唯一角色）: {name} ({uuid})");

                return (new ThirdPartyAuthResult
                {
                    Username = name,
                    Uuid = uuid,
                    AccessToken = accessToken,
                    ClientToken = clientToken
                }, null);
            }

            // 多角色
            var profiles = new List<ThirdPartyProfile>();
            if (root.TryGetProperty("availableProfiles", out var ap) && ap.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in ap.EnumerateArray())
                {
                    profiles.Add(new ThirdPartyProfile
                    {
                        Id = p.GetProperty("id").GetString() ?? "",
                        Name = p.GetProperty("name").GetString() ?? ""
                    });
                }
            }

            if (profiles.Count == 0)
                throw new Exception("该账号在皮肤站中没有任何角色。请先登录皮肤站网站创建一个 Minecraft 角色。");

            log($"[3rd] 检测到 {profiles.Count} 个角色，需要用户选择");

            var partial = new ThirdPartyAuthResult
            {
                Username = "",
                Uuid = "",
                AccessToken = accessToken,
                ClientToken = clientToken
            };

            return (partial, profiles);
        }

        /// <summary>
        /// 用户选定角色后，调用 refresh 拿到最终 token
        /// 🚨 注意：selectedProfile 必须同时包含 id 和 name，LittleSkin 严格校验
        /// </summary>
        public static async Task<ThirdPartyAuthResult> SelectProfileAsync(
            string apiRoot,
            string accessToken,
            string clientToken,
            string profileId,
            string profileName,
            Action<string> log)
        {
            apiRoot = apiRoot.TrimEnd('/');

            var payload = new
            {
                accessToken = accessToken,
                clientToken = clientToken,
                selectedProfile = new
                {
                    id = profileId,
                    name = profileName      // 🚨 关键：LittleSkin 要求同时提供
                },
                requestUser = true
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var url = $"{apiRoot}/authserver/refresh";

            log($"[3rd] POST {url} (角色: {profileName} / {profileId})");

            var resp = await Http.PostAsync(url, content);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                try
                {
                    using var errDoc = JsonDocument.Parse(json);
                    if (errDoc.RootElement.TryGetProperty("errorMessage", out var em))
                        throw new Exception(em.GetString() ?? "选角色失败");
                }
                catch (JsonException) { }
                throw new Exception($"refresh 失败: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var newAccessToken = root.GetProperty("accessToken").GetString() ?? "";
            var sp = root.GetProperty("selectedProfile");

            var name = sp.GetProperty("name").GetString() ?? profileName;
            var uuid = sp.GetProperty("id").GetString() ?? profileId;

            log($"[3rd] ✅ 角色已选择: {name} ({uuid})");

            return new ThirdPartyAuthResult
            {
                Username = name,
                Uuid = uuid,
                AccessToken = newAccessToken,
                ClientToken = clientToken
            };
        }
    }
}