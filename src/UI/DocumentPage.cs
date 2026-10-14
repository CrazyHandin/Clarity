using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public static class DocumentPage {
 public static FrameworkElement Create(bool thirdParty,Action back){
  string raw;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(thirdParty?"ClearSpace.ThirdParty.txt":"ClearSpace.Usage.txt"))using(var reader=new StreamReader(stream))raw=reader.ReadToEnd();
  if(!thirdParty){int english=raw.IndexOf("English User Guide",StringComparison.Ordinal);raw=UiText.English?raw.Substring(english):raw.Substring(0,raw.LastIndexOf("============================================================",english,StringComparison.Ordinal)).Trim();}
  else{raw=raw.Replace("The complete license is included in [Winapp2-License.md](docs/Winapp2-License.md).","The complete license is shown below.");using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ClearSpace.Winapp2License.txt"))using(var reader=new StreamReader(stream))raw+="\n\n"+reader.ReadToEnd();raw=Regex.Replace(raw,@"\[([^\]]+)\]\(([^)]+)\)","$1 ($2)");raw=Regex.Replace(raw,@"(?m)^#+\s*","").Replace("**","");}
  var root=new Grid{Margin=new Thickness(28,20,28,24)};NameScope.SetNameScope(root,new NameScope());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());
  var header=new DockPanel{Margin=new Thickness(0,0,0,20)};var button=new Button{Content="← 返回",Margin=new Thickness(0,0,18,0),Name="DocumentBack"};root.RegisterName(button.Name,button);button.Click+=(s,e)=>back();header.Children.Add(button);header.Children.Add(new TextBlock{Text=thirdParty?"第三方声明":"使用说明",FontSize=28,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});root.Children.Add(header);
  var text=new TextBox{Text=raw,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,FontSize=15,Padding=new Thickness(24),Background=App.Brush("#1D2933"),Foreground=App.Brush("#CFDFEB"),BorderBrush=App.Brush("#344651"),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Name="DocumentText"};root.RegisterName(text.Name,text);TextBlock.SetLineHeight(text,25);Grid.SetRow(text,1);root.Children.Add(text);return root;
 }
}
}
