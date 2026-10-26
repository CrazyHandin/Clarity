using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifyThemeMotionRegression(string output,Action<bool,string> check){
  string previous=UiTheme.Light?"light":"dark";var options=new CleanerSettings{Theme="dark"};var owner=new Window{Width=1050,Height=850,Resources=Window.Resources,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-15000,Top=-15000};ThemeSelector selector=null;TranslateTransform position=null;int general=0,applied=0,saved=0;bool savedBeforeMotion=false,fail=false;
  try{
   UiTheme.Set("dark",false);UiMotion.SetEnabled(owner,true);UiGlass.SetMode(owner,"internal");
   var view=RuleCenter.Create(owner,options,null,()=>general++,()=>{},()=>{},persist:s=>{savedBeforeMotion=position!=null&&!position.HasAnimatedProperties;Thread.Sleep(60);if(fail)throw new IOException("fixture save failure");File.WriteAllText(Path.Combine(output,"theme-motion.json"),s.Theme);saved++;},themeChanged:()=>{applied++;UiTheme.Set(options.Theme,true);});owner.Content=view.Root;owner.Show();owner.UpdateLayout();selector=(ThemeSelector)view.Root.FindName("ThemeChoice");selector.ApplyTemplate();var indicator=(Border)selector.Template.FindName("ThemeIndicator",selector);position=(TranslateTransform)indicator.RenderTransform;var light=(RadioButton)selector.Template.FindName("ThemeLight",selector);var dark=(RadioButton)selector.Template.FindName("ThemeDark",selector);
   light.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(savedBeforeMotion&&saved==1&&applied==1&&general==0&&UiTheme.Light,"Theme-only settings apply without the general disk and backup refresh and start sliding after persistence returns");
   double start=position.X;await Task.Delay(40);check(position.X<start&&position.X>start*0.5,"The theme slider starts gently rather than jumping across most of the track in its first frames");await Task.Delay(260);check(position.X==0&&!position.HasAnimatedProperties,"The gentler theme slide settles at its exact endpoint without retaining a clock");
   dark.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(60);double before=position.X;selector.Height+=4;owner.UpdateLayout();check(position.HasAnimatedProperties&&Math.Abs(position.X-before)<0.001,"A height-only layout change leaves the running theme slide intact");
   selector.Width+=24;owner.UpdateLayout();check(ReferenceEquals(position,indicator.RenderTransform)&&position.HasAnimatedProperties,"Resizing the theme track retargets the existing transform without snapping");await Task.Delay(280);check(Math.Abs(position.X-indicator.Width)<0.001&&!position.HasAnimatedProperties,"A resized theme track finishes at the newly measured segment position");
   fail=true;light.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(options.Theme=="dark"&&selector.Value=="dark"&&dark.IsChecked==true&&!position.HasAnimatedProperties&&general==0&&applied==2,"A failed theme save restores the selected segment without starting a stale slide or refreshing disk state");
   UiGlass.SetMode(owner,"full");bool native=UiGlass.GetHasBackdrop(owner);int activations=NativeGlass.AccentActivations;UiTheme.Set("light",true);UiTheme.Set("dark",true);check(NativeGlass.AccentActivations==activations&&UiGlass.GetHasBackdrop(owner)==native,"Palette transitions preserve existing native glass instead of recreating its compositor surface");UiGlass.SetMode(owner,"internal");
  }finally{UiMotion.SetEnabled(owner,false);owner.Close();UiTheme.Set(previous,false);}
 }
}
}
