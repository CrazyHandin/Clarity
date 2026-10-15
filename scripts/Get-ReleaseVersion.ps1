$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$definition = Get-Content -LiteralPath (Join-Path $root 'version.json') -Raw | ConvertFrom-Json
$build = if ($null -ne $definition.build) { [int]$definition.build } else { 0 }
if ($definition.channel -ne 'beta' -or $definition.number -lt 1 -or $definition.number -gt 65535 -or $build -lt 0 -or $build -gt 65535) { throw 'Invalid release version.' }
$tag = 'beta0.{0:00}' -f [int]$definition.number
$artifactTag = if ($build -gt 0) { "$tag-build$build" } else { $tag }
[pscustomobject]@{
    Tag = $tag
    Build = $build
    Highlights = $definition.highlights
    FullVersion = $(if ($build -gt 0) { "$tag+build.$build" } else { $tag })
    NumericVersion = '0.{0}.{1}.0' -f [int]$definition.number,$build
    ArtifactTag = $artifactTag
    AssetPrefix = "Clarity-$artifactTag"
    ChecksumFile = $(if ($build -gt 0) { "SHA256SUMS-build$build.txt" } else { 'SHA256SUMS.txt' })
}
