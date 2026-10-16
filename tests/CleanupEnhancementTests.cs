using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public static class CleanupEnhancementTests {
 public static void Verify(Action<bool,string> check){
  string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
  check(ScanPolicy.Excluded(windows)&&ScanPolicy.Excluded(Path.Combine(windows,"Temp","sample.tmp"))&&ScanPolicy.Excluded(Path.Combine(windows,"System32","..","Temp")),"Windows directory and normalized descendants are excluded before filesystem access");
  check(!ScanPolicy.Excluded(windows+".old")&&!ScanPolicy.Excluded(windows+"Cache"),"Windows exclusion respects directory boundaries");
  var rule=new Rule("windows-fixture","Windows fixture","系统垃圾","","",new[]{windows},0,true);int owners=0;var engine=new Engine(new List<Rule>{rule});engine.IsRunning=p=>{owners++;return false;};var finding=engine.ScanRule(rule,CancellationToken.None);
  check(owners==0&&finding.Files==0&&finding.Unreadable==0&&finding.Blocked==1&&finding.Candidates.Count==0,"A Windows scope is rejected before process or file enumeration");
  finding.Candidates.Add(new Snapshot{Path=Path.Combine(windows,"clarity-never-created.tmp")});var result=engine.Clean(new[]{finding},true,CancellationToken.None,null);
  check(result.Deleted==0&&result.Items.Single().Reason=="Windows 系统目录不参与清理","Forged Windows cleanup snapshots are refused before opening a file");
  check(RuleBook.Create().All(r=>r.Roots.All(p=>!ScanPolicy.Excluded(p))),"All built-in rule roots omit the Windows directory");
  check(!ApplicationDiscovery.FilesBounded(windows,".exe",2,10,CancellationToken.None).Any(),"Portable discovery cannot enumerate Windows executables");
  string template="Your %s volume now has %d MB free.";
  check(CleanupCompletionDialog.Matches(10,10,"Done","Your C: volume now has 1,234 MB free.","Done",template,true,false),"Only a localized completion message with its expected owner and OK button is accepted");
  check(!CleanupCompletionDialog.Matches(10,11,"Done","Your C: volume now has 1 MB free.","Done",template,true,false)&&!CleanupCompletionDialog.Matches(10,10,"Done","Permanently delete these files?","Done",template,true,false),"Other processes and destructive confirmations cannot be auto-accepted");
  check(!CleanupCompletionDialog.Matches(10,10,"Done","Your C: volume now has 1 MB free.","Done",template,true,true)&&!CleanupCompletionDialog.Matches(10,10,"Done","Your C: volume now has 1 MB free.","Done",template,false,false),"Cancelable and unrecognized dialogs stay open");
  string prefix=Path.Combine(Path.GetTempPath(),"clarity-issues-fixture");var a=new Finding{Rule=new Rule("a","Selected","软件缓存","","",new[]{prefix},0,true)};a.Candidates.AddRange(new[]{"ok","locked","cancelled"}.Select(n=>new Snapshot{Path=Path.Combine(prefix,n)}));result=new CleanResult{Cancelled=true};result.Items.Add(new AuditItem{Path=a.Candidates[0].Path,Status="已删除"});result.Items.Add(new AuditItem{Path=a.Candidates[1].Path,Status="已跳过",Reason="in use",ErrorCode=32});result.Items.Add(new AuditItem{Path="unselected",Status="已跳过"});var issues=CleanupIssues.Build(new[]{a,a},result);
  check(issues.Count==2&&issues.All(i=>i.Path!="unselected")&&issues.Any(i=>i.Reason=="in use"&&i.Status=="清理失败")&&issues.Any(i=>i.Reason=="任务已取消"),"Unfinished results contain only selected, deduplicated failures and unprocessed cancellation items");
  var hibernation=Hibernation.CreateRule();var h=new Finding{Rule=hibernation,VirtualItems=1,EligibleBytes=1024};int runs=0;engine=new Engine(new List<Rule>{hibernation}){UseFileBackup=true};engine.DisableHibernation=(token,status)=>{runs++;return new SystemCleanupResult{Completed=true,Bytes=1024,Message="fixture only"};};
  check(hibernation.Group=="系统垃圾"&&!ScanModes.IsQuickRule(hibernation)&&!hibernation.AllowManual&&hibernation.Roots.Length==0&&RiskConfirmation.Required(new[]{h})&&RiskConfirmation.ItemText(h).Contains("关闭 Windows 休眠"),"Hibernation is a warned, advanced-only system action with no raw deletion scope");
  engine.Clean(new[]{h},true,CancellationToken.None,null);engine.Clean(new Finding[0],false,CancellationToken.None,null);check(runs==0,"Quick cleanup and an unselected hibernation item never run powercfg");
  result=engine.Clean(new[]{h},false,CancellationToken.None,null);check(runs==1&&result.HibernationDisabled&&result.EstimatedFreedBytes==1024&&result.BackupFiles==0&&CleanupIssues.Build(new[]{h},result).Count==0,"Selected hibernation uses an injected system action and records its reclaimed space without a fictitious backup");
  engine.DisableHibernation=(token,status)=>{throw new UnauthorizedAccessException("fixture administrator required");};result=engine.Clean(new[]{h},false,CancellationToken.None,null);check(!result.HibernationDisabled&&result.PermissionDenied==1&&result.EstimatedFreedBytes==0&&CleanupIssues.Build(new[]{h},result).Single().Reason.Contains("administrator"),"Hibernation privilege failure appears in unfinished results without claiming space");
  check(!RuleBook.Create().Any(r=>r.Id=="support"||r.Roots.Any(p=>p.EndsWith("eSupport",StringComparison.OrdinalIgnoreCase))),"The manufacturer driver backup detection item is removed");
 }
}
public sealed partial class Controller {
 async Task VerifyCleanupEnhancements(string output,Action<bool,string> check){
  string directory=Path.Combine(output,"completion-fixture");Directory.CreateDirectory(directory);string ok=Path.Combine(directory,"old.tmp"),changed=Path.Combine(directory,"changed.tmp"),locked=Path.Combine(directory,"locked.tmp");
  foreach(string p in new[]{ok,changed,locked}){File.WriteAllText(p,"fixture");File.SetLastWriteTimeUtc(p,DateTime.UtcNow.AddDays(-30));}
  var savedFactory=RuleFactory;int mode=scanMode.SelectedIndex;var history=SaveHistory;var started=new ManualResetEventSlim();var release=new ManualResetEventSlim();
  try{
   ShowReady();scanMode.SelectedIndex=1;foreach(var group in groups)group.Selected=true;RuleFactory=()=>new List<Rule>{new Rule("completion-fixture","Fixture selected cache","软件缓存","","",new[]{directory},7,true)};await ScanAsync();engine.LiveProgress=p=>{throw new Exception("Cleanup must not rescan");};File.AppendAllText(changed,"modified after scan");Get<ComboBox>("CleanPolicy").SelectedIndex=1;
   SaveHistory=r=>{check(!Window.Dispatcher.CheckAccess(),"Cleanup history is written outside the UI thread");started.Set();if(!release.Wait(5000))throw new Exception("History fixture timeout");};
   using(var held=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.Read)){
    var operation=CleanAsync();try{check(await Task.Run(()=>started.Wait(5000)),"Cleanup reaches its asynchronous history phase");await Task.Delay(120);check(busy&&phase=="clean"&&progress.Value==98&&Get<TextBlock>("PercentText").Text=="98%","The UI remains responsive before 100 percent while history is being saved");}finally{release.Set();}await operation;
   }
   check(lastResult.Deleted==1&&cleanupIssues.Count==2&&phase=="complete"&&Get<Button>("CompleteIssuesButton").IsVisible,"Cleanup finishes without rescanning and exposes selected changed or locked files");App.Render(Window,Path.Combine(output,"completion-partial.png"));
   Get<Button>("CompleteIssuesButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));var page=(FrameworkElement)Get<ContentControl>("PageHost").Content;var list=(ListBox)page.FindName("IssuesList");check(list.Items.Count==2&&((TextBox)page.FindName("IssueDetails")).Text.Contains(directory),"The unfinished list exposes actual selected paths and reasons");App.Render(Window,Path.Combine(output,"cleanup-issues.png"));((Button)page.FindName("IssuesBack")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(phase=="complete"&&Get<FrameworkElement>("CompletePage").IsVisible,"Returning from unfinished items preserves the completion page");ShowReady();
  }finally{release.Set();SaveHistory=history;RuleFactory=savedFactory;scanMode.SelectedIndex=mode;started.Dispose();release.Dispose();}
  UiMotion.SetEnabled(Window,true);int minimized=0;var motion=windowTransitions.Minimize(()=>minimized++);await Task.Delay(50);check(minimized==0&&Get<Grid>("WindowRoot").Opacity<1,"Minimize waits for a visible animation before requesting the native state change");await windowTransitions.Minimize(()=>minimized++);await motion;check(minimized==1&&Get<Grid>("WindowRoot").Opacity==1&&Get<Grid>("WindowRoot").IsHitTestVisible,"Repeated minimize requests execute once and restore an interactive surface");motion=windowTransitions.Reveal();await Task.Delay(60);check(Get<Grid>("WindowRoot").Opacity<1,"Maximize and restore use a window-surface transition");await motion;check(Get<Grid>("WindowRoot").Opacity==1,"Window state transitions settle on an opaque surface");UiMotion.SetEnabled(Window,false);
 }
}
}
