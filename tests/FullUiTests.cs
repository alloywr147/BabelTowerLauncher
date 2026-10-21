using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using BabelManager;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
class FullUiTests {
 sealed class QuietWindow:MainWindow{protected override bool ShowWithoutActivation{get{return true;}}}
 static int passed,exit;static string stage="initializing";
 static T Field<T>(object target,string name){return (T)typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
 static void Put(object target,string name,object value){typeof(MainWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
 static void Push(object target){typeof(MainWindow).GetMethod("PushRhineState",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);}
 static Task<string> Js(WebView2 view,string script){return view.CoreWebView2.ExecuteScriptAsync(script);}
 static void Ok(bool condition,string message){if(!condition)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
 static async Task Wait(Func<Task<bool>> ready){var limit=DateTime.UtcNow.AddSeconds(30);while(!await ready()){if(DateTime.UtcNow>limit)throw new Exception("UI timeout: "+stage);await Task.Delay(120);}await Task.Delay(200);}
 static Task Page(WebView2 view,string page){stage=page;return Wait(async()=>await Js(view,(page=="home"?"!!document.querySelector('.launch-panel')":"!!document.querySelector('.page-"+page+"')")+"&&!document.querySelector('.page-surface').hasAttribute('inert')")=="true");}
 static async Task Capture(WebView2 view,string file){await Task.Delay(800);using(var output=File.Create(file))await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);}
 [STAThread]static void Main(string[] args){
  RhineRuntime.Prepare();MainWindow.RhineEnabled=true;MainWindow.RhineTestMode=true;Application.EnableVisualStyles();
  string fixture=Path.Combine(Path.GetTempPath(),"BT17-ui-"+Guid.NewGuid().ToString("N"));string game=Path.Combine(fixture,"Deadlock"),install=Path.Combine(fixture,"BabelTower"),package=Path.Combine(install,"BabelTower-121");
  Directory.CreateDirectory(Path.Combine(game,"game","bin","win64"));Directory.CreateDirectory(Path.Combine(game,"game","citadel"));File.WriteAllText(Path.Combine(game,"game","bin","win64","deadlock.exe"),"fixture");
  foreach(string sub in new[]{"core/web","portable-node","config"})Directory.CreateDirectory(Path.Combine(package,sub));
  File.WriteAllText(Path.Combine(package,"core","bridge_server.js"),"fixture");File.WriteAllText(Path.Combine(package,"core","bridge_https.js"),"fixture");File.WriteAllText(Path.Combine(package,"core","web","ui.html"),"fixture");File.WriteAllText(Path.Combine(package,"portable-node","node.exe"),"fixture");File.WriteAllText(Path.Combine(package,"pak01_dir.vpk"),"fixture");File.WriteAllText(Path.Combine(package,"VERSION"),"1.2.1");File.WriteAllText(Path.Combine(package,"config","config.json"),"{\"port\":8791}");
  Directory.CreateDirectory(Path.Combine(package,"scripts"));File.WriteAllText(Path.Combine(package,"scripts","gen_bridge_cert.ps1"),"fixture");
  Core.SettingsPath=Path.Combine(fixture,"settings.json");Core.BackupsDir=Path.Combine(fixture,"backups");ThemePreferences.Path=Path.Combine(fixture,"appearance.json");ThemePreferences.SaveDark(true);Core.Save(new Settings{GameRoot=game,InstallRoot=install,CurrentFolder=package});
  Directory.CreateDirectory(Core.DataDir);File.WriteAllText(Path.Combine(Core.DataDir,"interface-mode.txt"),"simple");
  string artifacts=args[0];Directory.CreateDirectory(artifacts);var window=new QuietWindow();window.EnableRhine();window.StartPosition=FormStartPosition.Manual;window.Location=new Point(-20000,-20000);window.ShowInTaskbar=false;
  window.Shown+=async(sender,e)=>{try{
   WebView2 view=null;stage="web ready";await Wait(()=>{view=Field<WebView2>(window,"rhineView");return Task.FromResult(view!=null&&view.CoreWebView2!=null&&Field<bool>(window,"rhineReady"));});await Page(view,"home");
   Ok(await Js(view,"!document.body.innerText.includes('简易')&&!document.querySelector('.simple-home,.exit-simple,.enter-simple')")=="true","1.7 has only the full interface despite old simple-mode preference");
   Ok(await Js(view,"document.querySelector('.footer').innerText.includes('1.7')")=="true","visible version is 1.7");
   stage="contour scene";await Wait(async()=>await Js(view,"document.querySelector('.array-background').dataset.sceneReady==='true'&&document.querySelectorAll('canvas').length===1")=="true");
   Ok(await Js(view,"document.querySelector('.array-background').dataset.motion==='running'")=="true","full contour background runs by default");
   await Js(view,"window.chrome.webview.postMessage({command:'interfaceMode',simple:true})");await Task.Delay(400);Ok(await Js(view,"!!document.querySelector('.launch-panel')&&!document.querySelector('.simple-home')")=="true","retired mode command cannot restore simple mode");
   Ok(await Js(view,"!!document.querySelector('.web-settings')&&document.querySelector('.web-settings').innerText.includes('Mod 设置')")=="true","full header exposes Mod web settings");
   await Js(view,"document.querySelector('.web-settings').click()");await Wait(()=>Task.FromResult(Field<System.Collections.Generic.List<string>>(window,"rhineLogs").Any(x=>x.Contains("http://127.0.0.1:8791/ui"))));Ok(true,"web settings command reaches selected endpoint without opening browser in test");
   var connectivity=Field<Connectivity>(window,"connectivity");connectivity.Channel=LowLatencyChannel.Status("untrusted");Push(window);await Wait(async()=>await Js(view,"document.querySelector('.channel-strip').innerText.includes('证书未信任')")=="true");Ok(await Js(view,"!document.querySelector('.channel-strip').classList.contains('is-ready')")=="true","untrusted certificate stays distinct from ready channel");
   Ok(await Js(view,"document.querySelector('.channel-strip .certificate-setup')?.innerText==='配置证书'")=="true","home exposes certificate setup beside channel status");
   await Js(view,"document.querySelector('.certificate-setup').click()");await Wait(async()=>await Js(view,"document.querySelector('[role=dialog]')?.innerText.includes('BabelTower-localhost')")=="true");
   Ok(await Js(view,"document.querySelector('[role=dialog]').innerText.includes('Steam')&&document.querySelector('[role=dialog]').innerText.includes('Windows')")=="true","certificate confirmation explains local trust and complete Steam restart");
   await Js(view,"document.querySelector('.dialog-close').click()");await Wait(async()=>await Js(view,"!document.querySelector('.dialog-shade')")=="true");Ok(!Field<System.Collections.Generic.List<string>>(window,"rhineLogs").Any(x=>x.Contains("证书配置测试")),"closing confirmation does not execute certificate setup");
   await Js(view,"document.querySelector('.certificate-setup').click()");await Wait(async()=>await Js(view,"!!document.querySelector('.certificate-confirm')")=="true");await Js(view,"document.querySelector('.certificate-confirm').click()");
   await Wait(()=>Task.FromResult(Field<System.Collections.Generic.List<string>>(window,"rhineLogs").Any(x=>x.Contains("证书配置测试"))));Ok(true,"confirmed action reaches native command without modifying certificate stores in UI tests");
   stage="certificate result";
   await Wait(async()=>await Js(view,"document.querySelector('.result-copy')?.innerText.includes('测试')")=="true");await Js(view,"document.querySelector('.dialog-close').click()");stage="certificate result closed";await Wait(async()=>await Js(view,"!document.querySelector('.dialog-shade')")=="true");Ok(true,"certificate result closes cleanly without being reopened by native state clearing");
   Put(window,"busy",true);Push(window);await Wait(async()=>await Js(view,"document.querySelector('.certificate-setup').disabled")=="true");Ok(true,"certificate setup cannot overlap another operation");Put(window,"busy",false);
   connectivity.Channel=LowLatencyChannel.Status("ready");Push(window);await Wait(async()=>await Js(view,"document.querySelector('.channel-strip').classList.contains('is-ready')")=="true");Ok(await Js(view,"document.querySelector('.metadata').innerText.includes('尚未检查')")=="true","channel and main bridge status are displayed independently");
   await Js(view,"document.querySelectorAll('.secondary-actions button')[1].click()");stage="report ready";await Wait(async()=>await Js(view,"document.querySelector('.report-preview')?.innerText.includes('诊断报告')")=="true");Ok(await Js(view,"document.querySelector('.report-preview').innerText.includes('低延迟通道：本地可用')")=="true","actual report command retains the last fresh channel evidence");await Js(view,"document.querySelector('.dialog-close').click()");await Wait(async()=>await Js(view,"!document.querySelector('.dialog-shade')")=="true");connectivity.Channel=LowLatencyChannel.Status("ready");
   connectivity.Channel.CheckedUtc=DateTime.UtcNow.AddSeconds(-30);Push(window);await Wait(async()=>await Js(view,"document.querySelector('.channel-strip').innerText.includes('尚未检查')")=="true");Ok(true,"expired channel result is not shown as ready");
   Ok(await Js(view,"document.documentElement.scrollHeight<=innerHeight")=="true","home fits without an outer scrollbar");
   await Capture(view,Path.Combine(artifacts,"home-dark.png"));
   await Js(view,"document.querySelector('.theme-switch').click()");await Wait(()=>Task.FromResult(!Theme.IsDark));Ok(ThemePreferences.LoadDark()==false,"light theme remains selectable and persistent");await Capture(view,Path.Combine(artifacts,"home-light.png"));await Js(view,"document.querySelector('.theme-switch').click()");await Wait(()=>Task.FromResult(Theme.IsDark));
   await Js(view,"document.querySelector('.log-toggle').click()");await Wait(async()=>await Js(view,"!document.querySelector('.log-content').hidden")=="true");await Js(view,"document.querySelector('.log-close').click()");await Wait(async()=>await Js(view,"document.querySelector('.log-content').hidden")=="true");Ok(true,"run log panel can be closed");
   await Js(view,"document.querySelectorAll('.menu-navigation button')[1].click()");await Page(view,"mod");Ok(await Js(view,"document.querySelectorAll('.update-action-button').length===1&&document.querySelector('.update-action-button').innerText==='检查更新'&&document.querySelectorAll('[data-update]').length===2")=="true","single-action update and both source tabs are preserved");
   Ok(await Js(view,"!document.querySelector('.archive-drop')")=="true","manual archive module stays removed");
   Ok(await Js(view,"(()=>{window.__requests=[];window.__original=window.chrome.webview.postMessage;window.__capture=function(m){if(['checkMod','downloadMod','checkSoftware','downloadSoftware'].includes(m.command)){window.__requests.push(m.command);return}return window.__original.call(window.chrome.webview,m)};window.chrome.webview.postMessage=window.__capture;return window.chrome.webview.postMessage===window.__capture})()")=="true","network actions are intercepted before update test clicks");
   await Js(view,"document.querySelector('.update-action-button').click()");Ok(await Js(view,"window.__requests.at(-1)==='checkMod'")=="true","Mod check remains bound to correct source");Put(window,"officialRelease",new OfficialRelease{Version=new Version(1,2,2),Source=UpdateSource.Mod});Push(window);await Wait(async()=>await Js(view,"document.querySelector('.update-action-button').innerText==='下载并安装'")=="true");await Js(view,"document.querySelector('.update-action-button').click()");Ok(await Js(view,"window.__requests.at(-1)==='downloadMod'")=="true","new Mod switches same action to install");Put(window,"officialRelease",null);
   await Js(view,"document.querySelector('[data-update=software]').click()");await Page(view,"software");await Js(view,"document.querySelector('.update-action-button').click()");Ok(await Js(view,"window.__requests.at(-1)==='checkSoftware'")=="true","launcher updates use the launcher source");Put(window,"launcherRelease",new OfficialRelease{Version=new Version(1,8,0),Source=UpdateSource.Launcher});Push(window);await Wait(async()=>await Js(view,"document.querySelector('.update-action-button').innerText==='下载并安装'")=="true");Put(window,"launcherRelease",null);await Capture(view,Path.Combine(artifacts,"update.png"));
   await Js(view,"document.querySelector('.back-link').click()");await Page(view,"home");await Js(view,"document.querySelectorAll('.menu-navigation button')[2].click()");await Page(view,"diagnostics");connectivity.Channel=LowLatencyChannel.Status("untrusted");Push(window);await Wait(async()=>await Js(view,"document.querySelector('.channel-diagnostic').innerText.includes('Windows')")=="true");Ok(await Js(view,"document.querySelector('.channel-diagnostic').innerText.includes('Steam')")=="true","diagnostics explain certificate trust and Steam restart");await Capture(view,Path.Combine(artifacts,"diagnostics.png"));
   await Js(view,"window.chrome.webview.postMessage({command:'settings'})");await Page(view,"settings");Ok(await Js(view,"document.querySelectorAll('.path-field input').length===3&&document.querySelector('.page-settings').innerText.includes('Mod 网页设置')")=="true","path settings retain automatic detection and expose Mod settings");
   window.ClientSize=new Size(960,680);await Task.Delay(600);await Capture(view,Path.Combine(artifacts,"settings-small.png"));
   Ok(await Js(view,"document.querySelector('.page-settings .page-primary').getBoundingClientRect().bottom<innerHeight-70")=="true","save paths action fits the minimum window above footer");
   Ok(await Js(view,"[...document.querySelectorAll('.top-actions button')].every(b=>b.getBoundingClientRect().right<=innerWidth)")=="true","header controls remain within the minimum supported window");
   Console.WriteLine("TOTAL "+passed+" full native UI checks passed; no real game, bridge or browser launched.");
  }catch(Exception error){exit=1;Console.WriteLine("FAIL "+stage+" "+error);Console.WriteLine(String.Join("\n",Field<System.Collections.Generic.List<string>>(window,"rhineLogs")));}finally{window.Close();}};
  Application.Run(window);Environment.Exit(exit);
 }
}
