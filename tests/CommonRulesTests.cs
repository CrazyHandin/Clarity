using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ClearSpace {
public static class CommonRulesTests {
 public static int Run(string output){
  Directory.CreateDirectory(output);var checks=new List<string>();Action<bool,string> verify=(ok,label)=>{if(!ok)throw new Exception("FAILED: "+label);checks.Add("PASS: "+label);};
  string root=Path.Combine(Path.GetFullPath(output),"common-fixtures-"+Guid.NewGuid().ToString("N")),local=Path.Combine(root,"Local"),roaming=Path.Combine(root,"Roaming"),profile=Path.Combine(root,"Profile"),data=Path.Combine(root,"ProgramData");
  Func<string,string,string> file=(tail,text)=>{string path=Path.Combine(root,tail);Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,text);File.SetLastWriteTimeUtc(path,DateTime.UtcNow.AddDays(-40));return path;};
  try{
   string legacy=Path.Combine(Path.GetPathRoot(root),"ClearSpaceBackups"),modern=Path.Combine(Path.GetPathRoot(root),"ClarityBackups");verify(BackupFolders.UpgradeDefault(legacy)==modern&&BackupFolders.UpgradeDefault(legacy+"\\")==modern,"Legacy default backup paths adopt ClarityBackups without moving any backup files");
   string custom=Path.Combine(root,"MyBackups");verify(BackupFolders.UpgradeDefault(custom)==custom&&BackupFolders.UpgradeDefault("")=="","Custom backup locations remain configured and blank uses automatic defaults");
   verify(BackupFolders.UpgradeDefault(Path.Combine(CleanerSettings.Root,"FileBackups"))==Path.Combine(CleanerSettings.Root,"ClarityBackups"),"The legacy same-drive fallback adopts ClarityBackups");
   string chat=file("Profile\\Documents\\Tencent Files\\10000\\nt_qq\\nt_db\\chat.db","chat records"),psd=file("Profile\\Pictures\\work.psd","image project"),recover=file("Roaming\\Adobe\\InDesign\\Recovery\\work.indd","unsaved project");
   string browser=file("Roaming\\Tencent\\WeChat\\radium\\web\\profiles\\web_shell\\Cache\\data_1","page resource"),cookies=file("Roaming\\Tencent\\WeChat\\radium\\web\\profiles\\web_shell\\Cookies","account cookies"),qqFile=file("Roaming\\QQEX\\users\\123\\Cache\\data_1","QQ page"),qqDb=file("Roaming\\QQEX\\users\\123\\Cache\\secret.db","private database");
   string ipch=file("Local\\Microsoft\\vscode-cpptools\\ipch\\old.ipch","compiled header"),fresh=file("Local\\Microsoft\\vscode-cpptools\\ipch\\fresh.ipch","fresh header");File.SetLastWriteTimeUtc(fresh,DateTime.UtcNow);
   string cfa=file("Roaming\\Adobe\\Common\\Media Cache Files\\audio.cfa","conformed audio"),project=file("Roaming\\Adobe\\Common\\Media Cache Files\\work.prproj","project"),settings=file("Roaming\\Adobe\\Common\\Media Cache Files\\Settings\\state.bin","settings"),wave=file("Roaming\\Adobe\\Common\\Media Cache Files\\AutoSave\\work.cfa","unsaved data");
   string wpsCache=file("Local\\Kingsoft\\office6\\cache\\httpcache\\data_1","http resource"),wpsBackup=file("Local\\Kingsoft\\office6\\backup\\document.docx","document recovery");
   var wechat=new Rule("wechat-web","Wechat","软件缓存","cache","cache",new string[0],1,true);var qq=new Rule("qq-web","QQ","软件缓存","cache","cache",new[]{Path.Combine(roaming,"QQEX\\users\\123\\Cache")},1,true);var media=new Rule("adobe-media","Adobe","软件缓存","media","media",new[]{Path.Combine(roaming,"Adobe\\Common\\Media Cache Files")},7,false);
   var rules=new List<Rule>{wechat,qq,media};CommonSoftwareRules.Add(rules,local,roaming,profile,data);
   verify(wechat.Roots.Contains(Path.GetDirectoryName(browser))&&!wechat.Roots.Any(p=>p.Contains("WeChat Files")||p.Contains("xwechat_files")),"Versioned WeChat webpage discovery never includes chat storage roots");
   verify(!qq.Accept(qqDb)&&!qq.ManualAccept(qqDb),"QQ private databases are excluded from normal and manual cache deletion");
   var engine=new Engine(rules){IsRunning=names=>false,FileBackupDirectory=Path.Combine(root,"ClarityBackups")};var found=engine.ScanRule(wechat,CancellationToken.None);var qqFound=engine.ScanRule(qq,CancellationToken.None);var result=engine.Clean(new[]{found,qqFound},true,CancellationToken.None,null);
   verify(result.Deleted==2&&!File.Exists(browser)&&!File.Exists(qqFile)&&File.Exists(cookies)&&File.Exists(chat)&&File.Exists(qqDb),"Quick messaging-page cleanup deletes only page caches and retains login and chat files");
   var cpp=rules.Single(r=>r.Id=="code-cpp-ipch");found=engine.ScanRule(cpp,CancellationToken.None);verify(found.Candidates.Count==1&&found.Candidates[0].Path==ipch,"C/C++ cache scan finds only old ipch files and retains recent files");
   result=engine.Clean(new[]{found},true,CancellationToken.None,null);verify(result.Deleted==1&&!File.Exists(ipch)&&File.Exists(fresh),"C/C++ cache cleanup preserves recent compilation data");
   found=engine.ScanRule(media,CancellationToken.None);verify(found.Candidates.Count==1&&found.Candidates[0].Path==cfa&&found.ManualCandidates.Count==0,"Adobe media scanning excludes projects, settings and automatic recovery even inside a cache directory");
   result=engine.Clean(new[]{found},true,CancellationToken.None,null);verify(result.Deleted==0&&File.Exists(cfa),"One-click cleanup cannot delete Adobe media cache");
   result=engine.Clean(new[]{found},false,CancellationToken.None,null);verify(result.Deleted==1&&result.BackupFiles==1&&File.Exists(result.FileBackupManifest)&&File.Exists(project)&&File.Exists(settings)&&File.Exists(wave),"Adobe advanced cleaning forces backup and preserves personal content");
   var restored=FileRecovery.RestoreDetailed(result.FileBackupManifest,true);verify(restored.Restored==1&&restored.Cleared==1&&File.Exists(cfa)&&!File.Exists(result.FileBackupManifest),"Adobe cache backup restores successfully and clears only its completed backup");
   engine.IsRunning=owners=>owners.Contains("CoreSync");found=engine.ScanRule(media,CancellationToken.None);verify(!found.Running&&found.Candidates.Count==1,"Unrelated Creative Cloud background synchronization does not block media-cache candidates");
   engine.IsRunning=owners=>owners.Contains("Adobe Premiere Pro");found=engine.ScanRule(media,CancellationToken.None);verify(found.Running&&found.Candidates.Count==0,"Running media applications protect shared Adobe media-cache files");engine.IsRunning=owners=>false;
   var wps=rules.Single(r=>r.Id=="wps-http-cache");found=engine.ScanRule(wps,CancellationToken.None);verify(found.Candidates.Count==1&&found.Candidates[0].Path==wpsCache&&wps.ForceBackup&&!wps.Quick,"WPS discovery limits candidates to HTTP cache and requires advanced backup");
   verify(File.Exists(wpsBackup)&&File.Exists(psd)&&File.Exists(recover)&&File.Exists(chat),"Office backups, Adobe projects and chat records remain untouched throughout cleanup");
   engine.IsRunning=owners=>owners.Contains("Code");found=engine.ScanRule(cpp,CancellationToken.None);verify(found.Running&&found.Candidates.Count==0,"VS Code running state blocks its C/C++ cache cleanup");
   var catalog=RuleBook.Create();verify(!catalog.Any(r=>new[]{"neko","aclos","ocs-web","perfect-web","taptap","discord-canary","opera-gx","vscode-insiders"}.Contains(r.Id)||r.Id.StartsWith("edge-dev")||r.Id.StartsWith("chrome-canary")||r.Id.StartsWith("discord-ptb")),"The built-in catalog omits niche launchers and preview-channel duplicates");
   verify(catalog.Select(r=>r.Id).Distinct().Count()==catalog.Count&&catalog.Where(r=>r.Id.StartsWith("adobe-")).All(r=>!r.Quick&&r.ForceBackup),"Common-software additions keep unique identifiers and Adobe remains advanced with mandatory backup");
   File.WriteAllLines(Path.Combine(output,"common-rules-test-results.txt"),checks.Concat(new[]{"ALL "+checks.Count+" COMMON CHECKS PASSED","Only generated workspace fixtures were modified."}));return 0;
  }catch(Exception ex){File.WriteAllLines(Path.Combine(output,"common-rules-test-results.txt"),checks.Concat(new[]{ex.ToString()}));return 1;}
 }
}
}
