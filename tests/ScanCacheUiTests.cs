#if CLARITY_TESTS
using System;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 void VerifyScanCacheSettings(Action<bool,string> check){
  var options=new CleanerSettings();int saved=0,cleared=0;bool fail=false;string notice="";
  var view=RuleCenter.Create(Window,options,null,()=>{},()=>{},()=>{},persist:s=>{if(fail)throw new Exception("fixture save failure");saved++;},success:s=>notice=s,clearScanCache:()=>cleared++);
  var choice=(CheckBox)view.Root.FindName("ScanCacheChoice");var clear=(Button)view.Root.FindName("ClearScanCache");
  check(choice.IsChecked==true&&options.ScanCacheEnabled,"Local scan acceleration defaults on in settings");
  choice.IsChecked=false;choice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!options.ScanCacheEnabled&&saved==1&&notice=="扫描加速设置已保存。","Scan acceleration changes persist immediately with a success notice");
  fail=true;choice.IsChecked=true;choice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(!options.ScanCacheEnabled&&choice.IsChecked==false,"Failed cache preference saves restore the previous setting");
  clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(cleared==1&&notice=="扫描缓存已清除，下次扫描将重新识别。","Settings clears only the injected cache and reports success");
  check(UiText.T("扫描加速")=="扫描加速","The cache setting preserves Chinese labels in Chinese mode");
  UiText.SetLanguage("en");try{check(UiText.T("启用本地扫描缓存")=="Enable local scan cache"&&UiText.T("清除扫描缓存")=="Clear scan cache","Cache settings provide English labels");}finally{UiText.SetLanguage("zh-CN");}
 }
}
}
#endif
