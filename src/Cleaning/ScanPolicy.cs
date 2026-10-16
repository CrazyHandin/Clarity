using System;
using System.IO;
namespace ClearSpace {
public static class ScanPolicy {
 // Compare directory boundaries before touching the filesystem; Windows.old is a different directory.
 public static bool Excluded(string path){return Inside(path,Environment.GetFolderPath(Environment.SpecialFolder.Windows))||Inside(path,@"C:\Windows");}
 public static bool Inside(string path,string directory){try{if(String.IsNullOrWhiteSpace(directory))return false;string full=Engine.Normalize(path),root=Engine.Normalize(directory);return full.Equals(root,StringComparison.OrdinalIgnoreCase)||full.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase);}catch{return true;}}
}
}
