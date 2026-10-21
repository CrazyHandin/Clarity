using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifyThemes(string output,Action<bool,string> check){
  ShowReady();string previous=settings.Theme;var save=SaveTheme;var system=UiTheme.SystemLight;int saved=0;string file=Path.Combine(output,"theme-settings.json");var selections=groups.Select(g=>g.Selected).ToArray();int scans=engine.Rules.Count;
  var themeButton=Get<Button>("ThemeButton");var minimize=Get<Button>("MinimizeWindow");var caption=themeButton.Parent as StackPanel;check(caption!=null&&caption==minimize.Parent&&caption.Children.IndexOf(themeButton)+1==caption.Children.IndexOf(minimize)&&themeButton.BorderThickness==new Thickness(0)&&System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(themeButton),"The borderless animated theme switch sits immediately beside Minimize in the interactive caption");
  var colorSlots=(Dictionary<string,ThemeColorSlot>)typeof(UiTheme).GetField("slots",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);var surfaceSlot=colorSlots.Values.Single(slot=>slot.Original==Color.FromRgb(22,29,37));int colorChanges=0;System.ComponentModel.PropertyChangedEventHandler onColor=(sender,args)=>colorChanges++;
  try{
   settings.Theme="dark";RefreshTheme(false);SaveTheme=s=>{saved++;File.WriteAllText(file,new JavaScriptSerializer().Serialize(s));};UiMotion.SetEnabled(Window,true);
   surfaceSlot.PropertyChanged+=onColor;
   Get<Button>("ThemeButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(80);var mark=Get<ThemeSwitchMark>("ThemeMark");Color middle=((SolidColorBrush)Get<Grid>("WindowRoot").Background).Color;
   check(saved==1&&settings.Theme=="light"&&UiTheme.Light&&mark.Sun.Opacity>0&&mark.Sun.Opacity<1&&mark.Moon.Opacity>0&&mark.Moon.Opacity<1,"The Sun and Moon crossfade as theme selection is saved");
   check(middle!=Color.FromRgb(22,29,37)&&middle!=Color.FromRgb(243,248,248),"Theme colors visibly interpolate between light and dark palettes");int revision=surfaceSlot.Revision;UiTheme.Set("light",true);check(surfaceSlot.Revision==revision,"Repeated refreshes preserve the running theme animation instead of restarting it");await Task.Delay(300);
   check(colorChanges==1&&!surfaceSlot.Brush.HasAnimatedProperties,"A theme transition updates its color source once and releases its completed animation clock");surfaceSlot.PropertyChanged-=onColor;
   check(((SolidColorBrush)Window.Background).Color==Color.FromRgb(243,248,248)&&((SolidColorBrush)Window.Foreground).Color==Color.FromRgb(23,62,61)&&new JavaScriptSerializer().Deserialize<CleanerSettings>(File.ReadAllText(file)).Theme=="light","Light mode uses the beta0.02 surface and text palette and persists to JSON");
   check(groups.Select(g=>g.Selected).SequenceEqual(selections)&&engine.Rules.Count==scans&&phase=="ready","Switching themes preserves scope selection and does not start scanning");App.Render(Window,Path.Combine(output,"light-startup.png"));
   UiTheme.SystemLight=()=>false;OpenSettings(null,null,s=>{saved++;});var settingsRoot=settingsView.Root;((ComboBox)settingsRoot.FindName("ThemeChoice")).SelectedIndex=0;await Task.Delay(320);check(settings.Theme=="system"&&!UiTheme.Light,"Settings can follow the Windows dark application preference");
   UiTheme.SystemLight=()=>true;OnWindowsThemeChanged();await Task.Delay(320);check(UiTheme.Light&&settings.Theme=="system"&&mark.Sun.Opacity==1,"A Windows theme notification refreshes colors and icon while preserving follow-system mode");App.Render(Window,Path.Combine(output,"light-settings.png"));OpenAbout();App.Render(Window,Path.Combine(output,"light-about.png"));BackPage();BackPage();
   ToggleTheme();await Task.Delay(35);ToggleTheme();await Task.Delay(35);ToggleTheme();await Task.Delay(350);check(!UiTheme.Light&&settings.Theme=="dark"&&mark.Moon.Opacity==1&&((SolidColorBrush)Window.Background).Color==Color.FromRgb(22,29,37),"Rapid theme toggles settle on the latest explicit choice and leave follow-system mode");
   SaveTheme=s=>{throw new IOException("fixture save failure");};ToggleTheme();check(settings.Theme=="dark"&&!UiTheme.Light,"A failed preference save retains the current theme");
  }finally{surfaceSlot.PropertyChanged-=onColor;while(pages.Count>0)BackPage();SaveTheme=save;UiTheme.SystemLight=system;settings.Theme=previous;UiMotion.SetEnabled(Window,false);RefreshTheme(false);}
 }
}
}
