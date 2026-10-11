using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinUi3_Minecraft_Launcher
{
    public class MicrosoftAuthResult
    {
        public string Username { get; set; } = "";
        public string Uuid { get; set; } = "";
        public string AccessToken { get; set; } = "";
    }

    public static class MicrosoftAuthService
    {
        // Prism Launcher 公开的 Client ID（开源社区标准做法）
        private const string ClientId = "c36a9fb6-4f2a-41ff-90bd-ae7cc92031eb";

        private const string DeviceCodeEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
        private const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
        private const string XblEndpoint = "https://user.auth.xboxlive.com/user/authenticate";
        private const string XstsEndpoint = "https://xsts.auth.xboxlive.com/xsts/authorize";
        private const string McLoginEndpoint = "https://api.minecraftservices.com/authentication/login_with_xbox";
        private const string McProfileEndpoint = "https://api.minecraftservices.com/minecraft/profile";

        private static readonly HttpClient Http = new HttpClient();

        public static async Task<MicrosoftAuthResult> LoginAsync(
            Action<string> log,
            Action<string, string>? onDeviceCode = null,
            CancellationToken cancellationToken = default)
        {
            log("[MSAuth] 开始微软登录（设备代码流程）");

            var deviceCodeInfo = await RequestDeviceCodeAsync(log);

            onDeviceCode?.Invoke(deviceCodeInfo.UserCode, deviceCodeInfo.VerificationUri);
            log($"[MSAuth] 请在浏览器访问 {deviceCodeInfo.VerificationUri} 并输入代码：{deviceCodeInfo.UserCode}");

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = deviceCodeInfo.VerificationUri,
                    UseShellExecute = true
                });
            }
            catch { }

            var msaToken = await PollForTokenAsync(deviceCodeInfo, log, cancellationToken);
            log("[MSAuth] MSA token 获取成功");

            var (xblToken, userHash) = await AuthenticateXboxLiveAsync(msaToken, log);
            var xstsToken = await AuthorizeXstsAsync(xblToken, log);
            var mcToken = await LoginMinecraftAsync(xstsToken, userHash, log);
            var (uuid, name) = await GetMinecraftProfileAsync(mcToken, log);

            log($"[MSAuth] ✅ 登录成功：{name} ({uuid})");

            return new MicrosoftAuthResult
            {
                Username = name,
                Uuid = uuid,
                AccessToken = mcToken
            };
        }

        private class DeviceCodeInfo
        {
            public string DeviceCode { get; set; } = "";
            public string UserCode { get; set; } = "";
            public string VerificationUri { get; set; } = "";
            public int Interval { get; set; } = 5;
            public int ExpiresIn { get; set; } = 900;
        }

        private static async Task<DeviceCodeInfo> RequestDeviceCodeAsync(Action<string> log)
        {
            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["scope"] = "XboxLive.signin offline_access"
            });

            var resp = await Http.PostAsync(DeviceCodeEndpoint, body);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"设备代码请求失败: {json}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new DeviceCodeInfo
            {
                DeviceCode = root.GetProperty("device_code").GetString() ?? "",
                UserCode = root.GetProperty("user_code").GetString() ?? "",
                VerificationUri = root.GetProperty("verification_uri").GetString() ?? "https://microsoft.com/link",
                Interval = root.TryGetProperty("interval", out var iv) ? iv.GetInt32() : 5,
                ExpiresIn = root.TryGetProperty("expires_in", out var ev) ? ev.GetInt32() : 900
            };
        }

        private static async Task<string> PollForTokenAsync(
            DeviceCodeInfo info,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.Now.AddSeconds(info.ExpiresIn);
            int interval = Math.Max(info.Interval, 5);

            while (DateTime.Now < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(interval * 1000, cancellationToken);

                var body = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = ClientId,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["device_code"] = info.DeviceCode
                });

                var resp = await Http.PostAsync(TokenEndpoint, body);
                var json = await resp.Content.ReadAsStringAsync();

                if (resp.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(json);
                    return doc.RootElement.GetProperty("access_token").GetString()
                        ?? throw new Exception("MSA token 为空");
                }

                using var errDoc = JsonDocument.Parse(json);
                var error = errDoc.RootElement.GetProperty("error").GetString();

                if (error == "authorization_pending") continue;
                else if (error == "slow_down") { interval += 5; continue; }
                else if (error == "expired_token") throw new Exception("设备代码已过期，请重新登录。");
                else if (error == "authorization_declined") throw new Exception("用户拒绝了授权。");
                else throw new Exception($"登录轮询失败: {error}\n{json}");
            }

            throw new Exception("登录超时（设备代码过期），请重试。");
        }

        private static async Task<(string token, string userHash)> AuthenticateXboxLiveAsync(string msaToken, Action<string> log)
        {
            var payload = new
            {
                Properties = new
                {
                    AuthMethod = "RPS",
                    SiteName = "user.auth.xboxlive.com",
                    RpsTicket = $"d={msaToken}"
                },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync(XblEndpoint, content);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"XBL 认证失败: {json}");

            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("Token").GetString();
            var userHash = doc.RootElement
                .GetProperty("DisplayClaims")
                .GetProperty("xui")[0]
                .GetProperty("uhs").GetString();

            log("[MSAuth] XBL token 获取成功");
            return (token ?? throw new Exception("XBL token 为空"),
                    userHash ?? throw new Exception("userHash 为空"));
        }

        private static async Task<string> AuthorizeXstsAsync(string xblToken, Action<string> log)
        {
            var payload = new
            {
                Properties = new
                {
                    SandboxId = "RETAIL",
                    UserTokens = new[] { xblToken }
                },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync(XstsEndpoint, content);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                try
                {
                    using var errDoc = JsonDocument.Parse(json);
                    if (errDoc.RootElement.TryGetProperty("XErr", out var xerr))
                    {
                        long code = xerr.GetInt64();
                        string msg = code switch
                        {
                            2148916233 => "该微软账号尚未注册 Xbox 账户，请先在 xbox.com 创建。",
                            2148916235 => "该账号所在地区被 Xbox Live 限制。",
                            2148916236 => "该账号需要成年验证。",
                            2148916237 => "该账号被限制登录。",
                            2148916238 => "该账号未满 18 岁，需要家长同意。",
                            _ => $"XSTS 认证失败（错误码 {code}）"
                        };
                        throw new Exception(msg);
                    }
                }
                catch (JsonException) { }
                throw new Exception($"XSTS 认证失败: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("Token").GetString();
            log("[MSAuth] XSTS token 获取成功");
            return token ?? throw new Exception("XSTS token 为空");
        }

        private static async Task<string> LoginMinecraftAsync(string xstsToken, string userHash, Action<string> log)
        {
            var payload = new
            {
                identityToken = $"XBL3.0 x={userHash};{xstsToken}"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var resp = await Http.PostAsync(McLoginEndpoint, content);
            var json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Minecraft 登录失败: {json}");

            using var doc = JsonDocument.Parse(json);
            var token = doc.RootElement.GetProperty("access_token").GetString();
            log("[MSAuth] Minecraft token 获取成功");
            return token ?? throw new Exception("Minecraft token 为空");
        }

        private static async Task<(string uuid, string name)> GetMinecraftProfileAsync(string mcToken, Action<string> log)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, McProfileEndpoint);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mcToken);

            var resp = await Http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                throw new Exception("该微软账号未购买 Minecraft Java 版。");

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"获取玩家档案失败: {json}");

            using var doc = JsonDocument.Parse(json);
            var uuid = doc.RootElement.GetProperty("id").GetString();
            var name = doc.RootElement.GetProperty("name").GetString();

            log("[MSAuth] 玩家档案获取成功");
            return (uuid ?? throw new Exception("UUID 为空"),
                    name ?? throw new Exception("玩家名为空"));
        }
    }
}