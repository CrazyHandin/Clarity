using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace ClearSpace {
public static class UiMotion {
 public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(UiMotion),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.Inherits));
 public static bool GetEnabled(DependencyObject target){return (bool)target.GetValue(EnabledProperty)&&SystemParameters.ClientAreaAnimation;}
 public static void SetEnabled(DependencyObject target,bool value){target.SetValue(EnabledProperty,value);}
 public static void Reveal(FrameworkElement element){
  if(element==null)return;var offset=new TranslateTransform();element.RenderTransform=offset;element.BeginAnimation(UIElement.OpacityProperty,null);element.Opacity=1;
  if(!GetEnabled(element))return;
  var easing=new QuadraticEase{EasingMode=EasingMode.EaseOut};var duration=TimeSpan.FromMilliseconds(180);
  element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop},HandoffBehavior.SnapshotAndReplace);
  offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(8,0,duration){EasingFunction=easing,FillBehavior=FillBehavior.Stop},HandoffBehavior.SnapshotAndReplace);
 }
 public static void Tabs(TabControl tabs){tabs.SelectionChanged+=(s,e)=>{if(!ReferenceEquals(e.Source,tabs))return;tabs.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{tabs.ApplyTemplate();Reveal(tabs.Template.FindName("PART_SelectedContentHost",tabs)as FrameworkElement);}));};}
}
}
