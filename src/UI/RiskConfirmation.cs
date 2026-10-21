using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClearSpace {
public static class RiskConfirmation {
 public static bool Risky(Finding f){return f.ManualOverride||f.Rule.Heuristic||f.Rule.ForceBackup||f.Rule.Privacy||f.Rule.Protected||!f.Rule.Quick||f.RegistryCandidates.Count>0||f.Rule.InfoKind=="system-disk-cleanup"||!String.IsNullOrWhiteSpace(f.Rule.Warning);}
 public static bool Required(IList<Finding> chosen){return chosen.Any(Risky);}
 public static string ItemText(Finding f){
  string policy=f.Rule.InfoKind=="disable-hibernation"?"关闭 Windows 休眠 · 不经过备份":f.Rule.InfoKind=="system-disk-cleanup"?"系统自动清理 · 永久删除":f.ManualOverride?"风险删除 · 强制先备份":f.Rule.Heuristic?"未知缓存 · 强制先备份":f.Rule.ForceBackup?"社区规则 · 强制先备份":f.Rule.Quick?"低风险缓存":"高级自选";
  string text=(f.Rule.Software??f.Rule.Title)+" · "+(f.Rule.Part??f.Rule.Title)+"\n"+policy+" · "+(f.Rule.InfoKind=="system-disk-cleanup"?"C 盘，容量由 Windows 计算":App.FormatSize(f.EligibleBytes)+(f.RegistryCandidates.Count>0?" / "+f.RegistryCandidates.Count+" 项注册表":""))+"\n"+f.Rule.Impact;
  if(f.ManualOverride)text+="\n已选择处理原本保留的内容，将绕过文件年龄和个人资料保留策略；可能丢失设置、账号登录、历史、离线文件或恢复资料。只对扫描所列文件操作，先完成备份。";
  if(f.Rule.Heuristic)text+="\n未知软件未验证可重建，需自行核对用途。";
  if(!String.IsNullOrWhiteSpace(f.Rule.Warning))text+="\n原规则警告："+f.Rule.Warning;
  if(f.ManualOverride||f.Rule.Heuristic)text+="\n范围："+String.Join("；",f.Rule.Roots);
  return text;
 }
 public static string Summary(IList<Finding> chosen,bool backed){return chosen.Count+" 个所选项目 · "+App.FormatSize(chosen.Sum(f=>f.EligibleBytes))+" 文件\n"+(backed?"普通文件先备份后删除。":"普通文件永久删除，无法通过澄净恢复。")+"社区、未知缓存及风险删除始终备份；注册表候选始终备份。\n占用、链接、无权限及扫描后变化的文件仍会跳过。Windows 系统清理不经过澄净备份。";}
 public static string Describe(IList<Finding> chosen,bool backed){var risks=chosen.Where(Risky).ToList();return Summary(risks,backed)+"\n\n"+String.Join("\n\n",risks.Select(ItemText));}
 public static bool Show(Window owner,IList<Finding> chosen,bool backed,string preview=null,string backupPath=null){
  chosen=chosen.Where(Risky).ToList();
  var window=new Window{Owner=owner,Title=App.DisplayName,Width=760,Height=640,MinWidth=620,MinHeight=460,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=App.Brush("#161D25"),Foreground=App.Brush("#DFEAF2"),FontFamily=new FontFamily("Microsoft YaHei UI"),ShowInTaskbar=false};
  window.Resources=owner.Resources;window.Width=Math.Min(window.Width,Math.Max(620,SystemParameters.WorkArea.Width-24));window.Height=Math.Min(window.Height,Math.Max(460,SystemParameters.WorkArea.Height-24));
  var surface=new GlassBackdrop{Background=App.Brush("#161D25")};window.Content=surface;if(owner!=null)UiGlass.SetEnabled(window,UiGlass.GetEnabled(owner));var grid=new Grid{Margin=new Thickness(24)};surface.Children.Add(grid);grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  var title=new TextBlock{Text="确认本次清理",FontSize=25,FontWeight=FontWeights.SemiBold,Foreground=App.Brush("#39C5BB")};grid.Children.Add(title);
  bool filesBacked=chosen.Any(f=>f.Candidates.Count>0&&(backed||f.Rule.Heuristic||f.Rule.ForceBackup||f.ManualOverride));string backupNote=filesBacked&&!String.IsNullOrEmpty(backupPath)?"\n备份至 "+backupPath+(String.Equals(System.IO.Path.GetPathRoot(backupPath),"C:\\",StringComparison.OrdinalIgnoreCase)?"（同在 C 盘，清空备份后才释放空间）":""):"";
  var summary=new TextBlock{Text=Summary(chosen,backed)+backupNote,FontSize=13,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,16),Foreground=App.Brush("#BCCEDA")};Grid.SetRow(summary,1);grid.Children.Add(summary);
  var stack=new StackPanel();foreach(var f in chosen){var label=new TextBlock{Text=ItemText(f),FontSize=13,TextWrapping=TextWrapping.Wrap,Foreground=App.Brush(f.ManualOverride||f.Rule.InfoKind=="system-disk-cleanup"?"#FFB4A9":f.Rule.Quick?"#C7E6E3":"#E4BE81")};stack.Children.Add(new GlassPanel{Child=label,Background=App.Brush("#202D37"),CornerRadius=new CornerRadius(9),Padding=new Thickness(14),Margin=new Thickness(0,0,8,9)});}
  var scroll=new ScrollViewer{Content=stack,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,2);grid.Children.Add(scroll);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};var cancel=new Button{Content="取消",IsCancel=true,Padding=new Thickness(25,11,25,11),Margin=new Thickness(0,0,12,0)};var confirm=new Button{Content="确认风险并清理",Padding=new Thickness(25,11,25,11),Background=App.Brush("#39C5BB"),Foreground=App.Brush("#083F3A"),FontWeight=FontWeights.SemiBold};cancel.Click+=(s,e)=>window.DialogResult=false;confirm.Click+=(s,e)=>window.DialogResult=true;actions.Children.Add(cancel);actions.Children.Add(confirm);Grid.SetRow(actions,3);grid.Children.Add(actions);
  UiText.Attach(window);
#if CLARITY_TESTS
  if(preview!=null){window.ShowInTaskbar=false;window.ShowActivated=false;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=window.Top=-10000;window.Loaded+=(s,e)=>window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{App.Render(window,preview);window.DialogResult=false;}));}
#endif
  return window.ShowDialog()==true;
 }
}
}
