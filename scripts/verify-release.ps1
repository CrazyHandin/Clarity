param(
    [Parameter(Mandatory=$true)][string]$Executable,
    [Parameter(Mandatory=$true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$Executable = (Resolve-Path -LiteralPath $Executable).Path
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$suites = [ordered]@{
    'self-test' = 'self-test-results.txt'; 'v2-self-test' = 'v2-test-results.txt'
    'v3-self-test' = 'v3-test-results.txt'; 'winapp2-self-test' = 'winapp2-test-results.txt'
    'beta-self-test' = 'beta-test-results.txt'; 'recovery-self-test' = 'recovery-test-results.txt'
    'common-rules-self-test' = 'common-rules-test-results.txt'; 'ui-self-test' = 'ui-test-results.txt'
}
$passed = 0
foreach ($suite in $suites.GetEnumerator()) {
    $directory = Join-Path $OutputDirectory $suite.Key
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $process = Start-Process -FilePath $Executable -ArgumentList @('--'+$suite.Key,('"'+$directory+'"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "Verification failed: $($suite.Key); exit $($process.ExitCode)" }
    $report = Get-Content -LiteralPath (Join-Path $directory $suite.Value) -Raw
    if ($report -notmatch 'ALL (\d+) (?:[A-Z0-9]+ )?CHECKS PASSED') { throw "Missing successful report: $($suite.Key)" }
    $passed += [int]$Matches[1]
    Write-Output "$($suite.Key): $($Matches[1]) checks passed."
}
$proof = [ordered]@{
    Version = [Diagnostics.FileVersionInfo]::GetVersionInfo($Executable).ProductVersion
    ExecutableSHA256 = (Get-FileHash -LiteralPath $Executable -Algorithm SHA256).Hash
    Checks = $passed
    VerifiedAt = [DateTime]::UtcNow.ToString('o')
}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'verification.json'),($proof | ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Output "ALL $passed CHECKS PASSED."
