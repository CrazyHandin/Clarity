版本：beta0.01。历史功能阶段保留，日常使用请选择 beta0.08。

[简体中文](README.md) | [English](README.en.md)


# 澄净 · C 盘清理工具 beta0.01

便携式 Windows 桌面程序。根据本次 C 盘清理经历整理识别规则，提供简洁一键清理和高级自选清理。界面为中文，绿色与浅灰配色，不联网、不发送文件信息、不安装服务或驱动。

## 开始使用

1. 将便携包解压到任意文件夹，双击 `ClearSpace.exe`。
2. 启动后自动只读扫描；扫描本身不会删除文件。
3. 简洁模式：点击“一键清理”，处理符合条件的可重建缓存。
4. 高级模式：勾选所需项目，查看右侧的说明、影响、文件位置和保留条件，点击“清理已选”，确认后执行。
5. 需要处理系统目录时，点击左下角“以管理员身份重新打开”，允许 Windows 权限确认，然后重新扫描。使用当前账户提权；换用另一管理员账户启动会识别另一账户的缓存。
6. 清理后更新磁盘可用空间，并可导出逐文件清理记录。记录包含已删除与跳过的文件及原因。

## 两种模式的范围

### 简洁模式

| 类型 | 处理范围 | 保留条件 |
| --- | --- | --- |
| VS Code 扩展安装包 | CachedExtensionVSIXs | 保留最近 7 天修改的文件；VS Code 运行时保留整项 |
| VS Code 编译和图形缓存 | CachedData、Cache、Code Cache、GPUCache、Dawn 缓存 | 保留最近 1 天修改的文件 |
| pip / npm 下载缓存 | pip/cache、npm-cache/_cacache 与 _logs | 保留最近 7 天；相关 Python、Node.js 或包管理器运行时保留整项 |
| Edge、Chrome、夸克 | Default / Profile 目录下的 Cache、Code Cache、图形缓存 | 保留最近 1 天；浏览器运行时保留整项 |
| Steam、Epic | 指定网页资源缓存子目录 | 保留最近 1 天；相关程序运行时保留整项 |

一键模式只处理以上已知缓存目录，采用保守保留期。密码、Cookie、浏览历史、书签、聊天数据、文档、存档、已安装软件与依赖都不在处理范围。没有符合条件的文件时，按钮不可用。

### 高级模式

| 类型 | 最小保留期 | 清理影响 / 需要确认 |
| --- | --- | --- |
| NVIDIA DXCache / GLCache / ComputeCache 等 | 1 天 | 游戏和渲染程序需要重新编译；首次运行可能变慢或短暂卡顿。先关闭游戏和渲染软件。识别出的已知相关进程运行时会保留；程序无法识别所有游戏名称。 |
| QQ 音乐播放缓存 | 7 天 | 部分歌曲、歌词或图片需要重新下载；建议优先使用 QQ 音乐内置清理功能。 |
| 用户 Temp / Windows Temp | 7 天 | 请先保存工作，关闭软件，确认没有安装或更新任务。临时工作资料可能具有保留价值。 |
| 应用 CrashDumps / 系统 MEMORY.DMP | 1 天 | 失去对应历史崩溃的诊断资料；系统文件通常需要管理员权限。 |
| 微信、Epic、VS Code、WPS、OneDrive 的独立诊断日志 | 14 天 | 失去旧诊断记录，不清理聊天或同步文件。 |
| 完美平台、Unity Hub、OCS Desktop 的更新安装包 | 7 天 | 只处理已知 updater 目录中允许的安装包类型。必须确认更新已经完成；pending 目录不能证明更新完成。 |
| NVIDIA 驱动下载资料 | 14 天 | 限定 ota-artifacts 的安装包；确认驱动安装成功、不需要本地备份且没有更新任务。 |

高级项目默认不勾选，执行前显示清理范围和永久删除确认。符合保留期只是文件候选条件，不能据此判断更新任务已经完成。

