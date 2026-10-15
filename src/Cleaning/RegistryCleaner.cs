using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace ClearSpace {
public sealed class RegistryScope {
 public RegistryHive Hive;public RegistryView View;public string Path,Kind;
}
public sealed class RegistryValueData {public string Name,Data;public string[] Strings;public int Kind;}
public sealed class RegistryEntry {
 public RegistryScope Scope;public string KeyPath,Title,Target,Fingerprint;public long Modified;
 public List<RegistryValueData> Values=new List<RegistryValueData>();
}
public sealed class RegistryBackup {
 public string Format="ClearSpace.RegistryBackup.v2",Computer,UserSid,Created;
 public List<RegistryEntry> Entries=new List<RegistryEntry>();
}
public static class RegistryCleaner {
 public const string AppPaths=@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
 public const string Uninstall=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
 public const string RunHistory=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RunMRU";
 public const string SearchHistory=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\WordWheelQuery";
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode)]static extern int RegQueryInfoKey(SafeRegistryHandle key,IntPtr cls,IntPtr clsLength,IntPtr reserved,IntPtr subkeys,IntPtr maxName,IntPtr maxClass,IntPtr values,IntPtr maxValue,IntPtr maxData,IntPtr security,out long time);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode)]static extern int RegCreateKeyEx(SafeRegistryHandle parent,string name,int reserved,string cls,int options,int access,IntPtr security,out SafeRegistryHandle handle,out int disposition);
 public static RegistryScope[] Scopes(string kind){
  string path=kind=="apppaths"?AppPaths:kind=="uninstall"?Uninstall:kind=="run"?RunHistory:SearchHistory;
  var views=Environment.Is64BitOperatingSystem?new[]{RegistryView.Registry64,RegistryView.Registry32}:new[]{RegistryView.Registry32};
  var scopes=new List<RegistryScope>();foreach(var hive in kind=="apppaths"||kind=="uninstall"?new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine}:new[]{RegistryHive.CurrentUser})foreach(var view in views)scopes.Add(new RegistryScope{Hive=hive,View=view,Path=path,Kind=kind});return scopes.ToArray();
 }
 public static bool Allowed(RegistryScope s){
  if(s==null||(s.View!=RegistryView.Registry32&&s.View!=RegistryView.Registry64)||(s.Hive!=RegistryHive.CurrentUser&&s.Hive!=RegistryHive.LocalMachine))return false;
  string expected=s.Kind=="apppaths"?AppPaths:s.Kind=="uninstall"?Uninstall:s.Kind=="run"?RunHistory:s.Kind=="search"?SearchHistory:null;if(expected==null)return false;
  if(String.Equals(s.Path,expected,StringComparison.OrdinalIgnoreCase))return (s.Kind=="apppaths"||s.Kind=="uninstall"||s.Hive==RegistryHive.CurrentUser);
#if CLARITY_TESTS
  string[] p=(s.Path??"").Split('\\');string tail=s.Kind=="apppaths"?"App Paths":s.Kind=="uninstall"?"Uninstall":s.Kind=="run"?"RunMRU":"WordWheelQuery";Guid id;
  return s.Hive==RegistryHive.CurrentUser&&p.Length==4&&p[0]=="Software"&&p[1]=="ClearSpace.Tests"&&Guid.TryParseExact(p[2],"N",out id)&&p[3]==tail;
#else
  return false;
#endif
 }
 static bool InScope(RegistryEntry e){
  if(e==null||!Allowed(e.Scope)||String.IsNullOrWhiteSpace(e.KeyPath))return false;
  if(e.Scope.Kind=="run"||e.Scope.Kind=="search")return String.Equals(e.KeyPath,e.Scope.Path,StringComparison.OrdinalIgnoreCase);
  string prefix=e.Scope.Path+"\\";return e.KeyPath.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&e.KeyPath.Substring(prefix.Length).Length>0&&!e.KeyPath.Substring(prefix.Length).Contains("\\");
 }
 static string Fingerprint(List<RegistryValueData> values){
  string json=new JavaScriptSerializer().Serialize(values.OrderBy(v=>v.Name,StringComparer.Ordinal).ToList());
  using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-","");
 }
 static RegistryEntry Read(RegistryScope s,string path){
  if(!Allowed(s))return null;
  using(var root=RegistryKey.OpenBaseKey(s.Hive,s.View))using(var key=root.OpenSubKey(path,false)){
   if(key==null||key.SubKeyCount>0||key.ValueCount==0||key.ValueCount>256)return null;long modified;if(RegQueryInfoKey(key.Handle,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,out modified)!=0)return null;
   var e=new RegistryEntry{Scope=s,KeyPath=path,Title=path.Substring(path.LastIndexOf('\\')+1),Modified=modified};
   foreach(string name in key.GetValueNames()){
    var kind=key.GetValueKind(name);object value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);var v=new RegistryValueData{Name=name,Kind=(int)kind};
    if(kind==RegistryValueKind.Binary)v.Data=Convert.ToBase64String((byte[])value);else if(kind==RegistryValueKind.MultiString)v.Strings=(string[])value;else if(kind==RegistryValueKind.String||kind==RegistryValueKind.ExpandString||kind==RegistryValueKind.DWord||kind==RegistryValueKind.QWord)v.Data=Convert.ToString(value,CultureInfo.InvariantCulture);else return null;
    if((v.Data??"").Length>1048576)return null;e.Values.Add(v);
   }
   e.Fingerprint=Fingerprint(e.Values);return e;
  }
 }
 static string Value(RegistryEntry e,string name){var v=e.Values.FirstOrDefault(i=>String.Equals(i.Name,name,StringComparison.OrdinalIgnoreCase));return v==null?null:v.Data;}
 static string Executable(string command){
  if(String.IsNullOrWhiteSpace(command))return null;command=Environment.ExpandEnvironmentVariables(command.Trim());string path;
  if(command.StartsWith("\"")){int end=command.IndexOf('"',1);if(end<2)return null;path=command.Substring(1,end-1);}else{int end=command.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);if(end<0)return null;path=command.Substring(0,end+4);}
  try{return Path.IsPathRooted(path)&&String.Equals(Path.GetExtension(path),".exe",StringComparison.OrdinalIgnoreCase)&&!path.StartsWith(@"\\")&&String.Equals(Path.GetPathRoot(Path.GetFullPath(path)),RuleBook.TargetDrive,StringComparison.OrdinalIgnoreCase)?Path.GetFullPath(path):null;}catch{return null;}
 }
 static bool MissingExecutableAndFolder(string path){return path!=null&&ShortcutAudit.ProvenMissingLocalTarget(path)&&ShortcutAudit.ProvenMissingLocalTarget(System.IO.Path.GetDirectoryName(path));}
 public static bool Qualifies(RegistryEntry e){
  if(!InScope(e))return false;
  if(e.Scope.Kind=="run"||e.Scope.Kind=="search")return e.Values.Any(v=>v.Name!="MRUList"&&v.Name!="MRUListEx");
  if(e.Scope.Kind=="apppaths"){
   if(e.Values.Any(v=>v.Name!=""&&v.Name!="Path"))return false;string target=Executable(Value(e,""));if(!MissingExecutableAndFolder(target)||!String.Equals(System.IO.Path.GetFileName(target),e.Title,StringComparison.OrdinalIgnoreCase))return false;e.Target=target;return true;
  }
  Guid product;string publisher=Value(e,"Publisher")??"",title=Value(e,"DisplayName")??"";if(String.IsNullOrWhiteSpace(title)||Guid.TryParse(e.KeyPath.Substring(e.KeyPath.LastIndexOf('\\')+1),out product)||publisher.IndexOf("Microsoft",StringComparison.OrdinalIgnoreCase)>=0||Value(e,"WindowsInstaller")=="1"||Value(e,"SystemComponent")=="1"||!String.IsNullOrEmpty(Value(e,"ParentKeyName"))||!String.IsNullOrEmpty(Value(e,"ReleaseType")))return false;
  string directory=Environment.ExpandEnvironmentVariables((Value(e,"InstallLocation")??"").Trim().Trim('"')),uninstaller=Executable(Value(e,"UninstallString"));
  if(!ShortcutAudit.ProvenMissingLocalTarget(directory)||!MissingExecutableAndFolder(uninstaller))return false;e.Target=directory+" | "+uninstaller;e.Title=title;return true;
 }
 public static void Scan(Rule rule,Finding f,CancellationToken token){
  foreach(var s in rule.RegistryScopes??new RegistryScope[0]){
   if(!Allowed(s)){f.Unreadable++;continue;}token.ThrowIfCancellationRequested();
   try{using(var root=RegistryKey.OpenBaseKey(s.Hive,s.View))using(var parent=root.OpenSubKey(s.Path,false)){
    if(parent==null)continue;var paths=s.Kind=="run"||s.Kind=="search"?new[]{s.Path}:parent.GetSubKeyNames().Select(n=>s.Path+"\\"+n).ToArray();
    foreach(string path in paths){token.ThrowIfCancellationRequested();f.VirtualItems++;var e=Read(s,path);if(e==null||!Qualifies(e))continue;if(e.Modified>DateTime.UtcNow.AddDays(-rule.Days).ToFileTimeUtc()){f.Recent++;continue;}if(f.RegistryCandidates.Any(i=>i.Scope.Hive==e.Scope.Hive&&i.KeyPath==e.KeyPath&&i.Fingerprint==e.Fingerprint))continue;f.RegistryCandidates.Add(e);if(f.Samples.Count<5)f.Samples.Add(Label(e));}
   }}catch(OperationCanceledException){throw;}catch{f.Unreadable++;}
  }
  f.SuspectedItems=f.RegistryCandidates.Count;f.Extra="检查 "+f.VirtualItems+" 项，可处理 "+f.RegistryCandidates.Count+" 项。注册表按条数统计，不计入磁盘释放空间。";
 }
 public static string Label(RegistryEntry e){return (e.Scope.Hive==RegistryHive.CurrentUser?"HKCU":"HKLM")+" ["+(e.Scope.View==RegistryView.Registry64?"64":"32")+"]\\"+e.KeyPath;}
 static bool Same(RegistryEntry a,RegistryEntry b){return b!=null&&a.Fingerprint==b.Fingerprint&&a.Modified==b.Modified;}
 public static void Clean(List<Finding> findings,string directory,CancellationToken token,CleanResult result){
  var entries=new List<RegistryEntry>();
  foreach(var f in findings)foreach(var e in f.RegistryCandidates){token.ThrowIfCancellationRequested();if(f.Rule.Protected||f.Rule.Quick||!(f.Rule.RegistryScopes??new RegistryScope[0]).Any(s=>s.Hive==e.Scope.Hive&&s.View==e.Scope.View&&s.Path==e.Scope.Path&&s.Kind==e.Scope.Kind)||!InScope(e))continue;try{var fresh=Read(e.Scope,e.KeyPath);if(Same(e,fresh)&&Qualifies(fresh)&&fresh.Modified<=DateTime.UtcNow.AddDays(-f.Rule.Days).ToFileTimeUtc()&&!entries.Any(i=>Label(i)==Label(e)))entries.Add(fresh);else{result.Skipped++;result.Items.Add(new AuditItem{Rule=f.Rule.Title,Path=Label(e),Status="已跳过",Reason="注册表或关联路径在扫描后变化"});}}catch{result.Skipped++;}}
  if(entries.Count==0)return;if(String.IsNullOrEmpty(directory))throw new IOException("必须先指定注册表备份位置。");Directory.CreateDirectory(directory);if(!Engine.SafeAncestors(Path.Combine(directory,"probe")))throw new IOException("备份目录包含链接或无法验证。");
  var backup=new RegistryBackup{Computer=Environment.MachineName,UserSid=WindowsIdentity.GetCurrent().User.Value,Created=DateTime.Now.ToString("o"),Entries=entries};
  string file=Path.Combine(directory,"registry-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")+".json");
  string json=new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue}.Serialize(backup);using(var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){byte[] bytes=Encoding.UTF8.GetBytes(json);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
  var saved=new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue}.Deserialize<RegistryBackup>(File.ReadAllText(file));if(saved==null||saved.Entries.Count!=entries.Count||!String.Equals(new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue}.Serialize(saved),json,StringComparison.Ordinal))throw new IOException("注册表备份校验失败，未删除。");result.RegistryBackupPath=file;
  foreach(var e in entries){
   if(token.IsCancellationRequested){result.Cancelled=true;return;}
   var item=new AuditItem{Rule="注册表",Path=Label(e)};
   try{var fresh=Read(e.Scope,e.KeyPath);if(!Same(e,fresh)||!Qualifies(fresh)){item.Status="已跳过";item.Reason="注册表或目标路径变化";result.Skipped++;}
    else{string parentPath=e.KeyPath.Substring(0,e.KeyPath.LastIndexOf('\\')),leaf=e.KeyPath.Substring(e.KeyPath.LastIndexOf('\\')+1);using(var root=RegistryKey.OpenBaseKey(e.Scope.Hive,e.Scope.View))using(var parent=root.OpenSubKey(parentPath,true)){if(parent==null)throw new IOException("无法写入此注册表位置");var final=Read(e.Scope,e.KeyPath);if(!Same(e,final)||!Qualifies(final)){item.Status="已跳过";item.Reason="删除前复核未通过";result.Skipped++;}else{parent.DeleteSubKey(leaf,false);result.RegistryDeleted++;item.Status="已删除";item.Reason="已先保存可恢复备份";}}}
   }catch(Exception ex){item.Status="已跳过";item.Reason=ex.Message;result.Skipped++;}result.Items.Add(item);
  }
 }
 static object Decode(RegistryValueData v){var kind=(RegistryValueKind)v.Kind;switch(kind){case RegistryValueKind.Binary:return Convert.FromBase64String(v.Data);case RegistryValueKind.MultiString:return v.Strings;case RegistryValueKind.DWord:return Int32.Parse(v.Data,CultureInfo.InvariantCulture);case RegistryValueKind.QWord:return Int64.Parse(v.Data,CultureInfo.InvariantCulture);case RegistryValueKind.String:case RegistryValueKind.ExpandString:return v.Data;default:throw new InvalidDataException("不支持的注册表值类型");}}
 public static int Restore(string file){
  var b=new JavaScriptSerializer{MaxJsonLength=16*1024*1024}.Deserialize<RegistryBackup>(File.ReadAllText(file));if(b==null||b.Format!="ClearSpace.RegistryBackup.v2"||b.Computer!=Environment.MachineName||b.UserSid!=WindowsIdentity.GetCurrent().User.Value||b.Entries==null||b.Entries.Count>4096)throw new InvalidDataException("请选择本机、本账户由本软件生成的注册表备份。");
  foreach(var e in b.Entries){if(!InScope(e)||e.Values==null||e.Values.Count>256||Fingerprint(e.Values)!=e.Fingerprint)throw new InvalidDataException("备份范围或内容校验失败。");foreach(var v in e.Values)Decode(v);}
  int restored=0;foreach(var e in b.Entries){string parentPath=e.KeyPath.Substring(0,e.KeyPath.LastIndexOf('\\')),leaf=e.KeyPath.Substring(e.KeyPath.LastIndexOf('\\')+1);using(var root=RegistryKey.OpenBaseKey(e.Scope.Hive,e.Scope.View))using(var parent=root.OpenSubKey(parentPath,true)){if(parent==null)throw new IOException("备份的父位置已不存在或无写入权限。");SafeRegistryHandle handle;int disposition;int error=RegCreateKeyEx(parent.Handle,leaf,0,null,0,0x2001F,IntPtr.Zero,out handle,out disposition);if(error!=0){if(handle!=null)handle.Dispose();throw new IOException("无法恢复注册表（"+error+"）");}using(handle){if(disposition!=1)continue;using(var key=RegistryKey.FromHandle(handle,e.Scope.View)){foreach(var v in e.Values)key.SetValue(v.Name,Decode(v),(RegistryValueKind)v.Kind);restored++;}}}}
  return restored;
 }
}
}
