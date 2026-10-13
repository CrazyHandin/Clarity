using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
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
[assembly: AssemblyDescription("便携式 Windows 缓存识别与清理工具")]
namespace ClearSpace {
public sealed class GroupOption : INotifyPropertyChanged {
 public string Name {get;set;} public string Description {get;set;} public string Icon {get;set;} bool selected=true;
 public bool Selected {get{return selected;}set{selected=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("Selected"));}}
 public event PropertyChangedEventHandler PropertyChanged;
}
public sealed class Row : INotifyPropertyChanged {
 public Finding Finding; public Action OnChanged; bool selected; bool advanced;
 public bool Advanced {get{return advanced;}set{advanced=value;Changed("CheckVisibility");}}
 public string Title {get{return Finding.Rule.Title;}}
 public string Category {get{return Finding.Rule.Group;}}
 public string Risk {get{return Finding.Rule.Protected?"protected":Finding.Rule.Privacy?"privacy":Finding.Rule.Quick?"quick":"manual";}}
 public string Status {get{return Finding.Rule.Protected?"仅展示 · 保留":Finding.Running?"使用中 · 保留":Finding.Rule.Privacy?"使用痕迹 · 需确认":Finding.Rule.Quick?"低风险 · 推荐":"有风险 · 需确认";}}
 public Brush StatusBrush {get{return App.Brush(Finding.Rule.Protected||Finding.Running?"#46729C":Finding.Rule.Privacy?"#B64E69":Finding.Rule.Quick?"#117E77":"#A46A13");}}
 public Brush StatusBackground {get{return App.Brush(Finding.Rule.Protected||Finding.Running?"#EAF1F9":Finding.Rule.Privacy?"#FCECF1":Finding.Rule.Quick?"#E3F8F5":"#FFF3DD");}}
 public Brush ActionBrush {get{return Selectable?StatusBrush:App.Brush("#46729C");}}
 public string Size {get{return Finding.Rule.InfoKind=="registry"?"检查 "+Finding.VirtualItems+" 项安装信息":Finding.Files>0?"占用 "+App.FormatSize(Finding.TotalBytes)+(Finding.Unreadable>0?"（部分）":"") : Finding.Unreadable>0?"部分目录无法读取":"保留个人与系统资料";}}
 public string EligibleLabel {get{return Selectable?"可处理 "+App.FormatSize(Finding.EligibleBytes):"不会删除";}}
 public string ActionReason {get{return Finding.Rule.Protected?Finding.Rule.InfoKind=="storage"?"交由 Windows 处理":"本工具保留":Finding.Running?"请先关闭相关软件":!Selectable?Finding.Unreadable>0?"权限不足或暂无候选文件":"近期文件或不符合条件":"符合保留期的 "+Finding.Candidates.Count+" 个文件";}}
 public bool Selectable {get{return !Finding.Rule.Protected&&!Finding.Running&&Finding.Candidates.Count>0;}}
 public Visibility CheckVisibility {get{return Advanced?Visibility.Visible:Visibility.Collapsed;}}
 public bool Selected {get{return selected;}set{selected=value&&Selectable;Changed("Selected");if(OnChanged!=null)OnChanged();}}
 void Changed(string property){if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs(property));}
 public event PropertyChangedEventHandler PropertyChanged;
}
public sealed class Controller {
 public Window Window; public Task LastOperation; public Func<List<Rule>> RuleFactory=RuleBook.Create;
 Engine engine=new Engine(new List<Rule>()); List<Row> rows=new List<Row>(); List<GroupOption> groups=new List<GroupOption>(); bool advanced,busy,testMode,hasScanned;
 CancellationTokenSource cancellation; CleanResult lastResult;
 ListBox list; Button scan,start,clean,simple,advancedButton,cancel,export,recommend,unselect,visibleSelect,admin,open,scope;
 ComboBox categoryFilter,riskFilter; TextBox search; TextBlock status,selection,found,quick,empty; ProgressBar progress;
 public Controller() {
  using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("ClearSpace.Main.xaml"))Window=(Window)XamlReader.Load(resource);
  var area=SystemParameters.WorkArea;Window.MinWidth=Math.Min(Window.MinWidth,Math.Max(760,area.Width-24));Window.MinHeight=Math.Min(Window.MinHeight,Math.Max(540,area.Height-24));Window.Width=Math.Min(Window.Width,Math.Max(Window.MinWidth,area.Width-24));Window.Height=Math.Min(Window.Height,Math.Max(Window.MinHeight,area.Height-24));
  list=Get<ListBox>("ResultsList");scan=Get<Button>("ScanButton");start=Get<Button>("StartButton");clean=Get<Button>("CleanButton");simple=Get<Button>("SimpleButton");advancedButton=Get<Button>("AdvancedButton");cancel=Get<Button>("CancelButton");export=Get<Button>("ExportButton");recommend=Get<Button>("RecommendButton");unselect=Get<Button>("UnselectButton");visibleSelect=Get<Button>("SelectVisibleButton");admin=Get<Button>("AdminButton");open=Get<Button>("OpenFolderButton");scope=Get<Button>("ScopeButton");
  categoryFilter=Get<ComboBox>("CategoryFilter");riskFilter=Get<ComboBox>("RiskFilter");search=Get<TextBox>("SearchBox");status=Get<TextBlock>("StatusText");selection=Get<TextBlock>("SelectionText");found=Get<TextBlock>("FoundText");quick=Get<TextBlock>("QuickText");empty=Get<TextBlock>("EmptyText");progress=Get<ProgressBar>("Progress");
  string[] descriptions={"临时文件、错误报告、图形缓存","浏览器、聊天、音乐与启动器缓存","VS Code、包下载与构建缓存","软件安装包与 Windows 更新资料","最近访问入口；登录与历史保留","仅识别明确失效的本地链接","安装信息只读检查，不删注册表","系统与恢复资料，仅展示保留"};
  string[] icons={"\uE770","\uE8FC","\uE943","\uE895","\uE81C","\uE71B","\uE713","\uE72E"};
  for(int i=0;i<Groups.Names.Length;i++)groups.Add(new GroupOption{Name=Groups.Names[i],Description=descriptions[i],Icon=icons[i]});Get<ItemsControl>("ScopeList").ItemsSource=groups;
  categoryFilter.ItemsSource=new[]{"全部分类"}.Concat(Groups.Names).ToArray();categoryFilter.SelectedIndex=0;riskFilter.ItemsSource=new[]{"全部风险","低风险 · 推荐","有风险 · 需确认","仅展示 · 不会删除"};riskFilter.SelectedIndex=0;
  Get<TextBlock>("AccountText").Text="当前用户 / "+Environment.UserName;Get<TextBlock>("MainTitleText").Text="给 "+RuleBook.TargetDrive.Substring(0,1)+" 盘留些呼吸空间";
  if(App.IsAdmin()){admin.Content="当前已是管理员";admin.IsEnabled=false;}
  scan.Click+=(s,e)=>{LastOperation=ScanAsync();};start.Click+=(s,e)=>{LastOperation=ScanAsync();};clean.Click+=(s,e)=>{LastOperation=CleanAsync();};simple.Click+=(s,e)=>SetMode(false);advancedButton.Click+=(s,e)=>SetMode(true);cancel.Click+=(s,e)=>{if(cancellation!=null)cancellation.Cancel();};
  recommend.Click+=(s,e)=>{foreach(var r in rows)r.Selected=r.Finding.Rule.Quick;};unselect.Click+=(s,e)=>{foreach(var r in rows)r.Selected=false;};visibleSelect.Click+=(s,e)=>{foreach(var r in VisibleRows())r.Selected=true;};export.Click+=(s,e)=>Export();admin.Click+=(s,e)=>Elevate();open.Click+=(s,e)=>OpenFolder();scope.Click+=(s,e)=>ShowScope();list.SelectionChanged+=(s,e)=>UpdateDetail();
  categoryFilter.SelectionChanged+=(s,e)=>RefreshList();riskFilter.SelectionChanged+=(s,e)=>RefreshList();search.TextChanged+=(s,e)=>RefreshList();
  list.PreviewMouseLeftButtonUp+=(s,e)=>{
   if(!advanced||busy)return;DependencyObject source=e.OriginalSource as DependencyObject;var current=source;
   while(current!=null&&current!=list){if(current is CheckBox||current is ScrollBar)return;var content=current as FrameworkContentElement;current=content!=null?content.Parent:VisualTreeHelper.GetParent(current);}
   var item=ItemsControl.ContainerFromElement(list,source) as ListBoxItem;if(item==null)return;var row=item.DataContext as Row;if(row!=null){list.SelectedItem=row;ToggleRow(row);}
  };
  Window.Closing+=(s,e)=>{if(busy){e.Cancel=true;if(cancellation!=null)cancellation.Cancel();status.Text="正在停止当前任务，完成后可以关闭窗口。";}};
  UpdateDisk();SetMode(false);ShowScope();
 }
 public T Get<T>(string name)where T:FrameworkElement{return (T)Window.FindName(name);}
 void ToggleRow(Row row){if(row.Selectable)row.Selected=!row.Selected;}
 void ShowScope(){if(busy)return;Get<FrameworkElement>("WelcomePanel").Visibility=Visibility.Visible;Get<FrameworkElement>("ResultsPanel").Visibility=Visibility.Collapsed;Get<FrameworkElement>("ScopeList").IsEnabled=true;UpdateDetail();UpdateSelection();}
 void SetBusy(bool value,string text){busy=value;scan.IsEnabled=start.IsEnabled=scope.IsEnabled=!value;simple.IsEnabled=advancedButton.IsEnabled=recommend.IsEnabled=unselect.IsEnabled=visibleSelect.IsEnabled=list.IsEnabled=!value;categoryFilter.IsEnabled=riskFilter.IsEnabled=search.IsEnabled=!value;Get<FrameworkElement>("ScopeList").IsEnabled=!value;admin.IsEnabled=!value&&!App.IsAdmin();cancel.Visibility=progress.Visibility=value?Visibility.Visible:Visibility.Collapsed;progress.IsIndeterminate=value;status.Text=text;UpdateSelection();}
 void UpdateDisk(){try{var d=new DriveInfo(RuleBook.TargetDrive);Get<TextBlock>("DiskFreeText").Text=App.FormatSize(d.AvailableFreeSpace);Get<TextBlock>("DiskTotalText").Text="共 "+App.FormatSize(d.TotalSize)+" · 已用 "+(100*(d.TotalSize-d.TotalFreeSpace)/(double)d.TotalSize).ToString("0")+"%";double ratio=Math.Max(.01,Math.Min(.999,(d.TotalSize-d.TotalFreeSpace)/(double)d.TotalSize)),angle=ratio*2*Math.PI;double x=35+31.5*Math.Sin(angle),y=35-31.5*Math.Cos(angle);Get<System.Windows.Shapes.Path>("DiskArc").Data=Geometry.Parse(String.Format(CultureInfo.InvariantCulture,"M 35,3.5 A 31.5,31.5 0 {0} 1 {1},{2}",ratio>.5?1:0,x,y));}catch{Get<TextBlock>("DiskFreeText").Text="暂不可读取";}}
 void Apply(List<Finding> findings){hasScanned=true;rows=findings.Select(f=>new Row{Finding=f,Advanced=advanced,OnChanged=UpdateSelection}).ToList();foreach(var r in rows)r.Selected=r.Finding.Rule.Quick;found.Text=App.FormatSize(findings.Where(f=>!f.Rule.Protected&&!f.Running).Sum(f=>f.EligibleBytes));quick.Text=App.FormatSize(findings.Where(f=>f.Rule.Quick&&!f.Running).Sum(f=>f.EligibleBytes));Get<FrameworkElement>("WelcomePanel").Visibility=Visibility.Collapsed;Get<FrameworkElement>("ResultsPanel").Visibility=Visibility.Visible;scan.Content="重新扫描";RefreshList();UpdateDisk();}
 List<Row> VisibleRows(){
  IEnumerable<Row> visible=advanced?rows:rows.Where(r=>r.Finding.Rule.Quick);
  string category=categoryFilter.SelectedItem as string,query=(search.Text??"").Trim();int risk=riskFilter.SelectedIndex;
  if(!String.IsNullOrEmpty(category)&&category!="全部分类")visible=visible.Where(r=>r.Category==category);
  if(advanced&&risk>0)visible=visible.Where(r=>risk==1?r.Risk=="quick":risk==2?r.Risk=="manual"||r.Risk=="privacy":r.Risk=="protected");
  if(query.Length>0)visible=visible.Where(r=>(r.Title+" "+r.Category+" "+r.Finding.Rule.Description).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);
  return visible.ToList();
 }
 void RefreshList(){if(list==null)return;foreach(var r in rows)r.Advanced=advanced;var prior=list.SelectedItem as Row;var visible=VisibleRows();list.ItemsSource=visible;Get<TextBlock>("ResultTitle").Text=advanced?"识别结果 · 点击整行勾选":"可重新生成的缓存";Get<TextBlock>("CountText").Text=visible.Count+" 项 / 共 "+rows.Count+" 项";empty.Visibility=visible.Count==0?Visibility.Visible:Visibility.Collapsed;empty.Text=hasScanned?"当前筛选没有可展示的项目。\n可切换高级模式或更改筛选条件。":"点击开始扫描后查看结果";if(visible.Count>0)list.SelectedItem=visible.Contains(prior)?prior:visible[0];else UpdateDetail();UpdateSelection();}
 void SetMode(bool value){if(busy)return;advanced=value;simple.Background=App.Brush(value?"Transparent":"#39C5BB");advancedButton.Background=App.Brush(value?"#39C5BB":"Transparent");simple.Foreground=App.Brush(value?"#5A7B7B":"#073F3D");advancedButton.Foreground=App.Brush(value?"#073F3D":"#5A7B7B");recommend.Visibility=unselect.Visibility=visibleSelect.Visibility=value?Visibility.Visible:Visibility.Collapsed;riskFilter.Visibility=value?Visibility.Visible:Visibility.Collapsed;Get<TextBlock>("SimpleHint").Visibility=value?Visibility.Collapsed:Visibility.Visible;RefreshList();}
 List<Finding> Chosen(){return rows.Where(r=>r.Selectable&&(advanced?r.Selected:r.Finding.Rule.Quick)).Select(r=>r.Finding).ToList();}
 void UpdateSelection(){if(selection==null)return;bool resultsVisible=Get<FrameworkElement>("ResultsPanel").Visibility==Visibility.Visible;var chosen=resultsVisible?Chosen():new List<Finding>();long bytes=chosen.Sum(f=>f.EligibleBytes);selection.Text=chosen.Count==0?hasScanned&&resultsVisible?"暂未选择可处理文件；使用中、近期及受保护资料会保留。":"扫描是只读操作；点击清理按钮后才会删除符合条件的文件。":"已选 "+chosen.Count+" 项 · 隐藏的已选项目也计入清理 · 最近修改文件已排除";Get<TextBlock>("PlannedText").Text="可处理 "+App.FormatSize(bytes);clean.Content=advanced?"清理已选":"一键清理低风险缓存";clean.IsEnabled=!busy&&hasScanned&&resultsVisible&&chosen.Any(f=>f.Candidates.Count>0);}
 void UpdateDetail(){
  var row=Get<FrameworkElement>("ResultsPanel").Visibility==Visibility.Visible?list.SelectedItem as Row:null;open.IsEnabled=row!=null&&(row.Finding.Rule.InfoKind=="storage"||row.Finding.Rule.Roots.Length>0);
  if(row==null){Get<TextBlock>("DetailTag").Text="操作由你决定";Get<Border>("DetailBadge").Background=App.Brush("#E3F8F5");Get<TextBlock>("DetailTag").Foreground=App.Brush("#117E77");Get<TextBlock>("DetailTitle").Text="先扫描，再选择";Get<TextBlock>("DetailAction").Text="扫描不会删除文件";Get<TextBlock>("DetailAction").Foreground=App.Brush("#117E77");Get<TextBlock>("DetailDescription").Text="点击开始扫描，识别当前用户和 C 盘内的已知缓存目录。可先勾选左侧要检查的分类。";Get<TextBlock>("DetailImpact").Text="简洁模式只处理可重建缓存。高级模式可自选有影响的项目，删除前需确认。";Get<TextBlock>("DetailRetention").Text="文档、聊天、密码、软件设置和未保存文件备份保留。使用中、近期修改或无法确认的文件跳过。";Get<TextBox>("DetailPaths").Text="扫描后选中项目，可复制目录和查看详细说明。";open.Content="打开文件位置 ↗";return;}
  var f=row.Finding;Get<TextBlock>("DetailTag").Text=row.Status;Get<TextBlock>("DetailTag").Foreground=row.StatusBrush;Get<Border>("DetailBadge").Background=row.StatusBackground;Get<TextBlock>("DetailTitle").Text=f.Rule.Title;Get<TextBlock>("DetailAction").Text=row.EligibleLabel;Get<TextBlock>("DetailAction").Foreground=row.ActionBrush;Get<TextBlock>("DetailDescription").Text=f.Rule.Description;Get<TextBlock>("DetailImpact").Text=f.Rule.Impact;
  string retention=f.Rule.Protected?"不会删除：本项不在清理范围。": "已识别 "+f.Files+" 个文件；符合条件 "+f.Candidates.Count+" 个。\n保留最近 "+f.Rule.Days+" 天修改的文件。";
  if(!String.IsNullOrEmpty(f.Extra))retention+="\n"+f.Extra;if(f.Running)retention+="\n相关程序仍在运行，整项保留。";if(f.Recent>0)retention+="\n近期文件保留："+f.Recent+" 个。";if(f.Unreadable>0)retention+="\n"+f.Unreadable+" 处无法读取，占用统计可能不完整。";if(f.Links>0)retention+="\n"+f.Links+" 处链接或受保护文件已跳过。";
  Get<TextBlock>("DetailRetention").Text=retention;Get<TextBox>("DetailPaths").Text=String.Join("\n\n",f.Rule.Roots)+(f.Samples.Count>0?"\n\n示例（最多 3 项）\n"+String.Join("\n",f.Samples.Take(3)):f.Rule.Roots.Length==0?"本项是保留说明或只读信息，不直接处理文件。":"");open.Content=f.Rule.InfoKind=="storage"?"打开 Windows 存储设置 ↗":"打开文件位置 ↗";
 }
 public async Task ScanAsync(){
  if(busy)return;var selectedGroups=groups.Where(g=>g.Selected).Select(g=>g.Name).ToArray();if(selectedGroups.Length==0){status.Text="请至少勾选一个扫描分类。";return;}cancellation=new CancellationTokenSource();SetBusy(true,"正在只读扫描所选分类…");
  try{var findings=await Task.Run(()=>{engine=new Engine(RuleFactory().Where(r=>selectedGroups.Contains(r.Group)).ToList());return engine.Scan(cancellation.Token,(done,total,title)=>Window.Dispatcher.BeginInvoke(new Action(()=>{status.Text="正在扫描："+title;selection.Text=(done+1)+" / "+total+" 项 · 不会删除文件";})));});Apply(findings);status.Text="扫描完成。低风险推荐可一键清理；有影响和保留项目请在高级模式查看。";}
  catch(OperationCanceledException){rows.Clear();hasScanned=false;found.Text=quick.Text="待扫描";ShowScopeAfterStop();status.Text="扫描已停止，文件未被修改。";}
  catch(Exception ex){rows.Clear();hasScanned=false;found.Text=quick.Text="待扫描";ShowScopeAfterStop();status.Text="扫描未完成："+ex.Message;}
  finally{SetBusy(false,status.Text);}
 }
 void ShowScopeAfterStop(){Get<FrameworkElement>("WelcomePanel").Visibility=Visibility.Visible;Get<FrameworkElement>("ResultsPanel").Visibility=Visibility.Collapsed;RefreshList();}
 public void PreviewScan(){engine=new Engine(RuleBook.Create());Apply(engine.Scan(CancellationToken.None,null));status.Text="扫描完成。低风险推荐可一键清理；有影响和保留项目请在高级模式查看。";SetMode(true);}
 async Task CleanAsync(){
  if(busy||Get<FrameworkElement>("ResultsPanel").Visibility!=Visibility.Visible)return;var chosen=Chosen();if(chosen.Count==0)return;
  if(advanced){string risky=String.Join("、",chosen.Where(f=>!f.Rule.Quick).Select(f=>f.Rule.Title));string message="将永久删除所选项目中符合条件的文件，预计 "+App.FormatSize(chosen.Sum(f=>f.EligibleBytes))+"。\n\n";if(risky.Length>0)message+="有影响 / 需确认："+risky+"。\n清理前请查看各项影响，确认工作已保存、更新已完成，使用痕迹和失效入口不再需要。\n\n";message+="正在使用、扫描后变化和受保护文件会跳过。是否开始？";if(MessageBox.Show(Window,message,"确认清理范围",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;}
  bool quickMode=!advanced;cancellation=new CancellationTokenSource();SetBusy(true,"正在清理选定缓存…");
  try{lastResult=await Task.Run(()=>engine.Clean(chosen,quickMode,cancellation.Token,(done,total,title)=>{if(done%10==0||done==total)Window.Dispatcher.BeginInvoke(new Action(()=>{status.Text="正在处理："+title;selection.Text=done+" / "+total+" 个文件";}));}));export.IsEnabled=true;bool logged=SaveAutomaticLog();string message=(lastResult.Cancelled?"已停止。":"清理完成。")+" 已删除 "+lastResult.Deleted+" 个文件（合计 "+App.FormatSize(lastResult.Bytes)+"）；跳过 "+lastResult.Skipped+" 个。";if(lastResult.Failed>0)message+=" 部分文件无法处理，请查看清理记录。";if(!logged)message+=" 记录未自动保存，请点击导出记录。";var refreshed=await Task.Run(()=>engine.Scan(CancellationToken.None,null));Apply(refreshed);status.Text=message;}
  catch(Exception ex){status.Text="清理中止："+ex.Message+"。已完成的项目可通过清理记录查看。";}finally{SetBusy(false,status.Text);}
 }
 bool SaveAutomaticLog(){if(testMode)return true;try{string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClearSpace","Logs");Directory.CreateDirectory(root);App.WriteJson(Path.Combine(root,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json"),lastResult);return true;}catch{return false;}}
 void Export(){if(lastResult==null)return;var dialog=new SaveFileDialog{Title="导出清理记录",Filter="清理记录 (*.json)|*.json",FileName="澄净-清理记录-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json"};if(dialog.ShowDialog(Window)==true){try{App.WriteJson(dialog.FileName,lastResult);status.Text="清理记录已导出。";}catch(Exception ex){MessageBox.Show(Window,"导出失败："+ex.Message);}}}
 void OpenFolder(){var row=list.SelectedItem as Row;if(row==null)return;try{if(row.Finding.Rule.InfoKind=="storage"){Process.Start(new ProcessStartInfo("ms-settings:storagesense"){UseShellExecute=true});return;}string target=row.Finding.Rule.Roots.FirstOrDefault(r=>Directory.Exists(r)||File.Exists(r));if(target!=null)Process.Start(new ProcessStartInfo("explorer.exe",File.Exists(target)?"/select,\""+target+"\"":"\""+target+"\""){UseShellExecute=true});else status.Text="当前目录不存在或无法访问。";}catch(Exception ex){status.Text="无法打开位置："+ex.Message;}}
 void Elevate(){if(busy)return;try{Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location){UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});Window.Close();}catch{status.Text="未获得管理员权限，仍可扫描当前用户缓存。";}}
 public async Task<List<string>> VerifyUI(string outputDirectory){
  testMode=true;var tests=new List<string>();Action<bool,string> verify=(condition,name)=>{if(!condition)throw new Exception("UI FAILED: "+name);tests.Add("PASS: "+name);};
  verify(engine.Rules.Count==0&&rows.Count==0&&!hasScanned&&!busy,"Startup creates no rules and performs no scan");verify(Get<FrameworkElement>("WelcomePanel").Visibility==Visibility.Visible&&start.IsEnabled&&!clean.IsEnabled,"Startup shows prominent Start button and disables cleanup");verify(groups.Count==8&&groups.All(g=>g.Selected),"Eight selectable scan categories shown on welcome screen");
  string fixture=Path.Combine(outputDirectory,"ui-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
  var safe=new Rule("ui-safe","Fixture cache","常用软件","desc","impact",new[]{Path.Combine(fixture,"safe")},7,true);var manual=new Rule("ui-manual","Fixture manual","系统垃圾","desc","impact",new[]{Path.Combine(fixture,"manual")},7,false);var protect=new Rule("ui-protect","Fixture settings","受保护资料","desc","impact",new[]{Path.Combine(fixture,"protected")},0,false){Protected=true};
  foreach(var rule in new[]{safe,manual,protect}){Directory.CreateDirectory(rule.Roots[0]);string file=Path.Combine(rule.Roots[0],"fixture.dat");File.WriteAllText(file,"test data");File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddDays(-30));}
  RuleFactory=()=>new List<Rule>{safe,manual,protect};foreach(var group in groups)group.Selected=group.Name=="常用软件";start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await LastOperation;
  verify(engine.Rules.Count==1&&rows.Count==1,"Start button scans only selected categories");foreach(var group in groups)group.Selected=true;scan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await LastOperation;
  verify(Get<FrameworkElement>("WelcomePanel").Visibility==Visibility.Collapsed&&Get<FrameworkElement>("ResultsPanel").Visibility==Visibility.Visible,"Scan switches welcome screen to results");verify(list.Items.Count==1,"Simple view shows only quick categories");verify(Chosen().Count==1&&clean.IsEnabled,"One-click button enabled for eligible fixture");
  advancedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(advanced&&list.Items.Count==3,"Advanced mode shows all categories");var protectedRow=rows.First(r=>r.Finding.Rule==protect);verify(!protectedRow.Selectable&&protectedRow.EligibleLabel=="不会删除","Protected category visibly retained and cannot be selected");
  var manualRow=rows.First(r=>r.Finding.Rule==manual);ToggleRow(manualRow);verify(Chosen().Count==2,"Whole row toggle changes advanced cleanup selection");ToggleRow(protectedRow);verify(!protectedRow.Selected,"Whole row toggle cannot select protected data");unselect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(Chosen().Count==0&&!clean.IsEnabled,"Clear selection disables cleanup");
  categoryFilter.SelectedItem="系统垃圾";verify(list.Items.Count==1,"Category filter narrows visible results");visibleSelect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(Chosen().Count==1&&Chosen()[0].Rule==manual,"Select visible acts only on filtered selectable rows");categoryFilter.SelectedIndex=0;riskFilter.SelectedIndex=3;verify(list.Items.Count==1&&(list.Items[0]as Row)==protectedRow,"Risk filter isolates protected items");riskFilter.SelectedIndex=0;search.Text="manual";verify(list.Items.Count==1,"Search filters title and description");search.Text="";
  recommend.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(Chosen().Count==1&&Chosen()[0].Rule==safe,"Recommended selection excludes manual data");list.SelectedItem=manualRow;verify(Get<TextBlock>("DetailTitle").Text==manual.Title&&Get<TextBox>("DetailPaths").Text.Contains(manual.Roots[0])&&Get<TextBlock>("DetailAction").Text.StartsWith("可处理"),"Selected row shows risk, capacity, explanation and location");
  scope.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(!clean.IsEnabled&&Get<FrameworkElement>("WelcomePanel").Visibility==Visibility.Visible,"Changing scan scope cannot trigger cleanup");scan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await LastOperation;simple.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(!advanced&&list.Items.Count==1,"Mode returns to one-click view");
  clean.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await LastOperation;verify(!File.Exists(Path.Combine(safe.Roots[0],"fixture.dat")),"Actual one-click button deletes isolated eligible cache");verify(File.Exists(Path.Combine(manual.Roots[0],"fixture.dat"))&&File.Exists(Path.Combine(protect.Roots[0],"fixture.dat")),"UI cleanup preserves manual and protected fixtures");verify(lastResult!=null&&lastResult.Deleted==1&&export.IsEnabled&&!clean.IsEnabled&&!busy,"Cleanup updates results, export and button state");return tests;
 }
}
public static class App {
 public static Brush Brush(string color){return (Brush)new BrushConverter().ConvertFromString(color);}
 public static bool IsAdmin(){try{return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);}catch{return false;}}
 public static string FormatSize(long bytes){if(bytes>=1024L*1024*1024)return (bytes/(1024.0*1024*1024)).ToString("0.00")+" GB";if(bytes>=1024*1024)return (bytes/(1024.0*1024)).ToString("0.0")+" MB";if(bytes>=1024)return (bytes/1024.0).ToString("0.0")+" KB";return bytes+" B";}
 public static void WriteJson(string path,object data){var json=new JavaScriptSerializer{MaxJsonLength=Int32.MaxValue,RecursionLimit=100};File.WriteAllText(path,json.Serialize(data),new UTF8Encoding(false));}
 [STAThread]public static int Main(string[] args){try{
  if(args.Length==2&&args[0]=="--self-test")return SelfTest.Run(args[1]);
  if(args.Length==2&&args[0]=="--scan-report"){var rules=RuleBook.Create();var e=new Engine(rules);var f=e.Scan(CancellationToken.None,null);WriteJson(args[1],new{ScannedAt=DateTime.Now.ToString("o"),User=Environment.UserName,RuleCount=rules.Count,Catalog=rules.Select(r=>new{r.Id,r.Title,Group=r.Group,r.Quick,r.Protected,r.Privacy,r.Description,r.Impact}),Findings=f.Select(x=>new{Title=x.Rule.Title,Group=x.Rule.Group,Category=x.Rule.Category,Quick=x.Rule.Quick,Protected=x.Rule.Protected,Running=x.Running,Total=x.TotalBytes,Eligible=x.EligibleBytes,Files=x.Files,Unreadable=x.Unreadable,Links=x.Links,Recent=x.Recent,Roots=x.Rule.Roots,Description=x.Rule.Description,Impact=x.Rule.Impact,Extra=x.Extra,VirtualItems=x.VirtualItems})});return 0;}
  var application=new Application();application.ShutdownMode=ShutdownMode.OnMainWindowClose;var controller=new Controller();application.MainWindow=controller.Window;
  if(args.Length==2&&args[0]=="--ui-self-test"){
   Directory.CreateDirectory(args[1]);HidePreview(controller.Window);controller.Window.Loaded+=async(s,e)=>{try{var results=await controller.VerifyUI(args[1]);File.WriteAllLines(Path.Combine(args[1],"ui-test-results.txt"),results.Concat(new[]{"ALL "+results.Count+" UI CHECKS PASSED"}));controller.Window.Close();}catch(Exception ex){File.WriteAllText(Path.Combine(args[1],"ui-test-results.txt"),ex.ToString());application.Shutdown(1);}};
  }else if(args.Length==2&&(args[0]=="--preview"||args[0]=="--preview-simple"||args[0]=="--preview-small"||args[0]=="--preview-start"||args[0]=="--preview-protected")){
   if(args[0]!="--preview-start")controller.PreviewScan();if(args[0]=="--preview-simple")controller.Get<Button>("SimpleButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(args[0]=="--preview-small"){controller.Window.Width=1120;controller.Window.Height=760;}if(args[0]=="--preview-protected"){controller.Get<ComboBox>("CategoryFilter").SelectedItem="注册表检查";controller.Get<ComboBox>("RiskFilter").SelectedIndex=3;}HidePreview(controller.Window);
   controller.Window.Loaded+=(s,e)=>controller.Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{controller.Window.UpdateLayout();var surface=(FrameworkElement)controller.Window.Content;var bitmap=new RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(surface);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(args[1]))encoder.Save(stream);controller.Window.Close();}));
  }
  application.Run(controller.Window);return 0;
 }catch(Exception ex){if(args.Length>=2){try{File.WriteAllText(args[1]+".error.txt",ex.ToString());}catch{}}else MessageBox.Show("程序无法启动："+ex.Message,"澄净");return 1;}}
 static void HidePreview(Window window){window.ShowInTaskbar=false;window.ShowActivated=false;window.Left=-10000;window.Top=-10000;window.WindowStartupLocation=WindowStartupLocation.Manual;}
}
}
