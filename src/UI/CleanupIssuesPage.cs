using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
namespace ClearSpace {
public static class CleanupIssuesPage {
 public static FrameworkElement Create(IList<CleanupIssue> issues,Action back,Action<string> notify){
  var root=new Grid{Margin=new Thickness(28,20,28,24)};NameScope.SetNameScope(root,new NameScope());
  root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  var header=new DockPanel{Margin=new Thickness(0,0,0,20)};var button=new Button{Content="← 返回",Margin=new Thickness(0,0,18,0),Name="IssuesBack"};root.RegisterName(button.Name,button);button.Click+=(s,e)=>back();header.Children.Add(button);header.Children.Add(new TextBlock{Text="未完成项目",FontSize=28,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});root.Children.Add(header);
  var list=new ListBox{ItemsSource=issues,Background=App.Brush("#1D2933"),Foreground=App.Brush("#DFEAF2"),BorderBrush=App.Brush("#344651"),Padding=new Thickness(12),Name="IssuesList",HorizontalContentAlignment=HorizontalAlignment.Stretch};root.RegisterName(list.Name,list);Grid.SetRow(list,1);root.Children.Add(list);
  list.ItemTemplate=(DataTemplate)XamlReader.Parse(UiTheme.Markup("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><StackPanel Margin='8,10'><TextBlock Text='{Binding Project}' FontSize='16' FontWeight='SemiBold'/><TextBlock Text='{Binding Status}' Foreground='#F2B868' Margin='0,5,0,3'/><TextBlock Text='{Binding Path}' Foreground='#B8CDD9' TextTrimming='CharacterEllipsis'/></StackPanel></DataTemplate>"));
  var panel=new StackPanel{Margin=new Thickness(0,16,0,0)};Grid.SetRow(panel,2);root.Children.Add(panel);
  var detail=new TextBox{IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Height=136,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(14),FontSize=14,Background=App.Brush("#1D2933"),Foreground=App.Brush("#DFEAF2"),BorderBrush=App.Brush("#344651"),Name="IssueDetails"};root.RegisterName(detail.Name,detail);panel.Children.Add(detail);
  var open=new Button{Content="打开位置",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,0)};panel.Children.Add(open);
  Func<string> location=()=>{var item=list.SelectedItem as CleanupIssue;if(item==null||!ApplicationLocations.Local(item.Path))return null;string path=item.Path;while(!String.IsNullOrEmpty(path)&&!Directory.Exists(path)&&!File.Exists(path))path=Path.GetDirectoryName(path);return path;};
  list.SelectionChanged+=(s,e)=>{var item=list.SelectedItem as CleanupIssue;detail.Text=item==null?"":UiText.T(item.Project+"\n"+item.Status+" · "+item.Reason)+"\n"+item.Path;open.IsEnabled=location()!=null;};
  open.Click+=(s,e)=>{try{string path=location();if(path!=null)ApplicationLocations.Open(path);}catch(Exception ex){notify(ex.Message);}};
  if(issues.Count>0)list.SelectedIndex=0;return root;
 }
}
}
