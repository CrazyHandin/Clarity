using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace ClearSpace {
public sealed class ThemeSwitchMark:Grid {
 public FrameworkElement Sun{get;private set;}public FrameworkElement Moon{get;private set;}public bool IsLight{get;private set;}
 sealed class MarkState {
  internal FrameworkElement Element;internal ScaleTransform Scale=new ScaleTransform();internal RotateTransform Rotation=new RotateTransform();internal int Revision;
  internal MarkState(FrameworkElement element){Element=element;var transforms=new TransformGroup();transforms.Children.Add(Scale);transforms.Children.Add(Rotation);element.RenderTransformOrigin=new Point(0.5,0.5);element.RenderTransform=transforms;}
 }
 MarkState sunState,moonState;
 public ThemeSwitchMark(){Width=26;Height=26;
  var sun=new Grid();sun.Children.Add(new Ellipse{Width=10,Height=10,Stroke=App.Brush("#F2B868"),StrokeThickness=1.8});sun.Children.Add(new Path{Data=Geometry.Parse("M13,1 L13,4 M13,22 L13,25 M1,13 L4,13 M22,13 L25,13 M4.5,4.5 L6.7,6.7 M19.3,19.3 L21.5,21.5 M4.5,21.5 L6.7,19.3 M19.3,6.7 L21.5,4.5"),Stroke=App.Brush("#F2B868"),StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});Sun=sun;
  Moon=new Path{Data=Geometry.Parse("M21,16 A10,10 0 1 1 10,3 A8,8 0 0 0 21,16 Z"),Stroke=App.Brush("#B8CDD9"),StrokeThickness=1.8,Fill=App.Brush("#27394C"),StrokeLineJoin=PenLineJoin.Round};Children.Add(Sun);Children.Add(Moon);sunState=new MarkState(Sun);moonState=new MarkState(Moon);Unloaded+=(s,e)=>Show(IsLight,false);Show(false,false);
 }
 public void Show(bool light,bool animate){if(animate&&IsLight==light)return;IsLight=light;Animate(sunState,light,animate);Animate(moonState,!light,animate);}
 static void Animate(MarkState state,bool visible,bool animate){
  var element=state.Element;var scale=state.Scale;var rotation=state.Rotation;double opacity=element.Opacity,size=scale.ScaleX,angle=rotation.Angle;int revision=++state.Revision;
  Action clear=()=>{element.BeginAnimation(OpacityProperty,null);rotation.BeginAnimation(RotateTransform.AngleProperty,null);scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);};clear();
  double targetOpacity=visible?1:0,targetSize=visible?1:0.55,targetAngle=visible?0:65;Action settle=()=>{element.Opacity=targetOpacity;scale.ScaleX=scale.ScaleY=targetSize;rotation.Angle=targetAngle;clear();};if(!animate){settle();return;}
  element.Opacity=opacity;scale.ScaleX=scale.ScaleY=size;rotation.Angle=angle;
  var ease=new CubicEase{EasingMode=EasingMode.EaseOut};var fade=new DoubleAnimation(opacity,targetOpacity,UiTheme.Transition){EasingFunction=ease};fade.Completed+=(s,e)=>{if(state.Revision==revision)settle();};
  element.BeginAnimation(OpacityProperty,fade,HandoffBehavior.SnapshotAndReplace);
  rotation.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(angle,targetAngle,UiTheme.Transition){EasingFunction=ease},HandoffBehavior.SnapshotAndReplace);
  scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(size,targetSize,UiTheme.Transition){EasingFunction=ease},HandoffBehavior.SnapshotAndReplace);scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(size,targetSize,UiTheme.Transition){EasingFunction=ease},HandoffBehavior.SnapshotAndReplace);
 }
}
public sealed partial class Controller {
 public Action<CleanerSettings> SaveTheme;
 void RefreshTheme(bool animate){UiTheme.Set(settings.Theme,animate);var mark=Get<ThemeSwitchMark>("ThemeMark");if(!animate||mark.IsLight!=UiTheme.Light)mark.Show(UiTheme.Light,animate);Get<Button>("ThemeButton").ToolTip=UiTheme.Light?"切换到深色":"切换到浅色";if(settingsView!=null){var selector=settingsView.Root.FindName("ThemeChoice")as ThemeSelector;if(selector!=null)selector.Value=UiTheme.Normalize(settings.Theme);}}
 void InitializeTheme(){UiGlass.SetEffect(Window,settings.GlassEffect);UiGlass.SetMode(Window,settings.GlassMode);UiGlass.SetEnabled(Window,settings.LiquidGlass);RefreshTheme(false);Get<Button>("ThemeButton").Click+=(s,e)=>ToggleTheme();
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
