using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 SoftwareRelease softwareRelease;bool checkingSoftware,installingSoftware,closeUpdateAfterCancel;CancellationTokenSource softwareCheckStop,softwareDownloadStop;SoftwareUpdateView softwareDialog;
 public Func<CancellationToken,SoftwareRelease> SoftwareCheck=SoftwareUpdates.Check;
 public Func<SoftwareAsset,string,CancellationToken,Action<long,long>,string> SoftwareDownload=SoftwareUpdates.Download;
 public Func<string> SoftwareSession=UpdateLauncher.NewSession;
 public Action<Window> ShowSoftwareDialog=window=>{window.ShowDialog();};
 public Task LastSoftwareOperation;
 void InitializeSoftwareUpdates(){Window.Closed+=(s,e)=>{if(softwareCheckStop!=null)softwareCheckStop.Cancel();if(softwareDownloadStop!=null)softwareDownloadStop.Cancel();};}
 public void EnableAutomaticSoftwareUpdates(){EventHandler first=null;first=(s,e)=>{Window.ContentRendered-=first;LastSoftwareOperation=CheckSoftwareUpdatesAsync(false);};Window.ContentRendered+=first;}
 bool SoftwareAvailable{get{return softwareRelease!=null&&softwareRelease.IsNewer(ReleaseVersion.Display,ReleaseVersion.Build);}}
 void RefreshUpdateBadges(){
  Visibility visible=SoftwareAvailable?Visibility.Visible:Visibility.Collapsed;Get<FrameworkElement>("RuleCenterBadge").Visibility=visible;
  if(settingsView!=null){var badge=settingsView.Root.FindName("AboutBadge")as FrameworkElement;if(badge!=null)badge.Visibility=visible;}
  foreach(var page in pages){var badge=page.FindName("AboutUpdateBadge")as FrameworkElement;if(badge!=null)badge.Visibility=visible;}
 }
 void RefreshAboutUpdateState(){foreach(var page in pages){var bar=page.FindName("AboutUpdateProgress")as ProgressBar;if(bar!=null)bar.Visibility=checkingSoftware?Visibility.Visible:Visibility.Collapsed;var check=page.FindName("SoftwareUpdateButton")as Button;if(check!=null)check.IsEnabled=!checkingSoftware&&!installingSoftware;}}
 public async Task CheckSoftwareUpdatesAsync(bool manual){
  if(checkingSoftware||installingSoftware)return;checkingSoftware=true;RefreshAboutUpdateState();var stop=new CancellationTokenSource();softwareCheckStop=stop;bool show=false;
  try{softwareRelease=await Task.Run(()=>SoftwareCheck(stop.Token));if(stop.IsCancellationRequested)return;RefreshUpdateBadges();if(SoftwareAvailable){if(manual)show=true;else notice.Show(UiText.T("发现软件新版本：")+softwareRelease.Label+"\n"+UiText.T("点击下载更新"),false,8,OpenSoftwareUpdates,true);}else if(manual)NotifySuccess("当前已是最新版本");}
  catch(Exception ex){if(!stop.IsCancellationRequested&&manual)Notify("软件更新检查失败："+ex.Message);}
  finally{checkingSoftware=false;if(ReferenceEquals(softwareCheckStop,stop))softwareCheckStop=null;stop.Dispose();RefreshAboutUpdateState();}
  if(show)OpenSoftwareUpdates();
 }
 void OpenReleaseNotes(){ShowSoftwareDialog(UpdateDialogs.Highlights(Window));}
 void OpenSoftwareUpdates(){
  if(busy){Notify("请等待当前任务结束后更新。");return;}if(!SoftwareAvailable)return;if(softwareDialog!=null){softwareDialog.Window.Activate();return;}
  var view=UpdateDialogs.Update(Window,softwareRelease);softwareDialog=view;closeUpdateAfterCancel=false;view.Install.Click+=(s,e)=>{LastSoftwareOperation=InstallSoftwareAsync(view);};view.Cancel.Click+=(s,e)=>{if(softwareDownloadStop!=null)softwareDownloadStop.Cancel();};view.Later.Click+=(s,e)=>view.Window.Close();
  view.Window.Closing+=(s,e)=>{if(installingSoftware){e.Cancel=true;closeUpdateAfterCancel=true;if(softwareDownloadStop!=null)softwareDownloadStop.Cancel();view.State.Text=UiText.T("正在取消下载…");}};
  view.Window.Closed+=(s,e)=>{if(ReferenceEquals(softwareDialog,view))softwareDialog=null;};ShowSoftwareDialog(view.Window);
 }
 void SetDownloadProgress(SoftwareUpdateView view,long done,long total){double percent=total>0?Math.Min(100,100.0*done/total):0;view.Progress.Value=percent;view.ProgressText.Text=((int)percent)+"% · "+App.FormatSize(done)+" / "+App.FormatSize(total);}
 async Task InstallSoftwareAsync(SoftwareUpdateView view){
  if(!SoftwareAvailable||installingSoftware||busy)return;installingSoftware=true;closeUpdateAfterCancel=false;var stop=new CancellationTokenSource();softwareDownloadStop=stop;var release=softwareRelease;view.Install.IsEnabled=false;view.Later.Visibility=Visibility.Collapsed;view.Cancel.Visibility=Visibility.Visible;view.Progress.Visibility=Visibility.Visible;view.ProgressText.Visibility=view.State.Visibility=Visibility.Visible;view.Progress.Value=0;view.State.Text=UiText.T("正在下载更新…");RefreshAboutUpdateState();
  try{bool installed=UpdateLauncher.Installed(AppDomain.CurrentDomain.BaseDirectory);var asset=release.Assets.Single(a=>a.Name.EndsWith(installed?"-setup.exe":"-portable.zip",StringComparison.Ordinal));SetDownloadProgress(view,0,asset.Size);string session=SoftwareSession();Action<long,long> progressUpdate=(done,total)=>Window.Dispatcher.BeginInvoke(new Action(()=>SetDownloadProgress(view,done,asset.Size)));
   if(installed&&UpdateServiceControl.Available(AppDomain.CurrentDomain.BaseDirectory)){view.State.Text=UiText.T("正在通过更新服务下载…");var prepared=await Task.Run(()=>ServiceUpdateClient.Prepare(release,stop.Token,progressUpdate));SetDownloadProgress(view,asset.Size,asset.Size);view.State.Text=UiText.T("校验完成，正在准备更新…");await ServiceUpdateClient.Handoff(release,prepared,session,stop.Token);}
   else{string package=await Task.Run(()=>SoftwareDownload(asset,session,stop.Token,progressUpdate));SetDownloadProgress(view,asset.Size,asset.Size);view.State.Text=UiText.T("校验完成，正在准备更新…");await UpdateLauncher.Launch(release,installed,package,session,stop.Token);}installingSoftware=false;view.Window.Close();Window.Close();}
  catch(Exception ex){view.State.Text=UiText.T(stop.IsCancellationRequested?"软件更新已取消":"软件更新未应用："+ex.Message);Notify(view.State.Text);}
  finally{installingSoftware=false;if(ReferenceEquals(softwareDownloadStop,stop))softwareDownloadStop=null;stop.Dispose();view.Install.IsEnabled=true;view.Later.Visibility=Visibility.Visible;view.Cancel.Visibility=Visibility.Collapsed;RefreshAboutUpdateState();if(closeUpdateAfterCancel)view.Window.Close();}
 }
}
}
