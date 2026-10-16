using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace ClearSpace {
public static class RulePackValidator {
 public static void Validate(ApplicationRulePack pack){
  Version version;if(pack==null||(pack.Schema!=1&&pack.Schema!=2)||!Version.TryParse(pack.Version,out version)||pack.Rules==null||pack.Rules.Count>500)throw new InvalidDataException("规则格式或版本无效");if(pack.Schema==2&&(!RulePackCatalog.ValidId(pack.PackId)||String.IsNullOrWhiteSpace(pack.Name)))throw new InvalidDataException("Schema 2 规则包需要 PackId 和 Name");
  foreach(string value in new[]{pack.Name,pack.Author,pack.Description})if(value!=null&&(value.Length>1000||value.Any(c=>Char.IsControl(c)&&c!='\n'&&c!='\r'&&c!='\t')))throw new InvalidDataException("规则包说明无效");
  var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var r in pack.Rules){
   if(r==null||!Regex.IsMatch(r.Id??"","^[a-z0-9][a-z0-9-]{1,79}$")||!ids.Add(r.Id)||String.IsNullOrWhiteSpace(r.Name)||r.Name.Length>100||r.Paths==null||r.Paths.Length==0||r.Paths.Length>30||r.KeepDays<7||r.KeepDays>3650||!new[]{"软件缓存","网页缓存","开发缓存"}.Contains(r.Group)||!new[]{"files","contents"}.Contains(r.Method)||((r.MatchNames??new string[0]).Length==0&&(r.ExecutableNames??new string[0]).Length==0))throw new InvalidDataException("规则字段、范围或保留期无效");
   if(r.Conditions!=null){if(pack.Schema<2)throw new InvalidDataException("文件条件需要 Schema 2");RuleConditionEvaluator.Validate(r.Conditions);}
   foreach(string value in new[]{r.Part,r.Description,r.Impact})if(value!=null&&value.Length>2000)throw new InvalidDataException("规则说明过长");
   foreach(var terms in new[]{r.MatchNames,r.ExecutableNames,r.Publishers,r.ProductNames,r.ExcludeNames,r.Extensions})if(terms!=null&&(terms.Length>64||terms.Any(t=>String.IsNullOrWhiteSpace(t)||t.Length>200)))throw new InvalidDataException("指纹及排除条件不可为空或过长");
   if((r.ExecutableNames??new string[0]).Any(n=>n.IndexOfAny(new[]{'\\','/',':','*','?'})>=0||!n.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("程序指纹必须是明确的 EXE 文件名");
   if((r.ExcludePaths??new string[0]).Length>100)throw new InvalidDataException("排除路径过多");
   foreach(string path in r.Paths)PathPolicy.ValidateTemplate(path);foreach(string path in r.ExcludePaths??new string[0])PathPolicy.ValidateTemplate(path,false);
   if(!String.IsNullOrEmpty(r.FileNameRegex)){if(r.FileNameRegex.Length>256)throw new InvalidDataException("正则过长");new Regex(r.FileNameRegex,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100));}
   if((r.Extensions??new string[0]).Any(e=>!Regex.IsMatch(e,"^\\.[a-zA-Z0-9]{1,12}$")))throw new InvalidDataException("扩展名无效");
  }
 }
}
}
