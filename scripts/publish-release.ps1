param(
    [string]$Repository = 'CrazyHandin/Clarity',
    [string]$OutputRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'releases'),
    [Parameter(Mandatory=$true)][string]$NotesFile,
    [string]$GitHubCli = 'gh'
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$release = Get-Content -LiteralPath (Join-Path $root 'version.json') -Raw | ConvertFrom-Json
$tag = 'beta0.{0:00}' -f [int]$release.number
$NotesFile = (Resolve-Path -LiteralPath $NotesFile).Path
$OutputRoot = (Resolve-Path -LiteralPath $OutputRoot).Path
$names = @("Clarity-$tag-setup.exe","Clarity-$tag-portable.zip","Clarity-$tag-source.zip",'SHA256SUMS.txt')
foreach ($name in $names) {
    if (-not (Test-Path -LiteralPath (Join-Path $OutputRoot $name) -PathType Leaf)) { throw "Missing release asset: $name" }
}
$checksums = @{}
foreach ($line in (Get-Content -LiteralPath (Join-Path $OutputRoot 'SHA256SUMS.txt'))) {
    if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') { throw 'Invalid checksum manifest.' }
    $checksums[$Matches[2]] = $Matches[1].ToLowerInvariant()
}
if ($checksums.Count -ne 3) { throw 'Expected three artifact checksums.' }
foreach ($name in $names[0..2]) {
    $actual = (Get-FileHash -LiteralPath (Join-Path $OutputRoot $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($checksums[$name] -ne $actual) { throw "Checksum mismatch: $name" }
}
function Invoke-Gh([string[]]$Arguments) {
    $result = & $GitHubCli @Arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub command failed: $($Arguments[0])" }
    return $result
}
$records = (Invoke-Gh -Arguments @('api',"repos/$Repository/releases?per_page=100",'--header','Cache-Control: no-cache') | Out-String | ConvertFrom-Json)
$existing = @($records | Where-Object { $_.tag_name -eq $tag })
if ($existing.Count -ne 0) { throw 'A release already exists for this version. Choose a new version; existing attachments will not be replaced.' }
Invoke-Gh -Arguments @('release','create',$tag,'--repo',$Repository,'--verify-tag','--draft','--title',"Clarity $tag",'--notes-file',$NotesFile) | Out-Null
# The release stays a draft if an upload or checksum verification fails.
foreach ($name in $names) {
    Invoke-Gh -Arguments @('release','upload',$tag,(Join-Path $OutputRoot $name),'--repo',$Repository) | Out-Null
}
$record = $null
for ($attempt=0; $attempt -lt 6; $attempt++) {
    $records = Invoke-Gh -Arguments @('api',"repos/$Repository/releases?per_page=100",'--header','Cache-Control: no-cache') | Out-String | ConvertFrom-Json
    $record = @($records | Where-Object { $_.tag_name -eq $tag -and $_.draft }) | Select-Object -First 1
    if ($record -and $record.assets.Count -eq 4) { break }
    Start-Sleep -Seconds 2
}
if (-not $record -or $record.assets.Count -ne 4) { throw 'Draft assets have not been confirmed.' }
foreach ($name in $names) {
    $asset = @($record.assets | Where-Object { $_.name -eq $name })
    $local = Get-Item -LiteralPath (Join-Path $OutputRoot $name)
    $hash = (Get-FileHash -LiteralPath $local.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($asset.Count -ne 1 -or $asset[0].size -ne $local.Length -or $asset[0].digest -ne "sha256:$hash") {
        throw "GitHub asset verification failed: $name"
    }
}
# GitHub cannot mark a prerelease as Latest. The beta label remains in the tag.
Invoke-Gh -Arguments @('release','edit',$tag,'--repo',$Repository,'--draft=false','--prerelease=false','--latest=true') | Out-Null
$latest = Invoke-Gh -Arguments @('api',"repos/$Repository/releases/latest",'--header','Cache-Control: no-cache') | Out-String | ConvertFrom-Json
if ($latest.tag_name -ne $tag -or $latest.draft -or $latest.prerelease) { throw 'The release was not confirmed as Latest.' }
Write-Output $latest.html_url
