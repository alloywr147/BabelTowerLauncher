using System;using System.Collections.Generic;using System.Diagnostics;using System.IO;using System.Linq;using System.Reflection;using System.Text;
namespace BabelManager {
 public static class SelfUpdater {
  public static Version CurrentVersion {get{var v=typeof(SelfUpdater).Assembly.GetName().Version;return new Version(v.Major,v.Minor,v.Build);}}
  public static string VersionLabel(Version version){return version.Build==0?version.ToString(2):version.ToString(3);}
  public static SelfUpdatePlan Prepare(OfficialRelease release,string archive,string targetDirectory,string executableName,int parentPid){
   OfficialUpdates.VerifyArchive(release,archive);if(release.Source!=UpdateSource.Launcher)throw new Exception("启动器只能从自己的仓库更新。");Core.NoLinks(targetDirectory);if(Path.GetFileName(executableName)!=executableName||!File.Exists(Core.Child(targetDirectory,executableName)))throw new Exception("当前启动器路径无效。");
   string job=Core.Child(Path.Combine(targetDirectory,"data"),"self-update-"+Guid.NewGuid().ToString("N")),stage=Core.Child(job,"stage");Directory.CreateDirectory(job);
   try{Core.Extract(archive,stage);var files=Directory.GetFiles(stage,"*",SearchOption.AllDirectories);if(Directory.GetDirectories(stage,"*",SearchOption.AllDirectories).Any(p=>Path.GetFileName(p).Equals("data",StringComparison.OrdinalIgnoreCase)))throw new Exception("更新包包含用户配置，请使用干净的发布包。");
    var executables=files.Where(p=>new[]{"巴别塔启动器.exe","巴别塔启动器-在线更新镜像.exe","BabelTowerLauncher.exe"}.Contains(Path.GetFileName(p),StringComparer.OrdinalIgnoreCase)).ToArray();if(executables.Length!=1)throw new Exception("更新包必须包含唯一的 Windows 启动器。");string sourceExe=executables[0],root=Path.GetDirectoryName(sourceExe);
    foreach(string required in new[]{Path.GetFileName(sourceExe)+".config","assets/app.ico","tools/7z.exe","tools/7z.dll"})if(!File.Exists(Core.Child(root,required)))throw new Exception("启动器更新包不完整："+required);
    var binary=FileVersionInfo.GetVersionInfo(sourceExe);if(new Version(binary.FileMajorPart,binary.FileMinorPart,binary.FileBuildPart)!=release.Version)throw new Exception("启动器文件版本与发布版本不一致。");
    var plan=new SelfUpdatePlan{TargetDirectory=Core.Full(targetDirectory),StageDirectory=stage,JobDirectory=job,ExecutableName=executableName,ParentPid=parentPid};using(var parent=Process.GetProcessById(parentPid)){plan.ParentExecutable=parent.MainModule.FileName;plan.ParentStartedUtc=parent.StartTime.ToUniversalTime().Ticks;}
    foreach(string file in Directory.GetFiles(root,"*",SearchOption.AllDirectories)){string relative=file.Substring(root.Length+1),first=relative.Split(Path.DirectorySeparatorChar)[0];bool include=first.Equals("assets",StringComparison.OrdinalIgnoreCase)||first.Equals("tools",StringComparison.OrdinalIgnoreCase)||first.Equals("docs",StringComparison.OrdinalIgnoreCase)||!relative.Contains(Path.DirectorySeparatorChar)&&new[]{Path.GetFileName(sourceExe),Path.GetFileName(sourceExe)+".config","README.md","LICENSE","使用说明.md"}.Contains(relative,StringComparer.OrdinalIgnoreCase);if(!include)continue;string target=relative;if(file==sourceExe)target=executableName;else if(file==sourceExe+".config")target=executableName+".config";plan.Entries.Add(new SelfUpdateEntry{Source=file.Substring(stage.Length+1),Target=target,Sha256=SelfUpdateFiles.Hash(file)});}
    SelfUpdateFiles.Validate(plan);File.WriteAllText(Path.Combine(job,"plan.json"),Core.Json().Serialize(plan),Encoding.UTF8);return plan;
   }catch{Core.NoLinks(job);if(Core.Under(Path.Combine(targetDirectory,"data"),job))Directory.Delete(job,true);throw;}
  }
  public static void Start(SelfUpdatePlan plan){StartCancelable(plan,System.Threading.CancellationToken.None);}
  public static void CancelHandoff(SelfUpdatePlan plan){if(plan==null)return;SelfUpdateFiles.Validate(plan);File.WriteAllText(Path.Combine(plan.JobDirectory,"cancel.txt"),"CANCEL");}
  public static void StartCancelable(SelfUpdatePlan plan,System.Threading.CancellationToken token){
   token.ThrowIfCancellationRequested();SelfUpdateFiles.Validate(plan);string source=Path.Combine(plan.TargetDirectory,"tools","BabelTowerUpdater.exe");if(!File.Exists(source))throw new Exception("后台更新程序缺失，请重新解压启动器完整包。");Core.NoLinks(source);
   // CLR startup has a legacy path limit before app.config switches apply. Keep the
   // helper beside the running app, with a shorter filename, outside replacement payload.
   string helper=Core.Child(plan.TargetDirectory,"btu"+Guid.NewGuid().ToString("N").Substring(0,6)+".exe");plan.HelperExecutable=helper;File.WriteAllText(Path.Combine(plan.JobDirectory,"plan.json"),Core.Json().Serialize(plan),Encoding.UTF8);File.Copy(source,helper,false);File.Copy(source+".config",helper+".config",false);
   using(var process=Process.Start(new ProcessStartInfo(helper,Core.Quote(Path.Combine(plan.JobDirectory,"plan.json"))){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=plan.JobDirectory})){
    var clock=Stopwatch.StartNew();string ready=Path.Combine(plan.JobDirectory,"ready.txt"),error=Path.Combine(plan.JobDirectory,"helper-error.txt");
    try{while(!File.Exists(ready)){token.ThrowIfCancellationRequested();if(File.Exists(error))throw new Exception(File.ReadAllText(error));if(process.HasExited)throw new Exception("后台更新程序未就绪，当前启动器将保持打开。");if(clock.ElapsedMilliseconds>15000)throw new Exception("后台更新程序启动超时，当前启动器将保持打开。");System.Threading.Thread.Sleep(50);}token.ThrowIfCancellationRequested();}catch{CancelHandoff(plan);try{if(!process.HasExited)process.Kill();process.WaitForExit(5000);}catch{}throw;}
   }
  }
 }
}
