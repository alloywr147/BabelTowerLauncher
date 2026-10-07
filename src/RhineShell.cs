using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BabelManager {
    public partial class MainWindow {
        public static bool RhineEnabled;
        internal static bool RhineTestMode = false;
        WebView2 rhineView;
        Panel rhineLoading;
        readonly List<Control> rhineFallbackControls = new List<Control>();
        System.Windows.Forms.Timer rhineTimer;
        readonly List<string> rhineLogs = new List<string>();
        string rhinePage = "home", rhineLastState = "", rhineReport = "", rhineResult = "", rhineGame = "";
        DetectionResult rhineDetection;
        int rhineDraftRevision;
        bool rhineReady;
        bool rhineConfirm;

        public void EnableRhine() {
            pendingDetection = null;
            ClientSize = new Size(1280, 800);
            MinimumSize = new Size(976, 719);
            Text = "巴别塔启动器 1.5";
            // Hide the native fallback before the form's first paint, not after navigation.
            rhineFallbackControls.AddRange(Controls.Cast<Control>());
            foreach (Control control in rhineFallbackControls) control.Visible = false;
            BackColor = RhineSurface();
            rhineLoading = new Panel { Dock = DockStyle.Fill, BackColor = RhineSurface() };
            rhineLoading.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "正在载入…", TextAlign = ContentAlignment.MiddleCenter, ForeColor = Theme.Muted, Font = Font });
            Controls.Add(rhineLoading);
            rhineLoading.BringToFront();
            Shown += async (sender, args) => await InitializeRhine();
            FormClosed += (sender, args) => { if (rhineTimer != null) rhineTimer.Dispose(); };
        }
        void RhineRoute(string page) { rhinePage = page; }
        static Color RhineSurface() { return Theme.IsDark ? Color.FromArgb(17, 24, 27) : Color.FromArgb(234, 229, 225); }
        void RhineLog(string message) {
            rhineLogs.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + message);
            if (rhineLogs.Count > 150) rhineLogs.RemoveAt(0);
        }
        async Task InitializeRhine() {
            try {
                string content = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "web");
                if (!File.Exists(Path.Combine(content, "index.html"))) throw new Exception("缺少 assets\\web 界面文件，请完整解压启动器。");
                rhineView = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = RhineSurface() };
                Controls.Add(rhineView);
                rhineView.BringToFront();
                var environment = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Core.DataDir, "webview-profile"));
                await rhineView.EnsureCoreWebView2Async(environment);
                var web = rhineView.CoreWebView2;
                web.SetVirtualHostNameToFolderMapping("babel.local", content, CoreWebView2HostResourceAccessKind.DenyCors);
                web.Settings.AreDefaultContextMenusEnabled = false;
                web.Settings.AreDevToolsEnabled = false;
                web.Settings.IsStatusBarEnabled = false;
                web.Settings.IsZoomControlEnabled = false;
                web.NavigationStarting += (sender, args) => { if (!RhineOrigin(args.Uri)) args.Cancel = true; };
                web.NewWindowRequested += (sender, args) => { args.Handled = true; };
                web.PermissionRequested += (sender, args) => { args.State = CoreWebView2PermissionState.Deny; };
                web.WebMessageReceived += RhineMessage;
                web.NavigationCompleted += (sender, args) => {
                    if (!args.IsSuccess) { Log("界面载入失败：" + args.WebErrorStatus); return; }
                    foreach (Control control in Controls) if (control != rhineView) control.Visible = false;
                    rhineReady = true; rhineLastState = ""; PushRhineState();
                };
                rhineTimer = new System.Windows.Forms.Timer { Interval = 500 };
                rhineTimer.Tick += (sender, args) => PushRhineState();
                rhineTimer.Start();
                web.Navigate("https://babel.local/index.html");
                if (!Core.IsGame(settings.GameRoot)) { RhineRoute("settings"); await DetectRhinePaths(); }
            } catch (Exception error) {
                if (rhineView != null) { Controls.Remove(rhineView); rhineView.Dispose(); rhineView = null; }
                if (rhineLoading != null) { Controls.Remove(rhineLoading); rhineLoading.Dispose(); rhineLoading = null; }
                foreach (Control control in rhineFallbackControls) control.Visible = true;
                BackColor = Theme.Background;
                body.BringToFront();
                Log("动态界面未能打开：" + error.Message);
                MessageBox.Show(this, "动态界面未能打开，已保留基础操作界面。\n\n" + error.Message +
                    "\n\n如未安装 Microsoft Edge WebView2 Runtime，可从微软官方下载后重新打开。", "界面环境", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        public static bool RhineOrigin(string value) {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.Scheme == "https" &&
                uri.Host == "babel.local" && uri.IsDefaultPort;
        }
        static string LabelText(Label label) { return label == null || label.IsDisposed ? "" : label.Text; }
        void PushRhineState() {
            if (!rhineReady || rhineView == null || IsDisposed || rhineView.CoreWebView2 == null) return;
            DateTime now = DateTime.UtcNow;
            Version mod = OfficialUpdates.CurrentVersion(settings.CurrentFolder);
            bool running = Bridge.GameRunning();
            var payload = new {
                type = "state", page = rhinePage, busy = busy || detecting, dark = Theme.IsDark,
                launcherVersion = SelfUpdater.VersionLabel(SelfUpdater.CurrentVersion),
                modVersion = mod == null ? "尚未安装" : mod.ToString(),
                gameRunning = running,
                launchConfirmation = rhineConfirm,
                configured = Core.IsGame(settings.GameRoot) && !String.IsNullOrEmpty(settings.InstallRoot),
                packageAvailable = Core.IsPackage(settings.CurrentFolder),
                local = connectivity.LocalFresh(now), translation = connectivity.TranslationFresh(now),
                localKnown = connectivity.Known, installReady = installation != null && installation.Ready,
                installKnown = installation != null,
                localText = LabelText(local), translationText = LabelText(translation),
                installText = installation == null ? "尚未检查" : installation.Summary,
                connectionError = connectivity.TranslationError + " " + connectivity.Error,
                gameMessage = String.IsNullOrEmpty(LabelText(gameNotice)) ? rhineGame : LabelText(gameNotice),
                gameRoot = settings.GameRoot, installRoot = settings.InstallRoot, currentFolder = settings.CurrentFolder,
                logs = rhineLogs.ToArray(), reportText = rhineReport, result = rhineResult,
                draftRevision = rhineDraftRevision,
                detection = rhineDetection == null ? null : new { game = rhineDetection.Game, installRoot = rhineDetection.InstallRoot, currentFolder = rhineDetection.Package, games = rhineDetection.Games, packages = rhineDetection.Packages },
                updateStatus = LabelText(officialStatus), downloadStatus = LabelText(downloadStatus),
                progress = downloadProgress == null || downloadProgress.IsDisposed ? 0 : downloadProgress.Value,
                updateAvailable = rhinePage == "software" ?
                    launcherRelease != null && launcherRelease.Version > SelfUpdater.CurrentVersion :
                    officialRelease != null && OfficialUpdates.IsNewer(officialRelease, settings.CurrentFolder),
                canCancel = onlineCancellation != null,
                canReadNotes = rhinePage == "software" ? launcherRelease != null : officialRelease != null
            };
            string json = Core.Json().Serialize(payload);
            if (json == rhineLastState) return;
            rhineLastState = json;
            try { rhineView.CoreWebView2.PostWebMessageAsJson(json); } catch (InvalidOperationException) { }
        }
        async void RhineMessage(object sender, CoreWebView2WebMessageReceivedEventArgs args) {
            if (!RhineOrigin(args.Source)) return;
            try {
                if (args.WebMessageAsJson.Length > 32768) throw new Exception("界面请求过大。");
                var request = Core.Json().Deserialize<Dictionary<string, object>>(args.WebMessageAsJson);
                object commandValue;
                if (request == null || !request.TryGetValue("command", out commandValue)) return;
                string command = Convert.ToString(commandValue);
                // Only this packaged document can invoke an explicit allowlisted command.
                if (busy || detecting) {
                    if (command == "cancel" && onlineCancellation != null) onlineCancellation.Cancel();
                    else if (command == "state") PushRhineState();
                    return;
                }
                switch (command) {
                    case "state": rhineLastState = ""; break;
                    case "home": ShowMain(); break;
                    case "mod": ShowImportPage(); break;
                    case "diagnostics": ShowDiagnostics(); break;
                    case "settings": ShowSetup(); pendingDetection = null; break;
                    case "software": ShowSoftwareUpdate(); break;
                    case "theme": ToggleTheme(); break;
                    case "launch": if (!Core.IsGame(settings.GameRoot)) ShowSetup(); else CheckLaunch(); break;
                    case "launchConfirmed": rhineConfirm = false; LaunchRhineGame(); break;
                    case "cancelLaunch": rhineConfirm = false; break;
                    case "check": ShowDiagnostics(); RunDiagnostics(false); break;
                    case "restart": ShowDiagnostics(); RunDiagnostics(true); break;
                    case "stop": StopBridge(); break;
                    case "repair": await RepairRhine(); break;
                    case "report": await PrepareRhineReport(); break;
                    case "saveReport": SaveRhineReport(); break;
                    case "clearResult": rhineResult = ""; break;
                    case "detect": ApplyRhineDraft(request); await DetectRhinePaths(); break;
                    case "pickGame": ApplyRhineDraft(request); PickRhineFolder("game"); break;
                    case "pickRoot": ApplyRhineDraft(request); PickRhineFolder("root"); break;
                    case "pickPackage": ApplyRhineDraft(request); PickRhineFolder("package"); break;
                    case "saveSettings": SaveRhineSettings(request); break;
                    case "import": ShowImportPage(); PickArchive(); break;
                    case "dropArchive": ImportRhineDrop(args); break;
                    case "checkMod": ShowImportPage(); CheckOfficial(); break;
                    case "downloadMod": ShowImportPage(); DownloadOfficial(); break;
                    case "checkSoftware": ShowSoftwareUpdate(); CheckSoftware(); break;
                    case "downloadSoftware": ShowSoftwareUpdate(); DownloadSoftware(); break;
                    case "notes": ShowReleaseNotes(rhinePage == "software" ? launcherRelease : officialRelease); break;
                    case "folder": OpenFolder(settings.InstallRoot); break;
                    case "bridgeLogs": if (Core.IsPackage(settings.CurrentFolder)) OpenFolder(Path.Combine(settings.CurrentFolder, "logs")); break;
                    default: return;
                }
            } catch (Exception error) { rhineResult = error.Message; Log(error.Message); }
            finally { rhineLastState = ""; PushRhineState(); }
        }
        void ImportRhineDrop(CoreWebView2WebMessageReceivedEventArgs args) {
            if (args.AdditionalObjects == null || args.AdditionalObjects.Count != 1) throw new Exception("一次只可拖入一个压缩包。");
            var file = args.AdditionalObjects[0] as CoreWebView2File;
            if (file == null) throw new Exception("请拖入完整的 ZIP、7Z 或 RAR 压缩包。");
            string archive = file.Path;
            if (!Path.IsPathRooted(archive)) throw new Exception("无法读取拖入文件，请点击选择压缩包。");
            ShowImportPage(); UpdateArchive(archive);
        }
        void LaunchRhineGame() {
            if (!connectivity.TranslationFresh(DateTime.UtcNow)) throw new Exception("检查结果已失效，请重新检查后启动。");
            var latest = Installation.Inspect(settings);
            if (!latest.Ready) throw new Exception(latest.Summary);
            string steam = Bridge.Steam();
            if (steam == null) {
                using (var picker = new OpenFileDialog { Title = "选择 Steam.exe", Filter = "Steam|steam.exe" }) {
                    if (picker.ShowDialog(this) != DialogResult.OK) return;
                    steam = picker.FileName;
                }
            }
            Bridge.LaunchGame(steam);
            Log("已请求 Steam 启动 Deadlock（-console -condebug）。");
        }
        async Task DetectRhinePaths() {
            detecting = true; PushRhineState();
            try {
                var input = rhineDetection == null ?
                    new Settings { GameRoot = settings.GameRoot, InstallRoot = settings.InstallRoot, CurrentFolder = settings.CurrentFolder } :
                    new Settings { GameRoot = rhineDetection.Game, InstallRoot = rhineDetection.InstallRoot, CurrentFolder = rhineDetection.Package };
                rhineDetection = await Task.Run(() => Discovery.Detect(input));
                rhineDraftRevision++;
                Log("自动检测完成：" + rhineDetection.Games.Count + " 个游戏目录，" + rhineDetection.Packages.Count + " 个完整版本。");
            } finally { detecting = false; }
        }
        void PickRhineFolder(string kind) {
            if (rhineDetection == null) rhineDetection = new DetectionResult { Game = settings.GameRoot, InstallRoot = settings.InstallRoot, Package = settings.CurrentFolder };
            string selected = kind == "game" ? rhineDetection.Game : kind == "root" ? rhineDetection.InstallRoot : rhineDetection.Package;
            string path = PickFolder(kind == "game" ? "选择 Deadlock 游戏根目录" : kind == "root" ? "选择专用巴别塔安装文件夹" : "选择已有完整版本", selected);
            if (path == null) return;
            if (kind == "game") rhineDetection.Game = path; else if (kind == "root") rhineDetection.InstallRoot = path; else rhineDetection.Package = path;
            rhineDraftRevision++;
        }
        void ApplyRhineDraft(Dictionary<string, object> request) {
            rhineDetection = new DetectionResult {
                Game = Input(request, "gameRoot"), InstallRoot = Input(request, "installRoot"), Package = Input(request, "currentFolder")
            };
        }
        static string Input(Dictionary<string, object> request, string field) {
            object value;
            string result = request.TryGetValue(field, out value) ? Convert.ToString(value).Trim() : "";
            if (result.Length > 2048) throw new Exception("目录路径过长。");
            return result;
        }
        void SaveRhineSettings(Dictionary<string, object> request) {
            var next = new Settings { GameRoot = Input(request, "gameRoot"), InstallRoot = Input(request, "installRoot"), CurrentFolder = Input(request, "currentFolder") };
            Core.ValidateSettings(next);
            if (!String.IsNullOrEmpty(next.CurrentFolder) &&
                (!Core.IsPackage(next.CurrentFolder) || !Core.Under(next.InstallRoot, next.CurrentFolder)))
                throw new Exception("已有版本必须是安装目录中的完整巴别塔文件夹。");
            Directory.CreateDirectory(next.InstallRoot);
            if (!String.IsNullOrEmpty(next.CurrentFolder)) next.VpkPath = Core.GameVpks(next).FirstOrDefault() ?? "";
            Core.Save(next); settings = next; installation = null; rhineDetection = null;
            SyncContext(); ShowMain(); StartupSettingsSaved(); Log("路径已保存。");
        }
        async Task RepairRhine() {
            SetBusy(true); ResetChecks(); rhineResult = "";
            try {
                var result = await Task.Run(() => RepairService.Run(settings, new BridgeRuntime(), Bridge.GameRunning, Log, LocalState, null));
                installation = result.Installation; Results(result.Check); RenderInstallation();
                rhineGame = (await Task.Run(() => GameDiagnostics.Inspect(settings))).Message;
                rhineResult = result.Check.Local && result.Check.Translation ?
                    "修复完成，安装文件、本地桥与翻译接口检查通过。" :
                    "文件检查与修复已完成，连接仍需处理：\n" + result.Check.Error;
                Log(rhineResult);
            } catch (Exception error) {
                installation = null;
                connectivity.RecordCheck(new CheckResult { Error = error.Message }, DateTime.UtcNow);
                RenderChecks(); RenderInstallation();
                rhineResult = error.Message; Log("修复未完成：" + error.Message);
            } finally { SetBusy(false); }
        }
        async Task PrepareRhineReport() {
            SetBusy(true); rhineReport = "";
            try {
                var report = await Task.Run(() => Tuple.Create(Installation.Inspect(settings), GameDiagnostics.Inspect(settings)));
                installation = report.Item1; rhineGame = report.Item2.Message;
                rhineReport = ReportExport.Build(settings, report.Item1, connectivity, report.Item2);
                Log("诊断报告已生成，等待保存。");
            } finally { SetBusy(false); }
        }
        void SaveRhineReport() {
            if (String.IsNullOrEmpty(rhineReport)) return;
            using (var picker = new SaveFileDialog { Title = "保存诊断报告", Filter = "文本报告|*.txt", FileName = "BabelTower-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt" }) {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                File.WriteAllText(picker.FileName, rhineReport, new System.Text.UTF8Encoding(true));
                Log("诊断报告已保存。");
            }
        }
    }
}
