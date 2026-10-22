using System;
using System.IO;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifyAppearance(string output,Action<bool,string> check){
  ShowReady();UiText.SetLanguage("zh-CN");long unit=1024L*1024*1024;
  try{
   ShowDiskSpace(100*unit,25*unit);check(Get<ProgressBar>("DiskBar").Value==75&&Get<TextBlock>("DiskPercentText").Text=="剩余 25%"&&Get<TextBlock>("DiskText").Text.Contains("25.00 GB"),"Disk fill represents occupied space while the primary label shows the remaining space");
   ShowDiskSpace(100*unit,5*unit);check(Get<ProgressBar>("DiskBar").Value==95&&((SolidColorBrush)Get<ProgressBar>("DiskBar").Foreground).Color==Color.FromRgb(242,119,119),"A nearly full disk uses occupied capacity and a warning color");
   ShowDiskSpace(100,150);check(Get<ProgressBar>("DiskBar").Value==0,"Free capacity above total is clamped to an empty used-space bar");ShowDiskSpace(0,0);check(Get<ProgressBar>("DiskBar").Value==0&&!Double.IsNaN(Get<ProgressBar>("DiskBar").Value),"An unreadable or zero total never divides by zero");
   UiText.SetLanguage("en");ShowDiskSpace(100*unit,25*unit);check(Get<TextBlock>("DiskPercentText").Text=="Free 25%"&&Get<ProgressBar>("DiskBar").ToolTip.ToString().Contains("Free"),"English disk labels consistently identify used and free capacity");
  }finally{UiText.SetLanguage("zh-CN");UpdateDisk();}
  bool previous=UiGlass.GetEnabled(Window);string previousMode=UiGlass.GetMode(Window);var allowed=NativeGlass.Allowed;var options=new CleanerSettings();string file=Path.Combine(output,"glass-preference.json");int saves=0;bool failSave=false;
  try{
   OpenSettings(options,null,s=>{if(failSave)throw new IOException("fixture save failure");saves++;File.WriteAllText(file,new JavaScriptSerializer().Serialize(s));});
   var choice=(CheckBox)settingsView.Root.FindName("LiquidGlassChoice");var mode=(ComboBox)settingsView.Root.FindName("GlassModeChoice");check(options.LiquidGlass&&choice.IsChecked==true&&choice.Content.ToString()=="液态玻璃外观（Beta）"&&mode.SelectedIndex==1&&options.GlassMode=="internal","Liquid Glass defaults to internal glass with the requested label");
   check(new JavaScriptSerializer().Deserialize<CleanerSettings>("{\"LiquidGlass\":true}").GlassMode=="internal"&&UiGlass.NormalizeMode("invalid")=="internal","Older preferences and invalid mode values default to internal glass");
   var note=(TextBlock)settingsView.Root.FindName("GlassPerformanceNote");check(((SolidColorBrush)note.Foreground).Color==((SolidColorBrush)App.Brush("#E8BE58")).Color,"The graphics-resource notice uses the yellow warning color");
   choice.IsChecked=false;choice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!UiGlass.GetEnabled(Window)&&!new JavaScriptSerializer().Deserialize<CleanerSettings>(File.ReadAllText(file)).LiquidGlass,"Disabling glass saves the preference and immediately restores the plain interface");
   var popup=UpdateDialogs.Highlights(Window);check(!UiGlass.GetEnabled(popup),"Update dialogs honor a disabled glass preference");popup.Close();
   choice.IsChecked=true;choice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(UiGlass.GetEnabled(Window)&&saves==2&&new JavaScriptSerializer().Deserialize<CleanerSettings>(File.ReadAllText(file)).LiquidGlass,"Re-enabling glass saves without changing scans or creating a new main window");
   check(!UiGlass.GetHasBackdrop(Window)&&UiGlass.GetMode(Window)=="internal"&&((SolidColorBrush)Window.Background).Color.A==255,"Default internal glass keeps the desktop behind an opaque window floor");
   mode.SelectedIndex=0;check(UiGlass.GetMode(Window)=="full"&&new JavaScriptSerializer().Deserialize<CleanerSettings>(File.ReadAllText(file)).GlassMode=="full"&&saves==3,"Full transparency can be selected and saved independently from enabling glass");
   bool native=UiGlass.GetHasBackdrop(Window);check(!native||((SolidColorBrush)Window.Background).Color.A==0&&System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(Window).Handle).CompositionTarget.BackgroundColor.A==0,"Native glass clears both the window and composition-target backgrounds");
   NativeGlass.Allowed=()=>false;UiGlass.SetEnabled(Window,true);check(!UiGlass.GetHasBackdrop(Window)&&((SolidColorBrush)Window.Background).Color.A==255,"Unavailable composition restores an opaque readable window instead of a black or invisible surface");NativeGlass.Allowed=allowed;UiGlass.SetEnabled(Window,true);check(UiGlass.GetHasBackdrop(Window)==native&&Get<Grid>("WindowRoot").Opacity==1,"Restoring compositor support keeps window content fully opaque while only materials become translucent");
   await Window.Dispatcher.InvokeAsync(new Action(()=>Window.UpdateLayout()),System.Windows.Threading.DispatcherPriority.ContextIdle);
   check(Get<Grid>("WindowRoot")is GlassBackdrop&&Get<Border>("HeroPanel")is GlassPanel,"The main interface uses the shared translucent material over the live native backdrop");
   popup=UpdateDialogs.Highlights(Window);check(popup.FindName("DialogSurface")is GlassPanel&&UiGlass.GetEnabled(popup)&&UiGlass.GetMode(popup)=="full","Version and software update dialogs inherit full transparency");popup.Close();
   mode.SelectedIndex=1;check(!UiGlass.GetHasBackdrop(Window)&&((SolidColorBrush)Window.Background).Color.A==255&&saves==4,"Switching back to internal glass removes the desktop backdrop immediately");
   failSave=true;mode.SelectedIndex=0;check(mode.SelectedIndex==1&&options.GlassMode=="internal"&&UiGlass.GetMode(Window)=="internal"&&!UiGlass.GetHasBackdrop(Window),"A failed mode save restores both the selector and the active appearance");failSave=false;
   popup=UpdateDialogs.Highlights(Window);popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=popup.Top=-12000;popup.ShowActivated=false;UiMotion.SetEnabled(popup,false);popup.Show();popup.UpdateLayout();check(UiGlass.GetMode(popup)=="internal"&&!UiGlass.GetHasBackdrop(popup)&&DialogPixelAlpha(popup,200,180)==255,"Internal-glass dialogs keep an opaque interior without showing windows or the desktop behind them");popup.Close();
   string themeBefore=UiTheme.Light?"light":"dark";
   try{foreach(string theme in new[]{"dark","light"}){
    UiTheme.Set(theme,false);UiGlass.SetMode(Window,"internal");Window.UpdateLayout();App.Render(Window,Path.Combine(output,"glass-settings-"+theme+".png"));
    foreach(string glassMode in new[]{"internal","full"}){
     UiGlass.SetMode(Window,glassMode);mode.ApplyTemplate();mode.IsDropDownOpen=true;await Task.Delay(180);
     var panel=(GlassPanel)mode.Template.FindName("DropDownPanel",mode);panel.UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(panel.ActualWidth),(int)Math.Ceiling(panel.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(panel);var pixel=new byte[4];image.CopyPixels(new Int32Rect(2,(int)panel.ActualHeight/2,1,1),pixel,4,0);
     check(panel.IsOverlay&&pixel[3]==255,"Dropdowns keep an opaque floor over the main interface in "+theme+" / "+glassMode+" glass");
     var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(output,"glass-dropdown-"+theme+"-"+glassMode+".png")))encoder.Save(stream);mode.IsDropDownOpen=false;
    }
   }}finally{mode.IsDropDownOpen=false;UiGlass.SetMode(Window,"internal");UiTheme.Set(themeBefore,false);}
   BackPage();App.Render(Window,Path.Combine(output,"glass-main-dark.png"));
  }finally{while(pages.Count>0)BackPage();NativeGlass.Allowed=allowed;UiGlass.SetMode(Window,previousMode);UiGlass.SetEnabled(Window,previous);}
 }
}
}
