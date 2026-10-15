using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
namespace ClearSpace {
public sealed partial class Controller {
 void VerifyResultPresentation(string output,Action<bool,string> check){
  var previousEngine=engine;bool previousQuick=quickScan;
  try{
   string fixture=Path.Combine(output,"result-presentation");
   foreach(string relative in new[]{"Editor\\Cache\\old.log","Editor\\Settings\\preferences.json","Browser\\Cache\\old.log"}){string path=Path.Combine(fixture,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,"generated fixture");File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-30));}
   string raw="[Fixture Editor Cache *]\nDetectFile=%LocalAppData%\\Editor\nFileKey1=%LocalAppData%\\Editor\\Cache|*.log\nFileKey2=%LocalAppData%\\Editor\\Settings|*.json\nRegKey1=HKCU\\Software\\Fixture\\MRU\n[Google Chrome Cache *]\nLangSecRef=3029\nDetectFile=%LocalAppData%\\Browser\nFileKey1=%LocalAppData%\\Browser\\Cache|*.log\nRegKey1=HKCU\\Software\\FixtureBrowser\\MRU\n";
   var environment=new Winapp2Environment{Drive=Path.GetPathRoot(fixture),OSVersion=new Version(10,0,19045),AllowedBases=new[]{fixture},DeniedBases=new string[0]};environment.Variables["LocalAppData"]=new[]{fixture};
   var discovery=new DiscoveryResult();discovery.Applications.Add(new ApplicationIdentity{Name="Fixture Editor",Executable=Path.Combine(fixture,"editor.exe")});
   var rules=Winapp2Adapter.Resolve(Winapp2Parser.Parse(raw),discovery,environment,CancellationToken.None,null);engine=new Engine(rules);engine.IsRunning=owners=>false;var findings=engine.Scan(CancellationToken.None,null);
   var registry=new Rule("fixture-registry-display","Registry fixture","注册表","UI fixture only","Never executed",new string[0],0,false){InfoKind="registry-clean",Software="Windows",Part="失效路径"};
   findings.Add(new Finding{Rule=registry,RegistryCandidates=new System.Collections.Generic.List<RegistryEntry>{new RegistryEntry{Scope=new RegistryScope{Kind="uninstall",Hive=Microsoft.Win32.RegistryHive.CurrentUser,View=Microsoft.Win32.RegistryView.Registry64},KeyPath="Software\\ClarityFixture\\Missing",Title="Fixture uninstall record"}}});
   quickScan=false;Apply(findings);SetMode(true);var retained=treeNodes.Single(n=>n.Title=="保留项目");var software=retained.Children.Single(n=>n.Title=="软件缓存");var app=software.Children.Single(n=>n.Title=="Fixture Editor");
   check(software.Depth==1&&app.Depth==2&&app.Children.All(n=>n.Depth==3&&n.Title.Contains("Fixture Editor")),"Retained community files and notices identify the app under Retained, App caches, then the software name");
   check(retained.Children.Single(n=>n.Title=="网页缓存").Children.Single(n=>n.Title=="Google Chrome").Children.All(n=>n.Title.Contains("Google Chrome")),"Retained browser community scopes preserve their own browser submenu and software names");
   var manual=app.Leaves().Single(n=>n.Finding.ManualOverride);check(manual.Selectable&&manual.Selected==false&&manual.Finding.Rule.ForceBackup,"Moving retained community files keeps deletion opt-in and mandatory backup protection");
   var registryNode=leaves.Single(n=>n.Finding.Rule==registry);registryNode.Selected=true;
   check(Get<TextBlock>("HeroNote").Text==leaves.Count(n=>n.Finding.Rule.InfoKind!="registry-clean")+" 个识别结果"&&!Get<TextBlock>("HeroNote").Text.Contains("注册")&&Get<TextBlock>("PlannedText").Text.Contains("1 项注册表"),"Registry candidates remain selectable in their category and footer while staying out of the space summary");
   search.Text="保留";foreach(var node in treeNodes)ExpandAll(node,true);Window.UpdateLayout();App.Render(Window,Path.Combine(output,"retained-community-scopes.png"));search.Text="";
   busy=true;phase="scan";OnProgress(new ScanProgress{Completed=1,Total=engine.Rules.Count,Files=3,RegistryItems=12,FoundBytes=32,Group="软件缓存",Title="Fixture"},epoch);check(Get<TextBlock>("HeroNote").Text=="容量随扫描实时更新","Live scanning keeps registry candidate counts out of the disk-space card");busy=false;phase="results";
  }finally{busy=false;engine=previousEngine;quickScan=previousQuick;ShowReady();}
 }
}
}
