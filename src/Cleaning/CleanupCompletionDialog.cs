using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
namespace ClearSpace {
// Only acknowledge the localized, informational completion dialog owned by the process we launched.
// Never accept a selection/confirmation dialog and never terminate cleanmgr.
public sealed class CleanupCompletionDialog {
 readonly string title,template;
 public CleanupCompletionDialog(){
  string root=Environment.GetFolderPath(Environment.SpecialFolder.System);
  foreach(string file in new[]{Path.Combine(root,CultureInfo.CurrentUICulture.Name,"cleanmgr.exe.mui"),Path.Combine(root,"cleanmgr.exe")}){
   IntPtr module=LoadLibraryEx(file,IntPtr.Zero,0x22);if(module==IntPtr.Zero)continue;
   try{title=Read(module,1206);template=Read(module,1208);if(!String.IsNullOrEmpty(title)&&!String.IsNullOrEmpty(template))break;}finally{FreeLibrary(module);}
  }
 }
 static string Read(IntPtr module,uint id){var value=new StringBuilder(2048);return LoadString(module,id,value,value.Capacity)>0?value.ToString():null;}
 public static bool Matches(uint expectedPid,uint pid,string caption,string body,string title,string template,bool hasOk,bool hasCancel){
  if(pid==0||pid!=expectedPid||!hasOk||hasCancel||String.IsNullOrWhiteSpace(title)||String.IsNullOrWhiteSpace(template)||caption!=title)return false;
  string pattern="^"+Regex.Escape(template.Trim()).Replace("%s",@"[^\r\n]{1,128}").Replace("%d",@"[\d.,\s]+")+"$";
  return Regex.IsMatch((body??"").Trim(),pattern,RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(50));
 }
 public void Acknowledge(int processId){
  if(String.IsNullOrEmpty(title)||String.IsNullOrEmpty(template))return;
  EnumWindows((window,unused)=>{try{
   uint pid;GetWindowThreadProcessId(window,out pid);if(pid!=(uint)processId||Text(window)!=title)return true;
   var className=new StringBuilder(128);GetClassName(window,className,className.Capacity);if(className.ToString()!="#32770")return true;
   IntPtr ok=GetDlgItem(window,1);bool match=false;
   EnumChildWindows(window,(child,data)=>{if(Matches((uint)processId,pid,Text(window),Text(child),title,template,ok!=IntPtr.Zero,GetDlgItem(window,2)!=IntPtr.Zero))match=true;return true;},IntPtr.Zero);
   uint current;GetWindowThreadProcessId(window,out current);if(match&&current==pid&&GetDlgItem(window,1)==ok)PostMessage(window,0x111,new IntPtr(1),ok);
  }catch{}return true;},IntPtr.Zero);
 }
 static string Text(IntPtr window){var text=new StringBuilder(4096);GetWindowText(window,text,text.Capacity);return text.ToString();}
 delegate bool WindowCallback(IntPtr window,IntPtr state);
 [DllImport("user32.dll")]static extern bool EnumWindows(WindowCallback callback,IntPtr state);
 [DllImport("user32.dll")]static extern bool EnumChildWindows(IntPtr window,WindowCallback callback,IntPtr state);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr window,StringBuilder text,int count);
 [DllImport("user32.dll")]static extern IntPtr GetDlgItem(IntPtr window,int id);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibraryEx(string file,IntPtr reserved,uint flags);
 [DllImport("kernel32.dll")]static extern bool FreeLibrary(IntPtr module);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int LoadString(IntPtr module,uint id,StringBuilder value,int count);
}
}
