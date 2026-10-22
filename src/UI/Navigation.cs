using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 readonly List<FrameworkElement> pages=new List<FrameworkElement>();SettingsView settingsView,rulePackView;PageTransition pageTransition;
 readonly List<FrameworkElement> retiredPages=new List<FrameworkElement>();bool cleanupQueued;
 public Action<string> OpenProject=url=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
 void InitializeChrome(){
  UiMotion.SetEnabled(Window,SystemParameters.ClientAreaAnimation);
  Get<Image>("CaptionLogo").Source=Window.Icon;
  Get<Button>("MinimizeWindow").Click+=(s,e)=>SystemCommands.MinimizeWindow(Window);
  Get<Button>("MaximizeWindow").Click+=(s,e)=>{if(Window.WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(Window);else SystemCommands.MaximizeWindow(Window);};
  Get<Button>("CloseWindow").Click+=(s,e)=>SystemCommands.CloseWindow(Window);
  UiMotion.CloseOnRequest(Window,Get<Grid>("WindowRoot"));
  Window.StateChanged+=(s,e)=>{Get<Button>("MaximizeWindow").Content=Window.WindowState==WindowState.Maximized?"\uE923":"\uE922";Get<Button>("MaximizeWindow").ToolTip=Window.WindowState==WindowState.Maximized?"还原窗口":"最大化";};
 }
 void SettingsChanged(){UpdateRecoveryNote();UpdateDisk();if(UiText.Language!=UiText.Normalize(settings.Language))UiText.SetLanguage(settings.Language);RefreshTheme(UiMotion.GetEnabled(Window));}
 void OpenSettings(CleanerSettings options=null,DiscoveryResult discovery=null,Action<CleanerSettings> persist=null,Action<string> openLocation=null){
  if(busy||pages.Count>0)return;
  settingsView=RuleCenter.Create(Window,options??settings,options==null?LastDiscovery:discovery,options==null?(Action)SettingsChanged:()=>UiText.SetLanguage(options.Language),BackPage,OpenAbout,persist,openLocation,()=>ElevateAction(),Notify,()=>PushPage(DocumentPage.JsonRules(BackPage)),NotifySuccess,()=>OpenRulePacks(options??settings,options==null?LastDiscovery:discovery,persist));
  PushPage(settingsView.Root);RefreshSoftwareUpdateState();
 }
 void OpenRulePacks(CleanerSettings options,DiscoveryResult discovery,Action<CleanerSettings> persist=null){if(settingsView!=null&&!settingsView.TryLeave())return;rulePackView=RulePackCenter.Create(Window,options,discovery,BackPage,SettingsChanged,Notify,NotifySuccess,null,null,persist);PushPage(rulePackView.Root);}
 void PushPage(FrameworkElement page){pages.Add(page);ShowNavigationPage(page);}
 void NavigateTo(FrameworkElement page,Action show,bool back=false){
  if(pageTransition==null)pageTransition=new PageTransition(Get<Grid>("NavigationSurface"),NavigationSettled);
  pageTransition.Switch(page,()=>{show();UiText.AttachPage(Window,page);},back);
 }
 void NavigationSettled(){
  if(cleanupQueued||retiredPages.Count==0)return;cleanupQueued=true;Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{
   cleanupQueued=false;if(UiMotion.GetNavigating(Get<Grid>("NavigationSurface")))return;
   foreach(var page in retiredPages)UiText.Forget(Window,page);retiredPages.Clear();
  }));
 }
 void ShowNavigationPage(FrameworkElement page,bool back=false){
  NavigateTo(page,()=>{Get<Grid>("MainLayout").Visibility=Visibility.Hidden;var host=Get<ContentControl>("PageHost");host.Visibility=Visibility.Visible;host.Content=page;},back);
 }
 void BackPage(){
  if(pages.Count==0)return;if(rulePackView!=null&&pages[pages.Count-1]==rulePackView.Root){if(!rulePackView.TryLeave())return;rulePackView=null;}if(pages.Count==1&&settingsView!=null&&!settingsView.TryLeave())return;
  var current=pages[pages.Count-1];retiredPages.Add(current);pages.RemoveAt(pages.Count-1);
  if(pages.Count>0){if(settingsView!=null&&pages[pages.Count-1]==settingsView.Root&&settingsView.Refresh!=null)settingsView.Refresh();ShowNavigationPage(pages[pages.Count-1],true);return;}
  NavigateTo(Get<Grid>("MainLayout"),()=>{var host=Get<ContentControl>("PageHost");host.Content=null;host.Visibility=Visibility.Collapsed;settingsView=null;Get<Grid>("MainLayout").Visibility=Visibility.Visible;},true);
 }
 void OpenAbout(){PushPage(About.Create(Window,BackPage,()=>PushPage(DocumentPage.Create(false,BackPage)),()=>PushPage(DocumentPage.Create(true,BackPage)),()=>PushPage(DocumentPage.Terms(BackPage)),OpenProject,SoftwareUpdateAction,OpenReleaseNotes));RefreshSoftwareUpdateState();}
 bool NavigationCanClose(){return (rulePackView==null||rulePackView.TryLeave())&&(settingsView==null||settingsView.TryLeave());}
}
}
