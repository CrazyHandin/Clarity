param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
$exe = Join-Path $OutputDirectory 'Clarity.exe'
$metadata = & (Join-Path $PSScriptRoot 'scripts/Get-ReleaseVersion.ps1')
$assemblyVersion = $metadata.NumericVersion
$releaseTag = $metadata.Tag
$fullVersion = $metadata.FullVersion
$buildNumber = $metadata.Build
$highlightsChinese = ConvertTo-Json -InputObject ([string]$metadata.Highlights.'zh-CN') -Compress
$highlightsEnglish = ConvertTo-Json -InputObject ([string]$metadata.Highlights.en) -Compress
$generatedVersion = Join-Path $OutputDirectory 'ReleaseVersion.g.cs'
$versionSource = @"
using System.Reflection;
[assembly: AssemblyProduct("Clarity")]
[assembly: AssemblyTitle("Clarity")]
[assembly: AssemblyCopyright("Clarity © 2026 CraZyX")]
[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$assemblyVersion")]
[assembly: AssemblyInformationalVersion("$fullVersion")]
namespace ClearSpace { public static class ReleaseVersion { public const string Display = "$releaseTag"; public const int Build = $buildNumber; public const string FullVersion = "$fullVersion"; public const string HighlightsChinese = $highlightsChinese; public const string HighlightsEnglish = $highlightsEnglish; } }
"@
[IO.File]::WriteAllText($generatedVersion,$versionSource,[Text.UTF8Encoding]::new($false))
$compilerArguments = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output', '/codepage:65001',
    "/out:$exe", "/win32manifest:$(Join-Path $PSScriptRoot 'assets/app.manifest')",
    "/win32icon:$(Join-Path $PSScriptRoot 'assets/Clarity.ico')",
    "/reference:$(Join-Path $framework 'WPF\PresentationFramework.dll')",
    "/reference:$(Join-Path $framework 'WPF\PresentationCore.dll')",
    "/reference:$(Join-Path $framework 'WPF\WindowsBase.dll')",
    '/reference:System.Xaml.dll', '/reference:System.Core.dll', '/reference:System.Web.Extensions.dll',
    '/reference:System.Security.dll', '/reference:System.Windows.Forms.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll'
)
$resources = [ordered]@{
    'src/UI/Main.xaml' = 'ClearSpace.Main.xaml'
    'src/UI/Settings.xaml' = 'ClearSpace.Settings.xaml'
    'src/UI/English.json' = 'ClearSpace.English.json'
    'docs/USAGE.txt' = 'ClearSpace.Usage.txt'
    'docs/TERMS.txt' = 'ClearSpace.Terms.txt'
    'docs/JSON规则填写说明.txt' = 'ClearSpace.JsonGuide.txt'
    'LICENSE.txt' = 'ClearSpace.MitLicense.txt'
    'THIRD_PARTY_NOTICES.md' = 'ClearSpace.ThirdParty.txt'
    'docs/Winapp2-License.md' = 'ClearSpace.Winapp2License.txt'
    'assets/Clarity.ico' = 'ClearSpace.AppIcon.ico'
    'rules/rule-public-key.xml' = 'ClearSpace.rule-public-key.xml'
    'rules/BundledRules.signed.json' = 'ClearSpace.BundledRules.signed.json'
    'rules/Winapp2.ini' = 'ClearSpace.Winapp2.ini'
}
foreach ($resource in $resources.GetEnumerator()) {
    $path = Join-Path $PSScriptRoot $resource.Key
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing build resource: $($resource.Key)" }
    $compilerArguments += "/resource:$path,$($resource.Value)"
}
$sourceRoots = @((Join-Path $PSScriptRoot 'src'),(Join-Path $PSScriptRoot 'tests'))
$sources = @(Get-ChildItem -LiteralPath $sourceRoots -Filter '*.cs' -Recurse -File | Where-Object { $_.FullName -ne $generatedVersion } | Sort-Object FullName)
if ($sources.Count -eq 0) { throw 'No C# source files found.' }
$compilerArguments += @($sources.FullName) + @($generatedVersion)
try {
    & $compiler @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
} finally {
    Remove-Item -LiteralPath $generatedVersion -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets/Clarity.exe.config') -Destination $OutputDirectory -Force
Get-Item -LiteralPath $exe | Select-Object FullName,Length
