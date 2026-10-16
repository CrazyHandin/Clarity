using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
namespace ClearSpace {
public sealed class RuleConditions {
 public string NamePattern;
 public int MinAgeDays;
 public long? MinSizeBytes,MaxSizeBytes;
}
public static class RuleConditionEvaluator {
 public static void Validate(RuleConditions c){
  if(c.MinAgeDays<0||c.MinAgeDays>3650||c.MinSizeBytes<0||c.MaxSizeBytes<0||c.MinSizeBytes>1099511627776L||c.MaxSizeBytes>1099511627776L||c.MinSizeBytes.HasValue&&c.MaxSizeBytes.HasValue&&c.MinSizeBytes>c.MaxSizeBytes)throw new InvalidDataException("文件条件的年龄或大小范围无效");
  if(c.NamePattern!=null&&(String.IsNullOrWhiteSpace(c.NamePattern)||c.NamePattern.Length>128||c.NamePattern.IndexOfAny(new[]{'\\','/',':','\0','<','>','"','|'})>=0))throw new InvalidDataException("NamePattern 只能是文件名通配符，不能包含路径");
 }
 public static bool NameMatches(RuleConditions c,string path){return c==null||String.IsNullOrEmpty(c.NamePattern)||Regex.IsMatch(Path.GetFileName(path),"^"+Regex.Escape(c.NamePattern).Replace("\\*",".*").Replace("\\?",".")+"$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));}
 public static bool SizeMatches(RuleConditions c,long bytes){return c==null||(!c.MinSizeBytes.HasValue||bytes>=c.MinSizeBytes.Value)&&(!c.MaxSizeBytes.HasValue||bytes<=c.MaxSizeBytes.Value);}
 public static string Describe(ApplicationRule rule){var c=rule.Conditions;return "保留最近 "+Math.Max(rule.KeepDays,c==null?0:c.MinAgeDays)+" 天"+(c==null?"":" · "+(String.IsNullOrEmpty(c.NamePattern)?"*":c.NamePattern)+(c.MinSizeBytes.HasValue?" · ≥ "+App.FormatSize(c.MinSizeBytes.Value):"")+(c.MaxSizeBytes.HasValue?" · ≤ "+App.FormatSize(c.MaxSizeBytes.Value):""));}
}
}
