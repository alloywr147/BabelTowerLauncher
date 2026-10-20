using System;using System.IO;using BabelManager;
// 1.2.1 适配:低延迟通道(core/bridge_https.js)必须与主桥一起起,否则启动器的用户
// 永远走位泵慢通道,1.2.1 的"网页保存 → 游戏 1 秒内生效"推送链路也完全失效。
class BridgeHttpsTests{
 static void Ok(bool yes,string s){if(!yes)throw new Exception(s);Console.WriteLine("PASS "+s);}
 static void Main(){string r=Path.Combine(Path.GetTempPath(),"BTHttps-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(r);try{
  string pkg=Path.Combine(r,"pkg");Directory.CreateDirectory(Path.Combine(pkg,"core"));Directory.CreateDirectory(Path.Combine(pkg,"portable-node"));
  File.WriteAllText(Path.Combine(pkg,"core","bridge_server.js"),"fixture");
  Ok(Bridge.StartPlan(pkg).Length==1,"start plan holds only the main bridge when the low-latency script is missing");
  File.WriteAllText(Path.Combine(pkg,"core","bridge_https.js"),"fixture");
  string[] plan=Bridge.StartPlan(pkg);
  Ok(plan.Length==2,"the low-latency channel script joins the start plan");
  Ok(plan[0].EndsWith("bridge_server.js")&&plan[1].EndsWith("bridge_https.js"),"the main bridge stays first and the low-latency channel second");
  Ok(Bridge.HttpsPort==8792,"the low-latency channel keeps port 8792");
  Ok(Bridge.SettingsUrl(pkg)=="http://127.0.0.1:8791/ui","the web settings page URL points at the bridge /ui endpoint");
  string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BabelTower","cert","bridge.pfx");
  Ok(Bridge.CertificatePresent()==File.Exists(expected),"certificate presence follows the bridge's own certificate path");
  Console.WriteLine("ALL LOW-LATENCY BRIDGE TESTS PASSED");
 }catch(Exception ex){Console.WriteLine("FAIL "+ex.Message);Environment.ExitCode=1;}finally{if(Directory.Exists(r)){string cleanup=Path.GetFullPath(r);string temporary=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!cleanup.StartsWith(temporary,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(cleanup).StartsWith("BTHttps-",StringComparison.Ordinal))throw new Exception("unsafe fixture cleanup");Directory.Delete(cleanup,true);}}}}
