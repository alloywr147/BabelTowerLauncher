using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
namespace BabelManager {
 public sealed class SelfUpdateEntry {public string Source,Target,Sha256;}
 public sealed class SelfUpdatePlan {public string TargetDirectory,StageDirectory,JobDirectory,ExecutableName,ParentExecutable,HelperExecutable;public int ParentPid;public long ParentStartedUtc;public List<SelfUpdateEntry> Entries=new List<SelfUpdateEntry>();}
 public sealed class SelfUpdateFailure:Exception {public readonly bool RecoveryComplete;public SelfUpdateFailure(string message,bool complete,Exception inner):base(message,inner){RecoveryComplete=complete;}}
 // Shared with the independent Windows updater; never references the running application assembly.
 public static class SelfUpdateFiles {
  public static string Hash(string file){using(var hash=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
  static string Full(string path){return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);}
  static bool Under(string root,string path){return Full(path).StartsWith(Full(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  public static void NoLinks(string path){for(string p=Path.GetFullPath(path);p!=null;){if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new Exception("更新路径不能包含链接。");var parent=Directory.GetParent(p);p=parent==null?null:parent.FullName;}}
  static string Member(string root,string relative){if(String.IsNullOrEmpty(relative)||Path.IsPathRooted(relative)||relative.Contains(":")||relative.IndexOf('\0')>=0)throw new Exception("更新文件路径无效。");foreach(var part in relative.Replace('\\','/').Split('/'))if(part==".."||part=="."||part==""||part.EndsWith(".")||part.EndsWith(" "))throw new Exception("更新文件路径无效。");string path=Path.GetFullPath(Path.Combine(root,relative));if(!Under(root,path))throw new Exception("更新文件超出目录。");NoLinks(path);return path;}
  public static void Validate(SelfUpdatePlan plan){
   if(plan==null||plan.Entries==null||plan.Entries.Count==0||plan.Entries.Count>50000)throw new Exception("更新计划无效。");
   NoLinks(plan.TargetDirectory);NoLinks(plan.JobDirectory);NoLinks(plan.StageDirectory);
   if(!Directory.Exists(plan.TargetDirectory)||!Under(Path.Combine(plan.TargetDirectory,"data"),plan.JobDirectory)||!Path.GetFileName(plan.JobDirectory).StartsWith("self-update-",StringComparison.Ordinal)||!Under(plan.JobDirectory,plan.StageDirectory))throw new Exception("更新暂存目录不属于当前启动器。");
   if(Path.GetFileName(plan.ExecutableName)!=plan.ExecutableName||!plan.ExecutableName.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(Member(plan.TargetDirectory,plan.ExecutableName)))throw new Exception("当前启动器不存在。");
   var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);bool executable=false,config=false;
   foreach(var entry in plan.Entries){string target=Member(plan.TargetDirectory,entry.Target),source=Member(plan.StageDirectory,entry.Source);if(entry.Target.Replace('\\','/').Split('/')[0].Equals("data",StringComparison.OrdinalIgnoreCase)||!seen.Add(target))throw new Exception("更新包试图覆盖用户配置或重复文件。");if(!File.Exists(source)||Hash(source)!=entry.Sha256)throw new Exception("暂存文件校验失败。");executable|=entry.Target.Equals(plan.ExecutableName,StringComparison.OrdinalIgnoreCase);config|=entry.Target.Equals(plan.ExecutableName+".config",StringComparison.OrdinalIgnoreCase);}
   if(!executable||!config)throw new Exception("更新包缺少启动器或运行配置。");
  }
  static void WriteAtomic(string source,string target){NoLinks(target);Directory.CreateDirectory(Path.GetDirectoryName(target));string temporary=target+".bt-update-"+Guid.NewGuid().ToString("N");try{File.Copy(source,temporary,false);if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);}finally{try{if(File.Exists(temporary))File.Delete(temporary);}catch{}}}
  public static void Apply(SelfUpdatePlan plan){
   string backup;var existed=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var changed=new List<SelfUpdateEntry>();
   // Finish every backup before touching any application file.
   try{Validate(plan);backup=Path.Combine(plan.JobDirectory,"backup");NoLinks(backup);Directory.CreateDirectory(backup);foreach(var entry in plan.Entries){string target=Member(plan.TargetDirectory,entry.Target);if(File.Exists(target)){string saved=Member(backup,entry.Target);Directory.CreateDirectory(Path.GetDirectoryName(saved));File.Copy(target,saved,false);existed.Add(entry.Target);}}}catch(Exception preparation){throw new SelfUpdateFailure("更新准备失败，原启动器文件未变。"+Environment.NewLine+preparation.Message,true,preparation);}
   try{foreach(var entry in plan.Entries){string target=Member(plan.TargetDirectory,entry.Target);WriteAtomic(Member(plan.StageDirectory,entry.Source),target);changed.Add(entry);if(Hash(target)!=entry.Sha256)throw new Exception("更新文件写入校验失败。");}File.WriteAllText(Path.Combine(plan.JobDirectory,"result.txt"),"SUCCESS",Encoding.UTF8);}
   catch(Exception error){var failures=new List<string>();foreach(var entry in changed.AsEnumerable().Reverse()){try{string target=Member(plan.TargetDirectory,entry.Target);if(!File.Exists(target)||Hash(target)!=entry.Sha256)throw new Exception("文件已被其他程序修改，保留备份。");if(existed.Contains(entry.Target))WriteAtomic(Member(backup,entry.Target),target);else File.Delete(target);}catch(Exception restore){failures.Add(entry.Target+": "+restore.Message);}}string message=failures.Count==0?"更新失败，已恢复原启动器。":"更新失败，部分文件未恢复。备份："+backup+Environment.NewLine+String.Join(Environment.NewLine,failures);try{File.WriteAllText(Path.Combine(plan.JobDirectory,"result.txt"),message,Encoding.UTF8);}catch{}throw new SelfUpdateFailure(message,failures.Count==0,error);}
  }
 }
}
