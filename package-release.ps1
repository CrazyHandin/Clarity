param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot 'releases'),
    [string]$VerificationDirectory = (Join-Path $PSScriptRoot 'release-verification'),
    [string]$InstallerCompiler
)
$ErrorActionPreference = 'Stop'
$version = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'version.json') -Raw | ConvertFrom-Json
$tag = 'beta0.{0:00}' -f [int]$version.number
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$destination = Join-Path $OutputRoot "Clarity-$tag"
$executable = Join-Path $destination 'Clarity.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build and verify the release executable in its destination before packaging.' }
$hash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
$proof = Get-Content -LiteralPath (Join-Path $VerificationDirectory 'verification.json') -Raw | ConvertFrom-Json
if ($proof.Version -ne $tag -or $proof.ExecutableSHA256 -ne $hash) { throw 'Verification reports do not belong to this executable.' }
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
if ($proof.Checks -ne $passed) { throw 'Verification check count mismatch.' }
$publicFiles = [ordered]@{
    'assets/Clarity.exe.config' = 'Clarity.exe.config'
    'README.md' = 'README.md'; 'README.en.md' = 'README.en.md'
    'CHANGELOG.md' = 'CHANGELOG.md'; 'THIRD_PARTY_NOTICES.md' = 'THIRD_PARTY_NOTICES.md'
    'docs/版本历史.md' = 'docs/版本历史.md'; 'docs/清理依据.md' = 'docs/清理依据.md'
    'docs/常用软件规则.md' = 'docs/常用软件规则.md'; 'docs/清理规则一览.md' = 'docs/清理规则一览.md'
    'docs/规则库维护.md' = 'docs/规则库维护.md'; 'docs/Winapp2-使用说明.md' = 'docs/Winapp2-使用说明.md'
    'docs/Winapp2-License.md' = 'docs/Winapp2-License.md'
    'docs/BUILD.md' = 'docs/BUILD.md'
    'docs/USAGE.txt' = '使用说明.txt'
    'rules/Winapp2.ini' = 'rules/Winapp2.ini'; 'rules/ApplicationRules.template.json' = 'rules/ApplicationRules.template.json'
    'rules/BundledRules.signed.json' = 'rules/BundledRules.signed.json'; 'rules/rule-public-key.xml' = 'rules/rule-public-key.xml'
}
foreach ($entry in $publicFiles.GetEnumerator()) {
    $target = Join-Path $destination $entry.Value
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $entry.Key) -Destination $target -Force
}
$verification = @"
Clarity $tag
Verified: $([DateTime]::UtcNow.ToString('yyyy-MM-dd')) UTC
$passed checks passed across eight isolated suites: cleaning, registry, rules, recovery, Winapp2, application caches and UI.
Tests used generated fixtures and temporary registry keys. No personal cache files were deleted and Windows Disk Cleanup was simulated.
The source archive uses an explicit public-file list; private keys, credentials, backups and real scan reports are excluded.
"@
$rebuildStatus = Join-Path $VerificationDirectory 'source-rebuild-status.txt'
if (Test-Path -LiteralPath $rebuildStatus) { $verification += "`r`n" + [IO.File]::ReadAllText($rebuildStatus) + "`r`n" }
[IO.File]::WriteAllText((Join-Path $destination 'VERIFICATION.txt'),$verification,[Text.UTF8Encoding]::new($true))
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
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file,$name.Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $archive.Dispose(); $stream.Dispose() }
}
$portable = Join-Path $OutputRoot "Clarity-$tag-portable.zip"
$source = Join-Path $OutputRoot "Clarity-$tag-source.zip"
$portableFiles = @('Clarity.exe') + @($publicFiles.Values) + @('VERIFICATION.txt','SHA256.txt')
$sourceFiles = @('version.json','build.ps1','package-release.ps1','.gitignore','src/UI/Main.xaml','src/UI/Settings.xaml','src/UI/English.json','assets/Clarity.ico','assets/app.manifest','scripts/create-icon.ps1','scripts/Publish-Rules.ps1','scripts/build-installer.ps1','scripts/verify-release.ps1','scripts/publish-release.ps1','installer/Clarity.iss') + @($publicFiles.Keys)
# Include source code from the two source folders, and only explicitly listed
# resources and documents. Local backups, signing keys, reports and build output stay out.
foreach ($file in (Get-ChildItem -LiteralPath @((Join-Path $PSScriptRoot 'src'),(Join-Path $PSScriptRoot 'tests')) -Filter '*.cs' -File -Recurse | Sort-Object FullName)) {
    $sourceFiles += $file.FullName.Substring($PSScriptRoot.TrimEnd('\').Length + 1).Replace('\','/')
}
$sourceFiles = @($sourceFiles | Sort-Object -Unique)
Write-Archive $portable $destination $portableFiles
Write-Archive $source $PSScriptRoot $sourceFiles
$installer = & (Join-Path $PSScriptRoot 'scripts/build-installer.ps1') -PortableArchive $portable -OutputDirectory $OutputRoot -CompilerPath $InstallerCompiler
$installerPath = Join-Path $OutputRoot "Clarity-$tag-setup.exe"
$manifestLines = foreach ($path in @($installerPath,$portable,$source)) {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(),[IO.Path]::GetFileName($path)
}
$checksumFile = Join-Path $OutputRoot 'SHA256SUMS.txt'
[IO.File]::WriteAllText($checksumFile,($manifestLines -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
[pscustomobject]@{Version=$tag;Checks=$passed;Executable=$executable;SHA256=$hash;Portable=$portable;PortableFiles=$portableFiles.Count;Source=$source;SourceFiles=$sourceFiles.Count;Installer=$installerPath;Checksums=$checksumFile}
