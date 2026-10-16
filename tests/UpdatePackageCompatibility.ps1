param(
 [Parameter(Mandatory=$true)][string]$BaselineExecutable,
 [Parameter(Mandatory=$true)][string]$ReleaseDirectory,
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [string]$RejectedArchive
)
$ErrorActionPreference='Stop'
# Run in Windows PowerShell 5.1 so the published .NET Framework assembly is used intact.
if($PSVersionTable.PSEdition -eq 'Core'){throw 'Run with Windows PowerShell 5.1'}
if((Get-FileHash -LiteralPath $BaselineExecutable -Algorithm SHA256).Hash -ne 'F470ADA42ECCD2C10E002037415BBA4180997F9689BAFB2E46ABB0449379224B'){throw 'Expected the authenticated 1.0.6 build 6 production executable'}
if(Test-Path -LiteralPath $OutputDirectory){throw 'Use a fresh output directory for generated fixtures'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
[void][Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($BaselineExecutable))
$checks=New-Object 'System.Collections.Generic.List[string]'
function Check([bool]$Passed,[string]$Label){if(-not $Passed){throw "FAILED: $Label"};$checks.Add($Label)}
if($RejectedArchive){
 $rejected=$false
 try{[void][ClearSpace.UpdatePackage]::Extract($RejectedArchive,[ClearSpace.SoftwareUpdates]::HashFile($RejectedArchive),(Join-Path $OutputDirectory 'rejected-payload'))}
 catch{if($_.Exception.ToString().Contains('rules/RulePack.template.json')){$rejected=$true}else{throw}}
 Check $rejected 'Published 1.0.6 reproduces the original unsupported-template failure'
}
$manifest=Join-Path $ReleaseDirectory 'Clarity-update.json'
$release=[ClearSpace.SoftwareUpdates]::ParseDownloadManifest([ClearSpace.UpdateTrust]::Verify([IO.File]::ReadAllText($manifest),[ClearSpace.UpdateTrust]::PublicKey))
Check ($release.IsNewer('1.0.6',6)) 'Published 1.0.6 accepts the signed newer release version'
$asset=@($release.Assets | Where-Object { $_.Name.EndsWith('-portable.zip') })[0]
$archive=Join-Path $ReleaseDirectory $asset.Name
Check ((Get-Item -LiteralPath $archive).Length -eq $asset.Size -and [ClearSpace.SoftwareUpdates]::HashFile($archive) -eq $asset.SHA256) 'Portable archive matches the publisher-signed size and hash'
$baseline=Split-Path -Parent $BaselineExecutable
$guide=(-join ([char[]]@(0x4F7F,0x7528,0x8BF4,0x660E)))+'.txt'
$runtime=@('Clarity.exe','Clarity.exe.config','Clarity.UpdateService.exe','Clarity.UpdateService.exe.config',$guide,'rules/Winapp2.ini')
foreach($mode in @('portable','installed')){
 $target=Join-Path $OutputDirectory $mode
 New-Item -ItemType Directory -Path $target | Out-Null
 foreach($name in $runtime){$file=Join-Path $target $name;New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null;Copy-Item -LiteralPath (Join-Path $baseline $name) -Destination $file}
 $sentinels=@('Logs/history.json','ClarityBackups/preserve.txt','unins000.exe','unins000.dat')
 foreach($name in $sentinels){$file=Join-Path $target $name;New-Item -ItemType Directory -Path (Split-Path -Parent $file) -Force | Out-Null;[IO.File]::WriteAllText($file,'preserved fixture; never execute')}
 $payload=Join-Path $OutputDirectory ($mode+'-payload')
 $verified=[ClearSpace.UpdatePackage]::Extract($archive,$asset.SHA256,$payload)
 Check ($verified.Count -eq 26) "$mode package extracts using the unmodified 1.0.6 updater"
 Check ([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payload 'Clarity.exe')).ProductVersion -eq $release.Version+'+build.'+$release.Build) "$mode payload version matches the signed release"
 if($mode -eq 'installed'){$verified=[ClearSpace.UpdatePackage]::ForInstalledApplication($verified);Check ($verified.Count -eq 6) 'Installed updates select only the six runtime files'}
 [ClearSpace.UpdatePackage]::Apply($target,$verified,$null)
 foreach($name in $runtime){Check ([ClearSpace.SoftwareUpdates]::HashFile((Join-Path $target $name)) -eq [ClearSpace.SoftwareUpdates]::HashFile((Join-Path $payload $name))) "$mode runtime file replaced: $name"}
 foreach($name in $sentinels){Check ([IO.File]::ReadAllText((Join-Path $target $name)) -eq 'preserved fixture; never execute') "$mode user or uninstall file preserved: $name"}
 if($mode -eq 'installed'){Check (-not (Test-Path -LiteralPath (Join-Path $target 'README.md'))) 'Installed updates do not add portable Markdown documents'}
}
$checks | ForEach-Object { Write-Output ('PASS: '+$_) }
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'compatibility-results.json'),(@{Baseline='1.0.6+build.6';Version=$release.Version;Build=$release.Build;Checks=$checks;Passed=$true}|ConvertTo-Json -Depth 4),[Text.UTF8Encoding]::new($false))
Write-Output ('ALL '+$checks.Count+' LEGACY UPDATE CHECKS PASSED. Only generated fixtures were changed.')
