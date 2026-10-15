using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;
namespace ClearSpace {
public sealed class RecoveryEntry {public string Format="ClearSpace.FileRecovery.v1",Computer,UserSid,Original,Stored,Hash,ApprovedRoot;public long Bytes,Modified;public bool Restored,Removed;}
public sealed class RestoreResult {public int Restored,Cleared,Skipped,Failed,Pending;public List<string> Errors=new List<string>();}
public sealed class RecoverySession {
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool DuplicateHandle(IntPtr sourceProcess,SafeFileHandle source,IntPtr targetProcess,out SafeFileHandle target,uint access,bool inherit,uint options);
 public string DirectoryPath,Manifest;public int Files;
 bool faulted;
 public RecoverySession(string root){if(String.IsNullOrEmpty(root))throw new IOException("请设置文件备份目录");DirectoryPath=Path.Combine(Path.GetFullPath(root),DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(DirectoryPath);if(!Engine.SafeAncestors(Path.Combine(DirectoryPath,"probe")))throw new IOException("备份路径包含链接");var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);foreach(var sid in new[]{WindowsIdentity.GetCurrent().User,new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null)})acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));Directory.SetAccessControl(DirectoryPath,acl);Manifest=Path.Combine(DirectoryPath,"files.csbackup");}
 public void Save(SafeFileHandle handle,Rule rule,Snapshot snapshot,CancellationToken token){
  if(faulted)throw new IOException("本批备份索引写入失败，保留后续文件");if(rule.Roots.Any(r=>DirectoryPath.StartsWith(Engine.Normalize(r)+"\\",StringComparison.OrdinalIgnoreCase)))throw new IOException("备份目录不能位于清理目录内");string stored=Guid.NewGuid().ToString("N")+".bin",file=Path.Combine(DirectoryPath,stored);string hash;
  try{SafeFileHandle copy;if(!DuplicateHandle(new IntPtr(-1),handle,new IntPtr(-1),out copy,0,false,2))throw new IOException("无法安全读取备份内容");
   using(copy)using(var source=new FileStream(copy,FileAccess.Read,65536,false))using(var target=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var sha=SHA256.Create()){byte[] buffer=new byte[65536];int count;long bytes=0;while((count=source.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();target.Write(buffer,0,count);sha.TransformBlock(buffer,0,count,null,0);bytes+=count;}sha.TransformFinalBlock(new byte[0],0,0);target.Flush(true);if(bytes!=snapshot.Bytes)throw new IOException("备份大小变化");hash=BitConverter.ToString(sha.Hash).Replace("-","");}
   token.ThrowIfCancellationRequested();var entry=new RecoveryEntry{Computer=Environment.MachineName,UserSid=WindowsIdentity.GetCurrent().User.Value,Original=snapshot.Path,Stored=stored,Hash=hash,Bytes=snapshot.Bytes,Modified=snapshot.Modified,ApprovedRoot=rule.Roots.First(r=>rule.Single?Engine.Normalize(r)==Engine.Normalize(snapshot.Path):snapshot.Path.StartsWith(Engine.Normalize(r)+"\\",StringComparison.OrdinalIgnoreCase))};string json=new JavaScriptSerializer().Serialize(entry);byte[] sealedData=ProtectedData.Protect(Encoding.UTF8.GetBytes(json),null,DataProtectionScope.CurrentUser);using(var stream=new FileStream(Manifest,FileMode.Append,FileAccess.Write,FileShare.Read)){long previousLength=stream.Length;try{byte[] data=Encoding.UTF8.GetBytes(Convert.ToBase64String(sealedData)+"\n");if(stream.Length+data.Length>64L*1024*1024)throw new IOException("本批备份索引已达上限，请保留后续文件");stream.Write(data,0,data.Length);stream.Flush(true);}catch{faulted=true;try{stream.SetLength(previousLength);stream.Flush(true);}catch{}throw;}}Files++;
  }catch{try{if(File.Exists(file)&&Engine.SafeAncestors(file)&&(File.GetAttributes(file)&FileAttributes.ReparsePoint)==0)File.Delete(file);}catch{}throw;}
 }
}
public static class FileRecovery {
 public static string DefaultDirectory(){try{var disk=DriveInfo.GetDrives().FirstOrDefault(d=>d.IsReady&&d.DriveType==DriveType.Fixed&&!d.Name.Equals(RuleBook.TargetDrive,StringComparison.OrdinalIgnoreCase)&&d.AvailableFreeSpace>512L*1024*1024);if(disk!=null)return Path.Combine(disk.Name,"ClarityBackups");}catch{}return Path.Combine(CleanerSettings.Root,"ClarityBackups");}
 public static List<RecoveryEntry> Read(string manifest){if(!Engine.SafeAncestors(manifest)||(File.GetAttributes(manifest)&FileAttributes.ReparsePoint)!=0||new FileInfo(manifest).Length>64L*1024*1024)throw new InvalidDataException("备份索引无效");string raw=File.ReadAllText(manifest);string[] lines=raw.Split('\n');var entries=new List<RecoveryEntry>();for(int i=0;i<lines.Length;i++){string line=lines[i].Trim();if(line.Length==0)continue;byte[] data;try{data=ProtectedData.Unprotect(Convert.FromBase64String(line),null,DataProtectionScope.CurrentUser);}catch{if(i==lines.Length-1&&!raw.EndsWith("\n")&&entries.Count>0)break;throw;}var e=new JavaScriptSerializer().Deserialize<RecoveryEntry>(Encoding.UTF8.GetString(data));if(e==null||e.Format!="ClearSpace.FileRecovery.v1"||e.Computer!=Environment.MachineName||e.UserSid!=WindowsIdentity.GetCurrent().User.Value||e.Stored==null||!System.Text.RegularExpressions.Regex.IsMatch(e.Stored,"^[0-9a-f]{32}\\.bin$")||e.Bytes<0||String.IsNullOrEmpty(e.Hash)||!Path.IsPathRooted(e.Original)||e.Original.StartsWith("\\\\"))throw new InvalidDataException("备份只能用于本机、本账户");string full=Engine.Normalize(e.Original),root=Engine.Normalize(e.ApprovedRoot);if(full!=root&&!full.StartsWith(root+"\\",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("备份范围校验失败");var previous=entries.FindIndex(p=>p.Stored==e.Stored);if(previous>=0){if(entries[previous].Original!=e.Original||entries[previous].Hash!=e.Hash||entries[previous].Bytes!=e.Bytes)throw new InvalidDataException("备份恢复记录不一致");entries[previous]=e;}else entries.Add(e);if(entries.Count>100000)throw new InvalidDataException("索引过大");}return entries.Where(e=>!e.Removed).ToList();}
 static void AppendState(string manifest,RecoveryEntry entry){byte[] sealedData=ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(entry)),null,DataProtectionScope.CurrentUser);using(var stream=new FileStream(manifest,FileMode.Append,FileAccess.Write,FileShare.Read)){long previous=stream.Length;try{byte[] bytes=Encoding.UTF8.GetBytes(Convert.ToBase64String(sealedData)+"\n");if(previous+bytes.Length>64L*1024*1024)throw new IOException("备份索引已达上限，保留备份");stream.Write(bytes,0,bytes.Length);stream.Flush(true);}catch{try{stream.SetLength(previous);stream.Flush(true);}catch{}throw;}}}
 static void PrepareJournal(string manifest){string raw=File.ReadAllText(manifest);if(raw.Length==0||raw.EndsWith("\n"))return;int start=raw.LastIndexOf('\n')+1;try{ProtectedData.Unprotect(Convert.FromBase64String(raw.Substring(start).Trim()),null,DataProtectionScope.CurrentUser);using(var stream=new FileStream(manifest,FileMode.Append,FileAccess.Write,FileShare.Read)){stream.WriteByte(10);stream.Flush(true);}}catch(System.Security.Cryptography.CryptographicException){TruncateJournal(manifest,raw.Substring(0,start));}catch(FormatException){TruncateJournal(manifest,raw.Substring(0,start));}}
 static void TruncateJournal(string manifest,string prefix){using(var stream=new FileStream(manifest,FileMode.Open,FileAccess.ReadWrite,FileShare.Read)){byte[] head=new byte[3];int n=stream.Read(head,0,3);bool bom=n==3&&head[0]==0xef&&head[1]==0xbb&&head[2]==0xbf;stream.SetLength(Encoding.UTF8.GetByteCount(prefix)+(bom?3:0));stream.Flush(true);}}
 static bool OriginalMatches(RecoveryEntry e){try{if(!Engine.SafeAncestors(e.Original)||(File.GetAttributes(e.Original)&FileAttributes.ReparsePoint)!=0)return false;using(var stream=new FileStream(e.Original,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())return stream.Length==e.Bytes&&BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","")==e.Hash;}catch{return false;}}
 public static int Restore(string manifest){var result=RestoreDetailed(manifest,true);if(result.Failed>0)throw new IOException("已恢复 "+result.Restored+" 个文件，保留未完成备份："+String.Join("；",result.Errors.Take(3)));return result.Restored;}
 public static int Restore(string manifest,bool clearRestored){var result=RestoreDetailed(manifest,clearRestored);if(result.Failed>0)throw new IOException(String.Join("；",result.Errors.Take(3)));return result.Restored;}
 public static RestoreResult RestoreDetailed(string manifest,bool clearRestored){
  string full=Path.GetFullPath(manifest),root=Path.GetDirectoryName(full);var result=new RestoreResult();bool removeIndex=false;
  using(var gate=OpenBatchLock(full)){
   var entries=Read(full);PrepareJournal(full);foreach(var e in entries){try{
    if(!e.Restored&&(File.Exists(e.Original)||Directory.Exists(e.Original))){result.Skipped++;continue;}
    string stored=Path.Combine(root,e.Stored);if(e.Restored&&!OriginalMatches(e)){result.Skipped++;continue;}
    if(e.Restored&&!File.Exists(stored)){e.Removed=true;AppendState(full,e);result.Cleared++;continue;}
    if(!Engine.SafeAncestors(stored))throw new IOException("备份文件包含链接");
    Native.INFO snapshot;using(var source=new FileStream(stored,FileMode.Open,FileAccess.Read,FileShare.Read)){
     if(!Native.GetFileInformationByHandle(source.SafeFileHandle,out snapshot)||(snapshot.Attributes&(Native.UnsafeAttributes|0x10))!=0||snapshot.Links!=1||!Engine.Normalize(Native.FinalPath(source.SafeFileHandle)).Equals(Engine.Normalize(stored),StringComparison.OrdinalIgnoreCase))throw new IOException("无法安全读取备份文件");
     using(var sha=SHA256.Create()){if(source.Length!=e.Bytes||BitConverter.ToString(sha.ComputeHash(source)).Replace("-","")!=e.Hash)throw new IOException("备份文件校验失败，保留备份");}source.Position=0;
     if(!e.Restored){string parent=Path.GetDirectoryName(e.Original),existing=parent;while(!Directory.Exists(existing)){existing=Path.GetDirectoryName(existing);if(String.IsNullOrEmpty(existing))throw new IOException("原磁盘不可用");}if(!Engine.SafeAncestors(Path.Combine(existing,"probe")))throw new IOException("恢复位置包含链接");Directory.CreateDirectory(parent);if(!Engine.SafeAncestors(e.Original))throw new IOException("恢复位置包含链接");
      string temporary=Path.Combine(parent,".clearspace-restore-"+Guid.NewGuid().ToString("N")+".tmp");try{
       using(var target=new FileStream(temporary,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None)){if(!Engine.Normalize(Native.FinalPath(target.SafeFileHandle)).Equals(Engine.Normalize(temporary),StringComparison.OrdinalIgnoreCase))throw new IOException("恢复位置发生变化");source.CopyTo(target);target.Flush(true);target.Position=0;using(var sha=SHA256.Create())if(target.Length!=e.Bytes||BitConverter.ToString(sha.ComputeHash(target)).Replace("-","")!=e.Hash)throw new IOException("恢复文件校验失败");}
       File.SetLastWriteTimeUtc(temporary,DateTime.FromFileTimeUtc(e.Modified));if(!Engine.SafeAncestors(temporary)||!Engine.SafeAncestors(e.Original)||(File.GetAttributes(temporary)&FileAttributes.ReparsePoint)!=0)throw new IOException("恢复位置包含链接");try{File.Move(temporary,e.Original);}catch(IOException){if(File.Exists(e.Original)||Directory.Exists(e.Original)){result.Skipped++;continue;}throw;}result.Restored++;
      }finally{if(File.Exists(temporary)&&Engine.SafeAncestors(temporary)&&(File.GetAttributes(temporary)&FileAttributes.ReparsePoint)==0)File.Delete(temporary);}
     }
     if(clearRestored){e.Restored=true;AppendState(full,e);}
    }
    if(clearRestored){using(var handle=Native.CreateFile(stored,0x10080u,1,IntPtr.Zero,3,0x200000,IntPtr.Zero)){Native.INFO fresh;if(handle.IsInvalid||!Native.GetFileInformationByHandle(handle,out fresh)||fresh.Volume!=snapshot.Volume||fresh.IndexHigh!=snapshot.IndexHigh||fresh.IndexLow!=snapshot.IndexLow||fresh.Bytes!=snapshot.Bytes||fresh.Write.Value!=snapshot.Write.Value||fresh.Links!=1||(fresh.Attributes&(Native.UnsafeAttributes|0x10|1))!=0||!Engine.Normalize(Native.FinalPath(handle)).Equals(Engine.Normalize(stored),StringComparison.OrdinalIgnoreCase))throw new IOException("文件已恢复，备份占用或发生变化，暂未清除");var disposition=new Native.DISPOSITION{Delete=true};if(!Native.SetFileInformationByHandle(handle,4,ref disposition,(uint)Marshal.SizeOf(typeof(Native.DISPOSITION))))throw new IOException("文件已恢复，备份权限限制，暂未清除");}}
    if(clearRestored){if(File.Exists(stored))throw new IOException("文件已恢复，系统尚未完成删除备份");e.Removed=true;AppendState(full,e);result.Cleared++;}
   }catch(Exception ex){result.Failed++;result.Errors.Add(Path.GetFileName(e.Original)+"："+ex.Message);}}
   result.Pending=Read(full).Count;removeIndex=clearRestored&&result.Pending==0&&result.Failed==0;
   if(removeIndex){if(!Engine.SafeAncestors(full)||(File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0)throw new IOException("备份索引位置发生变化");File.Delete(full);}
  }
  if(removeIndex)RemoveEmptyBatch(root);
  return result;
 }
 static void RemoveEmptyBatch(string root){
  if(!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(root),"^[0-9]{8}-[0-9]{6}-[0-9a-f]{32}$"))return;
  for(int attempt=0;attempt<4;attempt++)try{if(!Directory.Exists(root)||!Engine.SafeAncestors(Path.Combine(root,"probe"))||Directory.GetFileSystemEntries(root).Length!=0)return;Directory.Delete(root,false);return;}catch(IOException){Thread.Sleep(40*(attempt+1));}catch(UnauthorizedAccessException){return;}
 }
 internal static SafeFileHandle OpenBatchLock(string manifest){
  if(!Engine.SafeAncestors(manifest))throw new IOException("备份位置包含链接");string path=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(manifest)),"recovery.csbackup.lock");
  var handle=Native.CreateFile(path,0xC0010080u,0,IntPtr.Zero,4,0x200000,IntPtr.Zero);
  try{Native.INFO info;if(handle.IsInvalid||!Native.GetFileInformationByHandle(handle,out info)||(info.Attributes&(Native.UnsafeAttributes|0x10|1))!=0||info.Links!=1||info.Bytes!=0||!Engine.Normalize(Native.FinalPath(handle)).Equals(Engine.Normalize(path),StringComparison.OrdinalIgnoreCase))throw new IOException("备份正在处理中，或锁文件位置无法验证");var disposition=new Native.DISPOSITION{Delete=true};if(!Native.SetFileInformationByHandle(handle,4,ref disposition,(uint)Marshal.SizeOf(typeof(Native.DISPOSITION))))throw new IOException("无法安全锁定备份");return handle;}catch{handle.Dispose();throw;}
 }
 static void DeleteOwnedBackup(string file,bool missingAllowed){
  if(!Engine.SafeAncestors(file))throw new IOException("备份位置包含链接");using(var handle=Native.CreateFile(file,0x10080u,1,IntPtr.Zero,3,0x200000,IntPtr.Zero)){
   if(handle.IsInvalid){int error=Marshal.GetLastWin32Error();if(missingAllowed&&(error==2||error==3))return;throw new IOException("无法清除备份文件（"+error+"）");}
   Native.INFO info;if(!Native.GetFileInformationByHandle(handle,out info)||(info.Attributes&(Native.UnsafeAttributes|0x10|1))!=0||info.Links!=1||!Engine.Normalize(Native.FinalPath(handle)).Equals(Engine.Normalize(file),StringComparison.OrdinalIgnoreCase))throw new IOException("备份文件包含链接或位置发生变化");var disposition=new Native.DISPOSITION{Delete=true};if(!Native.SetFileInformationByHandle(handle,4,ref disposition,(uint)Marshal.SizeOf(typeof(Native.DISPOSITION))))throw new IOException("备份文件占用或无删除权限");
  }
 }
 public static void Purge(string manifest){
  string full=Path.GetFullPath(manifest),root=Path.GetDirectoryName(full);using(var gate=OpenBatchLock(full)){
   var entries=Read(full);PrepareJournal(full);foreach(var entry in entries){DeleteOwnedBackup(Path.Combine(root,entry.Stored),true);entry.Removed=true;AppendState(full,entry);}DeleteOwnedBackup(full,false);
  }RemoveEmptyBatch(root);
 }
}
}
