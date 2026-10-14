using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ClearSpace {
public static class About {
 static TextBlock Text(string value,int size=15,string color="#CFDFEB"){return new TextBlock{Text=value,FontSize=size,FontWeight=size>=17?FontWeights.SemiBold:FontWeights.Normal,Foreground=App.Brush(color),TextWrapping=TextWrapping.Wrap,LineHeight=size+9};}
 static Border Card(UIElement content){return new Border{Background=App.Brush("#202D37"),BorderBrush=App.Brush("#344651"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(22,18,22,18),Margin=new Thickness(0,0,0,10),Child=content};}
 static void Name(FrameworkElement root,FrameworkElement element,string name){element.Name=name;root.RegisterName(name,element);}
 static Border Link(FrameworkElement root,string title,string note,string name,Action action,Window owner){
  var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var words=new StackPanel{Margin=new Thickness(0,0,22,0)};words.Children.Add(Text(title,17,"#ECF3F8"));words.Children.Add(Text(note,14,"#A7BFCD"));row.Children.Add(words);
  var open=new Button{Content="打开",MinWidth=106,VerticalAlignment=VerticalAlignment.Center};Name(root,open,name);open.Click+=(s,e)=>{try{action();}catch(Exception ex){UiText.Show(owner,ex.Message);}};Grid.SetColumn(open,1);row.Children.Add(open);return Card(row);
 }
 public static FrameworkElement Create(Window owner,Action back,Action usage,Action thirdParty,Action<string> copy,Action<string> project){
  var root=new Grid{Margin=new Thickness(28,20,28,24)};NameScope.SetNameScope(root,new NameScope());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
  var header=new DockPanel{Margin=new Thickness(0,0,0,22)};var previous=new Button{Content="← 返回设置",Margin=new Thickness(0,0,18,0)};Name(root,previous,"AboutBack");previous.Click+=(s,e)=>back();header.Children.Add(previous);header.Children.Add(Text("关于 Clarity",28,"#ECF3F8"));root.Children.Add(header);
  var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);root.Children.Add(scroll);var stack=new StackPanel();scroll.Content=stack;
  var identity=new Grid();identity.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(76)});identity.ColumnDefinitions.Add(new ColumnDefinition());identity.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});identity.Children.Add(new Image{Source=owner.Icon,Width=52,Height=52,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center});
  var product=new StackPanel{VerticalAlignment=VerticalAlignment.Center};product.Children.Add(Text("Clarity",24,"#ECF3F8"));var version=Text(ReleaseVersion.Display,15,"#A7BFCD");Name(root,version,"AboutVersion");product.Children.Add(version);Grid.SetColumn(product,1);identity.Children.Add(product);
  var copyButton=new Button{Content="复制版本信息",VerticalAlignment=VerticalAlignment.Center,MinWidth=155};Name(root,copyButton,"CopyVersionButton");copyButton.Click+=(s,e)=>{try{copy("Clarity "+ReleaseVersion.Display+"\nWindows "+Environment.OSVersion.Version+"\n"+(Environment.Is64BitProcess?"64-bit":"32-bit"));copyButton.Content="已复制";}catch(Exception ex){UiText.Show(owner,ex.Message);}};Grid.SetColumn(copyButton,2);identity.Children.Add(copyButton);stack.Children.Add(Card(identity));
  var notice=Text("当前为 Beta 测试版，可能出现识别、清理或界面问题。重要资料请先备份，清理前查看风险说明。",15,"#F2CC90");Name(root,notice,"BetaNotice");var warning=Card(notice);warning.Background=App.Brush("#342D24");warning.BorderBrush=App.Brush("#6E5737");stack.Children.Add(warning);
  var intro=new StackPanel{Margin=new Thickness(0,14,0,22)};intro.Children.Add(Text("项目介绍",19,"#ECF3F8"));intro.Children.Add(Text("Windows 磁盘清理工具，提供快速与常规扫描、清理风险说明、文件备份和恢复。"));intro.Children.Add(Text("by CraZyX",14,"#A7BFCD"));stack.Children.Add(intro);
  stack.Children.Add(new TextBlock{Text="使用说明与项目链接",FontSize=19,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
  stack.Children.Add(Link(root,"软件使用说明","扫描、清理与备份恢复的操作指南","UsageButton",usage,owner));
  stack.Children.Add(Link(root,"第三方声明","Winapp2 规则来源与许可信息","ThirdPartyButton",thirdParty,owner));
  stack.Children.Add(Link(root,"源代码与反馈","github.com/CrazyHandin/Clarity","SourceButton",()=>project("https://github.com/CrazyHandin/Clarity"),owner));return root;
 }
}
}
