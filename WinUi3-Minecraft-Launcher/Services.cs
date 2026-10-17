using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder; // 🚨 必须有：用于 MLaunchOption 和 BuildProcessAsync
using Microsoft.UI.Xaml.Data;

namespace WinUi3_Minecraft_Launcher
{
    public enum AccountType { Offline, Microsoft, ThirdParty }

    public class UserAccount : INotifyPropertyChanged
    {
        private string _username = "Steve";
        private string _uuid = "";
        private string _accessToken = "";
        private AccountType _type = AccountType.Offline;
        private string _authServerUrl = "";

        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
        public string Uuid { get => _uuid; set { _uuid = value; OnPropertyChanged(); } }
        public string AccessToken { get => _accessToken; set { _accessToken = value; OnPropertyChanged(); } }
        public AccountType Type { get => _type; set { _type = value; OnPropertyChanged(); } }
        public string AuthServerUrl { get => _authServerUrl; set { _authServerUrl = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public static class AccountService
    {
        public static UserAccount Current { get; } = new UserAccount();

        public static MSession GetSession()
        {
            if (Current.Type == AccountType.Offline)
                return MSession.CreateOfflineSession(Current.Username);
            else
                return new MSession(Current.Username, Current.AccessToken, Current.Uuid);
        }

        public static string GetTypeDisplayName() => Current.Type switch
        {
            AccountType.Offline => "离线账户",
            AccountType.Microsoft => "微软正版",
            AccountType.ThirdParty => "第三方认证",
            _ => "未知"
        };
    }

    public class AccountTypeToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is AccountType type)
            {
                return type switch
                {
                    AccountType.Offline => "离线账户",
                    AccountType.Microsoft => "微软正版",
                    AccountType.ThirdParty => "第三方认证",
                    _ => "未知"
                };
            }
            return "未知";
        }
        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public static class LauncherState
    {
        public static string? CurrentMinecraftPath { get; set; }
        public static string? CurrentVersionName { get; set; }
        public static string? JavaPath { get; set; }
        public static int MaxRamMb { get; set; } = 0;
        public static bool AutoMemory { get; set; } = true;
        public static bool VersionIsolation { get; set; } = true;
        public static bool AutoIsolation { get; set; } = true;
    }

    public static class JavaFinder
    {
        private static readonly string[] SearchRoots =
        {
            @"C:\Program Files\Eclipse Adoptium", @"C:\Program Files\Java",
            @"C:\Program Files\Microsoft", @"C:\Program Files\BellSoft",
            @"C:\Program Files\Amazon Corretto", @"C:\Program Files\Zulu",
            @"C:\Program Files\Semeru", @"C:\Program Files\RedHat",
            @"C:\Program Files (x86)\Java", @"D:\Java", @"D:\Program Files\Java",
            @"D:\Program Files\Eclipse Adoptium", @"D:\Program Files (x86)\Java", @"E:\Java",
        };

