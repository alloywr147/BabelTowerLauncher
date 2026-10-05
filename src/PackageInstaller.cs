using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Security.Cryptography;using System.Text;using System.Text.RegularExpressions;using System.Threading;
namespace BabelManager {
 public sealed class InstallReport {public CheckResult Check;public InstallationResult Installation;public string Warning="";}
 public static class MountConfiguration {
  sealed class Token {public string Value;public int End;}
  static int Closing(List<Token> tokens,int open){int depth=0;for(int i=open;i<tokens.Count;i++){if(tokens[i].Value=="{")depth++;else if(tokens[i].Value=="}"&&--depth==0)return i;}throw new Exception("加载配置括号未闭合。");}
  static int Block(List<Token> tokens,int start,int end,string name){int found=-1;for(int i=start;i<end;){string key=tokens[i++].Value;if(i>=end)throw new Exception("加载配置缺少值。");int value=i++;if(tokens[value].Value=="{"){int close=Closing(tokens,value);bool conditional=close+1<end&&tokens[close+1].Value.StartsWith("[");if(key.Equals(name,StringComparison.OrdinalIgnoreCase)&&!conditional){if(found>=0)throw new Exception("加载配置存在多个 "+name+" 区块，请手动确认。");found=value;}i=close+1;}if(i<end&&tokens[i].Value.StartsWith("["))i++;}if(found<0)throw new Exception("加载配置缺少 "+name+" 区块，请先通过 Steam 验证游戏文件。");return found;}
  public static string EnsureAddons(string source){
   if(Installation.HasAddonsMount(source))return source;
   var tokens=new List<Token>();foreach(Match match in Regex.Matches(source,@"//[^\r\n]*|/\*[\s\S]*?\*/|""(?:\\.|[^""\\])*""|[{}]|[^\s{}""]+")){string value=match.Value;if(value.StartsWith("//")||value.StartsWith("/*"))continue;if(value.StartsWith("\""))value=value.Substring(1,value.Length-2).Replace("\\\"","\"").Replace("\\\\","\\");tokens.Add(new Token{Value=value,End=match.Index+match.Length});}
   int root=Block(tokens,0,tokens.Count,"GameInfo"),system=Block(tokens,root+1,Closing(tokens,root),"FileSystem"),paths=Block(tokens,system+1,Closing(tokens,system),"SearchPaths");
   string updated=source.Insert(tokens[paths].End,"\r\n\t\t\tGame\t\t\t\tcitadel/addons");if(!Installation.HasAddonsMount(updated))throw new Exception("加载路径修复无法通过检查，更新已取消。");return updated;
  }
  public static void Write(string file,byte[] bytes){Core.NoLinks(file);string temporary=file+".bt-new-"+Guid.NewGuid().ToString("N");try{File.WriteAllBytes(temporary,bytes);File.Replace(temporary,file,null);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
 }
 public static class PackageInstaller {
  static void Cleanup(string root,string path){Core.NoLinks(path);if(!Core.Under(root,path))throw new Exception("临时目录超出安装目录。");if(Directory.Exists(path))Directory.Delete(path,true);}
  public static InstallReport Install(Settings settings,string archive,IBridgeRuntime runtime,Func<bool> gameRunning,Action<string> log,Action<bool,string> local){
   Core.ValidateSettings(settings);if(gameRunning())throw new Exception("请退出 Deadlock 后再安装；已下载的压缩包会保留。");
   Directory.CreateDirectory(settings.InstallRoot);string lockKey;using(var sha=SHA256.Create())lockKey=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Core.Full(settings.GameRoot).ToUpperInvariant()))).Replace("-","");
   using(var mutex=new Mutex(false,"Local\\BabelTowerInstall-"+lockKey)){bool owned;try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}if(!owned)throw new Exception("这个游戏目录正在被另一个在线更新窗口处理，请稍后重试。");try{return Run(settings,archive,runtime,gameRunning,log,local);}finally{mutex.ReleaseMutex();}}
  }
  static InstallReport Run(Settings settings,string archive,IBridgeRuntime runtime,Func<bool> gameRunning,Action<string> log,Action<bool,string> local){
   string gi=Path.Combine(settings.GameRoot,"game","citadel","gameinfo.gi");Core.NoLinks(gi);if(!File.Exists(gi)||new FileInfo(gi).Length>2*1024*1024)throw new Exception("找不到有效的 gameinfo.gi，请先通过 Steam 验证游戏文件。");
   byte[] originalGi=File.ReadAllBytes(gi);string originalText=File.ReadAllText(gi),updatedText=MountConfiguration.EnsureAddons(originalText);
   string old=settings.CurrentFolder??"",stage=Core.Child(settings.InstallRoot,".bt-stage-"+Guid.NewGuid().ToString("N")),fresh="",mountBackup="";byte[] writtenGi=new UTF8Encoding(false).GetBytes(updatedText);bool moved=false,committed=false,stoppedOld=false,mountWritten=false;var report=new InstallReport();
   try{
    log("解压并检查完整包："+Path.GetFileName(archive));Core.Extract(archive,stage);string pkg=Core.FindPackage(stage);string version=Core.VersionOf(archive,pkg);if(!Core.IsBabelVpk(Path.Combine(pkg,"pak01_dir.vpk")))throw new Exception("压缩包的 VPK 不包含巴别塔聊天资源。");
    fresh=Core.Child(settings.InstallRoot,"BabelTower-"+version);if(Directory.Exists(fresh))throw new Exception("目标版本目录已存在："+fresh+"。请先确认该目录的用途。");
    if(gameRunning())throw new Exception("检测到游戏正在运行，请退出后重试。");Directory.Move(pkg,fresh);moved=true;
    if(!String.IsNullOrEmpty(old)){stoppedOld=runtime.HasOwned(old);if(stoppedOld)runtime.Stop(old);}
    report.Warning=Core.Commit(settings,fresh,()=>{
     log("检查新版桥和翻译接口…");runtime.Start(fresh,settings,log);report.Check=runtime.Check(fresh,log,local);if(!report.Check.Local||!report.Check.Translation)throw new Exception(report.Check.Error);
     if(gameRunning())throw new Exception("检测到游戏正在启动，请退出后重试。");
    },true,()=>{
     if(!File.ReadAllBytes(gi).SequenceEqual(originalGi))throw new Exception("游戏加载配置在更新期间发生变化，请重新检查后再更新。");
     if(updatedText!=originalText){mountBackup=Core.Child(Core.BackupsDir,"mount-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(mountBackup);File.WriteAllBytes(Path.Combine(mountBackup,"gameinfo.gi"),originalGi);File.WriteAllBytes(Path.Combine(mountBackup,"gameinfo.installed.gi"),writtenGi);MountConfiguration.Write(gi,writtenGi);mountWritten=true;log("已补齐 addons 优先加载路径，并保留原配置备份。");}
     var candidate=new Settings{GameRoot=settings.GameRoot,InstallRoot=settings.InstallRoot,CurrentFolder=fresh,VpkPath=Core.ChooseVpk(settings)};report.Installation=Installation.Inspect(candidate);if(!report.Installation.Ready)throw new Exception(report.Installation.Summary);
     if(gameRunning())throw new Exception("检测到游戏正在启动，更新已撤回，请退出后重试。");
    });
    committed=true;log("更新完成："+Path.GetFileName(fresh));return report;
   }catch(Exception failure){
    var transaction=failure as UpdateTransactionException;bool recovered=transaction==null||transaction.RecoveryComplete;
    if(mountWritten){try{if(!File.ReadAllBytes(gi).SequenceEqual(writtenGi)){recovered=false;log("加载配置被其他程序修改，已保留当前内容。原配置和安装快照备份："+mountBackup);}else{MountConfiguration.Write(gi,originalGi);log("已恢复原游戏加载配置。");}}catch(Exception e){recovered=false;log("加载配置恢复失败，备份："+mountBackup+"；"+e.Message);}}
    if(moved&&!committed){try{runtime.Stop(fresh);if(recovered)Cleanup(settings.InstallRoot,fresh);else log("恢复未全部完成，新旧版本目录均已保留："+fresh);}catch(Exception e){log("新目录暂未清理："+e.Message);}}
    if(stoppedOld&&!committed&&recovered&&Core.IsPackage(old)){try{runtime.Start(old,settings,log);log("已恢复旧版本桥。");}catch(Exception e){log("旧版本文件仍保留，桥重启未完成："+e.Message);}}
    if(!recovered)throw new UpdateTransactionException("更新失败，恢复未全部完成。新旧版本目录和备份已保留，请查看更新记录。\n"+failure.Message,failure,false,transaction==null?mountBackup:transaction.Backup);throw;
   }finally{if(Directory.Exists(stage)){try{Cleanup(settings.InstallRoot,stage);}catch(Exception e){log("解压临时目录待清理："+e.Message);}}}
  }
 }
}
