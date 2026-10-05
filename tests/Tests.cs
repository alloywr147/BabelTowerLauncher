using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using BabelManager;
class Tests {
 static void Assert(bool v,string m){if(!v)throw new Exception(m); Console.WriteLine("PASS "+m);}
 static void Reject(Action a,string m){bool bad=false;try{a();}catch{bad=true;}Assert(bad,m);}
 static void Fixture(string p){Directory.CreateDirectory(Path.Combine(p,"core"));Directory.CreateDirectory(Path.Combine(p,"portable-node"));File.WriteAllText(Path.Combine(p,"core","bridge_server.js"),"fixture");File.WriteAllText(Path.Combine(p,"portable-node","node.exe"),"fixture");File.WriteAllText(Path.Combine(p,"README.md"),"版本:1.0.8");File.WriteAllText(Path.Combine(p,"pak01_dir.vpk"),"fixture");}
 static void Vpk(string path){byte[] tree=Encoding.UTF8.GetBytes("vjs_c\0panorama/scripts\0lingua_chat\0");using(var b=new BinaryWriter(File.Create(path))){b.Write((uint)0x55AA1234);b.Write((uint)1);b.Write((uint)tree.Length);b.Write(tree);}}
 static void Main(string[] args){string r=Path.Combine(Path.GetTempPath(),"BTManager-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(r);try{
  Assert(Core.VersionOf("babeltower-108-win64.zip",r)=="108","archive version normalization");
  string pkg=Path.Combine(r,"pkg");Fixture(pkg);Assert(Core.VersionOf("release.zip",pkg)=="108","README version detection");
  Reject(()=>Core.Child(r,"..\\escape"),"reject path traversal");
  Reject(()=>Core.ValidateMember(r,"folder/.. /evil"),"reject Windows path normalization bypass");Reject(()=>Core.ValidateMember(r,"file.txt:stream"),"reject NTFS alternate data stream");Reject(()=>Core.ValidateMember(r,"NUL.txt"),"reject reserved Windows filename");
  string zip=Path.Combine(r,"evil.zip");using(var z=ZipFile.Open(zip,ZipArchiveMode.Create)){using(var w=new StreamWriter(z.CreateEntry("../escape.txt").Open()))w.Write("bad");}
  Reject(()=>Core.Extract(zip,Path.Combine(r,"extract")),"reject malicious ZIP");Assert(!File.Exists(Path.Combine(r,"escape.txt")),"no escaped file written");
  string game=Path.Combine(r,"gameRoot");Directory.CreateDirectory(Path.Combine(game,"game","bin","win64"));Directory.CreateDirectory(Path.Combine(game,"game","citadel","addons"));File.WriteAllText(Path.Combine(game,"game","bin","win64","deadlock.exe"),"");Assert(Core.IsGame(game),"validate game root");Reject(()=>Core.ValidateSettings(new Settings{GameRoot=r,InstallRoot=Path.Combine(r,"install")}),"reject invalid game root");
  string root=Path.Combine(r,"install");Directory.CreateDirectory(root);string old=Path.Combine(root,"BabelTower-107");Fixture(old);File.WriteAllText(Path.Combine(old,"custom.txt"),"do not migrate");
  string addon=Path.Combine(game,"game","citadel","addons","pak16_dir.vpk");File.WriteAllText(addon,"old VPK");string other=Path.Combine(game,"game","citadel","addons","pak17_dir.vpk");File.WriteAllText(other,"unrelated mod");
  var s=new Settings{GameRoot=game,InstallRoot=root,CurrentFolder=old,VpkPath=addon};string fresh=Path.Combine(root,"BabelTower-108");Fixture(fresh);
  Reject(()=>Core.Commit(s,fresh,()=>{throw new Exception("simulated bridge failure");},false),"failed check aborts update");Assert(File.ReadAllText(addon)=="old VPK"&&Directory.Exists(old)&&s.CurrentFolder==old,"failure preserves old package and VPK");
  Core.Commit(s,fresh,()=>{},false);Assert(File.ReadAllText(addon)=="fixture"&&s.CurrentFolder==fresh,"successful update replaces VPK");Assert(!Directory.Exists(old)&&!File.Exists(Path.Combine(fresh,"custom.txt")),"whole package replacement without migration");Assert(File.ReadAllText(other)=="unrelated mod","other mods preserved");
  string old2=s.CurrentFolder;string fresh2=Path.Combine(root,"BabelTower-109");Fixture(fresh2);Reject(()=>Core.Commit(s,fresh2,()=>{},false,()=>{throw new Exception("simulated commit failure");}),"commit failure rolls back");Assert(File.ReadAllText(addon)=="fixture"&&s.CurrentFolder==old2&&Directory.Exists(old2),"rollback restores VPK and current folder");
  Vpk(Path.Combine(fresh2,"pak01_dir.vpk"));Vpk(addon);string duplicate=Path.Combine(game,"game","citadel","addons","pak18_dir.vpk");Vpk(duplicate);s.CurrentFolder="";Core.SettingsPath=Path.Combine(r,"settings.json");Core.BackupsDir=Path.Combine(r,"production-backups");
  bool reached=false;Reject(()=>Core.Commit(s,fresh2,()=>{},true,()=>{reached=true;throw new Exception("interrupt after duplicate removal");}),"real install transaction rollback");Assert(reached&&File.Exists(duplicate)&&Core.IsBabelVpk(addon)&&File.ReadAllText(other)=="unrelated mod","rollback restores duplicate and preserves unrelated VPK");
  Core.Commit(s,fresh2,()=>{},true);Assert(s.VpkPath==addon&&!File.Exists(duplicate)&&File.ReadAllText(other)=="unrelated mod","reuse old VPK slot and remove only BabelTower duplicate");Assert(Core.Load().CurrentFolder==fresh2,"settings committed after successful update");
  string safezip=Path.Combine(r,"babeltower-110-win64.zip");ZipFile.CreateFromDirectory(pkg,safezip);string unpack=Path.Combine(r,"unpacked");Core.Extract(safezip,unpack);Assert(Core.IsPackage(Core.FindPackage(unpack)),"valid complete ZIP extraction");
  var invalid=new Settings{GameRoot=game,InstallRoot=game};Reject(()=>Core.ValidateSettings(invalid),"reject overlapping install and game folders");
  if(args.Length>0){string seven=Path.Combine(r,"seven");Core.Extract(args[0],seven);Assert(File.ReadAllText(Path.Combine(seven,"中文文件.txt"))=="portable archive test","bundled 7-Zip extraction with Chinese filename");}
  if(args.Length>1)Assert(Core.IsBabelVpk(args[1]),"recognize actual official BabelTower VPK");
  Console.WriteLine("ALL TESTS PASSED");
 }finally{if(Directory.Exists(r)){string cleanup=Path.GetFullPath(r);string temporary=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!cleanup.StartsWith(temporary,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(cleanup).StartsWith("BTManager-test-",StringComparison.Ordinal))throw new Exception("unsafe fixture cleanup");Directory.Delete(cleanup,true);}}}
}
