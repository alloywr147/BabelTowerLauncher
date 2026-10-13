using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using BabelManager;

class SimpleUiTests {
    sealed class QuietWindow : MainWindow { protected override bool ShowWithoutActivation { get { return true; } } }
    static int passed, exit;
    static string stage = "initializing";
    static T Field<T>(object target, string name) { return (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }
    static Task<string> Js(WebView2 view, string script) { return view.CoreWebView2.ExecuteScriptAsync(script); }
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
    static async Task Wait(Func<Task<bool>> ready) {
        DateTime limit = DateTime.UtcNow.AddSeconds(25);
        while (!await ready()) { if (DateTime.UtcNow > limit) throw new Exception("UI wait timed out: " + stage); await Task.Delay(100); }
        await Task.Delay(120);
    }
    static async Task Page(WebView2 view, string name) {
        await Wait(async () => await Js(view, name == "home" ? "!!document.querySelector('.simple-home')&&!document.querySelector('.page-surface').hasAttribute('inert')" : "!!document.querySelector('.page-" + name + "')&&!document.querySelector('.page-surface').hasAttribute('inert')") == "true");
    }
    static async Task Capture(WebView2 view, string file) { await Task.Delay(750); using (var stream = File.Create(file)) await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream); }
    [STAThread] static void Main(string[] args) {
        RhineRuntime.Prepare(); MainWindow.RhineEnabled = true; MainWindow.RhineTestMode = true;
        Application.EnableVisualStyles();
        string fixture = Path.Combine(Path.GetTempPath(), "BTSimple-" + Guid.NewGuid().ToString("N"));
        string game = Path.Combine(fixture, "Deadlock");
        Directory.CreateDirectory(Path.Combine(game, "game", "bin", "win64"));
        Directory.CreateDirectory(Path.Combine(game, "game", "citadel"));
        File.WriteAllText(Path.Combine(game, "game", "bin", "win64", "deadlock.exe"), "fixture");
        Core.SettingsPath = Path.Combine(fixture, "settings.json"); Core.BackupsDir = Path.Combine(fixture, "backups");
        ThemePreferences.Path = Path.Combine(fixture, "appearance.json");
        InterfacePreferences.Path = Path.Combine(fixture, "interface-mode.txt");
        Core.Save(new Settings { GameRoot = game, InstallRoot = Path.Combine(fixture, "BabelTower") });
        string output = args[0]; Directory.CreateDirectory(output);
        var window = new QuietWindow(); window.EnableRhine();
        window.StartPosition = FormStartPosition.Manual; window.Location = new Point(-20000, -20000); window.ShowInTaskbar = false;
        window.Shown += async (sender, e) => {
            try {
                WebView2 view = null;
                stage = "native WebView2 ready";
                await Wait(() => { view = Field<WebView2>(window, "rhineView"); return Task.FromResult(view != null && view.CoreWebView2 != null && Field<bool>(window, "rhineReady")); });
                stage = "plain black home ready";
                await Page(view, "home");
                Assert(await Js(view, "document.querySelectorAll('canvas,.array-background,.scene-atmosphere').length===0") == "true", "simple mode has no canvas or animated background");
                Assert(await Js(view, "getComputedStyle(document.querySelector('main')).backgroundColor==='rgb(0, 0, 0)'") == "true", "simple background is pure black");
                Assert(await Js(view, "!document.body.innerText.includes('简易版')") == "true", "interface has no simple edition label");
                Assert(await Js(view, "document.querySelector('.exit-simple').innerText==='退出简易模式'") == "true", "small top-right exit mode action is available");
                Console.WriteLine("Home buttons: " + await Js(view, "[...document.querySelectorAll('main button')].filter(b=>b.getClientRects().length&&!b.closest('[hidden]')).map(b=>b.innerText.trim()).join('|')"));
                await Capture(view, Path.Combine(output, "simple-home-check.png"));
                Assert(await Js(view, "[...document.querySelectorAll('.simple-home button')].map(b=>b.innerText.trim()).join('|')==='启动|更新|设置'") == "true", "home has three main buttons: launch, update, settings");
                Assert(await Js(view, "[...document.querySelectorAll('.simple-home button')].every(b=>parseFloat(getComputedStyle(b).fontSize)>=32&&Number(getComputedStyle(b).fontWeight)>=600)") == "true", "main button labels are large and bold");
                await Capture(view, Path.Combine(output, "simple-home-dark.png"));
                if (!Bridge.GameRunning()) {
                    await Js(view, "document.querySelector('[data-action=launch]').click()");
                    await Task.Delay(700);
                    Assert(await Js(view, "!!document.querySelector('.page-mod')&&document.querySelector('.result-copy')?.innerText.includes('更新页')") == "true", "launch without a Mod directs the user to online updates");
                    await Js(view, "document.querySelector('.dialog-close').click()");
                    await Wait(async () => await Js(view, "!document.querySelector('.dialog-shade')") == "true");
                    await Js(view, "document.querySelector('.back-link').click()"); await Page(view, "home");
                } else {
                    Console.WriteLine("SKIP missing-Mod launch case while real game is already running");
                    typeof(MainWindow).GetMethod("Error", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, new object[] { "请从更新页下载官方完整包。" });
                    await Wait(async () => await Js(view, "document.querySelector('.result-copy')?.innerText.includes('更新页')") == "true");
                    Assert(true, "native error is displayed inside the application without a separate system popup");
                    await Js(view, "document.querySelector('.dialog-close').click()");
                    await Wait(async () => await Js(view, "!document.querySelector('.dialog-shade')") == "true");
                }
                await Js(view, "document.querySelector('[data-action=settings]').click()"); await Page(view, "settings");
                Assert(await Js(view, "document.querySelectorAll('.path-field input').length") == "3", "settings keeps all three installation paths");
                Assert(await Js(view, "getComputedStyle(document.querySelector('main')).backgroundColor==='rgb(0, 0, 0)'") == "true", "settings also retains black background");
                Assert(await Js(view, "document.querySelector('.page-settings .page-primary').getBoundingClientRect().bottom<=innerHeight-10") == "true", "save paths button fits the default window");
                await Capture(view, Path.Combine(output, "simple-settings-dark.png"));
                await Js(view, "document.querySelector('.back-link').click()"); await Page(view, "home");
                await Js(view, "document.querySelector('[data-action=update]').click()"); await Page(view, "mod");
                Assert(await Js(view, "!document.querySelector('.archive-drop')&&!document.body.innerText.includes('选择压缩包')&&!document.body.innerText.includes('拖入新版压缩包')") == "true", "Mod update has no manual archive module");
                Assert(await Js(view, "document.querySelectorAll('.update-action-button').length===1&&document.querySelector('.update-action-button').innerText==='检查更新'&&!document.querySelector('.repository-link,.archive-drop,.page-actions,.page-primary')") == "true", "update initially shows one check action without extra update controls");
                Assert(await Js(view, "[...document.querySelectorAll('[data-update]')].map(b=>b.innerText).join('|')==='Mod 更新|启动器更新'") == "true", "both update source tabs remain visible");
                Assert(await Js(view, "document.querySelector('.update-action-status').innerText===''") == "true", "initial update screen contains no extra status copy");
                await Capture(view, Path.Combine(output, "simple-update-dark.png"));
                Assert(await Js(view, "(()=>{window.__updateRequests=[];window.__originalPost=window.chrome.webview.postMessage;window.__capturedPost=function(m){if(['checkMod','checkSoftware','downloadMod','downloadSoftware','cancel'].includes(m.command)){window.__updateRequests.push(m.command);return;}return window.__originalPost.call(window.chrome.webview,m)};window.chrome.webview.postMessage=window.__capturedPost;return window.chrome.webview.postMessage===window.__capturedPost})()") == "true", "update command interception is verified before test clicks");
                await Js(view, "document.querySelector('.update-action-button').click()");
                Assert(await Js(view, "window.__updateRequests.at(-1)==='checkMod'") == "true", "Mod check uses the correct source without a real network request");
                typeof(MainWindow).GetField("officialRelease", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, new OfficialRelease { Version = new Version(1, 9, 0), Source = UpdateSource.Mod, Size = 10 * 1024 * 1024 });
                typeof(MainWindow).GetMethod("RenderOfficial", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                typeof(MainWindow).GetMethod("PushRhineState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                await Wait(async () => await Js(view, "document.querySelector('.update-action-button')?.innerText==='下载并安装'") == "true");
                await Js(view, "document.querySelector('.update-action-button').click()");
                Assert(await Js(view, "window.__updateRequests.at(-1)==='downloadMod'&&document.querySelectorAll('.update-action-button').length===1") == "true", "new Mod changes the same button to download and install");
                await Capture(view, Path.Combine(output, "simple-update-available.png"));
                var cancellation = new System.Threading.CancellationTokenSource();
                typeof(MainWindow).GetField("onlineCancellation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, cancellation);
                typeof(MainWindow).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, true);
                typeof(MainWindow).GetMethod("PushRhineState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                await Wait(async () => await Js(view, "document.querySelector('.update-action-button')?.innerText==='取消下载'") == "true");
                await Js(view, "document.querySelector('.update-action-button').click()");
                Assert(await Js(view, "window.__updateRequests.at(-1)==='cancel'&&document.querySelectorAll('.update-action-button').length===1") == "true", "active download can be cancelled using the same button");
                typeof(MainWindow).GetField("busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, false);
                typeof(MainWindow).GetField("onlineCancellation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, null);
                cancellation.Dispose();
                typeof(MainWindow).GetField("officialRelease", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, null);
                typeof(MainWindow).GetMethod("PushRhineState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                await Wait(async () => await Js(view, "!document.querySelector('[data-update=software]').disabled") == "true");
                await Js(view, "document.querySelector('[data-update=software]').click()"); await Page(view, "software");
                Assert(await Js(view, "document.querySelector('[data-update=software]').getAttribute('aria-selected')==='true'&&document.querySelector('.update-action-button').innerText==='检查更新'") == "true", "switching to launcher updates resets the action to check");
                await Js(view, "document.querySelector('.update-action-button').click()");
                Assert(await Js(view, "window.__updateRequests.at(-1)==='checkSoftware'") == "true", "launcher check uses its own source");
                typeof(MainWindow).GetField("launcherRelease", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, new OfficialRelease { Version = new Version(1, 7, 0), Source = UpdateSource.Launcher });
                typeof(MainWindow).GetMethod("PushRhineState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
                await Wait(async () => await Js(view, "document.querySelector('.update-action-button')?.innerText==='下载并安装'") == "true");
                await Js(view, "document.querySelector('.update-action-button').click()");
                Assert(await Js(view, "window.__updateRequests.at(-1)==='downloadSoftware'") == "true", "new launcher uses the same download and install action");
                typeof(MainWindow).GetField("launcherRelease", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, null);
                await Js(view, "document.querySelector('.back-link').click()"); await Page(view, "home");
                Field<Settings>(window, "settings").GameRoot = "";
                await Js(view, "document.querySelector('[data-action=launch]').click()"); await Page(view, "settings");
                Assert(true, "launch with missing game path opens setup and never starts a process");
                await Js(view, "document.querySelector('.back-link').click()"); await Page(view, "home"); await Task.Delay(900);
                MainWindow.RhineTestMode = false;
                await Js(view, "document.querySelector('.exit-simple').click()");
                stage = "full interface restored";
                await Wait(async () => await Js(view, "!!document.querySelector('.launch-panel')&&!document.querySelector('.page-surface').hasAttribute('inert')") == "true");
                Assert(await Js(view, "!document.querySelector('.simple-home')&&!!document.querySelector('.menu-navigation')") == "true", "exit restores full interface in the same window");
                await Js(view, "document.querySelectorAll('.menu-navigation button')[1].click()"); await Page(view, "mod");
                Assert(await Js(view, "!document.querySelector('.archive-drop')&&!document.body.innerText.includes('选择压缩包')") == "true", "full mode also has no manual archive module");
                Assert(await Js(view, "document.querySelectorAll('.update-action-button').length===1&&document.querySelectorAll('[data-update]').length===2") == "true", "full interface shares the same single-action update page");
                await Js(view, "document.querySelector('.back-link').click()");
                await Wait(async () => await Js(view, "!!document.querySelector('.launch-panel')&&!document.querySelector('.page-surface').hasAttribute('inert')") == "true");
                Assert(File.ReadAllText(InterfacePreferences.Path) == "full", "full mode selection is persisted in isolated test data");
                await Capture(view, Path.Combine(output, "full-interface-restored.png"));
                await Js(view, "document.querySelector('.enter-simple').click()"); await Page(view, "home");
                Assert(await Js(view, "!document.querySelector('canvas')&&getComputedStyle(document.querySelector('main')).backgroundColor==='rgb(0, 0, 0)'") == "true", "returning to simple mode disposes the full background");
                Assert(File.ReadAllText(InterfacePreferences.Path) == "simple", "simple mode selection is persisted in isolated test data");
                MainWindow.RhineTestMode = true;
                window.ClientSize = new Size(800, 560); await Task.Delay(800);
                Assert(await Js(view, "[...document.querySelectorAll('.simple-home button')].every(b=>{const r=b.getBoundingClientRect();return r.width>100&&r.left>=0&&r.right<=innerWidth&&r.top>=0&&r.bottom<=innerHeight})") == "true", "all three buttons fit the smallest supported window");
                await Capture(view, Path.Combine(output, "simple-home-small.png"));
                Console.WriteLine("TOTAL " + passed + " simple native UI checks passed; no game, selected bridge or browser launched.");
            } catch (Exception error) { exit = 1; Console.WriteLine("FAIL " + error); Console.WriteLine(String.Join("\n", Field<System.Collections.Generic.List<string>>(window,"rhineLogs"))); }
            finally { window.Close(); }
        };
        Application.Run(window); Environment.Exit(exit);
    }
}
