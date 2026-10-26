using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace ClearSpace {
public sealed class ThemeSelector:Control {
 public static readonly DependencyProperty ValueProperty=DependencyProperty.Register("Value",typeof(string),typeof(ThemeSelector),new FrameworkPropertyMetadata("system",Changed,Coerce));
 public string Value{get{return (string)GetValue(ValueProperty);}set{SetValue(ValueProperty,value);}}
 public event RoutedPropertyChangedEventHandler<string> ValueChanged;
 static object Coerce(DependencyObject element,object value){return UiTheme.Normalize(value as string);}
 static void Changed(DependencyObject element,DependencyPropertyChangedEventArgs args){var selector=(ThemeSelector)element;var handler=selector.ValueChanged;if(handler!=null)handler(selector,new RoutedPropertyChangedEventArgs<string>((string)args.OldValue,(string)args.NewValue));selector.Select(UiMotion.GetEnabled(selector)&&selector.IsVisible);}
 readonly string[] values={"light","dark","system"};RadioButton[] buttons;Grid track;Border indicator;TranslateTransform position;int revision;
 public ThemeSelector(){IsVisibleChanged+=(s,e)=>Select(false);Unloaded+=(s,e)=>Select(false);}
 void Reflow(object sender,SizeChangedEventArgs args){if(args.WidthChanged)Select(position!=null&&position.HasAnimatedProperties&&UiMotion.GetEnabled(this));}
 public override void OnApplyTemplate(){
  if(buttons!=null)foreach(var button in buttons)if(button!=null)button.Click-=Choose;
  if(track!=null)track.SizeChanged-=Reflow;if(position!=null)position.BeginAnimation(TranslateTransform.XProperty,null);base.OnApplyTemplate();
  track=GetTemplateChild("ThemeTrack")as Grid;indicator=GetTemplateChild("ThemeIndicator")as Border;
  buttons=new[]{GetTemplateChild("ThemeLight")as RadioButton,GetTemplateChild("ThemeDark")as RadioButton,GetTemplateChild("ThemeSystem")as RadioButton};
  foreach(var button in buttons)if(button!=null)button.Click+=Choose;
  position=new TranslateTransform();if(indicator!=null)indicator.RenderTransform=position;if(track!=null)track.SizeChanged+=Reflow;Select(false);
 }
 void Choose(object sender,RoutedEventArgs args){int index=Array.IndexOf(buttons,(RadioButton)sender);if(index>=0)Value=values[index];}
 void Select(bool animate){
  if(buttons==null||track==null||indicator==null||position==null)return;int index=Array.IndexOf(values,Value);if(index<0)index=2;
  for(int i=0;i<buttons.Length;i++)if(buttons[i]!=null)buttons[i].SetCurrentValue(RadioButton.IsCheckedProperty,i==index);
  double width=Math.Max(0,track.ActualWidth/3),from=position.X,to=width*index;indicator.Width=width;int current=++revision;position.BeginAnimation(TranslateTransform.XProperty,null);position.X=to;
  if(!animate||width==0||Math.Abs(from-to)<0.1)return;position.X=from;
  var motion=new DoubleAnimation(from,to,UiTheme.Transition){EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}};
  motion.Completed+=(s,e)=>{if(current==revision){position.X=to;position.BeginAnimation(TranslateTransform.XProperty,null);}};position.BeginAnimation(TranslateTransform.XProperty,motion,HandoffBehavior.SnapshotAndReplace);
 }
 protected override void OnPreviewKeyDown(KeyEventArgs args){
  int index=Array.IndexOf(values,Value);if(index<0)index=2;
  if(args.Key==Key.Left||args.Key==Key.Up)index=(index+2)%3;else if(args.Key==Key.Right||args.Key==Key.Down)index=(index+1)%3;else if(args.Key==Key.Home)index=0;else if(args.Key==Key.End)index=2;else{base.OnPreviewKeyDown(args);return;}
  Value=values[index];if(buttons!=null&&buttons[index]!=null)buttons[index].Focus();args.Handled=true;
 }
}
}
