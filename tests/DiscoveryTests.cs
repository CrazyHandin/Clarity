#if CLARITY_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
 }
}
}
#endif
