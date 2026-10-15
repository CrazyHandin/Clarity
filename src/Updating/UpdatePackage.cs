using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
namespace ClearSpace {
public static class UpdatePackage {
 static readonly HashSet<string> Allowed=new HashSet<string>(new[]{"Clarity.exe","Clarity.exe.config","使用说明.txt","README.md","README.en.md","CHANGELOG.md","THIRD_PARTY_NOTICES.md","LICENSE.txt","VERIFICATION.txt","SHA256.txt","docs/版本历史.md","docs/清理依据.md","docs/常用软件规则.md","docs/清理规则一览.md","docs/规则库维护.md","docs/Winapp2-使用说明.md","docs/Winapp2-License.md","docs/BUILD.md","docs/TERMS.txt","docs/JSON规则填写说明.txt","rules/Winapp2.ini","rules/ApplicationRules.template.json","rules/BundledRules.signed.json","rules/rule-public-key.xml"},StringComparer.OrdinalIgnoreCase);
 public static string Destination(string root,string name){
  if(String.IsNullOrEmpty(name)||name.Contains("\\")||name.Split('/').Any(p=>p.Length==0||p=="."||p=="..")||!Allowed.Contains(name))throw new InvalidDataException("更新包包含不支持的路径："+name);
  string full=Path.GetFullPath(Path.Combine(root,name.Replace('/',Path.DirectorySeparatorChar))),parent=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  if(!full.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||!Engine.SafeAncestors(full))throw new InvalidDataException("更新路径越界或包含链接");return full;
 }
 public static List<string> Extract(string archivePath,string expectedHash,string destination){
  if(Directory.Exists(destination)||!Engine.SafeAncestors(destination))throw new IOException("更新展开目录无效");Directory.CreateDirectory(destination);var names=new List<string>();long expanded=0;
  using(var stream=new FileStream(archivePath,FileMode.Open,FileAccess.Read,FileShare.Read)){
   using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=expectedHash)throw new InvalidDataException("更新包在应用前发生变化");stream.Position=0;
   using(var zip=new ZipArchive(stream,ZipArchiveMode.Read,false)){
    if(zip.Entries.Count>256)throw new InvalidDataException("更新包文件过多");
    foreach(var entry in zip.Entries){string name=entry.FullName;if(entry.Length>64L*1024*1024||(expanded+=entry.Length)>300L*1024*1024||((entry.ExternalAttributes>>16)&0xf000)==0xa000||(entry.ExternalAttributes&0x400)!=0)throw new InvalidDataException("更新包包含异常文件");string target=Destination(destination,name);if(names.Contains(name,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("更新包路径重复");names.Add(name);Directory.CreateDirectory(Path.GetDirectoryName(target));using(var input=entry.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] buffer=new byte[65536];int count;long written=0;while((count=input.Read(buffer,0,buffer.Length))>0){written+=count;if(written>entry.Length)throw new InvalidDataException("更新解压大小不一致");output.Write(buffer,0,count);}if(written!=entry.Length)throw new InvalidDataException("更新文件不完整");}}
   }
  }
  if(!names.Contains("Clarity.exe")||!names.Contains("Clarity.exe.config"))throw new InvalidDataException("更新包缺少主程序");return names;
 }
 public static void Apply(string target,string payload,IList<string> names,Action<int> beforeReplace=null){
  target=Path.GetFullPath(target);if(!Directory.Exists(target)||!File.Exists(Path.Combine(target,"Clarity.exe"))||!Engine.SafeAncestors(Path.Combine(target,"probe")))throw new IOException("原软件目录无效");
  foreach(string name in names){Destination(target,name);string file=Destination(payload,name);if(!File.Exists(file)||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("待更新文件缺失或包含链接");}
  string rollback=Path.Combine(target,".ClarityUpdate-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(rollback);var completed=new List<string>();var existed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);bool success=false,restored=true;
  try{
   for(int i=0;i<names.Count;i++){
    string name=names[i],destination=Destination(target,name),backup=Destination(rollback,name),source=Destination(payload,name);if(beforeReplace!=null)beforeReplace(i);Directory.CreateDirectory(Path.GetDirectoryName(destination));Directory.CreateDirectory(Path.GetDirectoryName(backup));string temporary=Path.Combine(Path.GetDirectoryName(destination),".clarity-update-"+Guid.NewGuid().ToString("N")+".tmp");
    try{File.Copy(source,temporary,false);if(!Engine.SafeAncestors(destination)||!Engine.SafeAncestors(temporary))throw new IOException("更新目标发生变化");if(File.Exists(destination)){File.Copy(destination,backup,false);existed.Add(name);completed.Add(name);File.Replace(temporary,destination,null,true);}else{File.Move(temporary,destination);completed.Add(name);}}finally{if(File.Exists(temporary)&&Engine.SafeAncestors(temporary))File.Delete(temporary);}
   }
   success=true;
  }catch{
   foreach(string name in completed.AsEnumerable().Reverse())try{string destination=Destination(target,name),backup=Destination(rollback,name);if(existed.Contains(name))File.Copy(backup,destination,true);else if(File.Exists(destination))File.Delete(destination);}catch{restored=false;}
   if(!restored)throw new IOException("更新未完成，部分原文件保留在 "+rollback);throw;
  }finally{
   if(success||restored)try{foreach(string name in names){string file=Destination(rollback,name);if(File.Exists(file))File.Delete(file);}foreach(string directory in Directory.GetDirectories(rollback,"*",SearchOption.AllDirectories).OrderByDescending(p=>p.Length)){if(Engine.SafeAncestors(Path.Combine(directory,"probe"))&&Directory.GetFileSystemEntries(directory).Length==0)Directory.Delete(directory,false);}if(Directory.GetFileSystemEntries(rollback).Length==0)Directory.Delete(rollback,false);}catch(IOException){}catch(UnauthorizedAccessException){}
  }
 }
}
}
