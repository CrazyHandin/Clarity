param([Parameter(Mandatory=$true)][string]$ManifestFile,[Parameter(Mandatory=$true)][string]$KeyFile,[string]$PublicKeyFile=(Join-Path (Split-Path -Parent $PSScriptRoot) 'assets/update-public-key.xml'))
$ErrorActionPreference='Stop'
$manifest=Get-Content -LiteralPath $ManifestFile -Raw | ConvertFrom-Json
if ($manifest.Schema -ne 1 -or $manifest.Build -lt 1 -or @($manifest.Artifacts).Count -ne 2 -or $manifest.SignedPayload -or $manifest.Signature) { throw 'Expected an unsigned Clarity update manifest.' }
$payload=[Text.Encoding]::UTF8.GetBytes(($manifest | ConvertTo-Json -Depth 8 -Compress))
$parameters=[Security.Cryptography.CspParameters]::new()
$parameters.ProviderType=24
$parameters.Flags=[Security.Cryptography.CspProviderFlags]::CreateEphemeralKey
$rsa=[Security.Cryptography.RSACryptoServiceProvider]::new(3072,$parameters)
$rsa.PersistKeyInCsp=$false
try {
 $rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $KeyFile).Path))
 $public=[Security.Cryptography.RSACryptoServiceProvider]::new()
 try { $public.PersistKeyInCsp=$false; $public.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $PublicKeyFile).Path)); if ($rsa.KeySize -ne 3072 -or $public.ToXmlString($false) -cne $rsa.ToXmlString($false)) { throw 'Update signing key does not match the pinned public key.' } } finally { $public.Dispose() }
 $signature=$rsa.SignData($payload,[Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256'))
 $manifest | Add-Member -NotePropertyName SignedPayload -NotePropertyValue ([Convert]::ToBase64String($payload))
 $manifest | Add-Member -NotePropertyName Signature -NotePropertyValue ([Convert]::ToBase64String($signature))
 [IO.File]::WriteAllText([IO.Path]::GetFullPath($ManifestFile),($manifest | ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
} finally { $rsa.Dispose() }
