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

class WebHostTests {
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
    static async Task CaptureMotionPreview(WebView2 view, string output) {
        string frames = Path.Combine(output, "motion-preview-frames");
        Directory.CreateDirectory(frames);
        var timestamps = new System.Collections.Generic.List<string>();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int frame = 0, step = 0;
        int[] cues = { 400, 1600, 2800, 4200, 5600 };
        string[] actions = {
            "window.chrome.webview.postMessage({command:'diagnostics'})",
            "window.chrome.webview.postMessage({command:'mod'})",
            "window.chrome.webview.postMessage({command:'home'})",
            "[...document.querySelectorAll('.secondary-actions button')][0].click()",
            "document.querySelector('.dialog-close').click()"
        };
        while (clock.ElapsedMilliseconds < 6600) {
            long started = clock.ElapsedMilliseconds;
            if (step < cues.Length && started >= cues[step]) await Js(view, actions[step++]);
            await Js(view, "document.dispatchEvent(new PointerEvent('pointermove',{clientX:innerWidth*(.26+.08*Math.sin(performance.now()/1300)),clientY:innerHeight*(.47+.07*Math.sin(performance.now()/1100)),pointerType:'mouse',bubbles:true}))");
            string name = "frame-" + frame.ToString("D4") + ".jpg";
            timestamps.Add(name + "|" + clock.ElapsedMilliseconds);
            using (var file = File.Create(Path.Combine(frames, name)))
                await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Jpeg, file);
            frame++;
            int remaining = (int)(50 - (clock.ElapsedMilliseconds - started));
            if (remaining > 0) await Task.Delay(remaining);
        }
        File.WriteAllLines(Path.Combine(frames, "timestamps.txt"), timestamps);
        Console.WriteLine("Motion recording: " + frame + " frames in " + clock.ElapsedMilliseconds + " ms");
        await Js(view, "document.dispatchEvent(new PointerEvent('pointerout',{relatedTarget:null,bubbles:true}))");
    }
    static async Task DetailFits(WebView2 view, string label) {
        await Task.Delay(900); // Measure after the entrance animation and native status push.
        string geometry = "(() => {const page=document.querySelector('.detail-page');const rows=[...page.children].filter(e=>e.getBoundingClientRect().height>0);const bottom=Math.max(...rows.map(e=>e.getBoundingClientRect().bottom));const nav=document.querySelector('.menu-navigation').getBoundingClientRect();const controls=[...page.querySelectorAll('button')];return {height:innerHeight,documentHeight:document.documentElement.scrollHeight,bottom,navTop:nav.top,fit:document.documentElement.scrollHeight<=innerHeight+1 && bottom<=nav.top-12 && controls.every(b=>{const r=b.getBoundingClientRect();return r.top>=0 && r.bottom<=nav.top-12 && r.right<=innerWidth && parseFloat(getComputedStyle(b).fontSize)>=14})};})()";
        Console.WriteLine(label + " layout: " + await Js(view, "JSON.stringify(" + geometry + ")"));
        Assert(await Js(view, geometry + ".fit") == "true", label + " fits one screen with readable controls above navigation");
    }
    static async Task ReopenUi(WebView2 view) {
        var completed = new TaskCompletionSource<bool>();
        EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = (sender, e) => completed.TrySetResult(e.IsSuccess);
        view.CoreWebView2.NavigationCompleted += handler;
        try {
            view.CoreWebView2.Reload();
            if (await Task.WhenAny(completed.Task, Task.Delay(30000)) != completed.Task || !await completed.Task) throw new Exception("UI reopen failed");
            await Wait(async () => (await Js(view, "document.querySelector('.array-background')?.dataset.sceneReady === 'true'")) == "true");
        } finally { view.CoreWebView2.NavigationCompleted -= handler; }
    }
    static async Task BackgroundMoves(WebView2 view, string page, string output) {
        await Task.Delay(2000); // Page and theme entrance must settle before comparing pixels.
        int before = Int32.Parse((await Js(view, "Number(document.querySelector('.array-background').dataset.drawnFrames)")).Trim('"'));
        string first = Path.Combine(output, page + "-motion-a.png"), second = Path.Combine(output, page + "-motion-b.png");
        await Capture(view, first);
        await Task.Delay(1100);
        await Capture(view, second);
        int after = Int32.Parse((await Js(view, "Number(document.querySelector('.array-background').dataset.drawnFrames)")).Trim('"'));
        int changed = 0;
        using (var a = new Bitmap(first)) using (var b = new Bitmap(second)) {
            for (int y = a.Height / 3; y < a.Height * 3 / 4; y += 3)
                for (int x = 20; x < a.Width / 3; x += 3) {
                    Color ca = a.GetPixel(x, y), cb = b.GetPixel(x, y);
                    if (Math.Abs(ca.R - cb.R) + Math.Abs(ca.G - cb.G) + Math.Abs(ca.B - cb.B) > 8) changed++;
                }
        }
        Console.WriteLine(page + " actual renders=" + (after - before) + ", changed background pixels=" + changed);
        Assert(after - before > 20 && changed > 30, page + " background visibly animates after page entrance settles");
        int hoveredBefore = Int32.Parse((await Js(view, "Number(document.querySelector('.array-background').dataset.hoverCount||0)")).Trim('"'));
        double peakLift = 0;
        for (int sample = 0; sample < 12; sample++) {
            await Js(view, "(() => { const target=document.querySelector('.detail-page') || document.querySelector('.launch-panel');target.dispatchEvent(new PointerEvent('pointermove',{clientX:innerWidth*" + (.2 + sample * .015).ToString(System.Globalization.CultureInfo.InvariantCulture) + ",clientY:innerHeight*" + (.43 + sample % 3 * .06).ToString(System.Globalization.CultureInfo.InvariantCulture) + ",pointerType:'mouse',bubbles:true})); })()");
            await Task.Delay(200);
            double lift = Double.Parse((await Js(view, "Number(document.querySelector('.array-background').dataset.lift)")).Trim('"'), System.Globalization.CultureInfo.InvariantCulture);
            peakLift = Math.Max(peakLift, lift);
        }
        int hoveredAfter = Int32.Parse((await Js(view, "Number(document.querySelector('.array-background').dataset.hoverCount||0)")).Trim('"'));
        Console.WriteLine(page + " hover changes=" + (hoveredAfter - hoveredBefore) + ", peak lift=" + peakLift);
        Assert(hoveredAfter > hoveredBefore && peakLift > .04 && peakLift <= .221, page + " foreground pointer events reach a smooth bounded background hover");
        await Js(view, "document.dispatchEvent(new PointerEvent('pointerout',{relatedTarget:null,bubbles:true}))");
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
            bool legacyHiddenAtFirstShow = !Field<Panel>(window, "body").Visible && Field<System.Collections.Generic.List<Control>>(window, "rhineFallbackControls").All(control => !control.Visible);
            try {
                WebView2 view = null;
                stage = "native navigation";
                await Wait(() => { view = Field<WebView2>(window, "rhineView"); return Task.FromResult(view != null && view.CoreWebView2 != null && Field<bool>(window, "rhineReady")); });
                Console.WriteLine("System reduced motion before emulation: " + await Js(view, "matchMedia('(prefers-reduced-motion: reduce)').matches"));
                Assert(await Js(view, "document.querySelector('.launcher').dataset.uiMotion==='running'") == "true", "explicit startup motion preference enables both foreground and background");
                await Wait(async () => (await Js(view, "document.querySelector('.launcher').dataset.uiMotion==='running'")) == "true");
                Assert(legacyHiddenAtFirstShow, "legacy UI is hidden before first window paint");
                stage = "GLB scene";
                await Wait(async () => (await Js(view, "document.querySelector('.array-background').dataset.sceneReady === 'true'")) == "true");
                stage = "native status binding";
                await Wait(async () => (await Js(view, "document.querySelector('.mod-version')?.textContent === '1.0.8'")) == "true");
                Assert(await Js(view, "document.querySelector('.metadata').textContent.includes('尚未检查') && !document.querySelector('.ready-label').textContent.includes('准备就绪')") == "true", "native bridge sends actual untested state, no fake green status");
                Assert(await Js(view, "document.querySelector('.footer').textContent.includes('1.5') && !document.querySelector('.footer').textContent.includes('浏览器视图')") == "true", "native runtime binds production UI");
                await Js(view, "Promise.all([document.fonts.load('400 16px MiSans','巴别塔启动器 本地桥服务'),document.fonts.load('700 27px MiSans','巴别塔启动器'),document.fonts.load('300 43px MiSans','0123456789')]).then(groups=>document.body.dataset.rhineFonts=groups.every(faces=>faces.length>0 && faces.every(face=>face.status==='loaded'))?'loaded':'fallback').catch(()=>document.body.dataset.rhineFonts='error')");
                await Wait(async () => (await Js(view, "document.body.dataset.rhineFonts != null")) == "true");
                Assert(await Js(view, "document.body.dataset.rhineFonts==='loaded'") == "true", "original Rhine MiSans regular, bold and light fonts load locally without fallback");
                await Js(view, "localStorage.setItem('babel-motion','paused')");
                await ReopenUi(view);
                Assert(await Js(view, "document.querySelector('.array-background').dataset.motion==='running' && document.querySelector('.background-tools button')===null") == "true", "reopening defaults motion on even with an old saved pause preference");
                Assert(await Js(view, "document.querySelector('.background-tools button')===null && !document.body.innerText.includes('暂停动效')") == "true", "footer has no motion pause control");
                await ReopenUi(view);
                Assert(await Js(view, "document.querySelector('.array-background').dataset.motion==='running'") == "true", "reopening keeps motion enabled without a pause control");
                await Task.Delay(2200);
                await Js(view, "document.querySelector('.array-background canvas').dataset.testIdentity='persistent-background';");
                await Task.Delay(3000);
                Console.WriteLine("Observed scene fps: " + await Js(view, "document.querySelector('.array-background').dataset.renderFps"));
                await Js(view, "document.querySelector('.log-toggle').click()");
                await Task.Delay(500);
                Assert(await Js(view, "document.querySelector('.log-close') !== null && !document.querySelector('#log-content').hidden") == "true", "expanded runtime log has an independent close button");
                await Js(view, "document.querySelector('.log-close').click()");
                await Task.Delay(400);
                Assert(await Js(view, "document.querySelector('#log-content').hidden && document.querySelector('.log-toggle').getAttribute('aria-expanded')==='false'") == "true", "runtime log close returns to collapsed state");
                await Js(view, "document.querySelector('.log-toggle').click()");
                await Task.Delay(400);
                await Js(view, "document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))");
                await Task.Delay(400);
                Assert(await Js(view, "document.querySelector('#log-content').hidden") == "true", "Escape closes the runtime log");
                await Js(view, "document.querySelector('.log-toggle').click()");
                await Task.Delay(400);
                await Js(view, "document.body.dispatchEvent(new PointerEvent('pointerdown',{bubbles:true}))");
                await Task.Delay(400);
                Assert(await Js(view, "document.querySelector('#log-content').hidden") == "true", "clicking outside closes the runtime log");
                await BackgroundMoves(view, "home", output);
                await Capture(view, Path.Combine(output, "native-home-dark.png"));
                await Js(view, "(() => {const action=[...document.querySelectorAll('.secondary-actions button')][0];action.focus();action.click();})()");
                await Wait(async () => (await Js(view, "document.querySelector('.terminal-dialog')?.textContent.includes('一键修复')")) == "true");
                await Task.Delay(650);
                Assert(await Js(view, "document.querySelector('.terminal-top')!==null && document.activeElement===document.querySelector('.dialog-close')") == "true", "terminal repair dialog has upstream framing and focuses its close control");
                await Capture(view, Path.Combine(output, "native-repair-dark.png"));
                Assert(await Js(view, "(() => {document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));const shade=document.querySelector('.dialog-shade');return shade?.dataset.transition==='closing' && shade.inert;})()") == "true", "terminal close animates while preventing duplicate actions");
                await Wait(async () => (await Js(view, "document.querySelector('.dialog-shade')===null")) == "true");
                Assert(await Js(view, "document.activeElement===[...document.querySelectorAll('.secondary-actions button')][0]") == "true", "terminal dismissal restores focus to its originating action");
                await Js(view, "document.querySelector('.theme-switch').click()");
                await Wait(async () => (await Js(view, "document.documentElement.dataset.previewTheme === 'light'")) == "true");
                Assert(!Theme.IsDark, "theme command crosses web/native bridge");
                await Task.Delay(1500);
                await Capture(view, Path.Combine(output, "native-home-light.png"));
                await Js(view, "[...document.querySelectorAll('.secondary-actions button')][0].click()");
                await Task.Delay(650);
                await Capture(view, Path.Combine(output, "native-repair-light.png"));
                await Js(view, "document.querySelector('.dialog-close').click()");
                await Wait(async () => (await Js(view, "document.querySelector('.dialog-shade')===null")) == "true");
                await Js(view, "[...document.querySelectorAll('button')].find(b => b.textContent.includes('软件更新')).click()");
                await Wait(() => Task.FromResult(Field<string>(window, "rhinePage") == "software"));
                await Wait(async () => (await Js(view, "document.querySelector('.detail-page h1')?.textContent === '启动器更新'")) == "true");
                Assert(Field<bool>(window, "softwarePage"), "software navigation binds existing updater");
                Assert(await Js(view, "document.querySelector('.array-background canvas').dataset.testIdentity==='persistent-background'") == "true", "software page preserves the live 3D scene");
                await Task.Delay(900);
                await Capture(view, Path.Combine(output, "native-software.png"));
                await BackgroundMoves(view, "software", output);
                await Js(view, "document.body.dataset.exitSafe='waiting';document.body.dataset.counterObserved='waiting';new MutationObserver(()=>{const pane=document.querySelector('.page-surface');if(pane.dataset.transition==='closing') document.body.dataset.exitSafe=String(pane.inert && document.querySelector('.detail-page h1')?.textContent==='启动器更新');}).observe(document.querySelector('.page-surface'),{attributes:true,attributeFilter:['data-transition','inert']});new MutationObserver(()=>{const strips=[...document.querySelectorAll('.page-counter .digit-strip')];strips.forEach(strip=>getComputedStyle(strip).transform);if(strips.some(strip=>strip.getAnimations().some(animation=>animation.playState==='running')))document.body.dataset.counterObserved='animated';}).observe(document.querySelector('.page-counter b'),{attributes:true,subtree:true,attributeFilter:['style']})");
                await Js(view, "[...document.querySelectorAll('.menu-navigation button')][1].click()");
                await Wait(() => Task.FromResult(Field<bool>(window, "importPage")));
                await Wait(async () => (await Js(view, "document.querySelector('.archive-drop') !== null")) == "true");
                Assert(await Js(view, "document.body.dataset.exitSafe==='true'") == "true", "outgoing page fades first and cannot invoke actions in the new native context");
                Assert(await Js(view, "document.querySelector('.page-counter .rolling-number').getAttribute('aria-label')==='02' && document.body.dataset.counterObserved==='animated'") == "true", "page counter rolls to its actual new value");
                Assert(Field<string>(window, "rhinePage") == "mod", "mod page keeps drag-and-drop and official update actions");
                Assert(await Js(view, "document.querySelector('.array-background canvas').dataset.testIdentity==='persistent-background'") == "true", "Mod page preserves the live 3D scene");
                await Task.Delay(900);
                await Capture(view, Path.Combine(output, "native-mod.png"));
                await DetailFits(view, "Mod default window");
                window.ClientSize = new Size(960, 680);
                await DetailFits(view, "Mod minimum window");
                await Capture(view, Path.Combine(output, "native-mod-small.png"));
                // Simulate progress through existing native controls; never start a real download.
                var progressLabel = Field<Label>(window, "downloadStatus");
                var progressBar = Field<ProgressBar>(window, "downloadProgress");
                string originalProgressText = progressLabel.Text;
                int originalProgress = progressBar.Value;
                var originalCancellation = Field<System.Threading.CancellationTokenSource>(window, "onlineCancellation");
                using (var cancellation = new System.Threading.CancellationTokenSource()) {
                    progressLabel.Text = "正在下载新版完整 Windows 包 · 50%";
                    progressBar.Value = 50;
                    typeof(MainWindow).GetField("onlineCancellation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, cancellation);
                    await Wait(async () => (await Js(view, "document.querySelector('.download-state button')?.textContent==='取消下载'")) == "true");
                    await DetailFits(view, "Mod download and cancel at minimum window");
                    Assert(await Js(view, "document.querySelector('.download-percent .rolling-number').getAttribute('aria-label')==='50' && document.querySelector('progress').value===50") == "true", "rolling download number and accessible progress share the actual native value");
                    await Capture(view, Path.Combine(output, "native-mod-download-small.png"));
                    progressLabel.Text = originalProgressText;
                    progressBar.Value = originalProgress;
                    typeof(MainWindow).GetField("onlineCancellation", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, originalCancellation);
                }
                window.ClientSize = new Size(1280, 800);
                await BackgroundMoves(view, "mod", output);
                Assert(await Js(view, "document.querySelector('.array-background').dataset.motion==='running' && document.querySelector('.launcher').dataset.uiMotion==='running'") == "true", "background and foreground motion remain enabled on detail pages");
                await Js(view, "[...document.querySelectorAll('button')].find(b => b.textContent === '导出报告')?.click();");
                // Go home first, then use the real report action.
                await Js(view, "document.querySelector('.menu-navigation button').click()");
                await Wait(() => Task.FromResult(Field<string>(window, "rhinePage") == "home"));
                await Wait(async () => (await Js(view, "document.querySelector('.launch-panel') !== null")) == "true");
                await Js(view, "[...document.querySelectorAll('.secondary-actions button')][1].click()");
                await Wait(() => Task.FromResult(!String.IsNullOrEmpty(Field<string>(window, "rhineReport"))));
                await Wait(async () => (await Js(view, "document.querySelector('.report-preview')?.textContent.includes('诊断报告')")) == "true");
                Assert(!Field<string>(window, "rhineReport").Contains(fixture), "native report preview excludes private fixture paths");
                await Task.Delay(650);
                await Capture(view, Path.Combine(output, "native-report.png"));
                Assert(!Directory.Exists(Path.Combine(game, "game", "citadel", "addons")), "UI and report tests never mutate game files");
                Assert(RhineOriginTest(), "native commands reject remote and lookalike origins");
                await Js(view, "document.querySelector('.dialog-close').click()");
                window.ClientSize = new Size(960, 680);
                await Task.Delay(900);
                Assert(await Js(view, "[...document.querySelectorAll('.topbar button,.launch-button,.secondary-actions button,.menu-navigation button,.background-tools button')].every(b=>{const r=b.getBoundingClientRect();return r.top>=0 && r.left>=0 && r.right<=innerWidth && r.bottom<=innerHeight})") == "true", "minimum window keeps all home actions visible");
                await Capture(view, Path.Combine(output, "native-home-small.png"));
                await Js(view, "[...document.querySelectorAll('button')].find(b=>b.textContent==='设置').click()");
                await Wait(async () => (await Js(view, "document.querySelectorAll('.path-field input').length === 3")) == "true");
                await Js(view, "const input=document.querySelectorAll('.path-field input')[2];Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'');input.dispatchEvent(new Event('input',{bubbles:true}));document.querySelector('.theme-switch').click()");
                await Task.Delay(300);
                Assert(await Js(view, "document.querySelectorAll('.path-field input')[2].value === ''") == "true", "theme changes preserve unsaved path draft in production UI");
                Assert(await Js(view, "document.querySelector('.array-background canvas').dataset.testIdentity==='persistent-background'") == "true", "settings page preserves the live 3D scene");
                await BackgroundMoves(view, "settings", output);
                await Js(view, "document.querySelector('.page-primary').scrollIntoView({block:'center'})");
                Assert(await Js(view, "const r=document.querySelector('.page-primary').getBoundingClientRect();r.top>=0 && r.bottom<=innerHeight && getComputedStyle(document.querySelector('.detail-page')).pointerEvents==='auto'") == "true", "long settings page remains scrollable and actionable");
                await Capture(view, Path.Combine(output, "native-settings-small.png"));
                await Js(view, "[...document.querySelectorAll('.menu-navigation button')][2].click()");
                await Wait(() => Task.FromResult(Field<string>(window, "rhinePage") == "diagnostics"));
                Assert(await Js(view, "document.querySelector('.array-background canvas').dataset.testIdentity==='persistent-background'") == "true", "diagnostics page preserves the live 3D scene");
                await DetailFits(view, "Diagnostics minimum window with recent logs");
                window.ClientSize = new Size(1280, 800);
                await DetailFits(view, "Diagnostics default window with recent logs");
                await Capture(view, Path.Combine(output, "native-diagnostics.png"));
                window.ClientSize = new Size(960, 680);
                await Task.Delay(1800);
                await Capture(view, Path.Combine(output, "native-diagnostics-small.png"));
                await BackgroundMoves(view, "diagnostics", output);
                await Js(view, "document.body.dataset.statusObserved='waiting';new MutationObserver(()=>{const value=document.querySelector('.mod-version .status-value');if(value?.textContent==='1.0.9') document.body.dataset.statusObserved=value.getAnimations().some(animation=>animation.playState==='running')?'animated':'plain';}).observe(document.querySelector('.mod-version'),{subtree:true,childList:true,characterData:true})");
                File.WriteAllText(Path.Combine(package, "VERSION"), "1.0.9");
                await Wait(async () => (await Js(view, "document.body.dataset.statusObserved!=='waiting'")) == "true");
                Assert(await Js(view, "document.body.dataset.statusObserved==='animated' && document.querySelector('.mod-version').textContent==='1.0.9'") == "true", "status changes render the real new text immediately with upstream content fading");
                File.WriteAllText(Path.Combine(package, "VERSION"), "1.0.8");
                await Js(view, "window.chrome.webview.postMessage({command:'home'})");
                await Wait(async () => (await Js(view, "document.querySelector('.launch-panel')!==null")) == "true");
                await Task.Delay(650);
                await Js(view, "document.body.dataset.quickReturn='waiting';const quickReturnObserver=new MutationObserver(()=>{if(document.querySelector('.page-surface').dataset.transition==='closing'){quickReturnObserver.disconnect();document.body.dataset.quickReturn='sent';window.chrome.webview.postMessage({command:'home'});}});quickReturnObserver.observe(document.querySelector('.page-surface'),{attributes:true,attributeFilter:['data-transition']});window.chrome.webview.postMessage({command:'mod'})");
                await Task.Delay(750);
                Assert(await Js(view, "document.body.dataset.quickReturn==='sent' && document.querySelector('.launch-panel')!==null && document.querySelector('.detail-page')===null && !document.querySelector('.page-surface').inert && document.querySelector('.page-counter .rolling-number').getAttribute('aria-label')==='01'") == "true", "quick return cancels an outdated page transition and keeps the latest route usable");
                await Task.Delay(900);
                await CaptureMotionPreview(view, output);
                File.WriteAllText(Path.Combine(output, "webhost-result.txt"), "PASS " + passed + " native integration checks; no real game or bridge started.");
            } catch (Exception error) {
                failed++;
                Console.WriteLine("FAIL " + error);
                File.WriteAllText(Path.Combine(output, "webhost-result.txt"), "FAIL " + error);
            }
            if (failed > 0) {
                var failedView = Field<WebView2>(window, "rhineView");
                if (failedView != null && failedView.CoreWebView2 != null) {
                    Console.WriteLine(await Js(failedView, "JSON.stringify({url:location.href,text:document.body.innerText,scene:document.querySelector('.array-background')?.dataset.sceneReady})"));
                    await Capture(failedView, Path.Combine(output, "native-failure.png"));
                }
            }
            window.Close();
        };
        Application.Run(window);
        if (!Path.GetFullPath(fixture).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new Exception("unsafe fixture");
        Directory.Delete(fixture, true);
        Console.WriteLine("WEBHOST RESULT " + passed + " passed, " + failed + " failed");
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }
    static bool RhineOriginTest() {
        return MainWindow.RhineOrigin("https://babel.local/index.html") &&
            !MainWindow.RhineOrigin("https://babel.local.evil.example/") &&
            !MainWindow.RhineOrigin("http://babel.local/") &&
            !MainWindow.RhineOrigin("https://example.com/") &&
            !MainWindow.RhineOrigin("file:///C:/test.html");
    }
}
