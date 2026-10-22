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
 static readonly Brush lightTeal=Glow(Color.FromArgb(28,97,190,178),new Point(0.02,0.72),0.85);
 static readonly Brush lightBlue=Glow(Color.FromArgb(26,128,157,196),new Point(0.98,0.12),0.8);
 static readonly Brush lightPearl=Glow(Color.FromArgb(105,255,255,255),new Point(0.5,0.5),0.7);
 static readonly Brush lightGround=Gradient(Color.FromRgb(226,234,240),Color.FromRgb(244,247,248),new Point(0.75,1));
 internal static void DrawInternal(DrawingContext drawing,Rect bounds){if(UiTheme.Light)drawing.DrawRectangle(lightGround,null,bounds);drawing.DrawRectangle(UiTheme.Light?lightTeal:teal,null,bounds);drawing.DrawRectangle(UiTheme.Light?lightBlue:blue,null,bounds);drawing.DrawRectangle(UiTheme.Light?lightPearl:pearl,null,bounds);}
}
// Layered translucent material with a quiet specular edge. Children are drawn
// normally, so text stays sharp and opacity never dims labels or controls.
public sealed class GlassPanel:Border {
 public static readonly DependencyProperty IsOverlayProperty=DependencyProperty.Register("IsOverlay",typeof(bool),typeof(GlassPanel),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
 public bool IsOverlay{get{return (bool)GetValue(IsOverlayProperty);}set{SetValue(IsOverlayProperty,value);}}
 static Brush Sheen(bool light){var brush=new LinearGradientBrush(new GradientStopCollection{
  new GradientStop(Color.FromArgb(light?(byte)96:(byte)22,255,255,255),0),
  new GradientStop(Color.FromArgb(light?(byte)26:(byte)5,255,255,255),0.34),
  new GradientStop(Colors.Transparent,0.66),
  new GradientStop(Color.FromArgb(light?(byte)8:(byte)18,light?(byte)83:(byte)7,light?(byte)113:(byte)17,light?(byte)136:(byte)29),1)
 },new Point(0,0),new Point(0.32,1));brush.Freeze();return brush;}
 static readonly Brush sheen=Sheen(false),lightSheen=Sheen(true);
 static readonly Brush edge=UiGlass.Gradient(Color.FromArgb(75,230,255,253),Color.FromArgb(8,120,192,190),new Point(0.7,1));
 static readonly Brush lightEdge=UiGlass.Gradient(Color.FromArgb(250,255,255,255),Color.FromArgb(82,93,121,140),new Point(0.3,1));
 static Pen Rim(Brush brush,double width){var pen=new Pen(brush,width);pen.Freeze();return pen;}
 static readonly Pen lightRim=Rim(lightEdge,1),darkRim=Rim(edge,0.7);
 Geometry materialShape;Rect shapeBounds;CornerRadius shapeCorners;
 Geometry Shape(Rect bounds){
  if(materialShape!=null&&shapeBounds==bounds&&shapeCorners==CornerRadius)return materialShape;
  shapeBounds=bounds;shapeCorners=CornerRadius;double tl=CornerRadius.TopLeft,tr=CornerRadius.TopRight,br=CornerRadius.BottomRight,bl=CornerRadius.BottomLeft;
  double scale=1;foreach(double ratio in new[]{bounds.Width/Math.Max(1,tl+tr),bounds.Width/Math.Max(1,bl+br),bounds.Height/Math.Max(1,tl+bl),bounds.Height/Math.Max(1,tr+br)})scale=Math.Min(scale,ratio);tl*=scale;tr*=scale;br*=scale;bl*=scale;
  var shape=new StreamGeometry();using(var path=shape.Open()){
   path.BeginFigure(new Point(bounds.Left+tl,bounds.Top),true,true);path.LineTo(new Point(bounds.Right-tr,bounds.Top),true,false);Corner(path,new Point(bounds.Right,bounds.Top+tr),tr);
   path.LineTo(new Point(bounds.Right,bounds.Bottom-br),true,false);Corner(path,new Point(bounds.Right-br,bounds.Bottom),br);
   path.LineTo(new Point(bounds.Left+bl,bounds.Bottom),true,false);Corner(path,new Point(bounds.Left,bounds.Bottom-bl),bl);
   path.LineTo(new Point(bounds.Left,bounds.Top+tl),true,false);Corner(path,new Point(bounds.Left+tl,bounds.Top),tl);
  }shape.Freeze();return materialShape=shape;
 }
 static void Corner(StreamGeometryContext path,Point end,double radius){if(radius>0)path.ArcTo(end,new Size(radius,radius),0,false,SweepDirection.Clockwise,true,false);else path.LineTo(end,true,false);}
 protected override void OnRender(DrawingContext drawing){
  var color=Background as SolidColorBrush;
  if(!UiGlass.Active(this)||Background==null||Background.Opacity==0||color!=null&&color.Color.A==0){base.OnRender(drawing);return;}
  bool accent=color!=null&&color.Color.G-color.Color.R>65;
  bool floor=IsOverlay||UiGlass.GetMode(this)=="internal"&&Parent is Window;
  drawing.PushOpacity(floor?1:accent?0.86:UiTheme.Light?0.42:0.5);base.OnRender(drawing);drawing.Pop();
  if(ActualWidth<3||ActualHeight<3)return;
  var bounds=new Rect(0.5,0.5,Math.Max(0,ActualWidth-1),Math.Max(0,ActualHeight-1));
  var shape=Shape(bounds);
  if(floor){drawing.PushClip(shape);UiGlass.DrawInternal(drawing,bounds);drawing.Pop();}
  drawing.DrawGeometry(UiTheme.Light?lightSheen:sheen,null,shape);
  // Preserve intentionally borderless choices and their neutral selection edges.
  if(BorderThickness.Left>0&&BorderThickness.Top>0&&BorderThickness.Right>0&&BorderThickness.Bottom>0)drawing.DrawGeometry(null,UiTheme.Light?lightRim:darkRim,shape);
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
