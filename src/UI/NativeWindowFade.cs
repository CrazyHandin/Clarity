using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
namespace ClearSpace {
internal sealed class NativeWindowFade:Animatable {
 const int ExtendedStyle=-20,Layered=0x80000;const uint AlphaFlag=2;
 IntPtr handle;HwndSource source;HwndSourceHook styleHook;bool hadLayered,finished;uint originalColor,originalFlags;byte originalAlpha=255;
 internal static readonly DependencyProperty LevelProperty=DependencyProperty.Register("Level",typeof(double),typeof(NativeWindowFade),new PropertyMetadata(1.0,(target,args)=>((NativeWindowFade)target).SetLevel((double)args.NewValue)));
 [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int GetStyle(IntPtr window,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern int SetStyle(IntPtr window,int index,int value);
 [DllImport("user32.dll")]static extern bool GetLayeredWindowAttributes(IntPtr window,out uint color,out byte alpha,out uint flags);
 [DllImport("user32.dll")]static extern bool SetLayeredWindowAttributes(IntPtr window,uint color,byte alpha,uint flags);
 protected override Freezable CreateInstanceCore(){return new NativeWindowFade();}
 internal static NativeWindowFade Create(Window window){
  var fade=new NativeWindowFade{handle=new WindowInteropHelper(window).Handle};if(fade.handle==IntPtr.Zero||window.AllowsTransparency)return null;int style=GetStyle(fade.handle,ExtendedStyle);fade.hadLayered=(style&Layered)!=0;
  if(fade.hadLayered&&!GetLayeredWindowAttributes(fade.handle,out fade.originalColor,out fade.originalAlpha,out fade.originalFlags))return null;
  fade.source=HwndSource.FromHwnd(fade.handle);if(fade.source==null)return null;
  // WPF normally clears system alpha styles. Retain it only on this window during exit.
  fade.styleHook=(IntPtr hwnd,int message,IntPtr parameter,IntPtr data,ref bool handled)=>{if(message==0x7C&&parameter.ToInt64()==ExtendedStyle&&data!=IntPtr.Zero){Marshal.WriteInt32(data,4,Marshal.ReadInt32(data,4)|Layered);handled=true;}return IntPtr.Zero;};fade.source.AddHook(fade.styleHook);
  if(!fade.hadLayered)SetStyle(fade.handle,ExtendedStyle,style|Layered);
  if((GetStyle(fade.handle,ExtendedStyle)&Layered)==0||!fade.SetLevel(1)){fade.Restore();return null;}return fade;
 }
 bool SetLevel(double value){return !finished&&SetLayeredWindowAttributes(handle,originalColor,(byte)Math.Round(originalAlpha*Math.Max(0,Math.Min(1,value))),originalFlags|AlphaFlag);}
 internal void Restore(){
  if(finished)return;Finish();
  SetLayeredWindowAttributes(handle,originalColor,originalAlpha,originalFlags|AlphaFlag);
  if(hadLayered)SetLayeredWindowAttributes(handle,originalColor,originalAlpha,originalFlags);else SetStyle(handle,ExtendedStyle,GetStyle(handle,ExtendedStyle)&~Layered);
 }
 internal void Finish(){finished=true;BeginAnimation(LevelProperty,null);if(source!=null&&!source.IsDisposed)source.RemoveHook(styleHook);}
}
}
