using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace BabelManager {
 public sealed class ChannelCertificate {
  public bool Present,Valid,Trusted;
  public string Thumbprint="";
 }
 public sealed class ChannelStatus {
  public string Code="unchecked",Text="尚未检查",Message="低延迟通道尚未检查。";
  public DateTime CheckedUtc=DateTime.UtcNow;
  public bool Ready { get { return Code=="ready"; } }
  public bool Fresh(DateTime now){return now>=CheckedUtc&&now-CheckedUtc<=TimeSpan.FromSeconds(25);}
 }
 // Narrow test seam: fixtures never launch a real Mod or modify certificate stores.
 public interface IChannelRuntime {
  ChannelCertificate Certificate();
  List<int> Processes(string folder);
  List<int> Listening();
  void Start(string folder,Settings settings);
  bool Ping(string folder,ChannelCertificate certificate);
 }
 public sealed class ChannelRuntime:IChannelRuntime {
  public ChannelCertificate Certificate(){return LowLatencyChannel.ReadCertificate();}
  public List<int> Processes(string folder){return Bridge.HttpsProcesses(folder);}
  public List<int> Listening(){return Bridge.ListeningPids(Bridge.HttpsPort).Concat(Bridge.ListeningPids6(Bridge.HttpsPort)).Distinct().ToList();}
  public void Start(string folder,Settings settings){using(var process=Process.Start(LowLatencyChannel.StartInfo(folder,settings))) { }}
  public bool Ping(string folder,ChannelCertificate certificate){
   var before=Bridge.Probe(folder);if(!before.Local)return false;
   if(!LowLatencyChannel.Ping(folder,certificate))return false;
   var after=Bridge.Probe(folder);return after.Local&&after.Key==before.Key;
  }
 }
 public static class LowLatencyChannel {
  public static bool Supported(string folder){return !String.IsNullOrWhiteSpace(folder)&&File.Exists(Path.Combine(folder,"core","bridge_https.js"));}
  public static string CertificatePath(){string custom=Environment.GetEnvironmentVariable("BABEL_TOWER_CERT_PFX");return !String.IsNullOrWhiteSpace(custom)?Path.GetFullPath(custom):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BabelTower","cert","bridge.pfx");}
  public static ChannelCertificate ReadCertificate(){
   var result=new ChannelCertificate();
   try{
    string file=CertificatePath();Core.NoLinks(file);result.Present=File.Exists(file);if(!result.Present||new FileInfo(file).Length>1024*1024)return result;
    string password=Environment.GetEnvironmentVariable("BABEL_TOWER_CERT_PASS")??"btabridge";
    using(var cert=new X509Certificate2(file,password,X509KeyStorageFlags.EphemeralKeySet)){
     result.Thumbprint=cert.Thumbprint;
     DateTime now=DateTime.Now;result.Valid=cert.HasPrivateKey&&now>=cert.NotBefore&&now<cert.NotAfter;
     foreach(var location in new[]{StoreLocation.CurrentUser,StoreLocation.LocalMachine}){
      using(var store=new X509Store(StoreName.Root,location)){store.Open(OpenFlags.ReadOnly|OpenFlags.OpenExistingOnly);result.Trusted|=store.Certificates.Find(X509FindType.FindByThumbprint,cert.Thumbprint,false).Count>0;}
     }
    }
   }catch{result.Valid=false;}
   return result;
  }
  public static ProcessStartInfo StartInfo(string folder,Settings settings){
   var info=new ProcessStartInfo(Path.Combine(folder,"portable-node","node.exe"),Core.Quote(Path.Combine(folder,"core","bridge_https.js"))){WorkingDirectory=folder,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
   info.EnvironmentVariables["DEADLOCK_ROOT"]=settings.GameRoot;
   info.EnvironmentVariables["BABEL_TOWER_HTTPS_PORT"]=Bridge.HttpsPort.ToString();
   info.EnvironmentVariables["BABEL_TOWER_HTTP_HOST"]="127.0.0.1";
   info.EnvironmentVariables["BABEL_TOWER_HTTP_PORT"]=Bridge.Port(folder).ToString();
   return info;
  }
  public static ChannelStatus Status(string code){
   var s=new ChannelStatus{Code=code};
   switch(code){
    case "unsupported":s.Text="旧版不支持";s.Message="此 Mod 包没有低延迟脚本，继续使用原有桥通道。";break;
    case "missing_certificate":s.Text="缺少证书";s.Message="点击“配置证书”，在 Windows 安全警告中确认 BabelTower-localhost，完成后完全退出并重启 Steam。";break;
    case "invalid_certificate":s.Text="证书无效";s.Message="本机证书无法读取或已过期，可点击“配置证书”调用官方脚本；若仍未通过，请查看运行记录。完成后完全重启 Steam。";break;
    case "untrusted":s.Text="证书未信任";s.Message="证书文件存在，但 Windows 尚未信任。点击“配置证书”并确认安全警告，然后完全退出并重启 Steam。";break;
    case "port_conflict":s.Text="端口被占用";s.Message="8792 端口不属于所选 Mod 的低延迟进程；已保留其他进程，请先核对运行的版本。";break;
    case "offline":s.Text="未运行";s.Message="低延迟进程未运行，可用“重启桥”重新启动所选版本。";break;
    case "not_listening":s.Text="尚未就绪";s.Message="低延迟进程存在，但尚未监听 8792；主桥仍可使用原有通道。";break;
    case "probe_failed":s.Text="连接未通过";s.Message="HTTPS 证书、握手或桥响应验证未通过；请检查证书信任与所选桥。";break;
    case "changed":s.Text="需复检";s.Message="检查期间低延迟进程或端口归属发生变化，请重新检查。";break;
    case "ready":s.Text="本地可用";s.Message="本地 HTTPS 通道检查通过；游戏内连接仍需在网页设置页与游戏中确认。";break;
    case "error":s.Text="检查未完成";s.Message="无法完成低延迟通道检查；可导出报告并检查所选版本的桥日志。";break;
   }
   return s;
  }
  public static void Ensure(string folder,Settings settings,IChannelRuntime runtime,Action<string> log){
   if(!Supported(folder))return;
   try{
    var owned=runtime.Processes(folder);var listeners=runtime.Listening();
    if(listeners.Count>0&&!Bridge.PortOwned(owned,listeners)){log(Status("port_conflict").Message);return;}
    if(owned.Count>0)return;
    var cert=runtime.Certificate();
    if(!cert.Present){log(Status("missing_certificate").Message);return;}
    if(!cert.Valid){log(Status("invalid_certificate").Message);return;}
    runtime.Start(folder,settings);
    log("已请求启动低延迟通道，实际状态将单独检查。");
    if(!cert.Trusted)log(Status("untrusted").Message);
   }catch{log("低延迟通道未能启动，主桥继续使用原有通道；请在诊断页复检。");}
  }
  public static ChannelStatus Inspect(string folder,IChannelRuntime runtime){
   if(!Supported(folder))return Status("unsupported");
   try{
    var owned=runtime.Processes(folder);var listeners=runtime.Listening();
    if(listeners.Count>0&&!Bridge.PortOwned(owned,listeners))return Status("port_conflict");
    var cert=runtime.Certificate();
    if(!cert.Present)return Status("missing_certificate");
    if(!cert.Valid)return Status("invalid_certificate");
    if(!cert.Trusted)return Status("untrusted");
    if(owned.Count==0)return Status("offline");
    if(listeners.Count==0)return Status("not_listening");
    if(!runtime.Ping(folder,cert))return Status("probe_failed");
    var after=runtime.Listening();
    if(!Bridge.PortOwned(runtime.Processes(folder),after)||!listeners.OrderBy(x=>x).SequenceEqual(after.OrderBy(x=>x)))return Status("changed");
    return Status("ready");
   }catch{return Status("error");}
  }
  public static bool Ping(string folder,ChannelCertificate certificate){
   if(!certificate.Valid||!certificate.Trusted||String.IsNullOrEmpty(certificate.Thumbprint))return false;
   var request=(HttpWebRequest)WebRequest.Create("https://127.0.0.1:"+Bridge.HttpsPort+"/api/v1/health");
   request.Proxy=null;request.Timeout=2000;request.ReadWriteTimeout=2000;request.AllowAutoRedirect=false;
   // Per request: normal Windows chain/name validation AND the selected certificate.
   // Never assign ServicePointManager.ServerCertificateValidationCallback.
   request.ServerCertificateValidationCallback=(sender,cert,chain,errors)=>errors==SslPolicyErrors.None&&cert!=null&&String.Equals(cert.GetCertHashString(),certificate.Thumbprint,StringComparison.OrdinalIgnoreCase);
   try{
    using(var response=(HttpWebResponse)request.GetResponse())using(var stream=response.GetResponseStream())using(var data=new MemoryStream()){
     var buffer=new byte[4096];int read;while((read=stream.Read(buffer,0,buffer.Length))>0){if(data.Length+read>64*1024)return false;data.Write(buffer,0,read);}
     var value=Core.Json().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(data.ToArray()));object ok,name,port;
     return value!=null&&value.TryGetValue("ok",out ok)&&ok is bool&&(bool)ok&&value.TryGetValue("name",out name)&&Convert.ToString(name)=="Babel Tower Bridge"&&(!value.TryGetValue("port",out port)||Convert.ToInt32(port)==Bridge.Port(folder));
    }
   }catch{return false;}
  }
 }
}
