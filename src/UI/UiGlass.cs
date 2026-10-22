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
}
// Tint only the existing surface; text and controls retain their normal opacity.
// Border owns the corner geometry, including asymmetric and borderless choices.
public sealed class GlassPanel:Border {
 public static readonly DependencyProperty IsOverlayProperty=DependencyProperty.Register("IsOverlay",typeof(bool),typeof(GlassPanel),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
 public bool IsOverlay{get{return (bool)GetValue(IsOverlayProperty);}set{SetValue(IsOverlayProperty,value);}}
 protected override void OnRender(DrawingContext drawing){
  var color=Background as SolidColorBrush;
  if(!UiGlass.Active(this)||Background==null||Background.Opacity==0||color!=null&&color.Color.A==0){base.OnRender(drawing);return;}
  bool accent=color!=null&&color.Color.G-color.Color.R>65;
  bool floor=IsOverlay||UiGlass.GetMode(this)=="internal"&&Parent is Window;
  drawing.PushOpacity(floor?1:accent?0.98:UiTheme.Light?0.86:0.9);base.OnRender(drawing);drawing.Pop();
 }
}
// Internal mode keeps its opaque floor; full mode exposes just a little backdrop.
public sealed class GlassBackdrop:Grid {
 protected override void OnRender(DrawingContext drawing){
  if(!UiGlass.Active(this)){base.OnRender(drawing);return;}
  if(UiGlass.GetMode(this)=="internal"&&Parent is Window){
   base.OnRender(drawing);return;
  }
  drawing.PushOpacity(UiTheme.Light?0.86:0.9);base.OnRender(drawing);drawing.Pop();
 }
}
}
