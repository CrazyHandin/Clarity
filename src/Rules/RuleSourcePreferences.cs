using System;
namespace ClearSpace {
public static class RuleSourcePreferences {
 public static void SaveWinapp2(CleanerSettings settings,bool enabled,bool automatic,Action persist){bool? oldEnabled=settings.EnableWinapp2,oldAuto=settings.AutomaticWinapp2;try{settings.EnableWinapp2=enabled;settings.AutomaticWinapp2=automatic;persist();}catch{settings.EnableWinapp2=oldEnabled;settings.AutomaticWinapp2=oldAuto;throw;}}
 public static void SaveJson(CleanerSettings settings,bool enabled,bool automatic,string url,Action persist){RuleSourceAddresses.Validate(url);if(enabled&&automatic&&String.IsNullOrEmpty(url))throw new System.IO.InvalidDataException("自动检查需先填写发布地址");bool? oldEnabled=settings.EnableJson,oldAuto=settings.AutomaticJson;string oldUrl=settings.UpdateUrl;try{settings.EnableJson=enabled;settings.AutomaticJson=automatic;settings.UpdateUrl=url;persist();}catch{settings.EnableJson=oldEnabled;settings.AutomaticJson=oldAuto;settings.UpdateUrl=oldUrl;throw;}}
 public static void Save(CleanerSettings settings,bool iniEnabled,bool jsonEnabled,bool iniAutomatic,bool jsonAutomatic,string jsonUrl,bool editorIsWinapp2,Action persist){
  RuleSourceAddresses.Validate(jsonUrl);if(jsonEnabled&&jsonAutomatic&&String.IsNullOrEmpty(jsonUrl))throw new System.IO.InvalidDataException("自动检查需先填写发布地址");
  bool? oldIni=settings.EnableWinapp2,oldJson=settings.EnableJson,oldIniAuto=settings.AutomaticWinapp2,oldJsonAuto=settings.AutomaticJson;bool oldEditor=settings.UseWinapp2,oldAuto=settings.AutomaticUpdates;string oldUrl=settings.UpdateUrl;
  try{settings.EnableWinapp2=iniEnabled;settings.EnableJson=jsonEnabled;settings.AutomaticWinapp2=iniAutomatic;settings.AutomaticJson=jsonAutomatic;settings.UseWinapp2=editorIsWinapp2;settings.UpdateUrl=jsonUrl;settings.AutomaticUpdates=iniAutomatic||jsonAutomatic;persist();}
  catch{settings.EnableWinapp2=oldIni;settings.EnableJson=oldJson;settings.AutomaticWinapp2=oldIniAuto;settings.AutomaticJson=oldJsonAuto;settings.UseWinapp2=oldEditor;settings.UpdateUrl=oldUrl;settings.AutomaticUpdates=oldAuto;throw;}
 }
}
}
