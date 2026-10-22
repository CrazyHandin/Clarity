#if CLARITY_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace ClearSpace {
public static class DirectoryIndexTests {
 public static void Verify(string output,Action<bool,string> check){
  string root=Path.Combine(Path.GetFullPath(output),"directory-index-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  Func<string,string> write=name=>{string file=Path.Combine(root,name);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,"index fixture "+name);File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddDays(-30));return file;};
  string first=write("space 汉字.bin"),second=write("sub\\second.bin"),readOnly=write("readonly.bin");File.SetAttributes(readOnly,File.GetAttributes(readOnly)|FileAttributes.ReadOnly);
  var rule=new Rule("directory-index","Index fixture","软件缓存","","",new[]{root},7,true);bool ntfs=String.Equals(new DriveInfo(Path.GetPathRoot(root)).DriveFormat,"NTFS",StringComparison.OrdinalIgnoreCase);
  using(var session=new ScanSession()){
   var entries=session.Entries(root,CancellationToken.None).ToArray();check(entries.Length==3&&entries.Any(e=>e.Name=="space 汉字.bin")&&entries.Any(e=>(e.Attributes&FileAttributes.Directory)!=0),"Batched directory records preserve Unicode names, spaces and directory attributes");
   check(!ntfs||session.Statistics.IndexDirectories==1&&entries.All(e=>e.Indexed),"NTFS fixtures exercise the real native directory-index reader");
   var entry=entries.Single(e=>e.FullName==first);var expected=new FileInfo(first);check(entry.Bytes==expected.Length&&entry.Modified==expected.LastWriteTimeUtc.ToFileTimeUtc()&&entry.Created==expected.CreationTimeUtc.ToFileTimeUtc(),"Native batch metadata agrees with filesystem lengths and timestamps");
  }
  List<Finding> native;using(var session=new ScanSession()){
   native=new Engine(new List<Rule>{rule},session){IsRunning=p=>false}.Scan(CancellationToken.None,null);check(native.Single().Candidates.Count==2&&native.Single().Links==1,"Indexed scans retain the original read-only and per-file identity protections");
   check(!ntfs||session.Statistics.IndexDirectories==2,"Nested directories are read in bounded native batches");
  }
  DirectoryIndex.ForceFallback=true;try{using(var session=new ScanSession()){
   var managed=new Engine(new List<Rule>{rule},session){IsRunning=p=>false}.Scan(CancellationToken.None,null);check(session.Statistics.IndexDirectories==0&&session.Statistics.IndexFallbacks==2&&managed.Single().Candidates.Select(c=>c.Path).OrderBy(p=>p).SequenceEqual(native.Single().Candidates.Select(c=>c.Path).OrderBy(p=>p))&&managed.Single().EligibleBytes==native.Single().EligibleBytes,"Unsupported index APIs fall back to managed enumeration with identical candidates and bytes");
  }}finally{DirectoryIndex.ForceFallback=false;}
  string fresh=Path.Combine(root,"recent");Directory.CreateDirectory(fresh);for(int i=0;i<16;i++)File.WriteAllText(Path.Combine(fresh,i+".bin"),"recent fixture");var recentRule=new Rule("index-recent","Recent fixture","软件缓存","","",new[]{fresh},7,true){HeaderGuard=true};
  using(var session=new ScanSession()){
   var finding=new Engine(new List<Rule>{recentRule},session).Scan(CancellationToken.None,null).Single();check(finding.Files==16&&finding.Recent==16&&finding.Candidates.Count==0&&session.Statistics.IndividualCaptures==0&&session.Statistics.HeaderReads==0,"Recent files are rejected from live batch timestamps before individual opens or header reads");
  }
  string backup=Path.Combine(root,"backup");Directory.CreateDirectory(backup);write("backup\\files.csbackup");write("backup\\must-survive.bin");using(var session=new ScanSession()){
   var finding=new Engine(new List<Rule>{rule},session).Scan(CancellationToken.None,null).Single();check(!finding.Samples.Any(p=>p.StartsWith(backup,StringComparison.OrdinalIgnoreCase))&&!finding.Candidates.Any(p=>p.Path.StartsWith(backup,StringComparison.OrdinalIgnoreCase)),"Per-directory recovery checks keep backup manifests and contents outside candidate collection");
  }
  string changing=Path.Combine(root,"changing");Directory.CreateDirectory(changing);string mutable=write("changing\\old.bin");bool changed=false;var changingRule=new Rule("index-changing","Changing fixture","软件缓存","","",new[]{changing},7,true){AdditionalExclude=p=>{if(!changed){changed=true;File.AppendAllText(p," changed during scan");}return false;}};
  using(var session=new ScanSession()){
   var finding=new Engine(new List<Rule>{changingRule},session).Scan(CancellationToken.None,null).Single();check(finding.Candidates.Count==0&&(!ntfs||finding.Changed>0),"Files changed after batch enumeration are rejected by fresh native identity capture");
  }
  using(var source=new CancellationTokenSource())using(var session=new ScanSession()){
   bool canceled=false;try{foreach(var entry in session.Entries(root,source.Token)){source.Cancel();}}catch(OperationCanceledException){canceled=true;}check(canceled,"Native batch enumeration honors cancellation and unwinds its directory handle");
  }
  File.SetAttributes(readOnly,File.GetAttributes(readOnly)&~FileAttributes.ReadOnly);
 }
}
}
#endif
