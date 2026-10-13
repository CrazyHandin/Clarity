using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
namespace ClearSpace {
public static class V2Tests {
 public static int Run(string output){
  Directory.CreateDirectory(output);var checks=new List<string>();Action<bool,string> verify=(value,name)=>{if(!value)throw new Exception("FAILED: "+name);checks.Add("PASS: "+name);};
  string id=Guid.NewGuid().ToString("N"),testPath="Software\\ClearSpace.Tests\\"+id,files=Path.Combine(Path.GetFullPath(output),"v2-fixture-"+id);Directory.CreateDirectory(files);
  var view=Environment.Is64BitOperatingSystem?RegistryView.Registry64:RegistryView.Registry32;var appScope=new RegistryScope{Hive=RegistryHive.CurrentUser,View=view,Path=testPath+"\\App Paths",Kind="apppaths"};
  var uninstallScope=new RegistryScope{Hive=RegistryHive.CurrentUser,View=view,Path=testPath+"\\Uninstall",Kind="uninstall"};
  Func<RegistryScope,string,Action<RegistryKey>,string> make=(s,name,write)=>{string path=s.Path+"\\"+name;using(var root=RegistryKey.OpenBaseKey(s.Hive,s.View))using(var key=root.CreateSubKey(path))write(key);return path;};
  Func<RegistryScope,string,bool> exists=(s,p)=>{using(var root=RegistryKey.OpenBaseKey(s.Hive,s.View))using(var key=root.OpenSubKey(p,false))return key!=null;};
  string missing=Path.Combine(RuleBook.TargetDrive,"ClearSpace-Missing-"+id,"app.exe"),existing=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"cmd.exe");
  Func<RegistryScope,Rule> rule=s=>new Rule("test-"+s.Kind,s.Kind,"注册表","test","test",new string[0],0,false){InfoKind="registry-clean",RegistryScopes=new[]{s}};
  try{
   verify(RegistryCleaner.Allowed(appScope)&&!RegistryCleaner.Allowed(new RegistryScope{Hive=RegistryHive.CurrentUser,View=view,Path="Software\\Other",Kind="apppaths"}),"Registry cleaner limits writable scopes to exact whitelist or isolated test branch");
   string orphan=make(appScope,"app.exe",k=>{k.SetValue("",missing,RegistryValueKind.ExpandString);k.SetValue("Path",Path.GetDirectoryName(missing),RegistryValueKind.String);});
   string valid=make(appScope,"cmd.exe",k=>k.SetValue("",existing)),network=make(appScope,"remote.exe",k=>k.SetValue("",@"\\unknown-server\share\remote.exe")),complex=make(appScope,"complex.exe",k=>{k.SetValue("",missing.Replace("app.exe","complex.exe"));k.SetValue("DropTarget","{guid}");});
   var appRule=rule(appScope);var engine=new Engine(new List<Rule>{appRule});engine.RegistryBackupDirectory=Path.Combine(files,"backups");var finding=engine.ScanRule(appRule,CancellationToken.None);
   verify(finding.RegistryCandidates.Count==1&&finding.RegistryCandidates[0].KeyPath==orphan,"Only simple registration with missing executable and directory becomes a candidate");
   var result=engine.Clean(new[]{finding},true,CancellationToken.None,null);verify(result.RegistryDeleted==0&&exists(appScope,orphan),"One-click cleanup never deletes registry entries");
   engine.RegistryBackupDirectory=Path.Combine(files,"blocked");File.WriteAllText(engine.RegistryBackupDirectory,"fixture");bool blocked=false;try{engine.Clean(new[]{finding},false,CancellationToken.None,null);}catch(IOException){blocked=true;}verify(blocked&&exists(appScope,orphan),"Backup failure prevents registry deletion");
   engine.RegistryBackupDirectory=Path.Combine(files,"backups");result=engine.Clean(new[]{finding},false,CancellationToken.None,null);
   verify(result.RegistryDeleted==1&&!exists(appScope,orphan)&&File.Exists(result.RegistryBackupPath),"Explicit advanced cleanup backs up before removing orphan registration");
   verify(exists(appScope,valid)&&exists(appScope,network)&&exists(appScope,complex),"Valid, network and complex registrations remain intact");
   verify(RegistryCleaner.Restore(result.RegistryBackupPath)==1&&exists(appScope,orphan),"Generated backup restores deleted registration");
   using(var root=RegistryKey.OpenBaseKey(appScope.Hive,view))using(var key=root.OpenSubKey(orphan,true)){verify((string)key.GetValue("",null,RegistryValueOptions.DoNotExpandEnvironmentNames)==missing&&key.GetValueKind("")==RegistryValueKind.ExpandString,"Restore preserves expandable string type and raw data");key.SetValue("",existing);}
   verify(RegistryCleaner.Restore(result.RegistryBackupPath)==0,"Restore refuses to overwrite existing registration");
   using(var root=RegistryKey.OpenBaseKey(appScope.Hive,view))using(var key=root.OpenSubKey(orphan,true))key.SetValue("",missing,RegistryValueKind.ExpandString);
   finding=engine.ScanRule(appRule,CancellationToken.None);using(var root=RegistryKey.OpenBaseKey(appScope.Hive,view))using(var key=root.OpenSubKey(orphan,true))key.SetValue("Path","changed");
   result=engine.Clean(new[]{finding},false,CancellationToken.None,null);verify(result.RegistryDeleted==0&&exists(appScope,orphan),"Registry changes after scan block deletion");
   make(uninstallScope,"Fixture Tool",k=>{k.SetValue("DisplayName","Fixture Tool");k.SetValue("Publisher","Fixture Publisher");k.SetValue("InstallLocation",Path.GetDirectoryName(missing));k.SetValue("UninstallString","\""+missing+"\" /remove");k.SetValue("WindowsInstaller",0,RegistryValueKind.DWord);k.SetValue("Counter",1234567890123L,RegistryValueKind.QWord);k.SetValue("Binary",new byte[]{0,1,255},RegistryValueKind.Binary);k.SetValue("Multi",new[]{"a","b"},RegistryValueKind.MultiString);});
   make(uninstallScope,"MSI",k=>{k.SetValue("DisplayName","MSI");k.SetValue("InstallLocation",Path.GetDirectoryName(missing));k.SetValue("UninstallString",missing);k.SetValue("WindowsInstaller",1);});
   make(uninstallScope,"System",k=>{k.SetValue("DisplayName","System");k.SetValue("InstallLocation",Path.GetDirectoryName(missing));k.SetValue("UninstallString",missing);k.SetValue("SystemComponent",1);});
   make(uninstallScope,"Microsoft Item",k=>{k.SetValue("DisplayName","Microsoft Item");k.SetValue("InstallLocation",Path.GetDirectoryName(missing));k.SetValue("UninstallString",missing);k.SetValue("Publisher","Microsoft Corporation");});
   var uninstallRule=rule(uninstallScope);engine=new Engine(new List<Rule>{uninstallRule}){RegistryBackupDirectory=Path.Combine(files,"backups")};finding=engine.ScanRule(uninstallRule,CancellationToken.None);verify(finding.RegistryCandidates.Count==1,"Uninstall cleaning excludes MSI, system and Microsoft records");
   result=engine.Clean(new[]{finding},false,CancellationToken.None,null);verify(result.RegistryDeleted==1&&RegistryCleaner.Restore(result.RegistryBackupPath)==1,"Uninstall record can be removed and restored from backup");
   using(var root=RegistryKey.OpenBaseKey(uninstallScope.Hive,view))using(var key=root.OpenSubKey(uninstallScope.Path+"\\Fixture Tool",false))verify((int)key.GetValue("WindowsInstaller")==0&&(long)key.GetValue("Counter")==1234567890123L&&((byte[])key.GetValue("Binary")).SequenceEqual(new byte[]{0,1,255})&&((string[])key.GetValue("Multi")).SequenceEqual(new[]{"a","b"}),"Backup restores DWORD, QWORD, binary and multi-string values exactly");
   var forged=new RegistryBackup{Computer=Environment.MachineName,UserSid=System.Security.Principal.WindowsIdentity.GetCurrent().User.Value,Entries=new List<RegistryEntry>{new RegistryEntry{Scope=new RegistryScope{Hive=RegistryHive.CurrentUser,View=view,Path="Software\\Unknown",Kind="apppaths"},KeyPath="Software\\Unknown\\app.exe"}}};string fake=Path.Combine(files,"forged.json");App.WriteJson(fake,forged);bool rejected=false;try{RegistryCleaner.Restore(fake);}catch(InvalidDataException){rejected=true;}verify(rejected,"Restore rejects backup outside permitted registry scopes");
   var history=new RegistryScope{Hive=RegistryHive.CurrentUser,View=view,Path=testPath+"\\RunMRU",Kind="run"};using(var root=RegistryKey.OpenBaseKey(history.Hive,view))using(var key=root.CreateSubKey(history.Path)){key.SetValue("a","fixture command");key.SetValue("MRUList","a");}
   var historyRule=rule(history);engine=new Engine(new List<Rule>{historyRule}){RegistryBackupDirectory=Path.Combine(files,"backups")};finding=engine.ScanRule(historyRule,CancellationToken.None);result=engine.Clean(new[]{finding},false,CancellationToken.None,null);verify(result.RegistryDeleted==1&&RegistryCleaner.Restore(result.RegistryBackupPath)==1,"Optional run-history cleanup is backed up and reversible");
   string cache=Path.Combine(files,"cache");Directory.CreateDirectory(cache);string data=Path.Combine(cache,"old.dat");File.WriteAllText(data,"fixture");File.SetLastWriteTimeUtc(data,DateTime.UtcNow.AddDays(-30));var a=new Rule("dedup-a","a","软件缓存","","",new[]{cache},7,true);var b=new Rule("dedup-b","b","软件缓存","","",new[]{cache},7,true);var updates=new List<ScanProgress>();engine=new Engine(new List<Rule>{a,b}){LiveProgress=p=>updates.Add(p)};var results=engine.Scan(CancellationToken.None,null);
   verify(results.Sum(f=>f.Candidates.Count)==1&&results.Sum(f=>f.EligibleBytes)==new FileInfo(data).Length,"Overlapping rules do not double-count cleanup candidates");
   verify(updates.Count>=2&&updates.Last().Completed==2&&updates.Last().Total==2&&updates.Last().FoundBytes==new FileInfo(data).Length,"Scan progress reports real rule completion and running capacity");
   var catalog=RuleBook.Create();verify(catalog.Count>=180&&catalog.Select(r=>r.Group).Distinct().Count()==9,"Expanded online-researched catalog covers nine categories and at least 180 rules");
   verify(catalog.Select(r=>r.Id).Distinct().Count()==catalog.Count,"Expanded rule identifiers remain unique");verify(catalog.Where(r=>r.InfoKind=="registry-clean").All(r=>!r.Quick&&!r.Protected&&r.RegistryScopes.All(RegistryCleaner.Allowed)),"Registry candidates are advanced-only and scope-restricted");verify(catalog.Single(r=>r.Id=="uv-cache").Protected,"uv cache follows upstream policy and is not directly deleted");verify(catalog.Where(r=>r.Id.StartsWith("jb-")&&r.Part=="索引缓存").All(r=>r.Roots.All(p=>!p.EndsWith("LocalHistory",StringComparison.OrdinalIgnoreCase))),"JetBrains cache rules preserve Local History");verify(catalog.Where(r=>r.InfoKind=="storage").All(r=>r.Protected),"Windows-managed update and recovery content remains protected");
   File.WriteAllLines(Path.Combine(output,"v2-test-results.txt"),checks.Concat(new[]{"ALL "+checks.Count+" V2 CHECKS PASSED","Only isolated test files and HKCU\\"+testPath+" were modified."}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(output,"v2-test-results.txt"),checks.Concat(new[]{ex.ToString()}));return 1;}
  finally{using(var root=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,view)){if(testPath=="Software\\ClearSpace.Tests\\"+id&&Guid.ParseExact(id,"N")!=Guid.Empty)root.DeleteSubKeyTree(testPath,false);}}
 }
}
}
