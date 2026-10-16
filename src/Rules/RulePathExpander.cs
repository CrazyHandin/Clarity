using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace ClearSpace {
public static class RulePathExpander {
 public static string[] Expand(string root,string pattern,CancellationToken token){
  const int limit=256;var current=new List<string>{root};foreach(string segment in pattern.Split('\\')){token.ThrowIfCancellationRequested();var next=new List<string>();foreach(string parent in current){token.ThrowIfCancellationRequested();if(ScanPolicy.Excluded(parent)||!Engine.SafeAncestors(Path.Combine(parent,"probe")))continue;
   try{if(segment.IndexOfAny(new[]{'*','?'})>=0){foreach(string child in Directory.EnumerateDirectories(parent,segment).Take(limit)){token.ThrowIfCancellationRequested();if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0&&!ScanPolicy.Excluded(child))next.Add(child);if(next.Count>=limit)break;}}else{string child=Path.Combine(parent,segment);if(Directory.Exists(child)&&!ScanPolicy.Excluded(child))next.Add(child);}}catch(OperationCanceledException){throw;}catch(IOException){}catch(UnauthorizedAccessException){}
   if(next.Count>=limit)break;
  }current=next.Distinct(StringComparer.OrdinalIgnoreCase).Take(limit).ToList();if(current.Count==0)break;}return current.ToArray();
 }
}
}
