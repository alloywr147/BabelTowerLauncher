using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BabelManager {
 public enum UpdateSource {Mod,Launcher}
 public sealed class OfficialRelease {
  public string Tag,Name,Notes,PageUrl,DownloadUrl,Sha256;public Version Version;public long Size;public DateTime PublishedUtc;public UpdateSource Source;
 }
 public sealed class DownloadProgress {
  public long Received,Total;public double BytesPerSecond;
  public int Percent {get{return Total<=0?0:(int)Math.Min(100,Received*100/Total);}}
 }
 public static class OfficialUpdates {
  const string Repository="https://github.com/c1375rick/BabelTower";
  const string LauncherRepository="https://github.com/alloywr147/BabelTowerLauncher";
  const long MaximumPackage=512L*1024*1024;
  static object Value(Dictionary<string,object> d,string key){object value;return d.TryGetValue(key,out value)?value:null;}
  static string Text(Dictionary<string,object> d,string key){return Convert.ToString(Value(d,key));}
  static bool Flag(Dictionary<string,object> d,string key){object value=Value(d,key);return value!=null&&Convert.ToBoolean(value);}
  static Version TagVersion(string tag){var match=Regex.Match(tag??"",@"^v?(\d+)\.(\d+)(?:\.(\d+))?$");if(!match.Success)throw new Exception("官方版本号无法识别。");return new Version(Int32.Parse(match.Groups[1].Value),Int32.Parse(match.Groups[2].Value),(match.Groups[3].Success?Int32.Parse(match.Groups[3].Value):0));}
  public static OfficialRelease SelectLatest(string json){return Select(json,UpdateSource.Mod,false);}
  public static OfficialRelease SelectLauncher(string json){return Select(json,UpdateSource.Launcher,true);}
  static string Repo(UpdateSource source){if(source==UpdateSource.Mod)return Repository;if(source==UpdateSource.Launcher)return LauncherRepository;throw new Exception("未知的更新来源。");}
  static string PackageName(UpdateSource source,string tag){return (source==UpdateSource.Mod?"BabelTower-":"BabelTowerLauncher-")+tag.TrimStart('v')+"-win64.zip";}
  static OfficialRelease Select(string json,UpdateSource source,bool includePreview){
   var serializer=Core.Json();serializer.MaxJsonLength=4*1024*1024;
   var list=serializer.DeserializeObject(json) as object[];if(list==null)throw new Exception("GitHub 未返回有效的版本列表。");
   Dictionary<string,object> chosen=null;Version version=null;
   foreach(var item in list){var release=item as Dictionary<string,object>;if(release==null||Flag(release,"draft")||!includePreview&&Flag(release,"prerelease"))continue;Version candidate;try{candidate=TagVersion(Text(release,"tag_name"));}catch{continue;}if(version==null||candidate>version){chosen=release;version=candidate;}}
   if(chosen==null)throw new Exception("原作者尚未发布可用的正式版本。");
   string tag=Text(chosen,"tag_name"),name=PackageName(source,tag);
   var assets=Value(chosen,"assets") as object[];
   var matches=assets==null?new Dictionary<string,object>[0]:assets.OfType<Dictionary<string,object>>().Where(a=>Text(a,"name").Equals(name,StringComparison.OrdinalIgnoreCase)).ToArray();
   if(matches.Length!=1)throw new Exception("最新正式版没有唯一的 Windows 完整包。请查看官方发布页，或手动导入完整压缩包。");
   var asset=matches[0];var result=new OfficialRelease{Tag=tag,Version=version,Name=Text(asset,"name"),Notes=Text(chosen,"body"),PageUrl=Repo(source)+"/releases/tag/"+Uri.EscapeDataString(tag),DownloadUrl=Text(asset,"browser_download_url"),Size=Convert.ToInt64(Value(asset,"size")),Sha256=Text(asset,"digest"),Source=source};
   DateTime date;if(DateTime.TryParse(Text(chosen,"published_at"),null,System.Globalization.DateTimeStyles.RoundtripKind,out date))result.PublishedUtc=date.ToUniversalTime();
   Validate(result);return result;
  }
  public static void Validate(OfficialRelease release){
   if(release==null)throw new Exception("请先检查官方更新。");
   Version version=TagVersion(release.Tag);string expectedName=PackageName(release.Source,release.Tag);
   if(!String.Equals(release.Name,expectedName,StringComparison.OrdinalIgnoreCase)||release.Size<=0||release.Size>MaximumPackage)throw new Exception("官方安装包名称或体积异常。");
   Uri url;string path=new Uri(Repo(release.Source)).AbsolutePath+"/releases/download/"+Uri.EscapeDataString(release.Tag)+"/"+release.Name;if(!Uri.TryCreate(release.DownloadUrl,UriKind.Absolute,out url)||url.Scheme!="https"||!url.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||url.Port!=443||url.UserInfo!=""||url.Query!=""||url.Fragment!=""||url.AbsolutePath!=path)throw new Exception("下载链接不属于指定仓库的发布附件。");
   if(!Regex.IsMatch(release.Sha256??"",@"^sha256:[0-9a-fA-F]{64}$"))throw new Exception("官方附件没有可用的 SHA256 校验值，请手动下载并导入。");
  }
  public static OfficialRelease GetLatest(CancellationToken token){return Fetch(UpdateSource.Mod,token);}
  public static OfficialRelease GetLatestLauncher(CancellationToken token){return Fetch(UpdateSource.Launcher,token);}
  static OfficialRelease Fetch(UpdateSource source,CancellationToken token){
   using(var stream=OpenHttps(new Uri("https://api.github.com/repos"+new Uri(Repo(source)).AbsolutePath+"/releases?per_page=100"),token))using(var memory=new MemoryStream()){
    var bytes=new byte[16384];int count;while((count=stream.Read(bytes,0,bytes.Length))>0){token.ThrowIfCancellationRequested();memory.Write(bytes,0,count);if(memory.Length>4*1024*1024)throw new Exception("GitHub 版本列表体积异常。");}token.ThrowIfCancellationRequested();return Select(Encoding.UTF8.GetString(memory.ToArray()),source,source==UpdateSource.Launcher);
   }
  }
  public static Version CurrentVersion(string folder){
   if(String.IsNullOrWhiteSpace(folder)||!Directory.Exists(folder))return null;
   foreach(string name in new[]{"VERSION","README.md","安装使用说明.txt"}){string path=Path.Combine(folder,name);if(!File.Exists(path)||new FileInfo(path).Length>512*1024)continue;var match=Regex.Match(File.ReadAllText(path),name=="VERSION"?@"^\s*v?(\d+\.\d+\.\d+)\s*$":@"(?:版本\s*[:：]?\s*|Babel\s*Tower\s*v?)(\d+\.\d+\.\d+)",RegexOptions.IgnoreCase);if(match.Success){try{return TagVersion(match.Groups[1].Value);}catch{}}}
   var old=Regex.Match(Path.GetFileName(folder),@"^BabelTower-(\d)(\d)(\d)$",RegexOptions.IgnoreCase);return old.Success?new Version(Int32.Parse(old.Groups[1].Value),Int32.Parse(old.Groups[2].Value),Int32.Parse(old.Groups[3].Value)):null;
  }
  public static bool IsNewer(OfficialRelease release,string folder){Version current=CurrentVersion(folder);return current==null||release.Version>current;}
  public static void VerifyArchive(OfficialRelease release,string file){Validate(release);Core.NoLinks(file);if(!File.Exists(file)||new FileInfo(file).Length!=release.Size)throw new Exception("已下载的安装包缺失或体积变化，请重新下载。");using(var input=File.OpenRead(file))using(var hash=SHA256.Create()){string actual=BitConverter.ToString(hash.ComputeHash(input)).Replace("-","");if(!actual.Equals(release.Sha256.Substring(7),StringComparison.OrdinalIgnoreCase))throw new Exception("已下载的安装包校验未通过，请重新下载。");}}
  public static string Download(OfficialRelease release,string cache,CancellationToken token,Action<DownloadProgress> progress,Func<Uri,CancellationToken,Stream> open){
   Validate(release);token.ThrowIfCancellationRequested();Core.NoLinks(cache);Directory.CreateDirectory(cache);
   var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(cache)));if(drive.IsReady&&drive.AvailableFreeSpace<release.Size+64*1024*1024)throw new Exception("下载所在磁盘空间不足。");
   string job=Core.Child(cache,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(job);string part=Core.Child(job,release.Name+".part"),complete=Core.Child(job,release.Name);bool saved=false;
   try{
    using(var input=(open??OpenHttps)(new Uri(release.DownloadUrl),token))using(var output=new FileStream(part,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var hash=SHA256.Create()){
     long received=0;var clock=Stopwatch.StartNew();long last=0;var buffer=new byte[65536];int count;
     while((count=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();received+=count;if(received>release.Size)throw new Exception("下载内容超过官方标注体积。");output.Write(buffer,0,count);hash.TransformBlock(buffer,0,count,null,0);if(progress!=null&&(clock.ElapsedMilliseconds-last>=100||received==release.Size)){last=clock.ElapsedMilliseconds;progress(new DownloadProgress{Received=received,Total=release.Size,BytesPerSecond=received/Math.Max(.001,clock.Elapsed.TotalSeconds)});}}
     token.ThrowIfCancellationRequested();hash.TransformFinalBlock(new byte[0],0,0);if(received!=release.Size)throw new Exception("下载不完整，请重新下载。");string actual=BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();if(!actual.Equals(release.Sha256.Substring(7),StringComparison.OrdinalIgnoreCase))throw new Exception("SHA256 校验未通过，安装已取消。");output.Flush(true);
    }
    token.ThrowIfCancellationRequested();File.Move(part,complete);saved=true;return complete;
   }catch(WebException){token.ThrowIfCancellationRequested();throw;}catch(IOException){token.ThrowIfCancellationRequested();throw;}finally{if(!saved){try{Core.NoLinks(job);if(Core.Under(cache,job)&&File.Exists(part))File.Delete(part);if(Core.Under(cache,job)&&Directory.Exists(job)&&Directory.GetFileSystemEntries(job).Length==0)Directory.Delete(job);}catch{}}}
  }
  static Stream OpenHttps(Uri uri,CancellationToken token){
   token.ThrowIfCancellationRequested();ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
   var request=(HttpWebRequest)WebRequest.Create(uri);request.UserAgent="BabelTowerLauncher/1.4";request.Accept="application/vnd.github+json";request.Timeout=20000;request.ReadWriteTimeout=20000;request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
   CancellationTokenRegistration registration=token.Register(()=>request.Abort());
   try{var response=(HttpWebResponse)request.GetResponse();if(response.ResponseUri.Scheme!="https"){response.Dispose();throw new Exception("下载发生了不安全的 HTTP 跳转。");}return new ResponseStream(response,registration);}catch(WebException e){registration.Dispose();token.ThrowIfCancellationRequested();var response=e.Response as HttpWebResponse;if(response!=null){int status=(int)response.StatusCode;response.Dispose();if(status==403||status==429)throw new Exception("GitHub 请求受限，请稍后重试或手动导入压缩包。");throw new Exception("GitHub 请求失败（HTTP "+status+"），可稍后重试或手动导入。");}throw new Exception("无法连接 GitHub，请检查网络后重试，或手动导入压缩包。",e);}catch{registration.Dispose();throw;}
  }
  sealed class ResponseStream:Stream {
   readonly HttpWebResponse response;readonly Stream stream;CancellationTokenRegistration registration;
   public ResponseStream(HttpWebResponse response,CancellationTokenRegistration registration){this.response=response;this.registration=registration;stream=response.GetResponseStream();}
   public override bool CanRead{get{return stream.CanRead;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}public override long Length{get{throw new NotSupportedException();}}public override long Position{get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
   public override int Read(byte[] buffer,int offset,int count){return stream.Read(buffer,offset,count);}public override void Flush(){throw new NotSupportedException();}public override long Seek(long offset,SeekOrigin origin){throw new NotSupportedException();}public override void SetLength(long length){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
   protected override void Dispose(bool disposing){if(disposing){registration.Dispose();stream.Dispose();response.Dispose();}base.Dispose(disposing);}
  }
 }
}
