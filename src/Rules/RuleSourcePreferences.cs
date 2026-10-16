using System;
namespace ClearSpace {
public static class RuleSourcePreferences {
 public static void Save(CleanerSettings settings,bool iniEnabled,bool jsonEnabled,bool iniAutomatic,bool jsonAutomatic,string jsonUrl,bool editorIsWinapp2,Action persist){
  RuleSourceAddresses.Validate(jsonUrl);if(jsonEnabled&&jsonAutomatic&&String.IsNullOrEmpty(jsonUrl))throw new System.IO.InvalidDataException("自动检查需先填写发布地址");
  bool? oldIni=settings.EnableWinapp2,oldJson=settings.EnableJson,oldIniAuto=settings.AutomaticWinapp2,oldJsonAuto=settings.AutomaticJson;bool oldEditor=settings.UseWinapp2,oldAuto=settings.AutomaticUpdates;string oldUrl=settings.UpdateUrl;
  try{settings.EnableWinapp2=iniEnabled;settings.EnableJson=jsonEnabled;settings.AutomaticWinapp2=iniAutomatic;settings.AutomaticJson=jsonAutomatic;settings.UseWinapp2=editorIsWinapp2;settings.UpdateUrl=jsonUrl;settings.AutomaticUpdates=iniAutomatic||jsonAutomatic;persist();}
  catch{settings.EnableWinapp2=oldIni;settings.EnableJson=oldJson;settings.AutomaticWinapp2=oldIniAuto;settings.AutomaticJson=oldJsonAuto;settings.UseWinapp2=oldEditor;settings.UpdateUrl=oldUrl;settings.AutomaticUpdates=oldAuto;throw;}
 }
}
}
