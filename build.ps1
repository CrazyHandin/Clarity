param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'),[switch]$WithTests)
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
$isBetaLiteral = $metadata.IsBeta.ToString().ToLowerInvariant()
$winapp2 = Join-Path $PSScriptRoot 'rules/Winapp2.ini'
$winapp2Hash = (Get-FileHash -LiteralPath $winapp2 -Algorithm SHA256).Hash.ToLowerInvariant()
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
namespace ClearSpace { public static class ReleaseVersion { public const string Display = "$releaseTag"; public const bool IsBeta = $isBetaLiteral; public const int Build = $buildNumber; public const string FullVersion = "$fullVersion"; public const string HighlightsChinese = $highlightsChinese; public const string HighlightsEnglish = $highlightsEnglish; public const string Winapp2SHA256 = "$winapp2Hash"; } }
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
    'src/UI/Settings.Backup.xaml' = 'ClearSpace.Settings.Backup.xaml'
    'src/UI/Settings.Applications.xaml' = 'ClearSpace.Settings.Applications.xaml'
    'src/UI/Settings.Rules.xaml' = 'ClearSpace.Settings.Rules.xaml'
    'src/UI/RulePacks.xaml' = 'ClearSpace.RulePacks.xaml'
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
    'assets/update-public-key.xml' = 'ClearSpace.update-public-key.xml'
}
foreach ($resource in $resources.GetEnumerator()) {
    $path = Join-Path $PSScriptRoot $resource.Key
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing build resource: $($resource.Key)" }
    $compilerArguments += "/resource:$path,$($resource.Value)"
}
$allSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' -Recurse -File | Sort-Object FullName)
$serviceSources = @($allSources | Where-Object { $_.Directory.Name -eq 'UpdateService' })
$sources = @($allSources | Where-Object { $_.Directory.Name -ne 'UpdateService' })
$testSources = @()
if ($WithTests) { $testSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'tests') -Filter '*.cs' -Recurse -File | Sort-Object FullName) }
if ($sources.Count -eq 0) { throw 'No C# source files found.' }
$inputPaths = @($allSources.FullName) + @($testSources.FullName) + @($resources.Keys | ForEach-Object { Join-Path $PSScriptRoot $_ }) + @('build.ps1','version.json','scripts/Get-ReleaseVersion.ps1','assets/app.manifest','assets/Clarity.ico','assets/Clarity.exe.config','rules/Winapp2.ini' | ForEach-Object { Join-Path $PSScriptRoot $_ })
$inputPaths = @($inputPaths | Where-Object { $_ })
$inputPaths += @('installer/Clarity.iss','scripts/build-installer.ps1','scripts/Sign-UpdateManifest.ps1','scripts/Publish-UpdateChannel.ps1','scripts/publish-release.ps1','package-release.ps1','scripts/Test-UpdatePackageLayout.ps1','tests/UpdatePackageCompatibility.ps1' | ForEach-Object { Join-Path $PSScriptRoot $_ })
function Get-BuildInputs {
    foreach ($path in ($inputPaths | Sort-Object -Unique)) {
        [ordered]@{Path=$path.Substring($PSScriptRoot.TrimEnd('\').Length+1).Replace('\','/');SHA256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
    }
}
$inputsBefore = @(Get-BuildInputs)
$productionArguments = $compilerArguments + @($sources.FullName) + @($generatedVersion)
$testExe = Join-Path $OutputDirectory '_verification/Clarity.Tests.exe'
function Copy-BundledRules([string]$Base) {
    $directory = Join-Path $Base 'rules'
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $target = Join-Path $directory 'Winapp2.ini'
    Copy-Item -LiteralPath $winapp2 -Destination $target -Force
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $winapp2Hash) { throw 'Bundled rule file changed during compilation.' }
}
try {
    & $compiler @productionArguments
    if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets/Clarity.exe.config') -Destination ($exe+'.config') -Force
    Copy-BundledRules $OutputDirectory
    $serviceExe = Join-Path $OutputDirectory 'Clarity.UpdateService.exe'
    $serviceVersion = Join-Path $OutputDirectory 'ServiceVersion.g.cs'
    [IO.File]::WriteAllText($serviceVersion,($versionSource -replace 'namespace ClearSpace \{.*',''),[Text.UTF8Encoding]::new($false))
    try {
        & $compiler '/nologo' '/target:winexe' '/platform:anycpu' '/optimize+' '/utf8output' '/codepage:65001' "/out:$serviceExe" "/win32manifest:$(Join-Path $PSScriptRoot 'assets/app.manifest')" "/reference:$exe" '/reference:System.Core.dll' '/reference:System.Web.Extensions.dll' '/reference:System.ServiceProcess.dll' '/reference:System.Security.dll' @($serviceSources.FullName) $serviceVersion
        if ($LASTEXITCODE -ne 0) { throw 'Update service compilation failed.' }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets/Clarity.exe.config') -Destination ($serviceExe+'.config') -Force
    } finally { Remove-Item -LiteralPath $serviceVersion -Force }
    if ($WithTests) {
        New-Item -ItemType Directory -Path (Split-Path -Parent $testExe) -Force | Out-Null
        $testArguments = @($compilerArguments | Where-Object { -not $_.StartsWith('/out:',[StringComparison]::OrdinalIgnoreCase) }) + @("/out:$testExe",'/define:CLARITY_TESTS','/main:ClearSpace.App','/reference:System.ServiceProcess.dll') + @($allSources.FullName) + @($testSources.FullName) + @($generatedVersion)
        & $compiler @testArguments
        if ($LASTEXITCODE -ne 0) { throw 'Test-host compilation failed.' }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets/Clarity.exe.config') -Destination ($testExe+'.config') -Force
        Copy-BundledRules (Split-Path -Parent $testExe)
    }
    if (($inputsBefore | ConvertTo-Json -Depth 3 -Compress) -cne (@(Get-BuildInputs) | ConvertTo-Json -Depth 3 -Compress)) { throw 'Source inputs changed during compilation.' }
    $proof = [ordered]@{Schema=1;Version=$fullVersion;CompilerSHA256=(Get-FileHash -LiteralPath $compiler -Algorithm SHA256).Hash;ExecutableSHA256=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash;UpdateServiceSHA256=(Get-FileHash -LiteralPath $serviceExe -Algorithm SHA256).Hash;Inputs=$inputsBefore;BundledFiles=@([ordered]@{Path='rules/Winapp2.ini';SHA256=$winapp2Hash});TestHost=$null}
    if ($WithTests) { $proof.TestHost=[ordered]@{Path='_verification/Clarity.Tests.exe';SHA256=(Get-FileHash -LiteralPath $testExe -Algorithm SHA256).Hash;Define='CLARITY_TESTS'} }
    [IO.File]::WriteAllText((Join-Path $OutputDirectory 'build-manifest.json'),($proof | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
} finally {
    Remove-Item -LiteralPath $generatedVersion -Force
}
Get-Item -LiteralPath $exe | Select-Object FullName,Length
