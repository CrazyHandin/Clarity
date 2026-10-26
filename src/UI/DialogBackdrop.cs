using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
namespace ClearSpace {
// Capture only this app's owner surface once. The blurred pixels are frozen;
// no live cross-window brush, desktop capture or persistent blur effect remains.
internal static class DialogBackdrop {
 sealed class State {internal int Revision;internal readonly CancellationTokenSource Stop=new CancellationTokenSource();internal readonly CancellationToken Token;internal Task Pending=Task.FromResult(0);internal State(){Token=Stop.Token;}}
 static readonly ConditionalWeakTable<Window,State> states=new ConditionalWeakTable<Window,State>();
 internal static Task WhenReady(Window window){State state;return states.TryGetValue(window,out state)?state.Pending:Task.FromResult(0);}
 static Brush Material(BitmapSource bitmap){var brush=new ImageBrush(bitmap){Stretch=Stretch.Fill};brush.Freeze();return brush;}
 static async Task Refine(Window window,GlassPanel frame,State state,BitmapSource image,double radius,int revision){
  try{var result=await GlassRefraction.RenderAsync(image,radius,state.Token).ConfigureAwait(false);if(ReferenceEquals(result,image)||state.Token.IsCancellationRequested||window.Dispatcher.HasShutdownStarted)return;
   await window.Dispatcher.InvokeAsync(new Action(()=>{if(!state.Token.IsCancellationRequested&&state.Revision==revision&&window.IsVisible&&UiGlass.Enhanced(frame))frame.Backdrop=Material(result);}),System.Windows.Threading.DispatcherPriority.Background);
  }catch(OperationCanceledException){}catch{/* The immediately available frozen blur remains readable. */}
 }
 internal static void Attach(Window window,GlassPanel frame,Window owner){
  HwndSource source=null;HwndSourceHook hook=null;var state=new State();states.Add(window,state);
  Action capture=()=>{if(!UiGlass.Active(frame))return;try{var visual=owner.Content as FrameworkElement;if(visual==null||!owner.IsVisible||frame.ActualWidth<=0||frame.ActualHeight<=0)return;
   Point origin=visual.PointFromScreen(frame.PointToScreen(new Point()));var region=new Rect(origin,new Size(frame.ActualWidth,frame.ActualHeight));if(!region.IntersectsWith(new Rect(0,0,visual.ActualWidth,visual.ActualHeight)))return;
   double dpiX=Math.Min(1,Math.Sqrt(GlassRefraction.MaxPixels/(frame.ActualWidth*frame.ActualHeight))),dpiY=dpiX;int width=Math.Max(1,(int)Math.Floor(frame.ActualWidth*dpiX)),height=Math.Max(1,(int)Math.Floor(frame.ActualHeight*dpiY));int revision=++state.Revision;
   var drawing=new DrawingVisual();using(var context=drawing.RenderOpen()){context.DrawRectangle(App.Brush("#161D25"),null,new Rect(0,0,frame.ActualWidth,frame.ActualHeight));var brush=new VisualBrush(visual){AutoLayoutContent=false,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=region,Stretch=Stretch.Fill};context.DrawRectangle(brush,null,new Rect(0,0,frame.ActualWidth,frame.ActualHeight));}
   var snapshot=new RenderTargetBitmap(width,height,96*dpiX,96*dpiY,PixelFormats.Pbgra32);snapshot.Render(drawing);snapshot.Freeze();
   var image=new System.Windows.Controls.Image{Source=snapshot,Stretch=Stretch.Fill,Effect=new BlurEffect{Radius=UiGlass.Enhanced(frame)?12:16,RenderingBias=RenderingBias.Performance}};image.Measure(new Size(frame.ActualWidth,frame.ActualHeight));image.Arrange(new Rect(0,0,frame.ActualWidth,frame.ActualHeight));
   var blurred=new RenderTargetBitmap(width,height,96*dpiX,96*dpiY,PixelFormats.Pbgra32);blurred.Render(image);blurred.Freeze();frame.Backdrop=Material(blurred);state.Pending=UiGlass.Enhanced(frame)?Refine(window,frame,state,blurred,frame.CornerRadius.TopLeft,revision):Task.FromResult(0);
  }catch{/* Keep the neutral, readable material if a snapshot is unavailable. */}};
  window.Loaded+=(s,e)=>{capture();source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);if(source!=null){hook=(IntPtr handle,int message,IntPtr w,IntPtr l,ref bool handled)=>{if(message==0x232)capture();return IntPtr.Zero;};source.AddHook(hook);}};
  window.Closed+=(s,e)=>{state.Stop.Cancel();state.Stop.Dispose();states.Remove(window);frame.Backdrop=null;if(source!=null&&!source.IsDisposed&&hook!=null)source.RemoveHook(hook);};
 }
}
}
