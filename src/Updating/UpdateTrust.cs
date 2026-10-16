using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
namespace ClearSpace {
public static class UpdateTrust {
 public static string PublicKey {get{using(var stream=typeof(UpdateTrust).Assembly.GetManifestResourceStream("ClearSpace.update-public-key.xml"))using(var reader=new StreamReader(stream))return reader.ReadToEnd();}}
 static bool Same(object a,object b){
  var left=a as Dictionary<string,object>;var right=b as Dictionary<string,object>;
  if(left!=null||right!=null){if(left==null||right==null||left.Count!=right.Count)return false;foreach(var item in left){object value;if(!right.TryGetValue(item.Key,out value)||!Same(item.Value,value))return false;}return true;}
  var x=a as IList;var y=b as IList;if(x!=null||y!=null){if(x==null||y==null||x.Count!=y.Count)return false;for(int i=0;i<x.Count;i++)if(!Same(x[i],y[i]))return false;return true;}return Object.Equals(a,b);
 }
 public static string Verify(string raw,string publicKey=null){
  if(raw==null||Encoding.UTF8.GetByteCount(raw)>65536)throw new InvalidDataException("更新签名清单过大");
  var json=new JavaScriptSerializer{MaxJsonLength=65536,RecursionLimit=32};var envelope=json.Deserialize<Dictionary<string,object>>(raw);
  if(envelope==null||!envelope.ContainsKey("SignedPayload")||!envelope.ContainsKey("Signature"))throw new InvalidDataException("软件更新缺少发布者签名");
  byte[] data=Convert.FromBase64String(Convert.ToString(envelope["SignedPayload"])),signature=Convert.FromBase64String(Convert.ToString(envelope["Signature"]));
  if(data.Length>32768||signature.Length!=384)throw new InvalidDataException("软件更新签名无效");
  using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(publicKey??PublicKey);if(rsa.KeySize!=3072||!rsa.VerifyData(data,CryptoConfig.MapNameToOID("SHA256"),signature))throw new InvalidDataException("软件更新发布者签名校验失败");}
  string payload=new UTF8Encoding(false,true).GetString(data);var signed=json.Deserialize<Dictionary<string,object>>(payload);envelope.Remove("SignedPayload");envelope.Remove("Signature");if(!Same(envelope,signed))throw new InvalidDataException("更新清单与签名内容不一致");return payload;
 }
}
}
