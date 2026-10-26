using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
namespace ClearSpace {
public sealed partial class Controller {
 [DllImport("user32.dll",EntryPoint="SendMessageW")]static extern IntPtr GlassPanMessage(IntPtr window,int message,IntPtr w,IntPtr l);
 async Task VerifyMovingDialogBackdrop(string output,Action<bool,string> check){
  var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());var left=new Border{Background=new SolidColorBrush(Color.FromRgb(30,130,110))};var right=new Border{Background=new SolidColorBrush(Color.FromRgb(185,95,45))};Grid.SetColumn(right,1);grid.Children.Add(left);grid.Children.Add(right);
  var owner=new Window{Content=grid,Width=1500,Height=650,Resources=Window.Resources,ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-14000,Top=-14000};var available=UiGlass.OpticsAvailable;var previous=UiTheme.Light?"light":"dark";Window popup=null;
  try{
   UiTheme.Set("dark",false);UiGlass.OpticsAvailable=()=>true;UiGlass.SetMode(owner,"internal");UiGlass.SetEnabled(owner,true);owner.Show();owner.UpdateLayout();
   foreach(string effect in new[]{"light","enhanced"}){
    UiGlass.SetEffect(owner,effect);popup=UpdateDialogs.Highlights(owner);popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=owner.Left+35;popup.Top=owner.Top+90;popup.ShowActivated=false;UiMotion.SetEnabled(popup,false);var frame=(GlassPanel)popup.FindName("DialogSurface");popup.Show();
    check(frame.Backdrop==null,"The "+effect+" popup opens without waiting for capture or blur on its Loaded event");await DialogBackdrop.WhenReady(popup);check(frame.Backdrop!=null,"The "+effect+" popup receives its background after asynchronous preparation");
    var handle=new WindowInteropHelper(popup).Handle;GlassPanMessage(handle,0x231,IntPtr.Zero,IntPtr.Zero);popup.Left+=5;await popup.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);var first=(ImageBrush)frame.Backdrop;var source=(BitmapSource)first.ImageSource;var transform=(TranslateTransform)first.Transform;double origin=transform.X;var before=DialogCenterColor(popup);int redraws=frame.Redraws;
    popup.Left+=720;var moved=(ImageBrush)frame.Backdrop;check(ReferenceEquals(first,moved)&&ReferenceEquals(transform,moved.Transform)&&Math.Abs(transform.X-origin+720)<1,"Moving a "+effect+" popup updates its existing background immediately without a timer or replacement brush");await popup.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);var after=DialogCenterColor(popup);
    check(frame.Redraws==redraws,"Panning a "+effect+" background does not rebuild the popup surface drawing");
    check(before.G>before.R&&after.R>after.G,"The "+effect+" popup reveals the new background beneath its current position");
    check(source.IsFrozen&&source.PixelWidth*(long)source.PixelHeight<=GlassRefraction.MaxPixels&&source.DpiX<=48.01,"The "+effect+" background remains cached at half resolution within its pixel budget");
    GlassPanMessage(handle,0x232,IntPtr.Zero,IntPtr.Zero);await DialogBackdrop.WhenReady(popup);var settled=DialogCenterColor(popup);check(Math.Abs(settled.R-after.R)<=2&&Math.Abs(settled.G-after.G)<=2&&Math.Abs(settled.B-after.B)<=2,"Settling a "+effect+" popup retains the current background position");
    App.Render(popup,Path.Combine(output,"moving-popup-"+effect+".png"));GlassPanMessage(handle,0x231,IntPtr.Zero,IntPtr.Zero);popup.Left=owner.Left+owner.ActualWidth+60;await popup.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);check(DialogPixelAlpha(popup,330,180)==255,"Dragging "+effect+" glass beyond its owner preserves the opaque internal floor");
    popup.Close();check(frame.Backdrop==null,"Closing a moving "+effect+" popup releases the cached background");popup=null;
   }
   popup=UpdateDialogs.Highlights(owner);UiMotion.SetEnabled(popup,false);popup.ShowActivated=false;popup.Show();var pending=DialogBackdrop.WhenReady(popup);var abandoned=(GlassPanel)popup.FindName("DialogSurface");popup.Close();await pending;await Task.Delay(25);check(abandoned.Backdrop==null,"Closing before background preparation cancels queued work without reattaching pixels");popup=null;
   var pixels=new byte[31*19*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=40;pixels[i+1]=70;pixels[i+2]=90;pixels[i+3]=128;}var image=BitmapSource.Create(31,19,48,48,PixelFormats.Pbgra32,null,pixels,31*4);image.Freeze();var blurred=await GlassBackground.BlurAsync(image,16,CancellationToken.None);var result=new byte[pixels.Length];blurred.CopyPixels(result,31*4,0);check(System.Linq.Enumerable.SequenceEqual(pixels,result)&&blurred.IsFrozen,"Background blur preserves uniform premultiplied colors without dark seams");
  }finally{if(popup!=null){UiMotion.SetEnabled(popup,false);popup.Close();}owner.Close();UiGlass.OpticsAvailable=available;UiTheme.Set(previous,false);}
 }
 static Color DialogCenterColor(Window popup){var image=new RenderTargetBitmap((int)popup.ActualWidth,(int)popup.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(popup);var pixel=new byte[4];image.CopyPixels(new Int32Rect(330,180,1,1),pixel,4,0);return Color.FromArgb(pixel[3],pixel[2],pixel[1],pixel[0]);}
}
}
