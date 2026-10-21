using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace BabelManager {
 public class CheckResult {public bool Local,Translation;public string Text="",Error="",Key="";}
 public static class Bridge {
  // Microsoft MIB_TCPROW_OWNER_PID: IPv4, network-byte-order port, actual listener PID.
  // https://learn.microsoft.com/windows/win32/api/tcpmib/ns-tcpmib-mib_tcprow_owner_pid
  [DllImport("iphlpapi.dll",SetLastError=true)]static extern uint GetExtendedTcpTable(IntPtr table,ref int size,bool order,int family,int tableClass,uint reserved);
  public static List<int> ListeningPids(int port){var result=new List<int>();int size=0;uint code=GetExtendedTcpTable(IntPtr.Zero,ref size,false,2,3,0);if(code!=122&&code!=0)throw new Exception("无法读取本地端口归属："+code);
   for(int attempt=0;attempt<3;attempt++){if(size<4||size>4*1024*1024)throw new Exception("本地 TCP 状态表体积异常。");int allocated=size;IntPtr buffer=Marshal.AllocHGlobal(allocated);try{code=GetExtendedTcpTable(buffer,ref size,false,2,3,0);if(code==122)continue;if(code!=0)throw new Exception("无法读取本地端口归属："+code);int count=Marshal.ReadInt32(buffer);if(count<0||count>(allocated-4)/24)throw new Exception("本地 TCP 状态表格式异常。");for(int i=0;i<count;i++){IntPtr row=IntPtr.Add(buffer,4+i*24);uint address=unchecked((uint)Marshal.ReadInt32(row,4));int localPort=(Marshal.ReadByte(row,8)<<8)|Marshal.ReadByte(row,9);if(localPort==port&&(address==0||address==0x0100007F))result.Add(Marshal.ReadInt32(row,20));}return result.Distinct().ToList();}finally{Marshal.FreeHGlobal(buffer);}}
   throw new Exception("本地端口归属正在变化，请重试。");}
  // MIB_TCP6ROW_OWNER_PID: local address[16], scope, port, remote[16],
  // remote scope/port, state, PID. The HTTPS channel binds IPv4 and IPv6.
  public static List<int> ListeningPids6(int port){
   var result=new List<int>();if(!Socket.OSSupportsIPv6)return result;
   int size=0;uint code=GetExtendedTcpTable(IntPtr.Zero,ref size,false,23,3,0);
   if(code!=122&&code!=0)throw new Exception("无法读取 IPv6 本地端口归属："+code);
   for(int attempt=0;attempt<3;attempt++){
    if(size<4||size>4*1024*1024)throw new Exception("IPv6 TCP 状态表体积异常。");
    int allocated=size;IntPtr buffer=Marshal.AllocHGlobal(allocated);
    try{
     code=GetExtendedTcpTable(buffer,ref size,false,23,3,0);if(code==122)continue;
     if(code!=0)throw new Exception("无法读取 IPv6 本地端口归属："+code);
     int count=Marshal.ReadInt32(buffer);if(count<0||count>(allocated-4)/56)throw new Exception("IPv6 TCP 状态表格式异常。");
     for(int i=0;i<count;i++){
      IntPtr row=IntPtr.Add(buffer,4+i*56);bool wildcard=true,loopback=true;
      for(int b=0;b<16;b++){byte value=Marshal.ReadByte(row,b);wildcard&=value==0;loopback&=value==(b==15?1:0);}
      int localPort=(Marshal.ReadByte(row,20)<<8)|Marshal.ReadByte(row,21);
      if(localPort==port&&(wildcard||loopback))result.Add(Marshal.ReadInt32(row,52));
     }
     return result.Distinct().ToList();
    }finally{Marshal.FreeHGlobal(buffer);}
   }
   throw new Exception("IPv6 本地端口归属正在变化，请重试。");
  }
  public static bool PortOwned(IEnumerable<int> selected,IEnumerable<int> listeners){var owners=listeners.Distinct().ToArray();return owners.Length==1&&selected.Contains(owners[0]);}
  public static bool GameRunning(){return Process.GetProcessesByName("deadlock").Length>0;}
  // 1.2.1 起:低延迟通道(core/bridge_https.js,8792)必须与主桥一起起 —— 只起 bridge_server.js
  // 会让启动器的用户永远走位泵慢通道(译文 6.6s、配置读 7.8~26s),而且 1.2.1 的
  // "网页保存 → 游戏 1 秒内生效"推送链路(依赖 8792 的 https 面板通道)完全失效。
  public const int HttpsPort=8792;
  // 起桥脚本清单:主桥必起;低延迟通道脚本存在才起(旧版本包里没有这个文件)。
  public static string[] StartPlan(string folder){var list=new List<string>();list.Add(Path.Combine(folder,"core","bridge_server.js"));string https=Path.Combine(folder,"core","bridge_https.js");if(File.Exists(https))list.Add(https);return list.ToArray();}
  // 证书是低延迟通道的唯一拦点:core/bridge_https.js 缺证书会以退出码 3 结束。
  public static bool CertificatePresent(){string root=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);return File.Exists(LowLatencyChannel.CertificatePath());}
  // 1.2.1 的网页设置页:游戏内 /tr 就是打开它(桥 8791 提供 core/web/ui.html)。
  public static string SettingsUrl(string folder){return "http://127.0.0.1:"+Port(folder)+"/ui";}
  public static void OpenSettingsPage(string folder){if(String.IsNullOrWhiteSpace(folder))throw new Exception("尚未选择版本目录。");try{Process.Start(new ProcessStartInfo(SettingsUrl(folder)){UseShellExecute=true});}catch(Exception e){throw new Exception("无法打开网页设置页："+e.Message);}}
  public static int Port(string p){string cfg=Path.Combine(p,"config","config.json");if(!File.Exists(cfg))cfg=Path.Combine(p,"config","config.example.json");if(!File.Exists(cfg))return 8791;object value;var d=Core.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(cfg));int port=d.TryGetValue("port",out value)?Convert.ToInt32(value):8791;if(port<1||port>65535)throw new Exception("桥配置端口无效。");return port;}
  static List<int> ProcessesFor(string folder,string name){var ids=new List<int>();string exe=Path.Combine(folder,"portable-node","node.exe");string script=Path.Combine(folder,"core",name);using(var query=new ManagementObjectSearcher(new ManagementScope(),new ObjectQuery("SELECT ProcessId,ExecutablePath,CommandLine FROM Win32_Process WHERE Name='node.exe'"),new EnumerationOptions{Timeout=TimeSpan.FromSeconds(3)})){foreach(ManagementObject m in query.Get()){string path=m["ExecutablePath"] as string,cmd=m["CommandLine"] as string;if(path!=null&&path.Equals(exe,StringComparison.OrdinalIgnoreCase)&&cmd!=null&&(cmd.IndexOf(script,StringComparison.OrdinalIgnoreCase)>=0||Regex.IsMatch(cmd,@"(?:^|\s|"")core[\\/]"+Regex.Escape(name)+@"(?:\s|""|$)",RegexOptions.IgnoreCase)))ids.Add(Convert.ToInt32(m["ProcessId"]));}}return ids;}
  public static List<int> Processes(string folder){return ProcessesFor(folder,"bridge_server.js");}
  // 低延迟通道(core/bridge_https.js)进程:与主桥分开识别,停止时不能漏。
  public static List<int> HttpsProcesses(string folder){return ProcessesFor(folder,"bridge_https.js");}
  public static void Stop(string folder){if(String.IsNullOrWhiteSpace(folder))return;var ids=new List<int>();ids.AddRange(Processes(folder));ids.AddRange(HttpsProcesses(folder));foreach(int id in ids.Distinct()){try{using(var p=Process.GetProcessById(id)){p.Kill();if(!p.WaitForExit(5000))throw new Exception("桥进程未退出。");}}catch(ArgumentException){}}}
  public static Dictionary<string,object> Request(int port,string endpoint,bool post){var req=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:"+port+endpoint);req.Proxy=null;req.Timeout=post?45000:2000;req.ReadWriteTimeout=post?45000:2000;req.Method=post?"POST":"GET";if(post){req.ContentType="application/json";byte[] body=Encoding.UTF8.GetBytes(Core.Json().Serialize(new{text="Please translate this connection check sentence into Chinese. Reference "+Guid.NewGuid().ToString("N"),sourceLanguage="en",targetLanguage="zh-Hans"}));req.ContentLength=body.Length;using(var st=req.GetRequestStream())st.Write(body,0,body.Length);}try{using(var response=req.GetResponse())using(var sr=new StreamReader(response.GetResponseStream(),Encoding.UTF8))return Core.Json().Deserialize<Dictionary<string,object>>(sr.ReadToEnd());}catch(WebException ex){if(ex.Response!=null){using(var sr=new StreamReader(ex.Response.GetResponseStream())){string body=sr.ReadToEnd();try{var d=Core.Json().Deserialize<Dictionary<string,object>>(body);object error;if(d.TryGetValue("error",out error))throw new Exception(Convert.ToString(error));}catch(ArgumentException){}throw new Exception("桥接口返回错误："+body.Substring(0,Math.Min(body.Length,400)));}}throw new Exception("本地桥未响应："+ex.Message);}}
  public static bool TranslationValid(string input,string output){return !String.IsNullOrWhiteSpace(output)&&!String.Equals(input.Trim(),output.Trim(),StringComparison.OrdinalIgnoreCase)&&Regex.IsMatch(output,@"[\u3400-\u4DBF\u4E00-\u9FFF]");}
  public static ProbeResult Probe(string folder){var r=new ProbeResult();try{if(!Core.IsPackage(folder))throw new Exception("尚未选择完整版本。");int port=Port(folder);var ids=Processes(folder);var listeners=ListeningPids(port);if(!PortOwned(ids,listeners))throw new Exception("配置端口未由所选版本的桥监听，可能被其他服务占用。");var h=Request(port,"/api/v1/health",false);object ok,name;if(!h.TryGetValue("ok",out ok)||!Convert.ToBoolean(ok)||!h.TryGetValue("name",out name)||Convert.ToString(name)!="Babel Tower Bridge")throw new Exception("本地服务身份不匹配。");var after=ListeningPids(port);if(!PortOwned(Processes(folder),after)||!listeners.SequenceEqual(after))throw new Exception("健康查询期间桥已变化，请重试。");r.Local=true;r.Key=String.Join(",",after);}catch(Exception e){r.Error=e.Message;}return r;}
  public static void Start(string folder,Settings s,Action<string> log){if(!Core.IsPackage(folder))throw new Exception("请选择或安装完整的巴别塔版本。");int port=Port(folder);
   if(Processes(folder).Count>0){string cfg=Path.Combine(folder,"config","config.json");var values=Core.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(cfg));object value;string current=values.TryGetValue("gameLogPath",out value)?Convert.ToString(value):"";string expected=Path.Combine(s.GameRoot,"game","citadel","console.log");if(String.IsNullOrWhiteSpace(current)||!Core.Full(current).Equals(Core.Full(expected),StringComparison.OrdinalIgnoreCase))throw new Exception("现有桥的游戏日志路径与所选目录不同，请点击“重启桥并复检”。");if(values.TryGetValue("gameLogTail",out value)&&!Convert.ToBoolean(value))throw new Exception("桥配置关闭了游戏日志读取，无法建立游戏上行连接。");log("当前版本的桥已运行，开始检查。");LowLatencyChannel.Ensure(folder,s,new ChannelRuntime(),log);return;}
   using(var tcp=new TcpClient()){try{var a=tcp.BeginConnect("127.0.0.1",port,null,null);if(a.AsyncWaitHandle.WaitOne(700)&&tcp.Connected)throw new InvalidOperationException("端口 "+port+" 已被其他服务占用；不会结束其他版本的桥。");}catch(SocketException){}}
   Core.WriteGameLog(folder,s);
   var info=new ProcessStartInfo(Path.Combine(folder,"portable-node","node.exe"),Core.Quote(Path.Combine(folder,"core","bridge_server.js"))+" --no-watch"){WorkingDirectory=folder,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};info.EnvironmentVariables["DEADLOCK_ROOT"]=s.GameRoot;using(var process=Process.Start(info)) { }
   LowLatencyChannel.Ensure(folder,s,new ChannelRuntime(),log);
   log("桥已在后台启动，等待本地服务。");}
  public static CheckResult Check(string folder,Action<string> log,Action<bool,string> local=null){var r=new CheckResult();try{ProbeResult p=null;var clock=Stopwatch.StartNew();do{p=Probe(folder);if(p.Local)break;Thread.Sleep(400);}while(clock.Elapsed<TimeSpan.FromSeconds(14));if(!p.Local)throw new Exception(p.Error);r.Local=true;r.Key=p.Key;log("✓ 本地桥服务正常");if(local!=null)local(true,"");var test=Request(Port(folder),"/api/v1/test",true);object ok,translated;if(!test.TryGetValue("ok",out ok)||!Convert.ToBoolean(ok))throw new Exception(test.ContainsKey("error")?Convert.ToString(test["error"]):"翻译服务未返回成功。");if(!test.TryGetValue("translation",out translated)||!TranslationValid("",Convert.ToString(translated)))throw new Exception("在线测试未返回有效中文译文，不能确认翻译服务正常。");var after=Probe(folder);if(!after.Local||after.Key!=r.Key){r.Local=false;throw new Exception("测试期间桥已离线或重启，请重新检查。");}r.Translation=true;r.Text=Convert.ToString(translated);log("✓ 随机长句在线翻译测试通过（不使用 hello 词典命中）。");}catch(Exception e){r.Error=e.Message;log("检查失败："+e.Message);if(!r.Local&&local!=null)local(false,e.Message);}return r;}
  public static string Steam(){foreach(string path in new[]{@"Software\Valve\Steam",@"SOFTWARE\WOW6432Node\Valve\Steam"}){foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine}){using(var k=hive.OpenSubKey(path)){if(k==null)continue;string exe=k.GetValue("SteamExe") as string;if(!String.IsNullOrEmpty(exe)&&File.Exists(exe))return exe;string dir=k.GetValue("InstallPath") as string??k.GetValue("SteamPath") as string;if(!String.IsNullOrEmpty(dir)&&File.Exists(Path.Combine(dir,"steam.exe")))return Path.Combine(dir,"steam.exe");}}}return null;}
  public static void LaunchGame(string steam){if(GameRunning())throw new Exception("游戏已运行。请完全退出游戏后从管理器重新启动，才能带上连接所需的日志参数。");if(String.IsNullOrEmpty(steam)||!File.Exists(steam))throw new Exception("找不到 Steam.exe，请在弹出的窗口选择它。");Process.Start(new ProcessStartInfo(steam,"-applaunch 1422450 -console -condebug"){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Hidden});}
 }
}
