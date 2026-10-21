using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace ClearSpace {
// One owner and one clock for navigation; tab animations yield to this host.
sealed class PageTransition {
 readonly Grid surface;readonly CacheMode originalCache;int revision;EventHandler rendering;
 public PageTransition(Grid surface){this.surface=surface;originalCache=surface.CacheMode;surface.Unloaded+=(s,e)=>Stop();}
 bool CanAnimate(){
  if(!UiMotion.GetEnabled(surface)||!surface.IsVisible||(RenderCapability.Tier>>16)==0)return false;
  var source=PresentationSource.FromVisual(surface);if(source==null||source.CompositionTarget==null)return false;
  var dpi=source.CompositionTarget.TransformToDevice;double pixels=surface.ActualWidth*dpi.M11*surface.ActualHeight*dpi.M22;
  // Bound the temporary RGBA cache to about 16 MB; large/software-rendered surfaces switch immediately.
  return pixels>0&&pixels<=4000000;
 }
 void Stop(){revision++;if(rendering!=null){CompositionTarget.Rendering-=rendering;rendering=null;}var offset=surface.RenderTransform as TranslateTransform;if(offset!=null){offset.BeginAnimation(TranslateTransform.XProperty,null);offset.X=0;}surface.CacheMode=originalCache;UiMotion.SetNavigating(surface,false);}
 public void Switch(FrameworkElement destination,Action show,bool back=false){
  Stop();int current=revision;surface.BeginAnimation(UIElement.OpacityProperty,null);surface.Opacity=1;
  bool animate=CanAnimate();
  var offset=new TranslateTransform(animate?(back?-14:14):0,0);surface.RenderTransform=offset;
  UiMotion.SetNavigating(surface,animate);if(animate)surface.CacheMode=new BitmapCache();show();
  destination.BeginAnimation(UIElement.OpacityProperty,null);destination.Opacity=1;destination.RenderTransform=Transform.Identity;
  if(!animate)return;
  rendering=(s,e)=>{
   CompositionTarget.Rendering-=rendering;rendering=null;if(current!=revision)return;
   var motion=new DoubleAnimation(offset.X,0,TimeSpan.FromMilliseconds(260)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
   motion.Completed+=(sender,args)=>{if(current==revision)Stop();};offset.X=0;offset.BeginAnimation(TranslateTransform.XProperty,motion,HandoffBehavior.SnapshotAndReplace);
  };
  CompositionTarget.Rendering+=rendering;
 }
}
}
