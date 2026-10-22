using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
 // One static ambient layer per window, shared by all translucent surfaces.
 // No per-card blur, reflection pass, timer or screen capture is required.
 static Brush Ambient(Color color,Point origin,double radius){var brush=new RadialGradientBrush(color,Color.FromArgb(0,color.R,color.G,color.B)){Center=origin,GradientOrigin=origin,RadiusX=radius,RadiusY=radius};brush.Freeze();return brush;}
 static readonly Brush lightMint=Ambient(Color.FromArgb(64,57,197,187),new Point(0.05,0.05),0.85);
 static readonly Brush lightSky=Ambient(Color.FromArgb(42,120,166,231),new Point(1,0.2),0.8);
 static readonly Brush lightLilac=Ambient(Color.FromArgb(32,157,139,218),new Point(0.3,1),0.65);
 static readonly Brush darkMint=Ambient(Color.FromArgb(42,31,140,119),new Point(0.03,0.03),0.85);
 static readonly Brush darkSky=Ambient(Color.FromArgb(36,67,106,184),new Point(1,0.12),0.85);
 static readonly Brush darkLilac=Ambient(Color.FromArgb(24,115,83,170),new Point(0.25,1),0.7);
 static Brush Dither(){var pixels=new byte[64*64*4];uint state=137;for(int i=0;i<pixels.Length;i+=4){state=state*1664525+1013904223;byte alpha=3,value=(state&0x80000000)==0?(byte)0:alpha;pixels[i]=pixels[i+1]=pixels[i+2]=value;pixels[i+3]=alpha;}var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Pbgra32,null,pixels,64*4);bitmap.Freeze();var brush=new ImageBrush(bitmap){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,64,64)};brush.Freeze();return brush;}
 static readonly Brush dither=Dither();
 internal static void DrawAmbient(DrawingContext drawing,Rect bounds){drawing.DrawRectangle(UiTheme.Light?lightMint:darkMint,null,bounds);drawing.DrawRectangle(UiTheme.Light?lightSky:darkSky,null,bounds);drawing.DrawRectangle(UiTheme.Light?lightLilac:darkLilac,null,bounds);drawing.DrawRectangle(dither,null,bounds);}
}
// Tint only the existing surface; text and controls retain their normal opacity.
// Border owns the corner geometry, including asymmetric and borderless choices.
public sealed class GlassPanel:Border {
 public static readonly DependencyProperty BackdropProperty=DependencyProperty.Register("Backdrop",typeof(Brush),typeof(GlassPanel),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender));
 public Brush Backdrop{get{return (Brush)GetValue(BackdropProperty);}set{SetValue(BackdropProperty,value);}}
 public static readonly DependencyProperty IsOverlayProperty=DependencyProperty.Register("IsOverlay",typeof(bool),typeof(GlassPanel),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
 public bool IsOverlay{get{return (bool)GetValue(IsOverlayProperty);}set{SetValue(IsOverlayProperty,value);}}
 protected override void OnRender(DrawingContext drawing){
  var color=Background as SolidColorBrush;
  if(!UiGlass.Active(this)||Background==null||Background.Opacity==0||color!=null&&color.Color.A==0){base.OnRender(drawing);return;}
  bool accent=color!=null&&color.Color.G-color.Color.R>65;
  bool root=Parent is Window,floor=IsOverlay||UiGlass.GetMode(this)=="internal"&&root&&Backdrop==null;
  var bounds=new Rect(0,0,ActualWidth,ActualHeight);
  if(root&&Backdrop!=null){if(UiGlass.GetMode(this)=="internal")drawing.DrawRoundedRectangle(Background,null,bounds,CornerRadius.TopLeft,CornerRadius.TopLeft);drawing.DrawRoundedRectangle(Backdrop,null,bounds,CornerRadius.TopLeft,CornerRadius.TopLeft);}
  drawing.PushOpacity(floor?1:accent?0.98:UiTheme.Light?0.64:0.66);base.OnRender(drawing);drawing.Pop();
  if(root&&ActualWidth>0&&ActualHeight>0){drawing.PushClip(new RectangleGeometry(bounds,CornerRadius.TopLeft,CornerRadius.TopLeft));drawing.PushOpacity(0.6);UiGlass.DrawAmbient(drawing,bounds);drawing.Pop();drawing.Pop();}
 }
}
// Only the window root paints the ambient layer. In-place settings inherit it.
public sealed class GlassBackdrop:Grid {
 protected override void OnRender(DrawingContext drawing){
  if(!UiGlass.Active(this)){base.OnRender(drawing);return;}
  bool root=Parent is Window;
  double tint=root?(UiGlass.GetMode(this)=="internal"?1:UiTheme.Light?0.6:0.7):UiTheme.Light?0.12:0.18;
  drawing.PushOpacity(tint);base.OnRender(drawing);drawing.Pop();
  if(root){drawing.PushOpacity(UiGlass.GetMode(this)=="internal"?1:0.35);UiGlass.DrawAmbient(drawing,new Rect(0,0,ActualWidth,ActualHeight));drawing.Pop();}
 }
}
}
