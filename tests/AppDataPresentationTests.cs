using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 void VerifyAppDataPresentation(string output,Action<bool,string> check){
  var prior=LastDiscovery;bool previousQuick=quickScan;string language=UiText.Language;
  try{
   ShowReady();UiText.SetLanguage("zh-CN");quickScan=false;var rule=new Rule("ui-appdata-cache","Fixture cache","软件缓存","独立资源缓存","资源重新加载",new[]{Path.Combine(output,"appdata-display-cache")},1,true,"Fixture"){Software="Fixture App",Part="网页与图形缓存"};var finding=new Finding{Rule=rule,Files=4,TotalBytes=8192,Running=true,RunningBytes=8192,RecentBytes=2048,FilteredBytes=1024};
   engine=new Engine(new List<Rule>{rule});LastDiscovery=new DiscoveryResult{CacheSearch=new CacheSearchReport{Directories=120,Caches=12,Limited=true}};Apply(new List<Finding>{finding});detailNode=leaves.Single(n=>n.Finding.Rule==rule);ShowDetail();
   check(Get<TextBlock>("HeroNote").Text.Contains("AppData")&&Get<TextBlock>("HeroNote").Text.Contains("部分目录未完成"),"An incomplete AppData pass is visible in the result summary without a scan-complete animation");
   check(detailNode.Summary.Contains("使用中")&&detailNode.Summary.Contains(App.FormatSize(finding.TotalBytes))&&!detailNode.Selectable,"Running app caches remain visible with their detected size while cleanup stays disabled");
   var details=Get<TextBlock>("DetailRange");check(details.Text.Contains("软件使用中：")&&details.Text.Contains("保留近期文件：")&&details.Text.Contains("资料与安全过滤："),"Cache details explain running-state, recent-file and safety-filter retention separately");App.Render(Window,Path.Combine(output,"appdata-retention-chinese.png"));
   UiText.SetLanguage("en");check(details.Text.Contains("App in use:")&&details.Text.Contains("Recent files retained:")&&details.Text.Contains("safety filters:"),"AppData retention explanations follow the selected English language");App.Render(Window,Path.Combine(output,"appdata-retention-english.png"));
   check(groups.Single(g=>g.Name=="软件缓存").Description.Contains("AppData"),"The software-cache scope explicitly identifies AppData coverage");
  }finally{UiText.SetLanguage(language);LastDiscovery=prior;quickScan=previousQuick;ShowReady();}
 }
}
}
