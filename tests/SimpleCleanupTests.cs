using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace ClearSpace {
public static class SimpleCleanupTests {
 public static void Verify(string root,Action<bool,string> check){
  var catalog=RuleBook.Create();string[] added={"nvidia","d3d","intel-shader","amd-shader","go-build","app-diagnostic-3","chrome-reports","code-crash-reports","wechat-diagnostics"};
  check(added.All(id=>ScanModes.IsQuickRule(catalog.Single(r=>r.Id==id)))&&ScanModes.QuickRules().Count>60,"Simple cleanup includes a reviewed set of shader, compiler and older diagnostic caches");
  check(catalog.Where(r=>r.Quick).All(r=>!r.Privacy&&!r.Protected&&!r.Heuristic&&!r.ForceBackup&&String.IsNullOrEmpty(r.InfoKind)),"Expanded simple rules retain exclusions for protected content, privacy and system actions");
  check(new[]{"temp","qqmusic","spotify","gradle","adobe-media","adobe-photoshop-uxp-cache","system-hiberfil.sys"}.All(id=>!catalog.Single(r=>r.Id==id).Quick),"Temporary work, offline media, dependency stores, Adobe project caches and hibernation remain advanced-only");
  var fixture=Path.Combine(root,"simple-cleanup-fixture");Directory.CreateDirectory(fixture);string[] names={"old.log","old.xlog","notes.txt","settings.json","saved.bak","disguised.log","recent.log","User\\account.log","old.dmp"};
  foreach(string name in names){string file=Path.Combine(fixture,name);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,name=="disguised.log"?"%PDF personal document":"fixture diagnostic");File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddDays(name=="recent.log"?-1:-30));}
  var rule=new Rule("app-diagnostic-3","Fixture logs","开发缓存","","",new[]{fixture},1,false);SimpleCleanupRules.Apply(new List<Rule>{rule});var engine=new Engine(new List<Rule>{rule}){FileBackupDirectory=Path.Combine(root,"simple-fixture-backups"),UseFileBackup=true};engine.IsRunning=owners=>false;var finding=engine.ScanRule(rule,CancellationToken.None);
  check(finding.Candidates.Count==2&&finding.Candidates.All(s=>s.Path.EndsWith("old.log")||s.Path.EndsWith("old.xlog"))&&rule.Days==14,"Promoted log rules require old known log types and reject documents, configuration, backups, personal folders and disguised personal files");
  var result=engine.Clean(new[]{finding},true,CancellationToken.None,null);check(result.Deleted==2&&result.BackupFiles==2&&File.Exists(Path.Combine(fixture,"notes.txt"))&&File.Exists(Path.Combine(fixture,"recent.log")),"Expanded one-click cleanup backs up selected diagnostics and retains unrelated or recent files");
  var dump=new Rule("code-crash-reports","Fixture dumps","开发缓存","","",new[]{fixture},1,false);SimpleCleanupRules.Apply(new List<Rule>{dump});engine=new Engine(new List<Rule>{dump});engine.IsRunning=owners=>true;check(engine.ScanRule(dump,CancellationToken.None).Candidates.Count==0,"Running owners block newly promoted cleanup rules");engine.IsRunning=owners=>false;finding=engine.ScanRule(dump,CancellationToken.None);check(finding.Candidates.Count==1&&finding.Candidates[0].Path.EndsWith("old.dmp"),"Promoted crash rules match only old dump files");
  var go=new Rule("go-build","Fixture Go cache","开发缓存","","",new[]{fixture},14,false);SimpleCleanupRules.Apply(new List<Rule>{go});string objectFile=Path.Combine(fixture,new string('a',64)+"-d");File.WriteAllText(objectFile,"fixture Go cache");File.SetLastWriteTimeUtc(objectFile,DateTime.UtcNow.AddDays(-30));engine=new Engine(new List<Rule>{go});finding=engine.ScanRule(go,CancellationToken.None);check(finding.Candidates.Count==1&&finding.Candidates[0].Path==objectFile,"Simple Go cleanup selects only hash-named compiled artifacts");
  var imported=new Rule("library-custom","Imported cache","软件缓存","","",new[]{fixture},14,false){ForceBackup=true};SimpleCleanupRules.Apply(new List<Rule>{imported});check(!imported.Quick&&imported.ForceBackup,"Unreviewed imported rule identifiers cannot gain simple-cleanup eligibility");
 }
}
}
