using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace ClearSpace {
public static class HeuristicScanner {
 public static Rule CacheRule(string id,string directory,string owner,string[] processes){return new Rule(id,"疑似缓存 · "+owner,"软件缓存","按缓存目录特征识别并读取目录全部文件，包括无扩展名、二进制与子目录内容。普通候选保留最近 1 天及个人资料；保留内容可在高级模式单独自选。","无法确认每个文件可重建；可能重置软件状态、丢失离线内容。默认不选，清理前核对路径，所有未知缓存强制先备份。",new[]{directory},1,false,processes){Software=owner,Part=Path.GetFileName(directory)+"（疑似）",GroupOverride="软件缓存",Heuristic=true,Notice=true,AllowManual=true,ManualIgnoresPersonalNames=true,ExcludedNames=PathPolicy.SensitiveNames,Detection="启发式依据：缓存目录名与所在应用目录；读取全部文件并区分普通候选、需风险确认的保留内容与不可处理文件。\n无法确认可重建，不进入一键清理。"};}
 public static bool LooksPersonal(string path){try{using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){byte[] head=new byte[32];int n=stream.Read(head,0,head.Length);if(n<4)return false;string text=Encoding.ASCII.GetString(head,0,n);return text.StartsWith("%PDF")||text.StartsWith("SQLite format 3")||head[0]==0x50&&head[1]==0x4b||head[0]==0x4d&&head[1]==0x5a||head[0]==0x89&&head[1]==0x50||head[0]==0xff&&head[1]==0xd8||text.StartsWith("{\\rtf");}}catch{return true;}}
 public static List<Rule> Discover(DiscoveryResult discovery,IEnumerable<Rule> known,CancellationToken token,Action<string> status,ScanSession session=null){
  string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
  var starts=new[]{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),Path.Combine(profile,"AppData","LocalLow"),Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}.Concat(discovery.Applications.Where(a=>a.Evidence.Contains("自选便携目录")).Select(a=>a.InstallDirectory)).Where(p=>!String.IsNullOrWhiteSpace(p)&&p.StartsWith(RuleBook.TargetDrive,StringComparison.OrdinalIgnoreCase));
  return DiscoverUnder(starts,discovery,known,token,status,session);
 }
 internal static List<Rule> DiscoverUnder(IEnumerable<string> bases,DiscoveryResult discovery,IEnumerable<Rule> known,CancellationToken token,Action<string> status,ScanSession session=null,int directoryLimit=24000,int ruleLimit=1024){
  var report=new CacheSearchReport();var rules=new List<Rule>();var roots=known.Where(r=>!r.OnlyTop&&!r.Single).SelectMany(r=>r.Roots).Where(p=>!String.IsNullOrWhiteSpace(p)).Select(Engine.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var queue=new Queue<Tuple<string,string,int>>();var scopes=bases.Where(p=>!String.IsNullOrWhiteSpace(p)).Select(Engine.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();var merged=new Dictionary<string,Rule>(StringComparer.OrdinalIgnoreCase);var identities=discovery.Applications.Select(a=>new{App=a,Text=a.SearchText}).ToArray();var labels=new Dictionary<string,Tuple<string,string[]>>(StringComparer.OrdinalIgnoreCase);
  var knownScopes=new HashSet<string>(roots,StringComparer.OrdinalIgnoreCase);var excluded=PathPolicy.CacheExclusions.Where(p=>!String.IsNullOrWhiteSpace(p)).Select(Engine.Normalize).ToArray();
  foreach(string root in scopes)if(Directory.Exists(root))queue.Enqueue(Tuple.Create(root,"",0));
  while(queue.Count>0){
   token.ThrowIfCancellationRequested();if(report.Directories>=directoryLimit||rules.Count>=ruleLimit||report.Caches>=2048){report.Limited=true;break;}
   var item=queue.Dequeue();string current=Engine.Normalize(item.Item1);if(!seen.Add(current))continue;
   try{
    if(!scopes.Any(p=>Inside(current,p))||excluded.Any(p=>Inside(current,p))||!Engine.SafeAncestors(Path.Combine(current,"probe")))continue;
    report.Directories++;if(item.Item3>0&&Covered(current,knownScopes))continue;string name=Path.GetFileName(current);
    if(item.Item3>0&&PathPolicy.CacheName(name)&&PathPolicy.CacheBoundary(current)){
     // A known parent owns its whole cache, but a known child must not hide sibling caches.
     if(!Covered(current,knownScopes)){
      Tuple<string,string[]> label;if(!labels.TryGetValue(item.Item2,out label)){var owners=identities.Where(a=>!String.IsNullOrWhiteSpace(item.Item2)&&a.Text.IndexOf(item.Item2,StringComparison.OrdinalIgnoreCase)>=0).Take(5).Select(a=>a.App).ToArray();string app=owners.Length>0?owners[0].Name:item.Item2;var processes=owners.Select(a=>Path.GetFileNameWithoutExtension(a.Executable)).Where(p=>!String.IsNullOrEmpty(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(processes.Length==0)processes=new[]{item.Item2};label=Tuple.Create(String.IsNullOrWhiteSpace(app)?name:app,processes);labels[item.Item2]=label;}
      string key=label.Item1+"\0"+name;Rule rule;if(!merged.TryGetValue(key,out rule)||rule.Roots.Length>=32){rule=CacheRule("heuristic-"+rules.Count,current,label.Item1,label.Item2);rule.ExcludedRoots=new string[0];rule.Detection="AppData 扩展识别：Local / Roaming / LocalLow 与应用缓存结构；目录名为 "+name+"。用途未经应用规则确认，默认不选并强制先备份。";rules.Add(rule);merged[key]=rule;}else rule.Roots=rule.Roots.Concat(new[]{current}).ToArray();
      rule.ExcludedRoots=rule.ExcludedRoots.Concat(roots.Where(r=>Inside(r,current)&&!r.Equals(current,StringComparison.OrdinalIgnoreCase))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();report.Caches++;continue;
     }
     continue;
    }
    if(item.Item3>=12){if(Directory.EnumerateDirectories(current).Any(p=>!Skip(Path.GetFileName(p))))report.Limited=true;continue;}
    var children=Directory.EnumerateDirectories(current).Where(p=>!Skip(Path.GetFileName(p))).Take(513).Select(p=>new DirectoryInfo(p)).ToArray();if(children.Length>512)report.Limited=true;
    foreach(var child in children.Take(512).OrderBy(d=>Priority(d.Name)).ThenBy(d=>d.Name,StringComparer.OrdinalIgnoreCase)){
     token.ThrowIfCancellationRequested();if((child.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0||Skip(child.Name))continue;
     string owner=item.Item2;if(item.Item3==0||String.IsNullOrEmpty(owner)||new[]{"Microsoft","Tencent","Packages","Adobe","LocalCache","LocalState","RoamingState"}.Contains(owner,StringComparer.OrdinalIgnoreCase))owner=child.Name;
     if(Path.GetFileName(current).Equals("Packages",StringComparison.OrdinalIgnoreCase))owner=child.Name.Split('_')[0];queue.Enqueue(Tuple.Create(child.FullName,owner,item.Item3+1));
    }
   }catch(OperationCanceledException){throw;}catch{report.Unreadable++;}
   if(status!=null&&report.Directories%256==0)status("扩展识别 AppData 缓存 · 已检查 "+report.Directories+" 个目录");
  }
  discovery.CacheSearch=report;if(session!=null){session.Statistics.AppDataDirectories=report.Directories;session.Statistics.AppDataCacheRules=rules.Count;session.Statistics.AppDataLimited=report.Limited;}return rules;
 }
 static bool Inside(string path,string root){return path.Equals(root,StringComparison.OrdinalIgnoreCase)||path.Length>root.Length&&path[root.Length]==Path.DirectorySeparatorChar&&path.StartsWith(root,StringComparison.OrdinalIgnoreCase);}
 static bool Covered(string path,HashSet<string> roots){string current=path;while(true){if(roots.Contains(current))return true;int split=current.LastIndexOf(Path.DirectorySeparatorChar);if(split<2)return false;current=current.Substring(0,split);}}
 static int Priority(string name){return PathPolicy.CacheName(name)?0:new[]{"EBWebView","User Data","Default","Partitions","LocalCache","LocalState","profiles"}.Contains(name,StringComparer.OrdinalIgnoreCase)?1:2;}
 static bool Skip(string name){return name.StartsWith("sandbox.",StringComparison.OrdinalIgnoreCase)||AppDataRules.ProtectedFolders.Contains(name,StringComparer.OrdinalIgnoreCase)||new[]{"Programs","Package Cache","Installer","SystemData","WindowsApps","ConnectedDevicesPlatform","Identity","IdentityCache","TokenBroker","OneAuth","AAD","AuthBroker","Microsoft.AAD.BrokerPlugin",".git","node_modules","ClarityBackups","ClearSpaceBackups","System Volume Information","WeChat Files","xwechat_files","Tencent Files"}.Any(n=>name.Equals(n,StringComparison.OrdinalIgnoreCase)||name.StartsWith(n+"_",StringComparison.OrdinalIgnoreCase));}
}
public sealed class ScanCatalog {public List<Rule> Rules;public DiscoveryResult Discovery;public string RuleVersion,Trust;}
public static class DiscoveryCatalog {
 public static ScanCatalog Build(CleanerSettings settings,bool heuristic,CancellationToken token,Action<string> status,ScanSession session=null){var discovery=ApplicationDiscovery.Scan(settings.PortableDirectories,token,status,session);if(status!=null)status(discovery.Summary+" · 匹配路径规则…");var rules=RuleBook.Create();foreach(var rule in rules){var apps=discovery.Applications.Where(a=>ApplicationDiscovery.Matches(a,new[]{rule.Software??rule.Title},rule.Processes.Select(p=>p+".exe").ToArray(),null)).Take(3).ToArray();rule.Detection=apps.Length>0?String.Join("\n",apps.Select(RuleStore.Describe)):"标准路径规则；未核对到安装指纹。缓存目录存在不等于软件仍已安装。";}
  var external=ExternalRuleSources.Resolve(settings,discovery,token,status);rules.AddRange(external.Rules);string version=external.RuleVersion,trust=external.Trust;if(heuristic){if(status!=null)status("按缓存特征寻找未知目录…");rules.AddRange(HeuristicScanner.Discover(discovery,rules,token,status,session));}return new ScanCatalog{Rules=rules,Discovery=discovery,RuleVersion=version,Trust=trust};}
}
}
