using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;
namespace ClearSpace {
public sealed class UpdatePlan {public string Computer,User,Target,Archive,Hash,Version,Nonce;public int Build,ParentId;public long ParentStarted;public bool InstalledFiles;}
public static class UpdateLauncher {
 static readonly byte[] Entropy=Encoding.UTF8.GetBytes("Clarity.SoftwareUpdate.v1");
 public static string UpdatesDirectory{get{return Path.Combine(CleanerSettings.Root,"SoftwareUpdates");}}
 public static bool Installed(string applicationDirectory){
  foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64})try{using(var root=RegistryKey.OpenBaseKey(hive,view))using(var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{A58446CF-E0B8-4D66-97CA-71AE049410A8}_is1")){if(key==null)continue;string location=key.GetValue("InstallLocation")as string;if(!String.IsNullOrEmpty(location)&&String.Equals(Engine.Normalize(location),Engine.Normalize(applicationDirectory),StringComparison.OrdinalIgnoreCase))return true;}}catch{}return false;
 }
 public static string NewSession(){return NewSessionAt(UpdatesDirectory);}
 internal static string NewSessionAt(string root){UpdatePackage.CreateDirectory(root);string directory=Path.Combine(root,Guid.NewGuid().ToString("N"));if(Directory.Exists(directory))throw new IOException("更新缓存目录无效");UpdatePackage.CreateDirectory(directory);var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);foreach(var sid in new[]{WindowsIdentity.GetCurrent().User,new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null)})acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));Directory.SetAccessControl(directory,acl);return directory;}
 public static FileStream OpenPackage(string path,SoftwareAsset asset){
  if(asset==null||Path.GetFileName(path)!=asset.Name||!Engine.SafeAncestors(path))throw new InvalidDataException("更新包位置无效");var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
  try{Native.INFO info;if(!Native.GetFileInformationByHandle(stream.SafeFileHandle,out info)||(info.Attributes&(Native.UnsafeAttributes|0x10))!=0||info.Links!=1||!Engine.Normalize(Native.FinalPath(stream.SafeFileHandle)).Equals(Engine.Normalize(path),StringComparison.OrdinalIgnoreCase)||stream.Length!=asset.Size)throw new InvalidDataException("更新包位置或大小发生变化");using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=asset.SHA256)throw new InvalidDataException("更新包在应用前发生变化");stream.Position=0;return stream;}catch{stream.Dispose();throw;}
 }
 public static async Task Launch(SoftwareRelease release,SoftwareUpdateMode mode,string package,string session,CancellationToken token){
  string target=AppDomain.CurrentDomain.BaseDirectory;
  if(mode!=SoftwareUpdateMode.Portable&&mode!=SoftwareUpdateMode.InstalledFiles&&mode!=SoftwareUpdateMode.Installer)throw new InvalidOperationException("更新方式无效");
  bool installer=mode==SoftwareUpdateMode.Installer,installedFiles=mode==SoftwareUpdateMode.InstalledFiles;
  var selected=release.Assets.Single(a=>a.Name.EndsWith(installer?"-setup.exe":"-portable.zip",StringComparison.Ordinal));using(var lockedPackage=await Task.Run(()=>OpenPackage(package,selected),token)){
  if(installer){token.ThrowIfCancellationRequested();Process.Start(new ProcessStartInfo(package,"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /CLARITYUPDATE=1 /DIR=\""+target.TrimEnd('\\')+"\""){UseShellExecute=true});return;}
  if(installedFiles&&(!Installed(target)||UpdateStrategy.ProgramFilesLocation(target)))throw new IOException("安装版更新位置无效");
  if(!UpdateStrategy.Writable(target))throw new IOException("软件目录不可写，请以管理员身份更新或将程序移到可写目录。");
  var process=Process.GetCurrentProcess();var plan=new UpdatePlan{Computer=Environment.MachineName,User=WindowsIdentity.GetCurrent().User.Value,Target=Path.GetFullPath(target),Archive=package,Hash=selected.SHA256,Version=release.Version,Build=release.Build,ParentId=process.Id,ParentStarted=process.StartTime.ToUniversalTime().Ticks,Nonce=Guid.NewGuid().ToString("N"),InstalledFiles=installedFiles};string planPath=Path.Combine(session,"plan.dat");byte[] data=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(plan));File.WriteAllBytes(planPath,ProtectedData.Protect(data,Entropy,DataProtectionScope.CurrentUser));string helper=Path.Combine(session,"helper");Directory.CreateDirectory(helper);string executable=Path.Combine(helper,"Clarity.exe");File.Copy(Assembly.GetExecutingAssembly().Location,executable,false);string config=Assembly.GetExecutingAssembly().Location+".config";if(File.Exists(config))File.Copy(config,executable+".config",false);token.ThrowIfCancellationRequested();Process.Start(new ProcessStartInfo(executable,"--apply-update \""+planPath+"\""){UseShellExecute=false,WorkingDirectory=helper});
  for(int i=0;i<300;i++){token.ThrowIfCancellationRequested();string ready=Path.Combine(session,"ready");if(File.Exists(ready)&&File.ReadAllText(ready)==plan.Nonce)return;string error=Path.Combine(session,"error.txt");if(File.Exists(error))throw new IOException(File.ReadAllText(error));await Task.Delay(100,token);}throw new IOException("更新进程未就绪，当前版本继续运行。");}
 }
 public static int Apply(string planPath){
  UpdatePlan plan=null;bool parentExited=false,ownedSession=false;string session=null;
  try{
   string full=Path.GetFullPath(planPath);session=Path.GetDirectoryName(full);if(Path.GetFileName(full)!="plan.dat"||Path.GetDirectoryName(session)!=Path.GetFullPath(UpdatesDirectory)||!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(session),"^[0-9a-f]{32}$")||!Engine.SafeAncestors(full)||new FileInfo(full).Length>65536)throw new InvalidDataException("更新任务位置无效");
   ownedSession=true;plan=new JavaScriptSerializer().Deserialize<UpdatePlan>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(full),Entropy,DataProtectionScope.CurrentUser)));if(plan==null||plan.Computer!=Environment.MachineName||plan.User!=WindowsIdentity.GetCurrent().User.Value||plan.Target.StartsWith("\\\\")||!Engine.SafeAncestors(Path.Combine(plan.Target,"probe")))throw new InvalidDataException("更新任务身份无效");
   var parent=Process.GetProcessById(plan.ParentId);string original=Path.Combine(Path.GetFullPath(plan.Target),"Clarity.exe");if(parent.StartTime.ToUniversalTime().Ticks!=plan.ParentStarted||Engine.Normalize(parent.MainModule.FileName)!=Engine.Normalize(original)||Path.GetDirectoryName(plan.Archive)!=session)throw new InvalidDataException("原软件进程或更新目录已变化");
   if(plan.InstalledFiles&&(!Installed(plan.Target)||UpdateStrategy.ProgramFilesLocation(plan.Target)))throw new InvalidDataException("安装版更新位置无效");
   var release=SoftwareUpdates.Check(CancellationToken.None);var asset=release.Assets.SingleOrDefault(a=>a.Name.EndsWith("-portable.zip",StringComparison.Ordinal));if(asset==null||release.Version!=plan.Version||release.Build!=plan.Build||asset.SHA256!=plan.Hash||Path.GetFileName(plan.Archive)!=asset.Name)throw new InvalidDataException("更新任务与仓库发布内容不一致");
   string payload=Path.Combine(session,"payload");var files=UpdatePackage.Extract(plan.Archive,asset.SHA256,payload);if(plan.InstalledFiles)files=UpdatePackage.ForInstalledApplication(files);var version=FileVersionInfo.GetVersionInfo(Path.Combine(payload,"Clarity.exe"));if(version.ProductVersion!=release.Version+"+build."+release.Build)throw new InvalidDataException("软件包内部版本不一致");File.WriteAllText(Path.Combine(session,"ready"),plan.Nonce);if(!parent.WaitForExit(60000))throw new IOException("原软件仍在运行，更新未应用。");parentExited=true;UpdatePackage.Apply(plan.Target,files);Process.Start(new ProcessStartInfo(original){UseShellExecute=false,WorkingDirectory=plan.Target});return 0;
  }catch(Exception ex){if(ownedSession&&Engine.SafeAncestors(Path.Combine(session,"error.txt")))try{File.WriteAllText(Path.Combine(session,"error.txt"),ex.Message);}catch{}if(parentExited&&plan!=null)try{Process.Start(new ProcessStartInfo(Path.Combine(plan.Target,"Clarity.exe")){UseShellExecute=true,WorkingDirectory=plan.Target});}catch{}return 1;}
 }
}
}
