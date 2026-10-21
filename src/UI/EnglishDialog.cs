using System;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public static class EnglishDialog {
 public static MessageBoxResult Show(Window owner,string message,MessageBoxButton kind){
  var window=new Window{Owner=owner,Title=App.DisplayName,Width=Math.Min(620,SystemParameters.WorkArea.Width-24),SizeToContent=SizeToContent.Height,MaxHeight=Math.Max(320,SystemParameters.WorkArea.Height-24),WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false,Background=App.Brush("#161D25"),Foreground=App.Brush("#DFEAF2")};
  if(owner!=null){window.Resources=owner.Resources;window.Icon=owner.Icon;}var surface=new GlassBackdrop{Background=App.Brush("#161D25")};window.Content=surface;if(owner!=null)UiGlass.SetEnabled(window,UiGlass.GetEnabled(owner));var grid=new Grid{Margin=new Thickness(24)};surface.Children.Add(grid);grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  var body=new TextBlock{Text=UiText.T(message),TextWrapping=TextWrapping.Wrap,FontSize=14};var scroll=new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=Math.Max(200,SystemParameters.WorkArea.Height-180)};grid.Children.Add(scroll);
  var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,20,0,0)};Grid.SetRow(actions,1);grid.Children.Add(actions);MessageBoxResult result=MessageBoxResult.Cancel;
  Action<string,MessageBoxResult,bool> add=(caption,value,preferred)=>{var button=new Button{Content=caption,Padding=new Thickness(22,10,22,10),Margin=new Thickness(10,0,0,0),IsDefault=preferred};button.Click+=(s,e)=>{result=value;window.Close();};actions.Children.Add(button);};
  if(kind==MessageBoxButton.YesNo||kind==MessageBoxButton.YesNoCancel){add("Yes",MessageBoxResult.Yes,false);add("No",MessageBoxResult.No,true);if(kind==MessageBoxButton.YesNoCancel)add("Cancel",MessageBoxResult.Cancel,false);}
  else{add("OK",MessageBoxResult.OK,kind==MessageBoxButton.OK);if(kind==MessageBoxButton.OKCancel)add("Cancel",MessageBoxResult.Cancel,true);}
  UiText.Attach(window);window.ShowDialog();return result;
 }
}
}
