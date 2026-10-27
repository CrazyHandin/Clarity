using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 static bool CaptionCentered(Control control,string part,FrameworkElement reference=null){
  control.ApplyTemplate();var caption=control.Template.FindName(part,control)as ContentPresenter;if(caption==null)return false;
  reference=reference??control;Point origin=caption.TranslatePoint(new Point(),reference);
  bool centered=Math.Abs(origin.X+caption.ActualWidth/2-reference.ActualWidth/2)<=1.25&&Math.Abs(origin.Y+caption.ActualHeight/2-reference.ActualHeight/2)<=1.25;
  return centered&&VisualChildren<TextBlock>(caption).All(t=>t.ActualHeight+1.25>=t.FontSize*t.FontFamily.LineSpacing);
 }
 async Task VerifyControlAlignment(string output,Action<bool,string> check){
  string language=UiText.Language,theme=UiTheme.Light?"light":"dark";double width=Window.Width,height=Window.Height;
  try{
   ShowReady();Window.Height=880;
   foreach(string current in new[]{"zh-CN","en"}){
    UiText.SetLanguage(current);UiTheme.Set(current=="en"?"light":"dark",false);
    foreach(double size in new[]{1280.0,1040.0}){
     Window.Width=size;Window.UpdateLayout();var all=Get<Button>("SelectAllScopes");var clear=Get<Button>("ClearScopes");
     check(new[]{scan,all,clear}.All(b=>CaptionCentered(b,"ButtonCaption")),"Primary and scope-action captions are centered and unclipped in "+current+" at "+size);
     var actions=Get<StackPanel>("ScopeActions");var heading=Get<StackPanel>("ScopeHeading");var toolbar=Get<Grid>("ScopeToolbar");Point action=actions.TranslatePoint(new Point(),toolbar),mode=scanMode.TranslatePoint(new Point(),toolbar);
     check(all.Parent==actions&&clear.Parent==actions&&actions.Parent==heading&&action.X+actions.ActualWidth+12<=mode.X,"Scope batch actions stay beside their heading and clear of scan mode in "+current+" at "+size);
    }
    Window.Width=1280;Window.UpdateLayout();App.Render(Window,Path.Combine(output,"aligned-home-"+current+".png"));
    await WithSettings(new CleanerSettings{Language=current},null,root=>{
     Window.UpdateLayout();var buttons=new[]{"ClearScanCache","AdminMode","OpenHistory","OpenHistoryFolder"}.Select(n=>(Button)root.FindName(n));
     check(buttons.All(b=>CaptionCentered(b,"ButtonCaption")),"Fixed-height settings buttons preserve centered full-height captions in "+current);
     var tabs=new[]{"GeneralTab","BackupTab","ApplicationsTab","RulesTab"}.Select(n=>(TabItem)root.FindName(n));
     check(tabs.All(t=>{t.ApplyTemplate();return CaptionCentered(t,"TabCaption",(FrameworkElement)t.Template.FindName("TabSurface",t));}),"Settings navigation captions are centered inside their rounded surfaces in "+current);
     var tabControl=(TabControl)root.FindName("RulesTabs");check(VisualChildren<ScrollViewer>(tabControl).First().ActualWidth>=tabControl.ActualWidth-4,"Centering settings headers preserves the full-width settings content in "+current);
     var selector=(ThemeSelector)root.FindName("ThemeChoice");selector.ApplyTemplate();var segments=new[]{"ThemeLight","ThemeDark","ThemeSystem"}.Select(n=>(RadioButton)selector.Template.FindName(n,selector)).ToArray();
     foreach(var segment in segments)segment.Focus();Window.UpdateLayout();
     check(segments.All(b=>b.FocusVisualStyle==null&&!VisualChildren<Border>(b).Any(edge=>edge.BorderThickness!=new Thickness(0))),"Theme segments retain the shared sliding indicator without a focus or selection frame in "+current);
     App.Render(Window,Path.Combine(output,"aligned-settings-"+current+".png"));
    },s=>{});
   }
  }finally{while(pages.Count>0)BackPage();Window.Width=width;Window.Height=height;UiText.SetLanguage(language);UiTheme.Set(theme,false);ShowReady();}
 }
}
}
