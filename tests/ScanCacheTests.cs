#if CLARITY_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace ClearSpace {
public static class ScanCacheTests {
 public static void Verify(string output,Action<bool,string> check){
  string root=Path.Combine(Path.GetFullPath(output),"scan-cache-"+Guid.NewGuid().ToString("N")),data=Path.Combine(root,"Cache"),storage=Path.Combine(root,"index","metadata.dat");Directory.CreateDirectory(data);
  Func<string,string,string> write=(name,text)=>{string file=Path.Combine(data,name);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,text);File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddDays(-30));return file;};
  string first=write("one.bin","cache payload"),second=write("sub\\two.bin","more cache"),personal=write("document.bin","%PDF personal fixture");
  Func<List<Rule>> rules=()=>Enumerable.Range(0,3).Select(i=>new Rule("cache-test-"+i,"Fixture cache","软件缓存","","",new[]{data},7,true){HeaderGuard=true}).ToList();
  Func<ScanSession,List<Finding>> scan=session=>new Engine(rules(),session){IsRunning=p=>false}.Scan(CancellationToken.None,null);
  long bytes;using(var cold=new ScanSession(true,storage)){
   var findings=scan(cold);bytes=findings.Sum(f=>f.EligibleBytes);check(findings.Sum(f=>f.Candidates.Count)==2,"Cold scan retains personal file headers and deduplicates overlapping candidates");
   check(cold.Statistics.DirectoryReads==2&&cold.Statistics.DirectoryReuses==4&&cold.Statistics.SnapshotReuses>=4,"One scan shares bounded directory listings and native snapshots across overlapping rules");
   cold.Complete(CancellationToken.None);check(File.Exists(storage)&&cold.Statistics.HeaderReads==3,"The first scan records only bounded metadata, not candidate authorizations");
  }
  using(var warm=new ScanSession(true,storage)){
   var findings=scan(warm);check(findings.Sum(f=>f.EligibleBytes)==bytes&&findings.Sum(f=>f.Candidates.Count)==2,"A warm scan has the same live candidates and byte total as the cold scan");
   check(warm.Statistics.HeaderReads==0&&warm.Statistics.HeaderCacheHits==9,"Unchanged file classifications survive process sessions without reopening content");warm.Complete(CancellationToken.None);
  }
  write("added.bin","newly added cache");File.AppendAllText(second," changed length");File.SetLastWriteTimeUtc(second,DateTime.UtcNow.AddDays(-20));File.Delete(first);
  using(var changed=new ScanSession(true,storage)){
   var findings=scan(changed);check(findings.SelectMany(f=>f.Candidates).Any(f=>f.Path.EndsWith("added.bin"))&&!findings.SelectMany(f=>f.Candidates).Any(f=>f.Path==first),"Warm scans enumerate live directories and detect added and deleted files");
   check(findings.Sum(f=>f.EligibleBytes)==new FileInfo(second).Length+new FileInfo(Path.Combine(data,"added.bin")).Length,"Cached classifications do not supply stale file sizes");
   check(changed.Statistics.HeaderReads==2,"Modified files invalidate classifications independently of directory timestamps");changed.Complete(CancellationToken.None);
  }
  using(var running=new ScanSession(true,storage)){var findings=new Engine(rules(),running){IsRunning=p=>true}.Scan(CancellationToken.None,null);check(findings.All(f=>f.Running&&f.Candidates.Count==0),"Persistent caches never bypass fresh running-process protection");}
  var aged=rules();aged.ForEach(r=>r.Days=365);using(var session=new ScanSession(true,storage)){check(new Engine(aged,session).Scan(CancellationToken.None,null).All(f=>f.Candidates.Count==0),"Changing rule retention takes effect immediately with existing cache data");}
  string standalone=Path.Combine(root,"plain-cache");using(var disabled=new ScanSession(false,standalone)){scan(disabled);disabled.Complete(CancellationToken.None);check(!File.Exists(standalone)&&disabled.Statistics.HeaderCacheHits==0,"Disabled persistent caching performs live header checks and writes no local index");}
  // Preserving timestamps cannot authorize deletion: the cleaning path checks content and file identity afresh.
  string mutable=write("mutable.bin","CACHE same length"),oldText=File.ReadAllText(mutable);DateTime oldTime=File.GetLastWriteTimeUtc(mutable);
  using(var session=new ScanSession(true,storage)){scan(session);session.Complete(CancellationToken.None);}
  File.WriteAllText(mutable,"%PDF"+new string('x',oldText.Length-4));File.SetLastWriteTimeUtc(mutable,oldTime);
  using(var session=new ScanSession(true,storage)){
   var engine=new Engine(rules(),session){IsRunning=p=>false};var findings=engine.Scan(CancellationToken.None,null);check(findings.SelectMany(f=>f.Candidates).Any(s=>s.Path==mutable),"A matching metadata hint is exercised by the stale-classification regression fixture");
   foreach(var finding in findings)finding.Candidates.RemoveAll(s=>s.Path!=mutable);
   var result=engine.Clean(findings,true,CancellationToken.None,null);check(result.Deleted==0&&File.Exists(mutable),"Cleaning rechecks headers even when timestamps and cached classification were preserved");
  }
  string executable=Path.Combine(root,"fixture.exe");File.Copy(typeof(Engine).Assembly.Location,executable);int reads=0;
  Func<ApplicationIdentity> fingerprint=()=>{reads++;return new ApplicationIdentity{Name="Fixture",Product="Fixture",Company="Test",Version="1",Signature="签名验证通过",Signer="Fixture signer"};};
  using(var cold=new ScanSession(true,storage)){cold.Fingerprint(executable,fingerprint);cold.Complete(CancellationToken.None);}
  using(var warm=new ScanSession(true,storage)){
   var app=warm.Fingerprint(executable,fingerprint);app.Executable=executable;check(reads==1&&app.FingerprintCached&&warm.Statistics.FingerprintCacheHits==1,"Unchanged executable identity reuses fingerprint metadata across sessions");
   check(!RuleStore.MatchesDefinition(new ApplicationRule{MatchNames=new[]{"Fixture"},RequireSignature=true},app)&&!app.FingerprintCached,"Rules requiring trusted signatures verify the actual executable instead of cached trust");
  }
  File.AppendAllText(executable,"modified fixture; never execute");using(var session=new ScanSession(true,storage)){session.Fingerprint(executable,fingerprint);check(reads==2&&session.Statistics.FingerprintCacheHits==0,"Executable changes invalidate cached fingerprints");}
  string before=File.ReadAllText(storage);using(var canceled=new ScanSession(true,storage)){
   write("cancel.bin","cancel fixture");scan(canceled);using(var source=new CancellationTokenSource()){source.Cancel();bool threw=false;try{canceled.Complete(source.Token);}catch(OperationCanceledException){threw=true;}check(threw&&File.ReadAllText(storage)==before,"Cancellation keeps the previous cache and never commits a partial scan");}
  }
  File.WriteAllText(storage,"invalid or tampered cache");using(var invalid=new ScanSession(true,storage)){scan(invalid);check(invalid.Statistics.HeaderReads>0,"Invalid encrypted cache falls back to live scanning");invalid.Complete(CancellationToken.None);check(File.ReadAllText(storage)!="invalid or tampered cache","A completed scan safely replaces a corrupt cache");}
  string blocked=Path.Combine(root,"not-a-directory");File.WriteAllText(blocked,"fixture");using(var unavailable=new ScanSession(true,Path.Combine(blocked,"metadata.dat"))){scan(unavailable);unavailable.Complete(CancellationToken.None);check(unavailable.Statistics.DirectoryReads>0,"Unwritable cache storage does not prevent successful scanning");}
  using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();bool threw=false;try{using(var session=new ScanSession(true,storage))scanCancelled(session,cancelled.Token,data);}catch(OperationCanceledException){threw=true;}check(threw,"Scan-session enumeration remains immediately cancellable");}
  var metadata=new FileInfo(personal);var hint=new ScanSession.CacheEntry{Key="head:"+personal,Stamp=metadata.Length+":"+metadata.LastWriteTimeUtc.Ticks+":"+metadata.CreationTimeUtc.Ticks,Saved=DateTime.UtcNow.AddDays(-8).Ticks,Personal=false};
  Action<ScanSession.CacheFile> save=cache=>AtomicFile.Write(storage,Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(cache)),Encoding.UTF8.GetBytes("Clarity.ScanCache.1"),DataProtectionScope.CurrentUser)));
  save(new ScanSession.CacheFile{Entries=new List<ScanSession.CacheEntry>{hint}});using(var expired=new ScanSession(true,storage)){check(expired.Personal(new FileInfo(personal))&&expired.Statistics.HeaderReads==1,"Expired classifications are discarded and protected content is re-read");}
  hint.Saved=DateTime.UtcNow.Ticks;save(new ScanSession.CacheFile{Version="0.0.0+build.1",Entries=new List<ScanSession.CacheEntry>{hint}});using(var upgraded=new ScanSession(true,storage)){var findings=scan(upgraded);check(!findings.SelectMany(f=>f.Candidates).Any(s=>s.Path==personal)&&upgraded.Statistics.HeaderReads>0,"Software upgrades invalidate previous matcher metadata before scanning");}
  ScanSession.Clear(storage);check(!File.Exists(storage)&&File.Exists(personal),"Clearing the local index leaves application content and backups untouched");
 }
 static void scanCancelled(ScanSession session,CancellationToken token,string directory){foreach(var entry in session.Entries(directory,token)){} }
}
}
#endif
