using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace ClearSpace {
public static class AppDataCoverageTests {
 public static void Verify(string workspace,Action<bool,string> check){
  string root=Path.Combine(workspace,"appdata-coverage"),local=Path.Combine(root,"Local"),roaming=Path.Combine(root,"Roaming"),low=Path.Combine(root,"LocalLow");Directory.CreateDirectory(root);
  Func<string,string,string> put=(relative,body)=>{string path=Path.Combine(root,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,body);File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-4));return path;};
  string qq=put("Roaming\\QQ\\Cache\\Cache_Data\\data_0","downloaded web cache"),raw=put("Roaming\\QQ\\Cache\\Cache_Data\\f_000001","%PDF downloaded response copy"),code=put("Roaming\\QQ\\Code Cache\\js\\0123456789abcdef_0","compiled script");
  string doc=put("Roaming\\QQ\\Cache\\private.pdf","%PDF personal document"),text=put("Roaming\\QQ\\Cache\\notes.txt","personal note"),account=put("Roaming\\QQ\\Cache\\Network\\Cookies","account"),offline=put("Roaming\\QQ\\Cache\\CacheStorage\\offline.bin","offline content"),chat=put("Roaming\\QQ\\nt_data\\chat.db","chat data"),recent=put("Roaming\\QQ\\Cache\\Cache_Data\\fresh","fresh cache");File.SetLastWriteTimeUtc(recent,DateTime.UtcNow);
  string future=put("Roaming\\QQ\\Cache\\Cache_Data\\future","future-dated cache");File.SetLastWriteTimeUtc(future,DateTime.UtcNow.AddDays(2));
  string custom=put("Local\\Google\\Chrome\\User Data\\Work-Persona\\Cache\\Cache_Data\\data_0","profile cache");put("Local\\Google\\Chrome\\User Data\\Work-Persona\\Preferences","{}");
  string guest=put("Local\\Google\\Chrome\\User Data\\Guest Profile\\GPUCache\\data_0","guest GPU cache");
  string partition=put("Roaming\\QQ\\Partitions\\secondary\\GPUCache\\data_0","partition cache");
  string teams=put("Local\\Packages\\MSTeams_fixture\\LocalCache\\Microsoft\\MSTeams\\EBWebView\\WV2Profile_tfw\\Cache\\Cache_Data\\data_0","Teams cache");
  string song=put("Roaming\\Spotify\\Storage\\track.file","offline song"),spotify=put("Roaming\\Spotify\\GPUCache\\data_0","renderer cache");
  string crash=put("Local\\Codex\\Logs\\old.log","diagnostic log");File.SetLastWriteTimeUtc(crash,DateTime.UtcNow.AddDays(-30));string disguised=put("Local\\Codex\\Logs\\personal.log","%PDF disguised document");File.SetLastWriteTimeUtc(disguised,DateTime.UtcNow.AddDays(-30));
  var rules=new List<Rule>{new Rule("qq-web","QQ","软件缓存","","",new string[0],1,true,"QQ"),new Rule("chrome","Chrome","网页缓存","","",new string[0],1,true,"chrome")};AppDataRules.Add(rules,local,roaming,low);var qqRule=rules.Single(r=>r.Id=="qq-web");qqRule.AllowManual=true;
  check(qqRule.Roots.Any(r=>ScanPolicy.Inside(qq,r))&&qqRule.Roots.Any(r=>ScanPolicy.Inside(partition,r)),"QQ NT root and partition caches join the reviewed built-in AppData provider");
  check(rules.Single(r=>r.Id=="chrome").Roots.Any(r=>ScanPolicy.Inside(custom,r))&&rules.Single(r=>r.Id=="chrome").Roots.Any(r=>ScanPolicy.Inside(guest,r)),"Browser discovery includes marker-confirmed custom profiles and Guest Profile caches");
  check(rules.Single(r=>r.Id=="teams-web").Roots.Any(r=>ScanPolicy.Inside(teams,r)),"Store WebView2 providers find WV2Profile profiles instead of assuming Default");
  var engine=new Engine(rules){IsRunning=owners=>false,UseFileBackup=true,FileBackupDirectory=Path.Combine(root,"backups")};var found=engine.ScanRule(qqRule,CancellationToken.None);
  check(new[]{qq,raw,code,partition}.All(p=>found.Candidates.Any(c=>c.Path==p)),"Reviewed resource caches include raw downloaded response bodies as well as compiled and partition cache entries");
  check(new[]{doc,text,account,offline,chat}.All(p=>!found.Candidates.Any(c=>c.Path==p)&&!found.ManualCandidates.Any(c=>c.Path==p)),"AppData resource rules retain named documents, notes, account stores, offline storage and chats even for manual cleanup");
  check(found.RecentBytes==new FileInfo(recent).Length+new FileInfo(future).Length&&found.FilteredBytes>=new FileInfo(doc).Length+new FileInfo(text).Length,"Scan explanations count each recent or future-dated file once and account for safety-filtered content");
  engine.IsRunning=owners=>owners.Contains("QQ");var running=engine.ScanRule(qqRule,CancellationToken.None);check(running.Running&&running.EligibleBytes==0&&running.RunningBytes==running.TotalBytes&&running.TotalBytes>0,"A running app remains visible as detected cache with a quantified running-state explanation");engine.IsRunning=owners=>false;
  var cleaned=engine.Clean(new[]{found},true,CancellationToken.None,null);check(cleaned.Deleted==4&&cleaned.BackupFiles==4&&new[]{doc,text,account,offline,chat,recent}.All(File.Exists),"One-click AppData cleanup backs up selected cache files and retains all protected and recent fixtures");
  check(FileRecovery.Restore(cleaned.FileBackupManifest)==4&&new[]{qq,raw,code,partition}.All(File.Exists),"New AppData cache candidates round-trip through the existing verified backup format");
  var spotifyRule=rules.Single(r=>r.Id=="appdata-spotify-render");check(spotifyRule.Roots.Any(r=>ScanPolicy.Inside(spotify,r))&&!spotifyRule.Roots.Any(r=>ScanPolicy.Inside(song,r)),"Audio-app one-click providers cover renderer caches without including downloaded music");
  var diagnostics=rules.Single(r=>r.Id=="appdata-codex-diagnostics");found=engine.ScanRule(diagnostics,CancellationToken.None);check(found.Candidates.Count==1&&found.Candidates[0].Path==crash&&File.Exists(disguised),"New diagnostic providers require old log types and reject disguised personal content");
  string deep=put("LocalLow\\Vendor\\Product\\v2\\Host\\EBWebView\\WV2Profile_user\\GPUCache\\data_0","unknown cache");
  string sibling=put("Local\\Vendor\\User Data\\Custom\\ShaderCache\\data_0","sibling cache"),knownFile=put("Roaming\\Other\\Cache\\Managed\\old.bin","known child"),unknown=put("Roaming\\Other\\Cache\\loose.bin","unknown sibling");
  put("Local\\Microsoft\\TokenBroker\\Cache\\token.bin","account token");put("Local\\Packages\\Microsoft.AAD.BrokerPlugin_fixture\\LocalState\\Cache\\token.bin","broker token");put("LocalLow\\Vendor\\Product\\Sessions\\Cache\\session.db","session");
  var known=new[]{new Rule("top-only","Diagnostic","软件缓存","","",new[]{Path.Combine(local,"Vendor\\User Data")},14,false){OnlyTop=true},new Rule("known-child","Cache","软件缓存","","",new[]{Path.GetDirectoryName(knownFile)},1,true)};var discovery=new DiscoveryResult();
  using(var session=new ScanSession()){
   var extra=HeuristicScanner.DiscoverUnder(new[]{local,roaming,low},discovery,known,CancellationToken.None,null,session);
   check(extra.Any(r=>r.Roots.Any(p=>ScanPolicy.Inside(deep,p)))&&extra.Any(r=>r.Roots.Any(p=>ScanPolicy.Inside(sibling,p))),"Expanded discovery reaches deep LocalLow caches and does not hide sibling caches behind a top-only rule");
   check(!extra.Any(r=>r.Roots.Any(p=>p.Contains("TokenBroker")||p.Contains("AAD.BrokerPlugin")||p.Contains("Sessions"))),"AppData discovery excludes authentication brokers, tokens and session stores");
   var parent=extra.Single(r=>r.Roots.Any(p=>ScanPolicy.Inside(unknown,p)));check(parent.ExcludedRoots.Contains(Path.GetDirectoryName(knownFile))&&extra.All(r=>r.Heuristic&&!r.Quick),"Unknown parent caches explicitly exclude known child scopes and never join one-click cleanup");
   var unknownFinding=new Engine(extra){IsRunning=owners=>false,FileBackupDirectory=Path.Combine(root,"unknown-backups")}.ScanRule(parent,CancellationToken.None);check(unknownFinding.Candidates.Any(p=>p.Path==unknown)&&!unknownFinding.Candidates.Any(p=>p.Path==knownFile),"Unknown-cache scanning reads uncovered siblings while preserving the known child boundary");
   check(session.Statistics.AppDataDirectories==discovery.CacheSearch.Directories&&discovery.CacheSearch.Caches==extra.Sum(r=>r.Roots.Length)&&session.Statistics.AppDataCacheRules==extra.Count,"Discovery coverage counters match cache directories as well as merged rules");
  }
  var limited=new DiscoveryResult();HeuristicScanner.DiscoverUnder(new[]{local,roaming,low},limited,known,CancellationToken.None,null,directoryLimit:1);check(limited.CacheSearch.Limited&&limited.CacheSearch.Directories<=1,"A bounded discovery pass explicitly reports an incomplete scope instead of silently truncating");
  bool canceled=false;try{HeuristicScanner.DiscoverUnder(new[]{local},new DiscoveryResult(),known,new CancellationToken(true),null);}catch(OperationCanceledException){canceled=true;}check(canceled,"Expanded AppData discovery honors cancellation before traversing the scope");
  var catalog=RuleBook.Create();check(catalog.Select(r=>r.Id).Distinct().Count()==catalog.Count&&catalog.All(r=>r.Roots.All(p=>!ScanPolicy.Excluded(p))),"Expanded built-in catalog retains unique IDs and excludes the Windows system directory");
 }
}
}
