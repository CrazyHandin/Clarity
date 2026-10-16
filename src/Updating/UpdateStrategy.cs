using System;
using System.IO;
using System.Linq;
namespace ClearSpace {
public enum SoftwareUpdateMode {Portable,InstalledFiles,Service,Installer}
public static class UpdateStrategy {
 internal static bool ProgramFilesLocation(string directory){
  try{string path=Path.GetFullPath(directory).TrimEnd('\\');return new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetEnvironmentVariable("ProgramW6432")}.Where(root=>!String.IsNullOrEmpty(root)).Any(root=>String.Equals(path,root.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)||path.StartsWith(root.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase));}catch{return true;}
 }
 internal static bool Writable(string directory){
  try{
   if(!Directory.Exists(directory)||!Engine.SafeAncestors(Path.Combine(directory,"probe")))return false;
   string probe=Path.Combine(directory,".clarity-write-"+Guid.NewGuid().ToString("N"));using(var file=new FileStream(probe,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,1,FileOptions.DeleteOnClose))return true;
  }catch{return false;}
 }
 internal static SoftwareUpdateMode Select(bool installed,bool protectedLocation,bool serviceReady,bool writable){
  if(!installed)return SoftwareUpdateMode.Portable;
  if(protectedLocation&&serviceReady)return SoftwareUpdateMode.Service;
  if(!protectedLocation&&writable)return SoftwareUpdateMode.InstalledFiles;
  return SoftwareUpdateMode.Installer;
 }
 public static SoftwareUpdateMode ForDirectory(string directory){
  bool installed=UpdateLauncher.Installed(directory);if(!installed)return SoftwareUpdateMode.Portable;
  bool protectedLocation=ProgramFilesLocation(directory);bool serviceReady=protectedLocation&&UpdateServiceProtocol.ProtectedInstallation(directory)&&UpdateServiceControl.Available(directory);
  return Select(installed,protectedLocation,serviceReady,!protectedLocation&&Writable(directory));
 }
}
}
