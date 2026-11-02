using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace ClearSpace {
public sealed partial class Controller {
 async Task VerifyQuickSelection(string output,Action<bool,string> check){
  var priorFactory=QuickRuleFactory;var priorRegular=RuleFactory;var priorConfirm=ConfirmCleanup;var priorEngine=engine;bool priorQuick=quickScan;var priorScopes=groups.Select(g=>g.Selected).ToArray();int priorMode=scanMode.SelectedIndex,priorPolicy=Get<ComboBox>("CleanPolicy").SelectedIndex;string language=UiText.Language,theme=UiTheme.Light?"light":"dark";double width=Window.Width,height=Window.Height;
  string directory=Path.Combine(output,"quick-selection-fixture");var rules=new List<Rule>();
  for(int i=0;i<3;i++){string root=Path.Combine(directory,i.ToString());Directory.CreateDirectory(root);string file=Path.Combine(root,"old.cache");File.WriteAllText(file,new string('x',1024<<i));File.SetLastWriteTimeUtc(file,DateTime.UtcNow.AddDays(-30));rules.Add(new Rule("quick-selection-"+i,"Cache","软件缓存","Generated cleanup fixture","Resources are rebuilt when needed",new[]{root},7,true){Software=i<2?"Fixture Alpha":"Fixture Beta",Part=i==1?"代码缓存":"资源缓存"});}
  string recent=Path.Combine(rules[0].Roots[0],"recent.cache");File.WriteAllText(recent,"keep recent content");string excludedRoot=Path.Combine(directory,"review-required");Directory.CreateDirectory(excludedRoot);string excludedFile=Path.Combine(excludedRoot,"keep.cache");File.WriteAllText(excludedFile,"manual-only fixture");File.SetLastWriteTimeUtc(excludedFile,DateTime.UtcNow.AddDays(-30));var manual=new Rule("quick-selection-manual","Manual cache","软件缓存","","",new[]{excludedRoot},7,false);
  try{
   ShowReady();scanMode.SelectedIndex=0;foreach(var group in groups)group.Selected=group.Name=="软件缓存";QuickRuleFactory=()=>rules.Concat(new[]{manual}).ToList();RuleFactory=()=>{throw new Exception("Quick selection must not invoke application discovery");};await ScanAsync();foreach(var node in treeNodes)ExpandAll(node,true);Window.UpdateLayout();
   check(!advanced&&advancedButton.Visibility==Visibility.Collapsed&&riskFilter.Visibility==Visibility.Collapsed&&leaves.Count==3&&Chosen().Count==3,"Quick selection starts with all eligible embedded items selected without advanced or manual-only results");
   check(leaves.All(n=>n.CheckVisibility==Visibility.Visible)&&treeNodes.All(n=>n.CheckVisibility==Visibility.Visible)&&treeNodes.SelectMany(n=>n.Children).All(n=>n.CheckVisibility==Visibility.Visible),"Quick scan exposes checkboxes for categories, applications and individual cleanup items");
   var first=leaves.Single(n=>n.Finding.Rule==rules[0]);var second=leaves.Single(n=>n.Finding.Rule==rules[1]);var third=leaves.Single(n=>n.Finding.Rule==rules[2]);var box=VisualChildren<CheckBox>(tree).Single(c=>c.DataContext==first);
   typeof(ToggleButton).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(box,null);
   check(first.Selected==false&&Chosen().Count==2&&Chosen().All(f=>f.Rule!=rules[0])&&Get<TextBlock>("PlannedText").Text==App.FormatSize(6144),"A native quick-result checkbox removes its files from the cleanup payload and selected capacity");
   var category=treeNodes.Single();var app=category.Children.Single(n=>n.Title=="Fixture Alpha");app.Selected=false;check(first.Selected==false&&second.Selected==false&&third.Selected==true&&Chosen().Single().Rule==rules[2]&&category.Selected==null,"Application selection cascades to its quick items and updates the category's partial state");
   category.Selected=false;await CleanAsync();check(Chosen().Count==0&&!clean.IsEnabled&&phase=="results"&&rules.All(r=>File.Exists(Path.Combine(r.Roots[0],"old.cache"))),"Clearing every quick result disables cleanup and cannot delete files");
   search.Text="Alpha";visibleSelect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));search.Text="";check(first.Selected==true&&second.Selected==true&&third.Selected==false&&Chosen().Count==2,"Select-visible applies to quick search results and keeps other apps unchecked after clearing search");
   unselect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));check(Chosen().Count==0&&!clean.IsEnabled,"The quick-results clear action clears both hidden and visible selections");
   first.Selected=true;search.Text="Beta";check(Chosen().Single().Rule==rules[0]&&Get<TextBlock>("SelectionText").Text.Contains("筛选外 1 项"),"Quick search preserves selection and explicitly identifies selected items outside the filter");search.Text="";SetMode(false);check(Chosen().Single().Rule==rules[0]&&clean.Content.ToString()=="清理已选","Re-entering Simple cleanup preserves quick-item choices and labels the action as selected cleanup");
   foreach(var sample in new[]{new[]{"zh-CN","dark"},new[]{"zh-CN","light"},new[]{"en","light"}}){UiText.SetLanguage(sample[0]);UiTheme.Set(sample[1],false);Window.Width=sample[1]=="dark"?1280:1040;Window.Height=sample[1]=="dark"?900:740;foreach(var node in treeNodes)ExpandAll(node,true);App.Render(Window,Path.Combine(output,"quick-selection-"+sample[0]+"-"+sample[1]+".png"));}
   UiText.SetLanguage("zh-CN");int confirmations=0;ConfirmCleanup=(items,backed)=>{confirmations++;return false;};Get<ComboBox>("CleanPolicy").SelectedIndex=1;await CleanAsync();
   check(confirmations==0&&lastResult.Deleted==1&&!File.Exists(Path.Combine(rules[0].Roots[0],"old.cache"))&&File.Exists(Path.Combine(rules[1].Roots[0],"old.cache"))&&File.Exists(Path.Combine(rules[2].Roots[0],"old.cache")),"Quick cleanup deletes the checked cache only, keeps unchecked caches, and requires no low-risk popup");
   check(File.Exists(recent)&&File.Exists(excludedFile)&&phase=="complete","Quick selection keeps recent files and manual-only content and reaches cleanup completion");
  }finally{QuickRuleFactory=priorFactory;RuleFactory=priorRegular;ConfirmCleanup=priorConfirm;engine=priorEngine;quickScan=priorQuick;Window.Width=width;Window.Height=height;UiText.SetLanguage(language);UiTheme.Set(theme,false);ShowReady();scanMode.SelectedIndex=priorMode;Get<ComboBox>("CleanPolicy").SelectedIndex=priorPolicy;for(int i=0;i<groups.Count;i++)groups[i].Selected=priorScopes[i];}
 }
}
}
