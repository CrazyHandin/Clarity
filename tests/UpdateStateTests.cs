using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifySoftwareUpdateState(string output,Action<bool,string> check){
  var loader=SoftwareCheck;var previous=softwareRelease;var show=ShowSoftwareDialog;var downloader=SoftwareDownload;var sessions=SoftwareSession;var popups=new List<Window>();
  Func<SoftwareRelease> offer=()=>new SoftwareRelease{Version=ReleaseVersion.Display,Build=ReleaseVersion.Build+1,Notes="Fixture update",Assets=new List<SoftwareAsset>{new SoftwareAsset{Name="state-setup.exe",Size=1024},new SoftwareAsset{Name="state-portable.zip",Size=1024}}};
  Action<Button> click=button=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  ShowSoftwareDialog=popup=>{popups.Add(popup);popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=popup.Top=-12000;popup.ShowActivated=false;UiMotion.SetEnabled(popup,false);popup.Show();};
  try{
   softwareRelease=null;RefreshSoftwareUpdateState();ShowReady();OpenSettings(new CleanerSettings(),null,s=>{});OpenAbout();var about=pages[pages.Count-1];Func<string> caption=()=>((TextBlock)about.FindName("UpdateCheckText")).Text;var button=(Button)about.FindName("SoftwareUpdateButton");
   check(caption()=="检查软件更新"&&button.IsEnabled,"About initially offers a check when no newer release is known");
   using(var gate=new ManualResetEventSlim(false)){
    var entered=new TaskCompletionSource<bool>();int requests=0;SoftwareCheck=token=>{Interlocked.Increment(ref requests);entered.TrySetResult(true);gate.Wait(token);return offer();};
    var automatic=CheckSoftwareUpdatesAsync(false);try{await entered.Task;var repeated=CheckSoftwareUpdatesAsync(false);check(ReferenceEquals(automatic,repeated)&&requests==1&&caption()=="正在检查…"&&!button.IsEnabled,"Overlapping automatic checks share one request and a consistent checking state");}finally{gate.Set();}await automatic;
   }
   check(caption()=="点击更新"&&button.IsEnabled&&((FrameworkElement)about.FindName("AboutUpdateBadge")).Visibility==Visibility.Visible&&popups.Count==0,"An automatic result immediately turns the open About action into Update now without opening a popup");App.Render(Window,Path.Combine(output,"about-update-ready.png"));
   int checkedAgain=0;SoftwareCheck=token=>{checkedAgain++;throw new IOException("Fixture unavailable connection");};click(button);var cached=softwareDialog;check(cached!=null&&ReferenceEquals(cached.Release,softwareRelease)&&checkedAgain==0,"Clicking Update now opens the discovered release directly even when the check connection is unavailable");click(button);check(popups.Count==1&&ReferenceEquals(softwareDialog,cached),"Repeated update actions reuse the existing download window");cached.Window.Close();
   check(caption()=="点击更新"&&SoftwareAvailable,"Choosing Later retains the discovered update and its action");BackPage();OpenAbout();about=pages[pages.Count-1];button=(Button)about.FindName("SoftwareUpdateButton");check(caption()=="点击更新","Reopening About restores the cached update action");
   await CheckSoftwareUpdatesAsync(true);check(checkedAgain==1&&caption()=="点击更新"&&SoftwareAvailable&&Get<TextBlock>("ToastText").Text.Contains("检查失败"),"A failed recheck preserves the previous update and reports the connection error");
   var retained=softwareRelease;using(var gate=new ManualResetEventSlim(false)){
    var entered=new TaskCompletionSource<bool>();SoftwareCheck=token=>{entered.TrySetResult(true);gate.Wait();return new SoftwareRelease{Version=ReleaseVersion.Display,Build=ReleaseVersion.Build};};var cancelled=CheckSoftwareUpdatesAsync(false);try{await entered.Task;softwareCheckStop.Cancel();}finally{gate.Set();}await cancelled;
   }
   check(ReferenceEquals(softwareRelease,retained)&&caption()=="点击更新","A cancelled check cannot replace an existing update with a late result");
   SoftwareCheck=token=>new SoftwareRelease{Version=ReleaseVersion.Display,Build=ReleaseVersion.Build};await CheckSoftwareUpdatesAsync(true);check(caption()=="检查软件更新"&&button.IsEnabled&&Get<FrameworkElement>("RuleCenterBadge").Visibility==Visibility.Collapsed&&((FrameworkElement)about.FindName("AboutUpdateBadge")).Visibility==Visibility.Collapsed&&((FrameworkElement)settingsView.Root.FindName("AboutBadge")).Visibility==Visibility.Collapsed,"A completed no-update check clears all three badges and restores the check action");
   using(var gate=new ManualResetEventSlim(false)){
    var entered=new TaskCompletionSource<bool>();int requests=0;SoftwareCheck=token=>{Interlocked.Increment(ref requests);entered.TrySetResult(true);gate.Wait(token);return offer();};var automatic=CheckSoftwareUpdatesAsync(false);try{await entered.Task;var manual=CheckSoftwareUpdatesAsync(true);check(ReferenceEquals(automatic,manual)&&requests==1,"A manual request joins the pending startup check without another network request");}finally{gate.Set();}await automatic;
   }
   var dialog=softwareDialog;check(dialog!=null&&caption()=="点击更新","A manual request that joins startup checking receives the discovered update dialog");UiText.SetLanguage("en");check(caption()=="Update now","The cached update action follows the selected interface language");
   var shown=dialog.Release;softwareRelease=offer();softwareRelease.Build++;softwareRelease.Assets=new List<SoftwareAsset>{new SoftwareAsset{Name="other-setup.exe",Size=2048},new SoftwareAsset{Name="other-portable.zip",Size=2048}};RefreshSoftwareUpdateState();
   var downloadingEntered=new TaskCompletionSource<bool>();Action<long,long> staleProgress=null;SoftwareAsset selected=null;int downloads=0;SoftwareSession=()=>Path.Combine(output,"update-state-fixture");SoftwareDownload=(asset,directory,token,progress)=>{Interlocked.Increment(ref downloads);selected=asset;staleProgress=progress;progress(512,1024);downloadingEntered.TrySetResult(true);token.WaitHandle.WaitOne();token.ThrowIfCancellationRequested();throw new IOException("Fixture download must not install");};
   click(dialog.Install);var downloading=LastSoftwareOperation;await downloadingEntered.Task;await Task.Delay(60);await InstallSoftwareAsync(dialog);check(downloads==1&&shown.Assets.Contains(selected)&&caption()=="Updating…"&&!button.IsEnabled&&dialog.Progress.Value==50,"Downloading uses exactly the release shown in its window and rejects duplicate starts");dialog.Cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await downloading;
   check(caption()=="Update now"&&button.IsEnabled&&dialog.Install.IsEnabled&&dialog.Window.IsVisible,"Download cancellation restores the update action and leaves retry available");staleProgress(1024,1024);await Task.Delay(60);check(dialog.Progress.Value==50,"Late progress from a cancelled attempt cannot change the download display");
   SoftwareDownload=(asset,directory,token,progress)=>{throw new IOException("Fixture download failure");};click(dialog.Install);await LastSoftwareOperation;check(!installingSoftware&&caption()=="Update now"&&button.IsEnabled&&dialog.Install.IsEnabled&&dialog.State.Text.Contains("not applied"),"A download failure retains both update actions for retry");dialog.Window.Close();
  }finally{
   if(softwareCheckStop!=null)softwareCheckStop.Cancel();if(softwareDownloadStop!=null)softwareDownloadStop.Cancel();foreach(var popup in popups)if(popup.IsVisible){UiMotion.SetEnabled(popup,false);popup.Close();}SoftwareCheck=loader;SoftwareDownload=downloader;SoftwareSession=sessions;ShowSoftwareDialog=show;softwareRelease=previous;RefreshSoftwareUpdateState();while(pages.Count>0)BackPage();UiText.SetLanguage("zh-CN");UiMotion.SetEnabled(Window,false);
  }
 }
}
}
