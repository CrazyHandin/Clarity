using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;
namespace ClearSpace {
public sealed class InstalledRulePack {public string Id,FileName;public bool Enabled,CanRollback,IsPrimary;public LoadedRulePack Loaded;public string Name{get{return !String.IsNullOrWhiteSpace(Loaded.Pack.Name)?Loaded.Pack.Name:!String.IsNullOrWhiteSpace(FileName)?Path.GetFileNameWithoutExtension(FileName):"默认 JSON 规则库";}}}
public sealed class RulePackState {public int Schema=1;public string Active,Previous,FileName,PreviousFileName;public bool Enabled;}
public sealed class RulePackCatalog {
 static readonly object Gate=new object();public const int MaxPacks=20;
 readonly RuleStore parser;public readonly string DirectoryPath;
 public RulePackCatalog(string directory=null,string publicKey=null){DirectoryPath=Path.GetFullPath(directory??Path.Combine(CleanerSettings.Root,"RulePacks"));parser=new RuleStore(null,publicKey);}
 public static bool ValidId(string id){return id!=null&&Regex.IsMatch(id,"^[a-z0-9][a-z0-9-]{1,63}$");}
 string FileFor(string id){if(!ValidId(id))throw new InvalidDataException("无效的规则包 ID");return Path.Combine(DirectoryPath,id+".json");}
 public static string Identify(LoadedRulePack pack,string fileName){if(pack.Pack.Schema==2)return pack.Pack.PackId;if(String.IsNullOrWhiteSpace(fileName)||fileName!=Path.GetFileName(fileName)||fileName.Length>240)throw new InvalidDataException("旧格式 JSON 需要有效的文件名");using(var sha=SHA256.Create())return "legacy-"+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(fileName.ToLowerInvariant()))).Replace("-","").Substring(0,24).ToLowerInvariant();}
 RulePackState State(string id){string file=FileFor(id);if(!Engine.SafeAncestors(file)||(File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0||new FileInfo(file).Length>RuleStore.MaxBytes*4L)throw new IOException("无法验证规则包存储文件");var state=new JavaScriptSerializer{MaxJsonLength=RuleStore.MaxBytes*4}.Deserialize<RulePackState>(File.ReadAllText(file));if(state==null||state.Schema!=1||state.Active==null)throw new InvalidDataException("规则包记录无效");var pack=parser.Read(state.Active,true);if(Identify(pack,state.FileName)!=id)throw new InvalidDataException("规则包 ID 与存储记录不一致");return state;}
 InstalledRulePack Entry(string id,RulePackState state){return new InstalledRulePack{Id=id,FileName=state.FileName,Enabled=state.Enabled,CanRollback=!String.IsNullOrEmpty(state.Previous),Loaded=parser.Read(state.Active,true)};}
 void Save(string id,RulePackState state){AtomicFile.Write(FileFor(id),new JavaScriptSerializer{MaxJsonLength=RuleStore.MaxBytes*4}.Serialize(state));}
 public List<InstalledRulePack> List(IList<string> errors=null){lock(Gate){var result=new List<InstalledRulePack>();if(!Directory.Exists(DirectoryPath))return result;if(!Engine.SafeAncestors(Path.Combine(DirectoryPath,"probe"))){if(errors!=null){errors.Add("规则包目录包含链接");return result;}throw new IOException("规则包目录包含链接");}foreach(string file in Directory.EnumerateFiles(DirectoryPath,"*.json").OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).Take(MaxPacks+1)){try{if(result.Count>=MaxPacks)throw new InvalidDataException("规则包数量超过上限");string id=Path.GetFileNameWithoutExtension(file);result.Add(Entry(id,State(id)));}catch(Exception ex){if(errors!=null)errors.Add(Path.GetFileName(file)+" · "+ex.Message);}}return result;}}
 public LoadedRulePack Inspect(string raw,string fileName=null){var pack=parser.Read(raw,true);Identify(pack,fileName);return pack;}
 public InstalledRulePack Import(string raw,string fileName=null){lock(Gate){var loaded=Inspect(raw,fileName);string id=Identify(loaded,fileName);RulePackState state;if(File.Exists(FileFor(id))){state=State(id);var old=parser.Read(state.Active,true);if(old.Pack.Schema!=loaded.Pack.Schema)throw new InvalidDataException("该标识已被另一种格式的规则占用");if(new Version(loaded.Pack.Version)<=new Version(old.Pack.Version))throw new InvalidDataException("请提供更高版本的规则包；回退请使用回退按钮");state.Previous=state.Active;state.PreviousFileName=state.FileName;state.Active=raw;state.FileName=fileName;}else{if(Directory.Exists(DirectoryPath)&&Directory.EnumerateFiles(DirectoryPath,"*.json").Take(MaxPacks).Count()>=MaxPacks)throw new InvalidDataException("最多安装 20 个规则包");state=new RulePackState{Active=raw,FileName=fileName,Enabled=false};}Save(id,state);return Entry(id,state);}}
 public void SetEnabled(string id,bool enabled){lock(Gate){var state=State(id);state.Enabled=enabled;Save(id,state);}}
 public InstalledRulePack Rollback(string id){lock(Gate){var state=State(id);if(String.IsNullOrEmpty(state.Previous))throw new IOException("暂无上一版本");var previous=Inspect(state.Previous,state.PreviousFileName);if(Identify(previous,state.PreviousFileName)!=id)throw new InvalidDataException("上一版本属于不同规则包");string current=state.Active,name=state.FileName;state.Active=state.Previous;state.FileName=state.PreviousFileName;state.Previous=current;state.PreviousFileName=name;Save(id,state);return Entry(id,state);}}
 public void Remove(string id){lock(Gate){State(id);File.Delete(FileFor(id));}}
 public static List<Rule> Compile(InstalledRulePack pack,DiscoveryResult discovery,CancellationToken token){var rules=RuleCompiler.Resolve(pack.Loaded,discovery,token);foreach(var rule in rules){rule.Id="library-pack-"+pack.Id+"-"+rule.Id.Substring("library-".Length);rule.Source="JSON · "+pack.Name;rule.Detection="JSON 文件："+(pack.FileName??pack.Name)+"\n"+rule.Detection;}return rules;}
 public List<Rule> Resolve(DiscoveryResult discovery,CancellationToken token,IList<string> errors=null){var result=new List<Rule>();foreach(var pack in List(errors).Where(p=>p.Enabled)){token.ThrowIfCancellationRequested();result.AddRange(Compile(pack,discovery,token));}return result;}
}
public static class RulePreview {
 // The preview uses the production matcher and scanner. It never invokes Engine.Clean.
 public static List<Finding> Scan(LoadedRulePack pack,DiscoveryResult discovery,CancellationToken token,Action<ScanProgress> progress=null){var rules=RuleCompiler.Resolve(pack,discovery,token);token.ThrowIfCancellationRequested();return new Engine(rules){LiveProgress=progress}.Scan(token,null);}
}
}
