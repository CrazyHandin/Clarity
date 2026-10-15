using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 readonly List<FrameworkElement> pages=new List<FrameworkElement>();SettingsView settingsView;
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
 void SettingsChanged(){UpdateRecoveryNote();UpdateDisk();UiText.SetLanguage(settings.Language);}
 void OpenSettings(CleanerSettings options=null,DiscoveryResult discovery=null,Action<CleanerSettings> persist=null,Action<string> openLocation=null){
  if(busy||pages.Count>0)return;
  settingsView=RuleCenter.Create(Window,options??settings,options==null?LastDiscovery:discovery,options==null?(Action)SettingsChanged:()=>UiText.SetLanguage(options.Language),BackPage,OpenAbout,persist,openLocation,()=>ElevateAction(),Notify,()=>PushPage(DocumentPage.JsonRules(BackPage)),NotifySuccess);
  PushPage(settingsView.Root);RefreshUpdateBadges();
 }
 void PushPage(FrameworkElement page){pages.Add(page);ShowNavigationPage(page);}
 void ShowNavigationPage(FrameworkElement page){
  Get<Grid>("MainLayout").Visibility=Visibility.Collapsed;var host=Get<ContentControl>("PageHost");host.Visibility=Visibility.Visible;host.Content=page;Window.UpdateLayout();UiText.Attach(Window);UiMotion.Reveal(page);
 }
 void BackPage(){
  if(pages.Count==0)return;if(pages.Count==1&&settingsView!=null&&!settingsView.TryLeave())return;
  var current=pages[pages.Count-1];UiText.Forget(Window,current);pages.RemoveAt(pages.Count-1);
  if(pages.Count>0){ShowNavigationPage(pages[pages.Count-1]);return;}
  var host=Get<ContentControl>("PageHost");host.Content=null;host.Visibility=Visibility.Collapsed;settingsView=null;Get<Grid>("MainLayout").Visibility=Visibility.Visible;UiText.Attach(Window);UiMotion.Reveal(Get<Grid>("MainLayout"));
 }
 void OpenAbout(){PushPage(About.Create(Window,BackPage,()=>PushPage(DocumentPage.Create(false,BackPage)),()=>PushPage(DocumentPage.Create(true,BackPage)),()=>PushPage(DocumentPage.Terms(BackPage)),OpenProject,()=>{LastSoftwareOperation=CheckSoftwareUpdatesAsync(true);},OpenReleaseNotes));RefreshUpdateBadges();RefreshAboutUpdateState();}
 bool NavigationCanClose(){return settingsView==null||settingsView.TryLeave();}
}
}
