using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace ClearSpace {
public sealed partial class Controller {
 static CheckBox ScopeCard(DependencyObject node,GroupOption group){var box=node as CheckBox;if(box!=null&&ReferenceEquals(box.DataContext,group))return box;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var result=ScopeCard(VisualTreeHelper.GetChild(node,i),group);if(result!=null)return result;}return null;}
 static bool InsideControl(DependencyObject node,DependencyObject control){while(node!=null){if(node==control)return true;var content=node as FrameworkContentElement;node=content!=null?content.Parent:VisualTreeHelper.GetParent(node);}return false;}
 async Task VerifyInteractions(string output,Action<bool,string> check){
  ShowReady();Window.UpdateLayout();var card=ScopeCard(Get<ItemsControl>("ScopeList"),groups[0]);check(card!=null,"Scope selection uses a single full-card check control");
  var surface=(Border)card.Template.FindName("CardSurface",card);var points=new[]{new Point(3,3),new Point(surface.ActualWidth-3,3),new Point(4,surface.ActualHeight/2),new Point(surface.ActualWidth/2,surface.ActualHeight/2),new Point(surface.ActualWidth-4,surface.ActualHeight-4)};
  foreach(var point in points){var position=surface.TransformToAncestor(Window).Transform(point);check(InsideControl(Window.InputHitTest(position)as DependencyObject,card),"Visible scope card captures input at "+point);}
  bool selected=groups[0].Selected;typeof(ToggleButton).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(card,null);check(groups[0].Selected!=selected,"Native card clicking toggles its bound group exactly once");typeof(ToggleButton).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(card,null);check(groups[0].Selected==selected,"A second card click restores the scope selection");
  int rules=engine.Rules.Count;var startup=new Controller();startup.Window.ShowInTaskbar=false;startup.Window.ShowActivated=false;startup.Window.WindowStartupLocation=WindowStartupLocation.Manual;startup.Window.Left=-10000;startup.Window.Top=-10000;UiMotion.SetEnabled(startup.Window,true);
  try{startup.PrepareStartup();startup.Window.Show();await Task.Delay(140);var main=startup.Get<Grid>("MainLayout");check(main.Opacity>0&&main.Opacity<1,"Startup animation is visibly in progress after the first rendered frame");App.Render(startup.Window,Path.Combine(output,"startup-motion.png"));await Task.Delay(700);check(main.Opacity==1&&startup.phase=="ready"&&!startup.hasScanned&&startup.engine.Rules.Count==0&&engine.Rules.Count==rules,"Startup animation settles without scanning or changing rules");}finally{startup.Window.Close();}
  UiMotion.SetEnabled(Window,true);var savedResult=lastResult;lastResult=new CleanResult();ShowComplete();await Task.Delay(140);var completion=Get<Viewbox>("CompletionIcon");check(completion.Opacity>0&&completion.Opacity<1,"The cleanup completion icon animates when its page becomes visible");App.Render(Window,Path.Combine(output,"completion-motion.png"));await Task.Delay(700);check(completion.Opacity==1&&Get<System.Windows.Shapes.Path>("CompletionCheck").StrokeDashOffset==0&&phase=="complete","The completion icon settles with a complete checkmark and unchanged completion state");ShowReady();lastResult=savedResult;UiMotion.SetEnabled(Window,false);
  var savedFactory=RuleFactory;var savedMode=scanMode.SelectedIndex;var started=new ManualResetEventSlim();var release=new ManualResetEventSlim();
  try{scanMode.SelectedIndex=1;RuleFactory=()=>{started.Set();if(!release.Wait(5000))throw new Exception("Fixture release timeout");return new System.Collections.Generic.List<Rule>();};var task=ScanAsync();check(await Task.Run(()=>started.Wait(5000)),"Cancellation fixture entered the scan factory");cancellation.Cancel();release.Set();await task;check(!busy&&!hasScanned&&phase=="ready"&&Get<TextBlock>("ToastText").Text.Contains("扫描已取消"),"Cancelling a scan returns to preparation and shows the top notification");App.Render(Window,Path.Combine(output,"scan-cancel-notice.png"));}
  finally{release.Set();RuleFactory=savedFactory;scanMode.SelectedIndex=savedMode;started.Dispose();release.Dispose();}
  UiMotion.SetEnabled(Window,true);Notify("Fixture notification one");await Task.Delay(1000);Notify("Fixture notification two");await Task.Delay(1200);check(Get<Border>("ToastFrame").Visibility==Visibility.Visible&&Get<TextBlock>("ToastText").Text=="Fixture notification two","A new notice replaces its predecessor and resets the two-second lifetime");App.Render(Window,Path.Combine(output,"top-notice.png"));await Task.Delay(900);Notify("Fixture notification three");await Task.Delay(350);check(Get<Border>("ToastFrame").Visibility==Visibility.Visible&&Get<TextBlock>("ToastText").Text=="Fixture notification three","Replacing a dismissing notice keeps the new notice visible");await Task.Delay(2150);check(Get<Border>("ToastFrame").Visibility==Visibility.Collapsed,"The latest top notification disappears after its display time and fade");UiMotion.SetEnabled(Window,false);
  var options=new CleanerSettings();await WithSettings(options,null,w=>{var choice=(ComboBox)w.FindName("LanguageChoice");choice.SelectedIndex=1;check(Get<Border>("ToastFrame").Visibility==Visibility.Visible&&Get<TextBlock>("ToastText").Text=="Language preference saved.","Saved settings use the shared localized top notice");},s=>{});UiText.SetLanguage("zh-CN");
 }
}
}
