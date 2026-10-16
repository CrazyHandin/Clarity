using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;
namespace ClearSpace {
public sealed class ServiceUpdateRequest {public string Version;public int Build,ParentId;public long ParentStarted;}
public sealed class ServiceUpdateMessage {public string State,Message,Job,Nonce;public long Done,Total;}
public sealed class ServiceUpdateJob {public string Target,Version,Nonce;public int Build,ParentId;public long ParentStarted;}
public static class UpdateServiceProtocol {
 public const string ServiceName="ClarityUpdate",PipeName="Clarity.Update.v1",ExecutableName="Clarity.UpdateService.exe";
 public static string JobsRoot{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"ClarityUpdateJobs");}}
 public static bool TrustedJobsRoot(){try{if(!Engine.SafeAncestors(Path.Combine(JobsRoot,"probe")))return false;var acl=Directory.GetAccessControl(JobsRoot);string owner=acl.GetOwner(typeof(SecurityIdentifier)).Value;if(owner!="S-1-5-18"&&owner!="S-1-5-32-544"||!acl.AreAccessRulesProtected)return false;foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier)))if(rule.AccessControlType==AccessControlType.Allow&&rule.IdentityReference.Value!="S-1-5-18"&&rule.IdentityReference.Value!="S-1-5-32-544"&&((int)rule.FileSystemRights&0xd0156)!=0)return false;return true;}catch{return false;}}
 public static bool ProtectedInstallation(string directory){
  try{string full=Path.GetFullPath(directory).TrimEnd('\\');string[] roots={Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetEnvironmentVariable("ProgramW6432")};
   string protectedRoot=Array.Find(roots,root=>!String.IsNullOrEmpty(root)&&full.StartsWith(root.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase));if(protectedRoot==null||!Engine.SafeAncestors(Path.Combine(full,"probe")))return false;
   const string installer="S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";Func<FileSystemSecurity,bool> protectedAcl=acl=>{string owner=acl.GetOwner(typeof(SecurityIdentifier)).Value;if(owner!="S-1-5-18"&&owner!="S-1-5-32-544"&&owner!=installer)return false;foreach(FileSystemAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier))){if((rule.PropagationFlags&PropagationFlags.InheritOnly)!=0||rule.AccessControlType!=AccessControlType.Allow)continue;string sid=rule.IdentityReference.Value;if(sid!="S-1-5-18"&&sid!="S-1-5-32-544"&&sid!=installer&&((int)rule.FileSystemRights&0xd0156)!=0)return false;}return true;};
   string current=full;while(!String.IsNullOrEmpty(current)){if(!protectedAcl(Directory.GetAccessControl(current)))return false;if(String.Equals(current,protectedRoot,StringComparison.OrdinalIgnoreCase))break;current=Path.GetDirectoryName(current);}
   return File.Exists(Path.Combine(full,"Clarity.exe"))&&protectedAcl(File.GetAccessControl(Path.Combine(full,"Clarity.exe")))&&File.Exists(Path.Combine(full,ExecutableName))&&protectedAcl(File.GetAccessControl(Path.Combine(full,ExecutableName)));
  }catch{return false;}
 }
 public static string JobPath(string id){if(!System.Text.RegularExpressions.Regex.IsMatch(id??"",@"\A[0-9a-f]{32}\z"))throw new InvalidDataException("更新任务编号无效");if(Directory.Exists(JobsRoot)&&!TrustedJobsRoot())throw new InvalidDataException("更新服务目录权限无效");string path=Path.Combine(JobsRoot,id);if(!UpdatePackage.SafeFutureParents(Path.Combine(path,"probe")))throw new InvalidDataException("更新任务目录包含链接");return path;}
 public static void Write(StreamWriter writer,object value){writer.WriteLine(new JavaScriptSerializer().Serialize(value));writer.Flush();}
 public static T Read<T>(StreamReader reader){var value=new StringBuilder();int ch;while((ch=reader.Read())>=0&&ch!='\n'){if(value.Length>=8192)throw new InvalidDataException("更新服务消息过大");value.Append((char)ch);}if(ch<0&&value.Length==0)throw new EndOfStreamException("更新服务已断开");return new JavaScriptSerializer{MaxJsonLength=8192,RecursionLimit=16}.Deserialize<T>(value.ToString());}
 public static void SaveState(string job,ServiceUpdateMessage state){string path=Path.Combine(JobPath(job),"status.json"),temporary=path+".new";File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(state),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
 public static ServiceUpdateMessage ReadState(string job){string path=Path.Combine(JobPath(job),"status.json");if(!File.Exists(path))return null;using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream))return Read<ServiceUpdateMessage>(reader);}
 public static void ProtectJobsRoot(){
  if(!WindowsIdentity.GetCurrent().IsSystem&&!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException();
  UpdatePackage.CreateDirectory(JobsRoot);var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
  foreach(string sid in new[]{"S-1-5-18","S-1-5-32-544"})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
  acl.SetOwner(new SecurityIdentifier("S-1-5-32-544"));acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-5-4"),FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));Directory.SetAccessControl(JobsRoot,acl);
 }
}
public static class UpdateServiceControl {
 [StructLayout(LayoutKind.Sequential)]struct Status {public uint Type,State,Accepted,Exit,Specific,Checkpoint,Wait,Pid,Flags;}
 [StructLayout(LayoutKind.Sequential)]struct Config {public uint Type,Start,Error;public IntPtr Binary,Group;public uint Tag;public IntPtr Dependencies,Account,Display;}
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr OpenSCManager(string machine,string database,uint access);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr OpenService(IntPtr manager,string name,uint access);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateService(IntPtr manager,string name,string display,uint access,uint type,uint start,uint error,string binary,string group,IntPtr tag,string dependencies,string account,string password);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool ChangeServiceConfig(IntPtr service,uint type,uint start,uint error,string binary,string group,IntPtr tag,string dependencies,string account,string password,string display);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool QueryServiceStatusEx(IntPtr service,int level,out Status status,int size,out int needed);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryServiceConfig(IntPtr service,IntPtr config,int size,out int needed);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool StartService(IntPtr service,int argc,IntPtr argv);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool ControlService(IntPtr service,uint control,out Status status);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool DeleteService(IntPtr service);
 [DllImport("advapi32.dll")]static extern bool CloseServiceHandle(IntPtr handle);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string text,uint revision,out IntPtr descriptor,out uint length);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool SetServiceObjectSecurity(IntPtr service,uint information,IntPtr descriptor);
 [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr memory);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int id);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder text,ref int size);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe,out uint pid);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
 public static int Peer(SafePipeHandle pipe,bool server){uint id;if(!(server?GetNamedPipeServerProcessId(pipe,out id):GetNamedPipeClientProcessId(pipe,out id)))throw new Win32Exception();return checked((int)id);}
 public static string ImagePath(int pid){IntPtr process=OpenProcess(0x1000,false,pid);if(process==IntPtr.Zero)throw new Win32Exception();try{var path=new StringBuilder(32768);int size=path.Capacity;if(!QueryFullProcessImageName(process,0,path,ref size))throw new Win32Exception();return path.ToString();}finally{CloseHandle(process);}}
 static T With<T>(uint access,Func<IntPtr,T> action){IntPtr manager=OpenSCManager(null,null,1);if(manager==IntPtr.Zero)throw new Win32Exception();try{IntPtr service=OpenService(manager,UpdateServiceProtocol.ServiceName,access);if(service==IntPtr.Zero)throw new Win32Exception();try{return action(service);}finally{CloseServiceHandle(service);}}finally{CloseServiceHandle(manager);}}
 public static bool Available(string directory){try{return With(1,service=>{IntPtr memory=Marshal.AllocHGlobal(8192);try{int needed;if(!QueryServiceConfig(service,memory,8192,out needed))return false;var config=(Config)Marshal.PtrToStructure(memory,typeof(Config));return String.Equals(Marshal.PtrToStringUni(config.Binary),"\""+Path.Combine(Path.GetFullPath(directory).TrimEnd('\\'),UpdateServiceProtocol.ExecutableName)+"\"",StringComparison.OrdinalIgnoreCase)&&config.Start==3;}finally{Marshal.FreeHGlobal(memory);}});}catch{return false;}}
 public static int ProcessId(){return With(4,service=>{Status status;int needed;if(!QueryServiceStatusEx(service,0,out status,Marshal.SizeOf(typeof(Status)),out needed))throw new Win32Exception();return checked((int)status.Pid);});}
 public static void Start(){With(0x14,service=>{if(!StartService(service,0,IntPtr.Zero)&&Marshal.GetLastWin32Error()!=1056)throw new Win32Exception();return 0;});}
 public static void Stop(bool remove){try{With(remove?0x10024U:0x24U,service=>{Status status;ControlService(service,1,out status);var watch=Stopwatch.StartNew();int needed;while(watch.ElapsedMilliseconds<20000&&QueryServiceStatusEx(service,0,out status,Marshal.SizeOf(typeof(Status)),out needed)&&status.State!=1)System.Threading.Thread.Sleep(100);if(status.State!=1)throw new IOException("更新服务尚未停止");if(remove&&!DeleteService(service))throw new Win32Exception();return 0;});}catch(Win32Exception ex){if(ex.NativeErrorCode!=1060)throw;}}
 public static void Register(string executable){
  if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException();
  executable=Path.GetFullPath(executable);if(Path.GetFileName(executable)!=UpdateServiceProtocol.ExecutableName||!Engine.SafeAncestors(executable))throw new IOException("更新服务路径无效");string binary="\""+executable+"\"";
  IntPtr manager=OpenSCManager(null,null,3);if(manager==IntPtr.Zero)throw new Win32Exception();try{IntPtr service=OpenService(manager,UpdateServiceProtocol.ServiceName,0xf01ff);if(service==IntPtr.Zero){if(Marshal.GetLastWin32Error()!=1060)throw new Win32Exception();service=CreateService(manager,UpdateServiceProtocol.ServiceName,"Clarity Update",0xf01ff,0x10,3,1,binary,null,IntPtr.Zero,null,"LocalSystem",null);}if(service==IntPtr.Zero)throw new Win32Exception();try{if(!ChangeServiceConfig(service,0x10,3,1,binary,null,IntPtr.Zero,null,"LocalSystem",null,"Clarity Update"))throw new Win32Exception();IntPtr descriptor;uint length;if(!ConvertStringSecurityDescriptorToSecurityDescriptor("D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;CCLCRPLO;;;IU)",1,out descriptor,out length))throw new Win32Exception();try{if(!SetServiceObjectSecurity(service,4,descriptor))throw new Win32Exception();}finally{LocalFree(descriptor);}UpdateServiceProtocol.ProtectJobsRoot();}finally{CloseServiceHandle(service);}}finally{CloseServiceHandle(manager);}
 }
}
}
