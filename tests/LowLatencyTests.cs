using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using BabelManager;
class LowLatencyTests {
 static int passed;
 static void Ok(bool yes,string message){if(!yes)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
 sealed class Fake:IChannelRuntime {
  public List<int> Owned=new List<int>(),Listeners=new List<int>();
  public ChannelCertificate Cert=new ChannelCertificate{Present=true,Valid=true,Trusted=true,Thumbprint="fixture"};
  public bool PingOk=true,ChangeOwner;public int Started,Pings;
  public ChannelCertificate Certificate(){return Cert;}
  public List<int> Processes(string folder){return Owned.ToList();}
  public List<int> Listening(){if(ChangeOwner&&Pings>0)return new List<int>{999};return Listeners.ToList();}
  public void Start(string folder,Settings settings){Started++;Owned.Add(21);Listeners.Add(21);}
  public bool Ping(string folder,ChannelCertificate certificate){Pings++;return PingOk;}
 }
 static void Main(){string root=Path.Combine(Path.GetTempPath(),"BT17-channel-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"core"));try{
  var f=new Fake();var s=new Settings();var logs=new List<string>();
  LowLatencyChannel.Ensure(root,s,f,logs.Add);Ok(f.Started==0&&LowLatencyChannel.Inspect(root,f).Code=="unsupported","legacy package never starts or requires HTTPS channel");
  File.WriteAllText(Path.Combine(root,"core","bridge_https.js"),"fixture");
  LowLatencyChannel.Ensure(root,s,f,logs.Add);Ok(f.Started==1,"missing sidecar is started independently of an already healthy main bridge");
  LowLatencyChannel.Ensure(root,s,f,logs.Add);Ok(f.Started==1,"repeated start preserves existing selected sidecar");
  Ok(LowLatencyChannel.Inspect(root,f).Ready,"owned listener with trusted certificate and valid HTTPS identity becomes locally ready");
  f=new Fake{Owned=new List<int>{21},Listeners=new List<int>{99}};LowLatencyChannel.Ensure(root,s,f,logs.Add);Ok(f.Started==0&&LowLatencyChannel.Inspect(root,f).Code=="port_conflict","foreign port owner is neither replaced nor treated as ready");
  f=new Fake{Cert=new ChannelCertificate()};LowLatencyChannel.Ensure(root,s,f,logs.Add);Ok(f.Started==0&&LowLatencyChannel.Inspect(root,f).Code=="missing_certificate","missing certificate degrades gracefully without a failed child process");
  f=new Fake{Cert=new ChannelCertificate{Present=true,Valid=false,Trusted=true}};Ok(!LowLatencyChannel.Inspect(root,f).Ready,"unreadable or expired certificate cannot become ready");
  f=new Fake{Owned=new List<int>{21},Listeners=new List<int>{21},Cert=new ChannelCertificate{Present=true,Valid=true,Trusted=false}};Ok(LowLatencyChannel.Inspect(root,f).Code=="untrusted"&&f.Pings==0,"certificate existence alone never bypasses Windows trust");
  f=new Fake{Owned=new List<int>{21},Listeners=new List<int>{21},PingOk=false};Ok(LowLatencyChannel.Inspect(root,f).Code=="probe_failed","TLS or upstream identity failure cannot show ready");
  f=new Fake{Owned=new List<int>{21}};Ok(LowLatencyChannel.Inspect(root,f).Code=="not_listening","a running process without a listener is not a working channel");
  f=new Fake{Owned=new List<int>{21},Listeners=new List<int>{21},ChangeOwner=true};Ok(LowLatencyChannel.Inspect(root,f).Code=="changed","process ownership is rechecked after HTTPS response");
  var connectivity=new Connectivity();connectivity.ChangeContext("first");long revision=connectivity.Revision;Ok(connectivity.ApplyChannel(LowLatencyChannel.Status("ready"),revision,"first"),"matching channel result is accepted");connectivity.BeginAction();Ok(connectivity.Channel==null&&!connectivity.ApplyChannel(LowLatencyChannel.Status("ready"),revision,"first"),"starting an action rejects old channel evidence");revision=connectivity.Revision;connectivity.ChangeContext("second");Ok(!connectivity.ApplyChannel(LowLatencyChannel.Status("ready"),revision,"first"),"changing package rejects stale asynchronous channel result");var stale=LowLatencyChannel.Status("ready");stale.CheckedUtc=DateTime.UtcNow.AddSeconds(-30);Ok(!stale.Fresh(DateTime.UtcNow),"channel readiness expires without a fresh check");
  if(System.Net.Sockets.Socket.OSSupportsIPv6){var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.IPv6Loopback,0);listener.Server.DualMode=false;listener.Start();try{int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;Ok(Bridge.ListeningPids6(port).Contains(System.Diagnostics.Process.GetCurrentProcess().Id),"IPv6 loopback listener ownership is identified without touching real bridge ports");}finally{listener.Stop();}}
  connectivity.Channel=LowLatencyChannel.Status("ready");var snapshot=connectivity.Snapshot();connectivity.BeginAction();Ok(snapshot.Channel!=null&&snapshot.Channel.Ready&&connectivity.Channel==null,"read-only report snapshot survives operation invalidation");
  var callback=System.Net.ServicePointManager.ServerCertificateValidationCallback;var cert=LowLatencyChannel.ReadCertificate();Ok(System.Net.ServicePointManager.ServerCertificateValidationCallback==callback,"certificate inspection never installs a global TLS bypass");
  var command=LowLatencyChannel.StartInfo(root,new Settings{GameRoot=root});Ok(command.EnvironmentVariables["BABEL_TOWER_HTTP_PORT"]=="8791"&&command.EnvironmentVariables["BABEL_TOWER_HTTPS_PORT"]=="8792","sidecar receives explicit loopback/upstream ports");
  Ok(command.CreateNoWindow&&!command.UseShellExecute&&command.Arguments.Contains("bridge_https.js")&&!command.Arguments.Contains("--no-watch"),"sidecar runs hidden and never receives main-only arguments");
  File.WriteAllText(Path.Combine(root,"core","bridge_server.js"),"fixture");Ok(Bridge.StartPlan(root).Length==2&&Bridge.StartPlan(root)[0].EndsWith("bridge_server.js"),"contributor launch plan keeps main bridge first");
  Ok(Bridge.SettingsUrl(root)=="http://127.0.0.1:8791/ui","web settings use selected bridge HTTP endpoint");
  Console.WriteLine("LOW LATENCY RESULT "+passed+" passed; no real bridge, game, browser or certificate store writes");
 }catch(Exception e){Console.WriteLine("FAIL "+e);Environment.ExitCode=1;}finally{if(Path.GetFileName(root).StartsWith("BT17-channel-")&&Path.GetFullPath(root).StartsWith(Path.GetFullPath(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase))Directory.Delete(root,true);}}
}
