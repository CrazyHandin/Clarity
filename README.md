# Clarity

**简体中文** | [English](README.en.md)

Clarity（澄净）是一款 Windows C 盘清理工具，提供安装版与便携版，支持系统垃圾、常用软件缓存清理及备份恢复。

[下载最新版](https://github.com/CrazyHandin/Clarity/releases/latest) · [使用说明](docs/USAGE.txt) · [更新记录](docs/版本历史.md)

## 主要功能

- **分级清理**：简洁模式处理符合条件的低风险项目；高级模式支持自选，展示项目说明与清理风险。
- **两种扫描**：快速扫描仅使用内置规则；常规扫描增加应用识别、外部规则与未知缓存发现。本地缓存减少再次扫描的重复识别，启动时不会自动扫描。
- **扩展规则**：默认启用 Winapp2，可与 JSON 同时使用，支持多个 JSON 文件导入、独立启停、预览、升级与回退。
- **备份恢复**：默认先备份，支持恢复及清理记录查看；成功恢复后自动移除对应备份。
- **界面与更新**：支持中英文、深浅色、跟随 Windows 主题及液态玻璃外观样式，可在“关于”中检查并下载软件更新。

## 开始使用

需要 **Windows 10/11 与 .NET Framework 4.8**。

1. 下载安装包，或完整解压便携包后运行 `Clarity.exe`。
2. 选择扫描模式与范围，点击 **开始扫描**。
3. 查看结果和风险说明，使用简洁清理或高级自选。备份与规则在 **设置与备份** 中管理。

缓存清理可能导致重新下载或下次启动变慢。未知缓存与社区规则默认不选，仅供高级自选且先备份。

默认备份目录为 `ClarityBackups`；备份位于 C 盘时，需清除备份后才释放对应空间。**Windows 磁盘清理**为独立可选项，不经过 Clarity 备份。

## 文档与构建

[JSON 规则示例](rules/RulePack.template.json) · [Winapp2 说明](docs/Winapp2-使用说明.md) · [构建说明](docs/BUILD.md)

使用 C# / WPF 与 .NET Framework 编译器构建：

```powershell
.\build.ps1 -OutputDirectory .\dist
```

## 许可

Clarity 采用 [MIT 许可](LICENSE.txt)。Winapp2 规则采用独立的 **CC BY-SA 4.0** 许可，详见[第三方说明](THIRD_PARTY_NOTICES.md)。
