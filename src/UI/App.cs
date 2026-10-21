using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;


[assembly: AssemblyDescription("Windows disk cleanup with cache discovery and backup restoration")]

namespace ClearSpace {
public sealed class GroupOption : INotifyPropertyChanged {
 public string Name{get;set;}public string Icon{get;set;}public string Description{get;set;}bool selected=true;string state="等待扫描";
 public bool Selected{get{return selected;}set{selected=value;Notify("Selected");}}
 public string State{get{return state;}set{state=value;Notify("State");}}
 public Brush StateColor{get{return App.Brush(state.StartsWith("正在")?"#39C5BB":state.StartsWith("完成")?"#B8C8D5":"#8A9CAC");}}
 public void Notify(string name){if(PropertyChanged!=null){PropertyChanged(this,new PropertyChangedEventArgs(name));if(name=="State")PropertyChanged(this,new PropertyChangedEventArgs("StateColor"));}}
 public event PropertyChangedEventHandler PropertyChanged;
}
public sealed class CleanNode : INotifyPropertyChanged {
 public string Title{get;set;}public string Icon{get;set;}public int Depth{get;set;}public bool ShowChecks{get;set;}public Finding Finding;
 public List<CleanNode> Children{get;set;}public Action Changed;bool selected,expanded;
 public CleanNode(){Children=new List<CleanNode>();}
 public bool IsLeaf{get{return Finding!=null;}}
 public IEnumerable<CleanNode> Leaves(){return IsLeaf?new[]{this}:Children.SelectMany(n=>n.Leaves());}
 public bool Selectable{get{return IsLeaf?!Finding.Running&&(Finding.ManualOverride?Finding.Rule.AllowManual:!Finding.Rule.Protected)&&(Finding.Candidates.Count>0||Finding.RegistryCandidates.Count>0||(Finding.Rule.InfoKind=="system-disk-cleanup"||Finding.Rule.InfoKind=="disable-hibernation")&&Finding.VirtualItems>0):Children.Any(c=>c.Selectable);}}
 public bool? Selected{get{if(IsLeaf)return selected;var leaves=Leaves().Where(n=>n.Selectable).ToList();if(leaves.Count==0)return false;int count=leaves.Count(n=>n.selected);return count==0?(bool?)false:count==leaves.Count?(bool?)true:null;}set{SetSelection(value!=false);if(Changed!=null)Changed();}}
 void SetSelection(bool value){if(IsLeaf){selected=value&&Selectable;Notify("Selected");}else foreach(var n in Children)n.SetSelection(value);Notify("Selected");}
 public bool Expanded{get{return expanded;}set{expanded=value;Notify("Expanded");}}
 public Visibility CheckVisibility{get{return ShowChecks?Visibility.Visible:Visibility.Collapsed;}}
 public Visibility InfoVisibility{get{return IsLeaf?Visibility.Visible:Visibility.Collapsed;}}
 public double TitleSize{get{return Depth==0?18:Depth==1?16:14;}}
 public FontWeight TitleWeight{get{return Depth<2?FontWeights.SemiBold:FontWeights.Normal;}}
 public Brush TitleColor{get{return App.Brush(Depth==0?"#ECF3F8":Depth==1?"#DAE6EE":"#BFD0DD");}}
 string RiskCode{get{var list=Leaves().ToList();return list.Any(n=>n.Finding.ManualOverride)?"override":list.Any(n=>n.Finding.Rule.InfoKind=="system-disk-cleanup")?"system":list.All(n=>n.Finding.Rule.Protected||n.Finding.Running)?"protected":list.Any(n=>n.Finding.Rule.Heuristic)?"heuristic":list.Any(n=>n.Finding.Rule.ForceBackup)?"community":list.Any(n=>n.Finding.Rule.Privacy)?"privacy":list.Any(n=>!n.Finding.Rule.Quick&&!n.Finding.Rule.Protected)?"manual":"quick";}}
 public string Risk{get{return RiskCode=="override"?(IsLeaf?"风险删除 · 先备份":"含风险删除"):RiskCode=="system"?(IsLeaf?"系统清理 · 不可恢复":"含系统清理"):RiskCode=="protected"?"保留":RiskCode=="heuristic"?"疑似 · 需核对":RiskCode=="community"?(IsLeaf?"社区 · 先备份":"含社区项"):RiskCode=="privacy"?"使用痕迹":RiskCode=="quick"?"低风险":IsLeaf?"需确认":"含需确认项";}}
 public Brush RiskColor{get{return App.Brush(RiskCode=="override"||RiskCode=="system"?"#FFADA2":RiskCode=="protected"?"#87B5DF":RiskCode=="privacy"?"#F69DB5":RiskCode=="quick"?"#39C5BB":"#F2B868");}}
 public Brush RiskBackground{get{return App.Brush(RiskCode=="override"||RiskCode=="system"?"#49312F":RiskCode=="protected"?"#27394C":RiskCode=="privacy"?"#402C3B":RiskCode=="quick"?"#1B3B3B":"#41372A");}}
 public Visibility RiskVisibility{get{return Depth>0||RiskCode=="override"||RiskCode=="system"?Visibility.Visible:Visibility.Collapsed;}}
 public string Summary{get{
  var leaves=Leaves().ToList();long bytes=leaves.Sum(n=>n.Finding.EligibleBytes),chosen=leaves.Where(n=>n.selected).Sum(n=>n.Finding.EligibleBytes);int regs=leaves.Sum(n=>n.Finding.RegistryCandidates.Count),chosenRegs=leaves.Where(n=>n.selected).Sum(n=>n.Finding.RegistryCandidates.Count);
  if(IsLeaf){if(Finding.Rule.InfoKind=="disable-hibernation")return Finding.VirtualItems>0?"关闭休眠 · 需管理员 · "+App.FormatSize(Finding.EligibleBytes):"休眠未启用或文件不可读";if(Finding.Rule.InfoKind=="system-disk-cleanup")return Finding.VirtualItems>0?"可选自动清理 · 系统估算":"系统工具不可用";if(Finding.Running)return "使用中 · 关闭软件后重扫";if(Finding.ManualOverride)return "可自选删除 "+App.FormatSize(bytes);if(Finding.Rule.Protected)return (Finding.Rule.InfoKind=="storage"?"由系统清理处理":Finding.Rule.Roots.Length==0?"说明项 · 无文件删除目标":"系统保护 · 不直接删除")+(Finding.TotalBytes>0?" · "+App.FormatSize(Finding.TotalBytes):"");if(Finding.Rule.InfoKind=="registry-clean")return regs>0?"可处理 "+regs+" 项":"未发现可清理项";return Finding.Candidates.Count>0?"可处理 "+App.FormatSize(bytes):Finding.ManualCandidates.Count>0?"保留内容可在高级模式自选":"暂无候选 · 见说明";}
  string value=bytes>0?App.FormatSize(bytes):regs>0?regs+" 项注册记录":"暂无可处理文件";return value+(chosen>0?" · 已选 "+App.FormatSize(chosen):chosenRegs>0?" · 已选 "+chosenRegs+" 项":"");
 }}
 public Brush SummaryColor{get{return Depth>0?RiskColor:App.Brush("#C7D7E3");}}
 public void Notify(string name){if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs(name));}
 public void Refresh(){Notify("Selected");Notify("Summary");foreach(var c in Children)c.Refresh();}
 public event PropertyChangedEventHandler PropertyChanged;
}
public sealed partial class Controller {
 public Window Window;public Task LastOperation;public Func<List<Rule>> RuleFactory,QuickRuleFactory;public DiscoveryResult LastDiscovery;
 public Func<CleanResult,bool> ConfirmAdministrator;public Action ElevateAction;public Func<bool> AdministratorState=App.IsAdmin;public Func<IList<Finding>,bool,bool> ConfirmCleanup;public Action<string> OpenBackupFolder=BackupFolders.Open;Rule systemRule=SystemDiskCleanup.CreateRule();Finding systemFinding;CheckBox systemOption;
 CleanerSettings settings=CleanerSettings.Load();ScanSession activeScanSession;public ScanStatistics LastScanStatistics;bool quickScan;ComboBox scanMode;
 Engine engine=new Engine(new List<Rule>());List<GroupOption> groups=new List<GroupOption>();List<CleanNode> leaves=new List<CleanNode>(),treeNodes=new List<CleanNode>();
 bool busy,advanced,hasScanned;
#if CLARITY_TESTS
 bool testMode;
#endif
 string phase="ready";int epoch;CancellationTokenSource cancellation;CleanResult lastResult;List<CleanupIssue> cleanupIssues=new List<CleanupIssue>();long completionRemainingBytes;public Action<CleanResult> SaveHistory;
 Button scan,clean,simple,advancedButton,recommend,visibleSelect,unselect;
 TreeView tree;TextBox search;ComboBox riskFilter;ProgressBar progress;CleanNode detailNode;ScanProgress lastProgress;
 public Controller(){
  Window=(Window)App.LoadView("ClearSpace.Main.xaml");
  notice=new UiNotice(Window);Window.Title=App.DisplayName;UiText.SetLanguage(settings.Language);var logo=App.LoadIcon();Window.Icon=logo;Get<Image>("BrandLogo").Source=logo;ConfirmCleanup=(items,backed)=>RiskConfirmation.Show(Window,items,backed,null,String.IsNullOrEmpty(settings.RecoveryDirectory)?FileRecovery.DefaultDirectory():settings.RecoveryDirectory);
  var area=SystemParameters.WorkArea;Window.MinWidth=Math.Min(Window.MinWidth,Math.Max(820,area.Width-24));Window.MinHeight=Math.Min(Window.MinHeight,Math.Max(580,area.Height-24));Window.Width=Math.Min(Window.Width,Math.Max(Window.MinWidth,area.Width-24));Window.Height=Math.Min(Window.Height,Math.Max(Window.MinHeight,area.Height-24));
  scan=Get<Button>("ScanButton");clean=Get<Button>("CleanButton");simple=Get<Button>("SimpleButton");advancedButton=Get<Button>("AdvancedButton");recommend=Get<Button>("RecommendButton");visibleSelect=Get<Button>("SelectVisibleButton");unselect=Get<Button>("UnselectButton");tree=Get<TreeView>("ResultsTree");search=Get<TextBox>("SearchBox");riskFilter=Get<ComboBox>("RiskFilter");progress=Get<ProgressBar>("Progress");
  string[] icons={"\uE770","\uE8FC","\uE774","\uE943","\uE895","\uE81C","\uE71B","\uE713","\uE72E"};
  string[] hints={"临时文件 · 日志 · 转储","按软件分组的资源与媒体缓存","按浏览器分组的网页缓存","编辑器 · 依赖下载 · 编译缓存","软件与驱动更新下载资料","最近访问 · 搜索与运行历史","明确失效的本地快捷方式","失效路径 · 卸载记录 · 先备份","保留内容可自选，系统核心除外"};
  for(int i=0;i<Groups.Names.Length;i++)groups.Add(new GroupOption{Name=Groups.Names[i],Icon=icons[i],Description=hints[i]});Get<ItemsControl>("ScopeList").ItemsSource=groups;Get<ItemsControl>("ScanStages").ItemsSource=groups;
  riskFilter.ItemsSource=new[]{"全部风险","低风险","需确认 / 使用痕迹","保留 / 风险删除"};riskFilter.SelectedIndex=0;
  RuleFactory=CreateScanRules;QuickRuleFactory=ScanModes.QuickRules;scanMode=Get<ComboBox>("ScanMode");scanMode.ItemsSource=new[]{"快速扫描","常规扫描"};scanMode.SelectedIndex=0;Get<ComboBox>("CleanPolicy").ItemsSource=new[]{"先备份清理（可恢复）","永久删除（不可恢复）"};Get<ComboBox>("CleanPolicy").SelectedIndex=0;Get<ComboBox>("CleanPolicy").SelectionChanged+=(s,e)=>UpdateRecoveryNote();Get<Button>("RuleCenterButton").Click+=(s,e)=>OpenSettings();
  scan.Click+=(s,e)=>{if(busy){if(cancellation!=null)cancellation.Cancel();scan.IsEnabled=false;return;}if(phase=="results"){ShowReady();return;}LastOperation=ScanAsync();};
  systemOption=Get<CheckBox>("SystemCleanupOption");systemOption.Checked+=(s,e)=>UpdateSelection();systemOption.Unchecked+=(s,e)=>UpdateSelection();Get<Button>("OpenBackupButton").Click+=(s,e)=>{try{OpenBackupFolder(BackupFolders.Resolve(settings.RecoveryDirectory,lastResult==null?null:lastResult.FileBackupManifest));}catch(Exception ex){Status("打开备份位置失败："+ex.Message);}};
  Get<Button>("CompleteIssuesButton").Click+=(s,e)=>{if(!busy&&cleanupIssues.Count>0)PushPage(CleanupIssuesPage.Create(cleanupIssues,BackPage,Status));};Get<Button>("DoneButton").Click+=(s,e)=>ShowReady();Get<Button>("CompleteBackupButton").Click+=(s,e)=>Get<Button>("OpenBackupButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));clean.Click+=(s,e)=>{LastOperation=CleanAsync();};simple.Click+=(s,e)=>SetMode(false);advancedButton.Click+=(s,e)=>SetMode(true);
  recommend.Click+=(s,e)=>{SetAll(false);foreach(var n in leaves.Where(n=>n.Finding.Rule.Quick&&!n.Finding.ManualOverride&&!n.Finding.Rule.Protected))n.Selected=true;UpdateSelection();};
  unselect.Click+=(s,e)=>{SetAll(false);UpdateSelection();};visibleSelect.Click+=(s,e)=>{SetAll(false);foreach(var n in VisibleLeaves())n.Selected=true;UpdateSelection();};

  Get<Button>("ExpandButton").Click+=(s,e)=>{foreach(var n in treeNodes)ExpandAll(n,true);};Get<Button>("CollapseButton").Click+=(s,e)=>{foreach(var n in treeNodes)ExpandAll(n,false);};
  Get<Button>("CloseDetailButton").Click+=(s,e)=>Get<FrameworkElement>("DetailPanel").Visibility=Visibility.Collapsed;
  Get<Button>("OpenFolderButton").Click+=(s,e)=>OpenFolder();
  tree.AddHandler(Button.ClickEvent,new RoutedEventHandler((s,e)=>{var button=e.OriginalSource as Button;if(button!=null&&button.Tag as string=="info"){detailNode=button.DataContext as CleanNode;ShowDetail();e.Handled=true;}}));
  tree.PreviewMouseLeftButtonUp+=(s,e)=>{
   if(!advanced||busy)return;var source=e.OriginalSource as DependencyObject;var current=source;
   while(current!=null&&current!=tree){if(current is CheckBox||current is Button||current is ToggleButton||current is ScrollBar)return;var content=current as FrameworkContentElement;current=content!=null?content.Parent:VisualTreeHelper.GetParent(current);}
   var item=ItemsControl.ContainerFromElement(tree,source)as TreeViewItem;if(item==null)return;var node=item.DataContext as CleanNode;if(node!=null&&node.IsLeaf&&node.Selectable)node.Selected=node.Selected!=true;
  };
  search.TextChanged+=(s,e)=>RefreshTree();riskFilter.SelectionChanged+=(s,e)=>RefreshTree();
  Window.Closing+=(s,e)=>{if(!NavigationCanClose())e.Cancel=true;if(busy){e.Cancel=true;if(cancellation!=null)cancellation.Cancel();Status("正在停止…");}};
  ElevateAction=Elevate;ConfirmAdministrator=result=>AdministratorPrompt.Show(Window,result);ShowReady();UiText.Attach(Window);InitializeChrome();InitializeTheme();InitializeSoftwareUpdates();
 }
 List<Rule> CreateScanRules(){int id=epoch;var catalog=DiscoveryCatalog.Build(settings,true,cancellation==null?CancellationToken.None:cancellation.Token,title=>Window.Dispatcher.BeginInvoke(new Action(()=>{if(busy&&phase=="scan"&&id==epoch){Get<TextBlock>("ProgressLabel").Text=title;Get<TextBlock>("ProgressCount").Text="发现应用并匹配规则，尚未执行文件扫描";}})),activeScanSession);LastDiscovery=catalog.Discovery;return catalog.Rules;}
 public void EnableAutomaticRuleUpdates(){
  Func<string,bool> due=value=>{DateTime previous;return !DateTime.TryParse(value,out previous)||(DateTime.UtcNow-previous.ToUniversalTime()).TotalDays>=7;};
  bool ini=settings.Winapp2Enabled&&settings.AutoWinapp2&&due(settings.LastWinapp2Attempt),json=settings.JsonEnabled&&settings.AutoJson&&!String.IsNullOrWhiteSpace(settings.UpdateUrl)&&due(settings.LastUpdateAttempt);if(!ini&&!json)return;string url=settings.UpdateUrl;
  Window.Loaded+=async(s,e)=>{bool failed=false;try{if(ini)settings.LastWinapp2Attempt=DateTime.UtcNow.ToString("o");if(json)settings.LastUpdateAttempt=DateTime.UtcNow.ToString("o");settings.Save();await Task.Run(()=>{if(ini)try{new Winapp2Store().Update(CancellationToken.None);}catch{failed=true;}if(json)try{new RuleStore().Update(url,CancellationToken.None);}catch{failed=true;}});if(!busy)Status(failed?"部分规则更新未应用，继续使用当前规则":"规则已更新，手动扫描后生效");}catch{if(!busy)Status("更新未应用，继续使用当前规则");}};
 }
 void UpdateRecoveryNote(){var control=Get<ComboBox>("CleanPolicy");if(control==null)return;string path=String.IsNullOrEmpty(settings.RecoveryDirectory)?FileRecovery.DefaultDirectory():settings.RecoveryDirectory;var note=Get<TextBlock>("RecoveryNote");note.ToolTip=path;bool same=String.Equals(Path.GetPathRoot(path),RuleBook.TargetDrive,StringComparison.OrdinalIgnoreCase);note.Text=control.SelectedIndex==1?"社区、未知及风险删除先备份":same?"备份在 C 盘；清空后释放空间":"备份至 "+Path.GetPathRoot(path);note.Foreground=App.Brush(same?"#F2B868":"#9DBAC8");}
 public T Get<T>(string name)where T:FrameworkElement{return (T)Window.FindName(name);}
#if CLARITY_TESTS
 public void PreviewRules(string path,int tab){UiMotion.SetEnabled(Window,false);OpenSettings();var root=settingsView.Root;((TabControl)root.FindName("RulesTabs")).SelectedItem=root.FindName(tab==1?"ApplicationsTab":tab==2?"BackupTab":"RulesTab");App.Render(Window,path);}
#endif
 static void ExpandAll(CleanNode n,bool value){n.Expanded=value;foreach(var c in n.Children)ExpandAll(c,value);}
 void SetAll(bool value){foreach(var n in leaves)n.Selected=value;}
 void ShowReady(){
  if(busy)return;phase="ready";hasScanned=false;cleanupIssues.Clear();completionRemainingBytes=0;leaves.Clear();treeNodes.Clear();tree.ItemsSource=null;systemFinding=null;LastDiscovery=null;advanced=false;Get<FrameworkElement>("CompletePage").Visibility=Visibility.Collapsed;Get<FrameworkElement>("HeroPanel").Visibility=Visibility.Visible;Get<Grid>("MainLayout").RowDefinitions[1].Height=new GridLength(142);scanMode.IsEnabled=true;systemOption.IsChecked=false;systemOption.IsEnabled=false;Get<FrameworkElement>("ScopePage").Visibility=Visibility.Visible;Get<FrameworkElement>("ScanPage").Visibility=Get<FrameworkElement>("ResultPage").Visibility=Get<FrameworkElement>("FooterPanel").Visibility=Get<FrameworkElement>("ProgressPanel").Visibility=Get<FrameworkElement>("DetailPanel").Visibility=Visibility.Collapsed;
  scan.Content="开始扫描";Get<TextBlock>("HeroCaption").Text="请选择扫描范围";Get<TextBlock>("HeroSizeText").Text="准备清理 C 盘";Get<TextBlock>("HeroNote").Text="扫描不会删除文件";Status("");clean.IsEnabled=false;UpdateDisk();
 }
 void UpdateDisk(){try{var disk=new DriveInfo(RuleBook.TargetDrive);ShowDiskSpace(disk.TotalSize,disk.AvailableFreeSpace);}catch{Get<TextBlock>("DiskText").Text="磁盘空间暂无法读取";Get<TextBlock>("DiskTotalText").Text=Get<TextBlock>("DiskPercentText").Text="";Get<ProgressBar>("DiskBar").Value=0;Get<ProgressBar>("DiskBar").ToolTip=null;}}
 void ShowDiskSpace(long total,long available){
  total=Math.Max(0,total);available=Math.Max(0,Math.Min(total,available));long occupied=total-available;double percent=total>0?100.0*occupied/total:0;
  Get<TextBlock>("DiskText").Text=RuleBook.TargetDrive.Substring(0,1)+": 可用 "+App.FormatSize(available);Get<TextBlock>("DiskTotalText").Text="总容量 "+App.FormatSize(total);Get<TextBlock>("DiskPercentText").Text="剩余 "+(total>0?100.0*available/total:0).ToString("0.#")+"%";
  var bar=Get<ProgressBar>("DiskBar");bar.Value=percent;bar.Foreground=App.Brush(percent>=95?"#F27777":percent>=85?"#E8BE58":"#39C5BB");bar.ToolTip="已占用 "+App.FormatSize(occupied)+" · 可用 "+App.FormatSize(available)+" / "+App.FormatSize(total);System.Windows.Automation.AutomationProperties.SetName(bar,UiText.T(bar.ToolTip.ToString()));
 }
 void SetBusy(bool value,string kind){
  busy=value;if(value)phase=kind;else if(phase!="complete")phase="results";scanMode.IsEnabled=!value;scan.IsEnabled=true;scan.Content=value?(kind=="scan"?"取消扫描":"停止清理"):"重新扫描";simple.IsEnabled=advancedButton.IsEnabled=recommend.IsEnabled=visibleSelect.IsEnabled=unselect.IsEnabled=tree.IsEnabled=search.IsEnabled=riskFilter.IsEnabled=Get<Button>("RuleCenterButton").IsEnabled=Get<ComboBox>("CleanPolicy").IsEnabled=Get<Button>("OpenBackupButton").IsEnabled=!value;systemOption.IsEnabled=!value&&hasScanned&&systemFinding!=null&&systemFinding.VirtualItems>0;clean.IsEnabled=!value&&phase=="results"&&Chosen().Count>0;
  Get<FrameworkElement>("ProgressPanel").Visibility=value?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=false;if(value){progress.Value=0;Get<TextBlock>("ProgressCount").Text=kind=="scan"?(quickScan?"仅检查内置低风险规则":"发现应用并匹配规则"):"正在准备备份与清理…";Get<TextBlock>("PercentText").Text="0%";Get<TextBlock>("ProgressLabel").Text=kind=="scan"?"准备扫描规则…":"准备清理…";}
 }
 void OnProgress(ScanProgress p,int id){
  if(id!=epoch||!busy||phase!="scan")return;lastProgress=p;progress.Value=p.Total==0?0:100.0*p.Completed/p.Total;Get<TextBlock>("PercentText").Text=((int)progress.Value)+"%";
  Get<TextBlock>("ProgressLabel").Text="正在扫描 · "+p.Title;Get<TextBlock>("ProgressCount").Text=p.Completed+" / "+p.Total+" 条规则 · 已检查 "+p.Files+" 个文件";Get<TextBlock>("HeroCaption").Text="已发现可处理垃圾";Get<TextBlock>("HeroSizeText").Text=App.FormatSize(p.FoundBytes);Get<TextBlock>("HeroNote").Text="容量随扫描实时更新";
  foreach(var g in groups){int total=engine.Rules.Count(r=>r.Group==g.Name),done=engine.Rules.Take(p.Completed).Count(r=>r.Group==g.Name);if(!g.Selected)g.State="未选择";else if(total>0&&done==total)g.State="完成 · "+done+" / "+total;else if(g.Name==p.Group)g.State="正在扫描 · "+p.Title;else g.State="等待扫描";}Status("");
 }
 public async Task ScanAsync(){
  if(busy)return;var names=groups.Where(g=>g.Selected).Select(g=>g.Name).ToArray();if(names.Length==0){Status("请至少选一类内容。");return;}quickScan=scanMode.SelectedIndex==0;advanced=false;LastDiscovery=null;cancellation=new CancellationTokenSource();int id=++epoch;
  foreach(var g in groups)g.State=g.Selected?"等待扫描":"未选择";Get<FrameworkElement>("ScopePage").Visibility=Get<FrameworkElement>("ResultPage").Visibility=Get<FrameworkElement>("FooterPanel").Visibility=Get<FrameworkElement>("DetailPanel").Visibility=Visibility.Collapsed;Get<FrameworkElement>("ScanPage").Visibility=Visibility.Visible;Get<TextBlock>("HeroSizeText").Text="0 B";Get<TextBlock>("HeroCaption").Text="已发现可处理垃圾";Get<TextBlock>("HeroNote").Text="扫描不会删除文件";SetBusy(true,"scan");
  try{var findings=await Task.Run(()=>{using(var session=new ScanSession(settings.ScanCacheEnabled)){activeScanSession=session;try{var selectedRules=quickScan?QuickRuleFactory().Where(ScanModes.IsQuickRule):RuleFactory();engine=new Engine(selectedRules.Where(r=>names.Contains(r.Group)).OrderBy(r=>Array.IndexOf(Groups.Names,r.Group)).ThenBy(r=>r.Quick?0:1).ThenBy(r=>r.Software).ToList(),session);if(!String.IsNullOrEmpty(settings.RecoveryDirectory))engine.FileBackupDirectory=settings.RecoveryDirectory;engine.LiveProgress=p=>Window.Dispatcher.BeginInvoke(new Action(()=>OnProgress(p,id)));var scanned=engine.Scan(cancellation.Token,null);session.Complete(cancellation.Token);LastScanStatistics=session.Statistics;return scanned;}finally{activeScanSession=null;}}});cancellation.Token.ThrowIfCancellationRequested();Apply(findings);progress.Value=100;}
  catch(OperationCanceledException){leaves.Clear();hasScanned=false;busy=false;ShowReady();Status("扫描已取消，文件未被修改。");}
  catch(Exception ex){leaves.Clear();hasScanned=false;busy=false;ShowReady();Status("扫描未完成："+ex.Message);}
  finally{SetBusy(false,"");if(!hasScanned){phase="ready";scan.Content="开始扫描";clean.IsEnabled=false;}cancellation.Dispose();}
 }
 CleanNode Leaf(Finding f,string title){var n=new CleanNode{Finding=f,Title=title,Depth=2,ShowChecks=advanced,Changed=UpdateSelection};n.Selected=f.Rule.Quick&&!f.ManualOverride&&!f.Rule.Protected;return n;}
 void Apply(List<Finding> findings){
  hasScanned=true;leaves=new List<CleanNode>();if(!engine.Rules.Contains(systemRule))engine.Rules.Add(systemRule);systemFinding=engine.ScanRule(systemRule,CancellationToken.None);systemOption.IsEnabled=!busy&&systemFinding.VirtualItems>0;
  foreach(var f in findings.Where(f=>f.Rule.InfoKind!="system-disk-cleanup")){
   if(f.RegistryCandidates.Count>0)foreach(var e in f.RegistryCandidates){var subset=new Finding{Rule=f.Rule,RegistryCandidates=new List<RegistryEntry>{e},VirtualItems=f.VirtualItems,Extra=f.Extra,Samples=new List<string>{RegistryCleaner.Label(e)}};leaves.Add(Leaf(subset,e.Scope.Kind=="run"||e.Scope.Kind=="search"?f.Rule.Part:e.Title));}
   else {if(!f.Rule.Protected||f.ManualCandidates.Count==0)leaves.Add(Leaf(f,f.Rule.Part??f.Rule.Title));if(f.ManualCandidates.Count>0&&!quickScan){var manual=new Finding{Rule=f.Rule,ManualOverride=true,Candidates=f.ManualCandidates.ToList(),EligibleBytes=f.ManualBytes,TotalBytes=f.ManualBytes,Files=f.ManualCandidates.Count,Running=f.Running,Samples=f.ManualCandidates.Take(5).Select(p=>p.Path).ToList(),Extra="原本保留内容；自选时绕过年龄与个人资料保留策略，始终先备份。"};leaves.Add(Leaf(manual,(f.Rule.Part??f.Rule.Title)+(f.Rule.Protected?" · 自选删除":" · 保留内容自选删除")));}}
  }
  phase="results";scan.Content="重新扫描";Get<FrameworkElement>("ScopePage").Visibility=Get<FrameworkElement>("ScanPage").Visibility=Get<FrameworkElement>("ProgressPanel").Visibility=Visibility.Collapsed;Get<FrameworkElement>("ResultPage").Visibility=Get<FrameworkElement>("FooterPanel").Visibility=Visibility.Visible;
  var fileLeaves=leaves.Where(n=>n.Finding.Rule.InfoKind!="registry-clean"&&n.Finding.RegistryCandidates.Count==0).ToList();Get<TextBlock>("HeroCaption").Text=quickScan?"可一键处理 · 内置低风险":"可选择处理（含风险项目）";Get<TextBlock>("HeroSizeText").Text=App.FormatSize(fileLeaves.Sum(n=>n.Finding.EligibleBytes));Get<TextBlock>("HeroNote").Text=fileLeaves.Count+" 个识别结果";
  Get<TextBlock>("QuickMenuSize").Text="可一键处理 "+App.FormatSize(leaves.Where(n=>n.Finding.Rule.Quick&&!n.Finding.ManualOverride&&!n.Finding.Rule.Protected).Sum(n=>n.Finding.EligibleBytes));Get<TextBlock>("AdvancedMenuSize").Text="按分类、软件逐项选择";advancedButton.Visibility=quickScan?Visibility.Collapsed:Visibility.Visible;SetMode(advanced);UpdateDisk();
 }
 List<CleanNode> VisibleLeaves(){
  IEnumerable<CleanNode> source=advanced?leaves:leaves.Where(n=>n.Finding.Rule.Quick&&!n.Finding.ManualOverride&&!n.Finding.Rule.Protected);string q=(search.Text??"").Trim();int risk=riskFilter.SelectedIndex;
  if(advanced&&risk>0)source=source.Where(n=>risk==1?n.Finding.Rule.Quick&&!n.Finding.ManualOverride:risk==2?n.Finding.ManualOverride||!n.Finding.Rule.Quick&&!n.Finding.Rule.Protected:n.Finding.ManualOverride||n.Finding.Rule.Protected||n.Finding.Running);
  if(q.Length>0)source=source.Where(n=>(n.Title+" "+n.Finding.Rule.Software+" "+n.Finding.Rule.Group+(n.Finding.Rule.Heuristic?" 未知缓存":"")).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0||UiText.T(n.Title+" "+n.Finding.Rule.Software+" "+n.Finding.Rule.Group+(n.Finding.Rule.Heuristic?" 未知缓存":"")).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0);
  return source.ToList();
 }
 void RefreshTree(){
  if(tree==null||!hasScanned)return;var visible=VisibleLeaves();foreach(var n in leaves){n.ShowChecks=advanced;n.Depth=2;}treeNodes=new List<CleanNode>();
  foreach(string name in Groups.Names){var inGroup=visible.Where(n=>n.Finding.Rule.Group==name).ToList();if(inGroup.Count==0)continue;var root=new CleanNode{Title=name,Depth=0,Icon=groups.First(g=>g.Name==name).Icon,ShowChecks=advanced,Expanded=name=="软件缓存"||name=="网页缓存",Changed=UpdateSelection};
   if(name=="保留项目"){
    var community=inGroup.Where(n=>n.Finding.Rule.Source==Winapp2Store.Url&&(n.Finding.Rule.Category=="软件缓存"||n.Finding.Rule.Category=="网页缓存")).ToList();
    foreach(var category in community.GroupBy(n=>n.Finding.Rule.Category).OrderBy(g=>g.Key=="软件缓存"?0:1)){
     var apps=category.GroupBy(n=>n.Finding.Rule.Software??n.Finding.Rule.Title).OrderByDescending(a=>a.Sum(n=>n.Finding.EligibleBytes)).Select(app=>SoftwareNode(app,2)).ToList();root.Children.Add(new CleanNode{Title=category.Key,Depth=1,Children=apps,ShowChecks=advanced,Expanded=search.Text.Length>0,Changed=UpdateSelection});
    }
    inGroup=inGroup.Except(community).ToList();
   }
   foreach(var app in inGroup.GroupBy(n=>n.Finding.Rule.Software??n.Finding.Rule.Title).OrderByDescending(a=>a.Sum(n=>n.Finding.EligibleBytes))){
    if(name!="软件缓存"&&name!="网页缓存"&&name!="开发缓存"&&app.Count()==1){var leaf=app.First();leaf.Depth=1;root.Children.Add(leaf);}
    else {var appNode=SoftwareNode(app,1);if(name=="注册表")appNode.Expanded=true;root.Children.Add(appNode);}
   }treeNodes.Add(root);
  }
  tree.ItemsSource=treeNodes;Get<TextBlock>("EmptyText").Visibility=visible.Count==0?Visibility.Visible:Visibility.Collapsed;Get<TextBlock>("ResultCount").Text=visible.Count+" 项";UpdateSelection();
 }
 CleanNode SoftwareNode(IGrouping<string,CleanNode> app,int depth){
  var children=app.Where(n=>!n.Finding.Rule.Heuristic).ToList();foreach(var node in children)node.Depth=depth+1;var unknown=app.Where(n=>n.Finding.Rule.Heuristic).ToList();if(unknown.Count>0){foreach(var node in unknown)node.Depth=depth+2;children.Add(new CleanNode{Title="未知缓存",Depth=depth+1,Children=unknown,ShowChecks=advanced,Expanded=search.Text.Length>0,Changed=UpdateSelection});}return new CleanNode{Title=app.Key,Depth=depth,Children=children,ShowChecks=advanced,Expanded=search.Text.Length>0,Changed=UpdateSelection};
 }
 void SetMode(bool value){value=value&&!quickScan;advanced=value;simple.Background=App.Brush(value?"#202C36":"#254B49");simple.BorderBrush=App.Brush(value?"#33424E":"#39C5BB");advancedButton.Background=App.Brush(value?"#254B49":"#202C36");advancedButton.BorderBrush=App.Brush(value?"#39C5BB":"#33424E");Get<FrameworkElement>("AdvancedActions").Visibility=riskFilter.Visibility=value?Visibility.Visible:Visibility.Collapsed;Get<TextBlock>("ModeHint").Text=value?"需确认项目默认不选":quickScan?"快速扫描 · 内置低风险项目":"仅处理低风险缓存";RefreshTree();}
 List<Finding> Chosen(){var chosen=leaves.Where(n=>n.Selectable&&(advanced?n.Selected==true:n.Finding.Rule.Quick&&!n.Finding.ManualOverride&&!n.Finding.Rule.Protected)).Select(n=>n.Finding).ToList();if(systemOption!=null&&systemOption.IsChecked==true&&systemFinding!=null&&systemFinding.VirtualItems>0)chosen.Add(systemFinding);return chosen;}
 void UpdateSelection(){
  if(clean==null)return;UpdateRecoveryNote();var chosen=phase=="results"?Chosen():new List<Finding>();long bytes=chosen.Sum(f=>f.EligibleBytes);int regs=chosen.Sum(f=>f.RegistryCandidates.Count);
  Get<TextBlock>("PlannedText").Text=App.FormatSize(bytes)+(regs>0?" + "+regs+" 项注册表":"")+(chosen.Any(f=>f.Rule.InfoKind=="system-disk-cleanup")?" + 系统清理":"");int hidden=leaves.Count(n=>n.Selected==true&&n.Selectable&&!VisibleLeaves().Contains(n));Get<TextBlock>("SelectionText").Text=chosen.Count+" 项已选"+(advanced&&hidden>0?" · 含筛选外 "+hidden+" 项":"");Get<TextBlock>("SelectionText").Foreground=App.Brush(advanced&&hidden>0?"#F2B868":"#A2B5C4");clean.Content=advanced?"清理已选":"一键清理";clean.IsEnabled=!busy&&phase=="results"&&chosen.Count>0;foreach(var n in treeNodes)n.Refresh();
 }
 void ShowDetail(){
  if(detailNode==null||detailNode.Finding==null)return;var f=detailNode.Finding;Get<FrameworkElement>("DetailPanel").Visibility=Visibility.Visible;Get<TextBlock>("DetailTitle").Text=f.Rule.Software+" · "+detailNode.Title;Get<TextBlock>("DetailImpact").Text=f.Rule.Description+"\n清理影响："+f.Rule.Impact+(f.ManualOverride?"\n风险删除：会处理原本保留的近期、个人或备份数据，可能导致账号注销、设置重置与记录丢失；始终先备份。":"");Get<TextBlock>("DetailRange").Text=(f.Rule.InfoKind=="registry-clean"?"本项可处理 "+f.RegistryCandidates.Count+" 项。"+f.Extra:((f.Rule.InfoKind=="system-disk-cleanup"||f.Rule.InfoKind=="disable-hibernation")?f.Extra:(f.ManualOverride?"自选风险删除，保留策略已解除":"普通候选保留最近 "+f.Rule.Days+" 天")+" · 已读取 "+f.Files+" 个文件 · "+f.Candidates.Count+" 个候选 · "+f.ManualCandidates.Count+" 个保留候选"+(f.Running?" · 使用中":f.Unreadable>0?" · "+f.Unreadable+" 项无法读取":"")+(f.Links>0?" · "+f.Links+" 项链接/只读/云端文件":"")))+(String.IsNullOrEmpty(f.Rule.Detection)?"":"\n识别依据："+f.Rule.Detection);
  Get<TextBox>("DetailPaths").Text=String.Join("\n",f.Rule.Roots.Concat(f.Samples.Take(2)));Get<Button>("OpenFolderButton").Content=f.Rule.InfoKind=="storage"?"Windows 存储设置":"打开位置";Get<Button>("OpenFolderButton").IsEnabled=f.Rule.InfoKind=="storage"||f.Rule.Roots.Length>0;
 }
#if CLARITY_TESTS
 public void PreviewScan(){var catalog=DiscoveryCatalog.Build(settings,true,CancellationToken.None,null);LastDiscovery=catalog.Discovery;engine=new Engine(catalog.Rules);if(!String.IsNullOrEmpty(settings.RecoveryDirectory))engine.FileBackupDirectory=settings.RecoveryDirectory;Apply(engine.Scan(CancellationToken.None,null));Status("扫描完成");}
#endif
 async Task CleanAsync(){
  if(busy||phase!="results")return;var chosen=Chosen();if(chosen.Count==0)return;
  bool backed=Get<ComboBox>("CleanPolicy").SelectedIndex==0;
  // Only selected projects with cleanup risks require the confirmation dialog.
  if(RiskConfirmation.Required(chosen)&&(ConfirmCleanup==null||!ConfirmCleanup(chosen,backed)))return;
  bool quickMode=!advanced&&!chosen.Any(f=>f.Rule.InfoKind=="system-disk-cleanup");cancellation=new CancellationTokenSource();++epoch;SetBusy(true,"clean");engine.UseFileBackup=backed;engine.FileBackupDirectory=settings.RecoveryDirectory.Length>0?settings.RecoveryDirectory:FileRecovery.DefaultDirectory();engine.RegistryBackupDirectory=BackupDirectory();
#if CLARITY_TESTS
  if(testMode)engine.RegistryBackupDirectory=Path.Combine(engine.FileBackupDirectory,"registry-test-backups");
#endif
  long beforeBytes=leaves.Sum(n=>n.Finding.EligibleBytes);int cleanEpoch=epoch;object progressLock=new object();Tuple<int,int,string> latest=null;
  var refresh=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
  refresh.Tick+=(s,e)=>{Tuple<int,int,string> value;lock(progressLock){value=latest;latest=null;}if(value==null||!busy||phase!="clean"||epoch!=cleanEpoch)return;int done=value.Item1,total=value.Item2;progress.IsIndeterminate=done<0;progress.Value=total>0?Math.Min(97,97.0*done/total):0;Get<TextBlock>("PercentText").Text=done<0?"系统清理":((int)progress.Value)+"%";Get<TextBlock>("ProgressLabel").Text=done<0?value.Item3:"正在清理 · "+value.Item3;Get<TextBlock>("ProgressCount").Text=done<0?"由 Windows 处理，完成后自动关闭系统工具":done+" / "+total+" 个文件";};refresh.Start();
  string logError=null;
  try{
   lastResult=await Task.Run(()=>engine.Clean(chosen,quickMode,cancellation.Token,(done,total,title)=>{lock(progressLock)latest=Tuple.Create(done,total,title);}));
  }catch(Exception ex){lastResult=new CleanResult{Failed=1,PermissionDenied=ex is UnauthorizedAccessException?1:0,Cancelled=cancellation.IsCancellationRequested};foreach(var f in chosen)foreach(var file in f.Candidates)lastResult.Items.Add(new AuditItem{Rule=f.Rule.Title,Path=file.Path,Status="清理失败",Reason=ex.Message});}
  finally{refresh.Stop();}
  try{
   if(lastResult.SystemCleanupStarted)systemOption.IsChecked=false;progress.IsIndeterminate=false;progress.Value=98;Get<TextBlock>("PercentText").Text="98%";Get<TextBlock>("ProgressLabel").Text="正在汇总清理结果…";
   await Task.Run(()=>{cleanupIssues=CleanupIssues.Build(chosen,lastResult);logError=SaveLog();});
   completionRemainingBytes=Math.Max(0,beforeBytes-lastResult.Bytes);progress.Value=100;Get<TextBlock>("PercentText").Text="100%";ShowComplete();
  }finally{SetBusy(false,"");cancellation.Dispose();}
  if(logError!=null)Status("清理记录未保存："+logError);OfferAdministrator(lastResult);
 }

 public void OfferAdministrator(CleanResult result){if(AdministratorPrompt.ShouldOffer(result,AdministratorState())&&ConfirmAdministrator!=null&&ConfirmAdministrator(result)&&ElevateAction!=null)ElevateAction();}
 void ShowComplete(){
  phase="complete";var issuesButton=Get<Button>("CompleteIssuesButton");issuesButton.Visibility=cleanupIssues.Count>0?Visibility.Visible:Visibility.Collapsed;issuesButton.Content="查看未完成项目（"+cleanupIssues.Count+"）";Get<FrameworkElement>("HeroPanel").Visibility=Get<FrameworkElement>("ScopePage").Visibility=Get<FrameworkElement>("ScanPage").Visibility=Get<FrameworkElement>("ResultPage").Visibility=Get<FrameworkElement>("FooterPanel").Visibility=Get<FrameworkElement>("ProgressPanel").Visibility=Get<FrameworkElement>("DetailPanel").Visibility=Visibility.Collapsed;Get<Grid>("MainLayout").RowDefinitions[1].Height=new GridLength(0);Get<FrameworkElement>("CompletePage").Visibility=Visibility.Visible;
  Get<TextBlock>("CompleteTitle").Text=lastResult.Cancelled?"清理已停止":cleanupIssues.Count>0||lastResult.Failed>0||lastResult.SystemCleanupStarted&&!lastResult.SystemCleanupCompleted?"清理结束 · 部分未完成":"清理完成";Get<TextBlock>("CompleteSize").Text=App.FormatSize(lastResult.EstimatedFreedBytes);
  Get<TextBlock>("CompleteDetails").Text="已处理 "+lastResult.Deleted+" 个文件 · 移出 "+App.FormatSize(lastResult.Bytes)+(lastResult.RegistryDeleted>0?" · "+lastResult.RegistryDeleted+" 项注册记录":"")+(lastResult.BackupFiles>0?"\n已备份 "+lastResult.BackupFiles+" 个文件，可恢复":"");
  long remaining=completionRemainingBytes;string note="跳过 "+lastResult.Skipped+" 项 · 失败 "+lastResult.Failed+" 项"+(remaining>0?" · 仍有 "+App.FormatSize(remaining)+" 可自选内容（含风险项目）":"");if(lastResult.BackupFiles>0&&lastResult.EstimatedFreedBytes==0)note+="\n备份与原文件在同一磁盘；清空备份后才释放对应空间。";if(lastResult.HibernationDisabled)note+="\nWindows 休眠已关闭；如需恢复，以管理员身份运行 powercfg /hibernate on。";if(lastResult.SystemCleanupStarted)note+="\n"+lastResult.SystemCleanupMessage;Get<TextBlock>("CompleteRemaining").Text=note;UpdateDisk();PlayCompletion();
 }
 static string BackupDirectory(){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClearSpace","RegistryBackups");}
 string SaveLog(){
#if CLARITY_TESTS
  if(testMode&&SaveHistory==null)return null;
#endif
  try{if(SaveHistory!=null)SaveHistory(lastResult);else CleaningHistory.Save(lastResult);return null;}catch(Exception ex){return ex.Message;}}

 void OpenFolder(){if(detailNode==null)return;var r=detailNode.Finding.Rule;try{if(r.InfoKind=="storage"){Process.Start(new ProcessStartInfo("ms-settings:storagesense"){UseShellExecute=true});return;}string target=r.Roots.FirstOrDefault(p=>Directory.Exists(p)||File.Exists(p));if(target!=null)Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),File.Exists(target)?"/select,\""+target+"\"":"\""+target+"\""){UseShellExecute=true});}catch(Exception ex){Status(ex.Message);}}
 void Elevate(){try{Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location){UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});Window.Close();}catch{Status("仍以普通账户运行");}}
}
public static class App {
 public static string DisplayName{get{return "Clarity";}}
 internal static object LoadView(string name){
  var assembly=Assembly.GetExecutingAssembly();
  using(var stream=assembly.GetManifestResourceStream(name)){
   if(stream==null)throw new IOException("界面资源缺失："+name);
   using(var reader=new StreamReader(stream)){
    // Embedded views resolve their local controls from the assembly hosting them.
    string xaml=reader.ReadToEnd().Replace("clr-namespace:ClearSpace;assembly=Clarity","clr-namespace:ClearSpace;assembly="+assembly.GetName().Name);
    var view=(FrameworkElement)XamlReader.Parse(UiTheme.Markup(xaml));UiTheme.Attach(view);return view;
   }
  }
 }
 public static BitmapSource LoadIcon(){using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ClearSpace.AppIcon.ico")){if(stream==null)throw new IOException("程序图标资源缺失");var icon=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames.OrderByDescending(f=>f.PixelWidth).First();icon.Freeze();return icon;}}
 public static Brush Brush(string color){return UiTheme.Brush(color);}
 public static bool IsAdmin(){try{return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);}catch{return false;}}
 public static string FormatSize(long bytes){if(bytes>=1024L*1024*1024)return (bytes/(1024.0*1024*1024)).ToString("0.00")+" GB";if(bytes>=1024*1024)return (bytes/(1024.0*1024)).ToString("0.0")+" MB";if(bytes>=1024)return (bytes/1024.0).ToString("0.0")+" KB";return bytes+" B";}
#if CLARITY_TESTS
 public static void WriteJson(string path,object data){File.WriteAllText(path,new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue,RecursionLimit=100}.Serialize(data),new UTF8Encoding(false));}
