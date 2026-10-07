using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using BabelManager;

class SettingsTransitionTests {
    sealed class QuietWindow : MainWindow { protected override bool ShowWithoutActivation { get { return true; } } }
    static int passed, failed;
    static string stage = "initializing";
    static void Assert(bool condition, string message) {
        if (!condition) { failed++; throw new Exception(message); }
        passed++; Console.WriteLine("PASS " + message);
    }
    static T Field<T>(object target, string name) { return (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target); }
    static async Task<string> Js(WebView2 view, string script) { return await view.CoreWebView2.ExecuteScriptAsync(script); }
    static async Task Wait(Func<Task<bool>> ready) {
        DateTime limit = DateTime.UtcNow.AddSeconds(30);
        while (!await ready()) { if (DateTime.UtcNow > limit) throw new Exception("Native webview timed out: " + stage); await Task.Delay(100); }
    }
    static async Task Capture(WebView2 view, string path) {
        using (var file = File.Create(path)) await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, file);
    }
    [STAThread] static void Main(string[] args) {
        RhineRuntime.Prepare();
        MainWindow.RhineEnabled = true;
        MainWindow.RhineTestMode = true;
        Application.EnableVisualStyles();
        string fixture = Path.Combine(Path.GetTempPath(), "BT15-webhost-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        Core.SettingsPath = Path.Combine(fixture, "settings.json");
        Core.BackupsDir = Path.Combine(fixture, "backups");
        ThemePreferences.Path = Path.Combine(fixture, "appearance.json");
        string game = Path.Combine(fixture, "Deadlock"), install = Path.Combine(fixture, "BabelTower"), package = Path.Combine(install, "BabelTower-108");
        Directory.CreateDirectory(Path.Combine(game, "game", "bin", "win64"));
        Directory.CreateDirectory(Path.Combine(game, "game", "citadel"));
        File.WriteAllText(Path.Combine(game, "game", "bin", "win64", "deadlock.exe"), "");
        Directory.CreateDirectory(Path.Combine(package, "core"));
        Directory.CreateDirectory(Path.Combine(package, "portable-node"));
        File.WriteAllText(Path.Combine(package, "core", "bridge_server.js"), "");
        File.WriteAllText(Path.Combine(package, "portable-node", "node.exe"), "");
        File.WriteAllText(Path.Combine(package, "pak01_dir.vpk"), "fixture");
        File.WriteAllText(Path.Combine(package, "VERSION"), "1.0.8");
        File.WriteAllText(Path.Combine(game, "game", "citadel", "gameinfo.gi"), "GameInfo { FileSystem { SearchPaths { Game citadel } } }");
        Core.Save(new Settings { GameRoot = game, InstallRoot = install, CurrentFolder = package });
        string output = args.Length > 0 ? args[0] : AppDomain.CurrentDomain.BaseDirectory;
        Directory.CreateDirectory(output);
        var window = new QuietWindow();
        window.EnableRhine();
        window.StartPosition = FormStartPosition.Manual;
        window.Location = new Point(-20000, -20000);
        window.ShowInTaskbar = false;

        window.Shown += async (sender, e) => {
            try {
                WebView2 view = null;
                await Wait(() => { view = Field<WebView2>(window, "rhineView"); return Task.FromResult(view != null && view.CoreWebView2 != null && Field<bool>(window, "rhineReady")); });
                await Wait(async () => await Js(view, "document.querySelector('.array-background')?.dataset.sceneReady==='true'") == "true");
                foreach (var size in new [] { new Size(1280, 800), new Size(960, 680) }) {
                    window.ClientSize = size;
                    foreach (bool light in new [] { false, true }) {
                        await Js(view, "window.chrome.webview.postMessage({command:'home'})");
                        await Wait(async () => await Js(view, "document.querySelector('.launch-panel')!==null") == "true");
                        if (Theme.IsDark == light) {
                            await Js(view, "document.querySelector('.theme-switch').click()");
                            await Task.Delay(1800);
                        }
                        foreach (bool paused in new [] { false }) {
                            await Js(view, "window.chrome.webview.postMessage({command:'home'})");
                            await Wait(async () => await Js(view, "document.querySelector('.launch-panel')!==null") == "true");
                            await Task.Delay(1800);
                            string label = size.Width + "x" + size.Height + (light ? " light" : " dark") + (paused ? " paused" : " animated");
                            string recording = @"(() => {
                                const canvas=document.querySelector('.array-background canvas');
                                const before={width:canvas.width,height:canvas.height,view:document.querySelector('.launcher').getBoundingClientRect().width};
                                const samples=[];let start=performance.now();
                                const frame=t=>{samples.push({time:Math.round(t-start),width:canvas.width,height:canvas.height,view:document.querySelector('.launcher').getBoundingClientRect().width,hidden:document.querySelector('.page-surface').hidden,transition:document.querySelector('.page-surface').dataset.transition});if(t-start<1300)requestAnimationFrame(frame);else{window.settingsFlashSamples=samples;window.settingsFlashStable=samples.every(s=>s.width===before.width&&s.height===before.height&&s.view===before.view);window.settingsFlashDone=true;}};
                                window.settingsFlashDone=false;requestAnimationFrame(frame);
                                [...document.querySelectorAll('.utility-link')].find(b=>b.textContent==='设置').click();
                            })()";
                            await Js(view, recording);
                            await Wait(async () => await Js(view, "window.settingsFlashDone") == "true");
                            string timeline = await Js(view, "JSON.stringify(settingsFlashSamples)");
                            File.WriteAllText(Path.Combine(output, "settings-" + label.Replace(' ', '-') + ".json"), timeline);
                            Console.WriteLine(label + " distinct sizes: " + await Js(view, "JSON.stringify([...new Set(settingsFlashSamples.map(s=>s.width+'x'+s.height+'/'+s.view))])"));
                            await Capture(view, Path.Combine(output, "settings-" + label.Replace(' ', '-') + ".png"));
                            bool stable = await Js(view, "window.settingsFlashStable") == "true";
                            if (!stable) { failed++; Console.WriteLine("FAIL " + label + " settings navigation resets the background canvas or viewport width"); }
                            else { passed++; Console.WriteLine("PASS " + label + " settings navigation keeps background canvas and viewport width stable"); }
                            Assert(await Js(view, "document.querySelectorAll('.path-field input').length===3 && !document.querySelector('.page-surface').inert") == "true", label + " settings remains usable");
                        }
                    }
                }
            } catch(Exception error) { failed++; Console.WriteLine("FAIL " + error); }
            File.WriteAllText(Path.Combine(output, "settings-result.txt"), "Settings transition: " + passed + " passed, " + failed + " failed");
            window.Close();
        };
        Application.Run(window);
        if (!Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new Exception("unsafe fixture");
        Directory.Delete(fixture, true);
        Console.WriteLine("SETTINGS RESULT " + passed + " passed, " + failed + " failed");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
}
