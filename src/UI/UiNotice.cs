using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed class UiNotice {
 readonly Border frame;readonly TextBlock text;CancellationTokenSource pending;
 public UiNotice(Window owner){frame=(Border)owner.FindName("ToastFrame");text=(TextBlock)owner.FindName("ToastText");owner.Closed+=(s,e)=>{if(pending!=null)pending.Cancel();};}
 public async void Show(string message){
  if(String.IsNullOrWhiteSpace(message))return;
  var current=new CancellationTokenSource();var previous=pending;pending=current;if(previous!=null)previous.Cancel();
  text.Text=message;frame.Visibility=Visibility.Visible;UiMotion.Reveal(frame);
  try{await Task.Delay(2000,current.Token);if(ReferenceEquals(pending,current)){frame.BeginAnimation(UIElement.OpacityProperty,null);frame.Visibility=Visibility.Collapsed;}}
  catch(OperationCanceledException){}
  finally{if(ReferenceEquals(pending,current))pending=null;current.Dispose();}
 }
}
public sealed partial class Controller {
 UiNotice notice;
 void Status(string message){Get<TextBlock>("StatusText").Text=message;if(notice!=null)notice.Show(message);}
 public void Notify(string message){if(notice!=null)notice.Show(message);}
 public void PlayStartup(){UiMotion.Reveal(Get<Grid>("MainLayout"),280);}
}
}
