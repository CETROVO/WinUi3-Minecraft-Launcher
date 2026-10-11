using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;
using Microsoft.UI.Xaml.Data;

namespace WinUi3_Minecraft_Launcher
{
    public enum AccountType { Offline, Microsoft, ThirdParty }

    // ================= 可通知的账户模型 =================
    public class UserAccount : INotifyPropertyChanged
    {
        private string _username = "Steve";
        private string _uuid = "";
        private string _accessToken = "";
        private AccountType _type = AccountType.Offline;
        private string _authServerUrl = "";

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string Uuid
        {
            get => _uuid;
            set { _uuid = value; OnPropertyChanged(); }
        }

        public string AccessToken
        {
            get => _accessToken;
            set { _accessToken = value; OnPropertyChanged(); }
        }

        public AccountType Type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(); }
        }

        public string AuthServerUrl
        {
            get => _authServerUrl;
            set { _authServerUrl = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
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
            @"C:\Program Files\Eclipse Adoptium",
            @"C:\Program Files\Java",
            @"C:\Program Files\Microsoft",
            @"C:\Program Files\BellSoft",
            @"C:\Program Files\Amazon Corretto",
            @"C:\Program Files\Zulu",
            @"C:\Program Files\Semeru",
            @"C:\Program Files\RedHat",
            @"C:\Program Files (x86)\Java",
            @"D:\Java",
            @"D:\Program Files\Java",
            @"D:\Program Files\Eclipse Adoptium",
            @"D:\Program Files (x86)\Java",
            @"E:\Java",
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
                    var files = Directory.GetFiles(root, "javaw.exe", SearchOption.AllDirectories);
                    result.AddRange(files);
                }
                catch { }
            }
            return result.Distinct().ToList();
        }

        public static string? FindJava()
        {
            if (!string.IsNullOrEmpty(LauncherState.JavaPath) && File.Exists(LauncherState.JavaPath))
                return LauncherState.JavaPath;
            return FindAllJavas().FirstOrDefault();
        }

        public static string? FindJavaByMajorVersion(int majorVersion)
        {
            if (!string.IsNullOrEmpty(LauncherState.JavaPath) && File.Exists(LauncherState.JavaPath))
            {
                var manualMajor = GetJavaMajorVersion(LauncherState.JavaPath);
                if (manualMajor == majorVersion) return LauncherState.JavaPath;
            }

            foreach (var javaw in FindAllJavas())
            {
                if (GetJavaMajorVersion(javaw) == majorVersion)
                    return javaw;
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
                    var archLine = File.ReadAllLines(releaseFile)
                        .FirstOrDefault(l => l.StartsWith("OS_ARCH="));
                    if (archLine != null && archLine.Contains("x86") &&
                        !archLine.Contains("x86_64") && !archLine.Contains("amd64"))
                        return false;
                }
                return true;
            }
            catch { return true; }
        }

        public static int GetJavaMajorVersion(string javawPath)
        {
            try
            {
                if (string.IsNullOrEmpty(javawPath) || !File.Exists(javawPath)) return 0;
                var binDir = Path.GetDirectoryName(javawPath);
                var homeDir = Directory.GetParent(binDir ?? "")?.FullName;
                if (homeDir == null) return 0;

                var releaseFile = Path.Combine(homeDir, "release");
                if (File.Exists(releaseFile))
                {
                    var line = File.ReadAllLines(releaseFile)
                        .FirstOrDefault(l => l.StartsWith("JAVA_VERSION="));
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
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
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

    public static class GameLauncher
    {
        private static bool ShouldIsolate(string minecraftPath, string versionName)
        {
            var versionDir = Path.Combine(minecraftPath, "versions", versionName);

            var isolateIndicators = new[] { "mods", "saves", "config", "resourcepacks", "shaderpacks", "logs", "crash-reports" };
            foreach (var sub in isolateIndicators)
            {
                if (Directory.Exists(Path.Combine(versionDir, sub))) return true;
            }

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
                progress?.Report(new LaunchProgress
                {
                    Title = title,
                    Detail = detail,
                    Percentage = pct,
                    IsIndeterminate = indeterminate
                });
            }

            cancellationToken.ThrowIfCancellationRequested();
            Report("正在启动游戏...", "初始化中", 0);

            log("========== 启动流程开始 ==========");
            log($"游戏路径: {minecraftPath}");
            log($"目标版本: {versionName}");

            // 1. 读版本要求的 Java
            cancellationToken.ThrowIfCancellationRequested();
            Report("正在启动游戏...", "读取版本信息", 5);
            int requiredJavaMajor = 0;
            try
            {
                var versionJsonPath = Path.Combine(minecraftPath, "versions", versionName, $"{versionName}.json");
                if (File.Exists(versionJsonPath))
                {
                    var json = JsonDocument.Parse(File.ReadAllText(versionJsonPath));
                    if (json.RootElement.TryGetProperty("javaVersion", out var jv) &&
                        jv.TryGetProperty("majorVersion", out var mv))
                    {
                        requiredJavaMajor = mv.GetInt32();
                    }
                }
            }
            catch (Exception ex)
            {
                log($"⚠️ 读取版本 Java 要求失败: {ex.Message}");
            }

            if (requiredJavaMajor > 0)
                log($"📌 版本 {versionName} 需要 Java {requiredJavaMajor}");

            // 2. 扫 Java
            cancellationToken.ThrowIfCancellationRequested();
            Report("正在启动游戏...", "扫描 Java 环境", 10);
            var allJavas = JavaFinder.FindAllJavas();
            log($"========== 系统 Java 清单（共 {allJavas.Count} 个） ==========");
            foreach (var j in allJavas)
            {
                var ver = JavaFinder.GetJavaMajorVersion(j);
                var arch = JavaFinder.Is64BitJava(j) ? "x64" : "x86";
                log($"  [Java {ver,-3} {arch}] {j}");
            }
            log("=================================================");

            // 3. 选 Java
            cancellationToken.ThrowIfCancellationRequested();
            Report("正在启动游戏...", "选择 Java 运行时", 15);
            string? javaPath = null;

            if (!string.IsNullOrEmpty(LauncherState.JavaPath) && File.Exists(LauncherState.JavaPath))
            {
                var manualMajor = JavaFinder.GetJavaMajorVersion(LauncherState.JavaPath);
                if (requiredJavaMajor == 0 || manualMajor == requiredJavaMajor)
                {
                    javaPath = LauncherState.JavaPath;
                    log($"✅ 使用手动指定的 Java {manualMajor}: {javaPath}");
                }
            }

            if (javaPath == null && requiredJavaMajor > 0)
            {
                javaPath = JavaFinder.FindJavaByMajorVersion(requiredJavaMajor);
                if (javaPath != null)
                    log($"✅ 自动匹配到 Java {requiredJavaMajor}: {javaPath}");
            }

            if (javaPath == null && requiredJavaMajor == 0)
            {
                javaPath = JavaFinder.FindJava();
                if (javaPath != null)
                    log($"⚠️ 使用兜底 Java: {javaPath}");
            }

            if (javaPath == null)
            {
                if (requiredJavaMajor > 0)
                    throw new Exception($"版本 {versionName} 需要 Java {requiredJavaMajor}，但系统里找不到。\n请到 https://adoptium.net/temurin/releases/?version={requiredJavaMajor} 下载安装后再试。");
                else
                    throw new Exception("找不到可用的 Java，请在设置里手动指定 javaw.exe 路径。");
            }

            var actualMajor = JavaFinder.GetJavaMajorVersion(javaPath);
            if (requiredJavaMajor > 0 && actualMajor != requiredJavaMajor)
                throw new Exception($"Java 版本不匹配！版本要求 Java {requiredJavaMajor}，实际 Java {actualMajor}");

            log($"最终使用 Java {actualMajor}: {javaPath}");

            // 4. 版本隔离
            cancellationToken.ThrowIfCancellationRequested();
            var versionDir = Path.Combine(minecraftPath, "versions", versionName);
            string gameDir;

            if (LauncherState.AutoIsolation)
            {
                bool isolate = ShouldIsolate(minecraftPath, versionName);
                gameDir = isolate ? versionDir : minecraftPath;
                log($"🔍 智能隔离判断：{(isolate ? $"启用隔离 (versions/{versionName}/)" : "不隔离 (.minecraft 根目录)")}");
            }
            else
            {
                gameDir = LauncherState.VersionIsolation ? versionDir : minecraftPath;
                log($"🔧 手动隔离设置：{(LauncherState.VersionIsolation ? "启用隔离" : "不隔离")}");
            }

            log($"📂 游戏目录 (gameDir): {gameDir}");

            // 5. 账户
            cancellationToken.ThrowIfCancellationRequested();
            Report("正在启动游戏...", "初始化账户", 20);
            var session = AccountService.GetSession();
            log($"账户: {session.Username} ({AccountService.GetTypeDisplayName()})");

            // 6. 内存
            bool is64Bit = JavaFinder.Is64BitJava(javaPath);
            int finalRam;
            if (!is64Bit) finalRam = 1024;
            else if (maxRamMb <= 0 || LauncherState.AutoMemory)
            {
                finalRam = MemoryAllocator.CalculateAutoMemoryMb(versionDir, is64Bit);
            }
            else finalRam = maxRamMb;
            log($"内存分配: {finalRam}MB");

            // 7. 临时目录
            var tempDir = @"C:\MinecraftTemp";
            if (!Directory.Exists(tempDir))
            {
                try { Directory.CreateDirectory(tempDir); }
                catch { tempDir = Path.GetTempPath(); }
            }

            // 8. JVM 参数
            var jvmArgs = new List<MArgument>
            {
                new MArgument($"-Xmx{finalRam}M"),
                new MArgument($"-Xms{Math.Min(512, finalRam)}M"),
                new MArgument("-XX:+UseG1GC"),
                new MArgument("-XX:+UnlockExperimentalVMOptions"),
                new MArgument("-XX:G1NewSizePercent=20"),
                new MArgument("-XX:G1ReservePercent=20"),
                new MArgument("-XX:MaxGCPauseMillis=50"),
                new MArgument("-Dfile.encoding=UTF-8"),
                new MArgument("-Dstdout.encoding=UTF-8"),
                new MArgument("-Dstderr.encoding=UTF-8"),
                new MArgument($"-Djava.io.tmpdir={tempDir}"),
            };

            // 🚨 第三方登录：注入 authlib-injector（追加到末尾，不覆盖其他参数）
            if (AccountService.Current.Type == AccountType.ThirdParty &&
                !string.IsNullOrEmpty(AccountService.Current.AuthServerUrl))
            {
                try
                {
                    Report("正在启动游戏...", "准备第三方登录认证", 25);

                    var jarPath = await AuthlibInjectorService.EnsureJarAsync(log);

                    // 检查文件大小，异常则重新下载
                    var jarInfo = new FileInfo(jarPath);
                    if (!jarInfo.Exists || jarInfo.Length < 500_000)
                    {
                        log($"[Authlib] ⚠️ jar 文件异常（{jarInfo.Length} 字节），尝试重新下载");
                        try { File.Delete(jarPath); } catch { }
                        jarPath = await AuthlibInjectorService.EnsureJarAsync(log);
                    }

                    log($"[Authlib] jar 大小: {new FileInfo(jarPath).Length / 1024} KB");

                    // 🚨 用 Add 追加，不用 Insert(0)，保证其他 JVM 参数顺序正确
                    jvmArgs.Add(new MArgument($"-javaagent:{jarPath}={AccountService.Current.AuthServerUrl}"));
                    log($"[Authlib] 已注入: -javaagent:{jarPath}={AccountService.Current.AuthServerUrl}");
                    log($"[Authlib] 认证服务器: {AccountService.Current.AuthServerUrl}");
                }
                catch (Exception ex)
                {
                    log($"[Authlib] ⚠️ 注入失败: {ex.Message}");
                }
            }

            // 9. 构造启动选项
            var extraGameArgs = new List<MArgument>
            {
                new MArgument("--gameDir"),
                new MArgument(gameDir)
            };

            var option = new MLaunchOption
            {
                Session = session,
                MaximumRamMb = finalRam,
                JavaPath = javaPath,
                JvmArgumentOverrides = jvmArgs.ToArray(),
                ExtraGameArguments = extraGameArgs.ToArray()
            };

            // 10. 构建启动进程
            cancellationToken.ThrowIfCancellationRequested();
            Report("正在准备游戏资源...", "检查游戏文件完整性（如缺资源会自动下载）", 50, indeterminate: true);

            var launcher = new MinecraftLauncher(new MinecraftPath(minecraftPath));
            log("正在构建启动进程...");
            var process = await launcher.BuildProcessAsync(versionName, option);

            if (cancellationToken.IsCancellationRequested)
            {
                log("⚠️ 用户在构建完成后取消，未启动游戏进程");
                throw new OperationCanceledException(cancellationToken);
            }

            Report("正在启动 JVM...", "启动游戏进程", 90);

            log("========== 完整启动命令 ==========");
            log($"EXE: {process.StartInfo.FileName}");
            log($"CWD: {process.StartInfo.WorkingDirectory}");
            log($"ARGS: {process.StartInfo.Arguments}");
            log("==================================");

            process.Start();
            log($"✅ 游戏进程已启动，PID: {process.Id}");

            // 11. 等待游戏窗口
            Report("正在等待游戏窗口...", "游戏正在加载，请稍候", 95, indeterminate: true);
            try
            {
                await WaitForGameWindowAsync(process, cancellationToken, log);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(true);
                        log("🛑 用户取消，已终止游戏进程");
                    }
                }
                catch (Exception ex)
                {
                    log($"⚠️ 终止进程失败: {ex.Message}");
                }
                throw;
            }

            log("========== 启动流程结束 ==========");
            Report("✅ 游戏已启动", $"PID: {process.Id}", 100);
            return process;
        }

        private static async Task WaitForGameWindowAsync(
            Process process,
            CancellationToken cancellationToken,
            Action<string> log)
        {
            var timeout = DateTime.Now.AddSeconds(120);
            while (DateTime.Now < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (process.HasExited)
                    {
                        throw new Exception($"游戏进程已退出（ExitCode: {process.ExitCode}），可能启动失败，请检查游戏日志。");
                    }

                    process.Refresh();
                    if (process.MainWindowHandle != IntPtr.Zero)
                    {
                        log($"✅ 检测到游戏窗口 (Handle: {process.MainWindowHandle})");
                        await Task.Delay(500, cancellationToken);
                        return;
                    }
                }
                catch (InvalidOperationException)
                {
                    throw new Exception("游戏进程异常退出，请检查游戏日志。");
                }

                await Task.Delay(400, cancellationToken);
            }

            log("⚠️ 等待游戏窗口超时，但进程仍在运行");
        }
    }
}