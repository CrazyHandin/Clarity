using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
namespace ClearSpace {
// The compositor samples windows behind this HWND. No screen capture, global
// opacity, foreign window changes or extra runtime are involved.
internal sealed class NativeGlass {
 static readonly ConditionalWeakTable<Window,NativeGlass> states=new ConditionalWeakTable<Window,NativeGlass>();
 static readonly List<WeakReference> windows=new List<WeakReference>();
 readonly Window window;readonly Brush originalBackground;HwndSource source;IntPtr handle;Thickness originalGlass;bool applied,closed,queued;bool? lastLight;
 [StructLayout(LayoutKind.Sequential)]struct AccentPolicy {public int State,Flags,Color,Animation;}
 [StructLayout(LayoutKind.Sequential)]struct CompositionData {public int Attribute;public IntPtr Data;public int Size;}
 [StructLayout(LayoutKind.Sequential)]struct Margins {public int Left,Right,Top,Bottom;}
 [DllImport("user32.dll")]static extern bool SetWindowCompositionAttribute(IntPtr window,ref CompositionData data);
 [DllImport("dwmapi.dll")]static extern int DwmExtendFrameIntoClientArea(IntPtr window,ref Margins margins);
 [DllImport("dwmapi.dll")]static extern int DwmIsCompositionEnabled(out bool enabled);
 internal static Func<bool> Allowed=()=>!SystemParameters.HighContrast;
 internal static void Attach(Window window){NativeGlass state;if(!states.TryGetValue(window,out state)){state=new NativeGlass(window);states.Add(window,state);windows.Add(new WeakReference(window));}state.Apply();}
 internal static void RefreshAll(){foreach(var reference in windows.ToArray()){var window=reference.Target as Window;if(window==null){windows.Remove(reference);continue;}NativeGlass state;if(states.TryGetValue(window,out state))state.Apply();}}
 NativeGlass(Window window){
  this.window=window;originalBackground=window.Background;var chrome=WindowChrome.GetWindowChrome(window);originalGlass=chrome==null?new Thickness(0):chrome.GlassFrameThickness;
  window.SourceInitialized+=(s,e)=>Initialize();window.Loaded+=(s,e)=>Apply(true);window.Closed+=(s,e)=>{closed=true;if(source!=null&&!source.IsDisposed)source.RemoveHook(Message);states.Remove(window);windows.RemoveAll(w=>w.Target==window||!w.IsAlive);};
  if(new WindowInteropHelper(window).Handle!=IntPtr.Zero)Initialize();
 }
 void Initialize(){if(source!=null||closed)return;handle=new WindowInteropHelper(window).Handle;source=HwndSource.FromHwnd(handle);if(source!=null)source.AddHook(Message);Apply();}
 IntPtr Message(IntPtr hwnd,int message,IntPtr parameter,IntPtr data,ref bool handled){
  if((message==0x31E||message==0x31A||message==0x1A||message==0x232||message==0x7D||message==5)&&!queued&&!closed){queued=true;window.Dispatcher.BeginInvoke(new Action(()=>{queued=false;Apply(true);}));}return IntPtr.Zero;
 }
 static bool Accent(IntPtr handle,bool enabled){
  // Zero flags are essential: the tint flag makes the clear blur nearly opaque.
  var policy=new AccentPolicy{State=enabled?3:0};var memory=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AccentPolicy)));
  try{Marshal.StructureToPtr(policy,memory,false);var data=new CompositionData{Attribute=19,Data=memory,Size=Marshal.SizeOf(typeof(AccentPolicy))};return SetWindowCompositionAttribute(handle,ref data);}finally{Marshal.FreeHGlobal(memory);}
 }
 void Apply(bool force=false){
  if(closed||source==null||source.IsDisposed||handle==IntPtr.Zero)return;
  bool requested=UiGlass.GetEnabled(window)&&UiGlass.GetMode(window)=="full"&&Allowed(),composition=false;
  if(!requested&&!applied){RestoreBackground();return;}
  if(!force&&requested&&applied&&lastLight==UiTheme.Light)return;
  try{
   if(requested&&DwmIsCompositionEnabled(out composition)==0&&composition){
    var margins=new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1};bool ready=DwmExtendFrameIntoClientArea(handle,ref margins)==0;
    if(ready&&Accent(handle,true)){
     source.CompositionTarget.BackgroundColor=Colors.Transparent;window.Background=Brushes.Transparent;applied=true;lastLight=UiTheme.Light;UiGlass.SetHasBackdrop(window,true);return;
    }
   }
  }catch(EntryPointNotFoundException){}catch(DllNotFoundException){}catch(ExternalException){}
  Restore();
 }
 void Restore(){
  UiGlass.SetHasBackdrop(window,false);
  try{Accent(handle,false);var margins=new Margins();DwmExtendFrameIntoClientArea(handle,ref margins);}catch(EntryPointNotFoundException){}catch(DllNotFoundException){}
  var chrome=WindowChrome.GetWindowChrome(window);if(chrome!=null&&chrome.GlassFrameThickness!=originalGlass)chrome.GlassFrameThickness=originalGlass;
  RestoreBackground();applied=false;
 }
 void RestoreBackground(){UiGlass.SetHasBackdrop(window,false);window.Background=UiGlass.GetEnabled(window)&&UiGlass.GetMode(window)=="internal"&&!window.AllowsTransparency?App.Brush("#161D25"):originalBackground;if(!window.AllowsTransparency)source.CompositionTarget.BackgroundColor=Colors.Black;}
}
}
