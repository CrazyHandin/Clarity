param(
    [string]$OutputRoot = (Join-Path $PSScriptRoot 'releases'),
    [string]$VerificationDirectory = (Join-Path $PSScriptRoot 'release-verification'),
    [string]$InstallerCompiler,
    [Parameter(Mandatory=$true)][string]$UpdateSigningKey
)
$ErrorActionPreference = 'Stop'
$metadata = & (Join-Path $PSScriptRoot 'scripts/Get-ReleaseVersion.ps1')
$tag = $metadata.Tag
$prefix = $metadata.AssetPrefix
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$destination = Join-Path $OutputRoot "Clarity-$tag"
$executable = Join-Path $destination 'Clarity.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Build and verify the release executable in its destination before packaging.' }
$hash = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
$proof = Get-Content -LiteralPath (Join-Path $VerificationDirectory 'verification.json') -Raw | ConvertFrom-Json
if ($proof.Version -ne $metadata.FullVersion -or $proof.ExecutableSHA256 -ne $hash) { throw 'Verification reports do not belong to this executable.' }
$buildManifest = Join-Path $destination 'build-manifest.json'
$build = Get-Content -LiteralPath $buildManifest -Raw | ConvertFrom-Json
if ($proof.UpdateServiceSHA256 -ne $build.UpdateServiceSHA256 -or (Get-FileHash -LiteralPath (Join-Path $destination 'Clarity.UpdateService.exe') -Algorithm SHA256).Hash -ne $proof.UpdateServiceSHA256) { throw 'Update service does not match verification.' }
if ($proof.Schema -ne 2 -or -not $proof.TestHostIsolated -or $proof.BuildManifestSHA256 -ne (Get-FileHash -LiteralPath $buildManifest -Algorithm SHA256).Hash -or $build.ExecutableSHA256 -ne $hash -or $proof.TestHostSHA256 -ne $build.TestHost.SHA256) { throw 'Missing matching production/test-host verification.' }
if (@($build.BundledFiles).Count -ne 1 -or $build.BundledFiles[0].Path -cne 'rules/Winapp2.ini' -or $proof.BundledRuleSHA256 -ne $build.BundledFiles[0].SHA256 -or (Get-FileHash -LiteralPath (Join-Path $destination 'rules/Winapp2.ini') -Algorithm SHA256).Hash -ne $proof.BundledRuleSHA256) { throw 'Bundled rules do not match verified content.' }
foreach ($inputFile in $build.Inputs) {
    $path = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot $inputFile.Path))
    if (-not $path.StartsWith($PSScriptRoot.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $inputFile.SHA256) { throw "Source changed after verification: $($inputFile.Path)" }
}
$suites = @{
    'production-self-test' = 'production-test-results.txt';
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
    'docs/JSON规则填写说明.txt' = 'docs/JSON规则填写说明.txt'
    'docs/USAGE.txt' = '使用说明.txt'
    'docs/TERMS.txt' = 'docs/TERMS.txt'; 'LICENSE.txt' = 'LICENSE.txt'
    'rules/Winapp2.ini' = 'rules/Winapp2.ini'; 'rules/ApplicationRules.template.json' = 'rules/ApplicationRules.template.json'
    'rules/BundledRules.signed.json' = 'rules/BundledRules.signed.json'; 'rules/rule-public-key.xml' = 'rules/rule-public-key.xml'
}
foreach ($entry in $publicFiles.GetEnumerator()) {
    $target = Join-Path $destination $entry.Value
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $entry.Key) -Destination $target -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assets/Clarity.exe.config') -Destination (Join-Path $destination 'Clarity.UpdateService.exe.config') -Force
$verification = @"
Clarity $tag
Verified: $([DateTime]::UtcNow.ToString('yyyy-MM-dd')) UTC
$passed checks passed across nine isolated suites: production artifact structure, cleaning, registry, rules, recovery, Winapp2, application caches and UI.
Functional tests ran in a separate test host built from the same recorded source inputs. Production artifact metadata and embedded resources were checked separately; its exact hash is bound to these reports. Test executables are not included in the portable or installer packages.
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
$portable = Join-Path $OutputRoot "$prefix-portable.zip"
$source = Join-Path $OutputRoot "$prefix-source.zip"
$portableFiles = @('Clarity.exe','Clarity.UpdateService.exe','Clarity.UpdateService.exe.config') + @($publicFiles.Values) + @('VERIFICATION.txt','SHA256.txt')
$sourceFiles = @('version.json','build.ps1','package-release.ps1','.gitignore','src/UI/Main.xaml','src/UI/Settings.xaml','src/UI/English.json','assets/Clarity.ico','assets/app.manifest','scripts/create-icon.ps1','scripts/Publish-Rules.ps1','scripts/build-installer.ps1','scripts/verify-release.ps1','scripts/publish-release.ps1','installer/Clarity.iss') + @($publicFiles.Keys)
# Include source code from the two source folders, and only explicitly listed
# resources and documents. Local backups, signing keys, reports and build output stay out.
foreach ($file in (Get-ChildItem -LiteralPath @((Join-Path $PSScriptRoot 'src'),(Join-Path $PSScriptRoot 'tests')) -Filter '*.cs' -File -Recurse | Sort-Object FullName)) {
    $sourceFiles += $file.FullName.Substring($PSScriptRoot.TrimEnd('\').Length + 1).Replace('\','/')
}
$sourceFiles += 'scripts/Get-ReleaseVersion.ps1'
$sourceFiles += @('assets/update-public-key.xml','scripts/Sign-UpdateManifest.ps1')
$sourceFiles = @($sourceFiles | Sort-Object -Unique)
Write-Archive $portable $destination $portableFiles
Write-Archive $source $PSScriptRoot $sourceFiles
$installer = & (Join-Path $PSScriptRoot 'scripts/build-installer.ps1') -PortableArchive $portable -OutputDirectory $OutputRoot -CompilerPath $InstallerCompiler
$installerPath = Join-Path $OutputRoot "$prefix-setup.exe"
$manifestLines = foreach ($path in @($installerPath,$portable,$source)) {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant(),[IO.Path]::GetFileName($path)
}
$checksumFile = Join-Path $OutputRoot $metadata.ChecksumFile
[IO.File]::WriteAllText($checksumFile,($manifestLines -join "`n")+"`n",[Text.UTF8Encoding]::new($false))
$artifacts = foreach ($path in @($installerPath,$portable)) { [ordered]@{Name=[IO.Path]::GetFileName($path);Size=(Get-Item -LiteralPath $path).Length;SHA256=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()} }
$updateManifest = [ordered]@{Schema=1;Version=$tag;Build=$metadata.Build;Highlights=$metadata.Highlights;Artifacts=@($artifacts)}
[IO.File]::WriteAllText((Join-Path $OutputRoot 'Clarity-update.json'),($updateManifest | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
& (Join-Path $PSScriptRoot 'scripts/Sign-UpdateManifest.ps1') -ManifestFile (Join-Path $OutputRoot 'Clarity-update.json') -KeyFile $UpdateSigningKey
[pscustomobject]@{Version=$tag;Checks=$passed;Executable=$executable;SHA256=$hash;Portable=$portable;PortableFiles=$portableFiles.Count;Source=$source;SourceFiles=$sourceFiles.Count;Installer=$installerPath;Checksums=$checksumFile}
