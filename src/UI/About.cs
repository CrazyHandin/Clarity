using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ClearSpace {
public static class About {
 public static void Show(Window owner,string preview=null){
  var window=new Window{Owner=owner,Title=App.DisplayName,Width=540,Height=460,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=App.Brush("#161D25"),Foreground=App.Brush("#DFEAF2"),Icon=owner.Icon,Resources=owner.Resources};
  var root=new Grid{Background=App.Brush("#161D25")};window.Content=root;var stack=new StackPanel{Margin=new Thickness(32)};root.Children.Add(stack);
  stack.Children.Add(new Image{Source=owner.Icon,Width=64,Height=64,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,18)});
  stack.Children.Add(new TextBlock{Text=App.DisplayName,FontSize=26,FontWeight=FontWeights.SemiBold});
  stack.Children.Add(new TextBlock{Text="关于 Clarity",FontSize=15,Foreground=App.Brush("#39C5BB"),Margin=new Thickness(0,8,0,18)});
  stack.Children.Add(new TextBlock{Text="Windows 磁盘清理工具，提供快速与常规扫描、清理风险说明、文件备份和恢复。",TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,0,0,14)});
  stack.Children.Add(new TextBlock{Text="by CraZyX",FontSize=14,Foreground=App.Brush("#9DB5C3"),Margin=new Thickness(0,0,0,20)});
  var actions=new WrapPanel();var project=new Button{Content="GitHub 项目",Margin=new Thickness(0,0,12,0)};var close=new Button{Content="关闭",IsCancel=true};project.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo("https://github.com/CrazyHandin/Clarity"){UseShellExecute=true});}catch(Exception ex){UiText.Show(window,ex.Message);}};close.Click+=(s,e)=>window.Close();actions.Children.Add(project);actions.Children.Add(close);stack.Children.Add(actions);UiText.Attach(window);
  if(preview!=null){window.ShowActivated=false;window.ShowInTaskbar=false;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=window.Top=-10000;window.Loaded+=(s,e)=>window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle,new Action(()=>{App.Render(window,preview);window.Close();}));}window.ShowDialog();
 }
}
}
