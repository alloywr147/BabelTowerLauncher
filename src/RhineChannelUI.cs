using System;
using System.IO;
using System.Threading.Tasks;
namespace BabelManager {
 public partial class MainWindow {
  bool certificateConfiguring;
  async Task ConfigureRhineCertificate(bool confirmed = false){
   if(busy||detecting)return;
   try{CertificateSetup.StartInfo(settings);}catch(Exception error){Error(error.Message);return;}
   if(RhineTestMode){RhineLog("证书配置测试：未运行脚本或更改信任库。");rhineResult="证书配置测试：未运行脚本或更改信任库。";return;}
   if(!confirmed&&System.Windows.Forms.MessageBox.Show(this,
    "将运行所选 Mod 的官方证书脚本，为本机生成并信任 BabelTower-localhost 证书。\n\nWindows 可能弹出安全警告，请核对证书名称后选择“是”。完成后需完全退出并重新打开 Steam（包括托盘）；不会自动关闭游戏或 Steam。",
    "配置低延迟通道证书",System.Windows.Forms.MessageBoxButtons.YesNo,System.Windows.Forms.MessageBoxIcon.Information,System.Windows.Forms.MessageBoxDefaultButton.Button2)!=System.Windows.Forms.DialogResult.Yes)return;
   certificateConfiguring=true;rhineResult="";SetBusy(true);
   Log("正在配置证书；如出现 Windows 安全警告，请核对 BabelTower-localhost 后确认。");
   try{
    var result=await Task.Run(()=>CertificateSetup.Configure(settings,new CertificateSetupRuntime(),Log));
    rhineResult=result.Message;Log(result.Message);if(!rhineReady)Error(result.Message);
   }catch(Exception error){rhineResult="证书配置未完成："+error.Message;Log(rhineResult);if(!rhineReady)Error(rhineResult);}
   finally{certificateConfiguring=false;SetBusy(false);channelNextUtc=DateTime.MinValue;rhineLastState="";PushRhineState();}
  }
  bool channelChecking;
  DateTime channelNextUtc=DateTime.MinValue;
  async void RefreshRhineChannel(){
   if(RhineTestMode||channelChecking||busy||detecting||IsDisposed||DateTime.UtcNow<channelNextUtc)return;
   SyncContext();
   if(!Core.IsPackage(settings.CurrentFolder)){connectivity.Channel=null;channelNextUtc=DateTime.UtcNow.AddSeconds(8);return;}
   channelChecking=true;
   long revision=connectivity.Revision;string context=connectivity.Context,folder=settings.CurrentFolder;
   try{
    var result=await Task.Run(()=>LowLatencyChannel.Inspect(folder,new ChannelRuntime()));
    if(IsDisposed||Disposing||busy)return;
    SyncContext();connectivity.ApplyChannel(result,revision,context);
   }catch{if(!IsDisposed&&!busy)connectivity.ApplyChannel(LowLatencyChannel.Status("error"),revision,context);}
   finally{channelChecking=false;if(connectivity.Context==context)channelNextUtc=DateTime.UtcNow.AddSeconds(8);}
  }
  async Task OpenRhineWebSettings(){
   if(busy)return;
   if(!Core.IsPackage(settings.CurrentFolder)){Error("请先安装或选择完整 Mod 版本。");return;}
   if(!File.Exists(Path.Combine(settings.CurrentFolder,"core","web","ui.html"))){Error("所选 Mod 没有网页设置页，请更新到支持网页设置的版本。");return;}
   if(RhineTestMode){RhineLog("网页设置页："+Bridge.SettingsUrl(settings.CurrentFolder));return;}
   if(!Core.IsGame(settings.GameRoot)||String.IsNullOrWhiteSpace(settings.InstallRoot)){ShowSetup();Error("请先设置有效的游戏目录和巴别塔目录。");return;}
   SetBusy(true);
   try{
    Core.ValidateSettings(settings);Core.NoLinks(settings.CurrentFolder);
    if(!Core.Under(settings.InstallRoot,settings.CurrentFolder))throw new Exception("所选 Mod 不在安装目录内。");
    string folder=settings.CurrentFolder;
    await Task.Run(()=>{
     Bridge.Start(folder,settings,Log);var clock=System.Diagnostics.Stopwatch.StartNew();ProbeResult result;
     do{result=Bridge.Probe(folder);if(result.Local)return;System.Threading.Thread.Sleep(350);}while(clock.Elapsed<TimeSpan.FromSeconds(14));
     throw new Exception("网页设置所需的本地桥尚未就绪："+result.Error);
    });
    Bridge.OpenSettingsPage(folder);
   }catch(Exception error){Error(error.Message);}
   finally{SetBusy(false);channelNextUtc=DateTime.MinValue;}
  }
 }
}
