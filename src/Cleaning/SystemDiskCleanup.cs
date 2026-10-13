using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ClearSpace {
public sealed class SystemCleanupResult {public bool Completed;public string Message;}
public static class SystemDiskCleanup {
 public const string Arguments="/d C /verylowdisk";
 public static string Executable{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cleanmgr.exe");}}
 public static bool Available{get{return File.Exists(Executable)&&Directory.Exists("C:\\");}}
 public static Rule CreateRule(){return new Rule("windows-disk-cleanup-auto","Windows 磁盘清理（C 盘自动）","系统垃圾","调用 Windows 自带 cleanmgr，限定 C 盘，按系统默认项目自动清理；完成后系统工具自动退出。容量由 Windows 计算，不计入上方文件容量。","可能永久清空 C 盘回收站及系统默认选中的临时资料；系统删除不经过澄净备份，无法在本工具中恢复。",new string[0],0,false){Software="Windows",Part="磁盘清理 · 自动运行 C 盘",InfoKind="system-disk-cleanup",Notice=true,Source="https://learn.microsoft.com/windows-server/administration/windows-commands/cleanmgr",Detection="cleanmgr /d C /verylowdisk；不使用会遍历全部磁盘的 /sagerun。"};}
 public static SystemCleanupResult Run(CancellationToken token,Action<string> status){
  token.ThrowIfCancellationRequested();if(!Available)throw new FileNotFoundException("当前系统未安装 Windows 磁盘清理工具。");
  if(status!=null)status("Windows 正在清理 C 盘，完成后自动关闭");
  var start=new ProcessStartInfo(Executable,Arguments){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(Executable)};
  using(var process=Process.Start(start)){
   if(process==null)throw new IOException("无法启动 Windows 磁盘清理。");DateTime next=DateTime.UtcNow;
   // Let Windows finish its own cleanup. Never kill it in the middle of a system operation.
   while(!process.WaitForExit(300)){if(DateTime.UtcNow>=next){if(status!=null)status(token.IsCancellationRequested?"已请求停止，等待 Windows 完成当前系统清理":"Windows 正在清理 C 盘，完成后自动关闭");next=DateTime.UtcNow.AddSeconds(3);}}
   return new SystemCleanupResult{Completed=process.ExitCode==0,Message=process.ExitCode==0?"Windows 磁盘清理已结束，系统工具已退出；未将系统释放量计入文件估算。":"Windows 磁盘清理退出码 "+process.ExitCode+"，部分系统项目可能需要管理员权限。"};
  }
 }
}
}
