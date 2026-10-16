using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
namespace ClearSpace {
public sealed class ServiceCompletionPlan {public string User,Computer,Target,Job,Nonce,Version;public int Build,ParentId;public long ParentStarted;}
public static class ServiceUpdateClient {
 static readonly byte[] Entropy=Encoding.UTF8.GetBytes("Clarity.ServiceUpdate.v1");
 public static ServiceUpdateMessage Prepare(SoftwareRelease release,CancellationToken token,Action<long,long> progress){
  UpdateServiceControl.Start();using(var pipe=new NamedPipeClientStream(".",UpdateServiceProtocol.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous,TokenImpersonationLevel.Identification)){
   pipe.Connect(15000);if(UpdateServiceControl.Peer(pipe.SafePipeHandle,true)!=UpdateServiceControl.ProcessId())throw new InvalidDataException("更新服务身份校验失败");
   using(token.Register(()=>pipe.Dispose()))using(var reader=new StreamReader(pipe,Encoding.UTF8,false,4096,true))using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true)){
    var process=Process.GetCurrentProcess();UpdateServiceProtocol.Write(writer,new ServiceUpdateRequest{Version=release.Version,Build=release.Build,ParentId=process.Id,ParentStarted=process.StartTime.ToUniversalTime().Ticks});
    for(;;){token.ThrowIfCancellationRequested();var message=UpdateServiceProtocol.Read<ServiceUpdateMessage>(reader);if(message==null)throw new InvalidDataException("更新服务响应无效");if(message.State=="progress"){if(progress!=null)progress(message.Done,message.Total);continue;}if(message.State=="error")throw new IOException(message.Message);if(message.State!="ready"||String.IsNullOrEmpty(message.Nonce)||message.Nonce.Length!=32)throw new InvalidDataException("更新服务响应无效");UpdateServiceProtocol.JobPath(message.Job);return message;}
   }
  }
 }
 public static async Task Handoff(SoftwareRelease release,ServiceUpdateMessage ready,string session,CancellationToken token){
  var process=Process.GetCurrentProcess();var plan=new ServiceCompletionPlan{User=WindowsIdentity.GetCurrent().User.Value,Computer=Environment.MachineName,Target=AppDomain.CurrentDomain.BaseDirectory,Job=ready.Job,Nonce=ready.Nonce,Version=release.Version,Build=release.Build,ParentId=process.Id,ParentStarted=process.StartTime.ToUniversalTime().Ticks};
  string planPath=Path.Combine(session,"service-plan.dat"),helper=Path.Combine(session,"completion");File.WriteAllBytes(planPath,ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(plan)),Entropy,DataProtectionScope.CurrentUser));Directory.CreateDirectory(helper);string exe=Path.Combine(helper,"Clarity.exe");File.Copy(Assembly.GetExecutingAssembly().Location,exe);File.Copy(Assembly.GetExecutingAssembly().Location+".config",exe+".config");token.ThrowIfCancellationRequested();Process.Start(new ProcessStartInfo(exe,"--complete-service-update \""+planPath+"\""){UseShellExecute=false,WorkingDirectory=helper});
  for(int i=0;i<100;i++){token.ThrowIfCancellationRequested();string marker=Path.Combine(session,"completion-ready");if(File.Exists(marker)&&File.ReadAllText(marker)==ready.Nonce)return;await Task.Delay(100,token);}throw new IOException("更新重启进程未就绪，当前版本继续运行");
 }
 public static int Complete(string path){
  ServiceCompletionPlan plan=null;bool exited=false;
  try{
   string full=Path.GetFullPath(path),session=Path.GetDirectoryName(full);if(Path.GetFileName(full)!="service-plan.dat"||Path.GetDirectoryName(session)!=Path.GetFullPath(UpdateLauncher.UpdatesDirectory)||!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(session),"^[0-9a-f]{32}$")||!Engine.SafeAncestors(full)||new FileInfo(full).Length>65536)throw new InvalidDataException("更新重启任务位置无效");
   plan=new JavaScriptSerializer().Deserialize<ServiceCompletionPlan>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(full),Entropy,DataProtectionScope.CurrentUser)));if(plan.User!=WindowsIdentity.GetCurrent().User.Value||plan.Computer!=Environment.MachineName||!UpdateServiceProtocol.ProtectedInstallation(plan.Target))throw new InvalidDataException("更新重启任务身份无效");
   var job=new JavaScriptSerializer().Deserialize<ServiceUpdateJob>(File.ReadAllText(Path.Combine(UpdateServiceProtocol.JobPath(plan.Job),"job.json")));if(job.Target.TrimEnd('\\')!=plan.Target.TrimEnd('\\')||job.Nonce!=plan.Nonce||job.Version!=plan.Version||job.Build!=plan.Build||job.ParentId!=plan.ParentId||job.ParentStarted!=plan.ParentStarted)throw new InvalidDataException("更新任务不一致");
   var parent=Process.GetProcessById(plan.ParentId);if(parent.StartTime.ToUniversalTime().Ticks!=plan.ParentStarted||!String.Equals(UpdateServiceControl.ImagePath(plan.ParentId),Path.Combine(plan.Target,"Clarity.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("原软件进程已变化");File.WriteAllText(Path.Combine(session,"completion-ready"),plan.Nonce);if(!parent.WaitForExit(60000))return 1;exited=true;
   ServiceUpdateMessage state=null;var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<240000){state=UpdateServiceProtocol.ReadState(plan.Job);if(state!=null&&state.Nonce==plan.Nonce&&(state.State=="complete"||state.State=="error"))break;Thread.Sleep(200);}
   if(state==null||state.Nonce!=plan.Nonce||state.State!="complete"&&state.State!="error"){System.Windows.MessageBox.Show(UiText.T("更新仍在进行，请稍后手动打开 Clarity。"),"Clarity");return 1;}
   if(state.State=="error")System.Windows.MessageBox.Show(UiText.T("软件更新未应用：")+state.Message,"Clarity");
   Process.Start(new ProcessStartInfo(Path.Combine(plan.Target,"Clarity.exe")){UseShellExecute=true,WorkingDirectory=plan.Target});return state.State=="complete"?0:1;
  }catch{if(exited&&plan!=null)try{Process.Start(new ProcessStartInfo(Path.Combine(plan.Target,"Clarity.exe")){UseShellExecute=true,WorkingDirectory=plan.Target});}catch{}return 1;}
 }
}
}
