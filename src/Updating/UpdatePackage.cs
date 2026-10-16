using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
namespace ClearSpace {
internal sealed class UpdateFile {internal string Name,Hash;internal long Bytes;}
public sealed class VerifiedUpdatePayload {
 internal readonly string DirectoryPath;internal readonly List<UpdateFile> Files;
 internal VerifiedUpdatePayload(string directory,List<UpdateFile> files){DirectoryPath=Path.GetFullPath(directory);Files=files;}
 public int Count{get{return Files.Count;}}
}
public static class UpdatePackage {
 static readonly HashSet<string> Allowed=new HashSet<string>(new[]{"Clarity.exe","Clarity.exe.config","使用说明.txt","README.md","README.en.md","CHANGELOG.md","THIRD_PARTY_NOTICES.md","LICENSE.txt","VERIFICATION.txt","SHA256.txt","docs/版本历史.md","docs/清理依据.md","docs/常用软件规则.md","docs/清理规则一览.md","docs/规则库维护.md","docs/Winapp2-使用说明.md","docs/Winapp2-License.md","docs/BUILD.md","docs/TERMS.txt","docs/JSON规则填写说明.txt","rules/Winapp2.ini","rules/ApplicationRules.template.json","rules/BundledRules.signed.json","rules/rule-public-key.xml"},StringComparer.OrdinalIgnoreCase);
 static UpdatePackage(){Allowed.Add(UpdateServiceProtocol.ExecutableName);Allowed.Add(UpdateServiceProtocol.ExecutableName+".config");}
 static readonly string[] InstalledFiles={"Clarity.exe","Clarity.exe.config","Clarity.UpdateService.exe","Clarity.UpdateService.exe.config","使用说明.txt","rules/Winapp2.ini"};
 public static VerifiedUpdatePayload ForInstalledApplication(VerifiedUpdatePayload payload){
  if(payload==null||InstalledFiles.Any(name=>!payload.Files.Any(file=>file.Name.Equals(name,StringComparison.OrdinalIgnoreCase))))throw new InvalidDataException("安装版更新缺少必要的程序文件");
  return new VerifiedUpdatePayload(payload.DirectoryPath,payload.Files.Where(file=>InstalledFiles.Contains(file.Name,StringComparer.OrdinalIgnoreCase)).ToList());
 }
 public static bool SafeFutureParents(string path){
  try{string current=Path.GetDirectoryName(Path.GetFullPath(path));if(String.IsNullOrEmpty(current)||current.StartsWith("\\\\",StringComparison.Ordinal))return false;
   while(!Directory.Exists(current)){try{File.GetAttributes(current);return false;}catch(FileNotFoundException){}catch(DirectoryNotFoundException){}current=Path.GetDirectoryName(current);if(String.IsNullOrEmpty(current))return false;}
   return Engine.SafeAncestors(Path.Combine(current,"probe"));
  }catch{return false;}
 }
 public static void CreateDirectory(string directory){if(!SafeFutureParents(Path.Combine(directory,"probe")))throw new IOException("更新目录包含链接或无法验证");Directory.CreateDirectory(directory);if(!Engine.SafeAncestors(Path.Combine(directory,"probe")))throw new IOException("更新目录发生变化");}
 static void VerifyFile(FileStream stream,string path){Native.INFO info;if(!Native.GetFileInformationByHandle(stream.SafeFileHandle,out info)||(info.Attributes&(Native.UnsafeAttributes|0x10))!=0||info.Links!=1||!Engine.Normalize(Native.FinalPath(stream.SafeFileHandle)).Equals(Engine.Normalize(path),StringComparison.OrdinalIgnoreCase))throw new IOException("更新文件包含链接或位置发生变化");}
 static string Hash(Stream stream){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
 public static string Destination(string root,string name){
  if(String.IsNullOrEmpty(name)||name.Contains("\\")||name.Split('/').Any(p=>p.Length==0||p=="."||p=="..")||!Allowed.Contains(name))throw new InvalidDataException("更新包包含不支持的路径："+name);
  string full=Path.GetFullPath(Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar))),parent=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  if(!full.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!SafeFutureParents(full))throw new InvalidDataException("更新路径越界或包含链接");return full;
 }
 public static VerifiedUpdatePayload Extract(string archivePath,string expectedHash,string destination){
  if(Directory.Exists(destination)||File.Exists(destination))throw new IOException("更新展开目录无效");CreateDirectory(destination);var files=new List<UpdateFile>();long expanded=0;
  using(var stream=new FileStream(archivePath,FileMode.Open,FileAccess.Read,FileShare.Read)){
   VerifyFile(stream,archivePath);if(Hash(stream)!=expectedHash)throw new InvalidDataException("更新包在应用前发生变化");stream.Position=0;
   using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,false)){
    if(zip.Entries.Count>256)throw new InvalidDataException("更新包文件过多");
    foreach(var entry in zip.Entries){string name=entry.FullName;if(entry.Length>64L*1024*1024||(expanded+=entry.Length)>300L*1024*1024||((entry.ExternalAttributes>>16)&0xf000)==0xa000||(entry.ExternalAttributes&0x400)!=0)throw new InvalidDataException("更新包包含异常文件");string target=Destination(destination,name);if(files.Any(f=>f.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("更新包路径重复");CreateDirectory(Path.GetDirectoryName(target));using(var input=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){VerifyFile(output,target);byte[] buffer=new byte[65536];int count;long written=0;while((count=input.Read(buffer,0,buffer.Length))>0){written+=count;if(written>entry.Length)throw new InvalidDataException("更新解压大小不一致");output.Write(buffer,0,count);}if(written!=entry.Length)throw new InvalidDataException("更新文件不完整");output.Flush(true);output.Position=0;files.Add(new UpdateFile{Name=name,Hash=Hash(output),Bytes=written});}}
   }
  }
  if(!files.Any(f=>f.Name=="Clarity.exe")||!files.Any(f=>f.Name=="Clarity.exe.config")||!files.Any(f=>f.Name=="rules/Winapp2.ini"))throw new InvalidDataException("更新包缺少主程序或规则文件");return new VerifiedUpdatePayload(destination,files);
 }
 public static void Apply(string target,VerifiedUpdatePayload payload,Action<int> beforeReplace=null){
  if(payload==null)throw new InvalidDataException("缺少已校验的更新内容");var names=payload.Files.Select(f=>f.Name).ToList();
  target=Path.GetFullPath(target);if(!Directory.Exists(target)||!File.Exists(Path.Combine(target,"Clarity.exe"))||!Engine.SafeAncestors(Path.Combine(target,"probe")))throw new IOException("原软件目录无效");
  foreach(string name in names){Destination(target,name);string file=Destination(payload.DirectoryPath,name);if(!File.Exists(file)||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("待更新文件缺失或包含链接");}
  string rollback=Path.Combine(target,".ClarityUpdate-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(rollback);var completed=new List<string>();var existed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);bool success=false,restored=true;
  try{
   for(int i=0;i<names.Count;i++){
    string name=names[i],destination=Destination(target,name),backup=Destination(rollback,name),source=Destination(payload.DirectoryPath,name);if(beforeReplace!=null)beforeReplace(i);CreateDirectory(Path.GetDirectoryName(destination));CreateDirectory(Path.GetDirectoryName(backup));string temporary=Path.Combine(Path.GetDirectoryName(destination),".clarity-update-"+Guid.NewGuid().ToString("N")+".tmp");
    try{using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read)){VerifyFile(input,source);var expected=payload.Files[i];if(input.Length!=expected.Bytes||Hash(input)!=expected.Hash)throw new InvalidDataException("解压后的更新文件发生变化");input.Position=0;using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){VerifyFile(output,temporary);input.CopyTo(output);output.Flush(true);}}if(!Engine.SafeAncestors(destination)||!Engine.SafeAncestors(temporary))throw new IOException("更新目标发生变化");if(File.Exists(destination)){File.Copy(destination,backup,false);existed.Add(name);completed.Add(name);File.Replace(temporary,destination,null,true);}else{File.Move(temporary,destination);completed.Add(name);}}finally{if(File.Exists(temporary)&&Engine.SafeAncestors(temporary))File.Delete(temporary);}
   }
   success=true;
  }catch{
   foreach(string name in completed.AsEnumerable().Reverse())try{string destination=Destination(target,name),backup=Destination(rollback,name);if(existed.Contains(name))File.Copy(backup,destination,true);else if(File.Exists(destination))File.Delete(destination);}catch{restored=false;}
   if(!restored)throw new IOException("更新未完成，部分原文件保留在 "+rollback);throw;
  }finally{
   if(success||restored)try{foreach(string name in names){string file=Destination(rollback,name);if(File.Exists(file))File.Delete(file);}foreach(string directory in names.Select(name=>Path.GetDirectoryName(Destination(rollback,name))).Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(p=>p.Length)){if(Directory.Exists(directory)&&Engine.SafeAncestors(Path.Combine(directory,"probe"))&&Directory.GetFileSystemEntries(directory).Length==0)Directory.Delete(directory,false);}if(Directory.Exists(rollback)&&Directory.GetFileSystemEntries(rollback).Length==0)Directory.Delete(rollback,false);}catch(IOException){}catch(UnauthorizedAccessException){}
  }
 }
}
}
