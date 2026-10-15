using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace ClearSpace {
public static class UiMotion {
 public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(UiMotion),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.Inherits));
 public static bool GetEnabled(DependencyObject target){return (bool)target.GetValue(EnabledProperty);}
 public static void SetEnabled(DependencyObject target,bool value){target.SetValue(EnabledProperty,value);}
 public static void Reveal(FrameworkElement element,int milliseconds=180,double distance=8){
  if(element==null)return;var offset=new TranslateTransform();element.RenderTransform=offset;element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=1;
  if(!GetEnabled(element))return;
  var easing=new QuadraticEase{EasingMode=EasingMode.EaseOut};var duration=TimeSpan.FromMilliseconds(milliseconds);
  element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop},HandoffBehavior.SnapshotAndReplace);
  offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(distance,0,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop},HandoffBehavior.SnapshotAndReplace);
 }
 public static void Emphasize(FrameworkElement element){
  var scale=new ScaleTransform();var offset=new TranslateTransform();var transforms=new TransformGroup();transforms.Children.Add(scale);transforms.Children.Add(offset);element.RenderTransformOrigin=new Point(0.5,0.5);element.RenderTransform=transforms;element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=1;
  if(!GetEnabled(element))return;
  var duration=TimeSpan.FromMilliseconds(620);var easing=new BackEase{EasingMode=EasingMode.EaseOut,Amplitude=0.22};
  scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(0.82,1,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop});
  scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(0.82,1,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop});
  offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(14,0,duration){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});
  element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(280)){FillBehavior=FillBehavior.Stop});
 }
 public static void CompleteMark(System.Windows.Shapes.Path check,FrameworkElement sparkles){
  check.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty,null);check.StrokeDashOffset=0;sparkles.BeginAnimation(UIElement.OpacityProperty,null);sparkles.Opacity=1;
  if(!GetEnabled(check))return;
  var draw=new DoubleAnimationUsingKeyFrames{FillBehavior=FillBehavior.Stop};draw.KeyFrames.Add(new DiscreteDoubleKeyFrame(12,KeyTime.FromTimeSpan(TimeSpan.Zero)));draw.KeyFrames.Add(new LinearDoubleKeyFrame(12,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));draw.KeyFrames.Add(new EasingDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(540)),new QuadraticEase{EasingMode=EasingMode.EaseOut}));check.BeginAnimation(System.Windows.Shapes.Shape.StrokeDashOffsetProperty,draw);
  var twinkle=new DoubleAnimationUsingKeyFrames{FillBehavior=FillBehavior.Stop};twinkle.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));twinkle.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));twinkle.KeyFrames.Add(new LinearDoubleKeyFrame(0.35,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(460))));twinkle.KeyFrames.Add(new LinearDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(720))));sparkles.BeginAnimation(UIElement.OpacityProperty,twinkle);
 }
 public static void Tabs(TabControl tabs){tabs.SelectionChanged+=(s,e)=>{if(!ReferenceEquals(e.Source,tabs))return;tabs.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{tabs.ApplyTemplate();Reveal(tabs.Template.FindName("PART_SelectedContentHost",tabs)as FrameworkElement);}));};}
}
public sealed partial class Controller {
 public void PrepareStartup(){
  var main=Get<Grid>("MainLayout");main.Opacity=UiMotion.GetEnabled(main)?0:1;
  EventHandler rendered=null;rendered=(s,e)=>{Window.ContentRendered-=rendered;Window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{if(Window.IsVisible)PlayStartup();else main.Opacity=1;}));};Window.ContentRendered+=rendered;
 }
 public void PlayStartup(){UiMotion.Reveal(Get<Grid>("MainLayout"),560,18);UiMotion.Emphasize(Get<Image>("BrandLogo"));}
 void PlayCompletion(){
  var icon=Get<Viewbox>("CompletionIcon");Window.UpdateLayout();
  if(lastResult.Cancelled||lastResult.Failed>0||lastResult.SystemCleanupStarted&&!lastResult.SystemCleanupCompleted){UiMotion.Reveal(icon,240);return;}
  UiMotion.Emphasize(icon);UiMotion.CompleteMark(Get<System.Windows.Shapes.Path>("CompletionCheck"),Get<FrameworkElement>("CompletionSparkles"));
 }
}
}
