using System;
using System.Collections.Generic;
using System.Linq;
namespace ClearSpace {
public sealed class CleanupIssue {
 public string Project{get;set;}public string Path{get;set;}public string Status{get;set;}public string Reason{get;set;}
}
public static class CleanupIssues {
 public static List<CleanupIssue> Build(IEnumerable<Finding> selected,CleanResult result){
  var audit=result.Items.Where(a=>!String.IsNullOrEmpty(a.Path)).GroupBy(a=>a.Path,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.Last(),StringComparer.OrdinalIgnoreCase);
  var issues=new List<CleanupIssue>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var finding in selected){
   var paths=finding.Candidates.Select(s=>s.Path).Concat(finding.RegistryCandidates.Select(RegistryCleaner.Label));
   if(finding.Rule.InfoKind=="system-disk-cleanup"&&finding.VirtualItems>0)paths=paths.Concat(new[]{@"C:\"});
   if(finding.Rule.InfoKind=="disable-hibernation"&&finding.VirtualItems>0)paths=paths.Concat(new[]{Hibernation.FilePath});
   foreach(string path in paths){if(!seen.Add(path))continue;AuditItem item;audit.TryGetValue(path,out item);
    if(item!=null&&(item.Status=="已删除"||item.Status=="系统清理完成"))continue;
    issues.Add(new CleanupIssue{Project=finding.Rule.Title,Path=path,Status=item==null?"未处理":item.ErrorCode!=0?"清理失败":item.Status,Reason=item!=null&&!String.IsNullOrWhiteSpace(item.Reason)?item.Reason:result.Cancelled?"任务已取消":"清理未完成，请重新扫描后重试"});
   }
  }
  return issues;
 }
}
}
