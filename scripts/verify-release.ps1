param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$TestExecutable
)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$root = Split-Path -Parent $PSScriptRoot
$buildManifest = Join-Path (Split-Path -Parent $Executable) 'build-manifest.json'
$build = Get-Content -LiteralPath $buildManifest -Raw | ConvertFrom-Json
if (-not $TestExecutable) { $TestExecutable = Join-Path (Split-Path -Parent $Executable) '_verification/Clarity.Tests.exe' }
$TestExecutable = (Resolve-Path -LiteralPath $TestExecutable).Path
function Confirm-BuildInputs {
    if ($build.Schema -ne 1 -or $build.TestHost.Define -ne 'CLARITY_TESTS' -or $build.ExecutableSHA256 -ne (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash -or $build.TestHost.SHA256 -ne (Get-FileHash -LiteralPath $TestExecutable -Algorithm SHA256).Hash) { throw 'Build and test-host provenance does not match. Rebuild with -WithTests.' }
    if ([Diagnostics.FileVersionInfo]::GetVersionInfo($Executable).ProductVersion -ne $build.Version -or [Diagnostics.FileVersionInfo]::GetVersionInfo($TestExecutable).ProductVersion -ne $build.Version) { throw 'Production and test-host versions do not match.' }
    $serviceFile = Join-Path (Split-Path -Parent $Executable) 'Clarity.UpdateService.exe'
    if ((Get-FileHash -LiteralPath $serviceFile -Algorithm SHA256).Hash -ne $build.UpdateServiceSHA256 -or [Diagnostics.FileVersionInfo]::GetVersionInfo($serviceFile).ProductVersion -ne $build.Version) { throw 'Update service provenance does not match the build.' }
    if (@($build.BundledFiles).Count -ne 1 -or $build.BundledFiles[0].Path -cne 'rules/Winapp2.ini') { throw 'Bundled rule provenance is missing.' }
    foreach ($binary in @($Executable,$TestExecutable)) {
        $ruleFile = Join-Path (Split-Path -Parent $binary) 'rules/Winapp2.ini'
        if ((Get-FileHash -LiteralPath $ruleFile -Algorithm SHA256).Hash -ne $build.BundledFiles[0].SHA256) { throw 'Bundled rules no longer match the verified build.' }
    }
    $expected = @(Get-ChildItem -LiteralPath @((Join-Path $root 'src'),(Join-Path $root 'tests')) -Filter '*.cs' -Recurse -File | ForEach-Object { $_.FullName.Substring($root.TrimEnd('\').Length+1).Replace('\','/') } | Sort-Object)
    $recorded = @($build.Inputs | Where-Object { $_.Path.EndsWith('.cs') } | ForEach-Object Path | Sort-Object)
    if (($expected -join "`n") -cne ($recorded -join "`n")) { throw 'Build source list no longer matches the repository.' }
    foreach ($inputFile in $build.Inputs) {
        $path = [IO.Path]::GetFullPath((Join-Path $root $inputFile.Path))
        if (-not $path.StartsWith($root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $inputFile.SHA256) { throw "Build input changed: $($inputFile.Path)" }
    }
}
Confirm-BuildInputs
$suites = [ordered]@{
    'production-self-test' = 'production-test-results.txt'
    'self-test' = 'self-test-results.txt'; 'v2-self-test' = 'v2-test-results.txt'
    'v3-self-test' = 'v3-test-results.txt'; 'winapp2-self-test' = 'winapp2-test-results.txt'
    'beta-self-test' = 'beta-test-results.txt'; 'recovery-self-test' = 'recovery-test-results.txt'
    'common-rules-self-test' = 'common-rules-test-results.txt'; 'ui-self-test' = 'ui-test-results.txt'
}
$passed = 0
foreach ($suite in $suites.GetEnumerator()) {
    $directory = Join-Path $OutputDirectory $suite.Key
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $arguments = if ($suite.Key -eq 'production-self-test') { @('--'+$suite.Key,('"'+$Executable+'"'),('"'+$directory+'"')) } else { @('--'+$suite.Key,('"'+$directory+'"')) }
    $process = Start-Process -FilePath $TestExecutable -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Verification failed: $($suite.Key); exit $($process.ExitCode)" }
    $report = Get-Content -LiteralPath (Join-Path $directory $suite.Value) -Raw
    if ($report -notmatch 'ALL (\d+) (?:[A-Z0-9]+ )?CHECKS PASSED') { throw "Missing successful report: $($suite.Key)" }
    $passed += [int]$Matches[1]
    Write-Output "$($suite.Key): $($Matches[1]) checks passed."
}
Confirm-BuildInputs
$proof = [ordered]@{
    Schema = 2
    Version = [Diagnostics.FileVersionInfo]::GetVersionInfo($Executable).ProductVersion
    ExecutableSHA256 = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
    TestHostSHA256 = (Get-FileHash -LiteralPath $TestExecutable -Algorithm SHA256).Hash
    BuildManifestSHA256 = (Get-FileHash -LiteralPath $buildManifest -Algorithm SHA256).Hash
    UpdateServiceSHA256 = $build.UpdateServiceSHA256
    TestHostIsolated = $true
    BundledRuleSHA256 = $build.BundledFiles[0].SHA256
    Checks = $passed
    VerifiedAt = [DateTime]::UtcNow.ToString('o')
}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'verification.json'),($proof | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Output "ALL $passed CHECKS PASSED."
