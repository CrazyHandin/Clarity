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

[assembly: AssemblyTitle("澄净 · C 盘清理 · by CraZyX")]
[assembly: AssemblyDescription("便携式 Windows 缓存与残留清理工具")]
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
 public bool Selectable{get{return IsLeaf?!Finding.Rule.Protected&&!Finding.Running&&(Finding.Candidates.Count>0||Finding.RegistryCandidates.Count>0):Children.Any(c=>c.Selectable);}}
 public bool? Selected{get{if(IsLeaf)return selected;var leaves=Leaves().Where(n=>n.Selectable).ToList();if(leaves.Count==0)return false;int count=leaves.Count(n=>n.selected);return count==0?(bool?)false:count==leaves.Count?(bool?)true:null;}set{SetSelection(value!=false);if(Changed!=null)Changed();}}
 void SetSelection(bool value){if(IsLeaf){selected=value&&Selectable;Notify("Selected");}else foreach(var n in Children)n.SetSelection(value);Notify("Selected");}
 public bool Expanded{get{return expanded;}set{expanded=value;Notify("Expanded");}}
 public Visibility CheckVisibility{get{return ShowChecks?Visibility.Visible:Visibility.Collapsed;}}
 public Visibility InfoVisibility{get{return IsLeaf?Visibility.Visible:Visibility.Collapsed;}}
 public double TitleSize{get{return Depth==0?18:Depth==1?16:14;}}
 public FontWeight TitleWeight{get{return Depth<2?FontWeights.SemiBold:FontWeights.Normal;}}
 public Brush TitleColor{get{return App.Brush(Depth==0?"#ECF3F8":Depth==1?"#DAE6EE":"#BFD0DD");}}
 string RiskCode{get{var list=Leaves().ToList();return list.All(n=>n.Finding.Rule.Protected||n.Finding.Running)?"protected":list.Any(n=>n.Finding.Rule.Heuristic)?"heuristic":list.Any(n=>n.Finding.Rule.ForceBackup)?"community":list.Any(n=>n.Finding.Rule.Privacy)?"privacy":list.Any(n=>!n.Finding.Rule.Quick&&!n.Finding.Rule.Protected)?"manual":"quick";}}
 public string Risk{get{return RiskCode=="protected"?"保留":RiskCode=="heuristic"?"疑似 · 需核对":RiskCode=="community"?(IsLeaf?"社区 · 先备份":"含社区项"):RiskCode=="privacy"?"使用痕迹":RiskCode=="quick"?"低风险":IsLeaf?"需确认":"含需确认项";}}
 public Brush RiskColor{get{return App.Brush(RiskCode=="protected"?"#87B5DF":RiskCode=="privacy"?"#F69DB5":RiskCode=="quick"?"#39C5BB":"#F2B868");}}
 public Brush RiskBackground{get{return App.Brush(RiskCode=="protected"?"#27394C":RiskCode=="privacy"?"#402C3B":RiskCode=="quick"?"#1B3B3B":"#41372A");}}
 public Visibility RiskVisibility{get{return Depth>0?Visibility.Visible:Visibility.Collapsed;}}
 public string Summary{get{
  var leaves=Leaves().ToList();long bytes=leaves.Sum(n=>n.Finding.EligibleBytes),chosen=leaves.Where(n=>n.selected).Sum(n=>n.Finding.EligibleBytes);int regs=leaves.Sum(n=>n.Finding.RegistryCandidates.Count),chosenRegs=leaves.Where(n=>n.selected).Sum(n=>n.Finding.RegistryCandidates.Count);
  if(IsLeaf){if(Finding.Rule.Protected)return "不会删除"+(Finding.TotalBytes>0?" · "+App.FormatSize(Finding.TotalBytes):"");if(Finding.Running)return "不会删除 · 使用中";if(Finding.Rule.InfoKind=="registry-clean")return regs>0?"可处理 "+regs+" 项":"未发现可清理项";return bytes>0?"可处理 "+App.FormatSize(bytes):"不会删除 · 暂无候选";}
  string value=bytes>0?App.FormatSize(bytes):regs>0?regs+" 项注册记录":"暂无可处理文件";return value+(chosen>0?" · 已选 "+App.FormatSize(chosen):chosenRegs>0?" · 已选 "+chosenRegs+" 项":"");
 }}
 public Brush SummaryColor{get{return Depth>0?RiskColor:App.Brush("#C7D7E3");}}
 public void Notify(string name){if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs(name));}
 public void Refresh(){Notify("Selected");Notify("Summary");foreach(var c in Children)c.Refresh();}
 public event PropertyChangedEventHandler PropertyChanged;
}
public sealed class Controller {
 public Window Window;public Task LastOperation;public Func<List<Rule>> RuleFactory;public DiscoveryResult LastDiscovery;
 CleanerSettings settings=CleanerSettings.Load();bool heuristicRequested;
 Engine engine=new Engine(new List<Rule>());List<GroupOption> groups=new List<GroupOption>();List<CleanNode> leaves=new List<CleanNode>(),treeNodes=new List<CleanNode>();
 bool busy,advanced,hasScanned,testMode;string phase="ready";int epoch;CancellationTokenSource cancellation;CleanResult lastResult;
 Button scan,clean,simple,advancedButton,recommend,visibleSelect,unselect,admin,export,restore;
 TreeView tree;TextBox search;ComboBox riskFilter;ProgressBar progress;CleanNode detailNode;ScanProgress lastProgress;
 public Controller(){
  using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("ClearSpace.Main.xaml"))Window=(Window)XamlReader.Load(resource);
  var area=SystemParameters.WorkArea;Window.MinWidth=Math.Min(Window.MinWidth,Math.Max(820,area.Width-24));Window.MinHeight=Math.Min(Window.MinHeight,Math.Max(580,area.Height-24));Window.Width=Math.Min(Window.Width,Math.Max(Window.MinWidth,area.Width-24));Window.Height=Math.Min(Window.Height,Math.Max(Window.MinHeight,area.Height-24));
  scan=Get<Button>("ScanButton");clean=Get<Button>("CleanButton");simple=Get<Button>("SimpleButton");advancedButton=Get<Button>("AdvancedButton");recommend=Get<Button>("RecommendButton");visibleSelect=Get<Button>("SelectVisibleButton");unselect=Get<Button>("UnselectButton");admin=Get<Button>("AdminButton");export=Get<Button>("ExportButton");restore=Get<Button>("RestoreButton");tree=Get<TreeView>("ResultsTree");search=Get<TextBox>("SearchBox");riskFilter=Get<ComboBox>("RiskFilter");progress=Get<ProgressBar>("Progress");
  string[] icons={"\uE770","\uE8FC","\uE774","\uE943","\uE895","\uE81C","\uE71B","\uE713","\uE72E"};
  string[] hints={"临时文件 · 日志 · 转储","按软件分组的资源与媒体缓存","按浏览器分组的网页缓存","编辑器 · 依赖下载 · 编译缓存","软件与驱动更新下载资料","最近访问 · 搜索与运行历史","明确失效的本地快捷方式","失效路径 · 卸载记录 · 先备份","系统与恢复资料，保留不删"};
  for(int i=0;i<Groups.Names.Length;i++)groups.Add(new GroupOption{Name=Groups.Names[i],Icon=icons[i],Description=hints[i]});Get<ItemsControl>("ScopeList").ItemsSource=groups;Get<ItemsControl>("ScanStages").ItemsSource=groups;
  riskFilter.ItemsSource=new[]{"全部风险","低风险","需确认 / 使用痕迹","保留不删"};riskFilter.SelectedIndex=0;
  RuleFactory=CreateScanRules;Get<ComboBox>("CleanPolicy").ItemsSource=new[]{"先备份清理（可恢复）","永久删除（需确认）"};Get<ComboBox>("CleanPolicy").SelectedIndex=0;Get<ComboBox>("CleanPolicy").SelectionChanged+=(s,e)=>UpdateRecoveryNote();Get<Button>("RuleCenterButton").Click+=(s,e)=>{if(!busy)RuleCenter.Show(Window,settings,LastDiscovery,()=>{UpdateRecoveryNote();Get<TextBlock>("StatusText").Text="设置已更新，重新扫描生效";});};
  scan.Click+=(s,e)=>{if(busy){if(cancellation!=null)cancellation.Cancel();scan.IsEnabled=false;return;}if(phase=="results"){ShowReady();return;}LastOperation=ScanAsync();};
  clean.Click+=(s,e)=>{LastOperation=CleanAsync();};simple.Click+=(s,e)=>SetMode(false);advancedButton.Click+=(s,e)=>SetMode(true);
  recommend.Click+=(s,e)=>{SetAll(false);foreach(var n in leaves.Where(n=>n.Finding.Rule.Quick))n.Selected=true;UpdateSelection();};
  unselect.Click+=(s,e)=>{SetAll(false);UpdateSelection();};visibleSelect.Click+=(s,e)=>{SetAll(false);foreach(var n in VisibleLeaves())n.Selected=true;UpdateSelection();};
  admin.Click+=(s,e)=>Elevate();export.Click+=(s,e)=>Export();restore.Click+=(s,e)=>RestoreRegistry();
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
  Window.Closing+=(s,e)=>{if(busy){e.Cancel=true;if(cancellation!=null)cancellation.Cancel();Get<TextBlock>("StatusText").Text="正在停止…";}};
  if(App.IsAdmin()){admin.Content="管理员";admin.IsEnabled=false;}ShowReady();
 }
 List<Rule> CreateScanRules(){int id=epoch;var catalog=DiscoveryCatalog.Build(settings,heuristicRequested,cancellation==null?CancellationToken.None:cancellation.Token,title=>Window.Dispatcher.BeginInvoke(new Action(()=>{if(busy&&phase=="scan"&&id==epoch){Get<TextBlock>("ProgressLabel").Text=title;Get<TextBlock>("ProgressCount").Text="发现应用并匹配规则，尚未执行文件扫描";}})));LastDiscovery=catalog.Discovery;return catalog.Rules;}
 public void EnableAutomaticRuleUpdates(){if(!settings.AutomaticUpdates||!settings.UseWinapp2&&String.IsNullOrWhiteSpace(settings.UpdateUrl))return;bool community=settings.UseWinapp2;string url=settings.UpdateUrl;DateTime previous;if(DateTime.TryParse(community?settings.LastWinapp2Attempt:settings.LastUpdateAttempt,out previous)&&(DateTime.UtcNow-previous.ToUniversalTime()).TotalDays<7)return;Window.Loaded+=async(s,e)=>{try{if(community)settings.LastWinapp2Attempt=DateTime.UtcNow.ToString("o");else settings.LastUpdateAttempt=DateTime.UtcNow.ToString("o");settings.Save();await Task.Run(()=>{if(community)new Winapp2Store().Update(CancellationToken.None);else new RuleStore().Update(url,CancellationToken.None);});if(!busy)Get<TextBlock>("StatusText").Text="规则已更新，手动扫描后生效";}catch{if(!busy)Get<TextBlock>("StatusText").Text="更新未应用，继续使用当前规则";}};}
 void UpdateRecoveryNote(){var control=Get<ComboBox>("CleanPolicy");if(control==null)return;string path=String.IsNullOrEmpty(settings.RecoveryDirectory)?FileRecovery.DefaultDirectory():settings.RecoveryDirectory;var note=Get<TextBlock>("RecoveryNote");note.ToolTip=path;bool same=String.Equals(Path.GetPathRoot(path),RuleBook.TargetDrive,StringComparison.OrdinalIgnoreCase);note.Text=control.SelectedIndex==1?"社区及疑似项目先备份":same?"备份在 C 盘；清空后释放空间":"备份至 "+Path.GetPathRoot(path);note.Foreground=App.Brush(same?"#F2B868":"#9DBAC8");}
 public T Get<T>(string name)where T:FrameworkElement{return (T)Window.FindName(name);}
 public void PreviewRules(string path,int tab){RuleCenter.Show(Window,settings,LastDiscovery,()=>{},path,tab);}
 static void ExpandAll(CleanNode n,bool value){n.Expanded=value;foreach(var c in n.Children)ExpandAll(c,value);}
 void SetAll(bool value){foreach(var n in leaves)n.Selected=value;}
 void ShowReady(){
  if(busy)return;phase="ready";Get<FrameworkElement>("ScopePage").Visibility=Visibility.Visible;Get<FrameworkElement>("ScanPage").Visibility=Get<FrameworkElement>("ResultPage").Visibility=Get<FrameworkElement>("FooterPanel").Visibility=Get<FrameworkElement>("ProgressPanel").Visibility=Get<FrameworkElement>("DetailPanel").Visibility=Visibility.Collapsed;
  scan.Content="开始扫描";Get<TextBlock>("HeroCaption").Text="选择范围后开始";Get<TextBlock>("HeroSizeText").Text="准备清理 C 盘";Get<TextBlock>("HeroNote").Text="扫描不会删除文件";Get<TextBlock>("StatusText").Text="";clean.IsEnabled=false;UpdateDisk();
 }
 void UpdateDisk(){try{var disk=new DriveInfo(RuleBook.TargetDrive);Get<TextBlock>("DiskText").Text=RuleBook.TargetDrive.Substring(0,1)+": 可用 "+App.FormatSize(disk.AvailableFreeSpace)+" / "+App.FormatSize(disk.TotalSize);Get<ProgressBar>("DiskBar").Value=100*disk.AvailableFreeSpace/(double)disk.TotalSize;}catch{Get<TextBlock>("DiskText").Text="磁盘空间暂无法读取";}}
 void SetBusy(bool value,string kind){
  busy=value;phase=value?kind:"results";scan.IsEnabled=true;scan.Content=value?(kind=="scan"?"取消扫描":"停止清理"):"重新扫描";simple.IsEnabled=advancedButton.IsEnabled=recommend.IsEnabled=visibleSelect.IsEnabled=unselect.IsEnabled=tree.IsEnabled=search.IsEnabled=riskFilter.IsEnabled=restore.IsEnabled=Get<Button>("RuleCenterButton").IsEnabled=Get<ComboBox>("CleanPolicy").IsEnabled=!value;admin.IsEnabled=!value&&!App.IsAdmin();clean.IsEnabled=!value&&Chosen().Count>0;
  Get<FrameworkElement>("ProgressPanel").Visibility=value?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=false;if(value){progress.Value=0;Get<TextBlock>("PercentText").Text="0%";Get<TextBlock>("ProgressLabel").Text=kind=="scan"?"准备扫描规则…":"准备清理…";}
 }
 void OnProgress(ScanProgress p,int id){
  if(id!=epoch||!busy||phase!="scan")return;lastProgress=p;progress.Value=p.Total==0?0:100.0*p.Completed/p.Total;Get<TextBlock>("PercentText").Text=((int)progress.Value)+"%";
  Get<TextBlock>("ProgressLabel").Text="正在扫描 · "+p.Title;Get<TextBlock>("ProgressCount").Text=p.Completed+" / "+p.Total+" 条规则 · 已检查 "+p.Files+" 个文件";Get<TextBlock>("HeroCaption").Text="已发现可处理垃圾";Get<TextBlock>("HeroSizeText").Text=App.FormatSize(p.FoundBytes);Get<TextBlock>("HeroNote").Text=p.RegistryItems>0?"另有 "+p.RegistryItems+" 项注册表候选，需确认":"容量随扫描实时更新";
  foreach(var g in groups){int total=engine.Rules.Count(r=>r.Group==g.Name),done=engine.Rules.Take(p.Completed).Count(r=>r.Group==g.Name);if(!g.Selected)g.State="未选择";else if(total>0&&done==total)g.State="完成 · "+done+" / "+total;else if(g.Name==p.Group)g.State="正在扫描 · "+p.Title;else g.State="等待扫描";}Get<TextBlock>("StatusText").Text="";
 }
 public async Task ScanAsync(){
  if(busy)return;var names=groups.Where(g=>g.Selected).Select(g=>g.Name).ToArray();if(names.Length==0){Get<TextBlock>("StatusText").Text="请至少选一类内容。";return;}heuristicRequested=Get<CheckBox>("HeuristicOption").IsChecked==true;cancellation=new CancellationTokenSource();int id=++epoch;
  foreach(var g in groups)g.State=g.Selected?"等待扫描":"未选择";Get<FrameworkElement>("ScopePage").Visibility=Get<FrameworkElement>("ResultPage").Visibility=Get<FrameworkElement>("FooterPanel").Visibility=Get<FrameworkElement>("DetailPanel").Visibility=Visibility.Collapsed;Get<FrameworkElement>("ScanPage").Visibility=Visibility.Visible;Get<TextBlock>("HeroSizeText").Text="0 B";Get<TextBlock>("HeroCaption").Text="已发现可处理垃圾";Get<TextBlock>("HeroNote").Text="扫描不会删除文件";SetBusy(true,"scan");
  try{var findings=await Task.Run(()=>{engine=new Engine(RuleFactory().Where(r=>names.Contains(r.Group)).OrderBy(r=>Array.IndexOf(Groups.Names,r.Group)).ThenBy(r=>r.Quick?0:1).ThenBy(r=>r.Software).ToList());if(!String.IsNullOrEmpty(settings.RecoveryDirectory))engine.FileBackupDirectory=settings.RecoveryDirectory;engine.LiveProgress=p=>Window.Dispatcher.BeginInvoke(new Action(()=>OnProgress(p,id)));return engine.Scan(cancellation.Token,null);});Apply(findings);Get<TextBlock>("StatusText").Text="扫描完成";progress.Value=100;}
  catch(OperationCanceledException){leaves.Clear();hasScanned=false;busy=false;ShowReady();Get<TextBlock>("StatusText").Text="扫描已取消，文件未被修改。";}
  catch(Exception ex){leaves.Clear();hasScanned=false;busy=false;ShowReady();Get<TextBlock>("StatusText").Text="扫描未完成："+ex.Message;}
  finally{SetBusy(false,"");if(!hasScanned){phase="ready";scan.Content="开始扫描";clean.IsEnabled=false;}cancellation.Dispose();}
 }
 CleanNode Leaf(Finding f,string title){var n=new CleanNode{Finding=f,Title=title,Depth=2,ShowChecks=advanced,Changed=UpdateSelection};n.Selected=f.Rule.Quick;return n;}
 void Apply(List<Finding> findings){
  hasScanned=true;leaves=new List<CleanNode>();
  foreach(var f in findings){
   if(f.RegistryCandidates.Count>0)foreach(var e in f.RegistryCandidates){var subset=new Finding{Rule=f.Rule,RegistryCandidates=new List<RegistryEntry>{e},VirtualItems=f.VirtualItems,Extra=f.Extra,Samples=new List<string>{RegistryCleaner.Label(e)}};leaves.Add(Leaf(subset,e.Scope.Kind=="run"||e.Scope.Kind=="search"?f.Rule.Part:e.Title));}
   else leaves.Add(Leaf(f,f.Rule.Part??f.Rule.Title));
  }
  phase="results";scan.Content="重新扫描";Get<FrameworkElement>("ScopePage").Visibility=Get<FrameworkElement>("ScanPage").Visibility=Get<FrameworkElement>("ProgressPanel").Visibility=Visibility.Collapsed;Get<FrameworkElement>("ResultPage").Visibility=Get<FrameworkElement>("FooterPanel").Visibility=Visibility.Visible;
  Get<TextBlock>("HeroCaption").Text="可处理垃圾";Get<TextBlock>("HeroSizeText").Text=App.FormatSize(leaves.Sum(n=>n.Finding.EligibleBytes));int reg=leaves.Sum(n=>n.Finding.RegistryCandidates.Count);Get<TextBlock>("HeroNote").Text=reg>0?leaves.Count+" 个结果 · "+reg+" 项注册记录另行确认":leaves.Count+" 个识别结果";
  Get<TextBlock>("QuickMenuSize").Text="可一键处理 "+App.FormatSize(leaves.Where(n=>n.Finding.Rule.Quick).Sum(n=>n.Finding.EligibleBytes));Get<TextBlock>("AdvancedMenuSize").Text="按分类、软件逐项选择";SetMode(advanced);UpdateDisk();
 }
 List<CleanNode> VisibleLeaves(){
  IEnumerable<CleanNode> source=advanced?leaves:leaves.Where(n=>n.Finding.Rule.Quick);string q=(search.Text??"").Trim();int risk=riskFilter.SelectedIndex;
  if(advanced&&risk>0)source=source.Where(n=>risk==1?n.Finding.Rule.Quick:risk==2?!n.Finding.Rule.Quick&&!n.Finding.Rule.Protected:n.Finding.Rule.Protected||n.Finding.Running);
  if(q.Length>0)source=source.Where(n=>(n.Title+" "+n.Finding.Rule.Software+" "+n.Finding.Rule.Group).IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0);
  return source.ToList();
 }
 void RefreshTree(){
  if(tree==null||!hasScanned)return;var visible=VisibleLeaves();foreach(var n in leaves)n.ShowChecks=advanced;treeNodes=new List<CleanNode>();
  foreach(string name in Groups.Names){var inGroup=visible.Where(n=>n.Finding.Rule.Group==name).ToList();if(inGroup.Count==0)continue;var root=new CleanNode{Title=name,Depth=0,Icon=groups.First(g=>g.Name==name).Icon,ShowChecks=advanced,Expanded=name=="软件缓存"||name=="网页缓存",Changed=UpdateSelection};
   foreach(var app in inGroup.GroupBy(n=>n.Finding.Rule.Software??n.Finding.Rule.Title).OrderByDescending(a=>a.Sum(n=>n.Finding.EligibleBytes))){
    if(name!="软件缓存"&&name!="网页缓存"&&name!="开发缓存"&&app.Count()==1){var leaf=app.First();leaf.Depth=1;root.Children.Add(leaf);}
    else root.Children.Add(new CleanNode{Title=app.Key,Depth=1,Children=app.ToList(),ShowChecks=advanced,Expanded=search.Text.Length>0||name=="注册表",Changed=UpdateSelection});
   }treeNodes.Add(root);
  }
  tree.ItemsSource=treeNodes;Get<TextBlock>("EmptyText").Visibility=visible.Count==0?Visibility.Visible:Visibility.Collapsed;Get<TextBlock>("ResultCount").Text=visible.Count+" 项";UpdateSelection();
 }
 void SetMode(bool value){advanced=value;simple.Background=App.Brush(value?"#202C36":"#254B49");simple.BorderBrush=App.Brush(value?"#33424E":"#39C5BB");advancedButton.Background=App.Brush(value?"#254B49":"#202C36");advancedButton.BorderBrush=App.Brush(value?"#39C5BB":"#33424E");Get<FrameworkElement>("AdvancedActions").Visibility=riskFilter.Visibility=value?Visibility.Visible:Visibility.Collapsed;Get<TextBlock>("ModeHint").Text=value?"需确认项目默认不选":"仅处理低风险缓存";RefreshTree();}
 List<Finding> Chosen(){return leaves.Where(n=>n.Selectable&&(advanced?n.Selected==true:n.Finding.Rule.Quick)).Select(n=>n.Finding).ToList();}
 void UpdateSelection(){
  if(clean==null)return;UpdateRecoveryNote();var chosen=phase=="results"?Chosen():new List<Finding>();long bytes=chosen.Sum(f=>f.EligibleBytes);int regs=chosen.Sum(f=>f.RegistryCandidates.Count);
  Get<TextBlock>("PlannedText").Text=App.FormatSize(bytes)+(regs>0?" + "+regs+" 项注册表":"");int hidden=leaves.Count(n=>n.Selected==true&&n.Selectable&&!VisibleLeaves().Contains(n));Get<TextBlock>("SelectionText").Text=chosen.Count+" 项已选"+(advanced&&hidden>0?" · 含筛选外 "+hidden+" 项":"");Get<TextBlock>("SelectionText").Foreground=App.Brush(advanced&&hidden>0?"#F2B868":"#A2B5C4");clean.Content=advanced?"清理已选":"一键清理";clean.IsEnabled=!busy&&phase=="results"&&chosen.Count>0;foreach(var n in treeNodes)n.Refresh();
 }
 void ShowDetail(){
  if(detailNode==null||detailNode.Finding==null)return;var f=detailNode.Finding;Get<FrameworkElement>("DetailPanel").Visibility=Visibility.Visible;Get<TextBlock>("DetailTitle").Text=f.Rule.Software+" · "+detailNode.Title;Get<TextBlock>("DetailImpact").Text=f.Rule.Description+"\n清理影响："+f.Rule.Impact;Get<TextBlock>("DetailRange").Text=(f.Rule.InfoKind=="registry-clean"?"本项可处理 "+f.RegistryCandidates.Count+" 项。"+f.Extra:"保留最近 "+f.Rule.Days+" 天 · "+f.Candidates.Count+" 个候选文件"+(f.Running?" · 使用中，保留":f.Unreadable>0?" · 部分无权限":""))+(String.IsNullOrEmpty(f.Rule.Detection)?"":"\n识别依据："+f.Rule.Detection);
  Get<TextBox>("DetailPaths").Text=String.Join("\n",f.Rule.Roots.Concat(f.Samples.Take(2)));Get<Button>("OpenFolderButton").Content=f.Rule.InfoKind=="storage"?"Windows 存储设置":"打开位置";Get<Button>("OpenFolderButton").IsEnabled=f.Rule.InfoKind=="storage"||f.Rule.Roots.Length>0;
 }
 public void PreviewScan(){var catalog=DiscoveryCatalog.Build(settings,false,CancellationToken.None,null);LastDiscovery=catalog.Discovery;engine=new Engine(catalog.Rules);if(!String.IsNullOrEmpty(settings.RecoveryDirectory))engine.FileBackupDirectory=settings.RecoveryDirectory;Apply(engine.Scan(CancellationToken.None,null));Get<TextBlock>("StatusText").Text="扫描完成";}
 async Task CleanAsync(){
  if(busy||phase!="results")return;var chosen=Chosen();if(chosen.Count==0)return;int regs=chosen.Sum(f=>f.RegistryCandidates.Count);
  bool backed=Get<ComboBox>("CleanPolicy").SelectedIndex==0;if((advanced||!backed)&&!testMode){string message=(backed?"将先备份清理 ":"将永久删除 ")+App.FormatSize(chosen.Sum(f=>f.EligibleBytes))+" 的所选文件"+(regs>0?"，以及 "+regs+" 项注册表记录（先自动备份）":"")+"。\n\n";
   var risky=chosen.Where(f=>!f.Rule.Quick).Select(f=>f.Rule.Software+" · "+(f.Rule.Part??f.Rule.Title)).Distinct();if(risky.Any())message+="需确认："+String.Join("、",risky.Take(8))+(risky.Count()>8?"等 "+risky.Count()+" 项":"")+"。\n\n";if(chosen.Any(f=>f.Rule.ForceBackup||f.Rule.Heuristic))message+="社区及疑似项目始终先备份，所选永久删除方式不作用于这些项目。\n\n";var warnings=chosen.Select(f=>f.Rule.Warning).Where(t=>!String.IsNullOrWhiteSpace(t)).Distinct().Take(3);if(warnings.Any())message+="原规则警告："+String.Join("\n",warnings)+"\n\n";message+="使用中、近期或扫描后发生变化的内容会保留。是否清理？";if(MessageBox.Show(Window,message,"确认所选内容",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
  }
  bool quickMode=!advanced;cancellation=new CancellationTokenSource();++epoch;SetBusy(true,"clean");engine.UseFileBackup=backed;engine.FileBackupDirectory=settings.RecoveryDirectory.Length>0?settings.RecoveryDirectory:FileRecovery.DefaultDirectory();engine.RegistryBackupDirectory=testMode?Path.Combine(engine.FileBackupDirectory,"registry-test-backups"):BackupDirectory();
  try{lastResult=await Task.Run(()=>engine.Clean(chosen,quickMode,cancellation.Token,(done,total,title)=>Window.Dispatcher.BeginInvoke(new Action(()=>{if(!busy||phase!="clean")return;progress.Value=total>0?100.0*done/total:0;Get<TextBlock>("PercentText").Text=((int)progress.Value)+"%";Get<TextBlock>("ProgressLabel").Text="正在清理 · "+title;Get<TextBlock>("ProgressCount").Text=done+" / "+total+" 个文件";}))));export.IsEnabled=true;SaveLog();var refreshed=await Task.Run(()=>engine.Scan(CancellationToken.None,null));Apply(refreshed);Get<TextBlock>("StatusText").Text=(lastResult.Cancelled?"已停止":"清理完成")+" · 移出 "+App.FormatSize(lastResult.Bytes)+" · 估算释放 "+App.FormatSize(lastResult.EstimatedFreedBytes)+(lastResult.BackupFiles>0?" · 已备份":"")+(lastResult.RegistryDeleted>0?" / "+lastResult.RegistryDeleted+" 项注册表":"")+" · 跳过 "+lastResult.Skipped+" 项";}
  catch(Exception ex){Get<TextBlock>("StatusText").Text="清理停止："+ex.Message;}finally{SetBusy(false,"");cancellation.Dispose();}
 }
 static string BackupDirectory(){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClearSpace","RegistryBackups");}
 void SaveLog(){if(testMode)return;try{string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClearSpace","Logs");Directory.CreateDirectory(root);App.WriteJson(Path.Combine(root,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json"),lastResult);}catch{}}
 void Export(){if(lastResult==null)return;var dialog=new SaveFileDialog{Title="导出清理记录",Filter="清理记录 (*.json)|*.json",FileName="澄净-清理记录.json"};if(dialog.ShowDialog(Window)==true){try{App.WriteJson(dialog.FileName,lastResult);}catch(Exception ex){MessageBox.Show(Window,ex.Message);}}}
 void RestoreRegistry(){if(busy)return;var dialog=new OpenFileDialog{Title="选择澄净生成的注册表备份",Filter="澄净注册表备份 (*.json)|*.json",InitialDirectory=Directory.Exists(BackupDirectory())?BackupDirectory():AppDomain.CurrentDomain.BaseDirectory};if(dialog.ShowDialog(Window)!=true)return;if(MessageBox.Show(Window,"恢复本机备份中的缺失注册表项。已存在的项不会被覆盖。","恢复注册表备份",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;try{int count=RegistryCleaner.Restore(dialog.FileName);Get<TextBlock>("StatusText").Text="已恢复 "+count+" 项注册表；现有项保留。";}catch(Exception ex){MessageBox.Show(Window,"恢复失败："+ex.Message);}}
 void OpenFolder(){if(detailNode==null)return;var r=detailNode.Finding.Rule;try{if(r.InfoKind=="storage"){Process.Start(new ProcessStartInfo("ms-settings:storagesense"){UseShellExecute=true});return;}string target=r.Roots.FirstOrDefault(p=>Directory.Exists(p)||File.Exists(p));if(target!=null)Process.Start(new ProcessStartInfo("explorer.exe",File.Exists(target)?"/select,\""+target+"\"":"\""+target+"\""){UseShellExecute=true});}catch(Exception ex){Get<TextBlock>("StatusText").Text=ex.Message;}}
 void Elevate(){try{Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location){UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});Window.Close();}catch{Get<TextBlock>("StatusText").Text="仍以普通账户运行";}}
 public async Task<List<string>> VerifyUI(string output){
  testMode=true;var tests=new List<string>();Action<bool,string> verify=(value,name)=>{if(!value)throw new Exception("UI FAILED: "+name);tests.Add("PASS: "+name);};
  settings.RecoveryDirectory=Path.Combine(output,"ui-backups");UpdateRecoveryNote();verify(Get<Button>("RuleCenterButton")!=null&&Get<CheckBox>("HeuristicOption").IsChecked!=true&&Get<ComboBox>("CleanPolicy").SelectedIndex==0,"Application rules, optional heuristics and recoverable cleanup are available without automatic scanning");
  verify(engine.Rules.Count==0&&!hasScanned&&!busy,"Startup performs no scan");verify(Get<FrameworkElement>("ResultPage").Visibility==Visibility.Collapsed&&Get<FrameworkElement>("FooterPanel").Visibility==Visibility.Collapsed,"Cleanup modes and footer are absent before scanning");verify(scan.Content.ToString()=="开始扫描"&&Get<Button>("StartButton")==null&&Get<Button>("ScopeButton")==null,"Only one scan action exists with no separate scope button");verify(groups.Count==9,"Nine scope cards are selectable");
  string root=Path.Combine(output,"ui-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var safe=new Rule("ui-safe","Cache","软件缓存","desc","impact",new[]{Path.Combine(root,"safe")},7,true){Software="Fixture App",Part="资源缓存"};var manual=new Rule("ui-manual","Media","软件缓存","desc","impact",new[]{Path.Combine(root,"manual")},7,false){Software="Fixture App",Part="媒体缓存"};var protect=new Rule("ui-protect","Settings","保留项目","desc","impact",new[]{Path.Combine(root,"protect")},0,false){Protected=true,Software="Fixture App",Part="设置"};
  foreach(var r in new[]{safe,manual,protect}){Directory.CreateDirectory(r.Roots[0]);string file=Path.Combine(r.Roots[0],"test.dat");File.WriteAllText(file,"test cache");File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddDays(-30));}
  int scanStarts=0;RuleFactory=()=>{Interlocked.Increment(ref scanStarts);Thread.Sleep(100);return new List<Rule>{safe,manual,protect};};foreach(var g in groups)g.Selected=g.Name=="软件缓存";scan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  verify(busy&&phase=="scan"&&Get<FrameworkElement>("ProgressPanel").Visibility==Visibility.Visible&&!progress.IsIndeterminate,"Scanning uses a visible determinate progress panel");verify(Get<FrameworkElement>("ResultPage").Visibility==Visibility.Collapsed&&scan.Content.ToString()=="取消扫描","Cleanup modes stay hidden while scanning");await LastOperation;
  verify(engine.Rules.Count==2&&hasScanned,"Scan uses only selected scopes");verify(Get<FrameworkElement>("ResultPage").Visibility==Visibility.Visible,"Cleanup modes appear after scanning");verify(progress.Value==100&&lastProgress!=null,"Progress reports actual rule completion and reaches 100 percent");verify(!advanced&&VisibleLeaves().Count==1&&clean.IsEnabled,"Simple mode includes only eligible low-risk cache");verify(Get<TextBlock>("QuickMenuSize").Text.Contains("可一键处理"),"One-click capacity is displayed inside its menu");verify(treeNodes[0].Children[0].Title=="Fixture App"&&treeNodes[0].Children[0].Children.Count==1,"Software results are nested in a software submenu");
  scan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(phase=="ready"&&!busy&&scanStarts==1&&Get<FrameworkElement>("ScopePage").Visibility==Visibility.Visible&&Get<TextBlock>("HeroSizeText").Text=="准备清理 C 盘"&&scan.Content.ToString()=="开始扫描","Rescan returns to the preparation page without starting a scan");verify(groups.Count(g=>g.Selected)==1&&groups.Single(g=>g.Selected).Name=="软件缓存"&&Get<FrameworkElement>("ResultPage").Visibility==Visibility.Collapsed&&Get<FrameworkElement>("FooterPanel").Visibility==Visibility.Collapsed&&!clean.IsEnabled,"Rescan preserves category choices and hides cleanup controls");
  foreach(var g in groups)g.Selected=true;scan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await LastOperation;advancedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(scanStarts==2&&advanced&&VisibleLeaves().Count==3,"Explicit start scans updated choices and advanced mode includes manual and retained results");
  CleanNode safeNode=leaves.First(n=>n.Finding.Rule==safe),manualNode=leaves.First(n=>n.Finding.Rule==manual),protectedNode=leaves.First(n=>n.Finding.Rule==protect);verify(!protectedNode.Selectable&&protectedNode.Summary.StartsWith("不会删除"),"Protected result is prominent and cannot be selected");
  var parent=treeNodes.First(n=>n.Title=="软件缓存").Children[0];verify(parent.Selected==null,"Mixed selection uses a tri-state software checkbox");parent.Selected=true;verify(Chosen().Count==2,"Software submenu checkbox selects its eligible child items");unselect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(Chosen().Count==0&&!clean.IsEnabled,"Clear selection disables cleanup");
  search.Text="媒体";verify(VisibleLeaves().Count==1,"Search filters nested items");visibleSelect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(Chosen().Count==1&&Chosen()[0].Rule==manual,"Select visible replaces selection with current filtered items");search.Text="";riskFilter.SelectedIndex=3;verify(VisibleLeaves().Count==1,"Risk filter isolates retained content");riskFilter.SelectedIndex=0;
  recommend.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(Chosen().Count==1&&Chosen()[0].Rule==safe,"Recommended selection excludes higher-risk data");detailNode=manualNode;ShowDetail();verify(Get<TextBlock>("DetailTitle").Text.Contains("媒体缓存")&&Get<TextBox>("DetailPaths").Text.Contains(manual.Roots[0]),"On-demand details display impact and paths");
  scan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(phase=="ready"&&scanStarts==2&&!clean.IsEnabled&&Get<FrameworkElement>("ResultPage").Visibility==Visibility.Collapsed&&Get<FrameworkElement>("DetailPanel").Visibility==Visibility.Collapsed&&Get<FrameworkElement>("ProgressPanel").Visibility==Visibility.Collapsed,"Rescan from advanced results hides cleanup, details and progress without scanning");scan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await LastOperation;simple.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));clean.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await LastOperation;
  verify(!File.Exists(Path.Combine(safe.Roots[0],"test.dat")),"Actual one-click button cleans isolated low-risk cache");verify(File.Exists(Path.Combine(manual.Roots[0],"test.dat"))&&File.Exists(Path.Combine(protect.Roots[0],"test.dat")),"Actual cleanup preserves manual and protected data");verify(lastResult.Deleted==1&&export.IsEnabled&&!busy&&!clean.IsEnabled,"Cleanup updates counts, export and control state");
  verify(lastResult.BackupFiles==1&&File.Exists(lastResult.FileBackupManifest)&&FileRecovery.Read(lastResult.FileBackupManifest).Count==1,"Actual one-click cleanup writes a recoverable file backup");
  return tests;
 }
}
public static class App {
 public static Brush Brush(string color){return (Brush)new BrushConverter().ConvertFromString(color);}
 public static bool IsAdmin(){try{return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);}catch{return false;}}
 public static string FormatSize(long bytes){if(bytes>=1024L*1024*1024)return (bytes/(1024.0*1024*1024)).ToString("0.00")+" GB";if(bytes>=1024*1024)return (bytes/(1024.0*1024)).ToString("0.0")+" MB";if(bytes>=1024)return (bytes/1024.0).ToString("0.0")+" KB";return bytes+" B";}
 public static void WriteJson(string path,object data){File.WriteAllText(path,new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue,RecursionLimit=100}.Serialize(data),new UTF8Encoding(false));}
 [STAThread]public static int Main(string[] args){try{
  if(args.Length==2&&args[0]=="--self-test")return SelfTest.Run(args[1]);if(args.Length==2&&args[0]=="--v2-self-test")return V2Tests.Run(args[1]);
  if(args.Length==2&&args[0]=="--v3-self-test")return V3Tests.Run(args[1]);
  if(args.Length==2&&args[0]=="--winapp2-self-test")return Winapp2Tests.Run(args[1]);
  if(args.Length==2&&args[0]=="--winapp2-update-test"){var pack=new Winapp2Store(args[1]).Update(CancellationToken.None);Directory.CreateDirectory(args[1]);WriteJson(Path.Combine(args[1],"download-result.json"),new{pack.Version,Entries=pack.Entries.Count,pack.FileKeys,pack.RegistryKeys,pack.Hash,pack.Origin});return 0;}
  if(args.Length==2&&args[0]=="--winapp2-report"){var pack=new Winapp2Store().Current();var report=Winapp2Adapter.Analyze(pack,Winapp2Environment.Windows());File.WriteAllLines(args[1],new[]{"Winapp2 "+pack.Version+" / "+report.Summary,"SHA256 "+pack.Hash}.Concat(report.Lines),new UTF8Encoding(true));return 0;}
  if(args.Length==2&&args[0]=="--make-rule-template"){WriteJson(args[1],RuleStore.Defaults());return 0;}
  if(args.Length==2&&(args[0]=="--scan-report"||args[0]=="--discovery-report")){var catalog=DiscoveryCatalog.Build(CleanerSettings.Load(),args[0]=="--discovery-report",CancellationToken.None,null);var rules=catalog.Rules;var findings=new Engine(rules).Scan(CancellationToken.None,null);WriteJson(args[1],new{ScannedAt=DateTime.Now.ToString("o"),RuleCount=rules.Count,RuleVersion=catalog.RuleVersion,Trust=catalog.Trust,Discovery=catalog.Discovery,Catalog=rules.Select(r=>new{r.Id,r.Title,Group=r.Group,r.Software,r.Part,r.Quick,r.Protected,r.Privacy,r.Heuristic,r.InfoKind,r.Days,r.Source,r.Description,r.Impact,r.Detection}),Findings=findings.Select(f=>new{f.Rule.Id,f.Rule.Title,Group=f.Rule.Group,f.Rule.Software,f.TotalBytes,f.EligibleBytes,f.Files,f.Running,f.Unreadable,f.Recent,f.Rule.Protected,f.Rule.Heuristic,RegistryItems=f.RegistryCandidates.Count,f.Extra})});return 0;}
  var application=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};var c=new Controller();application.MainWindow=c.Window;
  if(args.Length==0)c.EnableAutomaticRuleUpdates();
  if(args.Length==2&&args[0]=="--ui-self-test"){Directory.CreateDirectory(args[1]);Hide(c.Window);c.Window.Loaded+=async(s,e)=>{try{var tests=await c.VerifyUI(args[1]);File.WriteAllLines(Path.Combine(args[1],"ui-test-results.txt"),tests.Concat(new[]{"ALL "+tests.Count+" UI CHECKS PASSED"}));c.Window.Close();}catch(Exception ex){File.WriteAllText(Path.Combine(args[1],"ui-test-results.txt"),ex.ToString());application.Shutdown(1);}};}
  else if(args.Length==2&&args[0].StartsWith("--preview")){
   if(args[0]!="--preview-start"&&args[0]!="--preview-scanning")c.PreviewScan();if(args[0]=="--preview"||args[0]=="--preview-small"||args[0]=="--preview-registry")c.Get<Button>("AdvancedButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(args[0]=="--preview-small"){c.Window.Width=1080;c.Window.Height=740;}if(args[0]=="--preview-registry"){c.Get<TextBox>("SearchBox").Text="失效";c.Get<Button>("ExpandButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}if(args[0]=="--preview-rescan")c.Get<Button>("ScanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Hide(c.Window);
   if(args[0]=="--preview-scanning"){c.Window.Loaded+=async(s,e)=>{var task=c.ScanAsync();await Task.Delay(300);Render(c.Window,args[1]);c.Get<Button>("ScanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await task;c.Window.Close();};}
   else c.Window.Loaded+=(s,e)=>c.Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{if(args[0]=="--preview-rules"||args[0]=="--preview-apps"||args[0]=="--preview-recovery")c.PreviewRules(args[1],args[0]=="--preview-apps"?1:args[0]=="--preview-recovery"?2:0);else Render(c.Window,args[1]);c.Window.Close();}));
  }
  application.Run(c.Window);return 0;
 }catch(Exception ex){if(args.Length>=2){try{File.WriteAllText(args[1]+".error.txt",ex.ToString());}catch{}}else MessageBox.Show("无法启动："+ex.Message,"澄净");return 1;}}
 static void Hide(Window w){w.ShowInTaskbar=false;w.ShowActivated=false;w.Left=-10000;w.Top=-10000;w.WindowStartupLocation=WindowStartupLocation.Manual;}
 public static void Render(Window w,string path){w.UpdateLayout();var surface=(FrameworkElement)w.Content;var bitmap=new RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);}
}
}
