using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace ClearSpace {
internal static class UpdateChecks {
 internal static SoftwareRelease Check(CancellationToken token,Func<string,long,CancellationToken,string> read,string publicKey=null,int hedgeMilliseconds=400,int timeoutMilliseconds=10000){
  token.ThrowIfCancellationRequested();
  if(hedgeMilliseconds<0||timeoutMilliseconds<1)throw new ArgumentOutOfRangeException();
  using(var stop=CancellationTokenSource.CreateLinkedTokenSource(token)){
   stop.CancelAfter(timeoutMilliseconds);var pending=new List<Task>();Exception failure=null;bool fallback=false;
   Func<string,Task<string>> start=url=>{
    var task=Task.Run(()=>read(url,65536,stop.Token));
    task.ContinueWith(t=>{var observed=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
    return task;
   };
   pending.Add(start(SoftwareUpdates.DirectManifestUrl));var hedge=Task.Delay(hedgeMilliseconds,stop.Token);
   try{
    while(true){
     var waiting=new List<Task>(pending);if(!fallback)waiting.Add(hedge);
     var completed=waiting[Task.WaitAny(waiting.ToArray(),stop.Token)];
     if(completed==hedge){fallback=true;pending.Add(start(SoftwareUpdates.LatestManifestUrl));continue;}
     pending.Remove(completed);
     try{string raw=((Task<string>)completed).GetAwaiter().GetResult();stop.Token.ThrowIfCancellationRequested();return SoftwareUpdates.ParseDownloadManifest(UpdateTrust.Verify(raw,publicKey));}
     catch(Exception ex){stop.Token.ThrowIfCancellationRequested();failure=ex;}
     if(!fallback){fallback=true;pending.Add(start(SoftwareUpdates.LatestManifestUrl));}
     if(pending.Count==0)throw failure;
    }
   }catch(OperationCanceledException){token.ThrowIfCancellationRequested();throw new IOException(UiText.T("软件更新检查超时，请检查网络后重试。"));}
   finally{stop.Cancel();}
  }
 }
}
}
