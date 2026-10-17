param(
    [string]$Repository='CrazyHandin/Clarity',
    [Parameter(Mandatory=$true)][string]$OutputRoot,
    [string]$GitHubCli='gh'
)
$ErrorActionPreference='Stop'
if($Repository -cne 'CrazyHandin/Clarity'){throw 'The update channel is pinned to the official repository.'}
$OutputRoot=(Resolve-Path -LiteralPath $OutputRoot).Path
$file=Join-Path $OutputRoot 'Clarity-update.json'
$bytes=[IO.File]::ReadAllBytes($file)
if($bytes.Length -gt 65536){throw 'Update manifest is too large.'}
$raw=[Text.UTF8Encoding]::new($false,$true).GetString($bytes)
$manifest=$raw|ConvertFrom-Json
if($manifest.Schema -ne 1 -or $manifest.Build -lt 1 -or -not $manifest.SignedPayload -or -not $manifest.Signature){throw 'Expected a signed update manifest.'}
$api="repos/$Repository"
$scratch=Join-Path $OutputRoot 'update-channel-api'
New-Item -ItemType Directory -Path $scratch -Force|Out-Null
function Invoke-Api([string]$Method,[string]$Path,$Body=$null,[switch]$Missing){
    $arguments=@('api','--method',$Method,$Path)
    if($Method -eq 'GET'){$arguments[3]+='?verify='+[Guid]::NewGuid().ToString('N')}
    if($null -ne $Body){
        $inputFile=Join-Path $scratch ('request-'+[Guid]::NewGuid().ToString('N')+'.json')
        [IO.File]::WriteAllText($inputFile,($Body|ConvertTo-Json -Depth 12 -Compress),[Text.UTF8Encoding]::new($false))
        $arguments+=@('--input',$inputFile)
    }
    $output=& $GitHubCli @arguments 2>$null
    if($LASTEXITCODE -ne 0){
        if($Missing){try{if(($output|Out-String|ConvertFrom-Json).status -eq 404){return $null}}catch{}}
        throw "GitHub request failed: $Method $Path"
    }
    return ($output|Out-String|ConvertFrom-Json)
}
function Confirm-Release {
    $release=Invoke-Api 'GET' "$api/releases/latest"
    $asset=@($release.assets|Where-Object name -ceq 'Clarity-update.json')
    $digest='sha256:'+(Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
    if($release.tag_name -cne $manifest.Version -or $release.draft -or $release.prerelease -or $asset.Count -ne 1 -or $asset[0].size -ne $bytes.Length -or $asset[0].digest -cne $digest){throw 'The channel must match the verified Latest release manifest.'}
}
Confirm-Release
$branch=Invoke-Api 'GET' "$api/git/ref/heads/updates" -Missing
$parent=$null;$tree=$null;$existing=$null
if($branch){
    $parent=$branch.object.sha
    $commit=Invoke-Api 'GET' "$api/git/commits/$parent"
    $tree=$commit.tree.sha
    $listing=Invoke-Api 'GET' "$api/git/trees/$tree"
    $existing=@($listing.tree|Where-Object path -ceq 'Clarity-update.json')|Select-Object -First 1
}
$header=[Text.Encoding]::UTF8.GetBytes(('blob '+$bytes.Length+[char]0))
$blobBytes=New-Object byte[] ($header.Length+$bytes.Length)
[Array]::Copy($header,0,$blobBytes,0,$header.Length);[Array]::Copy($bytes,0,$blobBytes,$header.Length,$bytes.Length)
$sha=[Security.Cryptography.SHA1]::Create()
try{$blobHash=([BitConverter]::ToString($sha.ComputeHash($blobBytes))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
if(-not $existing -or $existing.sha -cne $blobHash){
    $body=@{tree=@(@{path='Clarity-update.json';mode='100644';type='blob';content=$raw})}
    if($tree){$body.base_tree=$tree}
    $createdTree=Invoke-Api 'POST' "$api/git/trees" $body
    $entry=@($createdTree.tree|Where-Object path -ceq 'Clarity-update.json')|Select-Object -First 1
    if(-not $entry -or $entry.sha -cne $blobHash){throw 'Update channel bytes changed while preparing the Git tree.'}
    $body=@{message=('Update Clarity '+$manifest.Version+' build '+$manifest.Build);tree=$createdTree.sha;parents=@()}
    if($parent){$body.parents=@($parent)}
    $commit=Invoke-Api 'POST' "$api/git/commits" $body
    Confirm-Release
    if($parent){$ref=Invoke-Api 'PATCH' "$api/git/refs/heads/updates" @{sha=$commit.sha;force=$false}}
    else{$ref=Invoke-Api 'POST' "$api/git/refs" @{ref='refs/heads/updates';sha=$commit.sha}}
    if($ref.object.sha -cne $commit.sha){throw 'Update channel ref mismatch.'}
    $parent=$commit.sha
}
$confirmed=Invoke-Api 'GET' "$api/git/ref/heads/updates"
if($confirmed.object.sha -cne $parent){throw 'The update channel changed concurrently.'}
$blob=Invoke-Api 'GET' "$api/git/blobs/$blobHash"
$published=[Convert]::FromBase64String($blob.content)
if([Convert]::ToBase64String($published) -cne [Convert]::ToBase64String($bytes)){throw 'Published channel bytes do not match the release.'}
[IO.File]::WriteAllText((Join-Path $OutputRoot 'published-update-channel.json'),([ordered]@{Version=$manifest.Version;Build=$manifest.Build;Commit=$parent;Blob=$blobHash;VerifiedAt=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
Write-Output "Published signed update channel: $($manifest.Version) build $($manifest.Build)"
