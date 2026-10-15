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
  var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var words=new StackPanel{Margin=new Thickness(0,0,22,0)};words.Children.Add(Text(title,17,"#ECF3F8"));if(!String.IsNullOrEmpty(note))words.Children.Add(Text(note,14,"#A7BFCD"));row.Children.Add(words);
  var open=new Button{Content="打开",MinWidth=106,VerticalAlignment=VerticalAlignment.Center};Name(root,open,name);open.Click+=(s,e)=>{try{action();}catch(Exception ex){UiText.Show(owner,ex.Message);}};Grid.SetColumn(open,1);row.Children.Add(open);return Card(row);
 }
 public static FrameworkElement Create(Window owner,Action back,Action usage,Action thirdParty,Action terms,Action<string> copy,Action<string> project,Action<string> notify=null,Action softwareUpdates=null,Action releaseNotes=null){
  var root=new Grid{Margin=new Thickness(28,20,28,24)};NameScope.SetNameScope(root,new NameScope());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
  var header=new DockPanel{Margin=new Thickness(0,0,0,22)};var previous=new Button{Content="← 返回设置",Margin=new Thickness(0,0,18,0)};Name(root,previous,"AboutBack");previous.Click+=(s,e)=>back();header.Children.Add(previous);header.Children.Add(Text("关于 Clarity",28,"#ECF3F8"));root.Children.Add(header);
  var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,1);root.Children.Add(scroll);var stack=new StackPanel();scroll.Content=stack;
  var identity=new Grid();identity.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(76)});identity.ColumnDefinitions.Add(new ColumnDefinition());identity.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});identity.Children.Add(new Image{Source=owner.Icon,Width=52,Height=52,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center});
  var product=new StackPanel{VerticalAlignment=VerticalAlignment.Center};product.Children.Add(Text("Clarity",24,"#ECF3F8"));var version=Text(ReleaseVersion.Display,15,"#A7BFCD");Name(root,version,"AboutVersion");product.Children.Add(version);var build=Text(UiText.T("内部构建 ")+ReleaseVersion.Build,14,"#A7BFCD");Name(root,build,"AboutBuild");product.Children.Add(build);Grid.SetColumn(product,1);identity.Children.Add(product);
  var identityActions=new StackPanel{VerticalAlignment=VerticalAlignment.Center};var copyButton=new Button{Content="复制版本信息",MinWidth=155};Name(root,copyButton,"CopyVersionButton");copyButton.Click+=(s,e)=>{try{copy("Clarity "+ReleaseVersion.FullVersion+"\nWindows "+Environment.OSVersion.Version+"\n"+(Environment.Is64BitProcess?"64-bit":"32-bit"));if(notify!=null)notify("版本信息已复制。");}catch(Exception ex){UiText.Show(owner,ex.Message);}};identityActions.Children.Add(copyButton);var highlightsButton=new Button{Content="本版更新内容",Padding=new Thickness(12,7,12,7),FontSize=13,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,8,0,0)};Name(root,highlightsButton,"ReleaseNotesButton");highlightsButton.Click+=(s,e)=>{if(releaseNotes!=null)releaseNotes();};identityActions.Children.Add(highlightsButton);Grid.SetColumn(identityActions,2);identity.Children.Add(identityActions);stack.Children.Add(Card(identity));
  var notice=Text("当前为 Beta 测试版，可能出现识别、清理或界面问题。重要资料请先备份，清理前查看风险说明。",15,"#F2CC90");Name(root,notice,"BetaNotice");var warning=Card(notice);warning.Background=App.Brush("#342D24");warning.BorderBrush=App.Brush("#6E5737");stack.Children.Add(warning);
  var updateCard=Link(root,"软件更新","","SoftwareUpdateButton",softwareUpdates??back,owner);var updateButton=(Button)root.FindName("SoftwareUpdateButton");var updateCaption=new StackPanel{Orientation=Orientation.Horizontal};updateCaption.Children.Add(Text("检查软件更新",14));var badge=new System.Windows.Shapes.Ellipse{Width=8,Height=8,Fill=App.Brush("#F27777"),Visibility=Visibility.Collapsed,Margin=new Thickness(8,0,0,0)};Name(root,badge,"AboutUpdateBadge");updateCaption.Children.Add(badge);updateButton.Content=updateCaption;var updateGroup=new StackPanel();updateGroup.Children.Add(updateCard);var loading=new ProgressBar{Height=5,IsIndeterminate=true,Foreground=App.Brush("#39C5BB"),Background=App.Brush("#344651"),Margin=new Thickness(4,-4,4,12),Visibility=Visibility.Collapsed};Name(root,loading,"AboutUpdateProgress");updateGroup.Children.Add(loading);stack.Children.Add(updateGroup);
  var intro=new StackPanel{Margin=new Thickness(0,14,0,20)};intro.Children.Add(Text("版权",19,"#ECF3F8"));var copyright=Text("Clarity © 2026 CraZyX",16);Name(root,copyright,"CopyrightNotice");intro.Children.Add(copyright);
  intro.Children.Add(Text("本软件开源且完全免费，作者从未以任何形式出售。著作权由作者保留，本软件可依 MIT 许可自由使用。",15));
  intro.Children.Add(Text("MIT 允许保留版权与许可声明后的修改、再分发及商业使用；官方免费说明不限制第三方依许可收费。",14,"#A7BFCD"));
  intro.Children.Add(new TextBlock{Text="下列行为未获许可",FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,14,0,4)});
  intro.Children.Add(Text("• 分发软件或实质部分时，去除或遗漏版权与 MIT 许可声明。",14));intro.Children.Add(Text("• 对 Winapp2 规则附加与其独立许可冲突的限制。",14));stack.Children.Add(intro);
  stack.Children.Add(new TextBlock{Text="使用说明与项目链接",FontSize=19,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
  stack.Children.Add(Link(root,"软件使用说明","扫描、清理与备份恢复的操作指南","UsageButton",usage,owner));
  stack.Children.Add(Link(root,"使用条款","MIT 授权、清理风险与备份恢复说明","TermsButton",terms,owner));
  stack.Children.Add(Link(root,"第三方声明","Winapp2 规则来源与许可信息","ThirdPartyButton",thirdParty,owner));
  stack.Children.Add(Link(root,"源代码与反馈","github.com/CrazyHandin/Clarity","SourceButton",()=>project("https://github.com/CrazyHandin/Clarity"),owner));return root;
 }
}
}
