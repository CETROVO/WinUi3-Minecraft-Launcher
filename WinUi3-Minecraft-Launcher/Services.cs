using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using CmlLib.Core;
using CmlLib.Core.Auth;
using CmlLib.Core.ProcessBuilder;

namespace WinUi3_Minecraft_Launcher
{
    // ================= 账户类型 =================
    public enum AccountType { Offline, Microsoft, ThirdParty }

    public class AccountInfo
    {
        public AccountType Type { get; set; } = AccountType.Offline;
        public string Username { get; set; } = "Steve";
        public string Uuid { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string AuthServerUrl { get; set; } = "";
    }

    public static class AccountService
    {
        public static AccountInfo Current { get; set; } = new AccountInfo();

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

    // ================= 全局启动状态 =================
    public static class LauncherState
    {
        public static string? CurrentMinecraftPath { get; set; }
        public static string? CurrentVersionName { get; set; }
        public static string? JavaPath { get; set; }
        public static int MaxRamMb { get; set; } = 0; // 0 = 自动动态分配
        public static bool AutoMemory { get; set; } = true;
    }

    // ================= Java 探测 =================
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
            @"D:\Java",
            @"D:\Program Files\Java",
            @"D:\Program Files\Eclipse Adoptium"
        };

        /// <summary>
        /// 盲扫：找第一个能用的 javaw.exe
        /// </summary>
        public static string? FindJava()
        {
            if (!string.IsNullOrEmpty(LauncherState.JavaPath) && File.Exists(LauncherState.JavaPath))
                return LauncherState.JavaPath;

            var roots = SearchRoots.ToList();
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome)) roots.Add(javaHome);

            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                try
                {
                    var javaw = Directory.GetFiles(root, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (javaw != null) return javaw;
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// 按 Java 大版本号（17、21、25）查找 javaw.exe
        /// </summary>
        public static string? FindJavaByMajorVersion(int majorVersion)
        {
            var roots = SearchRoots.ToList();
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome)) roots.Add(javaHome);

            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                try
                {
                    var dirs = Directory.GetDirectories(root);
                    var matched = dirs.FirstOrDefault(d =>
                    {
                        var name = Path.GetFileName(d);
                        return name.StartsWith($"jdk-{majorVersion}.") ||
                               name.StartsWith($"jre-{majorVersion}.") ||
                               name.StartsWith($"jdk{majorVersion}") ||
                               name.StartsWith($"jre{majorVersion}") ||
                               name == $"jdk-{majorVersion}" ||
                               name == $"jre-{majorVersion}";
                    });

                    if (matched != null)
                    {
                        var javaw = Directory.GetFiles(matched, "javaw.exe", SearchOption.AllDirectories).FirstOrDefault();
                        if (javaw != null) return javaw;
                    }
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// 粗略判断 Java 是否为 64 位
        /// </summary>
        public static bool Is64BitJava(string javaPath)
        {
            try
            {
                if (string.IsNullOrEmpty(javaPath) || !File.Exists(javaPath)) return true;

                var binDir = Path.GetDirectoryName(javaPath);
                var homeDir = Directory.GetParent(binDir ?? "")?.FullName ?? "";
                if (homeDir.Contains("Program Files (x86)")) return false;

                // 读 release 文件里的 OS_ARCH（如果有）
                var releaseFile = Path.Combine(homeDir, "release");
                if (File.Exists(releaseFile))
                {
                    var archLine = File.ReadAllLines(releaseFile)
                        .FirstOrDefault(l => l.StartsWith("OS_ARCH="));
                    if (archLine != null && archLine.Contains("x86") && !archLine.Contains("x86_64") && !archLine.Contains("amd64"))
                        return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// 读 javaw.exe 对应的大版本号（通过 release 文件）
        /// </summary>
        public static int GetJavaMajorVersion(string javawPath)
        {
            try
            {
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

    // ================= 动态内存分配（PCL 同款渐进式策略） =================
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
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                    return memStatus.ullAvailPhys / 1024.0 / 1024.0 / 1024.0;
            }
            catch { }
            return 4.0;
        }

        public static double GetTotalPhysicalMemoryGB()
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus))
                    return memStatus.ullTotalPhys / 1024.0 / 1024.0 / 1024.0;
            }
            catch { }
            return 8.0;
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

            double availableRam = GetAvailablePhysicalMemoryGB();

            double allocated = 0;
            double remaining = availableRam;

            double phase1 = Math.Min(remaining, ramTarget1);
            allocated += phase1;
            remaining -= phase1;

            if (remaining > 0 && allocated < ramTarget2)
            {
                double phase2 = Math.Min(remaining * 0.7, ramTarget2 - allocated);
                allocated += phase2;
                remaining -= phase2;
            }

            if (remaining > 0 && allocated < ramTarget3)
            {
                double phase3 = Math.Min(remaining * 0.4, ramTarget3 - allocated);
                allocated += phase3;
                remaining -= phase3;
            }

            if (remaining > 0)
                allocated += remaining * 0.15;

            allocated = Math.Max(allocated, ramMinimum);
            allocated = Math.Min(allocated, availableRam * 0.8);

            int resultMb = (int)(allocated * 1024);
            resultMb = (resultMb / 32) * 32;
            return resultMb;
        }
    }

