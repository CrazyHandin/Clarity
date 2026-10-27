using System;
using System.Collections.Generic;
namespace ClearSpace {
internal static class ScanExplanation {
 internal static string Retained(Finding finding){
  var reasons=new List<string>();
  if(finding.RunningBytes>0)reasons.Add(UiText.T("软件使用中：")+App.FormatSize(finding.RunningBytes));
  if(finding.RecentBytes>0)reasons.Add(UiText.T("保留近期文件：")+App.FormatSize(finding.RecentBytes));
  if(finding.FilteredBytes>0)reasons.Add(UiText.T("资料与安全过滤：")+App.FormatSize(finding.FilteredBytes));
  if(finding.Unreadable>0)reasons.Add(UiText.T("无法读取：")+finding.Unreadable+UiText.T(" 项"));
  if(finding.Links>0)reasons.Add(UiText.T("链接、只读或云端文件：")+finding.Links+UiText.T(" 项"));
  if(finding.Changed>0)reasons.Add(UiText.T("扫描期间已变化：")+finding.Changed+UiText.T(" 项"));
  return reasons.Count==0?"":"\n"+UiText.T("保留原因：")+String.Join(" · ",reasons);
 }
 internal static string Coverage(CacheSearchReport report){return report==null?"":UiText.T("AppData 扩展识别：")+report.Directories+UiText.T(" 个目录")+" · "+report.Caches+UiText.T(" 处额外缓存")+(report.Limited?UiText.T(" · 已达识别上限，部分目录未完成"):report.Unreadable>0?" · "+report.Unreadable+UiText.T(" 处无法读取"):"");}
}
}
