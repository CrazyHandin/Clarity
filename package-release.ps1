param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot '../../outputs'),
    [string]$VerificationDirectory = (Join-Path $PSScriptRoot 'release-verification-final')
)
$ErrorActionPreference = 'Stop'
$version = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'version.json') -Raw | ConvertFrom-Json
$tag = 'beta0.{0:00}' -f [int]$version.number
$destination = Join-Path $OutputRoot "Clarity-$tag"
$executable = Join-Path $destination 'Clarity.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build and verify the release executable in its destination before packaging.' }
$suites = @{
    'self-test' = 'self-test-results.txt'; 'v2-self-test' = 'v2-test-results.txt';
    'v3-self-test' = 'v3-test-results.txt'; 'winapp2-self-test' = 'winapp2-test-results.txt';
    'beta-self-test' = 'beta-test-results.txt'; 'recovery-self-test' = 'recovery-test-results.txt'; 'ui-self-test' = 'ui-test-results.txt'; 'common-rules-self-test' = 'common-rules-test-results.txt'
}
$passed = 0
foreach ($suite in ($suites.Keys | Sort-Object)) {
    $report = Join-Path (Join-Path $VerificationDirectory $suite) $suites[$suite]
    $result = Get-Content -LiteralPath $report -Raw
    if ($result -notmatch 'ALL (\d+) (?:[A-Z0-9]+ )?CHECKS PASSED') { throw "Missing successful verification: $suite" }
    $passed += [int]$Matches[1]
}
$publicFiles = @('Clarity.exe.config','README.md','README.en.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md','版本历史.md','清理依据.md','常用软件规则.md','清理规则一览.md','规则库维护.md','Winapp2-使用说明.md','Winapp2-License.md','Winapp2.ini','ApplicationRules.template.json','BundledRules.signed.json','rule-public-key.xml')
foreach ($name in $publicFiles) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $destination $name) -Force }
$verification = @"
澄净 $tag — by CraZyX
日期：2026-10-13
通过 $passed 项检查：文件引擎、受限注册表、规则与恢复、Winapp2、beta 功能、恢复后清除备份、常用软件缓存边界、WPF 界面。
验证恢复后自动清除对应备份，冲突、失败、损坏与未恢复内容仍保留；完整恢复清除索引与空批次。
验证打开备份位置及清理按钮旁的独立 Windows 清理勾选项，分类列表不再包含系统清理动作。
验证未知缓存全部内容读取、无扩展名/二进制/子目录、自选风险删除及强制备份恢复。
验证默认快速扫描，跳过应用发现与外部规则并只提供简洁清理；常规扫描支持完整识别与高级自选。
验证软件缓存 → 软件名称 → 未知缓存的三级菜单及联动选择、搜索与风险筛选。
验证低风险清理直接执行，选中风险项目时确认，取消不删除；Windows 磁盘清理默认不选及模拟执行结果。
验证清理期间候选列表保持深色；结果页显示估算空间及备份情况，完成后回到准备页，不自动扫描。
验证 Winapp2 固定地址与 JSON 独立地址保存、来源切换、旧地址纠正和应用文件位置按钮。
验证程序文件名 Clarity.exe，中文澄净不变，界面图标和 Windows 图标使用同一份 ICO。
验证 ClarityBackups 默认目录与旧默认路径兼容，自定义目录和已有备份保留原位。
验证常用软件路径匹配、微信和 QQ 登录/聊天保护、VS Code ipch 年龄与占用保护、Adobe 强制备份及恢复、WPS 文档备份保留。
验证内置规则不再含小众启动器和浏览器测试渠道；UI 显示较小的 Clarity 名称。
删除验证仅操作生成的工作目录测试文件和随机 HKCU 测试分支。
系统清理使用模拟执行器，没有实际启动 cleanmgr 清理真实 C 盘。
本次删除和交互验证使用隔离模拟文件；没有删除真实用户缓存。
Windows 系统清理可能永久处理默认项目；不计入扫描容量或澄净备份。
源代码包使用白名单，排除私钥、真实扫描报告、个人路径日志和所有测试目录。
"@
$rebuildStatus = Join-Path $VerificationDirectory 'source-rebuild-status.txt'
if (Test-Path -LiteralPath $rebuildStatus) { $verification += "`r`n" + [IO.File]::ReadAllText($rebuildStatus) + "`r`n" }
[IO.File]::WriteAllText((Join-Path $destination '验证结果.txt'),$verification,[Text.UTF8Encoding]::new($true))
$hash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
[IO.File]::WriteAllText((Join-Path $destination 'SHA256.txt'),"$hash  Clarity.exe`r`n",[Text.UTF8Encoding]::new($false))
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
function Write-Archive([string]$ArchivePath,[string]$Base,[string[]]$Names) {
    $stream = [IO.File]::Open($ArchivePath,[IO.FileMode]::Create,[IO.FileAccess]::Write,[IO.FileShare]::None)
    $archive = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
    try {
        foreach ($name in $Names) {
            $file = Join-Path $Base $name
            if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Missing package file: $name" }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file,$name,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose(); $stream.Dispose() }
}
$portable = Join-Path $OutputRoot "澄净-C盘清理-便携版-$tag.zip"
$source = Join-Path $OutputRoot "澄净-C盘清理-源码-$tag.zip"
$portableFiles = @('Clarity.exe') + $publicFiles + @('验证结果.txt','SHA256.txt')
$sourceFiles = @('Engine.cs','ExtendedRules.cs','CatalogV2.cs','RegistryCleaner.cs','App.cs','SelfTest.cs','V2Tests.cs','V3Tests.cs','ApplicationDiscovery.cs','RuleLibrary.cs','HeuristicScanner.cs','FileRecovery.cs','RuleCenter.cs','Winapp2.cs','Winapp2Tests.cs','SystemDiskCleanup.cs','RiskConfirmation.cs','BetaTests.cs','BackupFolders.cs','RecoveryTests.cs','ScanModes.cs','CommonSoftwareRules.cs','CommonRulesTests.cs','ApplicationLocations.cs','RuleSourceAddresses.cs','UiTests.cs','version.json','Main.xaml','build.ps1','package-release.ps1','create-icon.ps1','Clarity.ico','app.manifest','Publish-Rules.ps1','.gitignore') + $publicFiles
Write-Archive $portable $destination $portableFiles
Write-Archive $source $PSScriptRoot $sourceFiles
[pscustomobject]@{Version=$tag;Checks=$passed;Executable=$executable;SHA256=$hash;Portable=$portable;PortableFiles=$portableFiles.Count;Source=$source;SourceFiles=$sourceFiles.Count}
