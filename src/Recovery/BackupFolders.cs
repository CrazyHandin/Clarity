using System;
using System.Diagnostics;
using System.IO;

namespace ClearSpace {
public static class BackupFolders {
 public static string UpgradeDefault(string configured){if(String.IsNullOrWhiteSpace(configured))return "";try{string full=Path.GetFullPath(configured),disk=Path.GetPathRoot(full);if(Engine.Normalize(full).Equals(Engine.Normalize(Path.Combine(disk,"ClearSpaceBackups")),StringComparison.OrdinalIgnoreCase))return Path.Combine(disk,"ClarityBackups");if(Engine.Normalize(full).Equals(Engine.Normalize(Path.Combine(CleanerSettings.Root,"FileBackups")),StringComparison.OrdinalIgnoreCase))return Path.Combine(CleanerSettings.Root,"ClarityBackups");}catch{}return configured;}
 public static string Resolve(string configured,string manifest){if(!String.IsNullOrWhiteSpace(manifest)&&File.Exists(manifest))return Path.GetDirectoryName(Path.GetFullPath(manifest));return Path.GetFullPath(String.IsNullOrWhiteSpace(configured)?FileRecovery.DefaultDirectory():configured);}
 public static void Open(string path){string full=Path.GetFullPath(path);if(full.StartsWith("\\\\")||full.IndexOf('"')>=0)throw new IOException("请选择本地备份目录");string ancestor=full;while(!Directory.Exists(ancestor)){ancestor=Path.GetDirectoryName(ancestor);if(String.IsNullOrEmpty(ancestor))throw new IOException("备份磁盘不可用");}if(!Engine.SafeAncestors(Path.Combine(ancestor,"probe")))throw new IOException("备份路径包含链接");Directory.CreateDirectory(full);if(!Engine.SafeAncestors(Path.Combine(full,"probe")))throw new IOException("备份路径包含链接");Process.Start(new ProcessStartInfo("explorer.exe","\""+full+"\""){UseShellExecute=true});}
}
}
