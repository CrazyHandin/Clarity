using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClearSpace {
public static class CommonSoftwareRules {
 public const string VscodeSource="https://code.visualstudio.com/docs/cpp/faq-cpp";
 public const string AdobeMediaSource="https://helpx.adobe.com/premiere/desktop/troubleshooting/media-issues/delete-media-cache-files-manually.html";
 public const string AdobeRawSource="https://helpx.adobe.com/camera-raw/desktop/get-started/overview-and-setup/introduction-camera-raw.html";
 static readonly string[] PersonalNames={"User","Backups","Backup","AutoRecover","AutoSave","Recovery","InDesign Recovery","Settings","Preferences","Plugins","Cookies","Login Data","Web Data","Local Storage","IndexedDB","Session Storage","Service Worker","Network","Msg","MsgAttach","FileRecv","FileStorage","Downloads","Documents","Projects","Assets","Libraries","Presets","workspaceStorage","globalStorage","agentSessionData"};
 static readonly string[] PersonalExtensions={".db",".sqlite",".sqlite3",".json",".ini",".config",".xml",".yaml",".yml",".doc",".docx",".xls",".xlsx",".ppt",".pptx",".pdf",".psd",".psb",".ai",".aep",".aepx",".prproj",".sesx",".indd",".idml",".lrcat",".xmp",".kdbx",".pem",".key",".pfx"};
 static string P(string root,string tail){return Path.Combine(root,tail);}
 static string[] Paths(string root,params string[] patterns){return patterns.SelectMany(p=>CatalogV2.Expand(root,p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
 static Rule Add(List<Rule> rules,string id,string app,string part,string[] roots,int days,bool quick,string source,string[] owners,string description,string impact){
  var r=new Rule(id,app+" "+part,"软件缓存",description,impact,roots,days,quick,owners){Software=app,Part=part,Source=source,ExcludedNames=PersonalNames,AdditionalExclude=p=>PersonalExtensions.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase),ForceBackup=!quick,AllowManual=true};rules.Add(r);return r;
 }
 static Rule Logs(List<Rule> rules,string id,string app,string[] roots,string[] owners){var r=Add(rules,id,app,"旧诊断日志",roots,14,false,CatalogV2.Community,owners,"仅处理明确日志目录内 14 天前的 .log、.txt、.xlog 文件。","历史故障诊断信息将丢失；保留设置、聊天、附件、文档和恢复数据，清理前备份。");r.Extensions=new[]{".log",".txt",".xlog"};return r;}
 static void Guard(Rule rule){rule.ExcludedNames=(rule.ExcludedNames??new string[0]).Concat(PersonalNames).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();var previous=rule.AdditionalExclude;rule.AdditionalExclude=p=>PersonalExtensions.Contains(Path.GetExtension(p),StringComparer.OrdinalIgnoreCase)||(previous!=null&&previous(p));}
 public static void Add(List<Rule> rules,string local,string roaming,string profile,string data){
  var wechat=rules.FirstOrDefault(r=>r.Id=="wechat-web");if(wechat!=null){wechat.Roots=wechat.Roots.Concat(Paths(roaming,"Tencent\\WeChat\\radium\\web\\profiles\\*\\Cache","Tencent\\WeChat\\radium\\web\\profiles\\*\\Code Cache","Tencent\\WeChat\\radium\\web\\profiles\\*\\GPUCache")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();wechat.Processes=new[]{"Weixin","WeChat","WeChatAppEx","WeChatApp","WeChatBrowser","WeixinAppEx"};wechat.Description="微信 3.x / 4.x 标准 radium/web/profiles 下的独立网页资源缓存，不遍历聊天目录。";Guard(wechat);}
  var qq=rules.FirstOrDefault(r=>r.Id=="qq-web");if(qq!=null){qq.Processes=new[]{"QQ","QQNT","QQApp","QQAppEx","QQBrowser"};qq.Description="QQ 内嵌页面 QQEX/users 下的独立 Cache、Code Cache、GPUCache；不处理 Tencent Files 或 nt_qq 聊天资料。";Guard(qq);}
  Logs(rules,"wechat-classic-logs","微信",new[]{P(roaming,"Tencent\\WeChat\\Log")},new[]{"WeChat","WeChatAppEx","Weixin"});
  var wechatDump=Add(rules,"wechat-diagnostics","微信","旧崩溃诊断",new[]{P(roaming,"Tencent\\xwechat\\crashinfo"),P(roaming,"Tencent\\xwechat\\dump")},14,false,CatalogV2.Community,new[]{"Weixin","WeChat","WeixinAppEx"},"新版微信独立 crashinfo / dump 目录中的旧转储与日志。","对应历史故障的诊断资料将丢失；先备份，不处理 config、插件、小程序或聊天存储。");wechatDump.Extensions=new[]{".dmp",".log",".txt",".xlog"};
  var browse=Logs(rules,"wechat-browser-logs","微信",Paths(local,"Tencent\\BrowsingService\\wechat","Tencent\\BrowsingService\\TGB_Market","Tencent\\BrowsingService\\unkwown"),new[]{"WeChat","Weixin","WeChatAppEx"});browse.OnlyTop=true;
  Logs(rules,"qq-page-logs","QQ",Paths(roaming,"QQEX\\logs","QQEX\\users\\*\\logs"),new[]{"QQ","QQNT","QQAppEx"});
  var code=rules.FirstOrDefault(r=>r.Id=="code-cache");if(code!=null){code.Roots=code.Roots.Concat(new[]{P(roaming,"Code\\ShaderCache")}).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();code.Source="https://code.visualstudio.com/docs/supporting/faq";Guard(code);}
  var vsix=rules.FirstOrDefault(r=>r.Id=="code-vsix");if(vsix!=null)vsix.Source="https://github.com/microsoft/vscode/blob/main/src/vs/platform/environment/common/environmentService.ts";
  var cpp=Add(rules,"code-cpp-ipch","VS Code","C/C++ 头文件编译缓存",new[]{P(local,"Microsoft\\vscode-cpptools\\ipch")},7,true,VscodeSource,new[]{"Code","cpptools","cpptools-srv"},"Microsoft C/C++ 扩展默认 ipch 目录内的预编译头缓存；只识别 .ipch。","下次 IntelliSense 重新编译头文件，可能暂时变慢；不处理源代码、扩展、设置与工作区状态。");cpp.Extensions=new[]{".ipch"};cpp.GroupOverride="开发缓存";
  var codeDump=Add(rules,"code-crash-reports","VS Code","旧崩溃报告",new[]{P(roaming,"Code\\Crashpad\\reports"),P(roaming,"Code\\Crashpad\\pending")},14,false,CatalogV2.ElectronSource,new[]{"Code"},"VS Code 独立 Crashpad 中 14 天前的诊断转储。","对应历史崩溃将无法用这些转储排查，先备份；保留未保存编辑、聊天与工作区。");codeDump.Extensions=new[]{".dmp"};codeDump.GroupOverride="开发缓存";
  var wps=Add(rules,"wps-http-cache","WPS", "网页资源缓存",Paths(local,"Kingsoft\\*\\cache\\http*","Kingsoft\\WPS Cloud Files\\UserData\\Default\\WebCache\\http*"),7,false,CatalogV2.Community,new[]{"wps","wpp","et","wpscloudsvr","wpscenter"},"仅识别 Kingsoft 明确 cache/http 与 WebCache/http 子目录，保留云文档本体和文档备份。","离线网页资源需重新下载；不同版本可能将资源与离线内容混用，先备份并核对。");
  var media=rules.FirstOrDefault(r=>r.Id=="adobe-media");if(media!=null){media.Software="Adobe 共享媒体缓存";media.Part="Premiere Pro / After Effects / Media Encoder / Audition";media.Description="Adobe/Common/Media Cache 与 Media Cache Files 的音频转换、波形和媒体索引副本；只处理已知缓存格式。";media.Impact="需保留原始素材并退出 Adobe 应用；下次打开工程会重建缓存，可能耗时。高级自选，始终先备份。";media.Processes=MediaOwners;media.Extensions=new[]{".cfa",".pek",".ims",".mcdb",".mpgindex"};media.Source=AdobeMediaSource;media.ForceBackup=true;Guard(media);}
  var raw=rules.FirstOrDefault(r=>r.Id=="adobe-camera-raw-cache");if(raw!=null){raw.Source=AdobeRawSource;raw.Processes=RasterOwners;raw.ForceBackup=true;raw.Extensions=new[]{".dat"};Guard(raw);}
  var bridge=Add(rules,"adobe-bridge-font-cache","Adobe Bridge","字体缓存",Paths(roaming,"Adobe\\Bridge*\\CT Font Cache"),14,false,CatalogV2.Community,new[]{"Bridge"},"Bridge 版本目录内的 CT Font Cache，仅清理字体缓存，不扫描预览和元数据数据库。","字体信息需要重新生成；不处理原图、评级与标签，清理前备份。");
  var ps=Add(rules,"adobe-photoshop-web-cache","Photoshop","旧网页临时缓存",Paths(roaming,"Adobe\\Adobe Photoshop*\\Adobe Photoshop*Settings\\web-cache-temp"),7,false,CatalogV2.Community,PhotoshopOwners,"Photoshop 设置目录中的独立 web-cache-temp 子目录；不清理整个 Settings。","欢迎页资源重新加载，先备份；保留预设、画笔、插件、历史工作与自动恢复。");
  Add(rules,"adobe-photoshop-uxp-cache","Photoshop","UXP 面板网页缓存",Paths(roaming,"Adobe\\UXP\\PluginsStorage\\PHSP\\*\\Shared\\EBWebView\\Default\\Cache","Adobe\\UXP\\PluginsStorage\\PHSP\\*\\Shared\\EBWebView\\Default\\Code Cache","Adobe\\UXP\\PluginsStorage\\PHSP\\*\\Shared\\EBWebView\\Default\\GPUCache","Adobe\\UXP\\PluginsStorage\\PHSP\\*\\Shared\\EBWebView\\Cache"),7,false,CatalogV2.Community,PhotoshopOwners,"仅识别 PHSP 版本下共享 WebView 的图片、脚本与图形缓存。","面板资源需重新加载；不处理插件文件和账户存储，先备份。");
  Add(rules,"adobe-acrobat-web-cache","Adobe Acrobat / Reader","网页资源缓存",Paths(local,"Adobe\\AcroCef\\DC\\Acrobat\\Cache").Concat(Paths(P(profile,"AppData\\LocalLow"),"Adobe\\AcroCef\\DC\\Acrobat\\Cache")).ToArray(),7,false,CatalogV2.Community,AcrobatOwners,"AcroCef 独立 Cache 目录中的界面资源，不清理 Cookie 或 PDF 文件。","在线面板资源可能需要重新下载；先备份，不处理批注、电子签名、文档与登录信息。");
  Logs(rules,"adobe-premiere-logs","Premiere Pro",Paths(roaming,"Adobe\\Premiere Pro\\*\\logs","Adobe\\Premiere Pro\\Logs"),AdobeOwners);
  Logs(rules,"adobe-illustrator-logs","Illustrator",Paths(roaming,"Adobe\\Logs\\Adobe Illustrator\\*\\Adobe Illustrator"),AdobeOwners);
  Logs(rules,"adobe-encoder-current-logs","Media Encoder",Paths(roaming,"Adobe\\Adobe Media Encoder\\*\\logs"),AdobeOwners);
  var lr=Logs(rules,"adobe-lightroom-logs","Lightroom",new[]{P(roaming,"Adobe\\Lightroom")},AdobeOwners);lr.OnlyTop=true;lr.Extensions=new[]{".log"};
  Logs(rules,"adobe-creative-cloud-logs","Creative Cloud",Paths(roaming,"Adobe\\CCX Welcome\\*\\clientLogs","Adobe\\LogTransport2\\Logs","Adobe\\LogTransport2CC\\Logs","Adobe\\ExtensibilityLibrary\\Log"),AdobeOwners);
  var install=Logs(rules,"adobe-install-logs","Creative Cloud",new[]{P(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),"Adobe\\Installers"),P(Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86),"Adobe\\Installers"),P(data,"Adobe\\UPI\\Log")},AdobeOwners);install.Days=30;install.OnlyTop=true;install.Extensions=new[]{".log"};
  foreach(var r in rules.Where(r=>r.Id.StartsWith("adobe-"))){Guard(r);if(!r.Quick)r.ForceBackup=true;}
 }
 static readonly string[] AdobeOwners={"Photoshop","Illustrator","AfterFX","Adobe Premiere Pro","Adobe Media Encoder","Audition","Bridge","InDesign","InCopy","Lightroom","LightroomClassic","Character Animator","Dreamweaver","Acrobat","AcroRd32","AcroCEF","RdrCEF","Creative Cloud","dynamiclinkmanager","dynamiclinkmediaserver"};
 static readonly string[] MediaOwners={"Adobe Premiere Pro","AfterFX","Adobe Media Encoder","Audition","Bridge","dynamiclinkmanager","dynamiclinkmediaserver"};
 static readonly string[] RasterOwners={"Photoshop","Bridge","Lightroom","LightroomClassic"};
 static readonly string[] PhotoshopOwners={"Photoshop","CEPHtmlEngine"};
 static readonly string[] AcrobatOwners={"Acrobat","AcroRd32","AcroCEF","RdrCEF"};
}
}
