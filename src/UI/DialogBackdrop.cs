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
  internal BitmapSource Image;internal int Revision,Position;internal bool Closed,Moving,Preparing,Refresh;internal DispatcherOperation Capture;internal CancellationTokenSource Detail;
  internal State(){Token=Stop.Token;}
 }
 static readonly ConditionalWeakTable<Window,State> states=new ConditionalWeakTable<Window,State>();
 internal static Task WhenReady(Window window){State state;return states.TryGetValue(window,out state)?state.Ready.Task:Task.FromResult(0);}
 static Brush Material(BitmapSource image,Rect? region=null){var brush=new ImageBrush(image){Stretch=Stretch.Fill};if(region.HasValue){brush.ViewboxUnits=BrushMappingMode.Absolute;brush.Viewbox=region.Value;}brush.Freeze();return brush;}
 static Rect Region(Window owner,GlassPanel frame){var visual=(FrameworkElement)owner.Content;var origin=visual.PointFromScreen(frame.PointToScreen(new Point()));return new Rect(origin,new Size(frame.ActualWidth,frame.ActualHeight));}
 static async Task Refine(Window window,GlassPanel frame,Window owner,State state){
  if(state.Image==null||state.Closed||state.Moving||!UiGlass.Enhanced(frame))return;if(state.Detail!=null){state.Detail.Cancel();state.Detail.Dispose();}state.Detail=CancellationTokenSource.CreateLinkedTokenSource(state.Token);var token=state.Detail.Token;int position=state.Position;var image=state.Image;var region=Region(owner,frame);double radius=frame.CornerRadius.TopLeft;
  try{var result=await Task.Run(()=>{var crop=GlassBackground.Crop(image,region,token);return crop==null?null:GlassRefraction.Apply(crop,radius,token);},token);
   if(result!=null&&!state.Closed&&!state.Moving&&state.Position==position&&ReferenceEquals(state.Image,image)&&UiGlass.Enhanced(frame))frame.Backdrop=Material(result);
  }catch(OperationCanceledException){}catch{/* Keep the readable cached material. */}
 }
 internal static void Attach(Window window,GlassPanel frame,Window owner){
  HwndSource source=null;HwndSourceHook hook=null;var state=new State();states.Add(window,state);
  Action update=()=>{if(state.Closed||state.Image==null||!window.IsVisible||!UiGlass.Active(frame))return;try{frame.Backdrop=Material(state.Image,Region(owner,frame));}catch{/* Retain the last valid crop during native layout changes. */}};
  // Event-driven and rate-limited: no timer or image work continues while idle.
  var pulse=new DispatcherTimer(DispatcherPriority.Render,window.Dispatcher){Interval=TimeSpan.FromMilliseconds((RenderCapability.Tier>>16)>=2?16:33)};pulse.Tick+=(s,e)=>{pulse.Stop();update();};
  Action queue=()=>{if(state.Closed)return;state.Position++;if(state.Detail!=null){state.Detail.Cancel();state.Detail.Dispose();state.Detail=null;}if(!pulse.IsEnabled)pulse.Start();};
  Action capture=null;capture=()=>{
   if(state.Closed||!UiGlass.Active(frame)){state.Ready.TrySetResult(0);return;}if(state.Preparing){state.Refresh=true;return;}state.Preparing=true;int revision=++state.Revision;
   state.Capture=window.Dispatcher.BeginInvoke(new Action(async()=>{
    try{
     var visual=owner.Content as FrameworkElement;if(state.Closed||visual==null||!owner.IsVisible||visual.ActualWidth<=0||visual.ActualHeight<=0)return;
     double scale=Math.Min(0.5,Math.Sqrt(GlassRefraction.MaxPixels/(visual.ActualWidth*visual.ActualHeight)));int width=Math.Max(1,(int)Math.Floor(visual.ActualWidth*scale)),height=Math.Max(1,(int)Math.Floor(visual.ActualHeight*scale));
     var bounds=new Rect(0,0,visual.ActualWidth,visual.ActualHeight);var drawing=new DrawingVisual();using(var context=drawing.RenderOpen()){context.DrawRectangle(App.Brush("#161D25"),null,bounds);context.DrawRectangle(new VisualBrush(visual){AutoLayoutContent=false,ViewboxUnits=BrushMappingMode.Absolute,Viewbox=bounds,Stretch=Stretch.Fill},null,bounds);}
     var snapshot=new RenderTargetBitmap(width,height,96*width/visual.ActualWidth,96*height/visual.ActualHeight,PixelFormats.Pbgra32);snapshot.Render(drawing);snapshot.Freeze();
     var blurred=await GlassBackground.BlurAsync(snapshot,UiGlass.Enhanced(frame)?12:16,state.Token);
     if(state.Closed||state.Revision!=revision||blurred==null||!UiGlass.Active(frame))return;state.Image=blurred;pulse.Stop();update();await Refine(window,frame,owner,state);
    }catch(OperationCanceledException){}catch{/* The neutral material stays visible if capture is unavailable. */}finally{state.Preparing=false;if(state.Refresh&&!state.Closed){state.Refresh=false;capture();}else state.Ready.TrySetResult(0);}
   }),DispatcherPriority.Background);
  };
  EventHandler moved=(s,e)=>queue();SizeChangedEventHandler resized=(s,e)=>queue();SizeChangedEventHandler ownerResized=(s,e)=>{if(state.Capture==null||state.Capture.Status!=DispatcherOperationStatus.Pending)capture();};
  window.LocationChanged+=moved;window.SizeChanged+=resized;owner.LocationChanged+=moved;owner.SizeChanged+=ownerResized;
  window.Loaded+=(s,e)=>{capture();source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);if(source!=null){hook=(IntPtr handle,int message,IntPtr w,IntPtr l,ref bool handled)=>{if(message==0x231){state.Moving=true;queue();}else if(message==0x232){state.Moving=false;queue();pulse.Stop();window.Dispatcher.BeginInvoke(new Action(async()=>{if(!state.Closed){update();await Refine(window,frame,owner,state);}}),DispatcherPriority.Background);}return IntPtr.Zero;};source.AddHook(hook);}};
  window.Closed+=(s,e)=>{state.Closed=true;pulse.Stop();state.Stop.Cancel();state.Stop.Dispose();if(state.Detail!=null){state.Detail.Dispose();state.Detail=null;}if(state.Capture!=null&&state.Capture.Status==DispatcherOperationStatus.Pending)state.Capture.Abort();state.Ready.TrySetResult(0);states.Remove(window);state.Image=null;frame.Backdrop=null;window.LocationChanged-=moved;window.SizeChanged-=resized;owner.LocationChanged-=moved;owner.SizeChanged-=ownerResized;if(source!=null&&!source.IsDisposed&&hook!=null)source.RemoveHook(hook);};
 }
}
}
