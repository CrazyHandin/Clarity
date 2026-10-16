using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
namespace ClearSpace {
public static class UpdateStrategyTests {
 public static void Verify(string root,Action<bool,string> check){
  Func<Action,bool> rejects=action=>{try{action();return false;}catch{return true;}};
  check(UpdateStrategy.Select(true,false,false,true)==SoftwareUpdateMode.InstalledFiles,"A writable custom installation updates files without running setup");
  check(UpdateStrategy.Select(true,true,true,false)==SoftwareUpdateMode.Service,"A protected installation with a registered service keeps the privileged update service");
  check(UpdateStrategy.Select(true,true,false,true)==SoftwareUpdateMode.Installer,"A missing service never turns Program Files into a user-writable update path");
  check(UpdateStrategy.Select(true,false,true,true)==SoftwareUpdateMode.InstalledFiles&&UpdateStrategy.Select(true,false,false,false)==SoftwareUpdateMode.Installer,"Custom locations never use a privileged service and unwritable ones retain explicit installer authorization");
  check(UpdateStrategy.Select(false,false,false,true)==SoftwareUpdateMode.Portable,"Portable applications keep their existing verified-file update flow");
  string programFiles=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);check(UpdateStrategy.ProgramFilesLocation(Path.Combine(programFiles,"Clarity"))&&!UpdateStrategy.ProgramFilesLocation(programFiles+"-custom\\Clarity"),"Protected install detection respects directory boundaries");
  string workspace=Path.Combine(root,"in-place-strategy");Directory.CreateDirectory(workspace);check(UpdateStrategy.Writable(workspace)&&Directory.GetFiles(workspace).Length==0&&!UpdateStrategy.Writable(Path.Combine(workspace,"absent")),"The write probe removes its own temporary file and does not create absent application directories");
  var acl=Directory.GetAccessControl(workspace);try{var denied=Directory.GetAccessControl(workspace);denied.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User,FileSystemRights.CreateFiles,AccessControlType.Deny));Directory.SetAccessControl(workspace,denied);check(!UpdateStrategy.Writable(workspace),"An actual directory write denial prevents the no-administrator update path");}finally{var restore=Directory.GetAccessControl(workspace);restore.SetSecurityDescriptorSddlForm(acl.GetSecurityDescriptorSddlForm(AccessControlSections.Access),AccessControlSections.Access);Directory.SetAccessControl(workspace,restore);}
  string[] installed={"Clarity.exe","Clarity.exe.config","Clarity.UpdateService.exe","Clarity.UpdateService.exe.config","使用说明.txt","rules/Winapp2.ini"};string zip=Path.Combine(workspace,"installed-files.zip");using(var archive=ZipFile.Open(zip,ZipArchiveMode.Create))foreach(string name in installed.Concat(new[]{"README.md","docs/TERMS.txt"}))using(var writer=new StreamWriter(archive.CreateEntry(name).Open()))writer.Write("new "+name);
  var full=UpdatePackage.Extract(zip,SoftwareUpdates.HashFile(zip),Path.Combine(workspace,"payload"));var payload=UpdatePackage.ForInstalledApplication(full);check(payload.Count==6&&payload.Files.All(file=>installed.Contains(file.Name)),"Installed updates select exactly the setup payload and exclude portable Markdown documents");
  var incomplete=new VerifiedUpdatePayload(full.DirectoryPath,full.Files.Where(file=>file.Name!="Clarity.UpdateService.exe").ToList());check(rejects(()=>UpdatePackage.ForInstalledApplication(incomplete)),"A file update missing an installed runtime component is rejected");
  string target=Path.Combine(workspace,"Installed App With Spaces");Directory.CreateDirectory(target);foreach(string name in installed){string file=Path.Combine(target,name);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,"old "+name);}foreach(string name in new[]{"unins000.exe","unins000.dat","Logs/history.json","ClarityBackups/keep.csbackup"}){string file=Path.Combine(target,name);Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(file,"keep "+name);}
  check(rejects(()=>UpdatePackage.Apply(target,payload,index=>{if(index==3)throw new IOException("Fixture interruption");}))&&installed.All(name=>File.ReadAllText(Path.Combine(target,name))=="old "+name),"An interrupted installed-file update rolls back all already replaced application files");
  UpdatePackage.Apply(target,payload);check(installed.All(name=>File.ReadAllText(Path.Combine(target,name))=="new "+name)&&!File.Exists(Path.Combine(target,"README.md"))&&!Directory.Exists(Path.Combine(target,"docs")),"An installed-file update replaces program files without adding portable-only documents");
  check(new[]{"unins000.exe","unins000.dat","Logs/history.json","ClarityBackups/keep.csbackup"}.All(name=>File.ReadAllText(Path.Combine(target,name))=="keep "+name),"Installed-file updates preserve the uninstaller, its records, cleanup history and backups");
 }
}
}
