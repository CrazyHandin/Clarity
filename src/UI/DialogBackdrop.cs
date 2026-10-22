using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
namespace ClearSpace {
// Capture only this app's owner surface once. The blurred pixels are frozen;
// no live cross-window brush, desktop capture or persistent blur effect remains.
internal static class DialogBackdrop {
 internal static void Attach(Window window,GlassPanel frame,Window owner){
  HwndSource source=null;HwndSourceHook hook=null;
  Action capture=()=>{if(!UiGlass.Active(frame))return;try{var visual=owner.Content as FrameworkElement;if(visual==null||!owner.IsVisible||frame.ActualWidth<=0||frame.ActualHeight<=0)return;
   Point origin=visual.PointFromScreen(frame.PointToScreen(new Point()));var region=new Rect(origin,new Size(frame.ActualWidth,frame.ActualHeight));if(!region.IntersectsWith(new Rect(0,0,visual.ActualWidth,visual.ActualHeight)))return;
   var matrix=PresentationSource.FromVisual(frame).CompositionTarget.TransformToDevice;double dpiX=Math.Max(1,matrix.M11),dpiY=Math.Max(1,matrix.M22);int width=(int)Math.Ceiling(frame.ActualWidth*dpiX),height=(int)Math.Ceiling(frame.ActualHeight*dpiY);if((long)width*height>2000000)return;
   var drawing=new DrawingVisual();using(var context=drawing.RenderOpen()){context.DrawRectangle(App.Brush("#161D25"),null,new Rect(0,0,frame.ActualWidth,frame.ActualHeight));var brush=new VisualBrush(visual){AutoLayoutContent=false,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=region,Stretch=Stretch.Fill};context.DrawRectangle(brush,null,new Rect(0,0,frame.ActualWidth,frame.ActualHeight));}
   var snapshot=new RenderTargetBitmap(width,height,96*dpiX,96*dpiY,PixelFormats.Pbgra32);snapshot.Render(drawing);snapshot.Freeze();
   var image=new System.Windows.Controls.Image{Source=snapshot,Stretch=Stretch.Fill,Effect=new BlurEffect{Radius=16,RenderingBias=RenderingBias.Performance}};image.Measure(new Size(frame.ActualWidth,frame.ActualHeight));image.Arrange(new Rect(0,0,frame.ActualWidth,frame.ActualHeight));
   var blurred=new RenderTargetBitmap(width,height,96*dpiX,96*dpiY,PixelFormats.Pbgra32);blurred.Render(image);blurred.Freeze();var material=new ImageBrush(blurred){Stretch=Stretch.Fill};material.Freeze();frame.Backdrop=material;
  }catch{/* Keep the neutral, readable material if a snapshot is unavailable. */}};
  window.Loaded+=(s,e)=>{capture();source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);if(source!=null){hook=(IntPtr handle,int message,IntPtr w,IntPtr l,ref bool handled)=>{if(message==0x232)capture();return IntPtr.Zero;};source.AddHook(hook);}};
  window.Closed+=(s,e)=>{frame.Backdrop=null;if(source!=null&&!source.IsDisposed&&hook!=null)source.RemoveHook(hook);};
 }
}
}
