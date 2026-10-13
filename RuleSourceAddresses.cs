using System;

namespace ClearSpace {
public static class RuleSourceAddresses {
 public static string Json(string address){string text=(address??"").Trim();return text.Equals(Winapp2Store.Url,StringComparison.OrdinalIgnoreCase)?"":text;}
 public static void Validate(string address){Uri parsed;if(address.Length>0&&(!Uri.TryCreate(address,UriKind.Absolute,out parsed)||parsed.Scheme!="https"||!String.IsNullOrEmpty(parsed.UserInfo)))throw new System.IO.InvalidDataException("请输入 HTTPS 规则地址");}
}
}
