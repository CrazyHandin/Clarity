using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace ClearSpace {
// Curated cache providers. These never enumerate or delete an application data
// directory as a whole, and do not promote imported or heuristic rules.
public static class AppDataRules {
 public const string Chromium="https://www.chromium.org/developers/design-documents/network-stack/disk-cache/";
 public const string WebView="https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder";
 public const string Electron="https://www.electronjs.org/docs/latest/api/session";
 public const string Nvidia="https://nvidia.custhelp.com/app/answers/detail/a_id/5735/~/deleting-nvidia-shader-cache-files";
 internal static readonly string[] ResourceFolders={"Cache","Code Cache","GPUCache","ShaderCache","GrShaderCache","DawnCache","DawnGraphiteCache","DawnWebGPUCache","GraphiteDawnCache"};
 internal static readonly string[] ProtectedFolders=PathPolicy.SensitiveNames.Concat(new[]{"Network","Service Worker","CacheStorage","FileStorage","FileRecv","MsgAttach","nt_qq","nt_data","auth","Downloads","Documents","Projects","AutoSave","AutoRecover","Recovery","Extensions","Plugins","Session","Sessions","Sync Data","WebStorage","agentSessionData","workspaceStorage","globalStorage"}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
 static readonly string[] PersonalExtensions={".db",".sqlite",".sqlite3",".json",".jsonl",".ini",".config",".xml",".yaml",".yml",".txt",".md",".rtf",".csv",".doc",".docx",".xls",".xlsx",".ppt",".pptx",".pdf",".pst",".ost",".psd",".psb",".ai",".aep",".prproj",".indd",".lrcat",".bak",".backup",".sav",".save",".pem",".key",".pfx",".kdbx"};
 static string P(string root,string tail){return Path.Combine(root,tail);}
 static bool PlainDirectory(string path){try{return Directory.Exists(path)&&(File.GetAttributes(path)&(FileAttributes.ReparsePoint|FileAttributes.Offline))==0&&Engine.SafeAncestors(Path.Combine(path,"probe"));}catch{return false;}}
 static IEnumerable<string> Children(string root,string pattern="*"){if(!PlainDirectory(root))return new string[0];try{return Directory.EnumerateDirectories(root,pattern).Take(256).Where(PlainDirectory).ToArray();}catch{return new string[0];}}
 internal static bool ProfileMarker(string directory){try{return new[]{"Preferences","Secure Preferences"}.Any(n=>{string file=P(directory,n);return File.Exists(file)&&(File.GetAttributes(file)&(FileAttributes.ReparsePoint|FileAttributes.Offline))==0;});}catch{return false;}}
 internal static string[] Profiles(string root,bool partitions){
  var result=new List<string>{root};foreach(string child in Children(root)){string name=Path.GetFileName(child);if(name.Equals("Default",StringComparison.OrdinalIgnoreCase)||name.StartsWith("Profile ",StringComparison.OrdinalIgnoreCase)||name.StartsWith("WV2Profile_",StringComparison.OrdinalIgnoreCase)||name.Equals("Guest Profile",StringComparison.OrdinalIgnoreCase)||!ProtectedFolders.Contains(name,StringComparer.OrdinalIgnoreCase)&&ProfileMarker(child))result.Add(child);}
  if(partitions)foreach(string part in new[]{"Partitions","partitions"}.Select(n=>P(root,n)).Distinct(StringComparer.OrdinalIgnoreCase))result.AddRange(Children(part));
  return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
 }
 internal static string[] CacheRoots(IEnumerable<string> bases,bool partitions=true){return bases.Where(PlainDirectory).SelectMany(root=>Profiles(root,partitions)).SelectMany(profile=>ResourceFolders.Select(folder=>P(profile,folder))).Where(PlainDirectory).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
 static string[] Minimal(IEnumerable<string> paths){var result=new List<string>();foreach(string path in paths.Where(p=>!String.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p.Length))if(!result.Any(parent=>ScanPolicy.Inside(path,parent)))result.Add(path);return result.ToArray();}
 static void Guard(Rule rule,bool header=false){
  rule.ExcludedNames=(rule.ExcludedNames??new string[0]).Concat(ProtectedFolders).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();var exclude=rule.AdditionalExclude;
  rule.AdditionalExclude=path=>PersonalExtensions.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase)||(exclude!=null&&exclude(path));rule.HeaderGuard=rule.HeaderGuard||header;
 }
 static Rule Resources(List<Rule> rules,string id,string app,string group,IEnumerable<string> bases,string[] owners,bool profiles=true){
  var rule=rules.FirstOrDefault(r=>r.Id==id);if(rule==null){rule=new Rule(id,app+" 网页与图形缓存",group,"","",new string[0],1,true,owners){Software=app,Part="网页与图形缓存",GroupOverride=group};rules.Add(rule);}
  var roots=profiles?CacheRoots(bases):bases.SelectMany(root=>ResourceFolders.Select(folder=>P(root,folder))).Where(PlainDirectory).ToArray();rule.Roots=Minimal(rule.Roots.Concat(roots));rule.Processes=rule.Processes.Concat(owners).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
  if(rule.Quick){rule.Days=1;rule.Description=app+" 在 AppData 中独立的网页、脚本及图形缓存，覆盖多配置与分区；保留最近 1 天的文件。";rule.Impact="请先关闭 "+app+"；资源会在下次使用时重新加载或编译，可能暂时变慢。保留账户、Cookie、聊天、文档和离线站点存储。";}
  rule.Source=Electron;Guard(rule);return rule;
 }
 static void Diagnostics(List<Rule> rules,string id,string app,string group,IEnumerable<string> roots,string[] owners,bool dumps=false){
  if(rules.Any(r=>r.Id==id))return;var rule=new Rule(id,app+(dumps?" 旧崩溃转储":" 旧诊断日志"),group,"AppData 中明确诊断目录内超过 14 天的"+(dumps?" .dmp/.hdmp 转储。":" .log/.xlog 诊断文件。"),"只会失去对应的历史故障诊断信息；保留聊天、会话、账户、文档与恢复文件。",Minimal(roots),14,true,owners){Software=app,Part=dumps?"旧崩溃转储":"旧诊断日志",Source=Electron,GroupOverride=group,Extensions=dumps?new[]{".dmp",".hdmp"}:new[]{".log",".xlog"}};Guard(rule,true);rules.Add(rule);
 }
 public static void Add(List<Rule> rules,string local,string roaming,string low){
  foreach(var spec in new[]{
   new[]{"edge","Microsoft Edge","Microsoft\\Edge\\User Data","msedge"},new[]{"chrome","Google Chrome","Google\\Chrome\\User Data","chrome"},new[]{"brave","Brave","BraveSoftware\\Brave-Browser\\User Data","brave"},new[]{"quark","夸克","Quark\\User Data","Quark"},new[]{"qqbrowser","QQ 浏览器","Tencent\\QQBrowser\\User Data","QQBrowser"}}){
   var r=Resources(rules,spec[0],spec[1],"网页缓存",new[]{P(local,spec[2])},new[]{spec[3]});r.Source=Chromium;
  }
  foreach(var spec in new[]{
   new[]{"code-cache","VS Code","Code","Code","开发缓存"},new[]{"qq-web","QQ","QQ","QQ","软件缓存"},
   new[]{"obsidian-web","Obsidian","obsidian","Obsidian","软件缓存"},new[]{"postman-web","Postman","Postman","Postman","软件缓存"},new[]{"notion-web","Notion","Notion","Notion","软件缓存"},new[]{"github-desktop-web","GitHub Desktop","GitHub Desktop","GitHubDesktop","软件缓存"},new[]{"figma-web","Figma","Figma","Figma","软件缓存"},
   new[]{"appdata-slack","Slack","Slack","slack","软件缓存"},new[]{"appdata-zoom-web","Zoom","Zoom","Zoom","软件缓存"},new[]{"appdata-chatgpt-web","ChatGPT","ChatGPT","ChatGPT","软件缓存"},new[]{"appdata-codex-web","Codex","Codex\\web","Codex","软件缓存"},new[]{"appdata-typora-web","Typora","Typora","Typora","软件缓存"}}){
   var r=Resources(rules,spec[0],spec[1],spec[4],new[]{P(roaming,spec[2]),P(local,spec[2])},new[]{spec[3]});
   Diagnostics(rules,"appdata-"+spec[0]+"-logs",spec[1],spec[4],new[]{P(roaming,spec[2]+"\\logs"),P(local,spec[2]+"\\logs")},r.Processes);
   Diagnostics(rules,"appdata-"+spec[0]+"-dumps",spec[1],spec[4],new[]{P(roaming,spec[2]+"\\Crashpad\\reports"),P(roaming,spec[2]+"\\Crashpad\\pending"),P(local,spec[2]+"\\Crashpad\\reports")},r.Processes,true);
  }
  // Chat and audio applications expose only renderer/code caches to one-click.
  // Their downloaded media, attachments, databases and offline libraries stay outside.
  foreach(var spec in new[]{new[]{"appdata-discord-render","Discord","discord","Discord"},new[]{"appdata-spotify-render","Spotify","Spotify","Spotify"},new[]{"appdata-netease-render","网易云音乐","Netease\\CloudMusic","cloudmusic"},new[]{"appdata-qqmusic-render","QQ 音乐","Tencent\\QQMusic","QQMusic"}}){
   var roots=new[]{P(roaming,spec[2]),P(local,spec[2])}.Where(PlainDirectory).SelectMany(root=>Profiles(root,true)).SelectMany(root=>new[]{"Code Cache","GPUCache","ShaderCache","DawnGraphiteCache","DawnWebGPUCache"}.Select(tail=>P(root,tail))).Where(PlainDirectory).ToArray();
   var r=new Rule(spec[0],spec[1]+" 界面编译缓存","软件缓存","仅检查 AppData 中界面的脚本与 GPU 编译缓存，保留最近 1 天。","界面需要重新编译；不清理聊天图片、音频、附件、离线歌曲或收藏。",Minimal(roots),1,true,spec[3]){Software=spec[1],Part="界面编译缓存",Source=Electron};Guard(r);rules.Add(r);
  }
  var qq=rules.FirstOrDefault(r=>r.Id=="qq-web");if(qq!=null){qq.Processes=qq.Processes.Concat(new[]{"QQNT","QQApp","QQAppEx"}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
  foreach(string name in new[]{"WeChat","xwechat"}){var w=rules.FirstOrDefault(r=>r.Id=="wechat-web");if(w!=null){string root=P(roaming,"Tencent\\"+name+"\\radium\\web\\profiles");w.Roots=Minimal(w.Roots.Concat(Children(root).SelectMany(p=>ResourceFolders.Select(folder=>P(p,folder))).Where(PlainDirectory)));Guard(w);}}
  var steam=rules.FirstOrDefault(r=>r.Id=="steam");if(steam!=null){steam.Roots=Minimal(steam.Roots.Concat(CacheRoots(new[]{P(local,"Steam\\htmlcache")})));Guard(steam);}
  var epic=rules.FirstOrDefault(r=>r.Id=="epic-cache");if(epic!=null){epic.Roots=Minimal(epic.Roots.Concat(CacheRoots(Children(P(local,"EpicGamesLauncher\\Saved")).Where(p=>Path.GetFileName(p).StartsWith("webcache",StringComparison.OrdinalIgnoreCase)))));Guard(epic);}
  foreach(string prefix in new[]{"MSTeams_","MicrosoftWindows.Client.WebExperience_","Microsoft.OutlookForWindows_"}){
   string app=prefix=="MSTeams_"?"Microsoft Teams":prefix.StartsWith("Microsoft.Outlook")?"Outlook":"Windows 小组件";string id=prefix=="MSTeams_"?"teams-web":prefix.StartsWith("Microsoft.Outlook")?"appdata-outlook-web":"widgets";string[] owners=prefix=="MSTeams_"?new[]{"ms-teams","Teams","msedgewebview2"}:prefix.StartsWith("Microsoft.Outlook")?new[]{"olk","msedgewebview2"}:new[]{"Widgets","WidgetService","msedgewebview2"};
   var bases=Children(P(local,"Packages"),prefix+"*").SelectMany(package=>new[]{P(package,"LocalCache\\Microsoft\\MSTeams\\EBWebView"),P(package,"LocalState\\EBWebView"),P(package,"LocalCache\\Microsoft\\Olk\\EBWebView")}).ToArray();var r=Resources(rules,id,app,prefix.StartsWith("MicrosoftWindows")?"系统垃圾":"软件缓存",bases,owners);r.Source=WebView;
  }
  Diagnostics(rules,"appdata-codex-diagnostics","Codex","软件缓存",new[]{P(local,"Codex\\Logs")},new[]{"Codex"});
  Diagnostics(rules,"appdata-epic-diagnostics","Epic Games","软件缓存",new[]{P(local,"EpicGamesLauncher\\Saved\\Logs"),P(local,"Epic\\EpicGamesLauncher\\Saved\\Logs")},new[]{"EpicGamesLauncher","EpicWebHelper"});
  Diagnostics(rules,"appdata-windows-wer-archive","Windows 错误报告","系统垃圾",new[]{P(local,"Microsoft\\Windows\\WER\\ReportArchive")},new[]{"WerFault","wermgr"},true);
  var thumbs=rules.FirstOrDefault(r=>r.Id=="thumbnails");if(thumbs!=null){thumbs.Quick=true;thumbs.Days=1;thumbs.HeaderGuard=true;thumbs.Source="https://support.microsoft.com/windows/free-up-drive-space-in-windows";thumbs.Description="仅检查 AppData/Local/Microsoft/Windows/Explorer 中 1 天前的 thumbcache_*.db；使用中的文件跳过。";thumbs.Impact="资源管理器会重新生成缩略图，首次显示可能变慢；不处理图片原件、文档和图标设置。";}
  var nvidia=rules.FirstOrDefault(r=>r.Id=="nvidia");if(nvidia!=null){nvidia.Roots=Minimal(nvidia.Roots.Concat(new[]{P(local,"NVIDIA\\PerDriverVersion\\DXCache"),P(local,"NVIDIA\\PerDriverVersion\\GLCache"),P(low,"NVIDIA\\PerDriverVersion\\DXCache"),P(low,"NVIDIA\\PerDriverVersion\\GLCache"),P(low,"NVIDIA\\GLCache"),P(local,"NVIDIA\\ComputeCache")}));nvidia.Source=Nvidia;nvidia.Description="NVIDIA 独立着色器与编译缓存，覆盖 Local、LocalLow 及按驱动版本区分的目录。保留最近 7 天；正在使用的文件与应用保留。";}
 }
}
}
