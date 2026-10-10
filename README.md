# WML 启动器 (WinUi3-Minecraft-Launcher)

> 本启动器 100% 由 AI 编写，无人工痕迹。  
> 如果遇到 Bug，那是 AI 的锅，与我（学生，主修原神）无关。  
> 如果你想要人写的启动器，请出门左转 [HMCL](https://github.com/HMCL-dev/HMCL)。

一个基于 **WinUI 3 + C# + .NET 10** 的 Minecraft 启动器，底层使用 [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core)。  
目标是做一个界面现代化、功能完整、支持 EasyTier 私有化联机模块的新一代启动器。

---

## ✨ 特性 / Features

### 已实现
- [x] 图吧工具箱风格自定义标题栏（亚克力毛玻璃 + 主题自适应）
- [x] PCL CE 风格四大导航（启动 / 下载 / 管理 / 更多）
- [x] 多 `.minecraft` 文件夹管理（添加 / 切换 / 重命名 / 删除）
- [x] 真实版本列表读取（自动过滤缺 `.json` / `.jar` 的无效目录）
- [x] 版本选择 / 重命名 / 删除
- [x] 离线账户登录
- [x] PCL 同款渐进式动态内存分配算法
- [x] Java 自动探测 + 按版本 `javaVersion` 智能匹配

### 开发中
- [ ] 微软正版登录（OAuth2 流程）
- [ ] 第三方认证登录（authlib-injector）
- [ ] 下载 Minecraft 原版 / 模组加载器（Forge / Fabric / NeoForge）
- [ ] 下载模组 / 资源包 / 光影包 / 数据包 / 整合包
- [ ] 内置联机（陶瓦 / EasyTier）

---

## 🖼️ 截图 / Screenshot

![WML Launcher](docs/screenshot.png)

> 深色模式 + 亚克力毛玻璃 + PCL CE 风格布局

---

## 🛠️ 技术栈 / Tech Stack

| 层级 | 技术 |
|---|---|
| UI 框架 | WinUI 3 (Windows App SDK) |
| 语言 | C# / .NET 10 |
| 启动核心 | [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core) |
| 目标平台 | Windows 10 1809+ / Windows 11 |
| 开发环境 | Visual Studio 2026 |

---

## 🚧 开发状态 / Status

早期开发中。UI 骨架和启动引擎已可用，正在攻克 Java 版本匹配与资源下载模块。

---

## 🐛 已知问题 / Known Issues

- Java 版本自动匹配在部分环境下仍会挑错版本，需要手动在设置里指定 `javaw.exe`
- 中文路径下启动部分版本会报 `UnsupportedClassVersionError`

---

## 📜 License

本项目采用 [GPL-3.0](LICENSE) 协议。

### 第三方组件

- [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core) - MIT
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) - MIT
- [EasyTier](https://github.com/EasyTier/EasyTier) - LGPL-3.0（计划中，独立进程调用）

---

## 🙏 致谢 / Credits

- [PCL2](https://github.com/Hex-Dragon/PCL2) - UI 设计灵感 & 动态内存算法参考
- [HMCL](https://github.com/HMCL-dev/HMCL) - 启动器功能参照
- [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core) - 核心启动库

---

*Crafted in the "D盘装原神" era.*
