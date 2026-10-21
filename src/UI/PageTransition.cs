using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace ClearSpace {
// One owner and one clock for navigation; tab animations yield to this host.
sealed class PageTransition {
 readonly Grid surface;int revision;
 public PageTransition(Grid surface){this.surface=surface;}
 public void Switch(FrameworkElement destination,Action show,bool back=false){
  int current=++revision;surface.BeginAnimation(UIElement.OpacityProperty,null);surface.Opacity=1;
  var offset=new TranslateTransform();surface.RenderTransform=offset;
  bool animate=UiMotion.GetEnabled(surface)&&surface.IsVisible;
  UiMotion.SetNavigating(surface,animate);show();surface.UpdateLayout();
  destination.BeginAnimation(UIElement.OpacityProperty,null);destination.Opacity=1;destination.RenderTransform=Transform.Identity;
  if(!animate)return;
  var motion=new DoubleAnimation(back?-8:8,0,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
  motion.Completed+=(s,e)=>{if(current!=revision)return;offset.BeginAnimation(TranslateTransform.XProperty,null);UiMotion.SetNavigating(surface,false);};
  offset.BeginAnimation(TranslateTransform.XProperty,motion,HandoffBehavior.SnapshotAndReplace);
 }
}
}
