using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace ClearSpace {
public sealed class SoftwareAsset {public string Name,Url,SHA256;public long Size;}
public sealed class SoftwareRelease {
 public string Version,Notes;public int Build;public List<SoftwareAsset> Assets=new List<SoftwareAsset>();
 public string Label{get{return Version+" · "+UiText.T("内部构建 ")+Build;}}
 public bool IsNewer(string version,int build){int remote=SoftwareUpdates.VersionNumber(Version),local=SoftwareUpdates.VersionNumber(version);return remote>local||remote==local&&Build>build;}
}
public static class SoftwareUpdates {
 public const string Repository="CrazyHandin/Clarity",ReleasePage="https://github.com/CrazyHandin/Clarity/releases/latest";
 public const long MaxPackageBytes=180L*1024*1024;
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=2*1024*1024,RecursionLimit=32};
 public static int VersionNumber(string version){var m=Regex.Match(version??"","^beta0\\.([0-9]{2,5})$");int value;if(!m.Success||!Int32.TryParse(m.Groups[1].Value,out value)||value<1||value>65535)throw new InvalidDataException("不支持的软件版本编号");return value;}
 public static Dictionary<string,object> Object(object value){var result=value as Dictionary<string,object>;if(result==null)throw new InvalidDataException("更新数据格式无效");return result;}
 static string Value(Dictionary<string,object> data,string key){return data.ContainsKey(key)?Convert.ToString(data[key],System.Globalization.CultureInfo.InvariantCulture):"";}
 public static string HashFile(string path){using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
 public static bool TrustedDownload(Uri uri){return uri!=null&&uri.Scheme=="https"&&uri.IsDefaultPort&&String.IsNullOrEmpty(uri.UserInfo)&&String.IsNullOrEmpty(uri.Fragment)&&(uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("release-assets.githubusercontent.com",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("objects.githubusercontent.com",StringComparison.OrdinalIgnoreCase));}
 public static string AssetUrl(string version,string name){VersionNumber(version);if(!Regex.IsMatch(name??"","^[A-Za-z0-9.+_-]+$"))throw new InvalidDataException("更新文件名无效");return "https://github.com/"+Repository+"/releases/download/"+version+"/"+name;}
 static void Transfer(string url,Stream target,long limit,CancellationToken token,Action<long,long> progress){
  token.ThrowIfCancellationRequested();
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var uri=new Uri(url);bool api=uri.Host.Equals("api.github.com",StringComparison.OrdinalIgnoreCase)&&uri.AbsolutePath.StartsWith("/repos/"+Repository+"/",StringComparison.Ordinal);
  for(int redirects=0;redirects<6;redirects++){
   if(!api&&!TrustedDownload(uri))throw new InvalidDataException("更新下载地址不受信任");var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=15000;request.UserAgent="Clarity/"+ReleaseVersion.FullVersion;request.Accept="application/vnd.github+json";
   using(token.Register(()=>request.Abort()))using(var response=(HttpWebResponse)request.GetResponse()){
    int code=(int)response.StatusCode;if(code==301||code==302||code==303||code==307||code==308){uri=new Uri(uri,response.Headers["Location"]);api=false;continue;}
    if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>limit)throw new IOException("更新服务器响应无效");long total=0;using(var source=response.GetResponseStream()){byte[] buffer=new byte[65536];int read;while((read=source.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();total+=read;if(total>limit)throw new InvalidDataException("更新文件超过大小限制");target.Write(buffer,0,read);if(progress!=null)progress(total,response.ContentLength);}}token.ThrowIfCancellationRequested();return;
   }
  }
  throw new IOException("更新下载重定向过多");
 }
 static string Text(string url,long limit,CancellationToken token){using(var memory=new MemoryStream()){Transfer(url,memory,limit,token,null);return Encoding.UTF8.GetString(memory.ToArray());}}
 public static SoftwareRelease Check(CancellationToken token){
  var release=Object(Json.DeserializeObject(Text("https://api.github.com/repos/"+Repository+"/releases/latest",2*1024*1024,token)));
  if(Convert.ToBoolean(release["draft"])||Convert.ToBoolean(release["prerelease"]))throw new InvalidDataException("更新尚未正式发布");string tag=Value(release,"tag_name");VersionNumber(tag);var assets=((object[])release["assets"]).Select(Object).ToArray();var manifest=assets.SingleOrDefault(a=>Value(a,"name")=="Clarity-update.json");
  if(manifest==null){if(VersionNumber(tag)<=VersionNumber(ReleaseVersion.Display))return new SoftwareRelease{Version=tag,Build=0};throw new InvalidDataException("新版缺少更新清单");}
  string digest=Value(manifest,"digest");if(!Regex.IsMatch(digest,"^sha256:[0-9a-f]{64}$")||Convert.ToInt64(manifest["size"])>65536)throw new InvalidDataException("更新清单校验数据无效");
  string raw=Text(AssetUrl(tag,"Clarity-update.json"),65536,token);string actual;using(var sha=SHA256.Create())actual=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-","").ToLowerInvariant();if(digest!="sha256:"+actual)throw new InvalidDataException("更新清单校验失败");return ParseManifest(raw,tag,assets,Value(release,"body"));
 }
 public static SoftwareRelease ParseManifest(string raw,string tag,IEnumerable<Dictionary<string,object>> published,string notes){
  var data=Object(Json.DeserializeObject(raw));int build=Convert.ToInt32(data["Build"]);if(Convert.ToInt32(data["Schema"])!=1||Value(data,"Version")!=tag||build<1||build>65535)throw new InvalidDataException("更新清单版本无效");VersionNumber(tag);var result=new SoftwareRelease{Version=tag,Build=build,Notes=notes??""};var assets=published.ToList();string prefix="Clarity-"+tag+"-build"+build;
  foreach(var value in (object[])data["Artifacts"]){var item=Object(value);string name=Value(item,"Name"),hash=Value(item,"SHA256");long size=Convert.ToInt64(item["Size"]);if(name!=prefix+"-setup.exe"&&name!=prefix+"-portable.zip")throw new InvalidDataException("更新清单包含未知软件包");if(size<=0||size>MaxPackageBytes||!Regex.IsMatch(hash,"^[0-9a-f]{64}$"))throw new InvalidDataException("更新文件校验信息无效");var asset=assets.SingleOrDefault(a=>Value(a,"name")==name);if(asset==null||Value(asset,"digest")!="sha256:"+hash||Convert.ToInt64(asset["size"])!=size||Value(asset,"browser_download_url")!=AssetUrl(tag,name))throw new InvalidDataException("更新文件与仓库记录不一致");result.Assets.Add(new SoftwareAsset{Name=name,Url=AssetUrl(tag,name),SHA256=hash,Size=size});}
  if(result.Assets.Count!=2||result.Assets.Select(a=>a.Name).Distinct().Count()!=2)throw new InvalidDataException("更新包不完整");return result;
 }
 public static string Download(SoftwareAsset asset,string directory,CancellationToken token,Action<long,long> progress){
  string path=Path.Combine(directory,asset.Name);if(!Engine.SafeAncestors(path))throw new IOException("更新缓存路径包含链接");try{using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Transfer(asset.Url,stream,Math.Min(MaxPackageBytes,asset.Size),token,progress);stream.Flush(true);}if(new FileInfo(path).Length!=asset.Size||HashFile(path)!=asset.SHA256)throw new InvalidDataException("下载包校验失败");return path;}catch{if(File.Exists(path)&&Engine.SafeAncestors(path))File.Delete(path);throw;}
 }
}
}
