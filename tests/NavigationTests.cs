using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 async Task WithSettings(CleanerSettings options,DiscoveryResult discovery,Action<FrameworkElement> ready,Action<CleanerSettings> persist=null,Action<string> location=null){
  OpenSettings(options,discovery,persist,location);await Window.Dispatcher.InvokeAsync(new Action(()=>Window.UpdateLayout()),System.Windows.Threading.DispatcherPriority.ContextIdle);
  try{ready(settingsView.Root);}finally{while(pages.Count>0){int count=pages.Count;BackPage();if(pages.Count==count)throw new Exception("Test navigation could not leave an active settings operation.");}}
 }
 async Task VerifyNavigation(string output,Action<bool,string> check){
  UiText.SetLanguage("zh-CN");Apply(engine.Scan(CancellationToken.None,null));SetMode(true);var chosen=leaves.First(n=>n.Selectable);chosen.Selected=true;var originalEngine=engine;var originalLeaves=leaves;var before=Chosen().ToArray();int windows=Application.Current.Windows.Count;
  check(Window.Title=="Clarity"&&Get<TextBlock>("VersionLabel")==null&&System.Windows.Shell.WindowChrome.GetWindowChrome(Window)!=null,"Clarity has a unified custom caption and shows no version outside About");
  var options=new CleanerSettings{RecoveryDirectory=Path.Combine(output,"navigation-backups")};OpenSettings(options,null,s=>{});
  await Window.Dispatcher.InvokeAsync(new Action(()=>Window.UpdateLayout()),System.Windows.Threading.DispatcherPriority.ContextIdle);
  var root=settingsView.Root;var tabs=(TabControl)root.FindName("RulesTabs");check(Application.Current.Windows.Count==windows&&Get<Grid>("MainLayout").Visibility==Visibility.Collapsed&&Get<ContentControl>("PageHost").Content==root,"Settings navigates inside the existing window without creating another window");
  var leave=settingsView.TryLeave;settingsView.TryLeave=()=>false;BackPage();check(pages.Count==1&&!NavigationCanClose()&&Get<ContentControl>("PageHost").Content==root,"An active settings operation blocks both leaving the page and closing the host");settingsView.TryLeave=leave;
  tabs.SelectedItem=root.FindName("BackupTab");string draft=Path.Combine(output,"draft-backup-folder");((TextBox)root.FindName("RecoveryPath")).Text=draft;
  var savedProject=OpenProject;int opened=0;OpenProject=s=>{check(s=="https://github.com/CrazyHandin/Clarity","The source link uses the configured public project URL");opened++;};
  try{
   ((Button)root.FindName("AboutButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var about=pages.Last();App.Render(Window,Path.Combine(output,"about-chinese.png"));
   check(Application.Current.Windows.Count==windows&&((TextBlock)about.FindName("AboutVersion")).Text==ReleaseVersion.Display&&((TextBlock)about.FindName("BetaNotice")).Visibility==(ReleaseVersion.IsBeta?Visibility.Visible:Visibility.Collapsed),"About is inline and shows a Beta warning only for Beta releases");
   check(((TextBlock)about.FindName("CopyrightNotice")).Text=="Clarity © 2026 CraZyX"&&!LanguageLabels(about).Any(t=>t.Text.Contains("by CraZyX")),"About shows the requested copyright without the former byline");
   ((Button)about.FindName("TermsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var terms=((TextBox)pages.Last().FindName("DocumentText")).Text;check(terms.Contains("备份、恢复与系统清理")&&terms.Contains("Copyright (c) 2026 CraZyX")&&terms.Contains("Permission is hereby granted")&&!terms.Contains("Shittim")&&!terms.Contains("Spine"),"Terms are adapted to Clarity and contain the full MIT license without reference-software clauses");App.Render(Window,Path.Combine(output,"terms-chinese.png"));BackPage();
   var versionLine=(StackPanel)about.FindName("AboutVersionLine");var version=(TextBlock)about.FindName("AboutVersion");var build=(TextBlock)about.FindName("AboutBuild");check(about.FindName("CopyVersionButton")==null&&versionLine.Orientation==Orientation.Horizontal&&Math.Abs(version.TranslatePoint(new Point(),versionLine).Y-build.TranslatePoint(new Point(),versionLine).Y)<1,"About removes the copy action and aligns release and internal build on the same line");
   ((Button)about.FindName("UsageButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var document=pages.Last();check(((TextBox)document.FindName("DocumentText")).Text.StartsWith("Clarity 使用说明"),"The usage entry opens the embedded Chinese guide without external files");App.Render(Window,Path.Combine(output,"guide-chinese.png"));BackPage();
   ((Button)about.FindName("ThirdPartyButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(((TextBox)pages.Last().FindName("DocumentText")).Text.Contains("CC BY-SA 4.0"),"Third-party attribution and licensing remain accessible in the application");BackPage();
   ((Button)about.FindName("SourceButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(opened==1,"Source navigation invokes only the injected opener during testing");BackPage();
   check(Get<ContentControl>("PageHost").Content==root&&tabs.SelectedItem==root.FindName("BackupTab")&&((TextBox)root.FindName("RecoveryPath")).Text==draft,"Returning from About preserves the selected settings section and unsaved location draft");BackPage();
   check(pages.Count==0&&ReferenceEquals(engine,originalEngine)&&ReferenceEquals(leaves,originalLeaves)&&phase=="results"&&hasScanned&&Chosen().SequenceEqual(before),"Returning to cleanup preserves scan results and exact selected findings without rescanning");
   UiMotion.SetEnabled(Window,true);OpenSettings(options,null,s=>{});var navigation=Get<Grid>("NavigationSurface");
   check(Get<Image>("NavigationSnapshot")==null&&UiMotion.GetNavigating(navigation)&&settingsView.Root.Opacity==1,"Navigation uses one host transition without an outgoing snapshot or a second page fade");
   await Task.Delay(70);var offset=(System.Windows.Media.TranslateTransform)navigation.RenderTransform;check(offset.X>0&&offset.X<8&&navigation.Opacity==1,"The single navigation slide interpolates while all content stays fully readable");
   var animatedTabs=(TabControl)settingsView.Root.FindName("RulesTabs");animatedTabs.SelectedIndex=1;animatedTabs.SelectedIndex=0;
   await Window.Dispatcher.InvokeAsync(new Action(()=>Window.UpdateLayout()),System.Windows.Threading.DispatcherPriority.ContextIdle);
   var content=(FrameworkElement)animatedTabs.Template.FindName("PART_SelectedContentHost",animatedTabs);check(content.Opacity==1&&!content.HasAnimatedProperties&&content.RenderTransform==System.Windows.Media.Transform.Identity,"Settings content does not start a second animation while its navigation host is moving");
   OpenAbout();BackPage();BackPage();await Task.Delay(290);
   check(Get<Grid>("MainLayout").Opacity==1&&Get<Grid>("MainLayout").Visibility==Visibility.Visible&&Application.Current.Windows.Count==windows&&Chosen().SequenceEqual(before),"Rapid animated navigation settles on the correct page without losing selection");
   check(!UiMotion.GetNavigating(navigation)&&!((System.Windows.Media.TranslateTransform)navigation.RenderTransform).HasAnimatedProperties&&((System.Windows.Media.TranslateTransform)navigation.RenderTransform).X==0,"Replaced navigation transitions release their clocks and active state");
   UiMotion.SetEnabled(Window,false);OpenSettings(options,null,s=>{});OpenAbout();BackPage();BackPage();check(!UiMotion.GetNavigating(navigation)&&navigation.Opacity==1&&((System.Windows.Media.TranslateTransform)navigation.RenderTransform).X==0,"Reduced-motion navigation switches synchronously with no transition clock");
  }finally{OpenProject=savedProject;UiMotion.SetEnabled(Window,false);while(pages.Count>0)BackPage();}
  UiText.SetLanguage("en");search.Text="Media cache";check(VisibleLeaves().Count>0&&VisibleLeaves().All(n=>n.Title=="媒体缓存"),"English item searches match displayed translations while preserving canonical rule names");search.Text="";UiText.SetLanguage("zh-CN");ShowReady();
 }
}
}
