using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
namespace ClearSpace {
public static class Hibernation {
 public const string Arguments="/hibernate off";
 public static string FilePath{get{return Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)),"hiberfil.sys");}}
 public static Rule CreateRule(){return new Rule("system-hiberfil.sys","Windows 休眠文件","系统垃圾","通过 Windows powercfg 关闭休眠，由系统释放 hiberfil.sys；不会直接删除系统文件。","清理该项会关闭 Windows 休眠及依赖休眠文件的快速启动、混合睡眠；不经过文件备份。恢复休眠需以管理员身份运行 powercfg /hibernate on。",new string[0],0,false){Software="Windows",Part="休眠文件 · 关闭休眠",GroupOverride="系统垃圾",InfoKind="disable-hibernation",Notice=true,Warning="清理会关闭 Windows 休眠和快速启动，需要管理员权限。",Source="https://learn.microsoft.com/troubleshoot/windows-client/setup-upgrade-and-drivers/disable-and-re-enable-hibernation"};}
 public static Finding Inspect(Rule rule){var finding=new Finding{Rule=rule,Extra="清理会关闭 Windows 休眠及快速启动；需管理员权限，不经过备份。"};try{if(!String.Equals(Path.GetPathRoot(FilePath),RuleBook.TargetDrive,StringComparison.OrdinalIgnoreCase))return finding;var info=new FileInfo(FilePath);if(info.Exists&&(info.Attributes&FileAttributes.ReparsePoint)==0){finding.VirtualItems=1;finding.TotalBytes=finding.EligibleBytes=info.Length;finding.Samples.Add(FilePath);}}catch{finding.Unreadable++;}return finding;}
 public static SystemCleanupResult Disable(CancellationToken token,Action<string> status){
  token.ThrowIfCancellationRequested();if(!App.IsAdmin())throw new UnauthorizedAccessException("关闭 Windows 休眠需要管理员权限。请在设置中以管理员身份重新启动，然后重新扫描。");
  if(!String.Equals(Path.GetPathRoot(FilePath),RuleBook.TargetDrive,StringComparison.OrdinalIgnoreCase))throw new IOException("Windows 休眠文件不在 C 盘。");
  long bytes=File.Exists(FilePath)?new FileInfo(FilePath).Length:0;if(status!=null)status("正在关闭 Windows 休眠…");
  var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"powercfg.exe"),Arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true,RedirectStandardOutput=true};
  using(var process=Process.Start(start)){if(process==null)throw new IOException("无法启动 Windows 电源管理工具。");var error=process.StandardError.ReadToEndAsync();var output=process.StandardOutput.ReadToEndAsync();process.WaitForExit();string message=error.Result+output.Result;if(process.ExitCode!=0)throw new IOException("关闭休眠失败（"+process.ExitCode+"）："+message.Trim());bool removed=!File.Exists(FilePath);return new SystemCleanupResult{Completed=removed,Bytes=removed?bytes:0,Message=removed?"Windows 休眠已关闭；如需恢复，以管理员身份运行 powercfg /hibernate on。":"Windows 仍保留休眠文件，请重新扫描后检查。"};}
 }
}
}