        public static List<string> FindAllJavas()
        {
            var result = new List<string>();
            var roots = SearchRoots.ToList();
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome) && Directory.Exists(javaHome)) roots.Add(javaHome);

            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                try
                {
                    var files = Directory.GetFiles(root, "java.exe", SearchOption.AllDirectories);
                    foreach (var f in files)
                    {
                        if (!result.Contains(f)) result.Add(f);
                    }
                }
                catch { }
            }
            return result;
        }

        public static string? FindJava()
        {
            if (!string.IsNullOrEmpty(LauncherState.JavaPath) && File.Exists(LauncherState.JavaPath))
            {
                if (LauncherState.JavaPath.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var javaExe = Path.Combine(Path.GetDirectoryName(LauncherState.JavaPath) ?? "", "java.exe");
                    if (File.Exists(javaExe)) return javaExe;
                }
                return LauncherState.JavaPath;
            }
            return FindAllJavas().FirstOrDefault();
        }

        public static string? FindJavaByMajorVersion(int majorVersion)
        {
            if (!string.IsNullOrEmpty(LauncherState.JavaPath) && File.Exists(LauncherState.JavaPath))
            {
                var manualMajor = GetJavaMajorVersion(LauncherState.JavaPath);
                if (manualMajor == majorVersion) return FindJava();
            }
            foreach (var javaExe in FindAllJavas())
            {
                if (GetJavaMajorVersion(javaExe) == majorVersion) return javaExe;
            }
            return null;
        }

        public static bool Is64BitJava(string javaPath)
        {
            try
            {
                if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath)) return true;
                var binDir = Path.GetDirectoryName(javaPath);
                var homeDir = Directory.GetParent(binDir ?? "")?.FullName ?? "";
                if (homeDir.Contains("Program Files (x86)")) return false;
                var releaseFile = Path.Combine(homeDir, "release");
                if (File.Exists(releaseFile))
                {
                    var archLine = File.ReadAllLines(releaseFile).FirstOrDefault(l => l.StartsWith("OS_ARCH="));
                    if (archLine != null && archLine.Contains("x86") && !archLine.Contains("x86_64") && !archLine.Contains("amd64")) return false;
                }
                return true;
            }
            catch { return true; }
        }

        public static int GetJavaMajorVersion(string javaPath)
        {
            try
            {
                if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath)) return 0;
                var binDir = Path.GetDirectoryName(javaPath);
                var homeDir = Directory.GetParent(binDir ?? "")?.FullName;
                if (homeDir == null) return 0;
                var releaseFile = Path.Combine(homeDir, "release");
                if (File.Exists(releaseFile))
                {
                    var line = File.ReadAllLines(releaseFile).FirstOrDefault(l => l.StartsWith("JAVA_VERSION="));
                    if (line != null)
                    {
                        var verStr = line.Split('=')[1].Trim('"');
                        var majorStr = verStr.Split('.')[0];
                        if (int.TryParse(majorStr, out int major)) return major;
                    }
                }
            }
            catch { }
            return 0;
        }
    }

    public static class MemoryAllocator
    {
        [StructLayout(LayoutKind.Sequential)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength; public uint dwMemoryLoad; public ulong ullTotalPhys; public ulong ullAvailPhys;
            public ulong ullTotalPageFile; public ulong ullAvailPageFile; public ulong ullTotalVirtual; public ulong ullAvailVirtual; public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() { this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public static double GetAvailablePhysicalMemoryGB()
        {
            try
            {
                var m = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(m)) return m.ullAvailPhys / 1024.0 / 1024.0 / 1024.0;
            }
            catch { }
            return 4.0;
        }

        public static int CalculateAutoMemoryMb(string versionDir, bool is64BitJava)
        {
            if (!is64BitJava) return 1024;
            int modCount = 0;
            var modsDir = Path.Combine(versionDir, "mods");
            if (Directory.Exists(modsDir))
            {
                try
                {
                    modCount = Directory.GetFiles(modsDir, "*.*", SearchOption.TopDirectoryOnly)
                        .Count(f => f.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                    f.EndsWith(".litemod", StringComparison.OrdinalIgnoreCase));
                }
                catch { }
            }
            double ramMinimum = 0.5 + modCount / 150.0;
            double ramTarget1 = 1.5 + modCount / 90.0;
            double ramTarget2 = 2.7 + modCount / 50.0;
            double ramTarget3 = 4.5 + modCount / 25.0;
            double avail = GetAvailablePhysicalMemoryGB();
            double allocated = 0;
            double remaining = avail;

            double p1 = Math.Min(remaining, ramTarget1);
            allocated += p1; remaining -= p1;
            if (remaining > 0 && allocated < ramTarget2)
            {
                double p2 = Math.Min(remaining * 0.7, ramTarget2 - allocated);
                allocated += p2; remaining -= p2;
            }
            if (remaining > 0 && allocated < ramTarget3)
            {
                double p3 = Math.Min(remaining * 0.4, ramTarget3 - allocated);
                allocated += p3; remaining -= p3;
            }
            if (remaining > 0) allocated += remaining * 0.15;
            allocated = Math.Max(allocated, ramMinimum);
            allocated = Math.Min(allocated, avail * 0.8);
            int resultMb = (int)(allocated * 1024);
            resultMb = (resultMb / 32) * 32;
            return resultMb;
        }
    }

    public static class AuthlibInjectorService
    {
        private static readonly HttpClient Http = new HttpClient();
        public static string CacheDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WML", "authlib");
        public static string JarPath => Path.Combine(CacheDir, "authlib-injector.jar");

        public static async Task<string> EnsureJarAsync(Action<string> log)
        {
            if (File.Exists(JarPath))
            {
                var len = new FileInfo(JarPath).Length;
                if (len > 1000000) { log($"[Authlib] 本地已存在有效 jar ({len / 1024} KB)"); return JarPath; }
                log($"[Authlib] 本地 jar 过小({len} 字节)，准备重新下载"); try { File.Delete(JarPath); } catch { }
            }

            Directory.CreateDirectory(CacheDir);
            log("[Authlib] 正在从官方源获取最新版本信息...");
            var metaJson = await Http.GetStringAsync("https://authlib-injector.yushi.moe/artifact/latest.json");
            using var doc = JsonDocument.Parse(metaJson);
            var downloadUrl = doc.RootElement.GetProperty("download_url").GetString() ?? throw new Exception("获取 authlib-injector 下载地址失败");

            log($"[Authlib] 正在下载: {downloadUrl}");
            var bytes = await Http.GetByteArrayAsync(downloadUrl);
            if (bytes.Length < 1000000) throw new Exception($"下载的文件过小 ({bytes.Length} 字节)。");

            await File.WriteAllBytesAsync(JarPath, bytes);
            log($"[Authlib] ✅ 下载完成 ({bytes.Length / 1024} KB)");
            return JarPath;
        }
    }

    public static class GameLauncher
    {
        private static bool ShouldIsolate(string minecraftPath, string versionName)
        {
            var versionDir = Path.Combine(minecraftPath, "versions", versionName);
            var isolateIndicators = new[] { "mods", "saves", "config", "resourcepacks", "shaderpacks", "logs", "crash-reports" };
            foreach (var sub in isolateIndicators) if (Directory.Exists(Path.Combine(versionDir, sub))) return true;
            if (File.Exists(Path.Combine(versionDir, "options.txt"))) return true;
            if (File.Exists(Path.Combine(versionDir, "servers.dat"))) return true;
            return false;
        }

        public static async Task<Process> LaunchAsync(
            string minecraftPath,
            string versionName,
            int maxRamMb,
            Action<string> log,
            IProgress<LaunchProgress>? progress,
            CancellationToken cancellationToken)
        {
            void Report(string title, string detail, double pct, bool indeterminate = false)
            {
                progress?.Report(new LaunchProgress { Title = title, Detail = detail, Percentage = pct, IsIndeterminate = indeterminate });
            }

            cancellationToken.ThrowIfCancellationRequested();
            Report("正在启动游戏...", "初始化中", 0);
            log("========== 启动流程开始 ==========");
            log($"游戏路径: {minecraftPath}");
            log($"目标版本: {versionName}");

            string? javaPath = JavaFinder.FindJava();
            if (javaPath == null) throw new Exception("找不到可用的 Java，请在设置里手动指定路径。");
            log($"最终使用 Java: {javaPath}");

            var versionDir = Path.Combine(minecraftPath, "versions", versionName);
            string gameDir = (LauncherState.AutoIsolation && ShouldIsolate(minecraftPath, versionName)) || LauncherState.VersionIsolation ? versionDir : minecraftPath;
            log($"📂 游戏目录 (gameDir): {gameDir}");

            var session = AccountService.GetSession();
            log($"账户: {session.Username} ({AccountService.GetTypeDisplayName()})");

            bool is64Bit = JavaFinder.Is64BitJava(javaPath);
            int finalRam = !is64Bit ? 1024 : (maxRamMb <= 0 || LauncherState.AutoMemory ? MemoryAllocator.CalculateAutoMemoryMb(versionDir, is64Bit) : maxRamMb);
            log($"内存分配: {finalRam}MB");

            // 🚨 4.0.6 的 JVM 参数类型是 MArgument
            var extraJvmArgs = new List<CmlLib.Core.ProcessBuilder.MArgument>();
            if (AccountService.Current.Type == AccountType.ThirdParty && !string.IsNullOrEmpty(AccountService.Current.AuthServerUrl))
            {
                try
                {
                    Report("正在启动游戏...", "准备第三方登录认证", 25);
                    var jarPath = await AuthlibInjectorService.EnsureJarAsync(log);
                    var agentArg = $"-javaagent:{jarPath}={AccountService.Current.AuthServerUrl}";
                    extraJvmArgs.Add(new CmlLib.Core.ProcessBuilder.MArgument(agentArg));
                    log($"[Authlib] 注入参数: {agentArg}");
                }
                catch (Exception ex) { log($"[Authlib] ⚠️ 注入失败: {ex.Message}"); }
            }

            var extraGameArgs = new List<CmlLib.Core.ProcessBuilder.MArgument>
            {
                new CmlLib.Core.ProcessBuilder.MArgument("--gameDir"),
                new CmlLib.Core.ProcessBuilder.MArgument(gameDir)
            };

            // 🚨 4.0.6 专属属性名
            var option = new MLaunchOption
            {
                Session = session,
                MaximumRamMb = finalRam,
                JavaPath = javaPath,
                JvmArgumentOverrides = extraJvmArgs.ToArray(),
                ExtraGameArguments = extraGameArgs.ToArray()
            };

            Report("正在准备游戏资源...", "检查游戏文件完整性", 50, indeterminate: true);

            var launcher = new MinecraftLauncher(new MinecraftPath(minecraftPath));
            var process = await launcher.BuildProcessAsync(versionName, option);

            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            Report("正在启动 JVM...", "启动游戏进程", 90);

            var stderrBuilder = new StringBuilder();
            var stdoutBuilder = new StringBuilder();

            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    log($"[MC] {e.Data}");
                    stdoutBuilder.AppendLine(e.Data);
                    if (stdoutBuilder.Length > 50000) stdoutBuilder.Remove(0, 25000);
                }
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    log($"[MC-ERR] {e.Data}");
                    stderrBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            log($"✅ 游戏进程已启动，PID: {process.Id}");

            Report("正在等待游戏窗口...", "游戏正在加载，请稍候", 95, indeterminate: true);
            try
            {
                await WaitForGameWindowAsync(process, cancellationToken, log, stderrBuilder, stdoutBuilder);
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) { process.Kill(true); log("🛑 用户取消，已终止游戏进程"); } } catch { }
                throw;
            }

            Report("✅ 游戏已启动", $"PID: {process.Id}", 100);
            return process;
        }

        private static async Task WaitForGameWindowAsync(Process process, CancellationToken ct, Action<string> log, StringBuilder stderrBuilder, StringBuilder stdoutBuilder)
        {
            var timeout = DateTime.Now.AddSeconds(120);
            while (DateTime.Now < timeout)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (process.HasExited)
                    {
                        var errorOutput = stderrBuilder.ToString();
                        var standardOutput = stdoutBuilder.ToString();
                        var fullLog = $"[STDERR]\n{errorOutput}\n\n[STDOUT]\n{standardOutput}";

                        if (string.IsNullOrWhiteSpace(errorOutput) && string.IsNullOrWhiteSpace(standardOutput))
                            fullLog = "Java 没有输出任何信息就死了，可能是 JVM 参数错误或内存不足。";

                        try
                        {
                            Directory.CreateDirectory(@"C:\MinecraftTemp");
                            var crashFile = Path.Combine(@"C:\MinecraftTemp", "last_crash.log");
                            await File.WriteAllTextAsync(crashFile, fullLog);
                            log($"[Crash] 崩溃日志已保存至: {crashFile}");
                        }
                        catch (Exception ex)
                        {
                            log($"[Crash] 写入崩溃日志失败: {ex.Message}");
                        }

                        var lines = fullLog.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                        var tail = string.Join("\n", lines.TakeLast(20));

                        throw new Exception($"游戏进程已退出 (ExitCode: {process.ExitCode})\n\n【崩溃信息摘要】:\n{tail}");
                    }
                    process.Refresh();
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        log($"✅ 检测到游戏窗口 (Handle: {process.MainWindowHandle})");
                        return;
                    }
                }
                catch (InvalidOperationException) { throw new Exception("游戏进程异常退出。"); }
                await Task.Delay(400, ct);
            }
            log("⚠️ 等待游戏窗口超时，但进程仍在运行");
        }
    }
}