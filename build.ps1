param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'))
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'csc.exe'))) {
    $framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
}
$compiler = Join-Path $framework 'csc.exe'
$exe = Join-Path $OutputDirectory 'ClearSpace.exe'
$release = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'version.json') -Raw | ConvertFrom-Json
$releaseTag = 'beta0.{0:00}' -f [int]$release.number
$assemblyVersion = '0.{0}.0.0' -f [int]$release.number
$generatedVersion = Join-Path $OutputDirectory 'ReleaseVersion.g.cs'
$versionSource = @"
using System.Reflection;
[assembly: AssemblyVersion("$assemblyVersion")]
[assembly: AssemblyFileVersion("$assemblyVersion")]
[assembly: AssemblyInformationalVersion("$releaseTag")]
namespace ClearSpace { public static class ReleaseVersion { public const string Display = "$releaseTag"; } }
"@
[IO.File]::WriteAllText($generatedVersion,$versionSource,[Text.UTF8Encoding]::new($false))
$compilerArguments = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output',
    '/codepage:65001', "/out:$exe", "/win32manifest:$(Join-Path $PSScriptRoot 'app.manifest')",
    "/resource:$(Join-Path $PSScriptRoot 'Main.xaml'),ClearSpace.Main.xaml",
    "/reference:$(Join-Path $framework 'WPF\PresentationFramework.dll')",
    "/reference:$(Join-Path $framework 'WPF\PresentationCore.dll')",
    "/reference:$(Join-Path $framework 'WPF\WindowsBase.dll')",
    '/reference:System.Xaml.dll', '/reference:System.Core.dll', '/reference:System.Web.Extensions.dll',
    (Join-Path $PSScriptRoot 'Engine.cs'), (Join-Path $PSScriptRoot 'App.cs'), (Join-Path $PSScriptRoot 'SelfTest.cs')
)
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'ClearSpace.ico')) { $compilerArguments += "/win32icon:$(Join-Path $PSScriptRoot 'ClearSpace.ico')" }
$compilerArguments += $generatedVersion
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Remove-Item -LiteralPath $generatedVersion -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ClearSpace.exe.config') -Destination $OutputDirectory -Force
Get-Item -LiteralPath $exe | Select-Object FullName, Length
