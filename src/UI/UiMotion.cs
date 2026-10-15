using System;
using System.Threading.Tasks;
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
 public static void Loading(ProgressBar bar){
  var track=new FrameworkElementFactory(typeof(Grid));track.SetValue(UIElement.ClipToBoundsProperty,true);track.SetBinding(Panel.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=System.Windows.Data.RelativeSource.TemplatedParent});
  var band=new FrameworkElementFactory(typeof(Border),"LoadingBand");band.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);band.SetValue(Border.BackgroundProperty,new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(0,57,197,187),0),new GradientStop(Color.FromRgb(57,197,187),0.22),new GradientStop(Color.FromRgb(57,197,187),0.78),new GradientStop(Color.FromArgb(0,57,197,187),1)},new Point(0,0.5),new Point(1,0.5)));track.AppendChild(band);bar.Template=new ControlTemplate(typeof(ProgressBar)){VisualTree=track};
  Action refresh=()=>{
   bar.ApplyTemplate();var element=bar.Template.FindName("LoadingBand",bar)as FrameworkElement;if(element==null)return;var position=element.RenderTransform as TranslateTransform;if(position==null||position.IsFrozen){position=new TranslateTransform();element.RenderTransform=position;}position.BeginAnimation(TranslateTransform.XProperty,null);if(!bar.IsVisible||bar.ActualWidth<=0)return;
   element.Width=Math.Max(64,Math.Min(220,bar.ActualWidth*0.24));position.X=(bar.ActualWidth-element.Width)/2;if(!GetEnabled(bar))return;position.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(-element.Width,bar.ActualWidth,TimeSpan.FromMilliseconds(1300)){RepeatBehavior=RepeatBehavior.Forever});
  };bar.IsVisibleChanged+=(s,e)=>refresh();bar.SizeChanged+=(s,e)=>refresh();bar.Loaded+=(s,e)=>refresh();
 }
 public static void CloseOnRequest(Window window,FrameworkElement surface){
  if(surface==null)return;bool animating=false,allowClose=false,closed=false;
  window.Closed+=(s,e)=>closed=true;window.PreviewKeyDown+=(s,e)=>{if(animating)e.Handled=true;};
  window.Closing+=(s,e)=>{
   if(e.Cancel||allowClose||!window.IsVisible||!GetEnabled(window))return;e.Cancel=true;if(animating)return;animating=true;
   bool hitTest=surface.IsHitTestVisible;double opacity=surface.Opacity;var original=surface.RenderTransform;var origin=surface.RenderTransformOrigin;surface.IsHitTestVisible=false;surface.RenderTransformOrigin=new Point(0.5,0.5);var scale=new ScaleTransform();surface.RenderTransform=scale;
   var duration=TimeSpan.FromMilliseconds(170);var easing=new QuadraticEase{EasingMode=EasingMode.EaseIn};scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(1,0.985,duration){EasingFunction=easing});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(1,0.985,duration){EasingFunction=easing});
   var fade=new DoubleAnimation(opacity,0,duration){EasingFunction=easing};fade.Completed+=(sender,args)=>window.Dispatcher.BeginInvoke(new Action(()=>{
    if(closed)return;allowClose=true;window.Close();
    if(!closed){allowClose=false;animating=false;surface.BeginAnimation(UIElement.OpacityProperty,null);surface.Opacity=opacity;surface.RenderTransform=original;surface.RenderTransformOrigin=origin;surface.IsHitTestVisible=hitTest;}
   }));surface.BeginAnimation(UIElement.OpacityProperty,fade,HandoffBehavior.SnapshotAndReplace);
  };
 }
 public static void Expander(Expander expander){
  int revision=0;
  Action<bool> resize=animate=>{expander.ApplyTemplate();var host=expander.Template.FindName("ExpandBody",expander)as FrameworkElement;var content=expander.Template.FindName("ExpandContent",expander)as FrameworkElement;if(host==null||content==null)return;int id=++revision;double from=host.ActualHeight;host.BeginAnimation(FrameworkElement.HeightProperty,null);content.Measure(new Size(Math.Max(1,expander.ActualWidth),Double.PositiveInfinity));double to=expander.IsExpanded?content.DesiredSize.Height:0;host.Height=to;if(!animate||!GetEnabled(expander)){if(expander.IsExpanded)host.Height=Double.NaN;return;}var motion=new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(220)){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.Stop};motion.Completed+=(s,e)=>{if(id!=revision)return;host.BeginAnimation(FrameworkElement.HeightProperty,null);host.Height=expander.IsExpanded?Double.NaN:0;};host.BeginAnimation(FrameworkElement.HeightProperty,motion);};
  expander.Loaded+=(s,e)=>resize(false);expander.Expanded+=(s,e)=>resize(true);expander.Collapsed+=(s,e)=>resize(true);
 }
}
public sealed partial class Controller {
 int startupRevision;
 public void PrepareStartup(){
  var main=Get<Grid>("MainLayout");bool animate=UiMotion.GetEnabled(main);main.Opacity=animate?0:1;main.IsHitTestVisible=!animate;Get<FrameworkElement>("StartupOverlay").Visibility=animate?Visibility.Visible:Visibility.Collapsed;if(animate)Get<BrandMark>("StartupLogo").Prepare();
  EventHandler rendered=null;rendered=(s,e)=>{Window.ContentRendered-=rendered;Window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{if(Window.IsVisible)PlayStartup();else main.Opacity=1;}));};Window.ContentRendered+=rendered;
 }
 public async void PlayStartup(){
  int revision=++startupRevision;var main=Get<Grid>("MainLayout");var overlay=Get<FrameworkElement>("StartupOverlay");var logo=Get<BrandMark>("StartupLogo");
  if(!UiMotion.GetEnabled(main)){logo.Finish();overlay.Visibility=Visibility.Collapsed;main.Opacity=1;main.IsHitTestVisible=true;return;}
  overlay.Visibility=Visibility.Visible;UiMotion.Reveal(overlay,130,0);logo.Play();
  await Task.Delay(780);if(revision!=startupRevision)return;
  if(Window.IsVisible){UiMotion.Reveal(main,260,8);overlay.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(180)){FillBehavior=FillBehavior.HoldEnd});await Task.Delay(280);}
  if(revision!=startupRevision)return;overlay.Visibility=Visibility.Collapsed;overlay.BeginAnimation(UIElement.OpacityProperty,null);overlay.Opacity=1;main.BeginAnimation(UIElement.OpacityProperty,null);main.Opacity=1;main.IsHitTestVisible=true;logo.Finish();
 }
 void PlayCompletion(){
  var icon=Get<Viewbox>("CompletionIcon");Window.UpdateLayout();
  if(lastResult.Cancelled||lastResult.Failed>0||lastResult.SystemCleanupStarted&&!lastResult.SystemCleanupCompleted){UiMotion.Reveal(icon,240);return;}
  UiMotion.Emphasize(icon);UiMotion.CompleteMark(Get<System.Windows.Shapes.Path>("CompletionCheck"),Get<FrameworkElement>("CompletionSparkles"));
 }
}
}
