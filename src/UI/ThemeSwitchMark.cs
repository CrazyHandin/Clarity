using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace ClearSpace {
public sealed class ThemeSwitchMark:Grid {
 public FrameworkElement Sun{get;private set;}public FrameworkElement Moon{get;private set;}public bool IsLight{get;private set;}
 public ThemeSwitchMark(){Width=26;Height=26;
  var sun=new Grid();sun.Children.Add(new Ellipse{Width=10,Height=10,Stroke=App.Brush("#F2B868"),StrokeThickness=1.8});sun.Children.Add(new Path{Data=Geometry.Parse("M13,1 L13,4 M13,22 L13,25 M1,13 L4,13 M22,13 L25,13 M4.5,4.5 L6.7,6.7 M19.3,19.3 L21.5,21.5 M4.5,21.5 L6.7,19.3 M19.3,6.7 L21.5,4.5"),Stroke=App.Brush("#F2B868"),StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});Sun=sun;
  Moon=new Path{Data=Geometry.Parse("M21,16 A10,10 0 1 1 10,3 A8,8 0 0 0 21,16 Z"),Stroke=App.Brush("#B8CDD9"),StrokeThickness=1.8,Fill=App.Brush("#27394C"),StrokeLineJoin=PenLineJoin.Round};Children.Add(Sun);Children.Add(Moon);Show(false,false);
 }
 public void Show(bool light,bool animate){IsLight=light;Animate(Sun,light,animate);Animate(Moon,!light,animate);}
 static void Animate(FrameworkElement element,bool visible,bool animate){
  double from=element.Opacity;element.BeginAnimation(OpacityProperty,null);element.Opacity=visible?1:0;var scale=new ScaleTransform();var rotation=new RotateTransform();var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(rotation);element.RenderTransformOrigin=new Point(0.5,0.5);element.RenderTransform=transforms;if(!animate)return;
  var duration=TimeSpan.FromMilliseconds(260);var ease=new QuadraticEase{EasingMode=EasingMode.EaseInOut};element.BeginAnimation(OpacityProperty,new DoubleAnimation(from,visible?1:0,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
  rotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(visible?-65:0,visible?0:65,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
  scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(visible?0.55:1,visible?1:0.55,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(visible?0.55:1,visible?1:0.55,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
 }
}
public sealed partial class Controller {
 public Action<CleanerSettings> SaveTheme;
 void RefreshTheme(bool animate){UiTheme.Set(settings.Theme,animate);var mark=Get<ThemeSwitchMark>("ThemeMark");if(!animate||mark.IsLight!=UiTheme.Light)mark.Show(UiTheme.Light,animate);Get<Button>("ThemeButton").ToolTip=UiTheme.Light?"切换到深色":"切换到浅色";}
 void InitializeTheme(){UiGlass.SetMode(Window,settings.GlassMode);UiGlass.SetEnabled(Window,settings.LiquidGlass);RefreshTheme(false);Get<Button>("ThemeButton").Click+=(s,e)=>ToggleTheme();
  Window.SourceInitialized+=(s,e)=>{var source=System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(Window).Handle);if(source!=null)source.AddHook((IntPtr handle,int message,IntPtr wParam,IntPtr lParam,ref bool handled)=>{if(message==0x1A||message==0x31A)Window.Dispatcher.BeginInvoke(new Action(OnWindowsThemeChanged));return IntPtr.Zero;});};
 }
 void OnWindowsThemeChanged(){if(Window.IsVisible&&UiTheme.Normalize(settings.Theme)=="system"&&UiTheme.Light!=UiTheme.SystemLight())RefreshTheme(UiMotion.GetEnabled(Window));}
 void ToggleTheme(){
  string previous=settings.Theme;settings.Theme=UiTheme.Light?"dark":"light";
  try{
#if CLARITY_TESTS
   if(!testMode&&SaveTheme==null)settings.Save();else if(SaveTheme!=null)SaveTheme(settings);
#else
   if(SaveTheme!=null)SaveTheme(settings);else settings.Save();
#endif
  }catch(Exception ex){settings.Theme=previous;Status("主题设置未保存："+ex.Message);return;}
  RefreshTheme(UiMotion.GetEnabled(Window));
 }
}
}
