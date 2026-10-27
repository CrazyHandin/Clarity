using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace ClearSpace {
public sealed class ScanStatistics {
 public long ElapsedMilliseconds;public int DirectoryReads,DirectoryReuses,HeaderReads,HeaderCacheHits,FingerprintReads,FingerprintCacheHits,SnapshotReuses,IndexDirectories,IndexFallbacks,IndividualCaptures;public int AppDataDirectories,AppDataCacheRules;public bool AppDataLimited;
 public int DiscoveryIndexReads,DiscoveryFallbackReads;
 public int CacheHits{get{return HeaderCacheHits+FingerprintCacheHits;}}
}
// A scan owns its transient reads. Persistent entries are hints, never deletion snapshots or rules.
public sealed class ScanSession:IDisposable {
 public const int MaxEntries=4096,MaxBytes=4*1024*1024,MaxDirectoryEntries=20000;
 public static string CachePath{get{return Path.Combine(CleanerSettings.Root,"ScanCache","metadata.dat");}}
 public readonly ScanStatistics Statistics=new ScanStatistics();
 readonly string path;readonly bool persistent;readonly Stopwatch clock=Stopwatch.StartNew();
 readonly Dictionary<string,CacheEntry> records=new Dictionary<string,CacheEntry>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,ScanEntry[]> directories=new Dictionary<string,ScanEntry[]>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Snapshot> snapshots=new Dictionary<string,Snapshot>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,bool> volumes=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
 int directoryEntries;bool changed,disposed;
 static readonly byte[] entropy=Encoding.UTF8.GetBytes("Clarity.ScanCache.1");
 public sealed class CacheFile{public int Schema=1;public string Version=ReleaseVersion.FullVersion;public List<CacheEntry> Entries;}
 public sealed class CacheEntry{public string Key,Stamp;public long Saved;public bool Personal;public ApplicationIdentity Identity;}
 public ScanSession(bool persistent=false,string cachePath=null){this.persistent=persistent;path=cachePath??CachePath;if(persistent)Load();}
 static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=MaxBytes,RecursionLimit=20};}
 void Load(){try{
  if(!File.Exists(path)||!Engine.SafeAncestors(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0||new FileInfo(path).Length>MaxBytes*2L)return;
  byte[] bytes=ProtectedData.Unprotect(Convert.FromBase64String(File.ReadAllText(path)),entropy,DataProtectionScope.CurrentUser);if(bytes.Length>MaxBytes)return;
  var cache=Serializer().Deserialize<CacheFile>(Encoding.UTF8.GetString(bytes));if(cache==null||cache.Schema!=1||cache.Version!=ReleaseVersion.FullVersion||cache.Entries==null||cache.Entries.Count>MaxEntries)return;
  long cutoff=DateTime.UtcNow.AddDays(-7).Ticks,now=DateTime.UtcNow.Ticks;
  foreach(var entry in cache.Entries)if(entry!=null&&entry.Key!=null&&entry.Key.Length<=32770&&entry.Stamp!=null&&entry.Stamp.Length<=256&&entry.Saved>=cutoff&&entry.Saved<=now)records[entry.Key]=entry;
 }catch{records.Clear();}}
 CacheEntry Read(string key,string stamp){CacheEntry entry;return persistent&&stamp!=null&&records.TryGetValue(key,out entry)&&entry.Stamp==stamp?entry:null;}
 void Put(CacheEntry entry){if(!persistent||entry.Stamp==null||records.Count>=MaxEntries&&!records.ContainsKey(entry.Key))return;entry.Saved=DateTime.UtcNow.Ticks;records[entry.Key]=entry;changed=true;}
 static string FileStamp(FileInfo info){try{if(!info.Exists||(info.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0)return null;return info.Length+":"+info.LastWriteTimeUtc.Ticks+":"+info.CreationTimeUtc.Ticks;}catch{return null;}}
 static string ExecutableStamp(string file){try{
  if(!Engine.SafeAncestors(file))return null;
  using(var handle=Native.CreateFile(file,0x80,7,IntPtr.Zero,3,0x200000,IntPtr.Zero)){
   Native.INFO info;if(handle.IsInvalid||!Native.GetFileInformationByHandle(handle,out info)||(info.Attributes&(Native.UnsafeAttributes|0x10))!=0||!String.Equals(Engine.Normalize(file),Engine.Normalize(Native.FinalPath(handle)),StringComparison.OrdinalIgnoreCase))return null;
   return info.Volume+":"+info.IndexHigh+":"+info.IndexLow+":"+info.Bytes+":"+info.Write.Value+":"+info.Creation.Value;
  }
 }catch{return null;}}
 static ApplicationIdentity Copy(ApplicationIdentity a){return new ApplicationIdentity{Name=a.Name,Product=a.Product,Company=a.Company,Version=a.Version,Signature=a.Signature,Signer=a.Signer};}
 public ApplicationIdentity Fingerprint(string file,Func<ApplicationIdentity> read){
  string stamp=persistent?ExecutableStamp(file):null,key="app:"+file;var cached=Read(key,stamp);
  if(cached!=null&&cached.Identity!=null){Statistics.FingerprintCacheHits++;var copy=Copy(cached.Identity);copy.FingerprintCached=true;return copy;}
  Statistics.FingerprintReads++;var value=read();if(value!=null&&stamp!=null&&stamp==ExecutableStamp(file))Put(new CacheEntry{Key=key,Stamp=stamp,Identity=Copy(value)});return value;
 }
 public bool Personal(FileInfo info){return Personal(ScanEntry.From(info));}
 public bool Personal(ScanEntry info){
  string stamp=persistent?info.Bytes+":"+DateTime.FromFileTimeUtc(info.Modified).Ticks+":"+DateTime.FromFileTimeUtc(info.Created).Ticks:null,key="head:"+info.FullName;var cached=Read(key,stamp);if(cached!=null){Statistics.HeaderCacheHits++;return cached.Personal;}
  Statistics.HeaderReads++;bool value=HeuristicScanner.LooksPersonal(info.FullName);var refreshed=new FileInfo(info.FullName);if(stamp!=null&&stamp==FileStamp(refreshed))Put(new CacheEntry{Key=key,Stamp=stamp,Personal=value});return value;
 }
 public void BeginFiles(){directories.Clear();snapshots.Clear();volumes.Clear();directoryEntries=0;}
 bool Ntfs(string directory){try{string root=Path.GetPathRoot(directory);bool value;if(!volumes.TryGetValue(root,out value)){value=String.Equals(new DriveInfo(root).DriveFormat,"NTFS",StringComparison.OrdinalIgnoreCase);volumes[root]=value;}return value;}catch{return false;}}
 public IEnumerable<ScanEntry> Entries(string directory,CancellationToken token){
  ScanEntry[] cached;if(directories.TryGetValue(directory,out cached)){Statistics.DirectoryReuses++;return cached;}
  Statistics.DirectoryReads++;return ReadEntries(directory,token);
 }
 IEnumerable<ScanEntry> RawEntries(string directory,CancellationToken token){
  using(var index=Ntfs(directory)?DirectoryIndex.Open(directory):null){
   if(index!=null){Statistics.IndexDirectories++;foreach(var entry in index.Read(token))yield return entry;yield break;}
  }
  Statistics.IndexFallbacks++;foreach(var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos()){token.ThrowIfCancellationRequested();yield return ScanEntry.From(entry);}
 }
 IEnumerable<ScanEntry> ReadEntries(string directory,CancellationToken token){
  var retained=new List<ScanEntry>();bool bounded=true;
  foreach(var entry in RawEntries(directory,token)){
   token.ThrowIfCancellationRequested();if(bounded){if(directoryEntries+retained.Count<MaxDirectoryEntries)retained.Add(entry);else{bounded=false;retained.Clear();}}yield return entry;
  }
  if(bounded){directoryEntries+=retained.Count;directories[directory]=retained.ToArray();}
 }
 public Snapshot SnapshotFor(string file,ScanEntry current){Snapshot item;return snapshots.TryGetValue(file,out item)&&item.Bytes==current.Bytes&&item.Modified==current.Modified?Clone(item):null;}
 public void Remember(Snapshot item){if(snapshots.Count<MaxDirectoryEntries)snapshots[item.Path]=Clone(item);}
 static Snapshot Clone(Snapshot s){return new Snapshot{Path=s.Path,Bytes=s.Bytes,Modified=s.Modified,Volume=s.Volume,IndexHigh=s.IndexHigh,IndexLow=s.IndexLow};}
 public void Complete(CancellationToken token){
  token.ThrowIfCancellationRequested();Statistics.ElapsedMilliseconds=clock.ElapsedMilliseconds;if(!persistent||!changed)return;
  try{string json=Serializer().Serialize(new CacheFile{Entries=records.Values.OrderByDescending(e=>e.Saved).Take(MaxEntries).ToList()});byte[] bytes=Encoding.UTF8.GetBytes(json);if(bytes.Length>MaxBytes)return;token.ThrowIfCancellationRequested();AtomicFile.Write(path,Convert.ToBase64String(ProtectedData.Protect(bytes,entropy,DataProtectionScope.CurrentUser)));}catch(OperationCanceledException){throw;}catch{/* A cache failure must not fail a scan. */}
 }
 public static void Clear(string cachePath=null){string file=cachePath??CachePath;if(!File.Exists(file))return;if(!Engine.SafeAncestors(file)||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("扫描缓存路径包含链接，无法清除。");File.Delete(file);}
 public void Dispose(){if(disposed)return;disposed=true;Statistics.ElapsedMilliseconds=clock.ElapsedMilliseconds;BeginFiles();records.Clear();clock.Stop();}
}
}
