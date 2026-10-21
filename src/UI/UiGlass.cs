using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ClearSpace {
public static class UiGlass {
 public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(UiGlass),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.Inherits|FrameworkPropertyMetadataOptions.AffectsRender));
 public static bool GetEnabled(DependencyObject element){return (bool)element.GetValue(EnabledProperty);}
 public static void SetEnabled(DependencyObject element,bool value){element.SetValue(EnabledProperty,value);var window=element as Window;if(window!=null)NativeGlass.Attach(window);}
 static readonly DependencyPropertyKey HasBackdropKey=DependencyProperty.RegisterAttachedReadOnly("HasBackdrop",typeof(bool),typeof(UiGlass),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.Inherits|FrameworkPropertyMetadataOptions.AffectsRender));
 public static readonly DependencyProperty HasBackdropProperty=HasBackdropKey.DependencyProperty;
 public static bool GetHasBackdrop(DependencyObject element){return (bool)element.GetValue(HasBackdropProperty);}
 internal static void SetHasBackdrop(DependencyObject element,bool value){element.SetValue(HasBackdropKey,value);}
 internal static bool Active(DependencyObject element){return GetEnabled(element)&&GetHasBackdrop(element)&&!SystemParameters.HighContrast;}
 internal static void Refresh(){NativeGlass.RefreshAll();}
 internal static Brush Gradient(Color first,Color last,Point end){var brush=new LinearGradientBrush(first,last,new Point(0,0),end);brush.Freeze();return brush;}
}
// Layered translucent material with a quiet specular edge. Children are drawn
// normally, so text stays sharp and opacity never dims labels or controls.
public sealed class GlassPanel:Border {
 static readonly Brush sheen=UiGlass.Gradient(Color.FromArgb(22,255,255,255),Color.FromArgb(0,255,255,255),new Point(0.25,0.8));
 static readonly Brush edge=UiGlass.Gradient(Color.FromArgb(75,230,255,253),Color.FromArgb(8,120,192,190),new Point(0.7,1));
 protected override void OnRender(DrawingContext drawing){
  var color=Background as SolidColorBrush;
  if(!UiGlass.Active(this)||Background==null||Background.Opacity==0||color!=null&&color.Color.A==0){base.OnRender(drawing);return;}
  bool accent=color!=null&&color.Color.G-color.Color.R>65;
  drawing.PushOpacity(accent?0.86:0.38);base.OnRender(drawing);drawing.Pop();
  if(ActualWidth<3||ActualHeight<3)return;
  double radius=Math.Max(0,Math.Min(CornerRadius.TopLeft,Math.Min(ActualWidth,ActualHeight)/2));
  var bounds=new Rect(0.5,0.5,Math.Max(0,ActualWidth-1),Math.Max(0,ActualHeight-1));
  drawing.DrawRoundedRectangle(sheen,null,bounds,radius,radius);
  // Preserve intentionally borderless choices and their neutral selection edges.
  if(BorderThickness.Left>0&&BorderThickness.Top>0)drawing.DrawRoundedRectangle(null,new Pen(edge,0.7),bounds,radius,radius);
 }
}
// A light tint over the live native backdrop, without painting an opaque floor.
public sealed class GlassBackdrop:Grid {
 protected override void OnRender(DrawingContext drawing){
  if(!UiGlass.Active(this)){base.OnRender(drawing);return;}drawing.PushOpacity(UiTheme.Light?0.24:0.22);base.OnRender(drawing);drawing.Pop();
 }
}
}
