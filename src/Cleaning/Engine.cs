using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace ClearSpace {
public sealed class Rule {
 public string Id, Title, Category, Description, Impact;
 public string InfoKind, GroupOverride, NamePrefix, Software, Part, Source; public bool OnlyTop, Shortcut, Privacy, Notice;
 public RegistryScope[] RegistryScopes;
 public string[] ExcludedRoots,ExcludedNames;public string FileNameRegex,Detection,Warning;public bool Heuristic,ForceBackup,HeaderGuard;public Func<string,bool> AdditionalExclude;
 public string[] Roots, Processes = new string[0], Extensions;
 public int Days; public bool Quick, Protected, Single, AllowManual, ManualIgnoresPersonalNames;
 public Func<string,bool> ManualAdditionalExclude;
 public Rule(string id, string title, string category, string description, string impact, string[] roots, int days, bool quick, params string[] processes) {
  Id=id; Title=title; Category=category; Description=description; Impact=impact; Roots=roots; Days=days; Quick=quick; Processes=processes;
 }
 public bool ExplicitExcluded(string path,bool manual){string full=Engine.Normalize(path);if((ExcludedRoots??new string[0]).Any(r=>full.Equals(Engine.Normalize(r),StringComparison.OrdinalIgnoreCase)||full.StartsWith(Engine.Normalize(r)+"\\",StringComparison.OrdinalIgnoreCase)))return true;foreach(string root in Roots){string normalized=Engine.Normalize(root);if(full.StartsWith(normalized+"\\",StringComparison.OrdinalIgnoreCase)&&!(manual&&ManualIgnoresPersonalNames)){string relative=full.Substring(normalized.Length+1);if(relative.Split('\\').Any(p=>(ExcludedNames??new string[0]).Contains(p,StringComparer.OrdinalIgnoreCase)))return true;}}return false;}
 public bool Excluded(string path){string full=Engine.Normalize(path);return ExplicitExcluded(path,false)||((Heuristic||Id.StartsWith("library-"))&&Roots.Any(root=>full.StartsWith(Engine.Normalize(root)+"\\",StringComparison.OrdinalIgnoreCase)&&PathPolicy.Sensitive(path,root)));}
 public bool MatchesPattern(string path){return (NamePrefix==null||Path.GetFileName(path).StartsWith(NamePrefix,StringComparison.OrdinalIgnoreCase))&&(Extensions==null||Extensions.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))&&(String.IsNullOrEmpty(FileNameRegex)||Regex.IsMatch(Path.GetFileName(path),FileNameRegex,RegexOptions.IgnoreCase,TimeSpan.FromMilliseconds(100)));}
 public bool Accept(string path){try{return !Excluded(path)&&MatchesPattern(path)&&(AdditionalExclude==null||!AdditionalExclude(path))&&(!(Heuristic||HeaderGuard)||!HeuristicScanner.LooksPersonal(path));}catch{return false;}}
 public bool ManualAccept(string path){try{var exclude=ManualAdditionalExclude??AdditionalExclude;return AllowManual&&!ExplicitExcluded(path,true)&&MatchesPattern(path)&&(exclude==null||!exclude(path));}catch{return false;}}
 public string Group { get { return GroupOverride??Groups.ForCategory(Category); } }
}
public sealed class Snapshot {
 public string Path, Condition; public long Bytes, Modified; public uint Volume, IndexHigh, IndexLow;
}
public sealed class Finding {
 public Rule Rule; public long TotalBytes, EligibleBytes; public int Files, Unreadable, Recent, Links, Changed;
 public bool Running; public List<Snapshot> Candidates = new List<Snapshot>(); public List<string> Samples = new List<string>();
 public int VirtualItems, SuspectedItems, Blocked; public string Extra;
 public bool ManualOverride;public long ManualBytes;public List<Snapshot> ManualCandidates=new List<Snapshot>();
 public List<RegistryEntry> RegistryCandidates=new List<RegistryEntry>();
 public string Status { get { return Rule.Protected ? "保留" : Running ? "使用中 · 已保护" : EligibleBytes==0 ? "暂无可清理项" : Rule.Quick ? "建议清理" : "需自行确认"; } }
}
public sealed class AuditItem { public string Rule, Path, Status, Reason; public long Bytes;public int ErrorCode; }
public sealed class CleanResult {
 public long Bytes; public int Deleted, Skipped, Failed; public bool Cancelled;public int PermissionDenied;
 public int RegistryDeleted; public string RegistryBackupPath;
 public string FileBackupManifest;public int BackupFiles;public long BackupBytes,EstimatedFreedBytes;
 public List<AuditItem> Items = new List<AuditItem>();public bool SystemCleanupStarted,SystemCleanupCompleted,HibernationDisabled;public string SystemCleanupMessage;
}
public sealed class ScanProgress {public int Completed,Total,Files,RegistryItems;public string Title,Group;public long FoundBytes;}
public static class Native {
 [StructLayout(LayoutKind.Sequential)] public struct FILETIME { public uint Low, High; public long Value { get { return ((long)High<<32)|Low; } } }
 [StructLayout(LayoutKind.Sequential)] public struct INFO { public uint Attributes; public FILETIME Creation, Access, Write; public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; public long Bytes { get { return ((long)SizeHigh<<32)|SizeLow; } } }
 [StructLayout(LayoutKind.Sequential)] public struct DISPOSITION { [MarshalAs(UnmanagedType.Bool)] public bool Delete; }
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
 [DllImport("kernel32.dll",SetLastError=true)] public static extern bool GetFileInformationByHandle(SafeFileHandle handle,out INFO info);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern uint GetFinalPathNameByHandle(SafeFileHandle handle,StringBuilder path,uint size,uint flags);
 [DllImport("kernel32.dll",SetLastError=true)] public static extern bool SetFileInformationByHandle(SafeFileHandle handle,int type,ref DISPOSITION value,uint size);
 public const uint UnsafeAttributes=0x400|0x1000|0x400000;
 public static string FinalPath(SafeFileHandle handle) {
  var buffer=new StringBuilder(32768); uint n=GetFinalPathNameByHandle(handle,buffer,(uint)buffer.Capacity,0);
  if(n==0 || n>=buffer.Capacity) throw new IOException("无法确认文件实际位置");
  string result=buffer.ToString(); if(result.StartsWith("\\\\?\\UNC\\")) return "\\\\"+result.Substring(8); if(result.StartsWith("\\\\?\\")) return result.Substring(4); return result;
 }
}
public sealed class Engine {
 static readonly object ProcessLock=new object(); static HashSet<string> ProcessNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase); static DateTime ProcessSnapshotTime=DateTime.MinValue; static bool ProcessSnapshotValid;
 public readonly List<Rule> Rules; public Func<string[],bool> IsRunning;
 public Action<ScanProgress> LiveProgress; public string RegistryBackupDirectory; ScanProgress live; long baseBytes; int baseFiles,baseRegistry;DateTime lastPulse;
 public bool UseFileBackup;public string FileBackupDirectory;
 public Func<CancellationToken,Action<string>,SystemCleanupResult> RunSystemCleanup=SystemDiskCleanup.Run,DisableHibernation=Hibernation.Disable;
 public Engine(List<Rule> rules) { Rules=rules; IsRunning=OwnersRunning;FileBackupDirectory=FileRecovery.DefaultDirectory(); }
 bool IsRecoveryPath(string path){try{string full=Normalize(path);foreach(string root in new[]{CleanerSettings.Root,FileBackupDirectory,RegistryBackupDirectory,CleaningHistory.DirectoryPath,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Logs")}){if(String.IsNullOrWhiteSpace(root))continue;string p=Normalize(root);if(full.Equals(p,StringComparison.OrdinalIgnoreCase)||full.StartsWith(p+"\\",StringComparison.OrdinalIgnoreCase))return true;}if(full.EndsWith(".csbackup",StringComparison.OrdinalIgnoreCase))return true;string directory=Directory.Exists(full)?full:Path.GetDirectoryName(full);return File.Exists(Path.Combine(directory,"files.csbackup"));}catch{return true;}}
 static void ResetProcessSnapshot() { lock(ProcessLock) { ProcessSnapshotTime=DateTime.MinValue; } }
 public static bool OwnersRunning(string[] owners) {
  if(owners.Length==0) return false;
  lock(ProcessLock) {
   if((DateTime.UtcNow-ProcessSnapshotTime).TotalMilliseconds>=250) {
    var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase); bool valid=true;
    try { foreach(var p in Process.GetProcesses()) { using(p) { try { names.Add(p.ProcessName); } catch { valid=false; } } } } catch { valid=false; }
    ProcessNames=names; ProcessSnapshotValid=valid; ProcessSnapshotTime=DateTime.UtcNow;
   }
   return !ProcessSnapshotValid || owners.Any(owner=>ProcessNames.Contains(owner));
  }
 }
 public static string Normalize(string path) { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar); }
 public static bool InScope(Rule rule,string path) {
  string full; try { full=Normalize(path); } catch { return false; }
  foreach(string r in rule.Roots) { string root=Normalize(r); if(rule.Single ? String.Equals(full,root,StringComparison.OrdinalIgnoreCase) : rule.OnlyTop ? String.Equals(Path.GetDirectoryName(full),root,StringComparison.OrdinalIgnoreCase) : full.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase)) return true; }
  return false;
 }
 public static bool SafeAncestors(string path) {
  try { string current=Path.GetDirectoryName(Path.GetFullPath(path)); while(!String.IsNullOrEmpty(current)) { if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) return false; current=Path.GetDirectoryName(current); } return true; } catch { return false; }
 }
 Snapshot Capture(string path, Finding result, DateTime cutoff) {
  if(!SafeAncestors(path)) { result.Links++; return null; }
  using(var handle=Native.CreateFile(path,0x80,7,IntPtr.Zero,3,0x200000,IntPtr.Zero)) {
   Native.INFO info; if(handle.IsInvalid || !Native.GetFileInformationByHandle(handle,out info)) { result.Unreadable++; return null; }
   if((info.Attributes & (Native.UnsafeAttributes|0x10|1))!=0 || info.Links!=1) { result.Links++; return null; }
   if(!String.Equals(Normalize(Native.FinalPath(handle)),Normalize(path),StringComparison.OrdinalIgnoreCase)) { result.Links++; return null; }
   if(info.Write.Value>cutoff.ToFileTimeUtc()) { result.Recent++; return null; }
   return new Snapshot { Path=Path.GetFullPath(path),Bytes=info.Bytes,Modified=info.Write.Value,Volume=info.Volume,IndexHigh=info.IndexHigh,IndexLow=info.IndexLow };
  }
 }
 void InspectFile(string path,Finding result,DateTime cutoff) {
  try {
   if(ScanPolicy.Excluded(path)||IsRecoveryPath(path))return;
   var info=new FileInfo(path); if((info.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0) { result.Links++; return; }
   if(!result.Rule.MatchesPattern(path))return;
   string condition=null;if(result.Rule.Shortcut) {condition=ShortcutAudit.MissingTarget(path);if(condition==null)return;}
   result.Files++; result.TotalBytes+=info.Length;
   if(result.Samples.Count<5) result.Samples.Add(path);
   if(result.Running)return;
   Snapshot snapshot=null;if(!result.Rule.Protected&&result.Rule.Accept(path))snapshot=Capture(path,result,cutoff);
   if(snapshot!=null){snapshot.Condition=condition;result.Candidates.Add(snapshot);result.EligibleBytes+=snapshot.Bytes;}
   else if(result.Rule.ManualAccept(path)){var manual=Capture(path,result,DateTime.UtcNow);if(manual!=null){manual.Condition=condition;result.ManualCandidates.Add(manual);result.ManualBytes+=manual.Bytes;}}
   else result.Blocked++;
  } catch { result.Unreadable++; }finally{Pulse(result,false);}
 }
 void Pulse(Finding f,bool force){if(LiveProgress==null||live==null)return;if(!force&&(DateTime.UtcNow-lastPulse).TotalMilliseconds<160)return;lastPulse=DateTime.UtcNow;LiveProgress(new ScanProgress{Completed=live.Completed,Total=live.Total,Title=live.Title,Group=live.Group,FoundBytes=baseBytes+f.EligibleBytes,Files=baseFiles+f.Files,RegistryItems=baseRegistry+f.RegistryCandidates.Count});}
 public Finding ScanRule(Rule rule,CancellationToken token) {
  if(rule.InfoKind=="disable-hibernation")return Hibernation.Inspect(rule);
  var result=new Finding { Rule=rule };
  if(rule.Roots.Length>0&&rule.Roots.All(ScanPolicy.Excluded)){result.Blocked=rule.Roots.Length;result.Extra="Windows 系统目录不参与扫描";return result;}
  result.Running=(!rule.Protected||rule.AllowManual)&&IsRunning(rule.Processes);
  if(rule.InfoKind=="system-disk-cleanup"){result.VirtualItems=SystemDiskCleanup.Available?1:0;result.Extra=result.VirtualItems>0?"由 Windows 估算；默认不选，处理后自动退出":"当前系统未找到 cleanmgr.exe";return result;}
  if(rule.InfoKind=="registry-clean") {RegistryCleaner.Scan(rule,result,token);return result;}
  if(rule.InfoKind=="registry") {RegistryAudit.Read(result,token);return result;}
  var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase); DateTime cutoff=DateTime.UtcNow.AddDays(-rule.Days);
  foreach(string rawRoot in rule.Roots) {
   token.ThrowIfCancellationRequested(); string root; try { root=Normalize(rawRoot); } catch { result.Unreadable++; continue; }
   if(ScanPolicy.Excluded(root)){result.Blocked++;continue;}
   if(!seen.Add(root)) continue;
   if(rule.Single) { if(File.Exists(root)) InspectFile(root,result,cutoff); continue; }
   if(!Directory.Exists(root)) continue;
   if(!SafeAncestors(Path.Combine(root,"probe"))) { result.Links++; continue; }
   var pending=new Stack<string>(); pending.Push(root);
   while(pending.Count>0) {
    token.ThrowIfCancellationRequested(); string current=pending.Pop();
    try {
     if(ScanPolicy.Excluded(current))continue;
     if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) { result.Links++; continue; }
     foreach(var entry in new DirectoryInfo(current).GetFileSystemInfos()) {
      token.ThrowIfCancellationRequested();
      if(ScanPolicy.Excluded(entry.FullName))continue;
      if((entry.Attributes&FileAttributes.ReparsePoint)!=0) { result.Links++; continue; }
      if((entry.Attributes&FileAttributes.Directory)!=0) {if(!rule.OnlyTop&&(!rule.Excluded(entry.FullName)||rule.AllowManual&&!rule.ExplicitExcluded(entry.FullName,true))&&!IsRecoveryPath(entry.FullName))pending.Push(entry.FullName);}
      else if(InScope(rule,entry.FullName)) InspectFile(entry.FullName,result,cutoff);
     }
    } catch(OperationCanceledException) { throw; } catch { result.Unreadable++; }
   }
  }
  return result;
 }
 public List<Finding> Scan(CancellationToken token,Action<int,int,string> progress) {
  ResetProcessSnapshot();var results=new List<Finding>();var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);baseBytes=0;baseFiles=0;baseRegistry=0;
  for(int i=0;i<Rules.Count;i++) {token.ThrowIfCancellationRequested();live=new ScanProgress{Completed=i,Total=Rules.Count,Title=Rules[i].Title,Group=Rules[i].Group};if(progress!=null)progress(i,Rules.Count,Rules[i].Title);var f=ScanRule(Rules[i],token);f.Candidates.RemoveAll(s=>!paths.Add(s.Path));f.EligibleBytes=f.Candidates.Sum(s=>s.Bytes);if(f.Files>0||f.Unreadable>0||f.Links>0||f.Rule.Notice||f.RegistryCandidates.Count>0)results.Add(f);live.Completed=i+1;Pulse(f,true);baseBytes+=f.EligibleBytes;baseFiles+=f.Files;baseRegistry+=f.RegistryCandidates.Count;}
  foreach(var f in results){f.ManualCandidates.RemoveAll(s=>!paths.Add(s.Path));f.ManualBytes=f.ManualCandidates.Sum(s=>s.Bytes);}live=null;
  return results.OrderBy(f=>f.Rule.Protected).ThenByDescending(f=>f.TotalBytes).ToList();
 }
 bool DeleteSnapshot(Rule rule,Snapshot snapshot,bool manual,RecoverySession recovery,CancellationToken token,out string reason,out int errorCode) {
  errorCode=0;
  if(ScanPolicy.Excluded(snapshot.Path)){reason="Windows 系统目录不参与清理";return false;}
  if(IsRecoveryPath(snapshot.Path)){reason="文件位于软件配置或恢复备份范围";return false;}
  reason="";
  if(!Rules.Contains(rule)||!InScope(rule,snapshot.Path)||(manual?!rule.ManualAccept(snapshot.Path):rule.Protected||!rule.Accept(snapshot.Path))) { reason="不在允许清理的范围"; return false; }
  if(!SafeAncestors(snapshot.Path)) { reason="路径已改变或包含链接"; return false; }
  using(var handle=Native.CreateFile(snapshot.Path,recovery==null?0x10080u:0x10081u,1,IntPtr.Zero,3,0x200000,IntPtr.Zero)) {
   Native.INFO info;
   if(handle.IsInvalid || !Native.GetFileInformationByHandle(handle,out info)) { errorCode=Marshal.GetLastWin32Error();reason="文件正在使用、无权限或已不存在"; return false; }
   if((info.Attributes&(Native.UnsafeAttributes|0x10|1))!=0 || info.Links!=1) { reason="链接、云端、只读或受保护文件"; return false; }
   if(!String.Equals(Normalize(Native.FinalPath(handle)),Normalize(snapshot.Path),StringComparison.OrdinalIgnoreCase)) { reason="文件实际位置已改变"; return false; }
   if(info.Volume!=snapshot.Volume || info.IndexHigh!=snapshot.IndexHigh || info.IndexLow!=snapshot.IndexLow || info.Bytes!=snapshot.Bytes || info.Write.Value!=snapshot.Modified) { reason="扫描后文件发生变化，请重新扫描"; return false; }
   if(info.Write.Value>DateTime.UtcNow.AddDays(manual?0:-rule.Days).ToFileTimeUtc()) { reason="文件最近仍在使用"; return false; }
   if(rule.Shortcut && (String.IsNullOrEmpty(snapshot.Condition)||!ShortcutAudit.ProvenMissingLocalTarget(snapshot.Condition))) {reason="快捷方式目标恢复或无法确认失效";return false;}
   if(recovery!=null)recovery.Save(handle,rule,snapshot,token);token.ThrowIfCancellationRequested();
   var disposition=new Native.DISPOSITION { Delete=true };
   if(!Native.SetFileInformationByHandle(handle,4,ref disposition,(uint)Marshal.SizeOf(typeof(Native.DISPOSITION)))) { errorCode=Marshal.GetLastWin32Error();reason="系统阻止删除（"+errorCode+"）"; return false; }
  }
  if(File.Exists(snapshot.Path)) { reason="系统尚未完成删除"; return false; }
  return true;
 }
 public CleanResult Clean(IEnumerable<Finding> chosen,bool quick,CancellationToken token,Action<int,int,string> progress) {
  ResetProcessSnapshot();var result=new CleanResult();var selected=chosen.Where(f=>Rules.Contains(f.Rule)&&(f.ManualOverride?f.Rule.AllowManual&&!quick:!f.Rule.Protected&&(!quick||f.Rule.Quick))).ToList();
  int total=selected.Sum(f=>f.Candidates.Count), done=0;
  var registryChosen=selected.Where(f=>Rules.Contains(f.Rule)&&f.Rule.InfoKind=="registry-clean"&&!f.Rule.Quick).ToList();
  RecoverySession recovery=null;if(selected.Any(f=>f.Candidates.Count>0&&(UseFileBackup||f.Rule.Heuristic||f.Rule.ForceBackup||f.ManualOverride))){recovery=new RecoverySession(FileBackupDirectory);result.FileBackupManifest=recovery.Manifest;}
  if(!quick&&registryChosen.Count>0){try{RegistryCleaner.Clean(registryChosen,RegistryBackupDirectory,token,result);}catch(OperationCanceledException){result.Cancelled=true;return result;}catch(Exception ex){result.Failed++;if(ex is UnauthorizedAccessException)result.PermissionDenied++;foreach(var f in registryChosen)foreach(var e in f.RegistryCandidates)if(!result.Items.Any(i=>i.Path==RegistryCleaner.Label(e)))result.Items.Add(new AuditItem{Rule=f.Rule.Title,Path=RegistryCleaner.Label(e),Status="清理失败",Reason=ex.Message});}}
  foreach(var finding in selected) {
   foreach(var file in finding.Candidates) {
    if(token.IsCancellationRequested) { result.Cancelled=true; return result; }
    string reason=""; bool removed=false;int errorCode=0;
    try {
     if(!Rules.Contains(finding.Rule)) reason="识别规则已变化";
     else if(IsRunning(finding.Rule.Processes)) reason="相关程序已启动，自动保留";
     else removed=DeleteSnapshot(finding.Rule,file,finding.ManualOverride,UseFileBackup||finding.Rule.Heuristic||finding.Rule.ForceBackup||finding.ManualOverride?recovery:null,token,out reason,out errorCode);
    } catch(OperationCanceledException){result.Cancelled=true;return result;}catch(UnauthorizedAccessException e){result.Failed++;errorCode=5;reason=e.Message;}catch(Exception e) { result.Failed++; reason=e.Message; }
    if(errorCode==5)result.PermissionDenied++;
    if(removed) { result.Deleted++; result.Bytes+=file.Bytes;if(UseFileBackup||finding.Rule.Heuristic||finding.Rule.ForceBackup||finding.ManualOverride){result.BackupFiles++;result.BackupBytes+=file.Bytes;if(!String.Equals(Path.GetPathRoot(file.Path),Path.GetPathRoot(recovery.DirectoryPath),StringComparison.OrdinalIgnoreCase))result.EstimatedFreedBytes+=file.Bytes;}else result.EstimatedFreedBytes+=file.Bytes; } else result.Skipped++;
    result.Items.Add(new AuditItem { Rule=finding.Rule.Title,Path=file.Path,Status=removed?"已删除":"已跳过",Reason=reason,Bytes=removed?file.Bytes:0,ErrorCode=errorCode });
    done++; if(progress!=null) progress(done,total,finding.Rule.Title);
   }
  }
  if(!quick)foreach(var f in selected.Where(f=>f.Rule.InfoKind=="disable-hibernation"&&f.VirtualItems>0)){
   if(token.IsCancellationRequested){result.Cancelled=true;return result;}
   try{var action=DisableHibernation(token,title=>{if(progress!=null)progress(-1,0,title);});if(action.Completed){result.HibernationDisabled=true;result.Bytes+=action.Bytes;result.EstimatedFreedBytes+=action.Bytes;if(action.Bytes>0)result.Deleted++;}else result.Failed++;result.Items.Add(new AuditItem{Rule=f.Rule.Title,Path=Hibernation.FilePath,Status=action.Completed?"已删除":"清理失败",Reason=action.Message,Bytes=action.Bytes});}
   catch(OperationCanceledException){result.Cancelled=true;return result;}catch(Exception ex){result.Failed++;int code=ex is UnauthorizedAccessException?5:0;if(code==5)result.PermissionDenied++;result.Items.Add(new AuditItem{Rule=f.Rule.Title,Path=Hibernation.FilePath,Status="清理失败",Reason=ex.Message,ErrorCode=code});}
  }
  if(!quick&&!token.IsCancellationRequested&&selected.Any(f=>f.Rule.InfoKind=="system-disk-cleanup"&&f.VirtualItems>0)){result.SystemCleanupStarted=true;try{var system=RunSystemCleanup(token,title=>{if(progress!=null)progress(-1,0,title);});result.SystemCleanupCompleted=system.Completed;result.SystemCleanupMessage=system.Message;if(!system.Completed)result.Failed++;result.Items.Add(new AuditItem{Rule="Windows 磁盘清理",Path="C:\\",Status=system.Completed?"系统清理完成":"系统清理未完成",Reason=system.Message});}catch(Exception ex){result.Failed++;result.SystemCleanupMessage=ex.Message;result.Items.Add(new AuditItem{Rule="Windows 磁盘清理",Path="C:\\",Status="系统清理未完成",Reason=ex.Message});}}
  if(token.IsCancellationRequested)result.Cancelled=true;return result;
 }
}
public static class RuleBook {
 public static string TargetDrive { get { try { if(new DriveInfo("C:\\").IsReady)return "C:\\"; } catch { } return Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)); } }
 static Rule Add(List<Rule> rules,string id,string title,string category,string desc,string impact,string[] roots,int days,bool quick,params string[] processes) { var r=new Rule(id,title,category,desc,impact,roots,days,quick,processes); rules.Add(r); return r; }
 static string P(string root,string tail) { return Path.Combine(root,tail); }
 static string[] BrowserRoots(string root,string[] tails) {
  var result=new List<string>(); if(!Directory.Exists(root)) return result.ToArray();
  try { foreach(var dir in new DirectoryInfo(root).GetDirectories()) { if(dir.Name=="Default" || dir.Name.StartsWith("Profile ",StringComparison.OrdinalIgnoreCase)) foreach(string tail in tails) result.Add(P(dir.FullName,tail)); } } catch { }
  return result.ToArray();
 }
 public static List<Rule> Create() {
  string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
  string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), low=P(profile,"AppData\\LocalLow"), windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
  string programData=Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), pf=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
  var rules=new List<Rule>();
  Add(rules,"code-vsix","VS Code 扩展安装包","开发工具","扩展下载安装包的缓存，不是已经安装的扩展。仅识别 7 天前的缓存。","以后重装扩展时可能需要重新下载；设置、扩展、聊天记录和未保存文件备份不受影响。",new[]{P(roaming,"Code\\CachedExtensionVSIXs")},7,true,"Code","Code - Insiders");
  Add(rules,"code-cache","VS Code 编译与图形缓存","开发工具","缓存编译结果和界面资源，可按需重新生成。仅处理 1 天前的文件。","下次启动可能稍慢。不会处理 User、Backups、agentSessionData 或已安装扩展。",new[]{P(roaming,"Code\\CachedData"),P(roaming,"Code\\Cache"),P(roaming,"Code\\Code Cache"),P(roaming,"Code\\GPUCache"),P(roaming,"Code\\DawnGraphiteCache"),P(roaming,"Code\\DawnWebGPUCache")},1,true,"Code","Code - Insiders");
  Add(rules,"pip","Python 下载缓存","开发工具","pip 下载的依赖安装包与 HTTP 缓存。仅处理 7 天前的文件，不删除已安装的依赖。","后续安装依赖可能重新联网下载。Python 或 pip 正在运行时整项保留。",new[]{P(local,"pip\\cache")},7,true,"python","pythonw","pip","pip3");
  Add(rules,"npm","npm 下载缓存","开发工具","npm 的内容缓存与历史日志。仅处理 7 天前的文件，不处理 node_modules。","后续安装依赖可能重新下载；Node.js 或 npm 正在运行时整项保留。",new[]{P(local,"npm-cache\\_cacache"),P(local,"npm-cache\\_logs")},7,true,"node","npm");
  Add(rules,"edge","Edge 网页资源缓存","浏览器","网页图片、脚本及图形缓存，关闭 Edge 后可重新生成。仅处理 1 天前的文件。","保留密码、收藏、历史记录、Cookie 和登录状态；再次访问网页时重新加载资源。",BrowserRoots(P(local,"Microsoft\\Edge\\User Data"),new[]{"Cache","Code Cache","GPUCache","DawnGraphiteCache","DawnWebGPUCache"}),1,true,"msedge");
  Add(rules,"chrome","Chrome 网页资源缓存","浏览器","只识别独立的网页资源缓存目录。仅处理 1 天前的文件。","保留浏览数据和登录信息；Chrome 正在运行时不清理。",BrowserRoots(P(local,"Google\\Chrome\\User Data"),new[]{"Cache","Code Cache","GPUCache"}),1,true,"chrome");
  Add(rules,"quark","夸克网页资源缓存","浏览器","浏览器网页图片与脚本缓存。仅处理 1 天前的文件。","重新访问页面时下载资源，不删除浏览器配置或账号数据。",BrowserRoots(P(local,"Quark\\User Data"),new[]{"Cache","Code Cache","GPUCache"}),1,true,"Quark","quark");
  Add(rules,"steam","Steam 网页资源缓存","软件缓存","Steam 商店和社区页面的图片、脚本缓存。仅处理 1 天前的文件。","不处理游戏、存档、登录数据库或整个 htmlcache 目录。",new[]{P(local,"Steam\\htmlcache\\Default\\Cache"),P(local,"Steam\\htmlcache\\Default\\Code Cache"),P(local,"Steam\\htmlcache\\GPUCache")},1,true,"steam","steamwebhelper");
  var epicRoots=new List<string>(); string epicSaved=P(local,"EpicGamesLauncher\\Saved"); if(Directory.Exists(epicSaved)) { try { foreach(var d in new DirectoryInfo(epicSaved).GetDirectories("webcache*")) { epicRoots.Add(P(d.FullName,"Cache")); epicRoots.Add(P(d.FullName,"Code Cache")); } } catch { } }
  Add(rules,"epic-cache","Epic 网页资源缓存","软件缓存","Epic 启动器网页界面的资源缓存。仅处理 1 天前的文件。","不处理游戏、存档、账号信息及下载任务。",epicRoots.ToArray(),1,true,"EpicGamesLauncher","EpicWebHelper");
  Add(rules,"nvidia","NVIDIA 显卡着色器缓存","显卡缓存","显卡保存的编译结果。本次 C 盘分析中发现过约 24 GB；实际容量以当前扫描为准。","清理后游戏或渲染程序会重新编译，首次启动可能变慢或短暂卡顿。请先关闭游戏和渲染软件。",new[]{P(local,"NVIDIA\\DXCache"),P(local,"NVIDIA\\GLCache"),P(low,"NVIDIA\\DXCache"),P(roaming,"NVIDIA\\ComputeCache"),P(local,"NVIDIA Corporation\\NV_Cache")},1,false,"NVIDIA App","NVIDIA Overlay","Blender","Cinema 4D","UnrealEditor","Unity","Photoshop","AfterFX","cs2","VALORANT-Win64-Shipping","FortniteClient-Win64-Shipping","GenshinImpact","StarRail");
  Add(rules,"qqmusic","QQ 音乐音频缓存","影音缓存","播放歌曲、歌词和图片时产生的缓存，限定 QQMusicCache 目录。","会失去部分离线缓存，歌曲可能需要重新联网加载。建议优先使用 QQ 音乐自带清理功能。",new[]{P(roaming,"Tencent\\QQMusic\\QQMusicCache")},7,false,"QQMusic");
  Add(rules,"temp","用户临时文件","临时文件","软件安装、解压或运行产生的临时资料，只考虑 7 天前的文件。","某些软件可能保留临时工作资料。请先保存工作并关闭相关软件；正在占用的文件会跳过。",new[]{P(local,"Temp")},7,false);
  Add(rules,"system-temp","Windows 临时文件","临时文件","Windows 临时目录中 7 天前的文件。权限不足和正在使用的文件自动保留。","可能包含安装任务的中间资料，请确认没有安装或更新任务。通常需要管理员权限。",new[]{P(windows,"Temp")},7,false,"msiexec","TiWorker","TrustedInstaller");
  var crash=Add(rules,"crash","软件崩溃转储","诊断资料","应用异常退出时生成的诊断文件，不是运行必需文件。","删除后无法用这些转储排查历史崩溃。不影响应用正常使用。",new[]{P(local,"CrashDumps")},1,false); crash.Extensions=new[]{".dmp"};
  var memory=Add(rules,"memory","系统崩溃转储 MEMORY.DMP","诊断资料","蓝屏或系统崩溃时生成的大型诊断文件。只识别指定文件，不扫描或清理 Windows 系统组件。","删除后无法用这个文件排查对应蓝屏。通常需要管理员权限；近期转储保留 1 天。",new[]{P(windows,"MEMORY.DMP")},1,false); memory.Single=true;
  Add(rules,"logs","旧软件诊断日志","诊断资料","微信、Epic、VS Code、WPS、OneDrive 的独立日志目录。仅处理 14 天前的文件。","旧诊断记录将无法恢复；不会删除聊天记录、文档或同步文件。",new[]{P(roaming,"Tencent\\xwechat\\log"),P(roaming,"Tencent\\Logs"),P(local,"Epic Games\\Epic Online Services\\InstallHelper\\Logs"),P(roaming,"Code\\logs"),P(roaming,"Kingsoft\\office6\\log"),P(local,"Microsoft\\OneDrive\\logs")},14,false,"Weixin","WeChat","Code","EpicGamesLauncher","OneDrive","wps");
  foreach(string updater in new[]{"unityhub-updater"}) {
   string title="Unity Hub 更新安装包";
   string process="Unity Hub";
   var update=Add(rules,"update-"+updater,title,"更新资料","发现下载的安装包，包括 pending 子目录。仅列为候选，不自动判定更新已完成；只处理 7 天前的安装包。","请确认软件已更新成功、没有待安装更新，并可重新下载。pending 不代表已完成，不能仅凭文件日期判断。",new[]{P(local,updater)},7,false,process,"Update","msiexec");
   update.Extensions=new[]{".exe",".msi",".msix",".zip",".nupkg",".blockmap"};
  }
  var driver=Add(rules,"driver-download","NVIDIA 驱动下载资料","更新资料","NVIDIA App 下载的驱动安装资料，限定 ota-artifacts 目录。仅识别 14 天前的安装包。","确认驱动已安装成功且不需本地备份后再选。不要在驱动更新期间清理。通常需要管理员权限。",new[]{P(programData,"NVIDIA Corporation\\NVIDIA App\\UpdateFramework\\ota-artifacts")},14,false,"NVIDIA App","setup","msiexec"); driver.Extensions=new[]{".exe",".zip",".cab",".msi"};
  var office=Add(rules,"office","Office 更新资料（保留）","受保护资料","Office 的更新下载资料可能用于安装、修复或回退。无法仅凭目录判断为垃圾。","删除可能影响安装、修复或回退。默认保留；高级自选需确认更新已完成并强制备份。",new[]{P(pf,"Microsoft Office\\Updates\\Download"),P(Environment.GetEnvironmentVariable("ProgramW6432")??pf,"Microsoft Office\\Updates\\Download"),P(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Microsoft Office\\Updates\\Download")},0,false); office.Protected=true;
  rules.Add(Hibernation.CreateRule());
  foreach(string systemFile in new[]{"pagefile.sys"}) { var s=Add(rules,"system-"+systemFile,"虚拟内存文件（保留）","受保护资料","属于 Windows 正常运行使用的系统文件，不是垃圾。","本工具不删除，也不修改相关系统设置。",new[]{Path.GetPathRoot(windows)+systemFile},0,false); s.Protected=true; s.Single=true; }
  ExtendedRules.Add(rules,local,roaming,profile,windows,programData);
  CatalogV2.Upgrade(rules,local,roaming,profile,windows,programData);
  CommonSoftwareRules.Add(rules,local,roaming,profile,programData);SimpleCleanupRules.Apply(rules);
  foreach(var rule in rules)if(rule.Roots.Length>0&&String.IsNullOrEmpty(rule.InfoKind)&&!rule.Id.StartsWith("system-"))rule.AllowManual=true;
  rules.First(r=>r.Id=="office").Processes=new[]{"OfficeClickToRun","msiexec","setup"};rules.First(r=>r.Id=="uv-cache").Processes=new[]{"uv"};

  rules.RemoveAll(r=>r.Roots.Length>0&&r.Roots.All(ScanPolicy.Excluded));
  foreach(var rule in rules) rule.Roots=rule.Roots.Where(r=>!ScanPolicy.Excluded(r)&&String.Equals(Path.GetPathRoot(Path.GetFullPath(r)),TargetDrive,StringComparison.OrdinalIgnoreCase)).ToArray();
  return rules;
 }
}
}