### 只展示、保留不删

- Office 更新资料：可能用于安装、修复或回退，不提供直接删除。
- 原厂 eSupport 驱动安装备份。
- hiberfil.sys 休眠文件、pagefile.sys 虚拟内存文件。
- Windows 组件库、Installer、DriverStore、Recovery 不在删除规则中。
- 其他未纳入规则的软件目录不被自动归类为垃圾。
- 回收站和个人下载目录不提供一键清空。

## 文件保护机制

- 只清理识别规则中的明确目录或单个文件，不提供任意目录递归删除。
- 跳过目录链接、文件链接、硬链接、云端占位文件、只读文件和受权限保护的文件。
- 扫描后再次核对文件实际位置、身份、大小和修改时间；变化的文件跳过，需重新扫描。
- 删除时使用独占写入/删除的文件句柄，正在占用的文件跳过，不强制关闭程序、不更改文件权限。
- 清理开始时及过程中定期检查相关进程（约每 250 毫秒刷新），发现相关程序启动后，后续文件保留；文件占用检查仍在每次删除时执行。
- 不删除缓存目录本身，只删除符合条件的缓存文件；空目录保留。
- 可以停止当前任务；已完成的删除不会回滚，尚未处理的文件保留。
- 估算容量按文件逻辑大小统计；压缩、稀疏文件和后台写入可能让磁盘实际可用空间变化与估算不同。

## 跨电脑使用

- 支持 Windows 10 / Windows 11，需 .NET Framework 4.8 或更高版本。新版本 Windows 通常自带；无需 Python、Node.js 或另外的 .NET SDK。
- 程序为 AnyCPU，可由 Windows 上对应的 .NET Framework 运行；验证环境为当前 Windows x64 电脑，未实际验证其他硬件。
- 自动读取当前用户与 Windows 的标准目录，不绑定 `13447` 等用户名。
- 默认只识别 C 盘中的已知目录；没有 C 盘时使用 Windows 所在盘。用户资料转移到其他盘时，不清理其他盘的缓存。
- 复制 `ClearSpace.exe` 与 `ClearSpace.exe.config` 即可使用。无需安装；关闭程序后即可移动或删除程序文件。
- 清理日志存于 `%LOCALAPPDATA%\ClearSpace\Logs`。日志包含本地文件路径，保存和导出由当前电脑管理。
- 代码签名状态：未签名。附带 SHA-256 校验值与源码。

## 验证

完成 24 项文件安全检查和 12 项界面操作检查，测试只修改新建的隔离模拟文件：保留期、运行进程、占用、只读、修改/替换、硬链接、目录链接、越界、取消、系统单文件删除边界、两种模式和真实按钮执行流程均通过。

另已对当前电脑进行只读扫描，并渲染检查简洁、高级和最小窗口尺寸。制作工具期间没有运行真实缓存清理。

## 源码构建

附带 C# / WPF 源码与 `build.ps1`。使用 Windows 自带的 .NET Framework C# 编译器，无需联网下载包。

```powershell
.\build.ps1 -OutputDirectory .\dist
```

隔离测试（仅生成和处理指定测试目录下的新文件）：

```powershell
.\dist\ClearSpace.exe --self-test .\verification
.\dist\ClearSpace.exe --ui-self-test .\ui-verification
```

只读扫描报告（不清理）：

```powershell
.\dist\ClearSpace.exe --scan-report .\scan-report.json
```

## 参考

- NVIDIA 对着色器缓存重建和清理的说明：https://nvidia.custhelp.com/app/answers/detail/a_id/5735/
- VS Code 官方扩展安装包缓存测试记录：https://github.com/microsoft/vscode/issues/99873
- Microsoft .NET Framework 版本与 Windows 依赖：https://learn.microsoft.com/en-us/dotnet/framework/install/versions-and-dependencies

版本日期：2026-10-13。
