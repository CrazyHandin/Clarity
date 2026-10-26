using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifyGlassEffects(string output,Action<bool,string> check){
  string previous=UiGlass.GetEffect(Window),mode=UiGlass.GetMode(Window),theme=UiTheme.Light?"light":"dark";bool enabled=UiGlass.GetEnabled(Window),motion=UiMotion.GetEnabled(Window);var available=UiGlass.OpticsAvailable;bool fail=false;var options=new CleanerSettings();string file=Path.Combine(output,"glass-effects.json");
  try{
   UiMotion.SetEnabled(Window,false);UiGlass.SetEnabled(Window,true);UiGlass.SetMode(Window,"internal");UiGlass.SetEffect(Window,"light");UiGlass.OpticsAvailable=()=>true;
   OpenSettings(options,null,s=>{if(fail)throw new IOException("fixture save failure");File.WriteAllText(file,new JavaScriptSerializer().Serialize(s));});Window.UpdateLayout();var root=settingsView.Root;var effect=(ComboBox)root.FindName("GlassEffectChoice");var glass=(CheckBox)root.FindName("LiquidGlassChoice");
   check(options.GlassEffect=="light"&&effect.SelectedIndex==0&&new JavaScriptSerializer().Deserialize<CleanerSettings>("{}").GlassEffect=="light","Glass effects default to Lightweight for both new and existing settings");
   check(new JavaScriptSerializer().Deserialize<CleanerSettings>("{\"GlassEffect\":\"invalid\"}").GlassEffect=="light"&&UiGlass.NormalizeEffect(null)=="light","Invalid glass renderer preferences safely normalize to Lightweight");
   effect.SelectedIndex=1;check(options.GlassEffect=="enhanced"&&UiGlass.GetEffect(Window)=="enhanced"&&new JavaScriptSerializer().Deserialize<CleanerSettings>(File.ReadAllText(file)).GlassEffect=="enhanced"&&UiGlass.GetMode(Window)=="internal","Enhanced refraction saves and applies without changing the separate transparency mode");
   foreach(string appearance in new[]{"light","dark"}){
    options.Theme=appearance;((ThemeSelector)root.FindName("ThemeChoice")).Value=appearance;UiTheme.Set(appearance,false);Get<ThemeSwitchMark>("ThemeMark").Show(UiTheme.Light,false);
    foreach(string rendering in new[]{"light","enhanced"}){
     effect.SelectedIndex=rendering=="enhanced"?1:0;Window.UpdateLayout();Get<FrameworkElement>("ToastFrame").Visibility=Visibility.Collapsed;App.Render(Window,Path.Combine(output,"glass-effects-settings-"+appearance+"-"+rendering+".png"));
     var popup=UpdateDialogs.Highlights(Window);UiMotion.SetEnabled(popup,false);popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=Window.Left+(Window.ActualWidth-popup.Width)/2;popup.Top=Window.Top+(Window.ActualHeight-popup.Height)/2;popup.ShowActivated=false;
     try{popup.Show();popup.UpdateLayout();await DialogBackdrop.WhenReady(popup);var panel=(GlassPanel)popup.FindName("DialogSurface");check(UiGlass.GetEffect(popup)==rendering&&panel.IsLens&&panel.Backdrop!=null&&panel.Effect==null&&panel.Child.Opacity==1,"Release notes inherit "+rendering+" material while leaving text and icons undistorted in "+appearance);var before=DialogCenterColor(popup);panel.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});check(panel.Gleam==0&&!panel.HasAnimatedProperties&&DialogCenterColor(popup)==before,"Hovering the "+rendering+" popup leaves its entire background unchanged in "+appearance);check(DialogPixelAlpha(popup,330,180)==255&&DialogPixelAlpha(popup,0,0)==0,"The "+rendering+" popup keeps an opaque readable interior and rounded transparent corners in "+appearance);App.Render(popup,Path.Combine(output,"glass-effects-popup-"+appearance+"-"+rendering+".png"));}finally{popup.Close();}
    }
    check(!File.ReadAllBytes(Path.Combine(output,"glass-effects-popup-"+appearance+"-light.png")).SequenceEqual(File.ReadAllBytes(Path.Combine(output,"glass-effects-popup-"+appearance+"-enhanced.png"))),"Lightweight and Enhanced produce distinct popup materials in "+appearance);
   }
   effect.SelectedIndex=1;fail=true;effect.SelectedIndex=0;check(effect.SelectedIndex==1&&options.GlassEffect=="enhanced"&&UiGlass.GetEffect(Window)=="enhanced","A failed preference save restores both the effect selector and active material");fail=false;
   glass.IsChecked=false;glass.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!effect.IsEnabled&&!UiGlass.Enhanced(Window),"Disabling Liquid Glass also disables its renderer selector and optical material");glass.IsChecked=true;glass.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   UiGlass.OpticsAvailable=()=>false;check(UiGlass.GetEffect(Window)=="enhanced"&&!UiGlass.Enhanced(Window),"Unsupported graphics retain the requested preference but fall back to Lightweight rendering");UiGlass.OpticsAvailable=()=>true;
   var lens=(GlassPanel)((Button)root.FindName("AboutButton")).Template.FindName("Surface",(Button)root.FindName("AboutButton"));UiMotion.SetEnabled(Window,true);lens.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseEnterEvent});await Task.Delay(60);check(lens.Gleam>0&&lens.Gleam<1,"Enhanced hover light transitions smoothly instead of starting a permanent animation");await Task.Delay(180);check(lens.Gleam==1&&!lens.HasAnimatedProperties,"Completed hover illumination releases its animation clock");lens.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseLeaveEvent});await Task.Delay(230);check(lens.Gleam==0&&!lens.HasAnimatedProperties,"Pointer exit returns optical illumination to rest and releases its clock");
   UiText.SetLanguage("en");check(UiText.T("轻量流畅")=="Lightweight"&&UiText.T("增强折射")=="Enhanced refraction","Both glass renderers have English labels");UiText.SetLanguage("zh-CN");
  }finally{UiMotion.SetEnabled(Window,false);while(pages.Count>0)BackPage();UiGlass.OpticsAvailable=available;UiGlass.SetEffect(Window,previous);UiGlass.SetMode(Window,mode);UiGlass.SetEnabled(Window,enabled);UiTheme.Set(theme,false);UiMotion.SetEnabled(Window,motion);}
 }
}
}