    // ================= 启动封装 =================
    public static class GameLauncher
    {
        public static async Task<System.Diagnostics.Process> LaunchAsync(
            string minecraftPath,
            string versionName,
            int maxRamMb,
            Action<string> log)
        {
            log($"========== 启动流程开始 ==========");
            log($"游戏路径: {minecraftPath}");
            log($"目标版本: {versionName}");

            var path = new MinecraftPath(minecraftPath);
            var launcher = new MinecraftLauncher(path);

            // ========== 1. 账户 ==========
            var session = AccountService.GetSession();
            log($"账户: {session.Username} ({AccountService.GetTypeDisplayName()})");

            // ========== 2. Java 智能匹配 ==========
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
                log($"读取版本 Java 要求失败: {ex.Message}");
            }

            if (requiredJavaMajor > 0)
                log($"版本要求 Java {requiredJavaMajor}");

            string? javaPath = null;

            // 优先级 1：手动指定，且版本号匹配
            if (!string.IsNullOrEmpty(LauncherState.JavaPath) && File.Exists(LauncherState.JavaPath))
            {
                var manualMajor = JavaFinder.GetJavaMajorVersion(LauncherState.JavaPath);
                if (requiredJavaMajor == 0 || manualMajor == requiredJavaMajor)
                {
                    javaPath = LauncherState.JavaPath;
                    log($"使用手动指定的 Java {manualMajor}: {javaPath}");
                }
                else
                {
                    log($"⚠️ 手动指定的 Java {manualMajor} 与版本要求 {requiredJavaMajor} 不匹配，尝试自动查找");
                }
            }

            // 优先级 2：按版本要求自动找
            if (javaPath == null && requiredJavaMajor > 0)
            {
                javaPath = JavaFinder.FindJavaByMajorVersion(requiredJavaMajor);
                if (javaPath != null)
                    log($"✅ 自动匹配到 Java {requiredJavaMajor}: {javaPath}");
            }

            // 优先级 3：盲扫兜底
            if (javaPath == null)
            {
                javaPath = JavaFinder.FindJava();
                if (javaPath != null)
                    log($"⚠️ 使用兜底 Java: {javaPath}");
            }

            if (javaPath == null)
                throw new Exception($"找不到可用的 Java。此版本需要 Java {requiredJavaMajor}，请安装后到设置里手动指定。");

            log($"Java 路径: {javaPath}");

            // ========== 3. 内存分配 ==========
            bool is64Bit = JavaFinder.Is64BitJava(javaPath);
            log($"Java 架构: {(is64Bit ? "64位" : "32位")}");

            int finalRam;
            if (!is64Bit)
            {
                finalRam = 1024;
                log("⚠️ 32位 Java，强制限制内存为 1024MB");
            }
            else if (maxRamMb <= 0 || LauncherState.AutoMemory)
            {
                var versionDir = Path.Combine(minecraftPath, "versions", versionName);
                finalRam = MemoryAllocator.CalculateAutoMemoryMb(versionDir, is64Bit);
                log($"自动内存分配: {finalRam}MB");
            }
            else
            {
                finalRam = maxRamMb;
                log($"手动内存分配: {finalRam}MB");
            }

            // ========== 4. 纯英文临时目录 ==========
            var tempDir = @"C:\MinecraftTemp";
            if (!Directory.Exists(tempDir))
            {
                try { Directory.CreateDirectory(tempDir); }
                catch { tempDir = Path.GetTempPath(); }
            }
            log($"临时目录: {tempDir}");

            // ========== 5. JVM 参数 ==========
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

            var option = new MLaunchOption
            {
                Session = session,
                MaximumRamMb = finalRam,
                JavaPath = javaPath,
                JvmArgumentOverrides = jvmArgs.ToArray()
            };

            log("JVM 参数:");
            foreach (var arg in jvmArgs)
                log($"  {arg}");

            // ========== 6. 构建启动进程 ==========
            log("正在构建启动进程...");
            System.Diagnostics.Process process;
            try
            {
                process = await launcher.BuildProcessAsync(versionName, option);
            }
            catch (Exception ex)
            {
                log($"❌ 构建进程失败: {ex.Message}");
                throw;
            }

            // ========== 7. 输出完整启动命令 ==========
            log("========== 启动命令 ==========");
            log($"可执行文件: {process.StartInfo.FileName}");
            log($"工作目录: {process.StartInfo.WorkingDirectory}");
            log($"参数: {process.StartInfo.Arguments}");
            log("==============================");

            // ========== 8. 启动 ==========
            log("启动游戏进程...");
            try
            {
                process.Start();
                log($"✅ 游戏已启动，PID: {process.Id}");
            }
            catch (Exception ex)
            {
                log($"❌ 启动失败: {ex.Message}");
                throw;
            }

            log("========== 启动流程结束 ==========");
            return process;
        }
    }
}