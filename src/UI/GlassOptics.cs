using System;
using System.Windows;
using System.Windows.Media;
namespace ClearSpace {
// A bounded set of retained vector bands approximates a curved lens rim.
// Text is rendered afterwards and is never blurred or distorted. No render
// loop, full-window shader or per-card bitmap is allocated by this material.
internal sealed class GlassOptics {
 readonly Size size;readonly double radius;Geometry[] rings;Transform[] bends;readonly Rect rim;
 static Brush Gradient(byte first,byte middle,byte last){var brush=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(first,255,255,255),0),new GradientStop(Color.FromArgb(middle,255,255,255),0.5),new GradientStop(Color.FromArgb(last,255,255,255),1)},new Point(0,0),new Point(1,1));brush.Freeze();return brush;}
 static Pen Pen(Brush brush,double width){var pen=new Pen(brush,width);pen.Freeze();return pen;}
 static readonly Pen lightRim=Pen(Gradient(35,4,20),0.8),strongRim=Pen(Gradient(105,12,62),1),litRim=Pen(Gradient(145,20,75),1.2);
 static readonly Brush gleam=Gradient(28,0,8);
 static Geometry Rounded(Rect rectangle,double curve){var result=new RectangleGeometry(rectangle,Math.Max(0,curve),Math.Max(0,curve));result.Freeze();return result;}
 public GlassOptics(Size size,double radius){
  this.size=size;this.radius=radius;rim=new Rect(0.7,0.7,Math.Max(0,size.Width-1.4),Math.Max(0,size.Height-1.4));
 }
 void Prepare(){if(rings!=null)return;
  int count=size.Width>3&&size.Height>3?8:0;rings=new Geometry[count];bends=new Transform[count];double band=Math.Min(10,Math.Min(size.Width,size.Height)*0.22);
  for(int i=0;i<count;i++){double outside=0.6+band*i/count,inside=0.6+band*(i+1)/count;var outer=Rounded(new Rect(outside,outside,Math.Max(0,size.Width-2*outside),Math.Max(0,size.Height-2*outside)),radius-outside);var inner=Rounded(new Rect(inside,inside,Math.Max(0,size.Width-2*inside),Math.Max(0,size.Height-2*inside)),radius-inside);var ring=new CombinedGeometry(GeometryCombineMode.Exclude,outer,inner);ring.Freeze();rings[i]=ring;double offset=2.6*Math.Pow(1.0-(i+0.5)/count,2);var bend=new ScaleTransform(1+2*offset/size.Width,1+2*offset/size.Height,size.Width/2,size.Height/2);bend.Freeze();bends[i]=bend;}
 }
 public bool Fits(Size value,double curve){return size==value&&radius==curve;}
 public void Refract(DrawingContext drawing,FrameworkElement surface,Brush backdrop){
  Prepare();
  Rect scene=new Rect(0,0,size.Width,size.Height);if(backdrop==null){var window=Window.GetWindow(surface);var root=window==null?null:window.Content as FrameworkElement;if(root!=null&&root!=surface&&root.ActualWidth>0&&root.ActualHeight>0)try{Point origin=surface.TranslatePoint(new Point(),root);scene=new Rect(-origin.X,-origin.Y,root.ActualWidth,root.ActualHeight);}catch(InvalidOperationException){}}
  for(int i=0;i<rings.Length;i++){drawing.PushClip(rings[i]);drawing.PushTransform(bends[i]);if(backdrop!=null)drawing.DrawRectangle(backdrop,null,new Rect(0,0,size.Width,size.Height));else{drawing.PushOpacity(0.45);UiGlass.DrawAmbient(drawing,scene);drawing.Pop();}drawing.Pop();drawing.Pop();}
 }
 public void Rim(DrawingContext drawing,bool enhanced,double amount){if(rim.Width<=0||rim.Height<=0)return;double curve=Math.Max(0,radius-0.7);drawing.DrawRoundedRectangle(null,enhanced?strongRim:lightRim,rim,curve,curve);if(enhanced&&amount>0){drawing.PushOpacity(Math.Max(0,Math.Min(1,amount)));drawing.DrawRoundedRectangle(gleam,litRim,rim,curve,curve);drawing.Pop();}}
}
}
