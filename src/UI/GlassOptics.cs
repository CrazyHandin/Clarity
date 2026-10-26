using System;
using System.Windows;
using System.Windows.Media;
namespace ClearSpace {
// One continuous surface and one simple clip. Foreground content is rendered
// later, so neither translucency nor hover light affects text sharpness.
internal sealed class GlassOptics {
 readonly Size size;readonly double radius;readonly Rect rim;readonly Geometry clip;
 static Brush Gradient(byte first,byte middle,byte last){var brush=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(first,255,255,255),0),new GradientStop(Color.FromArgb(middle,255,255,255),0.5),new GradientStop(Color.FromArgb(last,255,255,255),1)},new Point(0,0),new Point(0,1));brush.Freeze();return brush;}
 static Pen Pen(Brush brush,double width){var pen=new Pen(brush,width);pen.Freeze();return pen;}
 static readonly Pen lightRim=Pen(Gradient(14,2,6),0.7),strongRim=Pen(Gradient(34,5,16),0.75),litRim=Pen(Gradient(54,8,24),0.85);
 static readonly Brush lightSheen=Gradient(9,2,0),darkSheen=Gradient(15,3,0),gleam=Gradient(18,5,1);
 public GlassOptics(Size size,double radius){
  this.size=size;this.radius=radius;rim=new Rect(0.7,0.7,Math.Max(0,size.Width-1.4),Math.Max(0,size.Height-1.4));var geometry=new RectangleGeometry(new Rect(0,0,size.Width,size.Height),radius,radius);geometry.Freeze();clip=geometry;
 }
 public bool Fits(Size value,double curve){return size==value&&radius==curve;}
 public void Rim(DrawingContext drawing,bool enhanced,double amount){if(rim.Width<=0||rim.Height<=0)return;double curve=Math.Max(0,radius-0.7);drawing.PushClip(clip);if(enhanced)drawing.DrawRectangle(UiTheme.Light?lightSheen:darkSheen,null,new Rect(0,0,size.Width,size.Height));drawing.DrawRoundedRectangle(null,enhanced?strongRim:lightRim,rim,curve,curve);if(enhanced&&amount>0){drawing.PushOpacity(Math.Max(0,Math.Min(1,amount)));drawing.DrawRoundedRectangle(gleam,litRim,rim,curve,curve);drawing.Pop();}drawing.Pop();}
}
}
