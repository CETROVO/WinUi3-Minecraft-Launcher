# WML 启动器 (WinUi3-Minecraft-Launcher)

> ⚠️ **免责声明**：本启动器 100% 由 AI 编写，无人工痕迹。  
> 如果遇到 Bug，那是 AI 的锅，与我（学生，主修原神，副业修 Bug）无关。  
> 如果你想要纯正人类编写的启动器，请出门左转 [HMCL](https://github.com/HMCL-dev/HMCL) 或 [PCL CE](https://github.com/PCL-community/PCL-CE)。
>
> 本启动器仓库**不会实时更新**，只会在每个版本发布时同步更新一次仓库。

一个基于 **WinUI 3 + C# + .NET 10** 的 Minecraft 启动器，底层使用 [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core)。  
目标是做一个界面现代化、功能完整、并且未来会使用 EasyTier 制作私有化联机模块的新一代启动器。

---

## ✨ 特性 / Features

### 🚀 已实现
- [x] **极简现代 UI**：WinUi3风格自定义标题栏（亚克力毛玻璃 + 主题自适应）
- [x] **PCL 风格导航**：经典四大导航（启动 / 下载 / 管理 / 更多）
- [x] **多实例管理**：多 `.minecraft` 文件夹管理（添加 / 切换 / 重命名 / 删除）
- [x] **版本解析**：真实版本列表读取（自动过滤缺 `.json` / `.jar` 的无效目录）
- [x] **独家 Java 面板**：一键下载 Adoptium 官方 JRE / JDK，并自动配置环境
- [x] **全加载器制霸**：一键自动下载并静默安装原版 / Forge / NeoForge / Quilt / Fabric / OptiFine
- [x] **资源高速下载**：内置 Modrinth 镜像搜索，一键下载模组 / 资源包 / 光影包，自动补全前置依赖
- [x] **账户支持**：离线账户、微软正版 OAuth2 登录、第三方认证（authlib-injector / LittleSkin 等）
- [x] **内存智能分配**：PCL 同款渐进式动态内存分配算法
- [x] **Java 智能匹配**：Java 自动探测 + 按版本 `javaVersion` 字段精准适配
- [x] **全局下载源策略**：支持官方源 / BMCLAPI 镜像源智能切换与重试

### 🛠️ 开发中 / 计划中
- [ ] **内置联机模块**：基于 EasyTier 的私有化开黑联机
- [ ] **整合包支持**：Modrinth / CurseForge 整合包一键安装
- [ ] **UI 细节打磨**：更多丝滑的过渡动画与 Acrylic 材质适配

---

## 🛠️ 技术栈 / Tech Stack

| 层级 | 技术 |
|---|---|
| **UI 框架** | WinUI 3 (Windows App SDK) |
| **语言** | C# / .NET 8 / .NET 10 |
| **启动核心** | [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core) |
| **目标平台** | Windows 10 1809+ / Windows 11 |
| **开发环境** | Visual Studio 2022 / 2026 (Preview) |

---

## 🚧 开发状态 / Status

目前处于 **v0.2.0-beta** 测试阶段。核心启动引擎、加载器安装、资源下载已全面可用。正在收集社区反馈，并着手准备 EasyTier 联机模块的接入。

---

## 🐛 已知问题 / Known Issues

- Java 版本自动匹配在部分极其特殊的环境下仍会挑错版本，此时建议手动在设置里指定 `javaw.exe`。
- 中文路径下启动某些远古版本的 Forge 可能会报 `UnsupportedClassVersionError`。
- 安装包由于未购买企业级代码签名，首次运行可能会被 Windows Defender SmartScreen 拦截（请点击“更多信息 -> 仍要运行”）。

---

## 📜 License

本项目采用 [GPL-3.0](LICENSE) 协议开源，禁止任何闭源商业封装。

### 第三方组件致谢
- [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core) - MIT
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) - MIT
- [Modrinth API](https://docs.modrinth.com/) - 开源生态基石
- [BMCLAPI](https://bmclapidoc.bangbang93.com/) - 国内极速镜像
- [EasyTier](https://github.com/EasyTier/EasyTier) - LGPL-3.0（计划中）

---

## 🙏 致谢 / Credits

- [PCL2](https://github.com/Hex-Dragon/PCL2) - UI 设计灵感 & 动态内存算法参考
- [HMCL](https://github.com/HMCL-dev/HMCL) - 启动器功能参照与踩坑避雷
- [CmlLib.Core](https://github.com/CmlLib/CmlLib.Core) - 强大的底层启动库

---

*Crafted in the "D盘装原神" era. 提瓦特与方块世界，我全都要。*
