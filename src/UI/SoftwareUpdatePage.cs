using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 SoftwareRelease softwareRelease;bool checkingSoftware,installingSoftware;CancellationTokenSource softwareCheckStop,softwareDownloadStop;FrameworkElement softwarePage;string softwareMessage="";
 public Func<CancellationToken,SoftwareRelease> SoftwareCheck=SoftwareUpdates.Check;
 public Task LastSoftwareOperation;
 void InitializeSoftwareUpdates(){Window.Closed+=(s,e)=>{if(softwareCheckStop!=null)softwareCheckStop.Cancel();if(softwareDownloadStop!=null)softwareDownloadStop.Cancel();};}
 public void EnableAutomaticSoftwareUpdates(){EventHandler first=null;first=(s,e)=>{Window.ContentRendered-=first;LastSoftwareOperation=CheckSoftwareUpdatesAsync(false);};Window.ContentRendered+=first;}
 bool SoftwareAvailable{get{return softwareRelease!=null&&softwareRelease.IsNewer(ReleaseVersion.Display,ReleaseVersion.Build);}}
 void RefreshUpdateBadges(){
  Visibility visible=SoftwareAvailable?Visibility.Visible:Visibility.Collapsed;Get<FrameworkElement>("RuleCenterBadge").Visibility=visible;
  if(settingsView!=null){var badge=settingsView.Root.FindName("AboutBadge")as FrameworkElement;if(badge!=null)badge.Visibility=visible;}
  foreach(var page in pages){var badge=page.FindName("AboutUpdateBadge")as FrameworkElement;if(badge!=null)badge.Visibility=visible;}
 }
 public async Task CheckSoftwareUpdatesAsync(bool manual){
  if(checkingSoftware||installingSoftware)return;checkingSoftware=true;softwareMessage="正在检查软件更新…";RefreshSoftwarePage();var stop=new CancellationTokenSource();softwareCheckStop=stop;
  try{softwareRelease=await Task.Run(()=>SoftwareCheck(stop.Token));if(stop.IsCancellationRequested)return;softwareMessage=SoftwareAvailable?"发现可用更新":"当前已是最新版本";RefreshUpdateBadges();if(SoftwareAvailable)notice.Show(UiText.T("发现软件新版本：")+softwareRelease.Label,false,8,OpenSoftwareUpdates,true);else if(manual)NotifySuccess("当前已是最新版本");}
  catch(Exception ex){if(!stop.IsCancellationRequested){softwareMessage="软件更新检查失败："+ex.Message;if(manual)Notify(softwareMessage);}}
  finally{checkingSoftware=false;if(ReferenceEquals(softwareCheckStop,stop))softwareCheckStop=null;stop.Dispose();RefreshSoftwarePage();}
 }
 void OpenSoftwareUpdates(){
  if(busy){Notify("请等待当前任务结束后更新。");return;}
  if(softwarePage!=null&&pages.Contains(softwarePage)){while(pages.Last()!=softwarePage)BackPage();RefreshSoftwarePage();return;}
  var root=new Grid{Margin=new Thickness(28,20,28,24)};NameScope.SetNameScope(root,new NameScope());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());var header=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,22)};var back=new Button{Content="← 返回",Margin=new Thickness(0,0,18,0)};back.Name="UpdateBack";root.RegisterName(back.Name,back);back.Click+=(s,e)=>BackPage();header.Children.Add(back);header.Children.Add(new TextBlock{Text="软件更新",FontSize=28,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});root.Children.Add(header);
  var panel=new StackPanel{MaxWidth=820,HorizontalAlignment=HorizontalAlignment.Stretch};var scroll=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(scroll,1);root.Children.Add(scroll);
  panel.Children.Add(new Image{Source=Window.Icon,Width=64,Height=64,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,16)});panel.Children.Add(new TextBlock{Text="Clarity",FontSize=28,FontWeight=FontWeights.SemiBold});panel.Children.Add(new TextBlock{Text=UiText.T("当前版本：")+ReleaseVersion.Display+" · "+UiText.T("内部构建 ")+ReleaseVersion.Build,Foreground=App.Brush("#A7BFCD"),FontSize=15,Margin=new Thickness(0,8,0,22)});
  AddUpdateText(root,panel,"UpdateState",20);AddUpdateText(root,panel,"UpdateVersion",15);var bar=new ProgressBar{Height=9,Maximum=100,Foreground=App.Brush("#39C5BB"),Background=App.Brush("#344651"),Margin=new Thickness(0,16,0,14),Name="UpdateProgress",Visibility=Visibility.Collapsed};root.RegisterName(bar.Name,bar);panel.Children.Add(bar);AddUpdateText(root,panel,"UpdateProgressText",14);
  var actions=new WrapPanel{Margin=new Thickness(0,12,0,18)};var check=new Button{Content="检查软件更新",Name="CheckSoftware",Margin=new Thickness(0,0,12,10)};root.RegisterName(check.Name,check);check.Click+=(s,e)=>{LastSoftwareOperation=CheckSoftwareUpdatesAsync(true);};actions.Children.Add(check);var install=new Button{Content="下载并更新",Name="InstallSoftware",Background=App.Brush("#39C5BB"),Foreground=App.Brush("#083F3A"),Margin=new Thickness(0,0,12,10)};root.RegisterName(install.Name,install);install.Click+=(s,e)=>{LastSoftwareOperation=InstallSoftwareAsync();};actions.Children.Add(install);var cancel=new Button{Content="取消下载",Name="CancelSoftware",Margin=new Thickness(0,0,0,10)};root.RegisterName(cancel.Name,cancel);cancel.Click+=(s,e)=>{if(softwareDownloadStop!=null)softwareDownloadStop.Cancel();};actions.Children.Add(cancel);panel.Children.Add(actions);
  panel.Children.Add(new TextBlock{Text="更新来自 CrazyHandin/Clarity 的 GitHub Release。下载后核对 SHA-256；安装版自动升级，便携版自动替换程序文件并重新启动。",TextWrapping=TextWrapping.Wrap,Foreground=App.Brush("#9DB5C3"),FontSize=14,Margin=new Thickness(0,0,0,18)});panel.Children.Add(new TextBlock{Text="本版更新",FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,10)});AddUpdateText(root,panel,"SoftwareNotes",15);softwarePage=root;PushPage(root);RefreshSoftwarePage();if(softwareRelease==null&&!checkingSoftware)LastSoftwareOperation=CheckSoftwareUpdatesAsync(true);
 }
 static void AddUpdateText(FrameworkElement root,Panel panel,string name,int size){var text=new TextBlock{Name=name,FontSize=size,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10)};root.RegisterName(name,text);panel.Children.Add(text);}
 void RefreshSoftwarePage(){
  if(softwarePage==null)return;((TextBlock)softwarePage.FindName("UpdateState")).Text=softwareMessage;((TextBlock)softwarePage.FindName("UpdateVersion")).Text=softwareRelease==null?"":UiText.T("仓库版本：")+softwareRelease.Label;((TextBlock)softwarePage.FindName("SoftwareNotes")).Text=softwareRelease==null?"":softwareRelease.Notes;
  if(softwareRelease==null||String.IsNullOrWhiteSpace(softwareRelease.Notes))((TextBlock)softwarePage.FindName("SoftwareNotes")).Text=ReleaseHighlights.Current;
  ((Button)softwarePage.FindName("CheckSoftware")).IsEnabled=!checkingSoftware&&!installingSoftware;((Button)softwarePage.FindName("InstallSoftware")).IsEnabled=SoftwareAvailable&&!installingSoftware&&!checkingSoftware&&!busy;((Button)softwarePage.FindName("CancelSoftware")).Visibility=installingSoftware?Visibility.Visible:Visibility.Collapsed;((Button)softwarePage.FindName("UpdateBack")).IsEnabled=!installingSoftware;
 }
 async Task InstallSoftwareAsync(){
  if(!SoftwareAvailable||installingSoftware||busy)return;installingSoftware=true;var stop=new CancellationTokenSource();softwareDownloadStop=stop;var release=softwareRelease;RefreshSoftwarePage();
  try{bool installed=UpdateLauncher.Installed(AppDomain.CurrentDomain.BaseDirectory);var asset=release.Assets.Single(a=>a.Name.EndsWith(installed?"-setup.exe":"-portable.zip",StringComparison.Ordinal));string session=UpdateLauncher.NewSession();var bar=(ProgressBar)softwarePage.FindName("UpdateProgress");bar.Visibility=Visibility.Visible;bar.Value=0;softwareMessage="正在下载更新…";RefreshSoftwarePage();string package=await Task.Run(()=>SoftwareUpdates.Download(asset,session,stop.Token,(done,total)=>Window.Dispatcher.BeginInvoke(new Action(()=>{bar.Value=total>0?100.0*done/total:0;((TextBlock)softwarePage.FindName("UpdateProgressText")).Text=App.FormatSize(done)+" / "+App.FormatSize(asset.Size);})))) ;softwareMessage="校验完成，正在准备更新…";RefreshSoftwarePage();await UpdateLauncher.Launch(release,installed,package,session,stop.Token);Window.Close();}
  catch(Exception ex){softwareMessage=stop.IsCancellationRequested?"软件更新已取消":"软件更新未应用："+ex.Message;Notify(softwareMessage);}
  finally{installingSoftware=false;if(ReferenceEquals(softwareDownloadStop,stop))softwareDownloadStop=null;stop.Dispose();RefreshSoftwarePage();}
 }
}
}
