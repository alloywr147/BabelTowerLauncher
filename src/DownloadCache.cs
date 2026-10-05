using System;using System.Collections.Generic;using System.IO;using System.Text;
namespace BabelManager {
 public sealed class ModDownloadSession {readonly bool startedWhileGameRunning;public ModDownloadSession(bool running){startedWhileGameRunning=running;}public bool CanInstallAutomatically(bool gameRunningNow){return !startedWhileGameRunning&&!gameRunningNow;}}
 public static class DownloadCache {
  public static void Save(string root,OfficialRelease release,string archive){Core.NoLinks(root);Core.NoLinks(archive);if(!Core.Under(root,archive))throw new Exception("下载缓存文件超出指定目录。");OfficialUpdates.VerifyArchive(release,archive);Directory.CreateDirectory(root);string path=Path.Combine(root,"pending.json"),temporary=path+".tmp";File.WriteAllText(temporary,Core.Json().Serialize(new{Tag=release.Tag,Name=release.Name,Archive=archive}),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
  public static string Find(string root,OfficialRelease release){try{Core.NoLinks(root);string path=Path.Combine(root,"pending.json");Core.NoLinks(path);if(!File.Exists(path)||new FileInfo(path).Length>65536)return "";var record=Core.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(path));if(Convert.ToString(record["Tag"])!=release.Tag||Convert.ToString(record["Name"])!=release.Name)return "";string archive=Convert.ToString(record["Archive"]);if(!Core.Under(root,archive))return "";OfficialUpdates.VerifyArchive(release,archive);return archive;}catch{return "";}}
 }
}
