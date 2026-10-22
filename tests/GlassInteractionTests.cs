using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ClearSpace {
public sealed partial class Controller {
 static byte PanelAlpha(FrameworkElement panel,int x,int y){var image=new RenderTargetBitmap((int)Math.Ceiling(panel.ActualWidth),(int)Math.Ceiling(panel.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(panel);var pixel=new byte[4];image.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);return pixel[3];}
 static IEnumerable<T> VisualChildren<T>(DependencyObject root)where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T)yield return (T)child;foreach(var nested in VisualChildren<T>(child))yield return nested;}}
 async Task VerifyGlassInteractions(string output,Action<bool,string> check){
  string themeBefore=UiTheme.Light?"light":"dark",modeBefore=UiGlass.GetMode(Window);bool enabledBefore=UiGlass.GetEnabled(Window),quickBefore=quickScan;var engineBefore=engine;int saves=0;
  try{
   ShowReady();UiGlass.SetEnabled(Window,true);UiGlass.SetMode(Window,"internal");
   OpenSettings(new CleanerSettings(),null,s=>saves++);var root=settingsView.Root;var tabs=(TabControl)root.FindName("RulesTabs");Window.UpdateLayout();
   var tab=(TabItem)root.FindName("GeneralTab");tab.ApplyTemplate();var tabSurface=(GlassPanel)tab.Template.FindName("TabSurface",tab);
   check(PanelAlpha(tabSurface,0,(int)tabSurface.ActualHeight-1)==0&&PanelAlpha(tabSurface,(int)tabSurface.ActualWidth/2,(int)tabSurface.ActualHeight/2)>0,"The selected settings tab has a transparent rounded lower corner with no square backing");
   foreach(string name in new[]{"LanguageChoice","GlassModeChoice"}){
    var choice=(ComboBox)root.FindName(name);int selected=choice.SelectedIndex,before=saves;choice.Focus();int forwarded=0;var scroller=VisualChildren<ScrollViewer>(tabs).First();MouseWheelEventHandler observer=(sender,args)=>forwarded++;scroller.AddHandler(UIElement.MouseWheelEvent,observer,true);
    try{var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent};choice.RaiseEvent(wheel);check(wheel.Handled&&choice.SelectedIndex==selected&&saves==before&&forwarded==1,"Scrolling over "+name+" scrolls its page without selecting or saving another value");}finally{scroller.RemoveHandler(UIElement.MouseWheelEvent,observer);}
   }
   var language=(ComboBox)root.FindName("LanguageChoice");language.BringIntoView();Window.UpdateLayout();int languageBefore=language.SelectedIndex,savesBefore=saves;language.IsDropDownOpen=true;await Task.Delay(80);var openWheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent};language.RaiseEvent(openWheel);check(openWheel.Handled&&language.IsDropDownOpen&&language.SelectedIndex==languageBefore&&saves==savesBefore,"An explicitly opened dropdown scrolls its list without changing the saved selection");language.IsDropDownOpen=false;
   var holder=(Panel)language.Parent;var longList=new ComboBox{ItemsSource=Enumerable.Range(0,30).Select(i=>"Choice "+i).ToArray(),SelectedIndex=0,Width=270};holder.Children.Insert(0,longList);
   try{longList.BringIntoView();Window.UpdateLayout();longList.IsDropDownOpen=true;await Task.Delay(80);var popup=(Popup)longList.Template.FindName("PART_Popup",longList);var scroll=VisualChildren<ScrollViewer>(popup.Child).First();longList.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});Window.UpdateLayout();check(scroll.VerticalOffset>0&&longList.SelectedIndex==0,"A long open settings list actually scrolls while preserving its current value");}finally{longList.IsDropDownOpen=false;holder.Children.Remove(longList);}
   var isolated=new ComboBox{ItemsSource=new[]{"a","b"},SelectedIndex=0};var outsideWheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent};isolated.RaiseEvent(outsideWheel);check(!outsideWheel.Handled,"The settings wheel guard does not change unrelated selectors");
   var theme=(ThemeSelector)root.FindName("ThemeChoice");theme.Value="light";check(saves==1,"Explicit theme segment selection still saves the selected theme");
   VisualChildren<ScrollViewer>(tabs).First().ScrollToTop();foreach(string appearance in new[]{"light","dark"}){theme.Value=appearance;UiTheme.Set(appearance,false);Get<ThemeSwitchMark>("ThemeMark").Show(UiTheme.Light,false);Window.UpdateLayout();App.Render(Window,Path.Combine(output,"fixed-settings-"+appearance+".png"));}
   BackPage();
   string fixture=Path.Combine(output,"glass-result-fixture");var safe=new Rule("glass-safe","资源缓存","软件缓存","Generated UI fixture","Rebuildable",new[]{Path.Combine(fixture,"Cache")},7,true){Software="Visual Studio Code",Part="资源缓存"};
   var manual=new Rule("glass-manual","媒体缓存","软件缓存","Generated UI fixture","Requires selection",new[]{Path.Combine(fixture,"Media")},7,false){Software="Visual Studio Code",Part="媒体缓存"};
   var unknown=HeuristicScanner.CacheRule("glass-unknown",Path.Combine(fixture,"Unknown","Cache"),"Fixture App",new string[0]);var rules=new List<Rule>{safe,manual,unknown};
   foreach(var rule in rules){Directory.CreateDirectory(rule.Roots[0]);string file=Path.Combine(rule.Roots[0],"old.dat");File.WriteAllText(file,"Generated glass regression fixture");File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddDays(-30));}
   engine=new Engine(rules);engine.IsRunning=owners=>false;quickScan=false;Apply(engine.Scan(CancellationToken.None,null));SetMode(true);foreach(var node in treeNodes)ExpandAll(node,true);Window.UpdateLayout();int chosen=Chosen().Count;
   foreach(string appearance in new[]{"light","dark"})foreach(string mode in new[]{"internal","full"}){
    UiTheme.Set(appearance,false);Get<ThemeSwitchMark>("ThemeMark").Show(UiTheme.Light,false);UiGlass.SetMode(Window,mode);Window.UpdateLayout();await Window.Dispatcher.InvokeAsync(new Action(()=>{}),System.Windows.Threading.DispatcherPriority.ContextIdle);
    tree.ApplyTemplate();var surface=(Border)tree.Template.FindName("TreeSurface",tree);check(((SolidColorBrush)surface.Background).Color.A==0,"The result-list background exposes the glass material in "+appearance+" / "+mode);
    var row=VisualChildren<TreeViewItem>(tree).First(item=>item.DataContext is CleanNode&&((CleanNode)item.DataContext).IsLeaf);row.ApplyTemplate();var material=(GlassPanel)row.Template.FindName("HeaderSurface",row);
    check(material!=null&&material.CornerRadius.BottomLeft>0&&((SolidColorBrush)material.Background).Color.A>0&&Chosen().Count==chosen,"Cleanup leaves receive their own rounded glass surface without changing selection in "+appearance+" / "+mode);
    App.Render(Window,Path.Combine(output,"fixed-results-"+appearance+"-"+mode+".png"));
   }
   UiGlass.SetEnabled(Window,false);Window.UpdateLayout();check(((SolidColorBrush)((Border)tree.Template.FindName("TreeSurface",tree)).Background).Color.A==255,"Disabling glass restores the normal solid result-list background");
  }finally{while(pages.Count>0)BackPage();engine=engineBefore;quickScan=quickBefore;UiTheme.Set(themeBefore,false);UiGlass.SetMode(Window,modeBefore);UiGlass.SetEnabled(Window,enabledBefore);ShowReady();}
 }
}
}
