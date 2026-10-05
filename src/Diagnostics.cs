using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BabelManager {
 public class InstallationResult {
  public bool Vpk,Mount;public string VpkFile="";public List<string> Errors=new List<string>();
  public bool Ready {get{return Vpk&&Mount&&Errors.Count==0;}}
  public string Summary {get{return Ready?"VPK 与当前版本一致，加载路径正确。":String.Join(Environment.NewLine,Errors);}}
 }
 public static class Installation {
  sealed class Entry {public string Key,Value;public List<Entry> Children;}
  static List<string> Tokens(string source){var result=new List<string>();int i=0;while(i<source.Length){char c=source[i];if(Char.IsWhiteSpace(c)){i++;continue;}if(c=='/'&&i+1<source.Length&&source[i+1]=='/'){while(i<source.Length&&source[i]!='\n')i++;continue;}if(c=='/'&&i+1<source.Length&&source[i+1]=='*'){int end=source.IndexOf("*/",i+2,StringComparison.Ordinal);if(end<0)throw new Exception("加载配置注释未闭合。");i=end+2;continue;}if(c=='{'||c=='}'){result.Add(c.ToString());i++;continue;}if(c=='['){int end=source.IndexOf(']',i+1);if(end<0)throw new Exception("加载配置条件未闭合。");result.Add(source.Substring(i,end-i+1));i=end+1;continue;}var value=new StringBuilder();if(c=='\"'){i++;bool closed=false;while(i<source.Length){c=source[i++];if(c=='\"'){closed=true;break;}if(c=='\\'&&i<source.Length&&(source[i]=='\\'||source[i]=='\"'))c=source[i++];value.Append(c);}if(!closed)throw new Exception("加载配置引号未闭合。");}else{while(i<source.Length&&!Char.IsWhiteSpace(source[i])&&source[i]!='{'&&source[i]!='}')value.Append(source[i++]);}result.Add(value.ToString());}return result;}
  static List<Entry> Parse(List<string> tokens,ref int i,bool nested,int depth){if(depth>32)throw new Exception("加载配置嵌套过深。");var entries=new List<Entry>();while(i<tokens.Count){string key=tokens[i++];if(key=="}"){if(!nested)throw new Exception("加载配置括号不匹配。");return entries;}if(key=="{"||i>=tokens.Count)throw new Exception("加载配置缺少值。");string value=tokens[i++];var e=new Entry{Key=key};if(value=="{")e.Children=Parse(tokens,ref i,true,depth+1);else if(value=="}")throw new Exception("加载配置缺少值。");else e.Value=value;
    // Conditional search paths are not unconditional proof of installation.
    if(i<tokens.Count&&tokens[i].StartsWith("[")){i++;continue;}entries.Add(e);
   }if(nested)throw new Exception("加载配置括号未闭合。");return entries;}
  static List<Entry> Children(List<Entry> entries,string key){var e=entries.FirstOrDefault(x=>String.Equals(x.Key,key,StringComparison.OrdinalIgnoreCase));return e==null||e.Children==null?new List<Entry>():e.Children;}
  public static bool HasAddonsMount(string source){int i=0;var root=Parse(Tokens(source),ref i,false,0);var paths=Children(Children(Children(root,"GameInfo"),"FileSystem"),"SearchPaths");bool addons=false;foreach(var e in paths){if(e.Value==null||!e.Key.Split('+').Any(x=>x.Equals("Game",StringComparison.OrdinalIgnoreCase)))continue;string value=e.Value.Replace('\\','/').TrimEnd('/').ToLowerInvariant();if(value=="citadel"||value=="|gameinfo_path|."||value=="|gameinfo_path|")return addons;if(value=="citadel/addons"||value=="|gameinfo_path|addons")addons=true;}return addons;}
  static string Hash(string file){using(var f=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(f));}
  public static InstallationResult Inspect(Settings settings){var r=new InstallationResult();try{Core.ValidateSettings(settings);if(!Core.IsPackage(settings.CurrentFolder))throw new Exception("已有版本不完整，请重新导入完整 Windows 压缩包。");Core.NoLinks(settings.CurrentFolder);
    string packageVpk=Path.Combine(settings.CurrentFolder,"pak01_dir.vpk");if(!Core.IsBabelVpk(packageVpk))throw new Exception("当前包的 VPK 缺少巴别塔聊天资源。");string addons=Path.Combine(settings.GameRoot,"game","citadel","addons");
    var files=Directory.Exists(addons)?Directory.GetFiles(addons,"pak*_dir.vpk").Where(x=>Regex.IsMatch(Path.GetFileName(x),@"^pak\d+_dir\.vpk$",RegexOptions.IgnoreCase)&&Core.IsBabelVpk(x)).ToList():new List<string>();
    if(files.Count==0)r.Errors.Add("游戏 addons 中没有巴别塔 VPK，请退出游戏后重新安装。");else if(files.Count>1)r.Errors.Add("游戏里存在多个巴别塔 VPK，可能互相覆盖；请退出游戏后整理安装。");else{Core.NoLinks(files[0]);r.VpkFile=files[0];r.Vpk=Hash(packageVpk)==Hash(files[0]);if(!r.Vpk)r.Errors.Add("游戏里的巴别塔 VPK 与当前版本不同，请退出游戏后更新安装。");}
    string gi=Path.Combine(settings.GameRoot,"game","citadel","gameinfo.gi");if(!File.Exists(gi))r.Errors.Add("找不到 gameinfo.gi，无法确认 Mod 加载路径。");else{if(new FileInfo(gi).Length>2*1024*1024)throw new Exception("gameinfo.gi 体积异常，无法检查。");r.Mount=HasAddonsMount(File.ReadAllText(gi));if(!r.Mount)r.Errors.Add("gameinfo.gi 没有有效的 addons 优先加载路径；需退出游戏后修复加载配置。");}
    string cfg=Path.Combine(settings.CurrentFolder,"config","config.json");if(!File.Exists(cfg))cfg=Path.Combine(settings.CurrentFolder,"config","config.example.json");if(File.Exists(cfg)){var values=Core.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(cfg));if(values==null)throw new Exception("桥配置不是有效的 JSON 对象。");object value;if(values.TryGetValue("gameLogTail",out value)&&!Convert.ToBoolean(value))r.Errors.Add("桥配置关闭了 gameLogTail，请启用游戏日志读取后重启桥。");string version="";try{version=Core.VersionOf("",settings.CurrentFolder);}catch{}int port=values.TryGetValue("port",out value)?Convert.ToInt32(value):8791;if(port<1||port>65535)r.Errors.Add("桥配置端口无效。");else if((version=="107"||version=="108")&&port!=8791)r.Errors.Add("此版本游戏端默认使用 8791，桥配置端口为 "+port+"；请统一为 8791 后重启桥。");}
   }catch(Exception e){r.Errors.Add(e.Message);}return r;}
 }
 public class ProbeResult {public bool Local;public string Key="",Error="";}
 public class Connectivity {
  public string Context="",Key="",Error="",TranslationText="",TranslationError="";public bool Local,Known,Translation;public DateTime ProbeUtc,TranslationUtc;public long Revision;
  public void ChangeContext(string context){if(context==Context)return;Context=context;Revision++;Key="";Local=Known=Translation=false;Error=TranslationText=TranslationError="";ProbeUtc=TranslationUtc=DateTime.MinValue;}
  public long BeginAction(){return ++Revision;}
  public bool ApplyProbe(ProbeResult p,long revision,DateTime now){if(revision!=Revision)return false;if(!p.Local||Key!=p.Key){Translation=false;TranslationUtc=DateTime.MinValue;TranslationText=TranslationError="";}Local=p.Local;Known=true;Key=p.Key;Error=p.Error;ProbeUtc=now;return true;}
  public void RecordCheck(CheckResult r,DateTime now){Revision++;ApplyProbe(new ProbeResult{Local=r.Local,Key=r.Key,Error=r.Error},Revision,now);Translation=r.Local&&r.Translation;TranslationUtc=Translation?now:DateTime.MinValue;TranslationText=r.Text;TranslationError=r.Local&&!r.Translation?r.Error:"";}
  public bool TranslationFresh(DateTime now){return Local&&Translation&&now>=TranslationUtc&&now-TranslationUtc<=TimeSpan.FromMinutes(10)&&now>=ProbeUtc&&now-ProbeUtc<=TimeSpan.FromSeconds(45);}
  public bool LocalFresh(DateTime now){return Known&&Local&&now>=ProbeUtc&&now-ProbeUtc<=TimeSpan.FromSeconds(45);}
 }
 public interface IBridgeRuntime {bool HasOwned(string folder);void Stop(string folder);void Start(string folder,Settings settings,Action<string> log);CheckResult Check(string folder,Action<string> log,Action<bool,string> local);}
 public sealed class BridgeRuntime:IBridgeRuntime {
  public bool HasOwned(string folder){return Bridge.Processes(folder).Count>0;}public void Stop(string folder){Bridge.Stop(folder);}public void Start(string folder,Settings s,Action<string> log){Bridge.Start(folder,s,log);}public CheckResult Check(string folder,Action<string> log,Action<bool,string> local){return Bridge.Check(folder,log,local);}
 }
 public static class Recovery {
  public static CheckResult Run(Settings settings,bool restart,Action<string> log,Action<bool,string> local,IBridgeRuntime runtime){Core.ValidateSettings(settings);if(!Core.IsPackage(settings.CurrentFolder))throw new Exception("已有版本不完整，无法恢复桥。");Core.NoLinks(settings.CurrentFolder);if(!Core.Under(settings.InstallRoot,settings.CurrentFolder))throw new Exception("所选版本不在安装目录内。");if(restart&&runtime.HasOwned(settings.CurrentFolder)){log("仅重启所选版本的桥，游戏继续运行。");runtime.Stop(settings.CurrentFolder);}runtime.Start(settings.CurrentFolder,settings,log);return runtime.Check(settings.CurrentFolder,log,local);}
 }
 public class GameEvidence {public bool Running;public string Message="游戏尚未运行；启动后在 /tr 面板确认游戏内连接。";}
 public static class GameDiagnostics {
  public static GameEvidence Inspect(Settings s){var result=new GameEvidence();try{var options=new EnumerationOptions{Timeout=TimeSpan.FromSeconds(3)};using(var search=new ManagementObjectSearcher(new ManagementScope(),new ObjectQuery("SELECT ExecutablePath,CommandLine FROM Win32_Process WHERE Name='deadlock.exe'"),options))using(var rows=search.Get()){foreach(ManagementObject row in rows){result.Running=true;string path=row["ExecutablePath"] as string,command=row["CommandLine"] as string;if(path==null||command==null){result.Message="游戏正在运行；无法读取启动参数，请在游戏 /tr 面板测试。";continue;}string expected=Path.Combine(s.GameRoot,"game","bin","win64","deadlock.exe");if(!path.Equals(expected,StringComparison.OrdinalIgnoreCase)){result.Message="正在运行的游戏来自另一个目录，请核对路径设置。";continue;}if(!Regex.IsMatch(command,@"(?:^|[\s""\u0027])-condebug(?:[\s""\u0027]|$)",RegexOptions.IgnoreCase)){result.Message="当前游戏未带 -condebug；重启桥不能补上参数，需退出后从启动器进入。";return result;}result.Message=File.Exists(Path.Combine(s.GameRoot,"game","citadel","console.log"))?"游戏已带 -condebug；桥恢复后，请在游戏 /tr 面板点测试确认连接。":"游戏已带 -condebug，但日志尚未出现；等待游戏加载后在 /tr 测试。";return result;}}}catch{result.Running=Bridge.GameRunning();result.Message="无法读取游戏启动参数；本地服务正常不代表游戏内连接已通过。";}return result;}
 }
}
