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
$payloads = $packages + @($metadata.ChecksumFile)
$names = $payloads + @('Clarity-update.json')
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
function Get-AssetBuild([string]$Name,[string]$ReleaseTag) {
    if ($Name -cmatch ('^Clarity-'+[regex]::Escape($ReleaseTag)+'-build([0-9]{1,5})-(?:setup\.exe|portable\.zip|source\.zip)$')) { return [int]$Matches[1] }
    if ($Name -cmatch '^SHA256SUMS-build([0-9]{1,5})\.txt$') { return [int]$Matches[1] }
    return 0
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
foreach ($asset in $record.assets) { $maximum = [Math]::Max($maximum,(Get-AssetBuild $asset.name $tag)) }
if ($maximum -gt $metadata.Build) { throw 'A newer internal build is already published.' }
$previousBuild = 0
if (@($record.assets | Where-Object name -eq 'Clarity-update.json').Count -gt 0) {
    $previousFile = Join-Path $OutputRoot ('publish-state-'+[Guid]::NewGuid().ToString('N')+'.json')
    try {
        Invoke-Gh -Arguments @('release','download',$tag,'--repo',$Repository,'--pattern','Clarity-update.json','--output',$previousFile) | Out-Null
        $previous = Get-Content -LiteralPath $previousFile -Raw | ConvertFrom-Json
        if ($previous.Schema -ne 1 -or $previous.Version -ne $tag -or $previous.Build -lt 1 -or $previous.Build -gt 65535) { throw 'Invalid existing update manifest.' }
        $previousBuild = [int]$previous.Build
        if ($previousBuild -gt $metadata.Build) { throw 'A newer internal build is already published.' }
    } finally { if (Test-Path -LiteralPath $previousFile) { Remove-Item -LiteralPath $previousFile -Force } }
}
foreach ($name in $payloads) {
    $old = @($record.assets | Where-Object name -eq $name)
    $hash = (Get-FileHash -LiteralPath (Join-Path $OutputRoot $name) -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($old.Count -gt 0) {
        if ($old.Count -ne 1) { throw "Duplicate release attachment: $name" }
        if ($old[0].digest -eq "sha256:$hash") { continue }
        if ($previousBuild -lt 1 -or $previousBuild -ge $metadata.Build) { throw "Increment the internal build before replacing a package: $name" }
        Invoke-Gh -Arguments @('release','upload',$tag,(Join-Path $OutputRoot $name),'--clobber','--repo',$Repository) | Out-Null
        continue
    }
    Invoke-Gh -Arguments @('release','upload',$tag,(Join-Path $OutputRoot $name),'--repo',$Repository) | Out-Null
}
# Replace the current-build pointer after all packages are uploaded.
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
Invoke-Gh -Arguments @('release','edit',$tag,'--repo',$Repository,'--draft=false','--prerelease=false','--latest=true','--title',"Clarity $tag",'--notes-file',$NotesFile) | Out-Null
$latest = Invoke-Gh -Arguments @('api',"repos/$Repository/releases/latest",'--header','Cache-Control: no-cache') | Out-String | ConvertFrom-Json
if ($latest.tag_name -ne $tag -or $latest.draft -or $latest.prerelease) { throw 'Latest release was not confirmed.' }
# Retire older build files only after the new packages and current-build pointer are verified.
$obsolete = @($record.assets | Where-Object { $build = Get-AssetBuild $_.name $tag; $build -gt 0 -and $build -le $metadata.Build -and $_.name -notin $names })
foreach ($asset in $obsolete) {
    Invoke-Gh -Arguments @('api','--method','DELETE',"repos/$Repository/releases/assets/$($asset.id)") | Out-Null
}
if ($obsolete.Count -gt 0) {
    $remaining = Invoke-Gh -Arguments @('api',"repos/$Repository/releases/$($record.id)",'--header','Cache-Control: no-cache') | Out-String | ConvertFrom-Json
    if (@($remaining.assets | Where-Object { $build = Get-AssetBuild $_.name $tag; $build -gt 0 -and $build -le $metadata.Build -and $_.name -notin $names }).Count -gt 0) { throw 'Older build attachments were not fully removed.' }
    foreach ($name in $names) {
        $before = @($record.assets | Where-Object name -eq $name)
        $after = @($remaining.assets | Where-Object name -eq $name)
        if ($after.Count -ne 1 -or $after[0].id -ne $before[0].id -or $after[0].digest -ne $before[0].digest) { throw "Current build asset changed during cleanup: $name" }
    }
}
& (Join-Path $PSScriptRoot 'Publish-UpdateChannel.ps1') -Repository $Repository -OutputRoot $OutputRoot -GitHubCli $GitHubCli | Out-Host
Write-Output $latest.html_url
