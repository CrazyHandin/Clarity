using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
namespace ClearSpace {
// The same circle, arc and check geometry used by scripts/create-icon.ps1.
// Vector paths keep the in-app mark sharp at every display scale.
public sealed class BrandMark:Viewbox {
 readonly Path outline,check;
 public static readonly DependencyProperty OutlineProgressProperty=DependencyProperty.Register("OutlineProgress",typeof(double),typeof(BrandMark),new PropertyMetadata(1.0,Changed));
 public static readonly DependencyProperty CheckProgressProperty=DependencyProperty.Register("CheckProgress",typeof(double),typeof(BrandMark),new PropertyMetadata(1.0,Changed));
 public double OutlineProgress{get{return (double)GetValue(OutlineProgressProperty);}set{SetValue(OutlineProgressProperty,value);}}
 public double CheckProgress{get{return (double)GetValue(CheckProgressProperty);}set{SetValue(CheckProgressProperty,value);}}
 public BrandMark(){
  Stretch=Stretch.Uniform;var canvas=new Grid{Width=256,Height=256};Child=canvas;
  canvas.Children.Add(new Ellipse{Width=232.96,Height=232.96,Fill=App.Brush("#39C5BB"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center});
  outline=new Path{Stroke=App.Brush("#103C3B"),StrokeThickness=19.2,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round};
  check=new Path{Stroke=Brushes.White,StrokeThickness=16.64,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round};
  canvas.Children.Add(outline);canvas.Children.Add(check);UpdatePaths();
 }
 static void Changed(DependencyObject target,DependencyPropertyChangedEventArgs args){((BrandMark)target).UpdatePaths();}
 static double Clamp(double value){return Math.Max(0,Math.Min(1,value));}
 void UpdatePaths(){
  if(outline==null||check==null)return;double arc=Clamp(OutlineProgress),tick=Clamp(CheckProgress);
  if(arc<=0)outline.Data=null;else{
   double start=Math.PI/4,sweep=1.5*Math.PI*arc;var figure=new PathFigure{StartPoint=new Point(128+65.28*Math.Cos(start),125.44+65.28*Math.Sin(start))};
   figure.Segments.Add(new ArcSegment(new Point(128+65.28*Math.Cos(start+sweep),125.44+65.28*Math.Sin(start+sweep)),new Size(65.28,65.28),0,sweep>Math.PI,SweepDirection.Clockwise,true));outline.Data=new PathGeometry(new[]{figure});
  }
  if(tick<=0)check.Data=null;else{
   var a=new Point(102.4,128);var b=new Point(130.56,156.16);var c=new Point(204.8,84.48);double first=(b-a).Length,second=(c-b).Length,distance=tick*(first+second);var figure=new PathFigure{StartPoint=a};
   figure.Segments.Add(new LineSegment(distance<=first?a+(b-a)*(distance/first):b,true));if(distance>first)figure.Segments.Add(new LineSegment(b+(c-b)*((distance-first)/second),true));check.Data=new PathGeometry(new[]{figure});
  }
 }
 public void Prepare(){BeginAnimation(OutlineProgressProperty,null);BeginAnimation(CheckProgressProperty,null);OutlineProgress=CheckProgress=0;}
 public void Finish(){BeginAnimation(OutlineProgressProperty,null);BeginAnimation(CheckProgressProperty,null);OutlineProgress=CheckProgress=1;}
 public void Play(){
  Finish();if(!UiMotion.GetEnabled(this))return;
  BeginAnimation(OutlineProgressProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(460)){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.Stop});
  var tick=new DoubleAnimationUsingKeyFrames{FillBehavior=FillBehavior.Stop};tick.KeyFrames.Add(new DiscreteDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));tick.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480))));tick.KeyFrames.Add(new EasingDoubleKeyFrame(1,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(760)),new QuadraticEase{EasingMode=EasingMode.EaseOut}));BeginAnimation(CheckProgressProperty,tick);
 }
}
}
