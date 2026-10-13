# 澄净 beta0.08 的 Winapp2.ini 支持

默认启用 Winapp2 扩展来源，启动不会扫描、联网或清理。程序内置离线快照，也保留原始 INI 供查看、导入和发布。

## 更新和导入

1. 打开“应用与规则 → 规则更新”，来源选择 Winapp2.ini。
2. “检查更新”从用户指定的固定 HTTPS 地址下载；可取消，失败保留旧版。每 7 天自动检查可单独启用，默认关闭。
3. 本地导入支持 .ini；会提示原始条目数、可适配和保留指令数。导入后重新扫描生效。
4. 导出当前规则得到原始 INI；“兼容报告”逐指令列出支持或保留的原因。
5. 回退上一版恢复有效旧库。缓存目录：%LOCALAPPDATA%\ClearSpace\Winapp2。

来源：[用户指定的 Winapp2.ini](https://raw.githubusercontent.com/MoscaDotTo/Winapp2/master/Winapp2.ini)。本次快照为 260915，4,068 个原始节，14,121 条 FileKey、3,677 条 RegKey。原文 SHA-256：54CE86839B5C5E6ED796CC17B0CE879A795920BA5FCB930DC84BBA7ECE535C5B。

## 兼容范围

| 字段 | 行为 |
| --- | --- |
| 节名 | 保留名称和行号；末尾星号不自动勾选；重复节名拒绝应用 |
| 重复/编号键 | 逐行保留，多个同名 FileKey、DetectFile、ExcludeKey 不覆盖 |
| Detect / DetectFile | 多个条件 OR；注册表只读，路径变量展开，通配匹配有数量与深度上限 |
| SpecialDetect | DET_CHROME、DET_FIREFOX、DET_EDGE、DET_OPERA、DET_THUNDERBIRD、DET_IE 使用已发现程序指纹；其他代码保留 |
| DetectOS | 最低版本\|最高版本，端点可空，包含边界；无效版本保留 |
| FileKey | 普通与明确风险自选均遵循路径、分号分隔模式、* 和 ?；无递归标志只检查直接文件 |
| RECURSE | 遍历允许的子目录，跳过链接、排除和保护范围 |
| REMOVESELF | 包含递归行为，只清空通过条件的文件，保留目录；不宣称完整执行 CCleaner 的目录删除行为 |
| ExcludeKey FILE / PATH | FILE 按指定父目录和名称排除；PATH 覆盖匹配目录及其子目录；未知或错误排除使整项保留 |
| ExcludeKey REG | 解析，但因任意社区注册表删除未执行，不触发修改 |
| RegKey | 解析、计数、兼容报告和保留项目展示；不送入任意注册表删除执行器 |
| Warning | 保留完整文本，在项目说明和清理确认中显示 |
| LangSecRef / Section | 映射网页与软件缓存分组，原始英文名称保留 |
| Default | 不继承自动勾选设置，社区项始终默认不选 |
| 未支持字段/标志 | 不猜测执行方式，保留内容并记录原因 |

变量支持 AppData、LocalAppData、LocalLowAppData、ProgramData、CommonAppData、ProgramFiles、CommonProgramFiles、UserProfile、Temp、WinDir、SystemDrive、Public、Documents。ProgramFiles 和 CommonProgramFiles 检查 32/64 位位置。变量可用于检测，不代表允许清理对应全部目录；个人目录、Windows 和系统保护目录不进入社区删除范围。

## 清理边界

本次快照中 3,839 条文件指令可适配为普通候选，10,282 条文件指令默认保留，3,677 条社区注册表指令不执行任意删除。可适配表示符合解析和范围要求；实际电脑还必须满足目录存在、位于 C 盘、应用检测、文件模式、排除和安全条件。

社区项目只进入高级自选，不进入一键清理，至少保留最近 14 天的文件。普通候选保留账号、Cookie、密码、会话、配置、数据库、历史与备份。明确文件范围且指令/排除完整的保留内容可另行风险自选，解除个人资料与年龄保留时强制备份；系统目录、显式排除与未支持指令不提供直接删除。文件头用于额外排除文档、图片、数据库和程序，不能证明其他内容一定可重建。

社区清理即使选择“永久删除”也强制先备份。备份失败保留原文件；恢复不覆盖现有文件。内置基础低风险规则继续提供一键清理；原有受限注册表清理功能与社区 RegKey 分开。

Winapp2 是未签名社区库，HTTPS、格式检查和 SHA-256 不能替代发布者签名。下载器只使用上述固定地址，不上传应用清单或扫描结果，不运行脚本，不改变权限、不关闭程序。

## 署名与许可

Winapp2.ini 原始数据库由 [Winapp2 项目及贡献者](https://github.com/MoscaDotTo/Winapp2)提供，随包文件未修改，按 CC-BY-SA-4.0 再分发。完整许可见 Winapp2-License.md。澄净的解析、安全适配和 UI 代码为独立实现；没有使用其他项目的解析器源代码。

参考：[官方格式与字段说明](https://github.com/MoscaDotTo/Winapp2/blob/master/CONTRIBUTING.md)、[官方项目说明](https://github.com/MoscaDotTo/Winapp2)、[官方许可](https://github.com/MoscaDotTo/Winapp2/blob/master/License.md)。
