using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;

namespace ClearSpace {
public static class Groups {
 public static readonly string[] Names={"系统垃圾","软件缓存","网页缓存","开发缓存","更新残留","使用痕迹","快捷方式","注册表","保留项目"};
 public static string ForCategory(string category) {
  if(category=="开发工具"||category=="开发缓存")return "开发缓存";if(category=="更新资料"||category=="更新残留")return "更新残留";
  if(category=="临时文件"||category=="诊断资料"||category=="系统垃圾")return "系统垃圾";if(category=="受保护资料")return "保留项目";if(Names.Contains(category))return category;return "软件缓存";
 }
}
public static class ExtendedRules {
 static string P(string root,string tail){return Path.Combine(root,tail);}
 static Rule R(List<Rule> rules,string id,string title,string group,string desc,string impact,string[] roots,int days,bool quick,params string[] owners){var r=new Rule(id,title,group,desc,impact,roots,days,quick,owners);rules.Add(r);return r;}
 static string[] Profiles(string parent,string prefix,string[] tails){var roots=new List<string>();try{if(Directory.Exists(parent))foreach(var d in new DirectoryInfo(parent).GetDirectories()){if(prefix==null||d.Name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))foreach(string tail in tails)roots.Add(P(d.FullName,tail));}}catch{}return roots.ToArray();}
 static string[] Chromium(string parent){var roots=new List<string>();try{if(Directory.Exists(parent))foreach(var d in new DirectoryInfo(parent).GetDirectories()){if(d.Name=="Default"||d.Name.StartsWith("Profile "))foreach(string tail in new[]{"Cache","Code Cache","GPUCache","DawnGraphiteCache","DawnWebGPUCache"})roots.Add(P(d.FullName,tail));}}catch{}return roots.ToArray();}
 static void Web(List<Rule> rules,string id,string title,string parent,params string[] owners){R(rules,id,title,"常用软件","已知网页资源缓存，包括图片、脚本和图形编译结果。只处理 1 天前的文件。","资源会重新加载；保留账号、Cookie、收藏、历史、聊天和下载文件。软件运行时整项保留。",new[]{P(parent,"Cache"),P(parent,"Code Cache"),P(parent,"GPUCache")},1,true,owners);}
 public static void Add(List<Rule> rules,string local,string roaming,string profile,string windows,string data){
  R(rules,"firefox","Firefox 网页与启动缓存","常用软件","Firefox 本地配置下的 cache2、startupCache 和 shader-cache；不处理漫游配置或登录数据库。","下次启动或加载页面可能稍慢，不删除历史、密码和书签。",Profiles(P(local,"Mozilla\\Firefox\\Profiles"),null,new[]{"cache2","startupCache","shader-cache"}),1,true,"firefox");
  R(rules,"brave","Brave 网页资源缓存","常用软件","独立的 Cache、Code Cache 和图形缓存。","保留收藏、密码、钱包配置及站点数据。",Chromium(P(local,"BraveSoftware\\Brave-Browser\\User Data")),1,true,"brave");
  Web(rules,"opera","Opera 网页资源缓存",P(roaming,"Opera Software\\Opera Stable"),"opera");Web(rules,"opera-gx","Opera GX 网页资源缓存",P(roaming,"Opera Software\\Opera GX Stable"),"opera");
  R(rules,"qqbrowser","QQ 浏览器网页缓存","常用软件","只识别标准用户配置中的独立网页缓存。","保留密码、账号和历史；再次访问页面重新加载资源。",Chromium(P(local,"Tencent\\QQBrowser\\User Data")),1,true,"QQBrowser");
  Web(rules,"discord","Discord 网页资源缓存",P(roaming,"discord"),"Discord","Update");Web(rules,"discord-canary","Discord Canary 网页缓存",P(roaming,"discordcanary"),"DiscordCanary","Update");
  Web(rules,"slack","Slack 网页资源缓存",P(roaming,"Slack"),"slack");Web(rules,"zoom","Zoom 网页资源缓存",P(roaming,"Zoom"),"Zoom");
  Web(rules,"taptap","TapTap 启动器网页缓存",P(roaming,"TapTap"),"TapTap");Web(rules,"neko","N.E.K.O 网页资源缓存",P(roaming,"N.E.K.O"),"N.E.K.O","neko");
  Web(rules,"aclos","ACLOS 网页资源缓存",P(roaming,"ACLOS"),"ACLOS");Web(rules,"perfect-web","完美平台网页资源缓存",P(roaming,"perfectworldarena"),"perfectworldarena");
  Web(rules,"ocs-web","OCS Desktop 网页资源缓存",P(roaming,"OCS Desktop"),"OCS Desktop");
  R(rules,"wechat-web","微信网页资源缓存","常用软件","微信内嵌页面的独立图片、脚本与图形缓存，限定 radium/web/profiles。","不会处理聊天数据库、附件、收藏、插件和小程序离线数据。微信运行时整项保留。",Profiles(P(roaming,"Tencent\\xwechat\\radium\\web\\profiles"),null,new[]{"Cache","Code Cache","GPUCache"}),1,true,"Weixin","WeChat");
  R(rules,"qq-web","QQ 网页资源缓存","常用软件","QQEX 账号目录中的独立 Cache、Code Cache 和 GPUCache。","不处理聊天记录、账号设置和 Service Worker 数据。QQ 运行时整项保留。",Profiles(P(roaming,"QQEX\\users"),null,new[]{"Cache","Code Cache","GPUCache"}),1,true,"QQ","QQNT");
  Web(rules,"hoyoplay","米哈游启动器网页缓存",P(roaming,"miHoYo\\HYP\\1_1\\fedata"),"HYP","launcher","GenshinImpact","StarRail");
  R(rules,"teams-web","新版 Teams 网页缓存","常用软件","只识别 Teams 包中 EBWebView 的独立网页资源缓存。","保留会议、账号与聊天数据；Teams 运行时保留。",Profiles(P(local,"Packages"),"MSTeams_",new[]{"LocalCache\\Microsoft\\MSTeams\\EBWebView\\Default\\Cache","LocalCache\\Microsoft\\MSTeams\\EBWebView\\Default\\Code Cache"}),1,true,"ms-teams","Teams");
  R(rules,"widgets","Windows 小组件网页缓存","系统垃圾","Windows 小组件内嵌网页的图片和脚本缓存，限定已知包内目录。","小组件内容下次重新加载，不处理小组件配置或账户。",Profiles(P(local,"Packages"),"MicrosoftWindows.Client.WebExperience_",new[]{"LocalState\\EBWebView\\Default\\Cache","LocalState\\EBWebView\\Default\\Code Cache"}),1,true,"Widgets","WidgetService","msedgewebview2");
  R(rules,"wer","Windows 错误报告","系统垃圾","应用崩溃后的错误报告档案与等待发送的诊断资料。","删除会丢失这些历史诊断报告；保留 14 天内资料。通常需要管理员权限。",new[]{P(local,"Microsoft\\Windows\\WER\\ReportArchive"),P(local,"Microsoft\\Windows\\WER\\ReportQueue"),P(data,"Microsoft\\Windows\\WER\\ReportArchive"),P(data,"Microsoft\\Windows\\WER\\ReportQueue")},14,false,"WerFault","wermgr");
  var mini=R(rules,"minidump","Windows 小型蓝屏转储","系统垃圾","Minidump 中的 .dmp 诊断文件，只处理 7 天前资料。","会失去对应蓝屏的诊断记录；请先确认不需要排查历史问题。",new[]{P(windows,"Minidump")},7,false);mini.Extensions=new[]{".dmp"};
  var logs=R(rules,"windows-logs","Windows 旧安装与维护日志","系统垃圾","CBS、DISM 目录中的历史 .log、.cab 诊断资料，保留 30 天。","旧维护诊断记录将无法恢复；不会清理 Windows 组件、驱动或 Installer。",new[]{P(windows,"Logs\\CBS"),P(windows,"Logs\\DISM")},30,false,"TiWorker","TrustedInstaller","dism","msiexec");logs.Extensions=new[]{".log",".cab"};
  var thumbs=R(rules,"thumbnails","资源管理器缩略图缓存","系统垃圾","只识别 Explorer 目录内 thumbcache_*.db，保留 7 天。","缩略图需要重新生成，文件夹首次显示可能变慢。正在占用的缓存会跳过。",new[]{P(local,"Microsoft\\Windows\\Explorer")},7,false);thumbs.Extensions=new[]{".db"};thumbs.NamePrefix="thumbcache_";thumbs.OnlyTop=true;
  R(rules,"d3d","Windows DirectX 着色器缓存","系统垃圾","D3DSCache 中保存的图形编译结果。","下次运行游戏或渲染程序可能需要重新编译。请先关闭游戏；保留 7 天内文件。",new[]{P(local,"D3DSCache")},7,false,"Unity","UnrealEditor","Blender","cs2","GenshinImpact","StarRail");
  R(rules,"intel-shader","Intel 图形编译缓存","常用软件","Intel ShaderCache 中的编译结果。","清理后图形程序重新编译。请先关闭游戏和渲染软件。",new[]{P(local,"Intel\\ShaderCache")},7,false,"Unity","UnrealEditor","Blender");
  R(rules,"amd-shader","AMD 图形编译缓存","常用软件","AMD DxCache、GLCache、VkCache 中的图形编译结果。","可能触发游戏或渲染程序重新编译，首次运行变慢。请先关闭相关程序。",new[]{P(local,"AMD\\DxCache"),P(local,"AMD\\GLCache"),P(local,"AMD\\VkCache")},7,false,"RadeonSoftware","Unity","UnrealEditor","Blender","cs2");
  R(rules,"adobe-media","Adobe 音频与媒体缓存","常用软件","Adobe Common 的 Media Cache 和 Media Cache Files，不扫描项目或素材目录。","媒体索引和音频转换文件会重新生成，可能耗时。请关闭 Adobe 软件，确认保留原始素材。",new[]{P(roaming,"Adobe\\Common\\Media Cache"),P(roaming,"Adobe\\Common\\Media Cache Files")},7,false,"Adobe Premiere Pro","AfterFX","Adobe Media Encoder","Audition","Photoshop");
  R(rules,"spotify","Spotify 播放缓存","常用软件","Spotify Data 中的播放缓存。","可能失去部分离线播放能力，需重新联网加载。建议优先用软件内置清理。",new[]{P(local,"Spotify\\Data")},14,false,"Spotify");
  R(rules,"netease-music","网易云音乐播放缓存","常用软件","CloudMusic 标准 Cache 目录中的播放缓存，不扫描下载目录。","部分音频、图片需重新缓存；请确认仍可重新下载。",new[]{P(local,"NetEase\\CloudMusic\\Cache")},14,false,"cloudmusic");
  R(rules,"nuget-http","NuGet HTTP 下载缓存","开发缓存","NuGet v3-cache 的 HTTP 下载缓存，不处理已安装依赖 packages。","后续恢复依赖可能需要重新下载；保留 7 天，开发工具运行时保留。",new[]{P(local,"NuGet\\v3-cache")},7,true,"dotnet","MSBuild","devenv");
  R(rules,"yarn","Yarn 下载缓存","开发缓存","Yarn 标准 Cache 目录内的依赖下载副本，不处理项目 node_modules。","后续安装依赖可能重新下载，私有包需要有效访问权限。",new[]{P(local,"Yarn\\Cache")},14,false,"node","yarn");
  R(rules,"composer","Composer 下载缓存","开发缓存","Composer 的 cache/files 安装包副本。","后续安装可能需要联网和有效权限，保留 14 天内文件。",new[]{P(local,"Composer\\cache\\files")},14,false,"php","composer");
  R(rules,"gradle","Gradle 构建与下载缓存","开发缓存","用户 .gradle/caches 内的构建和依赖缓存。","清理后重新构建和下载可能耗时，离线构建受影响。Java、Gradle 或 Android Studio 运行时保留。",new[]{P(profile,".gradle\\caches")},30,false,"java","javaw","gradle","studio64");
  R(rules,"unity-build","Unity 构建缓存","开发缓存","Local/Unity/Caches 中的工具构建缓存，不扫描任何项目 Library 或 Assets。","后续构建可能变慢；请关闭 Unity 与 Unity Hub。",new[]{P(local,"Unity\\Caches")},14,false,"Unity","Unity Hub","bee_backend" );
  R(rules,"unreal-ddc","Unreal 派生数据缓存","开发缓存","明确的 DerivedDataCache 目录，不清理 Zen 数据库、项目或源资产。","着色器和资源派生结果需要重新生成，可能耗时；请关闭 Unreal Editor。",new[]{P(local,"UnrealEngine\\Common\\DerivedDataCache"),P(local,"UnrealEngine\\DerivedDataCache")},14,false,"UnrealEditor","ShaderCompileWorker");
  R(rules,"vscode-insiders","VS Code Insiders 缓存","开发缓存","Insiders 的扩展下载和独立编译、图形缓存，保留 7 天。","保留设置、扩展、聊天、未保存文件与备份；缓存按需重新生成。",new[]{P(roaming,"Code - Insiders\\CachedExtensionVSIXs"),P(roaming,"Code - Insiders\\CachedData"),P(roaming,"Code - Insiders\\Cache"),P(roaming,"Code - Insiders\\Code Cache"),P(roaming,"Code - Insiders\\GPUCache")},7,true,"Code - Insiders","Code");
  var recent=R(rules,"recent-links","最近访问记录与文档入口","使用痕迹","Recent 顶层 .lnk 记录，只处理 7 天前的访问入口。","会减少文件管理器的最近访问记录，不会删除原文件。固定列表、跳转列表和其他子目录保留。",new[]{P(roaming,"Microsoft\\Windows\\Recent")},7,false);recent.Extensions=new[]{".lnk"};recent.OnlyTop=true;recent.Privacy=true;
  var shortcuts=R(rules,"broken-shortcuts","失效的桌面与开始菜单快捷方式","快捷方式","检查桌面和开始菜单的 .lnk，仅将目标在 C 盘、明确不存在且 7 天前的链接列为候选。","可能移除你仍想保留的入口。网络、其他盘、系统特殊链接和权限不足的目标不归类；删除前再次核对目标。",new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.Programs),Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)},7,false);shortcuts.Extensions=new[]{".lnk"};shortcuts.Shortcut=true;
  var registry=R(rules,"registry-audit","注册表安装信息检查","注册表检查","只读检查已安装软件信息，关联安装目录缺失也不能证明注册表项无用。","不删除注册表。清理收益通常很小，误删可能破坏软件、卸载或系统功能。",new string[0],0,false);registry.Protected=true;registry.InfoKind="registry";registry.Notice=true;
  var history=R(rules,"browser-private","浏览历史、Cookie 与账号数据","使用痕迹","这些是使用记录和登录数据，不应当直接删除数据库文件。","本工具保留。需要清除时，请使用对应浏览器的“清除浏览数据”，自行确认注销与历史损失。",new string[0],0,false);history.Protected=true;history.Notice=true;
  var update=R(rules,"windows-update","Windows 更新下载资料","更新残留","Windows Update 的下载资料，可能与正在安装或待安装更新关联。","由 Windows 临时文件/磁盘清理功能处理，本工具不直接删除，以保护更新流程。",new[]{P(windows,"SoftwareDistribution\\Download")},0,false);update.Protected=true;update.Notice=true;update.InfoKind="storage";
  var delivery=R(rules,"delivery","Windows 传递优化缓存","系统垃圾","用于 Windows 更新和商店内容分发的缓存，系统也会自动管理。","本工具只展示占用；请通过 Windows 磁盘清理或临时文件功能处理。",new[]{P(windows,"ServiceProfiles\\NetworkService\\AppData\\Local\\Microsoft\\Windows\\DeliveryOptimization\\Cache"),P(data,"Microsoft\\Windows\\DeliveryOptimization\\Cache")},0,false);delivery.Protected=true;delivery.InfoKind="storage";delivery.Notice=true;
  var old=R(rules,"windows-old","旧版 Windows 安装资料","系统垃圾","Windows.old 可能用于回退及恢复升级前的个人文件。","本工具不直接删除。请确认无需回退和旧文件后，使用 Windows 临时文件功能。",new[]{P(RuleBook.TargetDrive,"Windows.old")},0,false);old.Protected=true;old.InfoKind="storage";
  var restore=R(rules,"restore","系统还原与恢复资料","受保护资料","系统还原点、Recovery、驱动库和安装修复资料具有恢复价值。","本工具不扫描或删除这些核心保护区域。",new string[0],0,false);restore.Protected=true;restore.Notice=true;
 }
}
public static class ShortcutAudit {
 public static bool ProvenMissingLocalTarget(string raw){
  try{string path=Environment.ExpandEnvironmentVariables(raw);if(String.IsNullOrWhiteSpace(path)||!Path.IsPathRooted(path)||path.StartsWith("\\\\")||path.Contains("%"))return false;
   if(!String.Equals(Path.GetPathRoot(Path.GetFullPath(path)),RuleBook.TargetDrive,StringComparison.OrdinalIgnoreCase))return false;
   try{File.GetAttributes(path);return false;}catch(FileNotFoundException){return true;}catch(DirectoryNotFoundException){return true;}
  }catch{}return false;
 }
 public static string MissingTarget(string path){object shell=null,link=null;try{var type=Type.GetTypeFromProgID("WScript.Shell");if(type==null)return null;shell=Activator.CreateInstance(type);link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});string target=(string)link.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,link,null);return ProvenMissingLocalTarget(target)?Environment.ExpandEnvironmentVariables(target):null;}catch{return null;}finally{if(link!=null&&Marshal.IsComObject(link))Marshal.FinalReleaseComObject(link);if(shell!=null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);}}
}
public static class RegistryAudit {
 public static void Read(Finding finding,CancellationToken token){
  const string uninstall="SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall";var seen=new HashSet<string>();
  foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}){
   token.ThrowIfCancellationRequested();try{using(var root=RegistryKey.OpenBaseKey(hive,view))using(var key=root.OpenSubKey(uninstall,false)){if(key==null)continue;foreach(string name in key.GetSubKeyNames()){
    token.ThrowIfCancellationRequested();using(var item=key.OpenSubKey(name,false)){if(item==null)continue;string display=Convert.ToString(item.GetValue("DisplayName"));if(String.IsNullOrWhiteSpace(display))continue;string location=Convert.ToString(item.GetValue("InstallLocation"));string identity=hive+"|"+name+"|"+location;if(!seen.Add(identity))continue;finding.VirtualItems++;if(ShortcutAudit.ProvenMissingLocalTarget(location)){finding.SuspectedItems++;if(finding.Samples.Count<5)finding.Samples.Add(display+" · 安装目录缺失（需人工判断）");}}
   }}}catch(OperationCanceledException){throw;}catch{finding.Unreadable++;}
  }
  finding.Extra="已检查 "+finding.VirtualItems+" 项安装信息；"+finding.SuspectedItems+" 项关联目录缺失。\n目录缺失不等于注册表垃圾，全部保留。";
 }
}
}
