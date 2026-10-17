using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace ClearSpace {
public sealed class RuleSourcesBinding {public Action Refresh;public Func<bool> TryLeave;}
public static class RuleSourcesPanel {
 public static RuleSourcesBinding Create(FrameworkElement root,Window owner,CleanerSettings settings,Action persist,Action changed,Action openJson,Action<string> notice,Action<string> success,Action<string> progress){
  Func<string,Button> button=n=>(Button)root.FindName(n);var enable=(CheckBox)root.FindName("EnableWinapp2");var automatic=(CheckBox)root.FindName("Automatic");var store=new Winapp2Store();var admin=(Button)root.FindName("AdminMode");bool adminEnabled=admin.IsEnabled;bool working=false,syncing=false;CancellationTokenSource stop=null;
  Action refresh=()=>{syncing=true;enable.IsChecked=settings.Winapp2Enabled;automatic.IsChecked=settings.AutoWinapp2;syncing=false;try{var ini=store.Current();((TextBlock)root.FindName("LibraryState")).Text=ini.Version+" · "+ini.Entries.Count+" 条定义 · "+(settings.Winapp2Enabled?"已启用":"未启用");}catch(Exception ex){((TextBlock)root.FindName("LibraryState")).Text=ex.Message;}try{var packs=new RulePackCatalog().List();((TextBlock)root.FindName("JsonSummary")).Text="默认规则库 + "+packs.Count+" 份导入文件 · "+(packs.Count(p=>p.Enabled)+(settings.JsonEnabled?1:0))+" 份已启用";}catch(Exception ex){((TextBlock)root.FindName("JsonSummary")).Text=ex.Message;}};
  Action save=()=>{if(syncing||working)return;try{RuleSourcePreferences.SaveWinapp2(settings,enable.IsChecked==true,automatic.IsChecked==true,persist);changed();refresh();success("规则设置已保存，下次常规扫描生效。");}catch(Exception ex){refresh();notice(ex.Message);}};
  enable.Click+=(s,e)=>save();automatic.Click+=(s,e)=>save();
  ((TextBox)root.FindName("UpdateUrl")).Text=Winapp2Store.Url;button("RulePacks").IsEnabled=openJson!=null;button("RulePacks").Click+=(s,e)=>{if(openJson!=null)openJson();};
  Action<bool> busy=value=>{working=value;admin.IsEnabled=!value&&adminEnabled;enable.IsEnabled=automatic.IsEnabled=button("ImportIni").IsEnabled=button("RollbackIni").IsEnabled=button("ExportIni").IsEnabled=button("RulePacks").IsEnabled=!value;button("Check").Content=value?"取消更新":"检查更新";};
  button("Check").Click+=async(s,e)=>{if(working){stop.Cancel();return;}busy(true);stop=new CancellationTokenSource();progress("从指定 Winapp2 仓库下载并检查格式…");try{await RuleCenter.UpdateRulesAsync(true,Winapp2Store.Url,store,new RuleStore(),stop.Token);refresh();changed();success("当前规则已就绪，重新扫描后生效。");}catch(Exception ex){notice("更新未应用："+ex.Message);}finally{busy(false);stop.Dispose();stop=null;}};
  button("ImportIni").Click+=(s,e)=>{var dialog=new OpenFileDialog{Filter="Winapp2 (*.ini)|*.ini",Title=UiText.T("导入 Winapp2 INI")};if(dialog.ShowDialog(owner)!=true)return;try{if(new FileInfo(dialog.FileName).Length>Winapp2Store.MaxBytes)throw new InvalidDataException("规则包过大");string raw=File.ReadAllText(dialog.FileName);var pack=Winapp2Parser.Parse(raw);if(UiText.Show(owner,"Winapp2 "+pack.Version+" · "+pack.Entries.Count+" 条定义\n导入仅更新 Winapp2，不改变 JSON 规则库。","检查规则来源",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;store.Import(raw);refresh();changed();success("导入完成。");}catch(Exception ex){notice("未导入："+ex.Message);}};
  button("ExportIni").Click+=(s,e)=>{var dialog=new SaveFileDialog{Filter="INI (*.ini)|*.ini",FileName="Winapp2.ini"};if(dialog.ShowDialog(owner)==true)try{File.WriteAllText(dialog.FileName,store.Current().Raw);success("当前规则已导出。");}catch(Exception ex){notice(ex.Message);}};
  button("RollbackIni").Click+=(s,e)=>{try{store.Rollback();refresh();changed();success("已回退，重新扫描后生效。");}catch(Exception ex){notice(ex.Message);}};
  button("Compatibility").Click+=(s,e)=>{var dialog=new SaveFileDialog{Filter="Text (*.txt)|*.txt",FileName="Winapp2-compatibility.txt"};if(dialog.ShowDialog(owner)==true)try{var report=Winapp2Adapter.Analyze(store.Current(),Winapp2Environment.Windows());File.WriteAllLines(dialog.FileName,new[]{report.Summary}.Concat(report.Lines));success("兼容报告已导出。");}catch(Exception ex){notice(ex.Message);}};
  refresh();return new RuleSourcesBinding{Refresh=refresh,TryLeave=()=>{if(!working)return true;stop.Cancel();progress("正在取消更新…");return false;}};
 }
}
}
