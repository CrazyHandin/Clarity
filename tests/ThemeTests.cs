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
using System.Windows.Input;
using System.Windows.Media;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifyThemes(string output,Action<bool,string> check){
  ShowReady();string previous=settings.Theme;var save=SaveTheme;var system=UiTheme.SystemLight;int saved=0;string file=Path.Combine(output,"theme-settings.json");var selections=groups.Select(g=>g.Selected).ToArray();int scans=engine.Rules.Count;
  var themeButton=Get<Button>("ThemeButton");var minimize=Get<Button>("MinimizeWindow");var caption=themeButton.Parent as StackPanel;check(caption!=null&&caption==minimize.Parent&&caption.Children.IndexOf(themeButton)+1==caption.Children.IndexOf(minimize)&&themeButton.BorderThickness==new Thickness(0)&&System.Windows.Shell.WindowChrome.GetIsHitTestVisibleInChrome(themeButton),"The borderless animated theme switch sits immediately beside Minimize in the interactive caption");
  var colorSlots=(Dictionary<string,ThemeColorSlot>)typeof(UiTheme).GetField("slots",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);var surfaceSlot=colorSlots.Values.Single(slot=>slot.Original==Color.FromRgb(22,29,37));int colorChanges=0;System.ComponentModel.PropertyChangedEventHandler onColor=(sender,args)=>colorChanges++;
  try{
   settings.Theme="dark";RefreshTheme(false);SaveTheme=s=>{saved++;File.WriteAllText(file,new JavaScriptSerializer().Serialize(s));};UiMotion.SetEnabled(Window,true);
   var continuityMark=Get<ThemeSwitchMark>("ThemeMark");continuityMark.Show(true,true);await Task.Delay(70);var transforms=(TransformGroup)continuityMark.Sun.RenderTransform;var scale=(ScaleTransform)transforms.Children[0];var rotation=(RotateTransform)transforms.Children[1];double opacityBefore=continuityMark.Sun.Opacity,scaleBefore=scale.ScaleX,angleBefore=rotation.Angle;
   continuityMark.Show(false,true);check(ReferenceEquals(transforms,continuityMark.Sun.RenderTransform)&&Math.Abs(continuityMark.Sun.Opacity-opacityBefore)<0.001&&Math.Abs(scale.ScaleX-scaleBefore)<0.001&&Math.Abs(rotation.Angle-angleBefore)<0.001,"Reversing a theme icon mid-animation preserves its current opacity, scale and angle without a jump");await Task.Delay(300);
   check(continuityMark.Sun.Opacity==0&&scale.ScaleX==0.55&&rotation.Angle==65&&!continuityMark.Sun.HasAnimatedProperties&&!scale.HasAnimatedProperties&&!rotation.HasAnimatedProperties,"Theme icon transitions settle on their exact endpoints and release completed clocks");continuityMark.Show(false,false);
   surfaceSlot.PropertyChanged+=onColor;
   Get<Button>("ThemeButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(80);var mark=Get<ThemeSwitchMark>("ThemeMark");Color middle=((SolidColorBrush)Get<Grid>("WindowRoot").Background).Color;
   check(saved==1&&settings.Theme=="light"&&UiTheme.Light&&mark.Sun.Opacity>0&&mark.Sun.Opacity<1&&mark.Moon.Opacity>0&&mark.Moon.Opacity<1,"The Sun and Moon crossfade as theme selection is saved");
   check(middle!=Color.FromRgb(22,29,37)&&middle!=Color.FromRgb(243,248,248),"Theme colors visibly interpolate between light and dark palettes");int revision=surfaceSlot.Revision;UiTheme.Set("light",true);check(surfaceSlot.Revision==revision,"Repeated refreshes preserve the running theme animation instead of restarting it");await Task.Delay(300);
   check(colorChanges==1&&!surfaceSlot.Brush.HasAnimatedProperties,"A theme transition updates its color source once and releases its completed animation clock");surfaceSlot.PropertyChanged-=onColor;
   check(((SolidColorBrush)Window.Background).Color==Color.FromRgb(243,248,248)&&((SolidColorBrush)Window.Foreground).Color==Color.FromRgb(23,62,61)&&new JavaScriptSerializer().Deserialize<CleanerSettings>(File.ReadAllText(file)).Theme=="light","Light mode uses the beta0.02 surface and text palette and persists to JSON");
   check(groups.Select(g=>g.Selected).SequenceEqual(selections)&&engine.Rules.Count==scans&&phase=="ready","Switching themes preserves scope selection and does not start scanning");App.Render(Window,Path.Combine(output,"light-startup.png"));
   bool failSettingsSave=false;UiTheme.SystemLight=()=>false;OpenSettings(null,null,s=>{if(failSettingsSave)throw new IOException("fixture save failure");saved++;});var settingsRoot=settingsView.Root;var selector=(ThemeSelector)settingsRoot.FindName("ThemeChoice");selector.Value="system";await Task.Delay(320);check(settings.Theme=="system"&&!UiTheme.Light,"Settings can follow the Windows dark application preference");
   UiTheme.SystemLight=()=>true;OnWindowsThemeChanged();await Task.Delay(320);check(UiTheme.Light&&settings.Theme=="system"&&mark.Sun.Opacity==1&&selector.Value=="system","A Windows theme notification refreshes colors and icon while preserving the System segment");App.Render(Window,Path.Combine(output,"light-settings.png"));
   selector.ApplyTemplate();var light=(RadioButton)selector.Template.FindName("ThemeLight",selector);var dark=(RadioButton)selector.Template.FindName("ThemeDark",selector);var automatic=(RadioButton)selector.Template.FindName("ThemeSystem",selector);var indicator=(Border)selector.Template.FindName("ThemeIndicator",selector);var position=(System.Windows.Media.TranslateTransform)indicator.RenderTransform;
   check(automatic.IsChecked==true&&light.IsChecked==false&&dark.IsChecked==false,"The three theme icons expose exactly one checked segment");
   dark.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(60);check(settings.Theme=="dark"&&!UiTheme.Light&&position.HasAnimatedProperties&&position.X>indicator.Width&&position.X<indicator.Width*2,"Clicking Dark saves its preference and smoothly slides the selected indicator");await Task.Delay(230);check(!position.HasAnimatedProperties&&dark.IsChecked==true,"The theme indicator releases its animation clock after settling");
   automatic.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(70);double indicatorBefore=position.X;dark.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Math.Abs(position.X-indicatorBefore)<0.001&&ReferenceEquals(position,indicator.RenderTransform),"Reversing the theme selection slider continues from its current position without resetting the transform");await Task.Delay(300);check(position.X==indicator.Width&&!position.HasAnimatedProperties,"Rapid theme segment changes settle on the latest selection and release the animation clock");
   failSettingsSave=true;light.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(settings.Theme=="dark"&&selector.Value=="dark"&&!UiTheme.Light&&dark.IsChecked==true,"A failed theme save restores the previous segment without changing the active theme");failSettingsSave=false;
   var key=new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(selector),Environment.TickCount,Key.Right){RoutedEvent=UIElement.PreviewKeyDownEvent};selector.RaiseEvent(key);check(key.Handled&&settings.Theme=="system"&&automatic.IsChecked==true,"Arrow-key navigation selects and saves the next theme segment");await Task.Delay(300);
   int beforeWheel=saved;selector.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});check(selector.Value=="system"&&saved==beforeWheel,"Scrolling over the theme segment bar never changes or saves a preference");
   UiText.SetLanguage("en");check(light.ToolTip.ToString()=="Light"&&dark.ToolTip.ToString()=="Dark"&&automatic.ToolTip.ToString()=="Follow Windows","Theme icon labels follow the selected UI language");UiText.SetLanguage("zh-CN");
   Get<Button>("ThemeButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(selector.Value==settings.Theme&&selector.Value=="dark","The settings theme bar stays synchronized with the title-bar switch");await Task.Delay(300);automatic.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(300);
   OpenAbout();App.Render(Window,Path.Combine(output,"light-about.png"));BackPage();BackPage();
   ToggleTheme();await Task.Delay(35);ToggleTheme();await Task.Delay(35);ToggleTheme();await Task.Delay(350);check(!UiTheme.Light&&settings.Theme=="dark"&&mark.Moon.Opacity==1&&((SolidColorBrush)Window.Background).Color==Color.FromRgb(22,29,37),"Rapid theme toggles settle on the latest explicit choice and leave follow-system mode");
   SaveTheme=s=>{throw new IOException("fixture save failure");};ToggleTheme();check(settings.Theme=="dark"&&!UiTheme.Light,"A failed preference save retains the current theme");
  }finally{surfaceSlot.PropertyChanged-=onColor;while(pages.Count>0)BackPage();SaveTheme=save;UiTheme.SystemLight=system;settings.Theme=previous;UiMotion.SetEnabled(Window,false);RefreshTheme(false);}
 }
}
}
