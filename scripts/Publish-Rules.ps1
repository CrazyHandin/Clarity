param(
 [Parameter(Mandatory=$true)][string]$RulesFile,
 [Parameter(Mandatory=$true)][string]$KeyFile,
 [string]$OutputFile=(Join-Path (Split-Path -Parent $PSScriptRoot) 'rules/rules.signed.json')
)
$ErrorActionPreference='Stop'
$ruleText=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $RulesFile).Path)
$pack=$ruleText | ConvertFrom-Json
if ($pack.Schema -notin @(1,2) -or -not $pack.Version -or -not $pack.Rules) {throw 'Invalid rule pack'}
$parameters=New-Object Security.Cryptography.CspParameters
$parameters.ProviderType=24
$parameters.Flags=[Security.Cryptography.CspProviderFlags]::CreateEphemeralKey
$rsa=New-Object Security.Cryptography.RSACryptoServiceProvider -ArgumentList 2048,$parameters
$rsa.PersistKeyInCsp=$false
try {
 $rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $KeyFile).Path))
 $payload=[Text.Encoding]::UTF8.GetBytes($ruleText)
 $signature=$rsa.SignData($payload,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
 $envelope=@{Payload=[Convert]::ToBase64String($payload);Signature=[Convert]::ToBase64String($signature)} | ConvertTo-Json -Compress
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputFile),$envelope,(New-Object Text.UTF8Encoding $false))
 Write-Output ('Signed '+$pack.Version+' / '+$pack.Rules.Count+' rules -> '+[IO.Path]::GetFullPath($OutputFile))
} finally {$rsa.Dispose()}
