using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace ClearSpace {
public static class ExternalRuleSources {
 public static ScanCatalog Resolve(CleanerSettings settings,DiscoveryResult discovery,CancellationToken token,Action<string> status=null,RuleStore json=null,Winapp2Store ini=null,RulePackCatalog catalog=null,Winapp2Environment environment=null){
  var rules=new List<Rule>();var versions=new List<string>();var trust=new List<string>();var errors=new List<string>();token.ThrowIfCancellationRequested();
  if(settings.Winapp2Enabled){var pack=(ini??new Winapp2Store()).Current();rules.AddRange(Winapp2Adapter.Resolve(pack,discovery,environment??Winapp2Environment.Windows(),token,status));versions.Add("Winapp2 "+pack.Version);trust.Add(pack.Origin+" · 社区未签名");}
  if(settings.JsonEnabled){var store=json??new RuleStore();var pack=store.Current();var compiled=store.Resolve(pack,discovery,token);foreach(var rule in compiled)rule.Id="library-primary-"+rule.Id.Substring("library-".Length);rules.AddRange(compiled);versions.Add("JSON "+pack.Pack.Version);trust.Add(pack.Trust);}
  var installed=(catalog??new RulePackCatalog()).List(errors);foreach(var pack in installed.Where(p=>p.Enabled)){token.ThrowIfCancellationRequested();rules.AddRange(RulePackCatalog.Compile(pack,discovery,token));versions.Add(pack.Name+" "+pack.Loaded.Pack.Version);trust.Add(pack.Loaded.Trust);}
  if(errors.Count>0){string warning="跳过 "+errors.Count+" 个无效规则包："+String.Join("；",errors);trust.Add(warning);if(status!=null)status(warning);}
  return new ScanCatalog{Rules=rules,Discovery=discovery,RuleVersion=String.Join(" + ",versions),Trust=String.Join(" · ",trust.Distinct())};
 }
}
}
