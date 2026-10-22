using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace ClearSpace {
// Only shared while matching one rule pack. These observations never authorize
// cleaning: the engine obtains fresh roots, handles and identities afterwards.
internal sealed class Winapp2Lookup:IDisposable {
 internal const int MaxKeys=4096,MaxPaths=8192;
 readonly Dictionary<string,bool> directories=new Dictionary<string,bool>(StringComparer.Ordinal),files=new Dictionary<string,bool>(StringComparer.Ordinal),safe=new Dictionary<string,bool>(StringComparer.Ordinal),registry=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,string[]> paths=new Dictionary<string,string[]>(StringComparer.Ordinal);
 int retainedPaths;bool disposed;internal int FileSystemReads,RegistryReads,PathReuses;
 bool Remember(Dictionary<string,bool> cache,string key,Func<bool> read){if(disposed)return read();bool value;if(cache.TryGetValue(key,out value))return value;value=read();if(cache.Count<MaxKeys)cache[key]=value;return value;}
 internal bool DirectoryExists(string path){return Remember(directories,path,()=>{FileSystemReads++;return Directory.Exists(path);});}
 internal bool FileExists(string path){return Remember(files,path,()=>{FileSystemReads++;return File.Exists(path);});}
 internal bool Safe(string path){return Remember(safe,path,()=>{FileSystemReads++;return Engine.SafeAncestors(Path.Combine(path,"probe"));});}
 internal bool Registry(string key,Func<bool> read){return Remember(registry,key,()=>{RegistryReads++;return read();});}
 internal string[] Paths(string key,Func<string[]> read,CancellationToken token){
  token.ThrowIfCancellationRequested();if(disposed)return read();string[] value;if(paths.TryGetValue(key,out value)){PathReuses++;return value;}
  value=read();token.ThrowIfCancellationRequested();if(paths.Count<MaxKeys&&retainedPaths+value.Length<=MaxPaths){paths[key]=value;retainedPaths+=value.Length;}return value;
 }
 internal string[] Children(string parent,string mask,bool includeFiles,CancellationToken token){return Paths("children|"+parent+"|"+mask+"|"+includeFiles,()=>{
  FileSystemReads++;var result=new List<string>();try{foreach(var entry in new DirectoryInfo(parent).EnumerateFileSystemInfos(mask).Take(256)){token.ThrowIfCancellationRequested();if((entry.Attributes&FileAttributes.ReparsePoint)==0&&((entry.Attributes&FileAttributes.Directory)!=0||includeFiles))result.Add(entry.FullName);}}catch(OperationCanceledException){throw;}catch(IOException){}catch(UnauthorizedAccessException){}return result.ToArray();
 },token);}
 public void Dispose(){disposed=true;directories.Clear();files.Clear();safe.Clear();registry.Clear();paths.Clear();retainedPaths=0;}
}
}
