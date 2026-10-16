using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
namespace ClearSpace {
public static class RulePackParser {
 public static LoadedRulePack Read(string text,bool allowUnsigned,string publicKey){
  if(String.IsNullOrWhiteSpace(text)||Encoding.UTF8.GetByteCount(text)>RuleStore.MaxBytes)throw new InvalidDataException("规则包为空或超过 3 MB");
  var json=new JavaScriptSerializer{MaxJsonLength=RuleStore.MaxBytes,RecursionLimit=32};
  var root=json.DeserializeObject(text) as Dictionary<string,object>;if(root==null)throw new InvalidDataException("规则包必须是 JSON 对象");
  string payload=text,trust="本地导入 · 未签名";
  if(root.ContainsKey("Payload")){
   Keys(root,new[]{"Payload","Signature"});var envelope=json.Deserialize<SignedRuleEnvelope>(text);byte[] data=Convert.FromBase64String(envelope.Payload??""),signature=Convert.FromBase64String(envelope.Signature??"");
   if(data.Length==0||data.Length>RuleStore.MaxBytes||String.IsNullOrEmpty(publicKey))throw new InvalidDataException("签名配置无效");
   using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(publicKey);if(!rsa.VerifyData(data,CryptoConfig.MapNameToOID("SHA256"),signature))throw new InvalidDataException("规则签名校验失败，当前规则保留");}
   payload=new UTF8Encoding(false,true).GetString(data);trust="发布者签名已验证";root=json.DeserializeObject(payload) as Dictionary<string,object>;
  }else if(!allowUnsigned)throw new InvalidDataException("云更新仅接受发布者签名包");
  Keys(root,new[]{"Schema","Version","Published","PackId","Name","Author","Description","Rules"});
  object rules;if(root.TryGetValue("Rules",out rules)&&rules is IEnumerable)foreach(var item in (IEnumerable)rules){var r=item as Dictionary<string,object>;Keys(r,new[]{"Id","Name","Group","Part","Description","Impact","FileNameRegex","Method","MatchNames","ExecutableNames","Publishers","ProductNames","Paths","ExcludePaths","ExcludeNames","Extensions","RequireSignature","KeepDays","Conditions"});object conditions;if(r.TryGetValue("Conditions",out conditions)&&conditions!=null)Keys(conditions as Dictionary<string,object>,new[]{"NamePattern","MinAgeDays","MinSizeBytes","MaxSizeBytes"});}
  var pack=json.Deserialize<ApplicationRulePack>(payload);RulePackValidator.Validate(pack);return new LoadedRulePack{Pack=pack,Raw=text,Trust=trust};
 }
 static void Keys(Dictionary<string,object> values,string[] allowed){if(values==null||values.Keys.Any(k=>!allowed.Contains(k,StringComparer.Ordinal)))throw new InvalidDataException("规则存在未知字段或结构无效，请检查 JSON 填写说明");}
}
}
