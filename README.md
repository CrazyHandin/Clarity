版本：beta0.02。历史功能阶段保留，日常使用请选择 beta0.08。

[简体中文](README.md) | [English](README.en.md)


# 澄净 · C 盘清理 beta0.02 — by CraZyX

便携式 Windows 桌面程序。主色为 #39C5BB，中文界面，本地运行、不联网。提供简洁一键清理和高级自选清理，识别规则扩展为 8 类、68 条，适配当前电脑的用户与标准路径。

## 开始使用

1. 解压便携包，双击 ClearSpace.exe。打开软件不会自动扫描、不会删除文件，只显示当前磁盘空间。
2. 在启动页勾选要检查的分类，点击醒目的“开始扫描”。勾选扫描分类只决定检查范围，不代表同意删除。
3. 扫描完成后，简洁模式可一键处理符合条件的低风险、可重建缓存。
4. 高级模式可点击整行或复选框勾选项目。按分类和风险筛选，或搜索名称；“选当前列表”只选当前可见且可处理的项目，“只选低风险”替换为推荐选择，“清空选择”清除全部勾选。
5. 隐藏的已选项目仍计入底部清理总量。检查底部“可处理”容量与右侧说明后，点击“清理已选”，确认后删除。
6. “扫描范围”可返回分类选择页；点击“重新扫描”按新范围检查。返回选择页时清理按钮不可用。
7. 系统目录权限不足时，可点击“以管理员身份重新打开”，允许 Windows 提权后再手动开始扫描。换用其他管理员账户时会识别该账户的缓存。
8. 清理后自动刷新统计，可导出逐文件删除与跳过记录。

## 风险与容量如何看

| 显示 | 含义 |
| --- | --- |
| 青绿色：低风险 · 推荐 | 明确目录中的可重建缓存，已过保留期，相关软件已关闭；可纳入简洁一键清理 |
| 橙色：有风险 · 需确认 | 可能影响离线播放、重新编译、旧诊断资料或更新安装包；默认不勾选，只能高级自选 |
| 粉色：使用痕迹 · 需确认 | 删除访问入口或使用记录，需自行确认；不删除原文档 |
| 蓝色：仅展示 · 保留 / 使用中 · 保留 | 不提供删除，或当前被运行中的软件保护 |

每项右侧醒目展示“可处理 xx GB / MB”或“不会删除”，旁边另列识别占用和保留原因。右侧详情展示文件用途、清理影响、保留期、权限情况与路径。

顶部“可处理合计”只累计当前符合删除条件的文件，含需确认项目；“符合一键清理条件”只累计推荐缓存。底部“可处理”是当前模式实际计划处理的估算容量。容量不等于承诺实际释放量；压缩、稀疏文件、权限变化及后台写入都会影响最终结果。

“低风险”表示保守的已知缓存范围，不代表绝对无影响：首次加载、编译或下载可能变慢，离线环境应按自己的需求保留缓存。

## 扫描内容

| 分类 | 主要内容 |
| --- | --- |
| 系统垃圾 | 用户/Windows 临时文件、应用崩溃转储、MEMORY.DMP、小型蓝屏转储、Windows 错误报告、旧 CBS/DISM 日志、缩略图、DirectX 缓存、小组件网页缓存 |
| 常用软件 | Edge、Chrome、夸克、Firefox、Brave、Opera/Opera GX、QQ 浏览器；Steam、Epic、Discord、Slack、Zoom、微信/QQ 网页缓存、TapTap、米哈游启动器、Teams、N.E.K.O、ACLOS、完美平台、OCS Desktop；NVIDIA/AMD/Intel、Adobe 媒体缓存、QQ 音乐、Spotify、网易云音乐 |
| 开发缓存 | VS Code/Insiders、pip、npm、NuGet HTTP、Yarn、Composer、Gradle、Unity 构建缓存、Unreal 派生数据缓存 |
| 更新残留 | 完美平台、Unity Hub、OCS Desktop 的更新安装包，NVIDIA 驱动下载资料；Windows Update 下载资料只展示 |
| 使用痕迹 | Recent 顶层旧 .lnk 访问入口；浏览历史、Cookie 与账号数据仅提供保留说明 |
| 快捷方式 | 桌面与开始菜单中目标位于 C 盘、明确不存在且已过保留期的 .lnk |
| 注册表检查 | 只读检查软件安装信息，展示检查项数及关联目录缺失提示 |
| 受保护资料 | Office 更新/修复资料、原厂驱动备份、休眠/虚拟内存文件、还原与恢复资料保留 |

