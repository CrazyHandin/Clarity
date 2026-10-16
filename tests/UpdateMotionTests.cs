using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifyUpdateMotion(string output,Action<bool,string> check){
  UiMotion.SetEnabled(Window,true);var popup=UpdateDialogs.Highlights(Window);popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=popup.Top=-12000;popup.ShowActivated=false;bool blockClose=false,closed=false;popup.Closing+=(s,e)=>{if(blockClose)e.Cancel=true;};popup.Closed+=(s,e)=>closed=true;
  try{
   popup.Show();await Task.Delay(90);check(popup.Opacity>0&&popup.Opacity<1&&((FrameworkElement)popup.FindName("DialogSurface")).RenderTransform is ScaleTransform,"Update popups fade and scale the whole surface on opening");await Task.Delay(200);check(popup.Opacity==1,"Update popup opening animation settles fully opaque");App.Render(popup,Path.Combine(output,"update-popup-motion.png"));
   blockClose=true;popup.Close();await Task.Delay(50);check(!closed&&popup.IsVisible&&popup.Opacity==1&&((FrameworkElement)popup.FindName("DialogSurface")).IsHitTestVisible,"A pending-download close guard prevents fading or disabling an update popup");
   blockClose=false;popup.Close();await Task.Delay(60);check(!closed&&popup.Opacity>0&&popup.Opacity<1&&!((FrameworkElement)popup.FindName("DialogSurface")).IsHitTestVisible,"Update popup close fades out and blocks repeated interaction");popup.Close();await Task.Delay(230);check(closed,"Repeated update popup close requests finish one smooth close");
  }finally{UiMotion.SetEnabled(popup,false);if(popup.IsVisible)popup.Close();UiMotion.SetEnabled(Window,false);}
  popup=UpdateDialogs.Highlights(Window);popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=popup.Top=-12000;popup.ShowActivated=false;closed=false;popup.Closed+=(s,e)=>closed=true;popup.Show();check(popup.Opacity==1,"Motion-disabled update popups open immediately without becoming transparent");popup.Close();check(closed,"Motion-disabled update popups close immediately");
 }
}
}
