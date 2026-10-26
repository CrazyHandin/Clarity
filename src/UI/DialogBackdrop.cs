using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace ClearSpace {
internal static class DialogBackdrop {
 sealed class State {
  internal readonly CancellationTokenSource Stop=new CancellationTokenSource();internal readonly CancellationToken Token;internal readonly TaskCompletionSource<int> Ready=new TaskCompletionSource<int>();
  internal BitmapSource Image;internal ImageBrush Pan;internal TranslateTransform Offset;internal int Revision,Position;internal bool Closed,Moving,Preparing,Refresh;internal DispatcherOperation Capture;internal CancellationTokenSource Detail;internal Task DetailWork=Task.FromResult(0);
  internal State(){Token=Stop.Token;}
 }
 static readonly ConditionalWeakTable<Window,State> states=new ConditionalWeakTable<Window,State>();
 internal static Task WhenReady(Window window){State state;return states.TryGetValue(window,out state)?AwaitReady(state):Task.FromResult(0);}
 static async Task AwaitReady(State state){await state.Ready.Task;await state.DetailWork;}
 static Brush Material(BitmapSource image,Rect region){double sx=image.DpiX/96,sy=image.DpiY/96;var brush=new ImageBrush(image){Stretch=Stretch.Fill,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(Math.Floor(region.X*sx)/sx-region.X,Math.Floor(region.Y*sy)/sy-region.Y,image.Width,image.Height)};brush.Freeze();return brush;}
 static Rect Region(Window owner,GlassPanel frame){var visual=(FrameworkElement)owner.Content;var origin=visual.PointFromScreen(frame.PointToScreen(new Point()));return new Rect(origin,new Size(frame.ActualWidth,frame.ActualHeight));}
 static async Task Refine(Window window,GlassPanel frame,Window owner,State state){
  if(state.Image==null||state.Closed||state.Moving||!UiGlass.Enhanced(frame))return;if(state.Detail!=null){state.Detail.Cancel();state.Detail.Dispose();}state.Detail=CancellationTokenSource.CreateLinkedTokenSource(state.Token);var token=state.Detail.Token;int position=state.Position;var image=state.Image;var region=Region(owner,frame);double radius=frame.CornerRadius.TopLeft;
  try{var result=await Task.Run(()=>{var crop=GlassBackground.Crop(image,region,token);return crop==null?null:GlassRefraction.Apply(crop,radius,token);},token);
   if(result!=null&&!state.Closed&&!state.Moving&&state.Position==position&&ReferenceEquals(state.Image,image)&&UiGlass.Enhanced(frame))frame.Backdrop=Material(result,region);
  }catch(OperationCanceledException){}catch{/* Keep the readable cached material. */}
 }
 internal static void Attach(Window window,GlassPanel frame,Window owner){
  HwndSource source=null;HwndSourceHook hook=null;var state=new State();states.Add(window,state);
  // Keep one retained brush. Panning changes only two compositor coordinates;
  // it neither rebuilds the panel drawing nor waits for dispatcher timer ticks.
  Action update=()=>{if(state.Closed||state.Image==null||!window.IsVisible||!UiGlass.Active(frame))return;try{
   var region=Region(owner,frame);if(state.Pan==null||!ReferenceEquals(state.Pan.ImageSource,state.Image)){state.Offset=new TranslateTransform();state.Pan=new ImageBrush(state.Image){ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,state.Image.Width,state.Image.Height),Stretch=Stretch.Fill,Transform=state.Offset};}
   state.Offset.X=-region.X;state.Offset.Y=-region.Y;if(!ReferenceEquals(frame.Backdrop,state.Pan))frame.Backdrop=state.Pan;
  }catch{/* Retain the last valid position during native layout changes. */}};
  Action queue=()=>{if(state.Closed)return;state.Position++;if(state.Detail!=null){state.Detail.Cancel();state.Detail.Dispose();state.Detail=null;}update();};
  Action capture=null;capture=()=>{
   if(state.Closed||!UiGlass.Active(frame)){state.Ready.TrySetResult(0);return;}if(state.Preparing){state.Refresh=true;return;}state.Preparing=true;int revision=++state.Revision;
   state.Capture=window.Dispatcher.BeginInvoke(new Action(async()=>{
    try{
     var visual=owner.Content as FrameworkElement;if(state.Closed||visual==null||!owner.IsVisible||visual.ActualWidth<=0||visual.ActualHeight<=0)return;
     double scale=Math.Min(0.5,Math.Sqrt(GlassRefraction.MaxPixels/(visual.ActualWidth*visual.ActualHeight)));int width=Math.Max(1,(int)Math.Floor(visual.ActualWidth*scale)),height=Math.Max(1,(int)Math.Floor(visual.ActualHeight*scale));
     var bounds=new Rect(0,0,visual.ActualWidth,visual.ActualHeight);var drawing=new DrawingVisual();using(var context=drawing.RenderOpen()){context.DrawRectangle(App.Brush("#161D25"),null,bounds);context.DrawRectangle(new VisualBrush(visual){AutoLayoutContent=false,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=bounds,Stretch=Stretch.Fill},null,bounds);}
     var snapshot=new RenderTargetBitmap(width,height,96*width/visual.ActualWidth,96*height/visual.ActualHeight,PixelFormats.Pbgra32);snapshot.Render(drawing);snapshot.Freeze();
     var blurred=await GlassBackground.BlurAsync(snapshot,UiGlass.Enhanced(frame)?12:16,state.Token);
     if(state.Closed||state.Revision!=revision||blurred==null||!UiGlass.Active(frame))return;state.Image=blurred;update();state.DetailWork=Refine(window,frame,owner,state);await state.DetailWork;
    }catch(OperationCanceledException){}catch{/* The neutral material stays visible if capture is unavailable. */}finally{state.Preparing=false;if(state.Refresh&&!state.Closed){state.Refresh=false;capture();}else state.Ready.TrySetResult(0);}
   }),DispatcherPriority.Background);
  };
  EventHandler moved=(s,e)=>queue();SizeChangedEventHandler resized=(s,e)=>queue();SizeChangedEventHandler ownerResized=(s,e)=>{if(state.Capture==null||state.Capture.Status!=DispatcherOperationStatus.Pending)capture();};
  window.LocationChanged+=moved;window.SizeChanged+=resized;owner.LocationChanged+=moved;owner.SizeChanged+=ownerResized;
  window.Loaded+=(s,e)=>{capture();source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);if(source!=null){hook=(IntPtr handle,int message,IntPtr w,IntPtr l,ref bool handled)=>{if(message==0x231){state.Moving=true;queue();}else if(message==0x232){state.Moving=false;queue();state.DetailWork=Refine(window,frame,owner,state);}return IntPtr.Zero;};source.AddHook(hook);}};
  window.Closed+=(s,e)=>{state.Closed=true;state.Stop.Cancel();state.Stop.Dispose();if(state.Detail!=null){state.Detail.Dispose();state.Detail=null;}if(state.Capture!=null&&state.Capture.Status==DispatcherOperationStatus.Pending)state.Capture.Abort();state.Ready.TrySetResult(0);states.Remove(window);state.Image=null;state.Pan=null;state.Offset=null;frame.Backdrop=null;window.LocationChanged-=moved;window.SizeChanged-=resized;owner.LocationChanged-=moved;owner.SizeChanged-=ownerResized;if(source!=null&&!source.IsDisposed&&hook!=null)source.RemoveHook(hook);};
 }
}
}
