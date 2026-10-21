using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
namespace ClearSpace {
// The outgoing page remains visible while the live destination is laid out.
// Only a bitmap is retained, so navigation never reparents a page or its drafts.
sealed class PageTransition {
 readonly Grid surface;readonly Image snapshot;int revision;
 public PageTransition(Grid surface,Image snapshot){this.surface=surface;this.snapshot=snapshot;}
 public void Switch(FrameworkElement destination,Action show,bool back=false){
  int current=++revision;BitmapSource previous=null;
  if(UiMotion.GetEnabled(surface)&&surface.IsVisible&&surface.ActualWidth>0&&surface.ActualHeight>0){
   surface.UpdateLayout();
   var source=PresentationSource.FromVisual(surface);var matrix=source==null||source.CompositionTarget==null?Matrix.Identity:source.CompositionTarget.TransformToDevice;
   double scale=Math.Min(1,Math.Sqrt(4000000/Math.Max(1,surface.ActualWidth*matrix.M11*surface.ActualHeight*matrix.M22)));
   double x=matrix.M11*scale,y=matrix.M22*scale;
   var bitmap=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(surface.ActualWidth*x)),Math.Max(1,(int)Math.Ceiling(surface.ActualHeight*y)),96*x,96*y,PixelFormats.Pbgra32);
   bitmap.Render(surface);bitmap.Freeze();previous=bitmap;
  }
  snapshot.BeginAnimation(UIElement.OpacityProperty,null);snapshot.Source=null;snapshot.Visibility=Visibility.Collapsed;
  show();surface.UpdateLayout();
  destination.BeginAnimation(UIElement.OpacityProperty,null);destination.Opacity=1;
  var offset=new TranslateTransform();destination.RenderTransform=offset;
  if(previous==null)return;
  snapshot.Source=previous;snapshot.Opacity=1;snapshot.Visibility=Visibility.Visible;
  var duration=TimeSpan.FromMilliseconds(250);var ease=new CubicEase{EasingMode=EasingMode.EaseOut};
  offset.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(back?-6:6,0,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop});
  var fade=new DoubleAnimation(1,0,duration){EasingFunction=ease,FillBehavior=FillBehavior.Stop};
  fade.Completed+=(s,e)=>{if(current!=revision)return;snapshot.Visibility=Visibility.Collapsed;snapshot.BeginAnimation(UIElement.OpacityProperty,null);snapshot.Source=null;};
  snapshot.BeginAnimation(UIElement.OpacityProperty,fade,HandoffBehavior.SnapshotAndReplace);
 }
}
}
