using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
namespace ClearSpace {
public sealed partial class Controller {
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int WindowStyleFlags(IntPtr window,int index);
 [DllImport("user32.dll")]static extern bool GetLayeredWindowAttributes(IntPtr window,out uint color,out byte alpha,out uint flags);
 async Task VerifyWindowChrome(string output,Action<bool,string> check){
  var fixture=new Controller();var window=fixture.Window;window.ShowInTaskbar=false;window.ShowActivated=false;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=window.Top=-12000;UiMotion.SetEnabled(window,true);bool blockClose=false,closed=false;window.Closed+=(s,e)=>closed=true;
  try{
   window.Show();await Task.Delay(60);var handle=new WindowInteropHelper(window).Handle;int style=WindowStyleFlags(handle,-16);var chrome=System.Windows.Shell.WindowChrome.GetWindowChrome(window);check((style&0x00C00000)==0x00C00000&&(style&0x00020000)!=0&&!window.AllowsTransparency&&chrome!=null&&chrome.GlassFrameThickness==new Thickness(0),"The custom main frame retains native caption and minimize styles without transparent-window rendering");
   var root=fixture.Get<Grid>("WindowRoot");fixture.Get<Button>("MinimizeWindow").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(100);check(window.WindowState==WindowState.Minimized&&window.Opacity==1&&root.Opacity==1&&root.RenderTransform.Value.IsIdentity,"Minimize dispatches the native window command without fading or scaling away the app before Windows animates it");SystemCommands.RestoreWindow(window);await Task.Delay(100);check(window.WindowState==WindowState.Normal&&window.Opacity==1&&root.IsHitTestVisible,"Native restore returns to the saved window state without a second content animation");
   window.Closing+=(s,e)=>{if(blockClose)e.Cancel=true;};window.Close();await Task.Delay(70);uint color,flags;byte alpha;bool layered=GetLayeredWindowAttributes(handle,out color,out alpha,out flags);check(!closed&&window.Opacity==1&&root.Opacity==1&&layered&&(flags&2)!=0&&alpha>0&&alpha<255,"Closing fades the complete native window including its opaque backing while keeping its content intact; native alpha="+alpha+", closed="+closed+", native="+layered+", flags="+flags+", exstyle="+WindowStyleFlags(handle,-20).ToString("X8"));blockClose=true;await Task.Delay(260);check(!closed&&window.IsVisible&&window.Opacity==1&&root.IsHitTestVisible&&(WindowStyleFlags(handle,-20)&0x80000)==0,"A late close cancellation restores opacity, native window styles and interaction");
   blockClose=false;window.Close();await Task.Delay(60);window.Close();await Task.Delay(260);check(closed,"A retry and repeated close requests finish one complete exit transition");
  }finally{blockClose=false;UiMotion.SetEnabled(window,false);if(window.IsVisible)window.Close();}
 }
}
}
