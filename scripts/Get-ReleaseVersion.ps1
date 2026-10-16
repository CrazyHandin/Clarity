$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$definition = Get-Content -LiteralPath (Join-Path $root 'version.json') -Raw | ConvertFrom-Json
$build = if ($null -ne $definition.build) { [int]$definition.build } else { 0 }
if ($build -lt 0 -or $build -gt 65535) { throw 'Invalid release version.' }
$isBeta = $definition.channel -ceq 'beta'
if ($isBeta) {
    if ($definition.number -lt 1 -or $definition.number -gt 65535) { throw 'Invalid beta version.' }
    $tag = 'beta0.{0:00}' -f [int]$definition.number
    $major = 0
    $minor = [int]$definition.number
} elseif ($definition.channel -ceq 'stable') {
    if ([string]$definition.version -cnotmatch '\A([1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})\z') { throw 'Invalid stable version; expected major.minor.patch.' }
    $major = [int]$Matches[1]
    $minor = [int]$Matches[2]
    $patch = [int]$Matches[3]
    if ($major -gt 65535 -or $minor -gt 65535 -or $patch -gt 65535) { throw 'Invalid stable version.' }
    $tag = [string]$definition.version
} else { throw 'Invalid release channel.' }
$artifactTag = $tag
[pscustomobject]@{
    Tag = $tag
    IsBeta = $isBeta
    Build = $build
    Highlights = $definition.highlights
    FullVersion = $(if ($build -gt 0) { "$tag+build.$build" } else { $tag })
    NumericVersion = $(if ($isBeta) { '0.{0}.{1}.0' -f $minor,$build } else { '{0}.{1}.{2}.{3}' -f $major,$minor,$patch,$build })
    ArtifactTag = $artifactTag
    AssetPrefix = "Clarity-$artifactTag"
    ChecksumFile = 'SHA256SUMS.txt'
}
