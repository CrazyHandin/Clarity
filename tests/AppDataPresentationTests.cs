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
   engine=new Engine(new List<Rule>{rule});LastDiscovery=new DiscoveryResult{CacheSearch=new CacheSearchReport{Directories=120,Caches=12,Limited=true}};Apply(new List<Finding>{finding});
   check(!Get<TextBlock>("HeroNote").Text.Contains("AppData")&&Get<TextBlock>("HeroNote").Text.Contains("部分目录未完成"),"Results omit implementation labels but retain an honest incomplete-scan notice");
   check(leaves.Count==0&&!clean.IsEnabled,"Running caches without actionable candidates do not create empty result rows");
   finding.Running=false;finding.Candidates.Add(new Snapshot{Path=Path.Combine(rule.Roots[0],"fixture.bin"),Bytes=512});finding.EligibleBytes=512;Apply(new List<Finding>{finding});detailNode=leaves.Single(n=>n.Finding.Rule==rule);ShowDetail();
   var details=Get<TextBlock>("DetailRange");check(details.Text.Contains("软件使用中：")&&details.Text.Contains("保留近期文件：")&&details.Text.Contains("资料与安全过滤："),"Cache details explain running-state, recent-file and safety-filter retention separately");App.Render(Window,Path.Combine(output,"appdata-retention-chinese.png"));
   UiText.SetLanguage("en");check(details.Text.Contains("App in use:")&&details.Text.Contains("Recent files retained:")&&details.Text.Contains("safety filters:"),"AppData retention explanations follow the selected English language");App.Render(Window,Path.Combine(output,"appdata-retention-english.png"));
   check(!ScanExplanation.Coverage(new CacheSearchReport{Directories=120,Caches=12}).Contains("AppData"),"Completed discovery does not add the internal scanning label to the interface");
   var emptyRule=new Rule("ui-empty","Empty cache","软件缓存","","",new string[0],1,true){Notice=true};var noTargets=new Finding{Rule=emptyRule,Files=2,TotalBytes=32,RecentBytes=32};
   var retainedRule=new Rule("ui-retained-only","Retained","保留项目","","",new string[0],1,false){Protected=true,AllowManual=true};var retained=new Finding{Rule=retainedRule,Files=1,TotalBytes=24,ManualBytes=24,ManualCandidates=new List<Snapshot>{new Snapshot{Path=Path.Combine(output,"retained-fixture.bin"),Bytes=24}}};
   var emptyRegistry=new Finding{Rule=new Rule("ui-empty-registry","Registry","注册表","","",new string[0],0,false){InfoKind="registry-clean",Notice=true}};
   UiText.SetLanguage("zh-CN");Apply(new List<Finding>{finding,noTargets,retained,emptyRegistry});SetMode(true);
   check(leaves.Count==2&&leaves.All(n=>n.Selectable)&&!leaves.Any(n=>n.Finding.Rule==emptyRule||n.Finding==emptyRegistry),"Empty, recent-only and empty-registry results are hidden while real manual deletion remains available");
   check(leaves.Single(n=>n.Finding.ManualOverride).Selected==false&&leaves.All(n=>!n.Summary.Contains("高级模式自选")&&!n.Summary.Contains("暂无候选")),"Risk deletion stays unchecked and redundant retained-content and no-candidate labels are removed");
  }finally{UiText.SetLanguage(language);LastDiscovery=prior;quickScan=previousQuick;ShowReady();}
 }
}
}
