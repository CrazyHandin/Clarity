using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifyUpdateUi(string output,Action<bool,string> check){
  ShowReady();UiText.SetLanguage("zh-CN");check(Get<Button>("ExportButton")==null&&scanMode.Items[0].ToString()=="快速扫描"&&scanMode.Items[1].ToString()=="常规扫描","The toolbar omits log export and scan modes have concise names");
  var loader=SoftwareCheck;var previous=softwareRelease;SoftwareCheck=token=>new SoftwareRelease{Version=ReleaseVersion.Display,Build=ReleaseVersion.Build+1,Notes=""};
  try{
   await CheckSoftwareUpdatesAsync(false);check(Get<FrameworkElement>("RuleCenterBadge").Visibility==Visibility.Visible&&Get<Border>("ToastFrame").IsHitTestVisible&&Get<TextBlock>("ToastCountdown").Text.Contains("秒后自动关闭"),"Startup-style update detection shows a red badge and a clickable countdown notification");
   OpenSettings(new CleanerSettings(),null,s=>{});var settingsRoot=settingsView.Root;check(((FrameworkElement)settingsRoot.FindName("AboutBadge")).Visibility==Visibility.Visible&&((Button)settingsRoot.FindName("OpenHistory"))!=null,"Settings exposes cleanup history and the About update badge");
   ((TabControl)settingsRoot.FindName("RulesTabs")).SelectedItem=settingsRoot.FindName("ApplicationsTab");await Task.Delay(60);UiMotion.SetEnabled(Window,true);var expander=(Expander)settingsRoot.FindName("PortableExpander");expander.IsExpanded=true;await Task.Delay(100);var body=(FrameworkElement)expander.Template.FindName("ExpandBody",expander);check(body.ActualHeight>0,"Portable directory expansion reveals content during its height animation");await Task.Delay(180);expander.IsExpanded=false;await Task.Delay(280);check(body.ActualHeight==0,"Portable directory collapse settles without an empty gap");UiMotion.SetEnabled(Window,false);
   ((TabControl)settingsRoot.FindName("RulesTabs")).SelectedItem=settingsRoot.FindName("RulesTab");((Button)settingsRoot.FindName("Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(((SolidColorBrush)Get<Border>("ToastFrame").BorderBrush).Color==Color.FromRgb(86,201,139),"Saving rule settings uses the green success notification");((Button)settingsRoot.FindName("JsonGuide")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(((TextBox)pages.Last().FindName("DocumentText")).Text.Contains("\"Schema\": 1"),"Rule management opens the embedded JSON authoring guide");BackPage();OpenAbout();var about=pages.Last();check(((TextBlock)about.FindName("AboutBuild")).Text.Contains(ReleaseVersion.Build.ToString())&&((FrameworkElement)about.FindName("AboutUpdateBadge")).Visibility==Visibility.Visible,"About displays the internal build number and a software update badge");
   check(((TextBlock)about.FindName("ReleaseSummary")).Text.StartsWith("新增"),"About includes a concise summary of the new version");
   await CheckSoftwareUpdatesAsync(false);Get<Border>("ToastFrame").RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});check(ReferenceEquals(pages.Last(),softwarePage)&&((Button)softwarePage.FindName("InstallSoftware")).IsEnabled,"Clicking an update notification enters the one-click software update page");App.Render(Window,Path.Combine(output,"software-update.png"));
   UiText.SetLanguage("en");check(((Button)softwarePage.FindName("CheckSoftware")).Content.ToString()=="Check for software updates"&&((TextBlock)softwarePage.FindName("SoftwareNotes")).Text==ReleaseHighlights.English,"The software update page follows the interface language");App.Render(Window,Path.Combine(output,"software-update-english.png"));
  }finally{SoftwareCheck=loader;softwareRelease=previous;RefreshUpdateBadges();while(pages.Count>0)BackPage();softwarePage=null;UiText.SetLanguage("zh-CN");UiMotion.SetEnabled(Window,false);}
 }
}
}
