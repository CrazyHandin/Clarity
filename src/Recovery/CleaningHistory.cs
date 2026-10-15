using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
namespace ClearSpace {
public static class CleaningHistory {
 public static string LastDirectory;
 public static string DirectoryPath{get{return LastDirectory??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Logs");}}
 public static string Save(CleanResult result,string applicationDirectory=null){
  string primary=Path.Combine(applicationDirectory??AppDomain.CurrentDomain.BaseDirectory,"Logs");
  try{return Write(primary,result);}catch(UnauthorizedAccessException){return Write(Path.Combine(CleanerSettings.Root,"Logs"),result);}catch(IOException){return Write(Path.Combine(CleanerSettings.Root,"Logs"),result);}
 }
 static string Write(string directory,CleanResult result){string path=Path.Combine(directory,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")+".json");AtomicFile.Write(path,new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=Int32.MaxValue,RecursionLimit=100}.Serialize(result));LastDirectory=directory;return path;}
 public static void Open(bool folder=false){Directory.CreateDirectory(DirectoryPath);string latest=Directory.GetFiles(DirectoryPath,"*.json").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();if(folder||latest==null){BackupFolders.Open(DirectoryPath);return;}Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"notepad.exe"),"\""+latest+"\""){UseShellExecute=true});}
}
}
