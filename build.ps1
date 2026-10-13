param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
$exe = Join-Path $OutputDirectory 'Clarity.exe'
$release = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'version.json') -Raw | ConvertFrom-Json
if ($release.channel -ne 'beta' -or $release.number -lt 1 -or $release.number -gt 65535) { throw 'Invalid beta version definition.' }
$assemblyVersion = '0.{0}.0.0' -f [int]$release.number
$releaseTag = 'beta0.{0:00}' -f [int]$release.number
$generatedVersion = Join-Path $OutputDirectory 'ReleaseVersion.g.cs'
$versionSource = @"
using System.Reflection;
[assembly: AssemblyProduct("Clarity")]
[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$assemblyVersion")]
[assembly: AssemblyInformationalVersion("$releaseTag")]
namespace ClearSpace { public static class ReleaseVersion { public const string Display = "$releaseTag"; } }
"@
[System.IO.File]::WriteAllText($generatedVersion,$versionSource,[System.Text.UTF8Encoding]::new($false))
$compilerArguments = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output',
    '/codepage:65001', "/out:$exe", "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')",
    "/resource:$(Join-Path $PSScriptRoot 'Main.xaml'),ClearSpace.Main.xaml",
    "/reference:$(Join-Path $framework 'WPF\PresentationFramework.dll')",
    "/reference:$(Join-Path $framework 'WPF\PresentationCore.dll')",
    "/reference:$(Join-Path $framework 'WPF\WindowsBase.dll')",
    '/reference:System.Xaml.dll', '/reference:System.Core.dll', '/reference:System.Web.Extensions.dll', '/reference:System.Security.dll', '/reference:System.Windows.Forms.dll',
    (Join-Path $PSScriptRoot 'Engine.cs'), (Join-Path $PSScriptRoot 'ExtendedRules.cs'), (Join-Path $PSScriptRoot 'CatalogV2.cs'), (Join-Path $PSScriptRoot 'RegistryCleaner.cs'), (Join-Path $PSScriptRoot 'App.cs'), (Join-Path $PSScriptRoot 'SelfTest.cs'), (Join-Path $PSScriptRoot 'V2Tests.cs'), (Join-Path $PSScriptRoot 'ApplicationDiscovery.cs'), (Join-Path $PSScriptRoot 'RuleLibrary.cs'), (Join-Path $PSScriptRoot 'HeuristicScanner.cs'), (Join-Path $PSScriptRoot 'FileRecovery.cs'), (Join-Path $PSScriptRoot 'RuleCenter.cs')
)
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'Clarity.ico')) { $compilerArguments += "/win32icon:$(Join-Path $PSScriptRoot 'Clarity.ico')" }
$compilerArguments += (Join-Path $PSScriptRoot 'V3Tests.cs')
$compilerArguments += (Join-Path $PSScriptRoot 'Winapp2.cs')
$compilerArguments += (Join-Path $PSScriptRoot 'Winapp2Tests.cs')
$compilerArguments += (Join-Path $PSScriptRoot 'BackupFolders.cs')
$compilerArguments += (Join-Path $PSScriptRoot 'RecoveryTests.cs')
$compilerArguments += (Join-Path $PSScriptRoot 'ScanModes.cs')
$compilerArguments += (Join-Path $PSScriptRoot 'ApplicationLocations.cs')
$compilerArguments += (Join-Path $PSScriptRoot 'RuleSourceAddresses.cs')
$compilerArguments += "/resource:$(Join-Path $PSScriptRoot 'Clarity.ico'),ClearSpace.AppIcon.ico"
$compilerArguments += (Join-Path $PSScriptRoot 'UiTests.cs')
$compilerArguments += $generatedVersion
$compilerArguments += (Join-Path $PSScriptRoot 'SystemDiskCleanup.cs')
$compilerArguments += (Join-Path $PSScriptRoot 'RiskConfirmation.cs')
$compilerArguments += (Join-Path $PSScriptRoot 'BetaTests.cs')
foreach ($resourceName in @('rule-public-key.xml','BundledRules.signed.json')) {if (Test-Path -LiteralPath (Join-Path $PSScriptRoot $resourceName)) {$compilerArguments += "/resource:$(Join-Path $PSScriptRoot $resourceName),ClearSpace.$resourceName"}}
$compilerArguments += "/resource:$(Join-Path $PSScriptRoot 'Winapp2.ini'),ClearSpace.Winapp2.ini"
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Remove-Item -LiteralPath $generatedVersion -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Clarity.exe.config') -Destination $OutputDirectory -Force
Get-Item -LiteralPath $exe | Select-Object FullName, Length

