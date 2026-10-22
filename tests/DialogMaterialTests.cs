using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
namespace ClearSpace {
public sealed partial class Controller {
 void VerifyDialogMaterials(string output,Action<bool,string> check){
  bool enabled=UiGlass.GetEnabled(Window);string mode=UiGlass.GetMode(Window),theme=UiTheme.Light?"light":"dark";
  try{foreach(string appearance in new[]{"light","dark"}){
   UiTheme.Set(appearance,false);UiGlass.SetMode(Window,"internal");UiGlass.SetEnabled(Window,true);Window.UpdateLayout();
   var popup=UpdateDialogs.Highlights(Window);UiMotion.SetEnabled(popup,false);popup.WindowStartupLocation=WindowStartupLocation.Manual;popup.Left=Window.Left+(Window.ActualWidth-popup.Width)/2;popup.Top=Window.Top+(Window.ActualHeight-popup.Height)/2;popup.ShowActivated=false;
   var frame=(GlassPanel)popup.FindName("DialogSurface");try{popup.Show();popup.UpdateLayout();var material=frame.Backdrop as ImageBrush;
    check(material!=null&&material.IsFrozen&&material.ImageSource.IsFrozen,"Release notes freeze a blurred view of their owner instead of keeping a live blur in "+appearance);
    check(DialogPixelAlpha(popup,2,180)==255&&DialogPixelAlpha(popup,330,180)==255&&DialogPixelAlpha(popup,0,0)==0,"Internal popup glass preserves opaque readable interiors and transparent rounded corners in "+appearance);
    App.Render(popup,Path.Combine(output,"release-notes-glass-"+appearance+".png"));
   }finally{popup.Close();}check(frame.Backdrop==null,"Closing release notes releases the captured owner surface in "+appearance);
  }
   UiGlass.SetEnabled(Window,false);var plain=UpdateDialogs.Highlights(Window);UiMotion.SetEnabled(plain,false);plain.WindowStartupLocation=WindowStartupLocation.Manual;plain.Left=Window.Left;plain.Top=Window.Top;plain.ShowActivated=false;try{plain.Show();plain.UpdateLayout();check(((GlassPanel)plain.FindName("DialogSurface")).Backdrop==null,"Disabling Liquid Glass skips popup snapshot and blur work entirely");}finally{plain.Close();}
  }finally{UiTheme.Set(theme,false);UiGlass.SetMode(Window,mode);UiGlass.SetEnabled(Window,enabled);}
 }
}
}
