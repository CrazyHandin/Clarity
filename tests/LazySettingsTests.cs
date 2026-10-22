#if CLARITY_TESTS
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifyLazySettings(string output,Action<bool,string> check){
  int reads=0;var entered=new TaskCompletionSource<bool>();var finished=new TaskCompletionSource<bool>();using(var gate=new ManualResetEventSlim(false)){
   RuleSourcesPanel.SummaryFixture=()=>{Interlocked.Increment(ref reads);entered.TrySetResult(true);gate.Wait();finished.TrySetResult(true);return new RuleSourcesSummary{Version="fixture",Definitions=123,Packs=2,EnabledPacks=1};};
   try{
    OpenSettings(new CleanerSettings(),null,s=>{});await Window.Dispatcher.InvokeAsync(new Action(()=>Window.UpdateLayout()),System.Windows.Threading.DispatcherPriority.ContextIdle);
    var root=settingsView.Root;var tabs=(TabControl)root.FindName("RulesTabs");var backup=(TabItem)root.FindName("BackupTab");var apps=(TabItem)root.FindName("ApplicationsTab");var rules=(TabItem)root.FindName("RulesTab");
    check(backup.Content==null&&apps.Content==null&&rules.Content==null&&root.FindName("RecoveryPath")==null&&reads==0,"Opening General settings constructs no inactive pages and reads no rule libraries");
    tabs.SelectedItem=backup;var page=backup.Content;var path=(TextBox)root.FindName("RecoveryPath");path.Text=Path.Combine(output,"lazy-backup-draft");tabs.SelectedItem=root.FindName("GeneralTab");tabs.SelectedItem=backup;
    check(ReferenceEquals(page,backup.Content)&&path.Text.EndsWith("lazy-backup-draft")&&apps.Content==null&&rules.Content==null,"A lazy settings tab is constructed once and retains its in-page draft");
    tabs.SelectedItem=apps;check(root.FindName("Apps")is ListBox&&root.FindName("PortableExpander")is Expander&&rules.Content==null,"Application controls and their names are available on first selection without constructing rules");
    tabs.SelectedItem=rules;settingsView.Refresh();settingsView.Refresh();check(await Task.WhenAny(entered.Task,Task.Delay(2000))==entered.Task,"Rule summary loading starts only when its page is selected");
    check(reads==1&&((TextBlock)root.FindName("LibraryState")).Text=="正在读取规则…","Repeated rule-summary refreshes share one background read");
    tabs.SelectedItem=root.FindName("GeneralTab");BackPage();check(pages.Count==0&&Get<Grid>("MainLayout").Visibility==Visibility.Visible,"A delayed rule summary cannot block tab navigation or returning to cleanup");
    gate.Set();await finished.Task;await Window.Dispatcher.InvokeAsync(new Action(()=>{}),System.Windows.Threading.DispatcherPriority.ContextIdle);check(pages.Count==0&&reads==1,"Completion of an unloaded settings read does not reopen or mutate the active page");
   }finally{gate.Set();while(pages.Count>0)BackPage();RuleSourcesPanel.SummaryFixture=null;}
  }
 }
}
}
#endif
