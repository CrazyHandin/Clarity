using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ClearSpace {
public static class UiGlass {
 public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(UiGlass),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.Inherits|FrameworkPropertyMetadataOptions.AffectsRender));
 public static bool GetEnabled(DependencyObject element){return (bool)element.GetValue(EnabledProperty);}
 public static void SetEnabled(DependencyObject element,bool value){element.SetValue(EnabledProperty,value);var window=element as Window;if(window!=null)NativeGlass.Attach(window);}
 public static readonly DependencyProperty ModeProperty=DependencyProperty.RegisterAttached("Mode",typeof(string),typeof(UiGlass),new FrameworkPropertyMetadata("internal",FrameworkPropertyMetadataOptions.Inherits|FrameworkPropertyMetadataOptions.AffectsRender));
 public static string NormalizeMode(string value){return String.Equals(value,"full",StringComparison.OrdinalIgnoreCase)?"full":"internal";}
 public static string GetMode(DependencyObject element){return NormalizeMode((string)element.GetValue(ModeProperty));}
 public static void SetMode(DependencyObject element,string value){element.SetValue(ModeProperty,NormalizeMode(value));var window=element as Window;if(window!=null)NativeGlass.Attach(window);}
 static readonly DependencyPropertyKey HasBackdropKey=DependencyProperty.RegisterAttachedReadOnly("HasBackdrop",typeof(bool),typeof(UiGlass),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.Inherits|FrameworkPropertyMetadataOptions.AffectsRender));
 public static readonly DependencyProperty HasBackdropProperty=HasBackdropKey.DependencyProperty;
 public static bool GetHasBackdrop(DependencyObject element){return (bool)element.GetValue(HasBackdropProperty);}
 internal static void SetHasBackdrop(DependencyObject element,bool value){element.SetValue(HasBackdropKey,value);}
 internal static bool Active(DependencyObject element){return GetEnabled(element)&&(GetMode(element)=="internal"||GetHasBackdrop(element))&&!SystemParameters.HighContrast;}
 internal static void Refresh(){NativeGlass.RefreshAll();}
 internal static Brush Gradient(Color first,Color last,Point end){var brush=new LinearGradientBrush(first,last,new Point(0,0),end);brush.Freeze();return brush;}
 static Brush Glow(Color color,Point center,double radius){var brush=new RadialGradientBrush(color,Color.FromArgb(0,color.R,color.G,color.B)){Center=center,GradientOrigin=center,RadiusX=radius,RadiusY=radius};brush.Freeze();return brush;}
 static readonly Brush teal=Glow(Color.FromArgb(40,57,197,187),new Point(0.05,0.2),0.85);
 static readonly Brush blue=Glow(Color.FromArgb(26,82,133,216),new Point(0.9,0.35),0.8);
 static readonly Brush pearl=Glow(Color.FromArgb(14,225,244,243),new Point(0.4,1),0.7);
 static readonly Brush lightTeal=Glow(Color.FromArgb(22,90,194,182),new Point(0.02,0.18),0.9);
 static readonly Brush lightBlue=Glow(Color.FromArgb(16,145,167,196),new Point(1,0.35),0.85);
 static readonly Brush lightPearl=Glow(Color.FromArgb(112,255,255,255),new Point(0.45,0.8),0.95);
 internal static void DrawInternal(DrawingContext drawing,Rect bounds){drawing.DrawRectangle(UiTheme.Light?lightTeal:teal,null,bounds);drawing.DrawRectangle(UiTheme.Light?lightBlue:blue,null,bounds);drawing.DrawRectangle(UiTheme.Light?lightPearl:pearl,null,bounds);}
}
// Layered translucent material with a quiet specular edge. Children are drawn
// normally, so text stays sharp and opacity never dims labels or controls.
public sealed class GlassPanel:Border {
 public static readonly DependencyProperty IsOverlayProperty=DependencyProperty.Register("IsOverlay",typeof(bool),typeof(GlassPanel),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
 public bool IsOverlay{get{return (bool)GetValue(IsOverlayProperty);}set{SetValue(IsOverlayProperty,value);}}
 static readonly Brush sheen=UiGlass.Gradient(Color.FromArgb(14,255,255,255),Color.FromArgb(0,255,255,255),new Point(0.25,0.8));
 static readonly Brush lightSheen=UiGlass.Gradient(Color.FromArgb(95,255,255,255),Color.FromArgb(0,255,255,255),new Point(0.25,0.8));
 static readonly Brush edge=UiGlass.Gradient(Color.FromArgb(75,230,255,253),Color.FromArgb(8,120,192,190),new Point(0.7,1));
 static readonly Brush lightEdge=UiGlass.Gradient(Color.FromArgb(230,255,255,255),Color.FromArgb(46,73,104,112),new Point(0.3,1));
 static Pen Rim(Brush brush,double width){var pen=new Pen(brush,width);pen.Freeze();return pen;}
 static readonly Pen lightRim=Rim(lightEdge,1),darkRim=Rim(edge,0.7);
 protected override void OnRender(DrawingContext drawing){
  var color=Background as SolidColorBrush;
  if(!UiGlass.Active(this)||Background==null||Background.Opacity==0||color!=null&&color.Color.A==0){base.OnRender(drawing);return;}
  bool accent=color!=null&&color.Color.G-color.Color.R>65;
  bool floor=IsOverlay||UiGlass.GetMode(this)=="internal"&&Parent is Window;
  drawing.PushOpacity(floor?1:accent?0.86:UiTheme.Light?0.62:0.5);base.OnRender(drawing);drawing.Pop();
  if(ActualWidth<3||ActualHeight<3)return;
  double radius=Math.Max(0,Math.Min(CornerRadius.TopLeft,Math.Min(ActualWidth,ActualHeight)/2));
  var bounds=new Rect(0.5,0.5,Math.Max(0,ActualWidth-1),Math.Max(0,ActualHeight-1));
  if(floor){drawing.PushClip(new RectangleGeometry(bounds,radius,radius));UiGlass.DrawInternal(drawing,bounds);drawing.Pop();}
  drawing.DrawRoundedRectangle(UiTheme.Light?lightSheen:sheen,null,bounds,radius,radius);
  // Preserve intentionally borderless choices and their neutral selection edges.
  if(BorderThickness.Left>0&&BorderThickness.Top>0)drawing.DrawRoundedRectangle(null,UiTheme.Light?lightRim:darkRim,bounds,radius,radius);
 }
}
// A light tint over the live native backdrop, without painting an opaque floor.
public sealed class GlassBackdrop:Grid {
 protected override void OnRender(DrawingContext drawing){
  if(!UiGlass.Active(this)){base.OnRender(drawing);return;}
  if(UiGlass.GetMode(this)=="internal"&&Parent is Window){
   base.OnRender(drawing);UiGlass.DrawInternal(drawing,new Rect(0,0,ActualWidth,ActualHeight));return;
  }
  drawing.PushOpacity(UiTheme.Light?0.42:0.22);base.OnRender(drawing);drawing.Pop();
 }
}
}
