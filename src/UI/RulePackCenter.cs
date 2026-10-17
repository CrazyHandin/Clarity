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
 public static Func<string[]> ImportFilesFixture;
 public static Func<string,bool> ConfirmImportFixture;
#endif
 public static SettingsView Create(Window owner,CleanerSettings settings,DiscoveryResult discovery,Action back,Action changed,Action<string> notify,Action<string> success,RulePackCatalog catalog=null,RuleStore primary=null,Action<CleanerSettings> persist=null){
  var store=catalog??new RulePackCatalog();var defaultStore=primary??new RuleStore();var root=(UserControl)App.LoadView("ClearSpace.RulePacks.xaml");
  Func<string,Button> button=n=>(Button)root.FindName(n);var list=(ListBox)root.FindName("PackList");var enabled=(CheckBox)root.FindName("PackEnabled");var details=(TextBox)root.FindName("PackDetails");var status=(TextBlock)root.FindName("PackPreviewStatus");var progress=(ProgressBar)root.FindName("PackProgress");var cloud=(FrameworkElement)root.FindName("JsonCloud");var url=(TextBox)root.FindName("JsonUpdateUrl");var auto=(CheckBox)root.FindName("JsonAutomatic");
  int generation=0;bool syncing=false,working=false;CancellationTokenSource stop=null;
  Action persistSettings=()=>{if(persist!=null)persist(settings);else settings.Save();};
  Func<InstalledRulePack> selected=()=>list.SelectedItem is ListBoxItem?(InstalledRulePack)((ListBoxItem)list.SelectedItem).Tag:null;
  Action saveDefault=()=>RuleSourcePreferences.SaveJson(settings,enabled.IsChecked==true,auto.IsChecked==true,RuleSourceAddresses.Json(url.Text),persistSettings);
  Action<InstalledRulePack> describe=entry=>{if(entry==null){details.Text="点击“导入 JSON”选择一份或多份规则文件。";return;}var pack=entry.Loaded.Pack;details.Text=entry.Name+" · "+pack.Version+"\n"+(entry.IsPrimary?"默认规则库":entry.FileName??entry.Id)+" · "+entry.Loaded.Trust+"\n"+(pack.Description??"")+"\n\n"+String.Join("\n\n",pack.Rules.Select(r=>r.Name+" · "+r.Part+"\n"+RuleConditionEvaluator.Describe(r)+"\n"+String.Join("\n",r.Paths)+"\n"+(r.Impact??"")));};
  Action selection=()=>{var entry=selected();syncing=true;enabled.IsChecked=entry!=null&&entry.Enabled;url.Text=RuleSourceAddresses.Json(settings.UpdateUrl);auto.IsChecked=settings.AutoJson;syncing=false;enabled.IsEnabled=button("PackPreview").IsEnabled=button("PackExport").IsEnabled=entry!=null&&!working;button("PackRemove").IsEnabled=entry!=null&&!entry.IsPrimary&&!working;button("PackRollback").IsEnabled=entry!=null&&entry.CanRollback&&!working;cloud.Visibility=entry!=null&&entry.IsPrimary?Visibility.Visible:Visibility.Collapsed;describe(entry);};
  Action<string> refresh=id=>{
   UiText.Forget(owner,list);list.Items.Clear();var errors=new List<string>();var packages=new List<InstalledRulePack>();
   try{packages.Add(new InstalledRulePack{Id="default",IsPrimary=true,Enabled=settings.JsonEnabled,Loaded=defaultStore.Current(),CanRollback=File.Exists(Path.Combine(defaultStore.DirectoryPath,"previous.json"))});}catch(Exception ex){errors.Add(ex.Message);}
   try{packages.AddRange(store.List(errors));}catch(Exception ex){errors.Add(ex.Message);}
   foreach(var entry in packages){var text=new StackPanel();text.Children.Add(new TextBlock{Text=entry.Name,FontSize=17,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});text.Children.Add(new TextBlock{Text=(entry.IsPrimary?"默认规则库":entry.FileName??entry.Id),Margin=new Thickness(0,5,0,0),TextWrapping=TextWrapping.Wrap,Foreground=App.Brush("#A7BFCD")});text.Children.Add(new TextBlock{Text=entry.Loaded.Pack.Version+" · "+entry.Loaded.Pack.Rules.Count+" 条规则 · "+(entry.Enabled?"已启用":"未启用"),Margin=new Thickness(0,6,0,0),Foreground=App.Brush(entry.Enabled?"#39C5BB":"#A7BFCD")});var item=new ListBoxItem{Content=text,Tag=entry};list.Items.Add(item);if((entry.IsPrimary?"@default":entry.Id)==id)list.SelectedItem=item;}
   ((TextBlock)root.FindName("PackWarnings")).Text=String.Join("\n",errors);((TextBlock)root.FindName("PackCount")).Text=packages.Count+" 份规则 · "+packages.Count(p=>p.Enabled)+" 份已启用";if(list.SelectedIndex<0&&list.Items.Count>0)list.SelectedIndex=0;selection();UiText.Attach(owner);
  };
  Action<bool> busy=value=>{working=value;list.IsEnabled=button("PackImport").IsEnabled=button("JsonSave").IsEnabled=button("JsonCheck").IsEnabled=auto.IsEnabled=url.IsEnabled=!value;if(value){enabled.IsEnabled=button("PackExport").IsEnabled=button("PackRollback").IsEnabled=button("PackRemove").IsEnabled=false;button("PackPreview").IsEnabled=true;button("PackPreview").Content="取消操作";progress.Visibility=Visibility.Visible;progress.IsIndeterminate=true;}else{button("PackPreview").Content="只读预览";progress.Visibility=Visibility.Collapsed;var entry=selected();enabled.IsEnabled=button("PackPreview").IsEnabled=button("PackExport").IsEnabled=entry!=null;button("PackRemove").IsEnabled=entry!=null&&!entry.IsPrimary;button("PackRollback").IsEnabled=entry!=null&&entry.CanRollback;}};
  Action finish=()=>{busy(false);if(stop!=null){stop.Dispose();stop=null;}};
  list.SelectionChanged+=(s,e)=>{if(!syncing)selection();};
  enabled.Click+=(s,e)=>{if(syncing||working)return;var entry=selected();if(entry==null)return;try{if(entry.IsPrimary)saveDefault();else store.SetEnabled(entry.Id,enabled.IsChecked==true);changed();refresh(entry.IsPrimary?"@default":entry.Id);success("规则设置已保存，下次常规扫描生效。");}catch(Exception ex){notify(ex.Message);refresh(entry.IsPrimary?"@default":entry.Id);}};
  button("PackBack").Click+=(s,e)=>back();
  button("PackImport").Click+=async(s,e)=>{
   string[] paths=null;
#if CLARITY_TESTS
   if(ImportFilesFixture!=null)paths=ImportFilesFixture();else
#endif
   {var dialog=new OpenFileDialog{Filter="JSON (*.json)|*.json",Title=UiText.T("导入 JSON（可多选）"),Multiselect=true};if(dialog.ShowDialog(owner)==true)paths=dialog.FileNames;}
   if(paths==null||paths.Length==0)return;busy(true);stop=new CancellationTokenSource();var token=stop.Token;++generation;status.Text="正在检查选中的 JSON 文件…";
   try{var plan=await Task.Run(()=>RulePackBatch.Prepare(store,paths,token));token.ThrowIfCancellationRequested();details.Text=plan.Describe();int ready=plan.Items.Count(i=>i.Ready);if(ready==0){status.Text="没有可导入的新规则；详情见下方。";notify(status.Text);return;}string question="可导入 "+ready+" 份规则\n"+String.Join("\n",plan.Items.Take(8).Select(i=>i.FileName+" · "+(i.Error??i.Action)))+(plan.Items.Count>8?"\n…":"")+"\n新文件独立保存，默认不启用；已有规则仅在版本提高时更新。";bool confirmed;
#if CLARITY_TESTS
    if(ConfirmImportFixture!=null)confirmed=ConfirmImportFixture(question);else
#endif
    confirmed=UiText.Show(owner,question,"检查规则来源",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes;
    if(!confirmed){status.Text="导入已取消，规则未更改。";return;}token.ThrowIfCancellationRequested();var result=await Task.Run(()=>plan.Apply(store,token));changed();refresh(result.LastId);details.Text=String.Join("\n",result.Lines);status.Text=result.Summary+" · 新增规则请手动启用";if(result.Failed>0||result.Cancelled)notify(result.Summary);else success(result.Summary);
   }catch(OperationCanceledException){status.Text="导入已取消，规则未更改。";}catch(Exception ex){notify("未导入："+ex.Message);}finally{finish();}
  };
  button("PackRollback").Click+=(s,e)=>{var entry=selected();if(entry==null)return;try{if(entry.IsPrimary)defaultStore.Rollback();else store.Rollback(entry.Id);refresh(entry.IsPrimary?"@default":entry.Id);changed();success("已回退，重新扫描后生效。");}catch(Exception ex){notify(ex.Message);}};
  button("PackExport").Click+=(s,e)=>{var entry=selected();if(entry==null)return;var dialog=new SaveFileDialog{Filter="JSON (*.json)|*.json",FileName=entry.FileName??(entry.IsPrimary?"Clarity-default-rules.json":entry.Id+".json")};if(dialog.ShowDialog(owner)==true)try{File.WriteAllText(dialog.FileName,entry.Loaded.Raw);success("当前规则已导出。");}catch(Exception ex){notify(ex.Message);}};
  button("PackRemove").Click+=(s,e)=>{var entry=selected();if(entry==null||entry.IsPrimary)return;if(UiText.Show(owner,"移除规则包："+entry.Name+"\n只移除规则定义，不会删除应用缓存或备份。","移除规则包",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;try{store.Remove(entry.Id);refresh(null);changed();success("规则包已移除。");}catch(Exception ex){notify(ex.Message);}};
  button("JsonSave").Click+=(s,e)=>{try{saveDefault();changed();success("规则设置已保存，下次常规扫描生效。");}catch(Exception ex){notify(ex.Message);}};
  button("JsonCheck").Click+=async(s,e)=>{try{saveDefault();if(String.IsNullOrWhiteSpace(settings.UpdateUrl)){notify("尚未配置 JSON 地址，可先导入本地规则包。");return;}busy(true);stop=new CancellationTokenSource();await RuleCenter.UpdateRulesAsync(false,settings.UpdateUrl,new Winapp2Store(),defaultStore,stop.Token);refresh("@default");changed();success("当前规则已就绪，重新扫描后生效。");}catch(Exception ex){notify("更新未应用："+ex.Message);}finally{finish();}};
  button("PackPreview").Click+=async(s,e)=>{if(working){stop.Cancel();return;}var entry=selected();if(entry==null)return;busy(true);stop=new CancellationTokenSource();var token=stop.Token;int operation=++generation;status.Text="正在匹配应用与缓存，只读预览…";details.Text="";
   try{var applications=discovery;var report=new Progress<ScanProgress>(p=>{if(!working||generation!=operation||token.IsCancellationRequested)return;progress.IsIndeterminate=false;progress.Maximum=Math.Max(1,p.Total);progress.Value=p.Completed;status.Text="正在扫描 · "+p.Title;});var results=await Task.Run(()=>{if(applications==null)applications=ApplicationDiscovery.Scan(settings.PortableDirectories,token,null);
#if CLARITY_TESTS
    if(PreviewFixture!=null)return PreviewFixture(entry.Loaded,applications,token,p=>((IProgress<ScanProgress>)report).Report(p));
#endif
    return RulePreview.Scan(entry.Loaded,applications,token,p=>((IProgress<ScanProgress>)report).Report(p));});token.ThrowIfCancellationRequested();status.Text="预览完成 · 可处理 "+App.FormatSize(results.Sum(f=>f.EligibleBytes))+" · "+results.Sum(f=>f.Candidates.Count)+" 个文件 · 未删除文件";details.Text=results.Count==0?"未找到符合条件的缓存。请确认应用已安装、路径存在及文件保留期。":String.Join("\n\n",results.Select(f=>f.Rule.Title+"\n"+f.Rule.Detection+"\n可处理 "+App.FormatSize(f.EligibleBytes)+" · "+f.Candidates.Count+" 个文件"+(f.Running?" · 使用中 · 已保护":"")+"\n"+String.Join("\n",f.Samples)));}catch(OperationCanceledException){status.Text="预览已取消，未删除文件。";}catch(Exception ex){status.Text="预览未完成，未删除文件。";notify(ex.Message);}finally{finish();}
  };
  UiMotion.Expander((Expander)cloud);refresh("@default");return new SettingsView{Root=root,TryLeave=()=>{if(!working)return true;stop.Cancel();status.Text="正在取消操作…";return false;}};
 }
}
}
