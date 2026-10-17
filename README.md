# WML 启动器 (WinUi3-Minecraft-Launcher)

> 本启动器 100% 由 AI 编写，无人工痕迹。  
> 如果遇到 Bug，那是 AI 的锅，与我（学生，主修原神）无关。  
> 如果你想要人写的启动器，请出门左转 [HMCL](https://github.com/HMCL-dev/HMCL) [PCL](https://github.com/Meloong-Git/PCL) [PCL CE](https://github.com/PCL-community/PCL-CE)。

一个基于 **WinUI 3 + C# + .NET 10** 的 Minecraft 启动器，底层使用 [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core)。  
目标是做一个界面现代化、功能完整、使用 EasyTier 制作的私有化联机模块的新一代启动器。

---

## ✨ 特性 / Features

### 已实现
- [x] WinUi3 风格自定义标题栏（亚克力毛玻璃 + 主题自适应）
- [x] PCL CE 风格四大导航（启动 / 下载 / 管理 / 更多）
- [x] 多 `.minecraft` 文件夹管理（添加 / 切换 / 重命名 / 删除）
- [x] **全局状态持久化**（账号、设置、当前版本重启自动恢复）
- [x] **账户系统**
  - [x] 离线账户登录
  - [x] 第三方认证登录（原生 authlib-injector 注入）
  - [x] 第三方多角色选择
  - [x] LittleSkin 等皮肤站头像拉取与渲染
- [x] **启动引擎**
  - [x] 真实版本列表读取与智能版本隔离
  - [x] Java 自动探测 + 强制 `java.exe` 启动（防闪退）
  - [x] PCL 同款渐进式动态内存分配算法
  - [x] 崩溃日志静默捕获（输出至 `C:\MinecraftTemp\last_crash.log`）
- [x] **下载系统**
  - [x] 在线版本列表获取（正式版 / 预览版 / 愚人节版本分类）
  - [x] 原版游戏极速下载
  - [x] **模组加载器一键安装**
    - [x] Fabric (支持联动勾选 Fabric API)
    - [x] Quilt
    - [x] Forge (调用官方 Installer 静默安装)
    - [x] NeoForge (调用官方 Installer 静默安装)
    - [x] OptiFine (自动下载并唤起官方 GUI 安装器)
  - [x] 自定义实例名称（下载前可直接重命名文件夹）

### 开发中
- [ ] 微软正版登录（OAuth2 流程）
- [ ] 资源下载（模组 / 资源包 / 光影包 / 数据包 / 整合包）
- [ ] 多线程并发下载引擎（目前使用 CmlLib 默认单线程）
- [ ] 内置联机（EasyTier 模块）

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

核心功能已可用！UI 骨架、账户认证、启动引擎以及核心下载/安装模块已全部打通。目前正在着手开发资源搜索下载模块，并准备重构极速下载引擎。

---

## 🐛 已知问题 / Known Issues

- **OptiFine**：受官方限制，无法做到完全静默安装，下载后需用户在弹出的安装器窗口中手动点击 `Install`。
- **资源下载**：目前暂未加入模组、资源包等资源的在线搜索与下载功能（开发中）。
- **下载速度**：目前原版下载使用 CmlLib 默认下载器，速度受制于网络单线程限制。

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
