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
 public bool IsNewer(string version,int build){long remote=SoftwareUpdates.VersionNumber(Version),local=SoftwareUpdates.VersionNumber(version);return remote>local||remote==local&&Build>build;}
}
public static class SoftwareUpdates {
 public const string Repository="CrazyHandin/Clarity",ReleasePage="https://github.com/CrazyHandin/Clarity/releases/latest";
 public const string LatestManifestUrl="https://github.com/"+Repository+"/releases/latest/download/Clarity-update.json";
 public const long MaxPackageBytes=180L*1024*1024;
 static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=2*1024*1024,RecursionLimit=32};
 public static long VersionNumber(string version){
  var beta=Regex.Match(version??"",@"\Abeta0\.([0-9]{2,5})\z");int number;
  if(beta.Success&&Int32.TryParse(beta.Groups[1].Value,out number)&&number>=1&&number<=65535)return (long)number<<16;
  // Accept the historical major.minor label as patch zero when comparing old releases.
  var stable=Regex.Match(version??"",@"\A([1-9][0-9]{0,4})\.(0|[1-9][0-9]{0,4})(?:\.(0|[1-9][0-9]{0,4}))?\z");int major,minor,patch;
  if(stable.Success&&Int32.TryParse(stable.Groups[1].Value,out major)&&Int32.TryParse(stable.Groups[2].Value,out minor)&&Int32.TryParse(stable.Groups[3].Success?stable.Groups[3].Value:"0",out patch)&&major<=65535&&minor<=65535&&patch<=65535)return ((long)major<<32)+((long)minor<<16)+patch;
  throw new InvalidDataException("不支持的软件版本编号");
 }
 public static Dictionary<string,object> Object(object value){var result=value as Dictionary<string,object>;if(result==null)throw new InvalidDataException("更新数据格式无效");return result;}
 static string Value(Dictionary<string,object> data,string key){return data.ContainsKey(key)?Convert.ToString(data[key],System.Globalization.CultureInfo.InvariantCulture):"";}
 public static string HashFile(string path){using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();}
 public static bool TrustedDownload(Uri uri){return uri!=null&&uri.Scheme=="https"&&uri.IsDefaultPort&&String.IsNullOrEmpty(uri.UserInfo)&&String.IsNullOrEmpty(uri.Fragment)&&(uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("release-assets.githubusercontent.com",StringComparison.OrdinalIgnoreCase)||uri.Host.Equals("objects.githubusercontent.com",StringComparison.OrdinalIgnoreCase));}
 public static string AssetUrl(string version,string name){VersionNumber(version);if(!Regex.IsMatch(name??"","^[A-Za-z0-9.+_-]+$"))throw new InvalidDataException("更新文件名无效");return "https://github.com/"+Repository+"/releases/download/"+version+"/"+name;}
 static void Transfer(string url,Stream target,long limit,CancellationToken token,Action<long,long> progress){
  token.ThrowIfCancellationRequested();
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;var uri=new Uri(url);
  for(int redirects=0;redirects<6;redirects++){
   if(!TrustedDownload(uri))throw new InvalidDataException("更新下载地址不受信任");var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=15000;request.ReadWriteTimeout=15000;request.UserAgent="Clarity/"+ReleaseVersion.FullVersion;request.Accept="application/octet-stream";request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);request.Headers[HttpRequestHeader.CacheControl]="no-cache";
   using(token.Register(()=>request.Abort()))using(var response=Response(request,token)){
    int code=(int)response.StatusCode;if(code==301||code==302||code==303||code==307||code==308){uri=new Uri(uri,response.Headers["Location"]);continue;}
    if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>limit)throw new IOException("更新服务器响应无效");long total=0;using(var source=response.GetResponseStream()){byte[] buffer=new byte[65536];int read;while((read=source.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();total+=read;if(total>limit)throw new InvalidDataException("更新文件超过大小限制");target.Write(buffer,0,read);if(progress!=null)progress(total,response.ContentLength);}}token.ThrowIfCancellationRequested();return;
   }
  }
  throw new IOException("更新下载重定向过多");
 }
 static HttpWebResponse Response(HttpWebRequest request,CancellationToken token){try{return (HttpWebResponse)request.GetResponse();}catch(WebException ex){token.ThrowIfCancellationRequested();using(var response=ex.Response as HttpWebResponse){if(response!=null){int code=(int)response.StatusCode;throw new IOException(UiText.T(code==403?"更新服务器拒绝访问，请稍后重试或检查网络连接（HTTP 403）。":code==429?"更新请求过于频繁，请稍后重试（HTTP 429）。":code==404?"暂未找到软件更新，请稍后重试（HTTP 404）。":"无法连接更新服务器，请稍后重试。"),ex);}}throw new IOException(UiText.T("无法连接更新服务器，请稍后重试。"),ex);}}
 static string Text(string url,long limit,CancellationToken token){using(var memory=new MemoryStream()){Transfer(url,memory,limit,token,null);return Encoding.UTF8.GetString(memory.ToArray());}}
 public static SoftwareRelease Check(CancellationToken token){
  return Check(token,Text);
 }
 public static SoftwareRelease Check(CancellationToken token,Func<string,long,CancellationToken,string> read){token.ThrowIfCancellationRequested();return ParseDownloadManifest(read(LatestManifestUrl,65536,token));}
 public static SoftwareRelease ParseDownloadManifest(string raw){
  if(raw==null||Encoding.UTF8.GetByteCount(raw)>65536)throw new InvalidDataException("更新清单过大");var data=Object(Json.DeserializeObject(raw));string tag=Value(data,"Version");VersionNumber(tag);int build=Convert.ToInt32(data["Build"]);if(Convert.ToInt32(data["Schema"])!=1||build<1||build>65535)throw new InvalidDataException("更新清单版本无效");var result=new SoftwareRelease{Version=tag,Build=build,Notes=""};string prefix="Clarity-"+tag+"-build"+build;
  if(data.ContainsKey("Highlights")){var highlights=data["Highlights"]as Dictionary<string,object>;if(highlights!=null)result.Notes=Value(highlights,UiText.English?"en":"zh-CN");}
  foreach(var value in (object[])data["Artifacts"]){var item=Object(value);string name=Value(item,"Name"),hash=Value(item,"SHA256");long size=Convert.ToInt64(item["Size"]);if(name!=prefix+"-setup.exe"&&name!=prefix+"-portable.zip")throw new InvalidDataException("更新清单包含未知软件包");if(size<=0||size>MaxPackageBytes||!Regex.IsMatch(hash,"^[0-9a-f]{64}$"))throw new InvalidDataException("更新文件校验信息无效");result.Assets.Add(new SoftwareAsset{Name=name,Url=AssetUrl(tag,name),SHA256=hash,Size=size});}
  if(result.Assets.Count!=2||result.Assets.Select(a=>a.Name).Distinct().Count()!=2)throw new InvalidDataException("更新包不完整");return result;
 }
 public static SoftwareRelease ParseManifest(string raw,string tag,IEnumerable<Dictionary<string,object>> published,string notes){
  var result=ParseDownloadManifest(raw);if(result.Version!=tag)throw new InvalidDataException("更新清单版本无效");result.Notes=notes??"";var assets=published.ToList();foreach(var item in result.Assets){var asset=assets.SingleOrDefault(a=>Value(a,"name")==item.Name);if(asset==null||Value(asset,"digest")!="sha256:"+item.SHA256||Convert.ToInt64(asset["size"])!=item.Size||Value(asset,"browser_download_url")!=item.Url)throw new InvalidDataException("更新文件与仓库记录不一致");}return result;
 }
 public static string Download(SoftwareAsset asset,string directory,CancellationToken token,Action<long,long> progress){
  string path=Path.Combine(directory,asset.Name);if(!Engine.SafeAncestors(path))throw new IOException("更新缓存路径包含链接");try{using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){Transfer(asset.Url,stream,Math.Min(MaxPackageBytes,asset.Size),token,progress);stream.Flush(true);}if(new FileInfo(path).Length!=asset.Size||HashFile(path)!=asset.SHA256)throw new InvalidDataException("下载包校验失败");return path;}catch{if(File.Exists(path)&&Engine.SafeAncestors(path))File.Delete(path);throw;}
 }
}
}
