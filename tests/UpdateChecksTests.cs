using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace ClearSpace {
internal static class UpdateChecksTests {
 internal static void Verify(string signed,string key,Action<bool,string> check){
  int primary=0,fallback=0;
  var release=UpdateChecks.Check(CancellationToken.None,(url,limit,token)=>{if(url==SoftwareUpdates.DirectManifestUrl)Interlocked.Increment(ref primary);else Interlocked.Increment(ref fallback);return signed;},key,1000,3000);
  check(primary==1&&fallback==0&&release.Build==2,"A fast signed channel needs only one request");
  primary=fallback=0;var watch=Stopwatch.StartNew();
  release=UpdateChecks.Check(CancellationToken.None,(url,limit,token)=>{if(url==SoftwareUpdates.DirectManifestUrl){Interlocked.Increment(ref primary);throw new IOException("primary unavailable");}Interlocked.Increment(ref fallback);return signed;},key,2000,3000);
  check(primary==1&&fallback==1&&watch.ElapsedMilliseconds<1500&&release.Build==2,"A failed primary starts the official fallback immediately");
  using(var cancelled=new ManualResetEventSlim()){
   release=UpdateChecks.Check(CancellationToken.None,(url,limit,token)=>{if(url==SoftwareUpdates.DirectManifestUrl){token.WaitHandle.WaitOne();cancelled.Set();token.ThrowIfCancellationRequested();}return signed;},key,10,3000);
   check(release.Build==2&&cancelled.Wait(1500),"The fallback can win a slow connection and cancels the losing primary");
  }
  primary=fallback=0;
  release=UpdateChecks.Check(CancellationToken.None,(url,limit,token)=>{if(url==SoftwareUpdates.DirectManifestUrl){Interlocked.Increment(ref primary);return "{}";}Interlocked.Increment(ref fallback);return signed;},key,1000,3000);
  check(primary==1&&fallback==1&&release.Build==2,"Unsigned primary data cannot win the race; fallback data must pass signature verification");
  bool rejected=false;try{UpdateChecks.Check(CancellationToken.None,(url,limit,token)=>"{}",key,10,3000);}catch(InvalidDataException){rejected=true;}
  check(rejected,"Two unsigned manifests still fail without offering an update");
  watch.Restart();bool timedOut=false;
  try{UpdateChecks.Check(CancellationToken.None,(url,limit,token)=>{token.WaitHandle.WaitOne();token.ThrowIfCancellationRequested();return signed;},key,10,150);}catch(IOException ex){timedOut=ex.Message.Contains("超时")||ex.Message.Contains("timed out");}
  check(timedOut&&watch.ElapsedMilliseconds<2000,"One total deadline bounds both slow connections and reports a timeout");
  using(var stop=new CancellationTokenSource()){
   stop.Cancel();int reads=0;bool stopped=false;try{UpdateChecks.Check(stop.Token,(url,limit,token)=>{reads++;return signed;},key);}catch(OperationCanceledException){stopped=true;}
   check(stopped&&reads==0,"Cancellation before a check prevents every request");
  }
  using(var stop=new CancellationTokenSource())using(var entered=new ManualResetEventSlim()){
   var work=Task.Run(()=>UpdateChecks.Check(stop.Token,(url,limit,token)=>{entered.Set();token.WaitHandle.WaitOne();token.ThrowIfCancellationRequested();return signed;},key,1000,3000));
   check(entered.Wait(1500),"The cancellation fixture starts a pending update request");stop.Cancel();bool stopped=false;try{work.GetAwaiter().GetResult();}catch(OperationCanceledException){stopped=true;}
   check(stopped,"Closing or cancelling propagates cancellation rather than a misleading network error");
  }
  check(SoftwareUpdates.TrustedManifest(new Uri(SoftwareUpdates.DirectManifestUrl+"?check=1"))&&!SoftwareUpdates.TrustedDownload(new Uri(SoftwareUpdates.DirectManifestUrl)),"Only manifest reads may use the exact official raw endpoint; packages cannot use it");
  check(!SoftwareUpdates.TrustedManifest(new Uri(SoftwareUpdates.DirectManifestUrl.Replace("/updates/","/main/")))&&!SoftwareUpdates.TrustedManifest(new Uri(SoftwareUpdates.DirectManifestUrl.Replace("https://","http://")))&&!SoftwareUpdates.TrustedManifest(new Uri(SoftwareUpdates.DirectManifestUrl+"#fragment")),"Manifest redirects cannot switch branches, downgrade HTTPS or add fragments");
 }
}
}
