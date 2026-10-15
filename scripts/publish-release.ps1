param(
    [string]$Repository = 'CrazyHandin/Clarity',
    [string]$OutputRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) 'releases'),
    [Parameter(Mandatory=$true)][string]$NotesFile,
    [string]$GitHubCli = 'gh'
)
$ErrorActionPreference = 'Stop'
$metadata = & (Join-Path $PSScriptRoot 'Get-ReleaseVersion.ps1')
if ($metadata.Build -lt 1) { throw 'An internal build number is required.' }
$tag = $metadata.Tag
$prefix = $metadata.AssetPrefix
$NotesFile = (Resolve-Path -LiteralPath $NotesFile).Path
$OutputRoot = (Resolve-Path -LiteralPath $OutputRoot).Path
$packages = @("$prefix-setup.exe","$prefix-portable.zip","$prefix-source.zip")
$immutable = $packages + @($metadata.ChecksumFile)
$names = $immutable + @('Clarity-update.json')
foreach ($name in $names) { if (-not (Test-Path -LiteralPath (Join-Path $OutputRoot $name) -PathType Leaf)) { throw "Missing release asset: $name" } }
$checksums = @{}
foreach ($line in Get-Content -LiteralPath (Join-Path $OutputRoot $metadata.ChecksumFile)) {
    if ($line -notmatch '^([0-9a-fA-F]{64})  (.+)$') { throw 'Invalid checksum manifest.' }
    $checksums[$Matches[2]] = $Matches[1].ToLowerInvariant()
}
if ($checksums.Count -ne 3) { throw 'Expected three artifact checksums.' }
foreach ($name in $packages) {
    if ((Get-FileHash -LiteralPath (Join-Path $OutputRoot $name) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $checksums[$name]) { throw "Checksum mismatch: $name" }
}
$manifest = Get-Content -LiteralPath (Join-Path $OutputRoot 'Clarity-update.json') -Raw | ConvertFrom-Json
if ($manifest.Version -ne $tag -or $manifest.Build -ne $metadata.Build -or $manifest.Schema -ne 1) { throw 'Invalid software update manifest.' }
function Invoke-Gh([string[]]$Arguments) {
    $result = & $GitHubCli @Arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub command failed: $($Arguments[0])" }
    return $result
}
$records = Invoke-Gh -Arguments @('api',"repos/$Repository/releases?per_page=100",'--header','Cache-Control: no-cache') | Out-String | ConvertFrom-Json
$record = @($records | Where-Object tag_name -eq $tag) | Select-Object -First 1
if (-not $record) {
    Invoke-Gh -Arguments @('release','create',$tag,'--repo',$Repository,'--verify-tag','--draft','--title',"Clarity $tag",'--notes-file',$NotesFile) | Out-Null
    $records = Invoke-Gh -Arguments @('api',"repos/$Repository/releases?per_page=100",'--header','Cache-Control: no-cache') | Out-String | ConvertFrom-Json
    $record = @($records | Where-Object tag_name -eq $tag) | Select-Object -First 1
    if (-not $record) { throw 'The release draft could not be located.' }
}
$maximum = 0
foreach ($asset in $record.assets) { if ($asset.name -match ('^Clarity-'+[regex]::Escape($tag)+'-build([0-9]+)-')) { $maximum = [Math]::Max($maximum,[int]$Matches[1]) } }
if ($maximum -gt $metadata.Build) { throw 'A newer internal build is already published.' }
foreach ($name in $immutable) {
    $old = @($record.assets | Where-Object name -eq $name)
    $hash = (Get-FileHash -LiteralPath (Join-Path $OutputRoot $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($old.Count -gt 0) {
        if ($old.Count -ne 1 -or $old[0].digest -ne "sha256:$hash") { throw "Internal build assets are immutable; increment the build number: $name" }
        continue
    }
    Invoke-Gh -Arguments @('release','upload',$tag,(Join-Path $OutputRoot $name),'--repo',$Repository) | Out-Null
}
# The current-build pointer is replaced only after the immutable packages exist.
Invoke-Gh -Arguments @('release','upload',$tag,(Join-Path $OutputRoot 'Clarity-update.json'),'--clobber','--repo',$Repository) | Out-Null
$confirmed = $false
for ($attempt=0; $attempt -lt 6; $attempt++) {
    $record = Invoke-Gh -Arguments @('api',"repos/$Repository/releases/$($record.id)",'--header','Cache-Control: no-cache') | Out-String | ConvertFrom-Json
    $confirmed = $true
    foreach ($name in $names) {
        $asset = @($record.assets | Where-Object name -eq $name)
        $file = Get-Item -LiteralPath (Join-Path $OutputRoot $name)
        $hash = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($asset.Count -ne 1 -or $asset[0].size -ne $file.Length -or $asset[0].digest -ne "sha256:$hash") { $confirmed = $false }
    }
    if ($confirmed) { break }
    Start-Sleep -Seconds 2
}
if (-not $confirmed) { throw 'The published files did not match the build artifacts.' }
Invoke-Gh -Arguments @('release','edit',$tag,'--repo',$Repository,'--draft=false','--prerelease=false','--latest=true') | Out-Null
$latest = Invoke-Gh -Arguments @('api',"repos/$Repository/releases/latest",'--header','Cache-Control: no-cache') | Out-String | ConvertFrom-Json
if ($latest.tag_name -ne $tag -or $latest.draft -or $latest.prerelease) { throw 'Latest release was not confirmed.' }
Write-Output $latest.html_url
