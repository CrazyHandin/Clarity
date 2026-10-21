using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ClearSpace {
public static class UiGlass {
 public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(UiGlass),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.Inherits|FrameworkPropertyMetadataOptions.AffectsRender));
 public static bool GetEnabled(DependencyObject element){return (bool)element.GetValue(EnabledProperty);}
 public static void SetEnabled(DependencyObject element,bool value){element.SetValue(EnabledProperty,value);}
 internal static bool Active(DependencyObject element){return GetEnabled(element)&&!SystemParameters.HighContrast;}
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
  drawing.PushOpacity(0.86);base.OnRender(drawing);drawing.Pop();
  if(ActualWidth<3||ActualHeight<3)return;
  double radius=Math.Max(0,Math.Min(CornerRadius.TopLeft,Math.Min(ActualWidth,ActualHeight)/2));
  var bounds=new Rect(0.5,0.5,Math.Max(0,ActualWidth-1),Math.Max(0,ActualHeight-1));
  drawing.DrawRoundedRectangle(sheen,null,bounds,radius,radius);
  // Preserve intentionally borderless choices and their neutral selection edges.
  if(BorderThickness.Left>0&&BorderThickness.Top>0)drawing.DrawRoundedRectangle(null,new Pen(edge,0.7),bounds,radius,radius);
 }
}
// Frozen radial brushes provide the blurred color field without per-frame blur
// or a desktop capture. The opaque window keeps native window animations intact.
public sealed class GlassBackdrop:Grid {
 static Brush Glow(Color color,double x,double y,double radius){var brush=new RadialGradientBrush(color,Color.FromArgb(0,color.R,color.G,color.B)){Center=new Point(x,y),GradientOrigin=new Point(x,y),RadiusX=radius,RadiusY=radius};brush.Freeze();return brush;}
 static readonly Brush teal=Glow(Color.FromArgb(36,57,197,187),0.08,0.12,0.78);
 static readonly Brush blue=Glow(Color.FromArgb(25,97,149,222),0.93,0.45,0.7);
 static readonly Brush pearl=Glow(Color.FromArgb(15,220,246,239),0.5,1,0.8);
 protected override void OnRender(DrawingContext drawing){
  base.OnRender(drawing);if(!UiGlass.Active(this)||ActualWidth<=0||ActualHeight<=0)return;
  var bounds=new Rect(0,0,ActualWidth,ActualHeight);drawing.DrawRectangle(teal,null,bounds);drawing.DrawRectangle(blue,null,bounds);drawing.DrawRectangle(pearl,null,bounds);
 }
}
}
