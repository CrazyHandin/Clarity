using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace ClearSpace {
public sealed partial class Controller {
 static byte DialogPixelAlpha(Window popup,int x,int y){var image=new RenderTargetBitmap((int)Math.Ceiling(popup.ActualWidth),(int)Math.Ceiling(popup.ActualHeight),96,96,PixelFormats.Pbgra32);image.Render(popup);var pixel=new byte[4];image.CopyPixels(new Int32Rect(x,y,1,1),pixel,4,0);return pixel[3];}
 async Task VerifyUpdateMotion(string output,Action<bool,string> check){
  UiMotion.SetEnabled(Window,true);var popup=UpdateDialogs.Highlights(Window);popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=popup.Top=-12000;popup.ShowActivated=false;var surface=(FrameworkElement)popup.FindName("DialogSurface");bool blockClose=false,closed=false,transparentEnd=false;popup.Closing+=(s,e)=>{if(blockClose)e.Cancel=true;if(surface.Opacity<0.001&&popup.IsLoaded)transparentEnd=DialogPixelAlpha(popup,330,180)==0;};popup.Closed+=(s,e)=>closed=true;
  try{
   check(popup.AllowsTransparency&&((SolidColorBrush)popup.Background).Color.A==0&&popup.Opacity==1&&surface.Opacity==0,"Update popups prepare a transparent backing and hidden content before creating a native window");
   popup.Show();await Task.Delay(70);check(popup.Opacity==1&&surface.Opacity>0&&surface.Opacity<1&&surface.RenderTransform is ScaleTransform,"Update popups animate their panel while native window opacity stays unchanged");check(DialogPixelAlpha(popup,2,180)==0,"Opening popup edges reveal the owner instead of an opaque black backing");App.Render(popup,Path.Combine(output,"update-popup-opening.png"));await Task.Delay(220);check(surface.Opacity==1,"Update popup opening animation settles fully opaque");App.Render(popup,Path.Combine(output,"update-popup-motion.png"));
   blockClose=true;popup.Close();await Task.Delay(50);check(!closed&&popup.IsVisible&&popup.Opacity==1&&surface.Opacity==1&&surface.IsHitTestVisible,"A pending-download close guard prevents fading or disabling an update popup");
   blockClose=false;popup.Close();await Task.Delay(60);check(!closed&&popup.Opacity==1&&surface.Opacity>0&&surface.Opacity<1&&!surface.IsHitTestVisible,"Update popup close fades only the panel and blocks repeated interaction");App.Render(popup,Path.Combine(output,"update-popup-closing.png"));popup.Close();await Task.Delay(230);check(closed&&transparentEnd,"Repeated popup close requests finish on a transparent frame without exposing a black backing");
  }finally{UiMotion.SetEnabled(popup,false);if(popup.IsVisible)popup.Close();UiMotion.SetEnabled(Window,false);}
  popup=UpdateDialogs.Highlights(Window);popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=popup.Top=-12000;popup.ShowActivated=false;closed=false;popup.Closed+=(s,e)=>closed=true;popup.Show();check(popup.Opacity==1,"Motion-disabled update popups open immediately without becoming transparent");popup.Close();check(closed,"Motion-disabled update popups close immediately");
  UiMotion.SetEnabled(Window,true);
  try{
   foreach(bool light in new[]{false,true}){
    UiTheme.Set(light?"light":"dark",false);popup=UpdateDialogs.Highlights(Window);popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=popup.Top=-12000;popup.ShowActivated=false;surface=(FrameworkElement)popup.FindName("DialogSurface");bool rendered=false;Exception failure=null;var modal=popup;
    popup.Loaded+=async(s,e)=>{try{await Task.Delay(45);check(modal.Opacity==1&&surface.Opacity>0&&surface.Opacity<1,"Modal release notes animate on the transparent backing in the "+(light?"light":"dark")+" theme");rendered=true;modal.Close();modal.Close();}catch(Exception ex){failure=ex;UiMotion.SetEnabled(modal,false);modal.Close();}};
    popup.ShowDialog();check(rendered&&!popup.IsVisible&&Window.IsEnabled,"Closing modal release notes during their entrance restores owner interaction");if(failure!=null)throw failure;
   }
  }finally{UiTheme.Set("dark",false);UiMotion.SetEnabled(Window,false);if(popup.IsVisible){UiMotion.SetEnabled(popup,false);popup.Close();}}
 }
}
}
