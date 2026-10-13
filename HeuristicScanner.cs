using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
namespace ClearSpace {
public static class HeuristicScanner {
 public static Rule CacheRule(string id,string directory,string owner,string[] processes){return new Rule(id,"疑似缓存 · "+owner,"软件缓存","按缓存目录特征识别并读取目录全部文件，包括无扩展名、二进制与子目录内容。普通候选保留最近 1 天及个人资料；保留内容可在高级模式单独自选。","无法确认每个文件可重建；可能重置软件状态、丢失离线内容。默认不选，清理前核对路径，所有未知缓存强制先备份。",new[]{directory},1,false,processes){Software="未知缓存 · "+owner,Part=Path.GetFileName(directory)+"（疑似）",GroupOverride="软件缓存",Heuristic=true,Notice=true,AllowManual=true,ManualIgnoresPersonalNames=true,ExcludedNames=PathPolicy.SensitiveNames,Detection="启发式依据：缓存目录名与所在应用目录；读取全部文件并区分普通候选、需风险确认的保留内容与不可处理文件。\n无法确认可重建，不进入一键清理。"};}
 public static bool LooksPersonal(string path){try{using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){byte[] head=new byte[32];int n=stream.Read(head,0,head.Length);if(n<4)return false;string text=Encoding.ASCII.GetString(head,0,n);return text.StartsWith("%PDF")||text.StartsWith("SQLite format 3")||head[0]==0x50&&head[1]==0x4b||head[0]==0x4d&&head[1]==0x5a||head[0]==0x89&&head[1]==0x50||head[0]==0xff&&head[1]==0xd8||text.StartsWith("{\\rtf");}}catch{return true;}}
 public static List<Rule> Discover(DiscoveryResult discovery,IEnumerable<Rule> known,CancellationToken token,Action<string> status){
  var rules=new List<Rule>();var knownRoots=known.SelectMany(r=>r.Roots).Select(Engine.Normalize).ToList();var starts=new List<string>();string[] bases={Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)};
  foreach(string root in bases)try{starts.AddRange(Directory.GetDirectories(root).Where(p=>!new[]{"Microsoft","Windows","Packages","Package Cache","Installer","SystemData","Documents","Downloads","Temp","ConnectedDevicesPlatform"}.Contains(Path.GetFileName(p),StringComparer.OrdinalIgnoreCase)).Take(150));}catch{}
  starts.AddRange(discovery.Applications.Where(a=>a.Evidence.Contains("自选便携目录")&&!String.IsNullOrEmpty(a.InstallDirectory)).Select(a=>a.InstallDirectory));var queue=new Queue<Tuple<string,string,int>>();foreach(string root in starts.Distinct(StringComparer.OrdinalIgnoreCase))queue.Enqueue(Tuple.Create(root,Path.GetFileName(root),0));int inspected=0;var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  while(queue.Count>0&&inspected++<12000&&rules.Count<200){token.ThrowIfCancellationRequested();var item=queue.Dequeue();string current=item.Item1;try{if(!Engine.SafeAncestors(Path.Combine(current,"probe"))||!current.StartsWith(RuleBook.TargetDrive,StringComparison.OrdinalIgnoreCase))continue;if(PathPolicy.CacheName(Path.GetFileName(current))&&PathPolicy.AllowedCacheRoot(current)&&item.Item3>0&&!knownRoots.Any(r=>current.Equals(r,StringComparison.OrdinalIgnoreCase)||current.StartsWith(r+"\\",StringComparison.OrdinalIgnoreCase))&&found.Add(current)){
    var owners=discovery.Applications.Where(a=>a.SearchText.IndexOf(item.Item2,StringComparison.OrdinalIgnoreCase)>=0).Take(5).Select(a=>Path.GetFileNameWithoutExtension(a.Executable)).Where(n=>!String.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();if(owners.Length==0)owners=new[]{item.Item2};rules.Add(CacheRule("heuristic-"+rules.Count,current,item.Item2,owners));continue;}
    if(item.Item3<4)foreach(var dir in new DirectoryInfo(current).GetDirectories().Take(200)){if((dir.Attributes&FileAttributes.ReparsePoint)==0&&!PathPolicy.SensitiveNames.Contains(dir.Name,StringComparer.OrdinalIgnoreCase))queue.Enqueue(Tuple.Create(dir.FullName,item.Item2,item.Item3+1));}
   }catch{}if(status!=null&&inspected%300==0)status("寻找未知缓存目录 · 已检查 "+inspected+" 个目录");
  }return rules;
 }
}
public sealed class ScanCatalog {public List<Rule> Rules;public DiscoveryResult Discovery;public string RuleVersion,Trust;}
public static class DiscoveryCatalog {
 public static ScanCatalog Build(CleanerSettings settings,bool heuristic,CancellationToken token,Action<string> status){var discovery=ApplicationDiscovery.Scan(settings.PortableDirectories,token,status);if(status!=null)status(discovery.Summary+" · 匹配路径规则…");var rules=RuleBook.Create();foreach(var rule in rules){var apps=discovery.Applications.Where(a=>ApplicationDiscovery.Matches(a,new[]{rule.Software??rule.Title},rule.Processes.Select(p=>p+".exe").ToArray(),null)).Take(3).ToArray();rule.Detection=apps.Length>0?String.Join("\n",apps.Select(RuleStore.Describe)):"标准路径规则；未核对到安装指纹。缓存目录存在不等于软件仍已安装。";}
  string version,trust;if(settings.UseWinapp2){var pack=new Winapp2Store().Current();rules.AddRange(Winapp2Adapter.Resolve(pack,discovery,Winapp2Environment.Windows(),token,status));version="Winapp2 "+pack.Version;trust=pack.Origin+" · 社区未签名";}else{var store=new RuleStore();var pack=store.Current();rules.AddRange(store.Resolve(pack,discovery));version=pack.Pack.Version;trust=pack.Trust;}if(heuristic){if(status!=null)status("按缓存特征寻找未知目录…");rules.AddRange(HeuristicScanner.Discover(discovery,rules,token,status));}return new ScanCatalog{Rules=rules,Discovery=discovery,RuleVersion=version,Trust=trust};}
}
}
