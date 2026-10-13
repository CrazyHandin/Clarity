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
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

[assembly: AssemblyTitle("澄净 · C 盘清理")]
[assembly: AssemblyDescription("便携式 Windows 缓存识别与清理工具")]
namespace ClearSpace {
public sealed class Row : INotifyPropertyChanged {
 public Finding Finding; public Action OnChanged; bool selected; public bool Advanced;
 public string Title { get { return Finding.Rule.Title; } }
 public string Category { get { return Finding.Rule.Category; } }
 public string Status { get { return Finding.Status; } }
 public Brush StatusBrush { get { return (Brush)new BrushConverter().ConvertFromString(Finding.Rule.Protected||Finding.Running?"#86978C":Finding.Rule.Quick?"#258B63":"#AF7B32"); } }
 public string Size { get { return App.FormatSize(Finding.TotalBytes); } }
 public string EligibleLabel { get { return Finding.Rule.Protected?"不会删除": "可处理 "+App.FormatSize(Finding.EligibleBytes); } }
 public bool Selectable { get { return !Finding.Rule.Protected && !Finding.Running && Finding.Candidates.Count>0; } }
 public Visibility CheckVisibility { get { return Advanced?Visibility.Visible:Visibility.Collapsed; } }
 public bool Selected { get { return selected; } set { selected=value&&Selectable; if(PropertyChanged!=null) PropertyChanged(this,new PropertyChangedEventArgs("Selected")); if(OnChanged!=null) OnChanged(); } }
 public event PropertyChangedEventHandler PropertyChanged;
}
public sealed class Controller {
 public Window Window; public Task LastOperation; Engine engine; List<Row> rows=new List<Row>(); bool advanced, busy, testMode; CancellationTokenSource cancellation; CleanResult lastResult;
 ListBox list; Button scan,clean,simple,advancedButton,cancel,export,recommend,unselect,admin,open;
 TextBlock status,selection,found,quick,empty; ProgressBar progress;
 public Controller() {
  using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("ClearSpace.Main.xaml")) Window=(Window)XamlReader.Load(resource);
  var workArea=SystemParameters.WorkArea;Window.MinWidth=Math.Min(Window.MinWidth,Math.Max(700,workArea.Width-24));Window.MinHeight=Math.Min(Window.MinHeight,Math.Max(500,workArea.Height-24));Window.Width=Math.Min(Window.Width,Math.Max(Window.MinWidth,workArea.Width-24));Window.Height=Math.Min(Window.Height,Math.Max(Window.MinHeight,workArea.Height-24));
  engine=new Engine(RuleBook.Create()); list=Get<ListBox>("ResultsList"); scan=Get<Button>("ScanButton"); clean=Get<Button>("CleanButton"); simple=Get<Button>("SimpleButton"); advancedButton=Get<Button>("AdvancedButton"); cancel=Get<Button>("CancelButton"); export=Get<Button>("ExportButton"); recommend=Get<Button>("RecommendButton"); unselect=Get<Button>("UnselectButton"); admin=Get<Button>("AdminButton"); open=Get<Button>("OpenFolderButton");
  status=Get<TextBlock>("StatusText"); selection=Get<TextBlock>("SelectionText"); found=Get<TextBlock>("FoundText"); quick=Get<TextBlock>("QuickText"); empty=Get<TextBlock>("EmptyText"); progress=Get<ProgressBar>("Progress");
  Get<TextBlock>("AccountText").Text="当前用户  /  "+Environment.UserName;
  Get<TextBlock>("MainTitleText").Text="给 "+RuleBook.TargetDrive.Substring(0,1)+" 盘留些呼吸空间";
  if(App.IsAdmin()) { admin.Content="当前已是管理员"; admin.IsEnabled=false; }
  scan.Click+=(s,e)=>{LastOperation=ScanAsync();}; clean.Click+=(s,e)=>{LastOperation=CleanAsync();}; simple.Click+=(s,e)=>SetMode(false); advancedButton.Click+=(s,e)=>SetMode(true); cancel.Click+=(s,e)=>{if(cancellation!=null)cancellation.Cancel();};
  recommend.Click+=(s,e)=>{foreach(var r in rows)r.Selected=r.Finding.Rule.Quick;}; unselect.Click+=(s,e)=>{foreach(var r in rows)r.Selected=false;}; export.Click+=(s,e)=>Export(); admin.Click+=(s,e)=>Elevate(); open.Click+=(s,e)=>OpenFolder(); list.SelectionChanged+=(s,e)=>UpdateDetail();
  Window.Closing+=(s,e)=>{if(busy){e.Cancel=true; if(cancellation!=null)cancellation.Cancel(); status.Text="正在停止当前任务，完成后可以关闭窗口。";}};
  UpdateDisk();
 }
 public T Get<T>(string name) where T:FrameworkElement { return (T)Window.FindName(name); }
 void SetBusy(bool value,string text) { busy=value; scan.IsEnabled=!value; clean.IsEnabled=!value; simple.IsEnabled=!value; advancedButton.IsEnabled=!value; recommend.IsEnabled=!value; unselect.IsEnabled=!value; list.IsEnabled=!value; admin.IsEnabled=!value&&!App.IsAdmin(); cancel.Visibility=value?Visibility.Visible:Visibility.Collapsed; progress.Visibility=value?Visibility.Visible:Visibility.Collapsed; progress.IsIndeterminate=value; status.Text=text; UpdateSelection(); }
 void UpdateDisk() {
  try { var d=new DriveInfo(RuleBook.TargetDrive); Get<TextBlock>("DiskFreeText").Text=App.FormatSize(d.AvailableFreeSpace); Get<TextBlock>("DiskTotalText").Text="共 "+App.FormatSize(d.TotalSize)+" · 已用 "+(100*(d.TotalSize-d.TotalFreeSpace)/(double)d.TotalSize).ToString("0")+"%";
   double ratio=Math.Max(.01,Math.Min(.999,(d.TotalSize-d.TotalFreeSpace)/(double)d.TotalSize)), angle=ratio*2*Math.PI; double x=35+31.5*Math.Sin(angle),y=35-31.5*Math.Cos(angle);
   string geom=String.Format(CultureInfo.InvariantCulture,"M 35,3.5 A 31.5,31.5 0 {0} 1 {1},{2}",ratio>.5?1:0,x,y); Get<System.Windows.Shapes.Path>("DiskArc").Data=Geometry.Parse(geom);
  } catch { Get<TextBlock>("DiskFreeText").Text="暂不可读取"; }
 }
 void Apply(List<Finding> findings) {
  rows=findings.Select(f=>new Row {Finding=f,Advanced=advanced,OnChanged=UpdateSelection}).ToList();
  foreach(var r in rows) r.Selected=r.Finding.Rule.Quick;
  found.Text=App.FormatSize(findings.Where(f=>!f.Rule.Protected).Sum(f=>f.TotalBytes)); quick.Text=App.FormatSize(findings.Where(f=>f.Rule.Quick&&!f.Running).Sum(f=>f.EligibleBytes)); RefreshList(); UpdateDisk();
 }
 void RefreshList() {
  foreach(var r in rows) r.Advanced=advanced;
  var visible=advanced?rows:rows.Where(r=>r.Finding.Rule.Quick).ToList(); list.ItemsSource=null; list.ItemsSource=visible;
  Get<TextBlock>("ResultTitle").Text=advanced?"识别结果 · 按需选择":"可重新生成的缓存"; Get<TextBlock>("CountText").Text=visible.Count+" 项";
  empty.Visibility=visible.Count==0?Visibility.Visible:Visibility.Collapsed; empty.Text=advanced?"未发现已知缓存或残留。受权限影响的目录会在结果中单独标记。":"暂未发现符合一键范围的缓存。\n高级模式可查看显卡缓存、临时文件和更新资料。";
  if(visible.Count>0)list.SelectedIndex=0; else UpdateDetail(); UpdateSelection();
 }
 void SetMode(bool value) { if(busy)return; advanced=value; simple.Background=(Brush)new BrushConverter().ConvertFromString(value?"Transparent":"White"); advancedButton.Background=(Brush)new BrushConverter().ConvertFromString(value?"White":"Transparent"); simple.Foreground=(Brush)new BrushConverter().ConvertFromString(value?"#6D8076":"#234D40"); advancedButton.Foreground=(Brush)new BrushConverter().ConvertFromString(value?"#234D40":"#6D8076"); recommend.Visibility=unselect.Visibility=value?Visibility.Visible:Visibility.Collapsed; Get<TextBlock>("SimpleHint").Visibility=value?Visibility.Collapsed:Visibility.Visible; RefreshList(); }
 List<Finding> Chosen() { return rows.Where(r=>r.Selectable&&(advanced?r.Selected:r.Finding.Rule.Quick)).Select(r=>r.Finding).ToList(); }
 void UpdateSelection() { if(selection==null)return; var selected=Chosen(); long bytes=selected.Sum(f=>f.EligibleBytes); selection.Text=selected.Count==0?"符合条件的文件才可清理；使用中和近期文件会保留。":"将处理 "+selected.Count+" 项 · 预计释放 "+App.FormatSize(bytes)+" · 最近修改的文件已排除"; clean.Content=advanced?"清理已选 · "+App.FormatSize(bytes):"一键清理 · "+App.FormatSize(bytes); clean.IsEnabled=!busy&&selected.Any(f=>f.Candidates.Count>0); }
 void UpdateDetail() {
  var row=list.SelectedItem as Row; open.IsEnabled=row!=null;
  if(row==null) { Get<TextBlock>("DetailTag").Text="清理说明"; Get<TextBlock>("DetailTitle").Text="先看清，再清理"; Get<TextBlock>("DetailDescription").Text="一键清理仅处理已知、可重新生成的缓存；高级模式提供更多需确认项目。"; Get<TextBlock>("DetailImpact").Text="不清理设置、账号数据、聊天记录、已安装程序或未保存文件备份。"; Get<TextBlock>("DetailRetention").Text="只读扫描，不会自动删除文件。"; Get<TextBox>("DetailPaths").Text="选择左侧项目查看目录。"; return; }
  var f=row.Finding; Get<TextBlock>("DetailTag").Text=f.Status; Get<TextBlock>("DetailTitle").Text=f.Rule.Title; Get<TextBlock>("DetailDescription").Text=f.Rule.Description; Get<TextBlock>("DetailImpact").Text=f.Rule.Impact;
  string retention=f.Rule.Protected?"本项只展示占用，不提供删除。":"已识别 "+f.Files+" 个文件；符合条件 "+f.Candidates.Count+" 个。\n保留最近 "+f.Rule.Days+" 天修改的文件。";
  if(f.Running)retention+="\n检测到相关程序仍在运行，整项保留。";
  if(f.Recent>0)retention+="\n近期文件保留："+f.Recent+" 个。";
  if(f.Unreadable>0)retention+="\n有 "+f.Unreadable+" 处无法读取；可尝试管理员身份。";
  if(f.Links>0)retention+="\n有 "+f.Links+" 处链接或受保护文件，已跳过。";
  Get<TextBlock>("DetailRetention").Text=retention; Get<TextBox>("DetailPaths").Text=String.Join("\n\n",f.Rule.Roots)+(f.Samples.Count>0?"\n\n示例文件（最多 3 个）\n"+String.Join("\n",f.Samples.Take(3)):"");
 }
 public async Task ScanAsync() {
  if(busy)return; cancellation=new CancellationTokenSource(); SetBusy(true,"正在只读扫描已知缓存目录…");
  try { engine=new Engine(RuleBook.Create()); var findings=await Task.Run(()=>engine.Scan(cancellation.Token,(done,total,title)=>Window.Dispatcher.BeginInvoke(new Action(()=>{status.Text="正在扫描："+title; selection.Text=(done+1)+" / "+total+" 项 · 不会删除文件";})))); Apply(findings); status.Text="扫描完成。点击项目查看说明，再决定清理范围。"; }
  catch(OperationCanceledException) { status.Text="扫描已停止。原有扫描结果保留，请重新扫描后再清理。"; rows.Clear(); RefreshList(); found.Text=quick.Text="—"; }
  catch(Exception ex) { status.Text="扫描未完成："+ex.Message; }
  finally { SetBusy(false,status.Text); }
 }
 public void PreviewScan() { Apply(engine.Scan(CancellationToken.None,null)); status.Text="扫描完成。点击项目查看说明，再决定清理范围。"; SetMode(true); }
 async Task CleanAsync() {
  if(busy)return; var chosen=Chosen(); if(chosen.Count==0)return;
  if(advanced) { string risky=String.Join("、",chosen.Where(f=>!f.Rule.Quick).Select(f=>f.Rule.Title)); string message="将永久删除所选项目中符合条件的文件，预计 "+App.FormatSize(chosen.Sum(f=>f.EligibleBytes))+"。\n\n";
   if(risky.Length>0)message+="需要自行确认："+risky+"。\n请确认已保存工作、关闭相关软件，更新资料对应的更新已完成。\n\n";
   message+="正在使用、扫描后变化和受保护的文件会跳过。是否开始？"; if(MessageBox.Show(Window,message,"确认清理范围",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
  }
  bool quickMode=!advanced; cancellation=new CancellationTokenSource(); SetBusy(true,"正在清理选定缓存…");
  try {
   lastResult=await Task.Run(()=>engine.Clean(chosen,quickMode,cancellation.Token,(done,total,title)=>{if(done%10==0||done==total)Window.Dispatcher.BeginInvoke(new Action(()=>{status.Text="正在处理："+title; selection.Text=done+" / "+total+" 个文件";}));}));
   export.IsEnabled=true; SaveAutomaticLog();
   string message=(lastResult.Cancelled?"已停止。":"清理完成。")+" 已删除 "+lastResult.Deleted+" 个文件（合计 "+App.FormatSize(lastResult.Bytes)+"）；跳过 "+lastResult.Skipped+" 个。";
   if(lastResult.Failed>0)message+=" 部分文件无法处理，可导出记录查看。";
   var refreshed=await Task.Run(()=>engine.Scan(CancellationToken.None,null)); Apply(refreshed); status.Text=message;
  } catch(Exception ex) { status.Text="清理中止："+ex.Message+"。已完成的项目可通过清理记录查看。"; }
  finally { SetBusy(false,status.Text); }
 }
 void SaveAutomaticLog() { if(testMode)return; try { string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClearSpace","Logs");Directory.CreateDirectory(root);App.WriteJson(Path.Combine(root,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json"),lastResult); } catch { status.Text="清理记录无法自动保存，可点击导出记录。"; } }
 void Export() { if(lastResult==null)return; var dialog=new SaveFileDialog {Title="导出清理记录",Filter="清理记录 (*.json)|*.json",FileName="澄净-清理记录-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json"}; if(dialog.ShowDialog(Window)==true) { try {App.WriteJson(dialog.FileName,lastResult);status.Text="清理记录已导出。";} catch(Exception ex){MessageBox.Show(Window,"导出失败："+ex.Message);} } }
 void OpenFolder() { var row=list.SelectedItem as Row;if(row==null)return; string target=row.Finding.Rule.Roots.FirstOrDefault(r=>Directory.Exists(r)||File.Exists(r)); if(target==null)return;
  try { Process.Start(new ProcessStartInfo("explorer.exe",File.Exists(target)?"/select,\""+target+"\"":"\""+target+"\""){UseShellExecute=true}); } catch(Exception ex){status.Text="无法打开目录："+ex.Message;}
 }
 void Elevate() { if(busy)return; try { Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location){UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory}); Window.Close(); } catch { status.Text="未获得管理员权限，仍可清理当前用户的缓存。"; } }
 public async Task<List<string>> VerifyUI(string outputDirectory) {
  testMode=true; var tests=new List<string>(); Action<bool,string> verify=(condition,name)=>{if(!condition)throw new Exception("UI FAILED: "+name);tests.Add("PASS: "+name);};
  string fixture=Path.Combine(outputDirectory,"ui-fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
  var safe=new Rule("ui-safe","Fixture cache","test","desc","impact",new[]{Path.Combine(fixture,"safe")},7,true);
  var manual=new Rule("ui-manual","Fixture manual","test","desc","impact",new[]{Path.Combine(fixture,"manual")},7,false);
  var protect=new Rule("ui-protect","Fixture settings","test","desc","impact",new[]{Path.Combine(fixture,"protected")},0,false){Protected=true};
  foreach(var rule in new[]{safe,manual,protect}) {Directory.CreateDirectory(rule.Roots[0]);string file=Path.Combine(rule.Roots[0],"fixture.dat");File.WriteAllText(file,"test data");File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddDays(-30));}
  engine=new Engine(new List<Rule>{safe,manual,protect});engine.IsRunning=owners=>false;Apply(engine.Scan(CancellationToken.None,null));
  verify(list.Items.Count==1,"Simple view only shows quick categories");verify(Chosen().Count==1&&clean.IsEnabled,"One-click button enabled for eligible fixture");
  advancedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(advanced&&list.Items.Count==3,"Advanced mode shows all categories");verify(!rows.First(r=>r.Finding.Rule==protect).Selectable,"Protected category checkbox disabled");
  rows.First(r=>r.Finding.Rule==manual).Selected=true;verify(Chosen().Count==2,"Advanced checkbox changes selected cleanup scope");unselect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(Chosen().Count==0&&!clean.IsEnabled,"Clear selection disables cleanup");
  recommend.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(Chosen().Count==1&&Chosen()[0].Rule==safe,"Recommended button excludes manual data");
  list.SelectedItem=rows.First(r=>r.Finding.Rule==manual);verify(Get<TextBlock>("DetailTitle").Text==manual.Title&&Get<TextBox>("DetailPaths").Text.Contains(manual.Roots[0]),"Selection displays description and file location");
  simple.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));verify(!advanced&&list.Items.Count==1,"Mode can return to one-click view");
  clean.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await LastOperation;
  verify(!File.Exists(Path.Combine(safe.Roots[0],"fixture.dat")),"Actual one-click button deletes isolated eligible cache");
  verify(File.Exists(Path.Combine(manual.Roots[0],"fixture.dat"))&&File.Exists(Path.Combine(protect.Roots[0],"fixture.dat")),"UI cleanup preserves manual and protected fixtures");
  verify(lastResult!=null&&lastResult.Deleted==1&&export.IsEnabled&&!clean.IsEnabled&&!busy,"Cleanup updates results, export and button state");
  return tests;
 }
}
public static class App {
 public static bool IsAdmin() { try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } catch { return false; } }
 public static string FormatSize(long bytes) { if(bytes>=1024L*1024*1024)return (bytes/(1024.0*1024*1024)).ToString("0.00")+" GB"; if(bytes>=1024*1024)return (bytes/(1024.0*1024)).ToString("0.0")+" MB"; if(bytes>=1024)return (bytes/1024.0).ToString("0.0")+" KB"; return bytes+" B"; }
 public static void WriteJson(string path,object data) { var json=new JavaScriptSerializer {MaxJsonLength=Int32.MaxValue,RecursionLimit=100}; File.WriteAllText(path,json.Serialize(data),new UTF8Encoding(false)); }
 [STAThread] public static int Main(string[] args) {
  try {
   if(args.Length==2&&args[0]=="--self-test")return SelfTest.Run(args[1]);
   if(args.Length==2&&args[0]=="--scan-report") { var e=new Engine(RuleBook.Create());var f=e.Scan(CancellationToken.None,null);WriteJson(args[1],new {ScannedAt=DateTime.Now.ToString("o"),User=Environment.UserName,Findings=f.Select(x=>new {Title=x.Rule.Title,Category=x.Rule.Category,Quick=x.Rule.Quick,Protected=x.Rule.Protected,Running=x.Running,Total=x.TotalBytes,Eligible=x.EligibleBytes,Files=x.Files,Unreadable=x.Unreadable,Links=x.Links,Recent=x.Recent,Roots=x.Rule.Roots,Description=x.Rule.Description,Impact=x.Rule.Impact})});return 0; }
   var application=new Application(); application.ShutdownMode=ShutdownMode.OnMainWindowClose;
   var controller=new Controller(); application.MainWindow=controller.Window;
   if(args.Length==2&&args[0]=="--ui-self-test") {
    Directory.CreateDirectory(args[1]);controller.Window.ShowInTaskbar=false;controller.Window.ShowActivated=false;controller.Window.Left=-10000;controller.Window.Top=-10000;controller.Window.WindowStartupLocation=WindowStartupLocation.Manual;
    controller.Window.Loaded+=async (s,e)=>{try{var results=await controller.VerifyUI(args[1]);File.WriteAllLines(Path.Combine(args[1],"ui-test-results.txt"),results.Concat(new[]{"ALL "+results.Count+" UI CHECKS PASSED"}));controller.Window.Close();}catch(Exception ex){File.WriteAllText(Path.Combine(args[1],"ui-test-results.txt"),ex.ToString());application.Shutdown(1);}};
   } else if(args.Length==2&&(args[0]=="--preview"||args[0]=="--preview-simple"||args[0]=="--preview-small")) {
    controller.PreviewScan(); if(args[0]=="--preview-simple")controller.Get<Button>("SimpleButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(args[0]=="--preview-small"){controller.Window.Width=1120;controller.Window.Height=730;}controller.Window.ShowInTaskbar=false;controller.Window.ShowActivated=false;controller.Window.Left=-10000;controller.Window.Top=-10000;controller.Window.WindowStartupLocation=WindowStartupLocation.Manual;
    controller.Window.Loaded+=(s,e)=>controller.Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{controller.Window.UpdateLayout(); var surface=(FrameworkElement)controller.Window.Content; var bitmap=new RenderTargetBitmap((int)surface.ActualWidth,(int)surface.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(surface);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(args[1]))encoder.Save(stream);controller.Window.Close();}));
   } else controller.Window.Loaded+=async (s,e)=>await controller.ScanAsync();
   return application.Run(controller.Window);
  } catch(Exception ex) { if(args.Length>0) {try {File.WriteAllText(args[args.Length-1]+".error.txt",ex.ToString());}catch{}} else MessageBox.Show("程序未能启动："+ex.Message,"澄净",MessageBoxButton.OK,MessageBoxImage.Error);return 1; }
 }
}
}
