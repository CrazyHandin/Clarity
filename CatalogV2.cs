using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClearSpace {
public static class CatalogV2 {
 public const string Community="https://github.com/MoscaDotTo/Winapp2";
 public const string ChromiumSource="https://github.com/bleachbit/bleachbit/blob/master/cleaners/chromium.xml";
 public const string ElectronSource="https://github.com/electron/electron/blob/main/docs/api/app.md";
 public const string JetBrainsSource="https://www.jetbrains.com/help/idea/directories-used-by-the-ide-to-store-settings-caches-plugins-and-logs.html";
 static string P(string root,string path){return Path.Combine(root,path);}
 public static string[] Expand(string root,string pattern){
  var current=new List<string>{root};foreach(string part in pattern.Split('\\')){
   var next=new List<string>();foreach(string parent in current){
    if(part.Contains("*")||part.Contains("?")){try{if(!Engine.SafeAncestors(P(parent,"probe")))continue;foreach(var d in new DirectoryInfo(parent).GetDirectories(part))if((d.Attributes&FileAttributes.ReparsePoint)==0)next.Add(d.FullName);}catch{}}
    else next.Add(P(parent,part));
   }current=next;
  }return current.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
 }
 static Rule Add(List<Rule> rules,string id,string group,string app,string part,string[] roots,int days,bool quick,string source,string[] owners,string desc,string impact){
  var r=new Rule(id,app+" "+part,group,desc,impact,roots,days,quick,owners){Software=app,Part=part,Source=source,GroupOverride=group};rules.Add(r);return r;
 }
 static Rule Logs(List<Rule> rules,string id,string group,string app,string[] roots,string[] owners){return Add(rules,id,group,app,"旧诊断日志",roots,14,false,Community,owners,"旧日志与诊断输出。","删除后无法排查对应历史问题。");}
 static Rule Dump(List<Rule> rules,string id,string group,string app,string[] roots,string[] owners){var r=Add(rules,id,group,app,"旧崩溃报告",roots,14,false,ChromiumSource,owners,"旧崩溃报告和转储。","删除后无法排查对应历史崩溃。");return r;}
 static string[] BrowserProfiles(string root,string tail){var profiles=new[]{P(root,"Default")}.Concat(Expand(root,"Profile *"));return profiles.Select(p=>P(p,tail)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
 static void Browser(List<Rule> rules,string id,string app,string root,params string[] owners){
  Add(rules,id+"-resources","网页缓存",app,"网页资源缓存",BrowserProfiles(root,"Cache").Concat(BrowserProfiles(root,"Code Cache")).Concat(BrowserProfiles(root,"GPUCache")).ToArray(),1,true,ChromiumSource,owners,"图片、脚本和图形编译缓存。","首次加载页面可能变慢；保留密码、Cookie、历史与会话。");
  Dump(rules,id+"-crash","网页缓存",app,new[]{P(root,"Crashpad\\reports"),P(root,"Crash Reports")},owners);
  var logs=Logs(rules,id+"-logs","网页缓存",app,new[]{root},owners);logs.OnlyTop=true;logs.Extensions=new[]{".log"};
 }
 static void Electron(List<Rule> rules,string id,string app,string root,string[] owners,bool chat){
  Add(rules,id+"-web","软件缓存",app,"网页资源缓存",new[]{P(root,"Cache"),P(root,"Code Cache"),P(root,"GPUCache")},7,!chat,ElectronSource,owners,"内嵌网页的图片、脚本与图形编译缓存。",chat?"部分离线图片或收藏动图可能需重新加载；不处理聊天数据库。":"界面资源重新生成；保留工作资料、账号与设置。");
  Dump(rules,id+"-crash","软件缓存",app,new[]{P(root,"Crashpad\\reports")},owners);
  var logs=Logs(rules,id+"-logs","软件缓存",app,new[]{P(root,"logs")},owners);logs.Extensions=new[]{".log",".txt"};
 }
 public static void Upgrade(List<Rule> rules,string local,string roaming,string profile,string windows,string data){
  rules.RemoveAll(r=>r.Id=="registry-audit"||r.Id=="browser-private"||r.Id=="restore");
  var oldLogs=rules.First(r=>r.Id=="logs");rules.Remove(oldLogs);string[] logApps={"微信","Tencent","Epic Games","VS Code","WPS","OneDrive"};string[][] logOwners={new[]{"Weixin","WeChat"},new[]{"Weixin","QQ"},new[]{"EpicGamesLauncher"},new[]{"Code"},new[]{"wps"},new[]{"OneDrive"}};
  for(int i=0;i<oldLogs.Roots.Length;i++)Logs(rules,"app-diagnostic-"+i,i==3?"开发缓存":"软件缓存",logApps[i],new[]{oldLogs.Roots[i]},logOwners[i]);
  var map=new Dictionary<string,string>{
   {"code-vsix","VS Code"},{"code-cache","VS Code"},{"pip","Python / pip"},{"npm","npm"},{"edge","Microsoft Edge"},{"chrome","Google Chrome"},{"quark","夸克"},{"steam","Steam"},{"epic-cache","Epic Games"},{"nvidia","NVIDIA"},{"qqmusic","QQ 音乐"},{"temp","用户临时文件"},{"system-temp","Windows 临时文件"},{"crash","应用诊断"},{"memory","Windows 诊断"},{"logs","应用诊断"},{"update-perfectworldarena-updater","完美平台"},{"update-unityhub-updater","Unity Hub"},{"update-ocs desktop-updater","OCS Desktop"},{"driver-download","NVIDIA 驱动"},{"office","Microsoft Office"},{"support","原厂驱动"},{"system-hiberfil.sys","Windows 系统文件"},{"system-pagefile.sys","Windows 系统文件"},
   {"firefox","Firefox"},{"brave","Brave"},{"opera","Opera"},{"opera-gx","Opera GX"},{"qqbrowser","QQ 浏览器"},{"discord","Discord"},{"discord-canary","Discord Canary"},{"slack","Slack"},{"zoom","Zoom"},{"taptap","TapTap"},{"neko","N.E.K.O"},{"aclos","ACLOS"},{"perfect-web","完美平台"},{"ocs-web","OCS Desktop"},{"wechat-web","微信"},{"qq-web","QQ"},{"hoyoplay","米哈游启动器"},{"teams-web","Microsoft Teams"},{"widgets","Windows 小组件"},{"wer","Windows 错误报告"},{"minidump","Windows 诊断"},{"windows-logs","Windows 维护"},{"thumbnails","资源管理器"},{"d3d","DirectX"},{"intel-shader","Intel"},{"amd-shader","AMD"},{"adobe-media","Adobe"},{"spotify","Spotify"},{"netease-music","网易云音乐"},{"nuget-http","NuGet"},{"yarn","Yarn"},{"composer","Composer"},{"gradle","Gradle"},{"unity-build","Unity"},{"unreal-ddc","Unreal Engine"},{"vscode-insiders","VS Code Insiders"},{"recent-links","Windows 最近访问"},{"broken-shortcuts","桌面与开始菜单"},{"windows-update","Windows Update"},{"delivery","传递优化"},{"windows-old","旧版 Windows"}
  };
  foreach(var r in rules){string app;if(map.TryGetValue(r.Id,out app))r.Software=app;else if(String.IsNullOrEmpty(r.Software))r.Software=r.Title;if(String.IsNullOrEmpty(r.Part))r.Part=r.Title.StartsWith(r.Software+" ",StringComparison.OrdinalIgnoreCase)?r.Title.Substring(r.Software.Length+1):r.Title;r.Source=r.Source??Community;
   if(new[]{"edge","chrome","quark","firefox","brave","opera","opera-gx","qqbrowser"}.Contains(r.Id))r.GroupOverride="网页缓存";
   if(r.Id.StartsWith("discord")){r.Quick=false;r.Impact="缓存清理可能影响离线内容与收藏动图；保留登录和聊天数据库。";}
   if(r.Id=="nvidia")r.Description="显卡着色器与编译缓存。";
  }
  foreach(var p in new[]{new[]{"edge","Microsoft Edge","Microsoft\\Edge\\User Data","msedge"},new[]{"chrome","Google Chrome","Google\\Chrome\\User Data","chrome"},new[]{"brave","Brave","BraveSoftware\\Brave-Browser\\User Data","brave"},new[]{"quark","夸克","Quark\\User Data","Quark"}}){
   Dump(rules,p[0]+"-reports","网页缓存",p[1],new[]{P(local,p[2]+"\\Crashpad\\reports")},new[]{p[3]});var logs=Logs(rules,p[0]+"-diagnostic","网页缓存",p[1],new[]{P(local,p[2])},new[]{p[3]});logs.OnlyTop=true;logs.Extensions=new[]{".log"};
  }
  var firefoxDump=Dump(rules,"firefox-reports","网页缓存","Firefox",new[]{P(roaming,"Mozilla\\Firefox\\Crash Reports\\pending"),P(roaming,"Mozilla\\Firefox\\Crash Reports\\submitted")},new[]{"firefox"});
  foreach(string spec in new[]{"opera-local|Opera|Opera Software\\Opera Stable|opera"}){
   var p=spec.Split('|');Add(rules,p[0],"网页缓存",p[1],"本地网页资源缓存",new[]{P(local,p[2]+"\\Cache"),P(local,p[2]+"\\Code Cache"),P(local,p[2]+"\\GPUCache")},7,true,ChromiumSource,new[]{p[3]},"独立的本地资源缓存。","访问页面时重新加载资源，保留浏览器配置。");
  }
  string[] electron={
   "obsidian|Obsidian|obsidian|Obsidian|normal","postman|Postman|Postman|Postman|normal",
   "notion|Notion|Notion|Notion|normal","github-desktop|GitHub Desktop|GitHub Desktop|GitHubDesktop|normal","figma|Figma|Figma|Figma|normal"
  };
  foreach(string spec in electron){var p=spec.Split('|');Electron(rules,p[0],p[1],P(roaming,p[2]),new[]{p[3]},p[4]=="chat");}
  Add(rules,"postman-partition","软件缓存","Postman","内嵌页面图形缓存",new[]{P(roaming,"Postman\\partitions\\postman_shell\\GPUCache")},7,true,Community,new[]{"Postman"},"请求界面的图形编译缓存。","重新编译界面资源；保留集合、环境、请求与凭据。");
  Add(rules,"teams-classic-cache","软件缓存","Teams 经典版","网页资源缓存",new[]{P(roaming,"Microsoft\\Teams\\Cache"),P(roaming,"Microsoft\\Teams\\GPUCache")},7,false,ElectronSource,new[]{"Teams"},"经典版 Teams 的独立资源缓存。","部分离线内容需重新加载；保留账号与聊天数据。");
  Logs(rules,"zoom-diagnostics","软件缓存","Zoom",new[]{P(roaming,"Zoom\\logs")},new[]{"Zoom"});
  var telegramLog=Logs(rules,"telegram-logs","软件缓存","Telegram",new[]{P(roaming,"Telegram Desktop")},new[]{"Telegram"});telegramLog.OnlyTop=true;telegramLog.NamePrefix="log";telegramLog.Extensions=new[]{".txt"};
  var telegramDump=Dump(rules,"telegram-dumps","软件缓存","Telegram",new[]{P(roaming,"Telegram Desktop\\tdata\\dumps")},new[]{"Telegram"});telegramDump.Extensions=new[]{".dmp"};
  Add(rules,"telegram-web","软件缓存","Telegram","内嵌网页缓存",new[]{P(roaming,"Telegram Desktop\\tdata\\User_Data\\Cache"),P(roaming,"Telegram Desktop\\tdata\\User_Data\\GPUCache")},14,false,Community,new[]{"Telegram"},"内嵌页面的独立资源缓存，保留 tdata 登录与聊天资料。","部分网页资源需重新加载。");
  Add(rules,"vlc-art","软件缓存","VLC","专辑封面缓存",new[]{P(roaming,"vlc\\art\\artistalbum")},14,false,"https://github.com/bleachbit/bleachbit/blob/master/cleaners/vlc.xml",new[]{"vlc"},"自动获取的专辑封面。","封面可能需要重新获取，保留媒体文件与播放列表。");
  var vlcDump=Dump(rules,"vlc-crash","软件缓存","VLC",new[]{P(roaming,"vlc\\crashdump")},new[]{"vlc"});vlcDump.Single=true;
  Logs(rules,"potplayer-log","软件缓存","PotPlayer",new[]{P(roaming,"Daum\\PotPlayer\\Log")},new[]{"PotPlayerMini","PotPlayerMini64"});
  string[] products={"IntelliJIdea","IdeaIC","PyCharm","PyCharmCE","WebStorm","CLion","Rider","GoLand"};
  foreach(string product in products){
   var owners=new[]{"idea64","pycharm64","webstorm64","phpstorm64","clion64","rider64","goland64","rubymine64","datagrip64","aqua64","dataspell64","java","javaw"};
   Add(rules,"jb-"+product.ToLowerInvariant()+"-cache","开发缓存",product,"索引缓存",Expand(P(local,"JetBrains"),product+"*\\caches").Concat(Expand(P(local,"JetBrains"),product+"*\\index")).ToArray(),30,false,JetBrainsSource,owners,"独立索引缓存；保留 LocalHistory、配置和插件。","重新建立项目索引可能耗时，开发工具运行时保留。");
   var logs=Logs(rules,"jb-"+product.ToLowerInvariant()+"-logs","开发缓存",product,Expand(P(local,"JetBrains"),product+"*\\log"),owners);logs.Source=JetBrainsSource;
  }
  Add(rules,"android-studio-index","开发缓存","Android Studio","索引缓存",Expand(P(local,"Google"),"AndroidStudio*\\caches").Concat(Expand(P(local,"Google"),"AndroidStudio*\\index")).ToArray(),30,false,JetBrainsSource,new[]{"studio64","java","javaw"},"独立索引缓存；保留 LocalHistory 和项目。","重新建立索引可能耗时。");
  var androidLogs=Logs(rules,"android-studio-logs","开发缓存","Android Studio",Expand(P(local,"Google"),"AndroidStudio*\\log"),new[]{"studio64","java","javaw"});androidLogs.Source=JetBrainsSource;
  var cargo=Add(rules,"cargo-archives","开发缓存","Rust / Cargo","依赖下载压缩包",new[]{P(profile,".cargo\\registry\\cache")},14,false,"https://doc.rust-lang.org/cargo/guide/cargo-home.html",new[]{"cargo","rustc"},"已下载的 .crate 压缩包；保留 bin、registry/src 和项目。","后续构建可能需重新下载，离线使用受影响。");cargo.Extensions=new[]{".crate"};
  var gradleLogs=Logs(rules,"gradle-logs","开发缓存","Gradle",new[]{P(profile,".gradle\\daemon")},new[]{"java","javaw","gradle"});gradleLogs.Extensions=new[]{".log"};
  var npmRoaming=Add(rules,"npm-roaming","开发缓存","npm","旧版下载缓存",new[]{P(roaming,"npm-cache\\_cacache")},14,false,Community,new[]{"node","npm"},"旧版本 npm 的漫游下载缓存；保留已安装依赖。","后续安装可能需重新下载。");
  var go=Add(rules,"go-build","开发缓存","Go","编译缓存",new[]{P(local,"go-build")},14,false,"https://pkg.go.dev/cmd/go#hdr-Build_and_test_caching",new[]{"go","compile","link"},"独立的 Go 编译缓存，不处理模块源代码。","后续编译可能变慢。");
  var conda=Add(rules,"conda-download","开发缓存","Conda","包下载压缩文件",new[]{P(profile,"anaconda3\\pkgs"),P(profile,"miniconda3\\pkgs"),P(local,"anaconda3\\pkgs"),P(local,"miniconda3\\pkgs")},30,false,"https://docs.conda.io/projects/conda/en/latest/commands/clean.html",new[]{"conda","python","pythonw"},"仅处理顶层 .conda、.bz2 下载压缩文件；保留解压包与环境。","后续安装可能需要重新下载。");conda.OnlyTop=true;conda.Extensions=new[]{".conda",".bz2"};
  var nugetOld=Add(rules,"nuget-legacy","开发缓存","NuGet","旧版下载缓存",new[]{P(local,"NuGet\\Cache")},14,false,"https://learn.microsoft.com/en-us/nuget/consume-packages/managing-the-global-packages-and-cache-folders",new[]{"dotnet","MSBuild","devenv"},"旧版 NuGet 下载缓存，不处理 global-packages。","恢复依赖时可能需要重新联网。");nugetOld.Extensions=new[]{".nupkg"};
  Add(rules,"unity-package-cache","开发缓存","Unity","包管理器下载缓存",new[]{P(local,"Unity\\cache\\npm"),P(local,"Unity\\cache\\packages")},30,false,"https://docs.unity3d.com/2022.3/Documentation/Manual/upm-cache.html",new[]{"Unity","Unity Hub","UnityPackageManager"},"Unity Package Manager 的下载副本。","后续项目依赖恢复需联网；保留项目和资产。");
  var adobeSpecs=new[]{"afterfx|After Effects|After Effects\\*\\Logs","audition|Adobe Audition|Audition\\*\\logs","bridge|Adobe Bridge|Bridge*\\Logs","camera-raw|Camera Raw|CameraRaw\\Logs","character|Character Animator|Character Animator\\*\\logs","encoder|Media Encoder|Media Encoder\\*\\logs","photoshop|Photoshop|Adobe Photoshop*\\Logs","indesign|InDesign|InDesign*\\Logs","dynamiclink|Adobe Dynamic Link|dynamiclinkmanager\\*\\logs"};
  foreach(string spec in adobeSpecs){var p=spec.Split('|');var logs=Logs(rules,"adobe-"+p[0]+"-logs","软件缓存",p[1],Expand(P(roaming,"Adobe"),p[2]),new[]{"AfterFX","Audition","Bridge","Photoshop","InDesign","Adobe Media Encoder","dynamiclinkmanager"});logs.Extensions=new[]{".log",".txt"};}
  Add(rules,"adobe-camera-raw-cache","软件缓存","Camera Raw","图像处理缓存",Expand(P(local,"Adobe"),"CameraRaw\\Cache*"),14,false,Community,new[]{"Photoshop","Bridge","Lightroom"},"已生成的图像处理缓存。","重新处理图像可能变慢；保留原图与目录。");
  var adobeDump=Dump(rules,"adobe-crash-logs","软件缓存","Adobe",new[]{P(roaming,"Adobe\\CRLogs"),P(profile,"AppData\\LocalLow\\Adobe\\CRLogs")},new[]{"Photoshop","AfterFX","Audition","InDesign"});adobeDump.Extensions=new[]{".log",".txt",".dmp"};
  var liveDump=Dump(rules,"windows-live-dumps","系统垃圾","Windows 诊断",new[]{P(windows,"LiveKernelReports")},new[]{"WerFault","wermgr"});liveDump.Extensions=new[]{".dmp"};
  foreach(string spec in new[]{"panther|Windows 安装|Panther","unattend|Windows 安装|Panther\\UnattendGC","debug|Windows 调试|Debug","update-etl|Windows 更新诊断|Logs\\WindowsUpdate"}){
   var p=spec.Split('|');var logs=Logs(rules,"win-"+p[0],"系统垃圾",p[1],new[]{P(windows,p[2])},new[]{"TiWorker","TrustedInstaller","setup","dism"});logs.Days=30;logs.OnlyTop=true;logs.Extensions=p[0]=="update-etl"?new[]{".etl"}:new[]{".log"};
  }
  var defenderLog=Logs(rules,"defender-support","系统垃圾","Microsoft Defender",new[]{P(data,"Microsoft\\Windows Defender\\Support")},new[]{"MpCmdRun"});defenderLog.Days=30;defenderLog.Extensions=new[]{".log"};
  var getCurrent=Logs(rules,"getcurrent-logs","系统垃圾","Windows 安装",new[]{P(RuleBook.TargetDrive,"$GetCurrent\\Logs")},new[]{"setup","TiWorker"});getCurrent.Days=30;getCurrent.Extensions=new[]{".log"};
  foreach(string kind in new[]{"apppaths","uninstall","run","search"}){
   string part=kind=="apppaths"?"失效应用路径":kind=="uninstall"?"失效卸载记录":kind=="run"?"运行命令历史":"资源管理器搜索历史";
   var r=Add(rules,"reg-"+kind,kind=="run"||kind=="search"?"使用痕迹":"注册表",kind=="run"||kind=="search"?"Windows":"失效注册记录",part,new string[0],kind=="run"||kind=="search"?0:7,false,kind=="apppaths"?"https://learn.microsoft.com/en-us/windows/win32/shell/app-registration":kind=="uninstall"?"https://learn.microsoft.com/en-us/windows/win32/msi/uninstall-registry-key":"https://github.com/bleachbit/bleachbit/blob/master/cleaners/windows_explorer.xml",new string[0],kind=="apppaths"?"仅识别程序及其目录均缺失的简单 App Paths 项。":kind=="uninstall"?"安装目录与卸载程序均缺失；排除 MSI、系统组件与 Microsoft 项。":"清除该项的历史列表。",kind=="run"||kind=="search"?"相关历史列表会重置；先备份，支持恢复。":"可能影响软件入口或卸载列表；默认不选，先备份再删除，支持恢复。");r.InfoKind="registry-clean";r.RegistryScopes=RegistryCleaner.Scopes(kind);r.Privacy=kind=="run"||kind=="search";r.Notice=true;
  }
  // uv explicitly requires its own cache operation; never delete its cache files directly.
  var uv=Add(rules,"uv-cache","开发缓存","uv","缓存（使用官方清理）",new[]{P(local,"uv\\cache")},0,false,"https://docs.astral.sh/uv/concepts/cache/",new string[0],"uv 缓存包含受其锁机制管理的资料。","仅展示占用，打开终端运行 uv cache clean 或 uv cache prune。");uv.Protected=true;
 }
}
}