#endif
 [STAThread]public static int Main(string[] args){try{
  if(args.Length==2&&args[0]=="--apply-update")return UpdateLauncher.Apply(args[1]);
  if(args.Length==2&&args[0]=="--complete-service-update")return ServiceUpdateClient.Complete(args[1]);
#if CLARITY_TESTS
  if(args.Length==3&&args[0]=="--production-self-test")return ProductionArtifactTests.Run(args[1],args[2]);
  if(args.Length==2&&args[0]=="--software-update-probe"){var release=SoftwareUpdates.Check(CancellationToken.None);WriteJson(args[1],new{release.Version,release.Build,release.Notes,Assets=release.Assets.Select(a=>new{a.Name,a.Size,a.Url,a.SHA256})});return 0;}
  if(args.Length==2&&args[0]=="--common-rules-self-test")return CommonRulesTests.Run(args[1]);
  if(args.Length==2&&args[0]=="--builtin-catalog"){var rules=RuleBook.Create();WriteJson(args[1],new{Version=ReleaseVersion.Display,RuleCount=rules.Count,QuickCount=rules.Count(ScanModes.IsQuickRule),Catalog=rules.Select(r=>new{r.Id,r.Title,Group=r.Group,r.Software,r.Part,r.Quick,r.Protected,r.ForceBackup,r.AllowManual,r.Privacy,r.InfoKind,r.Days,r.Source,r.Description,r.Impact})});return 0;}
  if(args.Length==2&&args[0]=="--self-test")return SelfTest.Run(args[1]);if(args.Length==2&&args[0]=="--v2-self-test")return V2Tests.Run(args[1]);
  if(args.Length==2&&args[0]=="--recovery-self-test")return RecoveryTests.Run(args[1]);
  if(args.Length==2&&args[0]=="--beta-self-test")return BetaTests.Run(args[1]);
  if(args.Length==2&&args[0]=="--v3-self-test")return V3Tests.Run(args[1]);
  if(args.Length==2&&args[0]=="--winapp2-self-test")return Winapp2Tests.Run(args[1]);
  if(args.Length==2&&args[0]=="--winapp2-update-test"){var pack=new Winapp2Store(args[1]).Update(CancellationToken.None);Directory.CreateDirectory(args[1]);WriteJson(Path.Combine(args[1],"download-result.json"),new{pack.Version,Entries=pack.Entries.Count,pack.FileKeys,pack.RegistryKeys,pack.Hash,pack.Origin});return 0;}
  if(args.Length==2&&args[0]=="--winapp2-report"){var pack=new Winapp2Store().Current();var report=Winapp2Adapter.Analyze(pack,Winapp2Environment.Windows());File.WriteAllLines(args[1],new[]{"Winapp2 "+pack.Version+" / "+report.Summary,"SHA256 "+pack.Hash}.Concat(report.Lines),new UTF8Encoding(true));return 0;}
  if(args.Length==2&&args[0]=="--make-rule-template"){WriteJson(args[1],RuleStore.Defaults());return 0;}
  if(args.Length==2&&(args[0]=="--scan-report"||args[0]=="--discovery-report")){var catalog=DiscoveryCatalog.Build(CleanerSettings.Load(),true,CancellationToken.None,null);var rules=catalog.Rules;var findings=new Engine(rules).Scan(CancellationToken.None,null);WriteJson(args[1],new{ScannedAt=DateTime.Now.ToString("o"),RuleCount=rules.Count,RuleVersion=catalog.RuleVersion,Trust=catalog.Trust,Discovery=catalog.Discovery,Catalog=rules.Select(r=>new{r.Id,r.Title,Group=r.Group,r.Software,r.Part,r.Quick,r.Protected,r.Privacy,r.Heuristic,r.InfoKind,r.Days,r.Source,r.Description,r.Impact,r.Detection}),Findings=findings.Select(f=>new{f.Rule.Id,f.Rule.Title,Group=f.Rule.Group,f.Rule.Software,f.TotalBytes,f.EligibleBytes,f.ManualBytes,ManualFiles=f.ManualCandidates.Count,f.Files,f.Running,f.Unreadable,f.Recent,f.Rule.Protected,f.Rule.Heuristic,RegistryItems=f.RegistryCandidates.Count,f.Extra})});return 0;}
#else
  if(args.Length!=0)throw new ArgumentException("Unsupported launch arguments.");
#endif
  var application=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};var c=new Controller();application.MainWindow=c.Window;
  if(args.Length==0){c.PrepareStartup();c.EnableAutomaticRuleUpdates();c.EnableAutomaticSoftwareUpdates();}
#if CLARITY_TESTS
  if(args.Length==2&&args[0]=="--ui-self-test"){Directory.CreateDirectory(args[1]);Hide(c.Window);c.Window.Loaded+=async(s,e)=>{try{var tests=await c.VerifyUI(args[1]);File.WriteAllLines(Path.Combine(args[1],"ui-test-results.txt"),tests.Concat(new[]{"ALL "+tests.Count+" UI CHECKS PASSED"}));c.Window.Close();}catch(Exception ex){File.WriteAllText(Path.Combine(args[1],"ui-test-results.txt"),ex.ToString());application.Shutdown(1);}};}
  else if(args.Length==2&&args[0].StartsWith("--preview")){
   if(args[0]!="--preview-start"&&args[0]!="--preview-scanning")c.PreviewScan();if(args[0]=="--preview"||args[0]=="--preview-small"||args[0]=="--preview-registry")c.Get<Button>("AdvancedButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(args[0]=="--preview-small"){c.Window.Width=1080;c.Window.Height=740;}if(args[0]=="--preview-registry"){c.Get<TextBox>("SearchBox").Text="失效";c.Get<Button>("ExpandButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}if(args[0]=="--preview-rescan")c.Get<Button>("ScanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Hide(c.Window);
   if(args[0]=="--preview-scanning"){c.Window.Loaded+=async(s,e)=>{var task=c.ScanAsync();await Task.Delay(300);Render(c.Window,args[1]);c.Get<Button>("ScanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await task;c.Window.Close();};}
   else c.Window.Loaded+=(s,e)=>c.Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{if(args[0]=="--preview-rules"||args[0]=="--preview-apps"||args[0]=="--preview-recovery")c.PreviewRules(args[1],args[0]=="--preview-apps"?1:args[0]=="--preview-recovery"?2:0);else Render(c.Window,args[1]);c.Window.Close();}));
  }
#endif
  application.Run(c.Window);return 0;
 }catch(Exception ex){
#if CLARITY_TESTS
  if(args.Length>=2){try{File.WriteAllText(args[1]+".error.txt",ex.ToString());}catch{}}else
#endif
  UiText.Show("无法启动："+ex.Message,"澄净");return 1;}}
#if CLARITY_TESTS
 static void Hide(Window w){w.ShowInTaskbar=false;w.ShowActivated=false;w.Left=-10000;w.Top=-10000;w.WindowStartupLocation=WindowStartupLocation.Manual;}
 public static void Render(Window w,string path){w.UpdateLayout();var surface=(FrameworkElement)w.Content;var bitmap=new RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);}
#endif
}
}
