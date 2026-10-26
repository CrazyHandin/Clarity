using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Microsoft.Win32;
namespace ClearSpace {
public sealed class SettingsView {public FrameworkElement Root;public Func<bool> TryLeave;public Action Refresh;}
public static class RuleCenter {
 static void RegisterNames(FrameworkElement root,DependencyObject node){var element=node as FrameworkElement;if(element!=null&&!String.IsNullOrEmpty(element.Name)&&root.FindName(element.Name)==null)root.RegisterName(element.Name,element);foreach(object child in LogicalTreeHelper.GetChildren(node)){var item=child as DependencyObject;if(item!=null)RegisterNames(root,item);}}
 static void LoadTab(FrameworkElement root,Window owner,TabItem tab,string resource,Action initialize){var view=(FrameworkElement)App.LoadView(resource);tab.Content=view;RegisterNames(root,view);UiText.AttachPage(owner,view);initialize();if(tab.Name=="ApplicationsTab")UiMotion.Expander((Expander)root.FindName("PortableExpander"));}
#if CLARITY_TESTS
 public static Func<bool,string,CancellationToken,Task> UpdateFixture;
#endif
 internal static Task UpdateRulesAsync(bool ini,string url,Winapp2Store community,RuleStore store,CancellationToken token){
#if CLARITY_TESTS
  if(UpdateFixture!=null)return UpdateFixture(ini,url,token);
#endif
  return Task.Run(()=>{if(ini)community.Update(token);else store.Update(url,token);});
 }
 public static SettingsView Create(Window owner,CleanerSettings settings,DiscoveryResult discovery,Action changed,Action back,Action about,Action<CleanerSettings> persist=null,Action<string> openLocation=null,Action restartAdministrator=null,Action<string> notify=null,Action jsonGuide=null,Action<string> success=null,Action rulePacks=null,Action clearScanCache=null,Action themeChanged=null){
  UserControl w;
  w=(UserControl)App.LoadView("ClearSpace.Settings.xaml");
  Action<string> message=value=>{((TextBlock)w.FindName("Status")).Text=value;((FrameworkElement)w.FindName("SettingsStatusFrame")).Visibility=Visibility.Collapsed;if(notify!=null)notify(value);};
  Action<string> progress=value=>{((TextBlock)w.FindName("Status")).Text=value;((FrameworkElement)w.FindName("SettingsStatusFrame")).Visibility=Visibility.Visible;};
  Action<string> saved=value=>{((TextBlock)w.FindName("Status")).Text=value;((FrameworkElement)w.FindName("SettingsStatusFrame")).Visibility=Visibility.Collapsed;if(success!=null)success(value);else if(notify!=null)notify(value);};
  WireHistory(w,message);
  Action persistSettings=()=>{if(persist!=null)persist(settings);else settings.Save();};Func<string,TextBox> text=n=>(TextBox)w.FindName(n);Func<string,Button> button=n=>(Button)w.FindName(n);
  var scanCache=(CheckBox)w.FindName("ScanCacheChoice");scanCache.IsChecked=settings.ScanCacheEnabled;
  scanCache.Click+=(s,e)=>{bool previous=settings.ScanCacheEnabled;try{settings.ScanCacheEnabled=scanCache.IsChecked==true;persistSettings();saved("扫描加速设置已保存。");}catch(Exception ex){settings.ScanCacheEnabled=previous;scanCache.IsChecked=previous;message("扫描加速设置未保存："+ex.Message);}};
  button("ClearScanCache").Click+=(s,e)=>{try{if(clearScanCache!=null)clearScanCache();else ScanSession.Clear();saved("扫描缓存已清除，下次扫描将重新识别。");}catch(Exception ex){message("扫描缓存未清除："+ex.Message);}};
  var language=(ComboBox)w.FindName("LanguageChoice");language.ItemsSource=new[]{"简体中文","English"};language.SelectedIndex=UiText.Normalize(settings.Language)=="en"?1:0;
  var glass=(CheckBox)w.FindName("LiquidGlassChoice");glass.IsChecked=settings.LiquidGlass;
  var glassMode=(ComboBox)w.FindName("GlassModeChoice");glassMode.ItemsSource=new[]{"全透式","内部通透"};glassMode.SelectedIndex=UiGlass.NormalizeMode(settings.GlassMode)=="full"?0:1;glassMode.IsEnabled=settings.LiquidGlass;bool restoringGlass=false;
  var glassEffect=(ComboBox)w.FindName("GlassEffectChoice");glassEffect.ItemsSource=new[]{"轻量流畅","增强折射"};glassEffect.SelectedIndex=settings.GlassEffect=="enhanced"?1:0;glassEffect.IsEnabled=settings.LiquidGlass;bool restoringEffect=false;
  Action applyGlass=()=>{UiGlass.SetEffect(owner,settings.GlassEffect);UiGlass.SetMode(owner,settings.GlassMode);UiGlass.SetEnabled(owner,settings.LiquidGlass);glassMode.IsEnabled=glassEffect.IsEnabled=settings.LiquidGlass;};
  glassEffect.SelectionChanged+=(s,e)=>{if(restoringEffect)return;string previous=settings.GlassEffect;try{settings.GlassEffect=glassEffect.SelectedIndex==1?"enhanced":"light";persistSettings();applyGlass();saved("玻璃样式已保存。");}catch(Exception ex){settings.GlassEffect=previous;restoringEffect=true;try{glassEffect.SelectedIndex=previous=="enhanced"?1:0;}finally{restoringEffect=false;}message("玻璃样式未保存："+ex.Message);}};
  glass.Click+=(s,e)=>{bool previous=settings.LiquidGlass;try{settings.LiquidGlass=glass.IsChecked==true;persistSettings();applyGlass();saved("玻璃样式已保存。");}catch(Exception ex){settings.LiquidGlass=previous;glass.IsChecked=previous;glassMode.IsEnabled=glassEffect.IsEnabled=previous;message("玻璃样式未保存："+ex.Message);}};
  glassMode.SelectionChanged+=(s,e)=>{if(restoringGlass)return;string previous=settings.GlassMode;try{settings.GlassMode=glassMode.SelectedIndex==0?"full":"internal";persistSettings();applyGlass();saved("玻璃样式已保存。");}catch(Exception ex){settings.GlassMode=previous;restoringGlass=true;try{glassMode.SelectedIndex=UiGlass.NormalizeMode(previous)=="full"?0:1;}finally{restoringGlass=false;}message("玻璃样式未保存："+ex.Message);}};
  var theme=(ThemeSelector)w.FindName("ThemeChoice");theme.Value=UiTheme.Normalize(settings.Theme);bool restoringTheme=false;
  theme.ValueChanged+=(s,e)=>{if(restoringTheme||theme.Value==UiTheme.Normalize(settings.Theme))return;string previous=settings.Theme;try{settings.Theme=theme.Value;persistSettings();if(themeChanged!=null)themeChanged();else{UiTheme.Set(settings.Theme,UiMotion.GetEnabled(owner));changed();}}catch(Exception ex){settings.Theme=previous;restoringTheme=true;try{theme.Value=UiTheme.Normalize(previous);}finally{restoringTheme=false;}message("主题设置未保存："+ex.Message);}};
  bool restoringLanguage=false;
  language.SelectionChanged+=(s,e)=>{if(restoringLanguage)return;string previous=settings.Language;try{settings.Language=language.SelectedIndex==1?"en":"zh-CN";persistSettings();UiText.SetLanguage(settings.Language);changed();saved("语言设置已保存。");}catch(Exception ex){settings.Language=previous;restoringLanguage=true;try{language.SelectedIndex=UiText.Normalize(previous)=="en"?1:0;}finally{restoringLanguage=false;}UiText.SetLanguage(previous);message("语言设置未保存："+ex.Message);}};
  var administrator=(Button)w.FindName("AdminMode");administrator.IsEnabled=!App.IsAdmin()&&restartAdministrator!=null;((TextBlock)w.FindName("PrivilegeState")).Text=App.IsAdmin()?"当前已以管理员身份运行。":"当前使用普通权限。";
  administrator.Click+=(s,e)=>{if(restartAdministrator!=null)restartAdministrator();};button("AboutButton").Click+=(s,e)=>about();button("SettingsBack").Click+=(s,e)=>back();
  RuleSourcesBinding sources=null;var status=(TextBlock)w.FindName("Status");bool restoring=false;
  Action refresh=()=>{var list=w.FindName("Portable")as ListBox;if(list!=null)list.ItemsSource=settings.PortableDirectories.ToArray();if(sources!=null)sources.Refresh();};
  Action initApplications=()=>{
   var portable=(ListBox)w.FindName("Portable");var apps=(ListBox)w.FindName("Apps");portable.ItemsSource=settings.PortableDirectories.ToArray();
  var identities=discovery==null?new List<ApplicationIdentity>():discovery.Applications;apps.ItemsSource=identities;text("AppSearch").ToolTip="搜索应用名、发行商或程序路径";text("AppSearch").TextChanged+=(s,e)=>apps.ItemsSource=identities.Where(a=>a.SearchText.IndexOf(text("AppSearch").Text,StringComparison.OrdinalIgnoreCase)>=0).ToList();apps.SelectionChanged+=(s,e)=>{var a=apps.SelectedItem as ApplicationIdentity;button("OpenApplication").IsEnabled=ApplicationLocations.Resolve(a)!=null;text("AppEvidence").Text=a==null?"":RuleStore.Describe(a)+"\n产品："+a.Product+"\n公司："+a.Company+"\n安装目录："+a.InstallDirectory;};if(identities.Count==0)text("AppEvidence").Text="常规扫描后会显示应用发现结果。";
  button("OpenApplication").Click+=(s,e)=>{try{string location=ApplicationLocations.Resolve(apps.SelectedItem as ApplicationIdentity);if(location==null)throw new IOException("应用文件或目录已不存在");if(openLocation!=null)openLocation(location);else ApplicationLocations.Open(location);}catch(Exception ex){message("打开应用位置失败："+ex.Message);}};
  button("AddPortable").Click+=(s,e)=>{using(var d=new System.Windows.Forms.FolderBrowserDialog{Description=UiText.T("选择便携或自定义安装目录")})if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK){if(!settings.PortableDirectories.Contains(d.SelectedPath,StringComparer.OrdinalIgnoreCase))settings.PortableDirectories.Add(d.SelectedPath);persistSettings();refresh();message("目录已添加，下次手动扫描时检查。");}};button("RemovePortable").Click+=(s,e)=>{var selected=portable.SelectedItem as string;if(selected!=null){settings.PortableDirectories.Remove(selected);persistSettings();refresh();}};
  };
  Action initBackups=()=>{
  text("RecoveryPath").Text=String.IsNullOrEmpty(settings.RecoveryDirectory)?FileRecovery.DefaultDirectory():settings.RecoveryDirectory;
  button("ChooseRecovery").Click+=(s,e)=>{using(var d=new System.Windows.Forms.FolderBrowserDialog{Description=UiText.T("选择文件备份保存目录；其他盘可释放 C 盘空间")})if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK)text("RecoveryPath").Text=d.SelectedPath;};button("SaveRecovery").Click+=(s,e)=>{try{string p=Path.GetFullPath(text("RecoveryPath").Text);if(p.StartsWith("\\\\")||!Directory.Exists(p)||!Engine.SafeAncestors(Path.Combine(p,"probe")))throw new IOException("请选择可验证的本地现有目录");settings.RecoveryDirectory=p;persistSettings();changed();message("备份位置已保存。");}catch(Exception ex){message(ex.Message);}};
  button("OpenRecovery").Click+=(s,e)=>{try{BackupFolders.Open(text("RecoveryPath").Text);}catch(Exception ex){message("打开备份位置失败："+ex.Message);}};
  button("RestoreFiles").Click+=async(s,e)=>{var d=new OpenFileDialog{Filter=UiText.T("澄净文件备份 (*.csbackup)|*.csbackup"),Title=UiText.T("选择文件备份索引")};if(d.ShowDialog(owner)!=true)return;if(UiText.Show(owner,"恢复本机、本账户的文件备份，已有文件保留。成功恢复并校验后会自动清除对应备份，未恢复内容仍保留。", "恢复文件",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;restoring=true;administrator.IsEnabled=false;button("RestoreFiles").IsEnabled=button("Purge").IsEnabled=false;progress("正在校验并恢复文件…");try{var result=await Task.Run(()=>FileRecovery.RestoreDetailed(d.FileName,true));message("已恢复 "+result.Restored+" 个文件 · 清除 "+result.Cleared+" 份备份 · 保留 "+result.Pending+" 份"+(result.Failed>0?" · 失败 "+result.Failed+" 项":"")+"。");status.ToolTip=String.Join("\n",result.Errors);changed();}catch(Exception ex){message("恢复停止："+ex.Message);}finally{restoring=false;administrator.IsEnabled=!App.IsAdmin()&&restartAdministrator!=null;button("RestoreFiles").IsEnabled=button("Purge").IsEnabled=true;}};
  button("Purge").Click+=(s,e)=>{var d=new OpenFileDialog{Filter=UiText.T("澄净文件备份 (*.csbackup)|*.csbackup"),Title=UiText.T("选择要清空的备份")};if(d.ShowDialog(owner)!=true)return;try{var entries=FileRecovery.Read(d.FileName);if(UiText.Show(owner,"永久删除该批 "+entries.Count+" 个备份文件。此后不能从该备份恢复。", "清空备份",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;FileRecovery.Purge(d.FileName);message("该批备份已清空。");}catch(Exception ex){message(ex.Message);}};
  string registryDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClearSpace","RegistryBackups");
  button("OpenRegistryBackup").Click+=(s,e)=>{try{if(!Directory.Exists(registryDirectory)){message("还没有注册表备份。");return;}BackupFolders.Open(registryDirectory);}catch(Exception ex){message("打开注册表备份位置失败："+ex.Message);}};
  button("RestoreRegistry").Click+=(s,e)=>{var d=new OpenFileDialog{Title=UiText.T("选择澄净生成的注册表备份"),Filter=UiText.T("澄净注册表备份 (*.json)|*.json"),InitialDirectory=Directory.Exists(registryDirectory)?registryDirectory:AppDomain.CurrentDomain.BaseDirectory};if(d.ShowDialog(owner)!=true)return;if(UiText.Show(owner,"恢复本机备份中的缺失注册表项。已存在的项不会被覆盖。","恢复注册表备份",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;try{int count=RegistryCleaner.Restore(d.FileName);message("已恢复 "+count+" 项注册表；现有项保留。");changed();}catch(Exception ex){message("恢复注册表失败："+ex.Message);}};
  };
  Action initRules=()=>{sources=RuleSourcesPanel.Create(w,owner,settings,persistSettings,changed,rulePacks,message,saved,progress);button("JsonGuide").Click+=(s,e)=>{if(jsonGuide!=null)jsonGuide();};};
  var settingsTabs=(TabControl)w.FindName("RulesTabs");
  settingsTabs.SelectionChanged+=(s,e)=>{if(e.OriginalSource!=settingsTabs)return;var selected=settingsTabs.SelectedItem as TabItem;if(selected==null||selected.Content!=null)return;
   if(selected.Name=="BackupTab")LoadTab(w,owner,selected,"ClearSpace.Settings.Backup.xaml",initBackups);
   else if(selected.Name=="ApplicationsTab")LoadTab(w,owner,selected,"ClearSpace.Settings.Applications.xaml",initApplications);
   else if(selected.Name=="RulesTab")LoadTab(w,owner,selected,"ClearSpace.Settings.Rules.xaml",initRules);
  };
  UiMotion.Tabs((TabControl)w.FindName("RulesTabs"));
  return new SettingsView{Root=w,Refresh=refresh,TryLeave=()=>{if(sources!=null&&!sources.TryLeave())return false;if(restoring){progress("正在恢复文件，请完成后关闭。");return false;}return true;}};
 }
 static void WireHistory(FrameworkElement root,Action<string> notify){((TextBlock)root.FindName("HistoryLocation")).Text=CleaningHistory.DirectoryPath;((Button)root.FindName("OpenHistory")).Click+=(s,e)=>{try{CleaningHistory.Open();}catch(Exception ex){notify(ex.Message);}};((Button)root.FindName("OpenHistoryFolder")).Click+=(s,e)=>{try{CleaningHistory.Open(true);}catch(Exception ex){notify(ex.Message);}};}
}
}
