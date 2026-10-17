using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace ClearSpace {
public sealed class RulePackImportItem {public string FileName,Id,Raw,Error,Action;public LoadedRulePack Loaded;public bool Ready{get{return Error==null&&Action!="跳过";}}}
public sealed class RulePackImportResult {public int Added,Updated,Skipped,Failed;public bool Cancelled;public string LastId;public readonly List<string> Lines=new List<string>();public string Summary{get{return "新增 "+Added+" · 更新 "+Updated+" · 跳过 "+Skipped+" · 失败 "+Failed+(Cancelled?" · 已取消":"");}}}
public sealed class RulePackBatch {
 public readonly List<RulePackImportItem> Items=new List<RulePackImportItem>();
 static string ReadFile(string path){using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(file.Length>RuleStore.MaxBytes)throw new InvalidDataException("规则包超过 3 MB");using(var reader=new StreamReader(file))return reader.ReadToEnd();}}
 public static RulePackBatch Prepare(RulePackCatalog store,IEnumerable<string> files,CancellationToken token=default(CancellationToken)){
  var paths=files.Take(RulePackCatalog.MaxPacks+1).ToArray();if(paths.Length>RulePackCatalog.MaxPacks)throw new InvalidDataException("一次最多选择 20 个 JSON 文件");var batch=new RulePackBatch();var existing=store.List().ToDictionary(p=>p.Id,StringComparer.OrdinalIgnoreCase);
  foreach(string path in paths){token.ThrowIfCancellationRequested();var item=new RulePackImportItem{FileName=Path.GetFileName(path)};batch.Items.Add(item);try{item.Raw=ReadFile(path);item.Loaded=store.Inspect(item.Raw,item.FileName);item.Id=RulePackCatalog.Identify(item.Loaded,item.FileName);InstalledRulePack old;if(existing.TryGetValue(item.Id,out old)){int compare=new Version(item.Loaded.Pack.Version).CompareTo(new Version(old.Loaded.Pack.Version));if(old.Loaded.Pack.Schema!=item.Loaded.Pack.Schema)throw new InvalidDataException("该标识已被另一种格式的规则占用");if(compare<0)throw new InvalidDataException("版本较旧，请使用回退按钮");if(compare==0){if(item.Raw!=old.Loaded.Raw)throw new InvalidDataException("内容已变化，请提高 Version 后再导入");item.Action="跳过";}else item.Action="更新";}else item.Action="新增";}catch(Exception ex){item.Error=ex.Message;}}
  foreach(var group in batch.Items.Where(i=>i.Id!=null).GroupBy(i=>i.Id,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1))foreach(var item in group)item.Error="包标识重复，请每个规则包只选择一份文件";
  return batch;
 }
 public string Describe(){return String.Join("\n",Items.Select(i=>i.FileName+" · "+(i.Error??i.Action+" · "+i.Loaded.Pack.Version+" · "+i.Loaded.Trust)));}
 public RulePackImportResult Apply(RulePackCatalog store,CancellationToken token=default(CancellationToken)){var result=new RulePackImportResult();foreach(var item in Items){if(token.IsCancellationRequested){result.Cancelled=true;result.Skipped++;result.Lines.Add(item.FileName+" · 已取消，未导入");continue;}if(item.Error!=null){result.Failed++;result.Lines.Add(item.FileName+" · 失败 · "+item.Error);continue;}if(!item.Ready){result.Skipped++;result.Lines.Add(item.FileName+" · 已存在，跳过");continue;}try{var entry=store.Import(item.Raw,item.FileName);result.LastId=entry.Id;if(item.Action=="更新")result.Updated++;else result.Added++;result.Lines.Add(item.FileName+" · "+item.Action+"成功");}catch(Exception ex){result.Failed++;result.Lines.Add(item.FileName+" · 失败 · "+ex.Message);}}return result;}
}
}
