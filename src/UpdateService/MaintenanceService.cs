using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace ClearSpace.Maintenance {
public sealed class MaintenanceService:ServiceBase {
 CancellationTokenSource stop;NamedPipeServerStream pipe;
 public MaintenanceService(){ServiceName=UpdateServiceProtocol.ServiceName;CanStop=true;AutoLog=false;}
 protected override void OnStart(string[] args){stop=new CancellationTokenSource();System.Threading.Tasks.Task.Run(()=>Serve());}
 protected override void OnStop(){if(stop!=null)stop.Cancel();if(pipe!=null)try{pipe.Dispose();}catch{}}
 public static PipeSecurity PipePermissions(){var acl=new PipeSecurity();acl.SetAccessRuleProtection(true,false);acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-2"),PipeAccessRights.FullControl,AccessControlType.Deny));foreach(string sid in new[]{"S-1-5-18","S-1-5-32-544"})acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(sid),PipeAccessRights.FullControl,AccessControlType.Allow));acl.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-4"),PipeAccessRights.ReadWrite,AccessControlType.Allow));return acl;}
 void Serve(){
  using(var deadline=new Timer(_=>{try{Stop();}catch{}},null,240000,Timeout.Infinite)){
  try{
   if(!UpdateServiceProtocol.TrustedJobsRoot())throw new UnauthorizedAccessException("更新服务目录权限无效");
   var acl=PipePermissions();
   using(pipe=new NamedPipeServerStream(UpdateServiceProtocol.PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,8192,8192,acl)){
    var connected=pipe.BeginWaitForConnection(null,null);if(WaitHandle.WaitAny(new[]{connected.AsyncWaitHandle,stop.Token.WaitHandle},30000)!=0)return;pipe.EndWaitForConnection(connected);
    using(var reader=new StreamReader(pipe,Encoding.UTF8,false,4096,true))using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true)){
     try{var receive=System.Threading.Tasks.Task.Run(()=>UpdateServiceProtocol.Read<ServiceUpdateRequest>(reader));if(!receive.Wait(10000))throw new IOException("更新请求超时");Prepare(receive.Result,writer);}catch(Exception ex){try{UpdateServiceProtocol.Write(writer,new ServiceUpdateMessage{State="error",Message=ex.GetBaseException().Message});}catch{}}
    }
   }
  }catch{}finally{try{Stop();}catch{}}}
 }
 void Prepare(ServiceUpdateRequest request,StreamWriter writer){
  string target=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);if(!WindowsIdentity.GetCurrent().IsSystem||!UpdateServiceProtocol.ProtectedInstallation(target))throw new UnauthorizedAccessException("更新服务仅支持受保护的 Program Files 安装");
  int peer=UpdateServiceControl.Peer(pipe.SafePipeHandle,false);ValidateRequest(request,peer,target);var parent=Process.GetProcessById(peer);
  // The client supplies only its version and identity, never paths or commands.
  var release=SoftwareUpdates.Check(stop.Token);if(release.Version!=request.Version||release.Build!=request.Build||!release.IsNewer(ReleaseVersion.Display,ReleaseVersion.Build))throw new InvalidDataException("更新版本已变化，请重新检查更新");
  string id=Guid.NewGuid().ToString("N"),directory=UpdateServiceProtocol.JobPath(id);if(Directory.Exists(directory))throw new IOException("更新任务重复");Directory.CreateDirectory(directory);
  var asset=release.Assets.Single(a=>a.Name.EndsWith("-setup.exe",StringComparison.Ordinal));string package=SoftwareUpdates.Download(asset,directory,stop.Token,(done,total)=>UpdateServiceProtocol.Write(writer,new ServiceUpdateMessage{State="progress",Done=done,Total=asset.Size}));
  var job=new ServiceUpdateJob{Target=target,Version=release.Version,Build=release.Build,ParentId=peer,ParentStarted=parent.StartTime.ToUniversalTime().Ticks,Nonce=Guid.NewGuid().ToString("N")};
  File.WriteAllText(Path.Combine(directory,"job.json"),new JavaScriptSerializer().Serialize(job));
  File.Copy(Assembly.GetExecutingAssembly().Location,Path.Combine(directory,"Runner.exe"));File.Copy(Path.Combine(target,"Clarity.exe"),Path.Combine(directory,"Clarity.exe"));File.Copy(Path.Combine(target,"Clarity.exe.config"),Path.Combine(directory,"Runner.exe.config"));
  UpdateServiceProtocol.SaveState(id,new ServiceUpdateMessage{State="prepared",Nonce=job.Nonce});stop.Token.ThrowIfCancellationRequested();
  Process.Start(new ProcessStartInfo(Path.Combine(directory,"Runner.exe"),"--install-update "+id){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory});
  bool waiting=false;for(int i=0;i<100;i++){stop.Token.ThrowIfCancellationRequested();var state=UpdateServiceProtocol.ReadState(id);if(state!=null&&state.State=="waiting"&&state.Nonce==job.Nonce){waiting=true;break;}if(state!=null&&state.State=="error")throw new IOException(state.Message);Thread.Sleep(100);}if(!waiting)throw new IOException("安装执行器未就绪，当前版本继续运行");
  UpdateServiceProtocol.Write(writer,new ServiceUpdateMessage{State="ready",Job=id,Nonce=job.Nonce});
 }
 public static void ValidateRequest(ServiceUpdateRequest request,int peer,string target){if(request==null||request.ParentId!=peer||peer<=0||request.Build<1||request.Build>65535||!String.Equals(UpdateServiceControl.ImagePath(peer),Path.Combine(target,"Clarity.exe"),StringComparison.OrdinalIgnoreCase)||Process.GetProcessById(peer).StartTime.ToUniversalTime().Ticks!=request.ParentStarted)throw new InvalidDataException("更新请求进程身份无效");SoftwareUpdates.VersionNumber(request.Version);}
 public static int InstallUpdate(string id){
  if(!WindowsIdentity.GetCurrent().IsSystem)return 1;
  ServiceUpdateJob job=null;
  try{
   string directory=UpdateServiceProtocol.JobPath(id);if(!String.Equals(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),directory,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新执行器位置无效");
   job=new JavaScriptSerializer().Deserialize<ServiceUpdateJob>(File.ReadAllText(Path.Combine(directory,"job.json")));if(!UpdateServiceProtocol.ProtectedInstallation(job.Target))throw new InvalidDataException("安装位置不再受保护");
   var parent=Process.GetProcessById(job.ParentId);if(parent.StartTime.ToUniversalTime().Ticks!=job.ParentStarted||!String.Equals(UpdateServiceControl.ImagePath(job.ParentId),Path.Combine(job.Target,"Clarity.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("原软件进程已变化");UpdateServiceProtocol.SaveState(id,new ServiceUpdateMessage{State="waiting",Nonce=job.Nonce});if(!parent.WaitForExit(60000))throw new IOException("原软件仍在运行，更新未应用");
   var release=SoftwareUpdates.Check(CancellationToken.None);if(release.Version!=job.Version||release.Build!=job.Build||!release.IsNewer(ReleaseVersion.Display,ReleaseVersion.Build))throw new InvalidDataException("更新版本已变化，请重新检查更新");var asset=release.Assets.Single(a=>a.Name.EndsWith("-setup.exe",StringComparison.Ordinal));string package=Path.Combine(directory,asset.Name);
   using(var locked=UpdateLauncher.OpenPackage(package,asset)){
    UpdateServiceProtocol.SaveState(id,new ServiceUpdateMessage{State="installing",Nonce=job.Nonce});
    using(var installer=Process.Start(new ProcessStartInfo(package,"/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /ALLUSERS /CLARITYSERVICEUPDATE=1 /DIR=\""+job.Target+"\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory})){
     if(!installer.WaitForExit(180000))throw new IOException("安装程序仍在运行，请稍后重新打开 Clarity");if(installer.ExitCode!=0)throw new IOException("更新安装未完成（"+installer.ExitCode+"）");
    }
   }
   if(FileVersionInfo.GetVersionInfo(Path.Combine(job.Target,"Clarity.exe")).ProductVersion!=job.Version+"+build."+job.Build)throw new IOException("安装后版本校验失败");UpdateServiceProtocol.SaveState(id,new ServiceUpdateMessage{State="complete",Nonce=job.Nonce});return 0;
  }catch(Exception ex){try{UpdateServiceProtocol.SaveState(id,new ServiceUpdateMessage{State="error",Nonce=job==null?null:job.Nonce,Message=ex.Message});}catch{}return 1;}
 }
}
public static class Program {
 public static int Main(string[] args){
  try{
   string target=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
   if(args.Length==2&&args[0]=="--register-target"){target=Path.GetFullPath(args[1]);if(!UpdateServiceProtocol.ProtectedInstallation(target))return 3;UpdateServiceControl.Register(Path.Combine(target,UpdateServiceProtocol.ExecutableName));return 0;}
   if(args.Length==1&&args[0]=="--register"){if(!UpdateServiceProtocol.ProtectedInstallation(target))return 3;UpdateServiceControl.Register(Assembly.GetExecutingAssembly().Location);return 0;}
   if(args.Length==1&&(args[0]=="--unregister"||args[0]=="--stop")){if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))return 1;if(args[0]=="--stop"||UpdateServiceControl.Available(target))UpdateServiceControl.Stop(args[0]=="--unregister");return 0;}
   if(args.Length==2&&args[0]=="--install-update")return MaintenanceService.InstallUpdate(args[1]);
   if(args.Length!=0||!WindowsIdentity.GetCurrent().IsSystem)return 1;ServiceBase.Run(new MaintenanceService());return 0;
  }catch{return 1;}
 }
}
}
