using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.VisualBasic.FileIO;
using SearchOption = System.IO.SearchOption;

namespace BabelManager {
 public class Settings {
  public string GameRoot="",InstallRoot="",CurrentFolder="",VpkPath="";
 }
 public static class Core {
  public static readonly string DataDir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data");
  public static string SettingsPath=Path.Combine(DataDir,"settings.json");
  public static string BackupsDir=Path.Combine(DataDir,"backups");
  public static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=1024*1024};}
  public static Settings Load(){try{return Json().Deserialize<Settings>(File.ReadAllText(SettingsPath));}catch{return new Settings();}}
  public static void Save(Settings s){Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));string tmp=SettingsPath+".tmp";File.WriteAllText(tmp,Json().Serialize(s),Encoding.UTF8);if(File.Exists(SettingsPath))File.Replace(tmp,SettingsPath,SettingsPath+".bak");else File.Move(tmp,SettingsPath);}
  public static string Full(string p){return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);}
  public static bool Under(string root,string p){return Full(p).StartsWith(Full(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
  public static string Child(string root,string relative){string p=Path.GetFullPath(Path.Combine(root,relative));if(!Under(root,p))throw new Exception("文件路径超出目标文件夹："+relative);return p;}
  public static void NoLinks(string p){string cur=Path.GetFullPath(p);while(!String.IsNullOrEmpty(cur)){if((Directory.Exists(cur)||File.Exists(cur))&&(File.GetAttributes(cur)&FileAttributes.ReparsePoint)!=0)throw new Exception("不能使用符号链接或目录联接："+cur);var par=Directory.GetParent(cur);cur=par==null?null:par.FullName;}}
  public static bool IsGame(string p){return !String.IsNullOrWhiteSpace(p)&&File.Exists(Path.Combine(p,"game","bin","win64","deadlock.exe"))&&Directory.Exists(Path.Combine(p,"game","citadel"));}
  public static void ValidateSettings(Settings s){if(!IsGame(s.GameRoot))throw new Exception("请选择 Deadlock 根目录，里面应包含 game 文件夹和 game\\bin\\win64\\deadlock.exe。");if(String.IsNullOrWhiteSpace(s.InstallRoot)||Full(s.InstallRoot).Length<=3)throw new Exception("请选择专用的 BabelTower 文件夹，不能直接使用磁盘根目录。");NoLinks(s.InstallRoot);NoLinks(s.GameRoot);if(Under(s.GameRoot,s.InstallRoot)||Under(s.InstallRoot,s.GameRoot)||Full(s.GameRoot)==Full(s.InstallRoot))throw new Exception("BabelTower 文件夹与游戏目录必须分开。");}
  public static bool IsPackage(string p){return File.Exists(Path.Combine(p,"core","bridge_server.js"))&&File.Exists(Path.Combine(p,"portable-node","node.exe"))&&File.Exists(Path.Combine(p,"pak01_dir.vpk"));}
  public static string FindPackage(string p){var candidates=new List<string>();if(IsPackage(p))candidates.Add(p);foreach(string d in Directory.GetDirectories(p,"*",SearchOption.AllDirectories)){NoLinks(d);if(IsPackage(d))candidates.Add(d);}if(candidates.Count!=1)throw new Exception("压缩包必须包含一个完整的 Windows 巴别塔版本（core、portable-node、pak01_dir.vpk）。");return candidates[0];}
  public static void ValidateMember(string root,string name){if(Path.IsPathRooted(name)||name.Contains(":")||name.IndexOf('\0')>=0)throw new Exception("压缩包含不安全路径。");foreach(string part in name.Replace('\\','/').Split('/')){if(part.Length==0)continue;if(part==".."||part=="."||part.EndsWith(" ")||part.EndsWith(".")||Regex.IsMatch(part,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\.|$)",RegexOptions.IgnoreCase))throw new Exception("压缩包包含 Windows 不安全文件名。");}Child(root,name.Replace('/',Path.DirectorySeparatorChar));}
  public static string VersionOf(string archive,string folder){var m=Regex.Match(Path.GetFileName(archive),@"babeltower[-_ ](?:v)?(\d{3,6})(?:[-_ .]|$)",RegexOptions.IgnoreCase);if(m.Success)return m.Groups[1].Value;foreach(string name in new[]{"VERSION","README.md","安装使用说明.txt"}){string f=Path.Combine(folder,name);if(!File.Exists(f))continue;string t=File.ReadAllText(f);m=Regex.Match(t,@"(?:版本\s*[:：]?\s*|^\s*v?)(\d+)\.(\d+)\.(\d+)",RegexOptions.Multiline);if(m.Success)return m.Groups[1].Value+m.Groups[2].Value+m.Groups[3].Value;}throw new Exception("无法识别版本号。请保留官方文件名，例如 babeltower-108-win64.zip。");}
  public static void Extract(string archive,string destination){
   NoLinks(destination);Directory.CreateDirectory(destination);
   if(Path.GetExtension(archive).Equals(".zip",StringComparison.OrdinalIgnoreCase)){
    using(var z=ZipFile.OpenRead(archive)){long total=0;foreach(var e in z.Entries){ValidateMember(destination,e.FullName);if((e.ExternalAttributes>>16&0xF000)==0xA000)throw new Exception("压缩包包含符号链接。");total+=e.Length;if(total>4L*1024*1024*1024||z.Entries.Count>50000)throw new Exception("压缩包解压体积或文件数异常。");}
     foreach(var e in z.Entries){string p=Child(destination,e.FullName.Replace('/',Path.DirectorySeparatorChar));if(String.IsNullOrEmpty(e.Name)){Directory.CreateDirectory(p);continue;}Directory.CreateDirectory(Path.GetDirectoryName(p));e.ExtractToFile(p,false);}
    }
   }else{
    string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tools","7z.exe");if(!File.Exists(exe))throw new Exception("缺少 tools\\7z.exe。请将管理器 ZIP 整包解压后运行。");
    string listing=Run(exe,"l -slt -ba -- "+Quote(archive));long total=0;int count=0;foreach(string line in listing.Split('\n')){string l=line.TrimEnd('\r');if(l.StartsWith("Path = ")){string name=l.Substring(7);if(Path.IsPathRooted(name)||name.Contains(":")||name.Replace('\\','/').Split('/').Any(x=>x==".."))throw new Exception("压缩包含不安全路径。");Child(destination,name);if(++count>50000)throw new Exception("压缩包文件数异常。");}if(l.StartsWith("Size = ")){long n;if(Int64.TryParse(l.Substring(7),out n))total+=n;if(total>4L*1024*1024*1024)throw new Exception("解压体积超过 4GB。");}if(l.StartsWith("Symbolic Link = ")||l.StartsWith("Hard Link = ")||l.StartsWith("Attributes = ")&&(l.Contains(" l")||l.Contains(" L")))throw new Exception("压缩包含链接。");}
    Run(exe,"x -y -o"+Quote(destination)+" -- "+Quote(archive));
   }
   foreach(string f in Directory.GetFileSystemEntries(destination,"*",SearchOption.AllDirectories))NoLinks(f);
  }
  public static string Quote(string s){if(s.Contains("\""))throw new Exception("路径不能含引号。");return "\""+s+"\"";}
  static string Run(string exe,string args){var si=new ProcessStartInfo(exe,"-sccUTF-8 "+args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};using(var p=Process.Start(si)){p.StandardInput.Close();var err=p.StandardError.ReadToEndAsync();var outputTask=p.StandardOutput.ReadToEndAsync();if(!p.WaitForExit(120000)){p.Kill();throw new Exception("解压超时。");}string output=outputTask.Result,error=err.Result;if(p.ExitCode!=0)throw new Exception("解压失败："+error+output);return output;}}
  public static bool IsBabelVpk(string file){try{using(var f=File.OpenRead(file))using(var b=new BinaryReader(f)){if(b.ReadUInt32()!=0x55AA1234)return false;uint ver=b.ReadUInt32(),size=b.ReadUInt32();int header=ver==1?12:ver==2?28:0;if(header==0||size>32*1024*1024||size==0||f.Length<header+size)return false;f.Position=header;string tree=Encoding.UTF8.GetString(b.ReadBytes((int)size));return tree.IndexOf("lingua_chat\0",StringComparison.OrdinalIgnoreCase)>=0&&tree.IndexOf("panorama/scripts\0",StringComparison.OrdinalIgnoreCase)>=0;}}catch{return false;}}
  public static List<string> GameVpks(Settings s){string addons=Path.Combine(s.GameRoot,"game","citadel","addons");Directory.CreateDirectory(addons);return Directory.GetFiles(addons,"pak*_dir.vpk").Where(f=>Regex.IsMatch(Path.GetFileName(f),@"^pak\d+_dir\.vpk$",RegexOptions.IgnoreCase)&&IsBabelVpk(f)).OrderBy(f=>f).ToList();}
  public static string ChooseVpk(Settings s){var list=GameVpks(s);if(list.Count>0)return list.Contains(s.VpkPath,StringComparer.OrdinalIgnoreCase)?s.VpkPath:list[0];string addons=Path.Combine(s.GameRoot,"game","citadel","addons");for(int i=1;i<1000;i++){string p=Path.Combine(addons,"pak"+i.ToString("00")+"_dir.vpk");if(!File.Exists(p))return p;}throw new Exception("游戏 addons 文件夹没有可用 VPK 编号。");}
  public static void WriteGameLog(string pkg,Settings s){string cfg=Path.Combine(pkg,"config","config.json");if(!File.Exists(cfg)){string example=Path.Combine(pkg,"config","config.example.json");if(!File.Exists(example))throw new Exception("新版本缺少配置模板。");File.Copy(example,cfg);}var obj=Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(cfg));obj["gameLogPath"]=Path.Combine(s.GameRoot,"game","citadel","console.log");File.WriteAllText(cfg,Json().Serialize(obj),new UTF8Encoding(false));Directory.CreateDirectory(Path.Combine(pkg,"logs"));}
  // Checks run before touching game files. Journal/backups persist outside addons for recovery.
  public static string Commit(Settings s,string fresh,Action check,bool production,Action afterCopy=null){
   ValidateSettings(s);NoLinks(fresh);if(!Under(s.InstallRoot,fresh)||!IsPackage(fresh))throw new Exception("新版本必须是安装目录下的完整巴别塔文件夹。");string old=s.CurrentFolder;string oldVpk=s.VpkPath;
   if(!String.IsNullOrEmpty(old)){NoLinks(old);if(!Under(s.InstallRoot,old)||!IsPackage(old)||Under(old,fresh)||Under(fresh,old)||Full(old)==Full(fresh))throw new Exception("旧版本必须是安装目录下独立的完整巴别塔文件夹。");}
   check();string slot=production?ChooseVpk(s):s.VpkPath;if(String.IsNullOrEmpty(slot))throw new Exception("没有 VPK 目标。");string addons=Path.Combine(s.GameRoot,"game","citadel","addons");if(!Under(addons,slot))throw new Exception("VPK 目标不在游戏 addons 中。");NoLinks(slot);
   var targets=production?GameVpks(s):new List<string>();if(File.Exists(slot)&&!targets.Contains(slot,StringComparer.OrdinalIgnoreCase))targets.Add(slot);
   string backup=Path.Combine(production?BackupsDir:Path.Combine(Path.GetDirectoryName(s.InstallRoot),"backups"),DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));Directory.CreateDirectory(backup);foreach(string f in targets)File.Copy(f,Path.Combine(backup,Path.GetFileName(f)));File.WriteAllText(Path.Combine(backup,"recovery.json"),Json().Serialize(new{gameRoot=s.GameRoot,oldFolder=old,newFolder=fresh,slot=slot,files=targets}),Encoding.UTF8);
   string temporary=slot+".bt-new";bool saved=false;
   try{
    File.Copy(Path.Combine(fresh,"pak01_dir.vpk"),temporary,true);if(File.Exists(slot))File.Replace(temporary,slot,null);else File.Move(temporary,slot);foreach(string f in targets)if(!f.Equals(slot,StringComparison.OrdinalIgnoreCase))File.Delete(f);if(afterCopy!=null)afterCopy();s.CurrentFolder=fresh;s.VpkPath=slot;if(production)Save(s);saved=true;
   }catch{
    foreach(string f in targets)File.Copy(Path.Combine(backup,Path.GetFileName(f)),f,true);if(!targets.Contains(slot,StringComparer.OrdinalIgnoreCase)&&File.Exists(slot))File.Delete(slot);s.CurrentFolder=old;s.VpkPath=oldVpk;throw;
   }finally{if(File.Exists(temporary))File.Delete(temporary);}
   string warning="";if(saved&&!String.IsNullOrEmpty(old)&&Directory.Exists(old)){try{NoLinks(old);if(!Under(s.InstallRoot,old))throw new Exception("旧目录边界变化。");if(production)FileSystem.DeleteDirectory(old,UIOption.OnlyErrorDialogs,RecycleOption.SendToRecycleBin);else Directory.Delete(old,true);}catch(Exception ex){warning="更新已完成，但旧文件夹未能移入回收站："+ex.Message;}}
   return warning;
  }
 }
}
