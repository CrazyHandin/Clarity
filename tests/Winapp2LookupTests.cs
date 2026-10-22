#if CLARITY_TESTS
using System;
using System.IO;
using System.Linq;
using System.Threading;
namespace ClearSpace {
public static class Winapp2LookupTests {
 public static void Verify(string output,Action<bool,string> check){
  string root=Path.Combine(Path.GetFullPath(output),"winapp2-lookup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  var environment=new Winapp2Environment{Drive=Path.GetPathRoot(root),AllowedBases=new[]{root},DeniedBases=new string[0],OSVersion=new Version(10,0,19045)};environment.Variables["LocalAppData"]=new[]{root};
  string raw="[Fixture Cache *]\nDetectFile=%LocalAppData%\\Fixture\\Cache\nFileKey1=%LocalAppData%\\Fixture\\Cache|*.tmp\n";var pack=Winapp2Parser.Parse(raw);var discovery=new DiscoveryResult();
  check(Winapp2Adapter.Resolve(pack,discovery,environment,CancellationToken.None,null).Count==0,"A missing cache directory produces no Winapp2 cleanup scope");string cache=Path.Combine(root,"Fixture","Cache");Directory.CreateDirectory(cache);
  var rules=Winapp2Adapter.Resolve(pack,discovery,environment,CancellationToken.None,null);check(rules.Count==1&&rules[0].Roots.Contains(cache)&&!rules[0].Quick&&rules[0].ForceBackup,"A new match scope refreshes previous negative detections and preserves community backup policy");
  using(var lookup=new Winapp2Lookup()){
   var local=environment.ForMatching(lookup);string[] first=local.Expand("%LocalAppData%\\Fixture\\*",false,CancellationToken.None);int reads=lookup.FileSystemReads;
   string[] second=local.Expand("%LocalAppData%\\Fixture\\*",false,CancellationToken.None);check(first.SequenceEqual(second)&&lookup.FileSystemReads==reads&&lookup.PathReuses>0,"Repeated wildcard and prefix lookups share one bounded set of observations within matching");
   int probes=0;bool a=lookup.Registry("HKCU\\Software\\Fixture",()=>{probes++;return false;}),b=lookup.Registry("hkcu\\software\\fixture",()=>{probes++;return true;});check(!a&&!b&&probes==1,"Repeated positive or negative registry conditions are read once per match scope");
   using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool canceled=false;try{local.Expand("%LocalAppData%\\Fixture\\*",false,cancel.Token);}catch(OperationCanceledException){canceled=true;}check(canceled,"A cached path expansion still responds immediately to cancellation");}
   string missing=Path.Combine(root,"Later");check(!lookup.DirectoryExists(missing),"The cache exercises a negative directory observation");Directory.CreateDirectory(missing);lookup.Dispose();check(lookup.DirectoryExists(missing),"Disposal releases observations and cannot return a stale directory result afterwards");
  }
  using(var bounded=new Winapp2Lookup()){
   for(int i=0;i<Winapp2Lookup.MaxKeys;i++)bounded.Paths("key-"+i,()=>new string[0],CancellationToken.None);int reads=0;for(int i=0;i<2;i++)bounded.Paths("overflow",()=>{reads++;return new string[0];},CancellationToken.None);check(reads==2,"Path-query key retention has a fixed upper bound");
  }
  using(var bounded=new Winapp2Lookup()){
   var block=Enumerable.Range(0,256).Select(i=>"path-"+i).ToArray();for(int i=0;i<Winapp2Lookup.MaxPaths/block.Length;i++)bounded.Paths("block-"+i,()=>block,CancellationToken.None);int reads=0;for(int i=0;i<2;i++)bounded.Paths("overflow",()=>{reads++;return new[]{"another path"};},CancellationToken.None);check(reads==2,"Retained path results have a separate memory bound from query keys");
  }
  var numbered=new Winapp2Entry();foreach(string key in new[]{"FileKey","filekey1","FileKey999","FileKey1x","FileKeyExtra","DetectFile"})numbered.Directives.Add(new IniDirective{Key=key});check(numbered.Get("FileKey").Count()==3&&!numbered.Get("Detect").Any(),"Directive grouping preserves duplicate and numbered keys without accepting unrelated suffixes");
  string file=Path.Combine(cache,"payload.tmp");File.WriteAllText(file,"temporary fixture");File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddDays(-30));var engine=new Engine(rules){IsRunning=p=>false,FileBackupDirectory=Path.Combine(root,"backups")};var findings=engine.Scan(CancellationToken.None,null);File.WriteAllText(file,"%PDF personal fixture replacing cached content");File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddDays(-30));var result=engine.Clean(findings,false,CancellationToken.None,null);check(result.Deleted==0&&File.Exists(file),"Matched Winapp2 roots never bypass fresh content and identity checks at cleanup");
 }
}
}
#endif
