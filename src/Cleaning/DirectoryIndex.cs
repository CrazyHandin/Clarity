using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;
namespace ClearSpace {
public sealed class ScanEntry {
 public string FullName,Name;public FileAttributes Attributes;public long Bytes,Modified,Created;public uint Volume;public ulong FileId;public bool Indexed;
 public static ScanEntry From(FileSystemInfo info){bool directory=(info.Attributes&FileAttributes.Directory)!=0;return new ScanEntry{FullName=info.FullName,Name=info.Name,Attributes=info.Attributes,Bytes=directory?0:((FileInfo)info).Length,Modified=info.LastWriteTimeUtc.ToFileTimeUtc(),Created=info.CreationTimeUtc.ToFileTimeUtc()};}
}
// Read only the requested directory's index in 64 KB batches. No volume, raw MFT,
// backup privilege, resident watcher or persistent file identity index is used.
internal sealed class DirectoryIndex:IDisposable {
 const int Capacity=65536,HeaderSize=104;readonly SafeFileHandle handle;readonly IntPtr buffer;readonly string directory;readonly uint volume;bool empty,disposed;
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandleEx(SafeFileHandle file,int kind,IntPtr data,uint size);
#if CLARITY_TESTS
 internal static bool ForceFallback;
#endif
 DirectoryIndex(string directory,SafeFileHandle handle,IntPtr buffer,uint volume,bool empty){this.directory=directory;this.handle=handle;this.buffer=buffer;this.volume=volume;this.empty=empty;}
 public static DirectoryIndex Open(string directory){
#if CLARITY_TESTS
  if(ForceFallback)return null;
#endif
  SafeFileHandle handle=null;IntPtr buffer=IntPtr.Zero;
  try{
   if(ScanPolicy.Excluded(directory)||!Engine.SafeAncestors(Path.Combine(directory,"probe")))return null;
   handle=Native.CreateFile(directory,0x81,7,IntPtr.Zero,3,0x02200000,IntPtr.Zero);Native.INFO info;
   if(handle.IsInvalid||!Native.GetFileInformationByHandle(handle,out info)||(info.Attributes&0x400)!=0||!String.Equals(Engine.Normalize(directory),Engine.Normalize(Native.FinalPath(handle)),StringComparison.OrdinalIgnoreCase))return null;
   buffer=Marshal.AllocHGlobal(Capacity);bool success=GetFileInformationByHandleEx(handle,11,buffer,Capacity);int error=Marshal.GetLastWin32Error();
   if(!success&&error!=18)return null;
   var reader=new DirectoryIndex(directory,handle,buffer,info.Volume,!success);handle=null;buffer=IntPtr.Zero;return reader;
  }catch(EntryPointNotFoundException){return null;}catch(IOException){return null;}catch(UnauthorizedAccessException){return null;}
  finally{if(handle!=null)handle.Dispose();if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);}
 }
 public IEnumerable<ScanEntry> Read(CancellationToken token){
  if(empty)yield break;
  do{
   int offset=0;
   while(true){
    token.ThrowIfCancellationRequested();if(offset<0||offset>Capacity-HeaderSize)throw new IOException("Invalid directory index record");IntPtr record=IntPtr.Add(buffer,offset);
    int next=Marshal.ReadInt32(record,0),length=Marshal.ReadInt32(record,60);if(length<=0||(length&1)!=0||length>Capacity-offset-HeaderSize||next!=0&&(next<HeaderSize+length||(next&7)!=0||next>Capacity-offset))throw new IOException("Invalid directory index bounds");
    string name=Marshal.PtrToStringUni(IntPtr.Add(record,HeaderSize),length/2);
    if(name!="."&&name!=".."){if(name.IndexOfAny(new[]{'\\','/',':','\0'})>=0)throw new IOException("Invalid directory index name");
     yield return new ScanEntry{FullName=Path.Combine(directory,name),Name=name,Attributes=(FileAttributes)Marshal.ReadInt32(record,56),Bytes=Marshal.ReadInt64(record,40),Created=Marshal.ReadInt64(record,8),Modified=Marshal.ReadInt64(record,24),FileId=unchecked((ulong)Marshal.ReadInt64(record,96)),Volume=volume,Indexed=true};
    }
    if(next==0)break;offset+=next;
   }
   token.ThrowIfCancellationRequested();if(GetFileInformationByHandleEx(handle,10,buffer,Capacity))continue;int error=Marshal.GetLastWin32Error();if(error==18)yield break;throw new IOException("Directory index read failed",new Win32Exception(error));
  }while(true);
 }
 public void Dispose(){if(disposed)return;disposed=true;Marshal.FreeHGlobal(buffer);handle.Dispose();}
}
}