只显示当前发现的已知目录，以及必要的保留说明。未安装的软件、没有文件的普通缓存规则不会占据结果列表。识别规则使用标准路径，不保证覆盖软件的自定义目录或所有版本；未知目录不自动视为垃圾。

详细规则见同目录的“清理规则一览.md”，软件内可选中项目查看具体保留期。

## 重要边界

- 不删除注册表。安装目录缺失不等于注册表项无用；这类检查结果全部保留。
- Windows Update 下载资料、传递优化缓存、Windows.old 交由 Windows 自带功能处理。“打开 Windows 存储设置”只打开系统设置，不会自动执行清理。
- 不直接删除 Windows 组件库、Installer、DriverStore、Recovery、还原点。
- 不清空回收站，不删除个人下载、桌面文档、聊天记录、账号、密码、Cookie、书签、软件设置、已安装扩展、已安装依赖或未保存文件备份。
- Recent 只处理顶层 .lnk，不处理固定/跳转列表或原文件。失效快捷方式排除网络目标、其他盘、系统特殊目标和无法确定的目标，删除前再次核对。
- 更新安装包需确认更新已完成、无需本地备份。pending 目录和文件日期均不能证明更新已经完成。
- 显卡、构建、媒体和音乐缓存清理有重新生成/下载或离线使用的影响，归入需确认范围。程序能检查已知相关进程，无法识别所有游戏或所有自定义程序名称。

## 文件保护

只处理规则中明确允许的目录、文件类型或单个文件；不提供任意路径递归删除。跳过目录链接、文件链接、硬链接、云端占位、只读、近期修改和无法读取的文件。

扫描后删除时再次确认文件实际位置、身份、大小和修改时间；变化的文件保留。通过文件句柄删除，正在使用或系统阻止删除的文件跳过，不关闭程序、不更改权限。处理时定期重新检查相关进程，相关软件启动后保留后续文件。

只删除符合条件的文件，空目录保留。删除为永久删除，不进入回收站。可以停止任务；已完成的删除不会回滚。

## 跨电脑使用

支持 Windows 10 / Windows 11，需要 .NET Framework 4.8 或更高版本。无须 Python、Node.js 或额外的 .NET SDK。AnyCPU 程序，当前实际验证环境为 Windows x64，其他硬件未实测。

复制 ClearSpace.exe 和 ClearSpace.exe.config 即可使用，不安装服务或驱动，不绑定特定用户名。默认仅检查 C 盘；没有 C 盘时使用 Windows 所在盘。用户资料已迁移到其他盘时，不清理其他盘中的缓存。

清理日志保存在 %LOCALAPPDATA%\ClearSpace\Logs，包含本地文件路径。程序未做代码签名，附带 SHA-256 校验值和可构建源码。

## 验证与构建

最终 EXE 已通过 36 项文件安全检查和 23 项界面操作检查。删除测试只作用于程序新建的隔离模拟文件，没有清理当前电脑的真实缓存。

已对当前电脑进行只读扫描，并检查启动页、高级结果、保留项目与较小窗口的实际 WPF 渲染。

源码构建使用 Windows 自带 .NET Framework 编译器，无需联网下载包：

~~~powershell
.\build.ps1 -OutputDirectory .\dist
~~~

可选重建图标：运行 create-icon.ps1。隔离验证：

~~~powershell
.\dist\ClearSpace.exe --self-test .\verification
.\dist\ClearSpace.exe --ui-self-test .\ui-verification
~~~

只读报告：

~~~powershell
.\dist\ClearSpace.exe --scan-report .\scan-report.json
~~~

## 参考

- [Microsoft 关于注册表清理工具的支持政策](https://support.microsoft.com/sl-si/topic/microsoft-support-policy-for-the-use-of-registry-cleaning-utilities-0485f4df-9520-3691-2461-7b0fd54e8b3a)
- [Windows 传递优化工作流程与缓存目录](https://learn.microsoft.com/en-ca/windows/deployment/do/delivery-optimization-workflow)
- [Windows 传递优化与系统清理入口](https://support.microsoft.com/en-us/windows/deployment/updates-lifecycle/delivery-optimization-in-windows)
- [NVIDIA 着色器缓存与重新编译说明](https://nvidia.custhelp.com/app/answers/detail/a_id/5735/)
- [.NET Framework 版本与 Windows 依赖](https://learn.microsoft.com/en-us/dotnet/framework/install/versions-and-dependencies)

版本日期：2026-10-13。by CraZyX。
