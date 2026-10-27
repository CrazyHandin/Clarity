#if CLARITY_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
namespace ClearSpace {
public static class DiscoveryTests {
 public static void Verify(string output,Action<bool,string> check){
  string root=Path.Combine(Path.GetFullPath(output),"discovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string exe=Path.Combine(root,"Fixture.exe");File.Copy(typeof(Engine).Assembly.Location,exe);
  var app=new ApplicationIdentity{Name="Fixture",Executable=exe,Product="Fixture product",Publisher="Fixture publisher",Signature="签名按需验证"};
  var rule=new ApplicationRule{MatchNames=new[]{"Fixture"},ProductNames=new[]{"Fixture product"},Publishers=new[]{"Fixture publisher"}};
  check(RuleStore.MatchesDefinition(rule,app)&&!app.SignatureChecked,"Ordinary name, product and publisher matching does not eagerly verify signatures");
  rule.RequireSignature=true;rule.ProductNames=new[]{"Different product"};check(!RuleStore.MatchesDefinition(rule,app)&&!app.SignatureChecked,"An unrelated product is rejected before signature work");
  rule.ProductNames=new[]{"Fixture product"};check(!RuleStore.MatchesDefinition(rule,app)&&app.SignatureChecked&&app.Signature!="签名验证通过","Signature-requiring rules verify a fresh executable and reject unsigned content");
  app.FingerprintCached=true;app.SignatureChecked=false;app.Signature="签名验证通过";app.Signer="Fixture signer";
  check(!RuleStore.MatchesDefinition(rule,app)&&app.SignatureChecked&&!app.FingerprintCached&&String.IsNullOrEmpty(app.Signer),"Cached trust is rechecked against the actual executable instead of authorizing a match");
  rule.RequireSignature=false;rule.Publishers=new[]{"Fixture signer"};app.FingerprintCached=true;app.SignatureChecked=false;app.Signer="Fixture signer";
  check(!RuleStore.MatchesDefinition(rule,app)&&app.SignatureChecked&&String.IsNullOrEmpty(app.Signer),"Publisher matching cannot trust an unverified cached signer");
  var paths=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);File.WriteAllText(Path.Combine(root,"unins000.exe"),"never executed fixture");
  check(ApplicationDiscovery.FindExecutable("\""+root+"\"",paths,CancellationToken.None)==exe&&paths.Count==1,"Executable fallback normalizes quoted local paths and ignores uninstallers");
  check(ApplicationDiscovery.FindExecutable(root+Path.DirectorySeparatorChar,paths,CancellationToken.None)==exe,"Directory fallback preserves the executable for repeated registry references");
  check(ApplicationDiscovery.InstallDirectory(@"\\server\share")==null&&ApplicationDiscovery.InstallDirectory(@"\relative")==null&&ApplicationDiscovery.InstallDirectory("relative")==null&&ApplicationDiscovery.InstallDirectory(@"\\?\C:\apps")==null,"Remote, relative and device paths are rejected before fallback directory enumeration");
  string absent=Path.Combine(root,"missing");check(ApplicationDiscovery.FindExecutable(absent,paths,CancellationToken.None)==null&&paths.ContainsKey(absent),"Missing fallback directories are memoized within a scan");
  using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool canceled=false;try{ApplicationDiscovery.FindExecutable(root,paths,cancel.Token);}catch(OperationCanceledException){canceled=true;}check(canceled,"Cached executable fallback still honors cancellation immediately");}
  check(new[]{"unins000.exe","unins001.exe","uninst32.exe","uninstall.exe","Uninstall-App.exe","uninstaller.exe","unwise.exe"}.All(n=>ApplicationDiscovery.Uninstaller(Path.Combine(root,n))),"Application discovery consistently recognizes common uninstaller filenames across every source");
  check(ApplicationDiscovery.Uninstaller(Path.Combine(root,"helper.exe"),"Uninstall Fixture App")&&ApplicationDiscovery.Uninstaller(Path.Combine(root,"helper.exe"),"卸载 Fixture App"),"An uninstall shortcut is excluded even when its target has a generic helper filename");
  check(!ApplicationDiscovery.Uninstaller(exe,"Fixture")&&!ApplicationDiscovery.Uninstaller(Path.Combine(root,"UninstallTool.exe"),"Uninstall Tool")&&!ApplicationDiscovery.Uninstaller(Path.Combine(root,"Unison.exe"),"Unison"),"Actual applications, including an uninstall utility and similarly named executables, remain discoverable");
  string renamed=Path.Combine(root,"cleanup-agent.exe");File.WriteAllText(renamed,"fixture uninstall command; never executed");paths[root]=renamed;
  check(ApplicationDiscovery.FindExecutable(root,paths,CancellationToken.None,new HashSet<string>(new[]{renamed},StringComparer.OrdinalIgnoreCase))==exe,"An executable cached before uninstall-command discovery is replaced by the real application target");
  string id="ClarityDiscoveryFixture-"+Guid.NewGuid().ToString("N"),keyA=RegistryCleaner.Uninstall+"\\"+id,keyB=keyA+"-helper",appPath=RegistryCleaner.AppPaths+"\\"+id+".exe";
  string only=Path.Combine(root,"uninstaller-only"),onlyExe=Path.Combine(only,"unins000.exe"),keyOnly=keyA+"-only",keyDual=keyA+"-dual",component=Path.Combine(root,"FixturePlugin.exe");Directory.CreateDirectory(only);File.WriteAllText(onlyExe,"fixture; never executed");File.Copy(exe,component);
  try{
   using(var key=Registry.CurrentUser.CreateSubKey(keyA)){key.SetValue("DisplayName",id);key.SetValue("InstallLocation",root);key.SetValue("DisplayIcon",Path.Combine(root,"unins000.exe"));key.SetValue("UninstallString","\""+Path.Combine(root,"unins000.exe")+"\" /SILENT");}
   using(var key=Registry.CurrentUser.CreateSubKey(keyB)){key.SetValue("DisplayName",id);key.SetValue("InstallLocation",root);key.SetValue("DisplayIcon",exe);key.SetValue("UninstallString","\""+renamed+"\" /remove");}
   using(var key=Registry.CurrentUser.CreateSubKey(keyOnly)){key.SetValue("DisplayName",id+" only");key.SetValue("InstallLocation",only);key.SetValue("DisplayIcon",onlyExe);key.SetValue("UninstallString","\""+onlyExe+"\"");}
   using(var key=Registry.CurrentUser.CreateSubKey(keyDual)){key.SetValue("DisplayName",id+" component");key.SetValue("InstallLocation",root);key.SetValue("DisplayIcon",component);key.SetValue("UninstallString","\""+exe+"\" /uninstall-component");}
   using(var key=Registry.CurrentUser.CreateSubKey(appPath))key.SetValue("",renamed);
   var inventory=ApplicationDiscovery.Scan(new[]{root},CancellationToken.None,null);
   check(inventory.Applications.Any(a=>a.Executable==exe&&a.Evidence.Contains("卸载注册信息"))&&!inventory.Applications.Any(a=>a.Executable==renamed||ApplicationDiscovery.Uninstaller(a.Executable)),"Registry, App Paths and portable inputs never create separate app entries for the generated uninstall executables");
   check(inventory.Applications.Any(a=>a.Name==id+" only"&&a.Executable==""&&a.InstallDirectory==only),"An installed product keeps its installation record when its icon points only to an uninstaller");
   check(inventory.Applications.Any(a=>a.Executable==exe)&&inventory.Applications.Any(a=>a.Executable==component),"A genuine registered app remains visible when the same executable also removes an optional component");
  }finally{foreach(string key in new[]{keyA,keyB,keyOnly,keyDual,appPath})Registry.CurrentUser.DeleteSubKeyTree(key,false);}
 }
}
}
#endif
