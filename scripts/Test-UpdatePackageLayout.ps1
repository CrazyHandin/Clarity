param([Parameter(Mandatory=$true)][string[]]$Names)
$ErrorActionPreference='Stop'
# Keep the portable update format compatible with the published 1.0.6 updater.
# New examples and developer files belong in the source archive.
$allowed=@('Clarity.exe','Clarity.exe.config','Clarity.UpdateService.exe','Clarity.UpdateService.exe.config','使用说明.txt','README.md','README.en.md','CHANGELOG.md','THIRD_PARTY_NOTICES.md','LICENSE.txt','VERIFICATION.txt','SHA256.txt','docs/版本历史.md','docs/清理依据.md','docs/常用软件规则.md','docs/清理规则一览.md','docs/规则库维护.md','docs/Winapp2-使用说明.md','docs/Winapp2-License.md','docs/BUILD.md','docs/TERMS.txt','docs/JSON规则填写说明.txt','rules/Winapp2.ini','rules/ApplicationRules.template.json','rules/BundledRules.signed.json','rules/rule-public-key.xml')
$seen=New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
foreach($name in $Names){
 if($allowed -notcontains $name -or -not $seen.Add($name)){throw "Unsupported or duplicate portable update path: $name"}
}
foreach($required in @('Clarity.exe','Clarity.exe.config','Clarity.UpdateService.exe','Clarity.UpdateService.exe.config','使用说明.txt','rules/Winapp2.ini')){
 if(-not $seen.Contains($required)){throw "Missing update runtime file: $required"}
}
