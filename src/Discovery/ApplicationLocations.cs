using System;
using System.Diagnostics;
using System.IO;

namespace ClearSpace {
public static class ApplicationLocations {
 public static bool Local(string path){return !String.IsNullOrWhiteSpace(path)&&Path.IsPathRooted(path)&&!path.StartsWith("\\\\")&&path.IndexOf('"')<0;}
 public static string Resolve(ApplicationIdentity app){if(app==null)return null;try{if(Local(app.Executable)&&File.Exists(app.Executable))return Path.GetFullPath(app.Executable);if(Local(app.InstallDirectory)&&Directory.Exists(app.InstallDirectory))return Path.GetFullPath(app.InstallDirectory);}catch{}return null;}
 public static void Open(string path){if(!Local(path))throw new IOException("应用位置无效");string full=Path.GetFullPath(path);bool file=File.Exists(full);if(!file&&!Directory.Exists(full))throw new IOException("应用文件或目录已不存在");Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),(file?"/select,":"")+"\""+full+"\""){UseShellExecute=true});}
}
}
