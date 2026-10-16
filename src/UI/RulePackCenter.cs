using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace ClearSpace {
public static class RulePackCenter {
#if CLARITY_TESTS
 public static Func<LoadedRulePack,DiscoveryResult,CancellationToken,Action<ScanProgress>,List<Finding>> PreviewFixture;
#endif
 public static SettingsView Create(Window owner,CleanerSettings settings,DiscoveryResult discovery,Action back,Action changed,Action<string> notify,Action<string> success,RulePackCatalog catalog=null){
  var store=catalog??new RulePackCatalog();var root=(UserControl)App.LoadView("ClearSpace.RulePacks.xaml");Func<string,Button> button=n=>(Button)root.FindName(n);var list=(ListBox)root.FindName("PackList");var enabled=(CheckBox)root.FindName("PackEnabled");var details=(TextBox)root.FindName("PackDetails");var status=(TextBlock)root.FindName("PackPreviewStatus");var progress=(ProgressBar)root.FindName("PackProgress");int generation=0;bool syncing=false,working=false;CancellationTokenSource stop=null;
  Func<InstalledRulePack> selected=()=>list.SelectedItem is ListBoxItem?(InstalledRulePack)((ListBoxItem)list.SelectedItem).Tag:null;
  Action<InstalledRulePack> describe=entry=>{if(entry==null){details.Text="还没有附加规则包。可导入 Schema 2 JSON；旧 JSON 仍可在规则管理中使用。";return;}var pack=entry.Loaded.Pack;details.Text=pack.Name+" · "+pack.Version+"\n"+pack.PackId+" · "+entry.Loaded.Trust+"\n"+(pack.Author??"")+"\n"+(pack.Description??"")+"\n\n"+String.Join("\n\n",pack.Rules.Select(r=>r.Name+" · "+r.Part+"\n"+RuleConditionEvaluator.Describe(r)+"\n"+String.Join("\n",r.Paths)+"\n"+(r.Impact??"")));};
  Action selection=()=>{var entry=selected();syncing=true;enabled.IsChecked=entry!=null&&entry.Enabled;syncing=false;enabled.IsEnabled=button("PackPreview").IsEnabled=button("PackRemove").IsEnabled=entry!=null&&!working;button("PackRollback").IsEnabled=entry!=null&&entry.CanRollback&&!working;describe(entry);};
  Action<string> refresh=id=>{UiText.Forget(owner,list);list.Items.Clear();var errors=new List<string>();var packages=new List<InstalledRulePack>();try{packages=store.List(errors);}catch(Exception ex){errors.Add(ex.Message);}foreach(var entry in packages){var text=new StackPanel();text.Children.Add(new TextBlock{Text=entry.Loaded.Pack.Name,FontSize=17,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});text.Children.Add(new TextBlock{Text=entry.Loaded.Pack.Version+" · "+(entry.Enabled?"已启用":"未启用"),Margin=new Thickness(0,6,0,0),Foreground=App.Brush(entry.Enabled?"#39C5BB":"#A7BFCD")});var item=new ListBoxItem{Content=text,Tag=entry};list.Items.Add(item);if(entry.Id==id)list.SelectedItem=item;}((TextBlock)root.FindName("PackWarnings")).Text=String.Join("\n",errors);if(list.SelectedIndex<0&&list.Items.Count>0)list.SelectedIndex=0;selection();UiText.Attach(owner);};
  list.SelectionChanged+=(s,e)=>selection();enabled.Click+=(s,e)=>{if(syncing||working)return;var entry=selected();if(entry==null)return;try{store.SetEnabled(entry.Id,enabled.IsChecked==true);changed();refresh(entry.Id);success("规则设置已保存，下次常规扫描生效。");}catch(Exception ex){notify(ex.Message);refresh(entry.Id);}};
  button("PackBack").Click+=(s,e)=>back();
  button("PackImport").Click+=(s,e)=>{var dialog=new OpenFileDialog{Filter="JSON (*.json)|*.json",Title=UiText.T("导入规则包")};if(dialog.ShowDialog(owner)!=true)return;try{if(new FileInfo(dialog.FileName).Length>RuleStore.MaxBytes)throw new InvalidDataException("规则包过大");string raw=File.ReadAllText(dialog.FileName);var pack=store.Inspect(raw);if(UiText.Show(owner,pack.Pack.Name+" · "+pack.Pack.Version+"\n"+pack.Trust+"\n导入仅保存规则，不扫描、不删除文件。新规则包默认关闭。","检查规则来源",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;var imported=store.Import(raw);refresh(imported.Id);changed();success("规则包已导入。");}catch(Exception ex){notify("未导入："+ex.Message);}};
  button("PackRollback").Click+=(s,e)=>{var entry=selected();if(entry==null)return;try{store.Rollback(entry.Id);refresh(entry.Id);changed();success("已回退，重新扫描后生效。");}catch(Exception ex){notify(ex.Message);}};
  button("PackRemove").Click+=(s,e)=>{var entry=selected();if(entry==null)return;if(UiText.Show(owner,"移除规则包："+entry.Loaded.Pack.Name+"\n只移除规则定义，不会删除应用缓存或备份。","移除规则包",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;try{store.Remove(entry.Id);refresh(null);changed();success("规则包已移除。");}catch(Exception ex){notify(ex.Message);}};
  button("PackPreview").Click+=async(s,e)=>{if(working){stop.Cancel();return;}var entry=selected();if(entry==null)return;working=true;stop=new CancellationTokenSource();var token=stop.Token;int operation=++generation;list.IsEnabled=enabled.IsEnabled=button("PackImport").IsEnabled=button("PackRollback").IsEnabled=button("PackRemove").IsEnabled=false;button("PackPreview").Content="取消预览";progress.Visibility=Visibility.Visible;progress.IsIndeterminate=true;status.Text="正在匹配应用与缓存，只读预览…";details.Text="";try{var applications=discovery;var report=new Progress<ScanProgress>(p=>{if(!working||generation!=operation||token.IsCancellationRequested)return;progress.IsIndeterminate=false;progress.Maximum=Math.Max(1,p.Total);progress.Value=p.Completed;status.Text="正在扫描 · "+p.Title;});var results=await Task.Run(()=>{if(applications==null)applications=ApplicationDiscovery.Scan(settings.PortableDirectories,token,null);
#if CLARITY_TESTS
   if(PreviewFixture!=null)return PreviewFixture(entry.Loaded,applications,token,p=>((IProgress<ScanProgress>)report).Report(p));
#endif
   return RulePreview.Scan(entry.Loaded,applications,token,p=>((IProgress<ScanProgress>)report).Report(p));});token.ThrowIfCancellationRequested();status.Text="预览完成 · 可处理 "+App.FormatSize(results.Sum(f=>f.EligibleBytes))+" · "+results.Sum(f=>f.Candidates.Count)+" 个文件 · 未删除文件";details.Text=results.Count==0?"未找到符合条件的缓存。请确认应用已安装、路径存在及文件保留期。":String.Join("\n\n",results.Select(f=>f.Rule.Title+"\n"+f.Rule.Detection+"\n可处理 "+App.FormatSize(f.EligibleBytes)+" · "+f.Candidates.Count+" 个文件"+(f.Running?" · 使用中 · 已保护":"")+"\n"+String.Join("\n",f.Samples)));}catch(OperationCanceledException){status.Text="预览已取消，未删除文件。";}catch(Exception ex){status.Text="预览未完成，未删除文件。";notify(ex.Message);}finally{working=false;stop.Dispose();stop=null;progress.Visibility=Visibility.Collapsed;button("PackPreview").Content="只读预览";list.IsEnabled=button("PackImport").IsEnabled=true;enabled.IsEnabled=button("PackPreview").IsEnabled=button("PackRemove").IsEnabled=true;button("PackRollback").IsEnabled=entry.CanRollback;}};
  refresh(null);return new SettingsView{Root=root,TryLeave=()=>{if(!working)return true;stop.Cancel();status.Text="正在取消预览…";return false;}};
 }
}
}
