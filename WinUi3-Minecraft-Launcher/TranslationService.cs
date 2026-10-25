using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinUi3_Minecraft_Launcher
{
    public static class TranslationService
    {
        // 本地存放路径：C:\Users\你的用户名\AppData\Local\WML\mod_translation.json
        private static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WML");
        private static readonly string LocalJsonPath = Path.Combine(CacheDir, "mod_translation.json");

        public static Dictionary<string, string> Dictionary { get; private set; } = new(StringComparer.OrdinalIgnoreCase);
        private static Timer? _updateTimer;

        // ================= 首次打开写入的初始全量词库 =================
        private static readonly Dictionary<string, string> DefaultDictionary = new(StringComparer.OrdinalIgnoreCase)
        {
            { "钠", "sodium" }, { "锂", "lithium" }, { "磷", "phosphor" }, { "氪", "krypton" }, { "氙", "xenon" }, { "铷", "rubidium" },
            { "机械动力", "create" }, { "天境", "aether" }, { "暮色森林", "twilight forest" }, { "格雷科技", "gregtech" },
            { "工业时代2", "industrialcraft" }, { "工业时代", "industrialcraft" }, { "幸运方块", "lucky block" },
            { "虚无世界", "advent of ascension" }, { "冰火传说", "ice and fire" }, { "超多生物群系", "biomes o plenty" },
            { "通用机械", "mekanism" }, { "植物魔法", "botania" }, { "匠魂", "tinkers construct" }, { "降魂", "tinkers construct" },
            { "沉浸工程", "immersive engineering" }, { "无尽贪婪", "avaritia" }, { "玉", "jade" }, { "旅行地图", "journeymap" },
            { "区块加载", "chunky" }, { "苹果皮", "apple skin" }, { "小地图", "xaero" }, { "光影", "shader" }, { "资源包", "resourcepack" },
            { "农夫乐事", "farmers delight" }, { "潘马斯农场", "pams harvestcraft" }, { "热力膨胀", "thermal expansion" },
            { "建筑", "buildcraft" }, { "星系", "galacticraft" }, { "等价交换", "projecte" }, { "神秘时代", "thaumcraft" },
            { "血魔法", "blood magic" }, { "深渊国度", "abyssalcraft" }, { "林业", "forestry" }, { "电脑", "computercraft" },
            { "应用能源2", "applied energistics 2" }, { "AE2", "applied energistics 2" }, { "精致存储", "refined storage" },
            { "RS", "refined storage" }, { "JEI", "jei" }, { "物品管理器", "jei" }, { "检测器", "wthit" }, { "环境音乐", "ambientsounds" },
            { "动态光源", "dynamic lights" }, { "高清修复", "optifine" }, { "星辉魔法", "astral sorcery" }, { "星辉", "astral sorcery" },
            { "拔刀剑", "slashblade" }, { "塞尔达", "zeldacraft" }, { "原神", "genshin" }
        };

        public static async Task InitializeAsync()
        {
            try
            {
                Directory.CreateDirectory(CacheDir);

                // 1. 首次打开：如果本地没有词典文件，把自带的词库写进去
                if (!File.Exists(LocalJsonPath))
                {
                    var defaultJson = JsonSerializer.Serialize(DefaultDictionary, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(LocalJsonPath, defaultJson);
                }

                // 2. 加载本地文件进内存
                LoadLocalDictionary();

                // 3. 启动定时器：每隔 20 分钟重新读取一次本地的 JSON 文件（实现热重载）
                // 这样你改了本地 JSON 加点新词，不用重启启动器，20分钟内就会自动生效
                _updateTimer = new Timer(_ =>
                {
                    LoadLocalDictionary();
                }, null, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(20));
            }
            catch { /* 忽略异常 */ }
        }

        /// <summary>
        /// 从本地文件加载词典到内存
        /// </summary>
        private static void LoadLocalDictionary()
        {
            try
            {
                if (!File.Exists(LocalJsonPath)) return;

                var localJson = File.ReadAllText(LocalJsonPath);
                var localDict = JsonSerializer.Deserialize<Dictionary<string, string>>(localJson);

                if (localDict != null && localDict.Count > 0)
                {
                    var newDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in localDict) newDict[kvp.Key] = kvp.Value;
                    Dictionary = newDict; // 原子替换
                }
            }
            catch { }
        }

        /// <summary>
        /// 智能翻译中文查询
        /// </summary>
        public static string TranslateQuery(string query)
        {
            string result = query;
            var sortedKeys = Dictionary.Keys.OrderByDescending(k => k.Length).ToList();

            foreach (var key in sortedKeys)
            {
                if (result.Contains(key))
                {
                    result = result.Replace(key, $" {Dictionary[key]} ");
                }
            }
            return result.Trim();
        }
    }
}