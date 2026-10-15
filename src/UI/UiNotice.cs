using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
namespace ClearSpace {
public sealed class UiNotice {
 readonly Border frame;readonly TextBlock text,countdown,icon;CancellationTokenSource pending;Action activate;
 public UiNotice(Window owner){frame=(Border)owner.FindName("ToastFrame");text=(TextBlock)owner.FindName("ToastText");countdown=(TextBlock)owner.FindName("ToastCountdown");icon=(TextBlock)owner.FindName("ToastIcon");owner.Closed+=(s,e)=>{if(pending!=null)pending.Cancel();};frame.MouseLeftButtonUp+=(s,e)=>{if(activate!=null){e.Handled=true;Activate();}};frame.KeyDown+=(s,e)=>{if(activate!=null&&(e.Key==Key.Enter||e.Key==Key.Space)){e.Handled=true;Activate();}};}
 void Activate(){var action=activate;activate=null;if(pending!=null)pending.Cancel();frame.Visibility=Visibility.Collapsed;frame.IsHitTestVisible=false;if(action!=null)action();}
 public async void Show(string message,bool success=false,int seconds=2,Action click=null,bool showCountdown=false){
  if(String.IsNullOrWhiteSpace(message))return;
  var current=new CancellationTokenSource();var previous=pending;pending=current;if(previous!=null)previous.Cancel();
  activate=click;frame.IsHitTestVisible=frame.Focusable=click!=null;frame.Cursor=click!=null?Cursors.Hand:Cursors.Arrow;frame.Background=App.Brush(success?"#203D31":"#3B3323");frame.BorderBrush=App.Brush(success?"#56C98B":"#E8BE58");text.Foreground=App.Brush(success?"#C9F4DB":"#FFE6A3");icon.Foreground=App.Brush(success?"#7FDBAC":"#F2CB68");text.Text=message;countdown.Visibility=showCountdown?Visibility.Visible:Visibility.Collapsed;frame.Visibility=Visibility.Visible;UiMotion.Reveal(frame);
  try{
   for(int remaining=seconds;remaining>0;remaining--){if(showCountdown)countdown.Text=remaining+" "+UiText.T("秒后自动关闭");await Task.Delay(1000,current.Token);}
   if(ReferenceEquals(pending,current)){
    if(UiMotion.GetEnabled(frame)){
     frame.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(frame.Opacity,0,TimeSpan.FromMilliseconds(240)){EasingFunction=new QuadraticEase{EasingMode=EasingMode.EaseIn},FillBehavior=FillBehavior.HoldEnd},HandoffBehavior.SnapshotAndReplace);
     await Task.Delay(240,current.Token);
    }
    if(ReferenceEquals(pending,current)){activate=null;frame.IsHitTestVisible=false;frame.Visibility=Visibility.Collapsed;frame.BeginAnimation(UIElement.OpacityProperty,null);frame.Opacity=1;}
   }
  }
  catch(OperationCanceledException){}
  finally{if(ReferenceEquals(pending,current))pending=null;current.Dispose();}
 }
}
public sealed partial class Controller {
 UiNotice notice;
 void Status(string message){Get<TextBlock>("StatusText").Text=message;if(notice!=null)notice.Show(message);}
 public void Notify(string message){if(notice!=null)notice.Show(message);}
 public void NotifySuccess(string message){if(notice!=null)notice.Show(message,true);}
}
}
