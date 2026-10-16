using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace ClearSpace {
public static class SimpleCleanupRules {
 static readonly HashSet<string> ShaderIds=new HashSet<string>(new[]{"nvidia","d3d","intel-shader","amd-shader"},StringComparer.Ordinal);
 static readonly HashSet<string> Logs=new HashSet<string>(new[]{"app-diagnostic-0","app-diagnostic-1","app-diagnostic-2","app-diagnostic-3","app-diagnostic-4","app-diagnostic-5","edge-diagnostic","chrome-diagnostic","brave-diagnostic","quark-diagnostic","obsidian-logs","postman-logs","notion-logs","github-desktop-logs","figma-logs","zoom-diagnostics","potplayer-log","jb-intellijidea-logs","jb-ideaic-logs","jb-pycharm-logs","jb-pycharmce-logs","jb-webstorm-logs","jb-clion-logs","jb-rider-logs","jb-goland-logs","android-studio-logs","gradle-logs","wechat-classic-logs","wechat-browser-logs","qq-page-logs"},StringComparer.Ordinal);
 static readonly HashSet<string> Dumps=new HashSet<string>(new[]{"crash","edge-reports","chrome-reports","brave-reports","quark-reports","firefox-reports","obsidian-crash","postman-crash","notion-crash","github-desktop-crash","figma-crash","telegram-dumps","code-crash-reports","wechat-diagnostics"},StringComparer.Ordinal);
 // Called only while constructing the built-in catalog. Imported/community rules cannot opt in here.
 public static void Apply(List<Rule> rules){
  foreach(var rule in rules){
   bool shader=ShaderIds.Contains(rule.Id),log=Logs.Contains(rule.Id),dump=Dumps.Contains(rule.Id),go=rule.Id=="go-build";
   if(!(shader||log||dump||go)||rule.Protected||rule.Privacy||rule.Heuristic||rule.Shortcut||!String.IsNullOrEmpty(rule.InfoKind)||!String.IsNullOrEmpty(rule.Warning))continue;
   rule.Quick=true;rule.ForceBackup=false;rule.HeaderGuard=true;rule.Days=Math.Max(rule.Days,shader?7:14);
   rule.ExcludedNames=(rule.ExcludedNames??new string[0]).Concat(PathPolicy.SensitiveNames).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
   var previous=rule.AdditionalExclude;var current=rule;
   rule.AdditionalExclude=path=>(previous!=null&&previous(path))||new[]{".bak",".backup",".sav",".save"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase)||current.Roots.Where(root=>ScanPolicy.Inside(path,root)).Any(root=>PathPolicy.Sensitive(path,root));
   if(log){rule.Extensions=new[]{".log",".xlog"};rule.Description="独立日志目录中超过 "+rule.Days+" 天的 .log/.xlog 诊断输出；保留个人文档、配置、数据库与备份。";rule.Impact="会失去这些旧日志对应的历史故障诊断信息，不影响软件正常运行。";}
   if(dump){rule.Extensions=rule.Id=="wechat-diagnostics"?new[]{".dmp",".log",".xlog"}:new[]{".dmp"};rule.Description="明确诊断目录中超过 "+rule.Days+" 天的崩溃转储与诊断输出，不处理聊天、配置或恢复资料。";rule.Impact="会失去对应历史崩溃的诊断资料，不影响软件正常运行。";}
   if(go){rule.FileNameRegex=@"^[0-9a-f]{64}-[ad]$";rule.Description="Go 构建缓存中超过 "+rule.Days+" 天、符合 Go 哈希命名格式的编译结果；保留模块源码与标记文件。";}
  }
 }
}
}
