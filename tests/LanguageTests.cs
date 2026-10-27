using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ClearSpace {
public sealed partial class Controller {
 static IEnumerable<TextBlock> LanguageLabels(DependencyObject node){var label=node as TextBlock;if(label!=null)yield return label;for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in LanguageLabels(VisualTreeHelper.GetChild(node,i)))yield return child;}
 static void ExpandLanguageNodes(IEnumerable<CleanNode> nodes){foreach(var node in nodes){node.Expanded=true;ExpandLanguageNodes(node.Children);}}
 public async Task VerifyLanguages(string output,Action<bool,string> check){
  UiText.SetLanguage("zh-CN");check(Window.Title=="Clarity"&&!Window.Title.Contains("澄净")&&!Window.Title.Contains("CraZyX"),"The window caption contains only Clarity");
  check(Get<Button>("AdminButton")==null,"Administrator restart is absent from the main toolbar");
  check(scanMode.Items.Count==2&&scanMode.Items[0].ToString().StartsWith("快速")&&scanMode.Items[1].ToString().StartsWith("常规"),"Quick scanning precedes regular scanning");
  var scopes=groups.Select(g=>g.Name).ToArray();int mode=scanMode.SelectedIndex;object source=scanMode.ItemsSource;
  UiText.SetLanguage("en");check(Get<Button>("ScanButton").Content.ToString()=="Start scan","English localizes the live scan action");
  check(groups.Select(g=>g.Name).SequenceEqual(scopes)&&scanMode.SelectedIndex==mode&&ReferenceEquals(scanMode.ItemsSource,source),"Changing language preserves canonical scopes and the selected scan mode");
  string path="D:\\测试用户\\缓存\\登录资料.txt";check(UiText.T(path)==path&&UiText.T("范围："+path)=="Scope: "+path,"Localization preserves Chinese filenames and full paths");
  check(Get<TextBlock>("EnglishBrandName").Visibility==Visibility.Collapsed,"English branding avoids a duplicate Clarity label");App.Render(Window,Path.Combine(output,"startup-english.png"));
  var labels=LanguageLabels(Get<ItemsControl>("ScopeList")).Select(t=>t.Text).ToArray();check(labels.Contains("System junk")&&labels.Contains("App caches")&&labels.Contains("Temporary files · Logs · Dumps")&&!labels.Contains("系统垃圾"),"English localizes existing scope-card names and descriptions without changing their models");
  Apply(engine.Scan(CancellationToken.None,null));SetMode(true);ExpandLanguageNodes(treeNodes);await Window.Dispatcher.InvokeAsync(new Action(()=>Window.UpdateLayout()),System.Windows.Threading.DispatcherPriority.ContextIdle);App.Render(Window,Path.Combine(output,"results-english.png"));
  var rows=LanguageLabels(tree).Select(t=>t.Text).ToArray();check(rows.Contains("App caches")&&rows.Contains(UiText.T("未知缓存"))&&!rows.Contains("软件缓存"),"Result rows created after choosing English localize their nested category and unknown-cache labels");
  var untranslated=rows.Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t,"[\\u4e00-\\u9fff]")).ToArray();check(untranslated.Length==0,"Generated fixture results have English item, risk, capacity, and details labels"+(untranslated.Length>0?": "+String.Join(" | ",untranslated):""));
  check(treeNodes.Single(n=>n.Title=="软件缓存").Children.SelectMany(n=>n.Children).Any(n=>n.Title=="未知缓存"),"English result rendering preserves canonical category and unknown-cache model names");
  ShowReady();
  int saves=0;string settingsFile=Path.Combine(output,"language-settings-fixture.json");var options=new CleanerSettings{Language="en",RecoveryDirectory=Path.Combine(output,"language-backups")};Exception error=null;
  await WithSettings(options,null,w=>{try{
   var tabs=(TabControl)w.FindName("RulesTabs");check(tabs.SelectedItem==w.FindName("GeneralTab")&&Window.Title==App.DisplayName,"Settings starts with General and uses the unified window name");
   check(((TextBlock)w.FindName("Heading")).Text=="Settings & Backups","The settings heading is localized");App.Render(Window,Path.Combine(output,"settings-english.png"));
   tabs.SelectedItem=w.FindName("BackupTab");check(((Button)w.FindName("RestoreFiles")).Content.ToString()=="Restore file backup","Backup actions are localized");App.Render(Window,Path.Combine(output,"backups-english.png"));
   var choice=(ComboBox)w.FindName("LanguageChoice");choice.SelectedIndex=0;check(saves==1&&options.Language=="zh-CN"&&UiText.Language=="zh-CN","Selecting Chinese saves and applies its language preference immediately");
   choice.SelectedIndex=1;check(saves==2&&new JavaScriptSerializer().Deserialize<CleanerSettings>(File.ReadAllText(settingsFile)).Language=="en","The English preference is persisted for the next launch");
   tabs.SelectedItem=w.FindName("RulesTab");check(((Button)w.FindName("Check")).Content.ToString()=="Check for updates","Rule management actions are localized");App.Render(Window,Path.Combine(output,"rules-english.png"));
   OpenAbout();App.Render(Window,Path.Combine(output,"about-english.png"));check(LanguageLabels(pages.Last()).Any(t=>t.Text.Contains("personal, noncommercial use"))&&!LanguageLabels(pages.Last()).Any(t=>t.Text.Contains("Clarity is open source")),"English About presents source availability and personal-use terms without the former open-source claim");((Button)pages.Last().FindName("TermsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var terms=((TextBox)pages.Last().FindName("DocumentText")).Text;check(terms.StartsWith("English Terms of Use")&&terms.Contains("Clarity Personal Use and Source Available License 1.0")&&terms.Contains("rights lawfully obtained")&&!terms.Contains("Permission is hereby granted"),"English terms include the current personal-use license and preserved historical rights");App.Render(Window,Path.Combine(output,"terms-english.png"));BackPage();((Button)pages.Last().FindName("UsageButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(((TextBox)pages.Last().FindName("DocumentText")).Text.StartsWith("English User Guide"),"The embedded usage entry follows the English interface language");App.Render(Window,Path.Combine(output,"guide-english.png"));BackPage();BackPage();
  }catch(Exception ex){error=ex;}},s=>{saves++;AtomicFile.Write(settingsFile,new JavaScriptSerializer().Serialize(s));});if(error!=null)throw error;
  var failedOptions=new CleanerSettings{Language="en"};int failedSaves=0;
  await WithSettings(failedOptions,null,w=>{var choice=(ComboBox)w.FindName("LanguageChoice");choice.SelectedIndex=0;check(failedSaves==1&&choice.SelectedIndex==1&&failedOptions.Language=="en"&&UiText.Language=="en","A failed language save restores the visible choice and current language without retrying the write");},s=>{failedSaves++;throw new IOException("Fixture save failure");});
  UiText.SetLanguage("zh-CN");check(Get<Button>("ScanButton").Content.ToString()=="开始扫描"&&Get<TextBlock>("EnglishBrandName").Visibility==Visibility.Visible,"Switching back restores Chinese controls and branding without restarting");
  App.Render(Window,Path.Combine(output,"startup-chinese-restored.png"));check(LanguageLabels(Get<ItemsControl>("ScopeList")).Any(t=>t.Text=="系统垃圾"),"Switching back also restores Chinese data-bound scope cards");
  int prompts=0,elevations=0;var previousCheck=AdministratorState;var previousConfirm=ConfirmAdministrator;var previousElevate=ElevateAction;
  try{AdministratorState=()=>false;ConfirmAdministrator=r=>{prompts++;check(!busy,"Administrator decisions happen after cleanup unlocks the interface");return false;};ElevateAction=()=>elevations++;
   OfferAdministrator(new CleanResult{Failed=1});check(prompts==1&&elevations==0,"Declining an administrator prompt never restarts or retries cleanup");
   OfferAdministrator(new CleanResult{Skipped=1,Items=new List<AuditItem>{new AuditItem{ErrorCode=32}}});check(prompts==1,"A sharing violation alone does not suggest administrator permissions");
   OfferAdministrator(new CleanResult{Cancelled=true,Failed=1});check(prompts==1,"Cancelled cleanup does not trigger administrator restart");
   AdministratorState=()=>true;OfferAdministrator(new CleanResult{Failed=1,PermissionDenied=1});check(prompts==1,"Already elevated sessions do not prompt for elevation again");
   AdministratorState=()=>false;ConfirmAdministrator=r=>{prompts++;return true;};OfferAdministrator(new CleanResult{PermissionDenied=1});check(elevations==1,"Explicitly accepting an access-denied prompt invokes only the injected restart action");
  }finally{AdministratorState=previousCheck;ConfirmAdministrator=previousConfirm;ElevateAction=previousElevate;}
 }
}
}
