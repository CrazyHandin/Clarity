using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
namespace ClearSpace {
public static class UiGlass {
 public static readonly DependencyProperty EffectProperty=DependencyProperty.RegisterAttached("Effect",typeof(string),typeof(UiGlass),new FrameworkPropertyMetadata("light",FrameworkPropertyMetadataOptions.Inherits|FrameworkPropertyMetadataOptions.AffectsRender));
 public static string NormalizeEffect(string value){return String.Equals(value,"enhanced",StringComparison.OrdinalIgnoreCase)?"enhanced":"light";}
 public static string GetEffect(DependencyObject element){return NormalizeEffect((string)element.GetValue(EffectProperty));}
 public static void SetEffect(DependencyObject element,string value){element.SetValue(EffectProperty,NormalizeEffect(value));}
 internal static Func<bool> OpticsAvailable=()=> (RenderCapability.Tier>>16)>0;
 internal static bool Enhanced(DependencyObject element){return Active(element)&&GetEffect(element)=="enhanced"&&OpticsAvailable();}
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
 // Restore the restrained mint, sky and pearl color field from 1.1.1.
 // The same low-opacity colors tint either theme without tinting typography.
 static readonly Brush mint=Ambient(Color.FromArgb(36,57,197,187),new Point(0.08,0.12),0.78);
 static readonly Brush sky=Ambient(Color.FromArgb(25,97,149,222),new Point(0.93,0.45),0.7);
 static readonly Brush pearl=Ambient(Color.FromArgb(15,220,246,239),new Point(0.5,1),0.8);
 static Brush Dither(){var pixels=new byte[64*64*4];uint state=137;for(int i=0;i<pixels.Length;i+=4){state=state*1664525+1013904223;byte alpha=1,value=(state&0x80000000)==0?(byte)0:alpha;pixels[i]=pixels[i+1]=pixels[i+2]=value;pixels[i+3]=alpha;}var bitmap=BitmapSource.Create(64,64,96,96,PixelFormats.Pbgra32,null,pixels,64*4);bitmap.Freeze();var brush=new ImageBrush(bitmap){TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,64,64)};brush.Freeze();return brush;}
 static readonly Brush dither=Dither();
 internal static void DrawAmbient(DrawingContext drawing,Rect bounds){drawing.DrawRectangle(mint,null,bounds);drawing.DrawRectangle(sky,null,bounds);drawing.DrawRectangle(pearl,null,bounds);drawing.DrawRectangle(dither,null,bounds);}
}
// Tint only the existing surface; text and controls retain their normal opacity.
// Border owns the corner geometry, including asymmetric and borderless choices.
public sealed class GlassPanel:Border {
 public static readonly DependencyProperty IsLensProperty=DependencyProperty.Register("IsLens",typeof(bool),typeof(GlassPanel),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
 public bool IsLens{get{return (bool)GetValue(IsLensProperty);}set{SetValue(IsLensProperty,value);}}
 public static readonly DependencyProperty GleamProperty=DependencyProperty.Register("Gleam",typeof(double),typeof(GlassPanel),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender));
 public double Gleam{get{return (double)GetValue(GleamProperty);}set{SetValue(GleamProperty,value);}}
 GlassOptics shape;int gleamRevision;
 public GlassPanel(){Unloaded+=(s,e)=>{shape=null;gleamRevision++;BeginAnimation(GleamProperty,null);Gleam=0;};}
 protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e){base.OnPropertyChanged(e);if((e.Property==UiGlass.EffectProperty||e.Property==UiGlass.EnabledProperty)&&(!UiGlass.GetEnabled(this)||UiGlass.GetEffect(this)!="enhanced")){shape=null;gleamRevision++;BeginAnimation(GleamProperty,null);Gleam=0;}}
 void Illuminate(double to){var tint=Background as SolidColorBrush;if(Parent is Window||!IsEnabled||!IsLens||IsOverlay||Background==null||tint!=null&&(tint.Color.A==0||tint.Color.G-tint.Color.R>65)||!UiGlass.Enhanced(this))return;double from=Gleam;int revision=++gleamRevision;BeginAnimation(GleamProperty,null);Gleam=from;if(!UiMotion.GetEnabled(this)){Gleam=to;return;}var motion=new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(150)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};motion.Completed+=(s,e)=>{if(revision==gleamRevision){Gleam=to;BeginAnimation(GleamProperty,null);}};BeginAnimation(GleamProperty,motion,HandoffBehavior.SnapshotAndReplace);}
 protected override void OnMouseEnter(System.Windows.Input.MouseEventArgs e){base.OnMouseEnter(e);Illuminate(1);}
 protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e){base.OnMouseLeave(e);Illuminate(0);}
 // Brush transforms update the retained composition resource directly.
 // Rebuild the drawing only when the brush itself is replaced.
 public static readonly DependencyProperty BackdropProperty=DependencyProperty.Register("Backdrop",typeof(Brush),typeof(GlassPanel),new FrameworkPropertyMetadata(null,FrameworkPropertyMetadataOptions.AffectsRender|FrameworkPropertyMetadataOptions.SubPropertiesDoNotAffectRender));
 public Brush Backdrop{get{return (Brush)GetValue(BackdropProperty);}set{SetValue(BackdropProperty,value);}}
 public static readonly DependencyProperty IsOverlayProperty=DependencyProperty.Register("IsOverlay",typeof(bool),typeof(GlassPanel),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
 public bool IsOverlay{get{return (bool)GetValue(IsOverlayProperty);}set{SetValue(IsOverlayProperty,value);}}
#if CLARITY_TESTS
 internal int Redraws{get;private set;}
#endif
 protected override void OnRender(DrawingContext drawing){
#if CLARITY_TESTS
  Redraws++;
#endif
  var color=Background as SolidColorBrush;
  if(!UiGlass.Active(this)||Background==null||Background.Opacity==0||color!=null&&color.Color.A==0){base.OnRender(drawing);return;}
  bool accent=color!=null&&color.Color.G-color.Color.R>65;
  bool root=Parent is Window,floor=IsOverlay||UiGlass.GetMode(this)=="internal"&&root&&Backdrop==null;
  var bounds=new Rect(0,0,ActualWidth,ActualHeight);
  bool lens=IsLens&&!IsOverlay&&!accent,enhanced=UiGlass.Enhanced(this);if(lens&&(shape==null||!shape.Fits(bounds.Size,CornerRadius.TopLeft)))shape=new GlassOptics(bounds.Size,CornerRadius.TopLeft);
  if(Backdrop!=null){if(root&&UiGlass.GetMode(this)=="internal")drawing.DrawRoundedRectangle(Background,null,bounds,CornerRadius.TopLeft,CornerRadius.TopLeft);drawing.DrawRoundedRectangle(Backdrop,null,bounds,CornerRadius.TopLeft,CornerRadius.TopLeft);}
  drawing.PushOpacity(floor?1:accent?0.98:enhanced?(UiTheme.Light?0.54:0.62):UiTheme.Light?0.80:0.82);base.OnRender(drawing);drawing.Pop();
  if(root&&Backdrop==null&&ActualWidth>0&&ActualHeight>0){drawing.PushClip(new RectangleGeometry(bounds,CornerRadius.TopLeft,CornerRadius.TopLeft));drawing.PushOpacity(0.3);UiGlass.DrawAmbient(drawing,bounds);drawing.Pop();drawing.Pop();}
  if(lens)shape.Rim(drawing,enhanced,Gleam);
 }
}
// Only the window root paints the ambient layer. In-place settings inherit it.
public sealed class GlassBackdrop:Grid {
 protected override void OnRender(DrawingContext drawing){
  if(!UiGlass.Active(this)){base.OnRender(drawing);return;}
  bool root=Parent is Window;
  bool enhanced=UiGlass.Enhanced(this);double tint=root?(UiGlass.GetMode(this)=="internal"?1:UiTheme.Light?(enhanced?0.55:0.66):(enhanced?0.74:0.78)):UiTheme.Light?(enhanced?0.04:0.10):(enhanced?0.06:0.12);
  drawing.PushOpacity(tint);base.OnRender(drawing);drawing.Pop();
  if(root){drawing.PushOpacity(UiGlass.GetMode(this)=="internal"?1:0.35);UiGlass.DrawAmbient(drawing,new Rect(0,0,ActualWidth,ActualHeight));drawing.Pop();}
 }
}
}
