using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace ClearSpace {
public sealed class WindowTransitions {
 readonly Window window;readonly FrameworkElement surface;int revision;bool active,closing;
 public Task LastTransition=Task.FromResult(0);
 public WindowTransitions(Window window,FrameworkElement surface){this.window=window;this.surface=surface;
  window.Closing+=(s,e)=>{if(e.Cancel)return;closing=true;Stop();window.Dispatcher.BeginInvoke(new Action(()=>{if(window.IsVisible)closing=false;}));};
  window.Closed+=(s,e)=>{closing=true;++revision;};
  window.StateChanged+=(s,e)=>{if(window.WindowState!=WindowState.Minimized&&!closing)LastTransition=Reveal();};
 }
 void Stop(){++revision;if(!active)return;active=false;surface.BeginAnimation(UIElement.OpacityProperty,null);surface.Opacity=1;surface.RenderTransform=Transform.Identity;surface.IsHitTestVisible=true;}
 public async Task Minimize(Action request=null){
  if(active||closing)return;if(request==null)request=()=>SystemCommands.MinimizeWindow(window);
  if(!UiMotion.GetEnabled(window)){request();return;}int id=++revision;active=true;surface.IsHitTestVisible=false;
  Animate(1,0.2,1,0.96,150);await Task.Delay(160);if(id!=revision||closing)return;
  try{request();}finally{if(id==revision)Stop();}
 }
 public async Task Reveal(){
  Stop();if(closing||!UiMotion.GetEnabled(window))return;int id=++revision;active=true;Animate(0.4,1,0.975,1,220);await Task.Delay(230);if(id==revision)Stop();
 }
 void Animate(double from,double to,double sizeFrom,double sizeTo,int milliseconds){
  var scale=new ScaleTransform();surface.RenderTransformOrigin=new Point(0.5,0.5);surface.RenderTransform=scale;
  var duration=TimeSpan.FromMilliseconds(milliseconds);var ease=new QuadraticEase{EasingMode=EasingMode.EaseOut};
  surface.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(from,to,duration){EasingFunction=ease});
  scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(sizeFrom,sizeTo,duration){EasingFunction=ease});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(sizeFrom,sizeTo,duration){EasingFunction=ease});
 }
}
}
