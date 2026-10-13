param(
    [Parameter(Mandatory=$true)][string]$PortableArchive,
    [string]$OutputDirectory,
    [string]$CompilerPath = $env:CLARITY_ISCC
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$release = Get-Content -LiteralPath (Join-Path $root 'version.json') -Raw | ConvertFrom-Json
$tag = 'beta0.{0:00}' -f [int]$release.number
$numeric = '0.{0}.0.0' -f [int]$release.number
$PortableArchive = (Resolve-Path -LiteralPath $PortableArchive).Path
if (-not $OutputDirectory) { $OutputDirectory = Split-Path -Parent $PortableArchive }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (-not $CompilerPath) {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    if ($command) { $CompilerPath = $command.Source }
    else {
        foreach ($base in @(${env:ProgramFiles(x86)},$env:ProgramFiles)) {
            if (-not $base) { continue }
            foreach ($folder in @('Inno Setup 7','Inno Setup 6')) {
                $candidate = Join-Path $base "$folder/ISCC.exe"
                if (Test-Path -LiteralPath $candidate -PathType Leaf) { $CompilerPath = $candidate; break }
            }
            if ($CompilerPath) { break }
        }
    }
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw 'Install Inno Setup 6.7.3 or newer, or specify -CompilerPath / CLARITY_ISCC.'
}
$CompilerPath = (Resolve-Path -LiteralPath $CompilerPath).Path
$stage = Join-Path $OutputDirectory ('installer-stage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($PortableArchive)
try {
    foreach ($entry in $archive.Entries) {
        $target = [IO.Path]::GetFullPath((Join-Path $stage $entry.FullName))
        if (-not $target.StartsWith($stage.TrimEnd('\') + '\',[StringComparison]::OrdinalIgnoreCase)) {
            throw 'Unsafe portable archive entry.'
        }
    }
} finally { $archive.Dispose() }
try {
    [IO.Compression.ZipFile]::ExtractToDirectory($PortableArchive,$stage)
    $exe = Join-Path $stage 'Clarity.exe'
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe)
    if ($info.ProductVersion -ne $tag -or $info.FileVersion -ne $numeric) {
        throw 'Portable application version does not match version.json.'
    }
    $hashLine = (Get-Content -LiteralPath (Join-Path $stage 'SHA256.txt') -Raw).Trim()
    $actual = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
    if ($hashLine -ne "$actual  Clarity.exe") { throw 'Portable executable checksum mismatch.' }
    $arguments = @('/Qp',"/DPayloadDir=$stage","/DReleaseTag=$tag","/DNumericVersion=$numeric","/DReleaseOutput=$OutputDirectory",(Join-Path $root 'installer/Clarity.iss'))
    if (Test-Path -LiteralPath (Join-Path (Split-Path -Parent $CompilerPath) 'Languages/ChineseSimplified.isl')) { $arguments = @('/DIncludeChinese') + $arguments }
    $compilerOutput = @(& $CompilerPath @arguments)
    if ($LASTEXITCODE -ne 0) { throw ('Installer compilation failed: ' + ($compilerOutput -join ' ')) }
    $installer = Join-Path $OutputDirectory "Clarity-$tag-setup.exe"
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'Installer output is missing.' }
    [pscustomobject]@{Version=$tag;Installer=$installer;SHA256=(Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash}
} finally {
    $resolvedStage = [IO.Path]::GetFullPath($stage)
    if (-not $resolvedStage.StartsWith($OutputDirectory.TrimEnd('\')+'\installer-stage-',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid staging cleanup path.' }
    try { Remove-Item -LiteralPath $resolvedStage -Recurse -Force }
    catch { Write-Warning "Temporary compiler files could not be removed: $resolvedStage" }
}
