using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using BabelManager;

class StartupUpdateTests
{
    static int passed;
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); passed++; Console.WriteLine("PASS " + message); }
    static object Call(Type type, object instance, string method, params object[] arguments)
    {
        try { return type.GetMethod(method).Invoke(instance, arguments); }
        catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    static OfficialRelease Release(UpdateSource source, string version)
    {
        string name = (source == UpdateSource.Mod ? "BabelTower-" : "BabelTowerLauncher-") + version + "-win64.zip";
        string repo = source == UpdateSource.Mod ? "c1375rick/BabelTower" : "alloywr147/BabelTowerLauncher";
        return new OfficialRelease { Source = source, Tag = "v" + version, Version = new Version(version), Name = name, Size = 3, Sha256 = "sha256:" + new string('a', 64), DownloadUrl = "https://github.com/" + repo + "/releases/download/v" + version + "/" + name };
    }
    static object Wait(object result)
    {
        var task = (Task)result;
        task.GetAwaiter().GetResult();
        return result.GetType().GetProperty("Result").GetValue(result, null);
    }
    static Control[] All(Control control) { return control.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(All(child))).ToArray(); }
    [STAThread]
    static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "BTStartup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var service = typeof(Core).Assembly.GetType("BabelManager.StartupUpdates");
            Check(service != null, "startup update service checks both sources without installing");
            int modCalls = 0, launcherCalls = 0;
            Func<CancellationToken, OfficialRelease> mod = token => { Interlocked.Increment(ref modCalls); return Release(UpdateSource.Mod, "1.0.10"); };
            Func<CancellationToken, OfficialRelease> launcher = token => { Interlocked.Increment(ref launcherCalls); return Release(UpdateSource.Launcher, "1.3.3"); };
            var report = Wait(Call(service, null, "CheckAsync", mod, launcher, CancellationToken.None));
            Check(modCalls == 1 && launcherCalls == 1, "startup check reads Mod and launcher repositories once each");
            Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "VERSION"), "1.0.9");
            var settings = new Settings { CurrentFolder = root };
            var reportType = report.GetType();
            Check((bool)Call(reportType, report, "ModAvailable", settings) && (bool)Call(reportType, report, "LauncherAvailable", new Version(1, 3, 2)), "both newer versions can be offered in one notification");
            File.WriteAllText(Path.Combine(root, "VERSION"), "1.0.11");
            Check(!(bool)Call(reportType, report, "ModAvailable", settings), "startup comparison never proposes a Mod downgrade");
            Check(!(bool)Call(reportType, report, "LauncherAvailable", new Version(1, 3, 3)), "same launcher release does not cause an update popup");
            Check(!(bool)Call(reportType, report, "ModAvailable", new Settings()), "unconfigured Mod does not claim an installed version is outdated");
            Func<CancellationToken, OfficialRelease> offline = token => { throw new IOException("fixture offline"); };
            var partial = Wait(Call(service, null, "CheckAsync", offline, launcher, CancellationToken.None));
            Check((bool)Call(reportType, partial, "LauncherAvailable", new Version(1, 3, 2)) && reportType.GetField("ModError").GetValue(partial).ToString().Contains("offline"), "one unavailable repository does not suppress the other update");
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel(); int previous = modCalls + launcherCalls; bool cancelled = false;
                try { Wait(Call(service, null, "CheckAsync", mod, launcher, cancellation.Token)); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && modCalls + launcherCalls == previous, "closing before startup check cancels requests without notifications");
            }
            var noticeType = typeof(Core).Assembly.GetType("BabelManager.StartupUpdateNotice");
            Check(noticeType != null, "update notice has a per-launch notification guard");
            var notice = Activator.CreateInstance(noticeType);
            File.WriteAllText(Path.Combine(root, "VERSION"), "1.0.9");
            Check((bool)Call(noticeType, notice, "TryClaim", report, settings, new Version(1, 3, 2)) && !(bool)Call(noticeType, notice, "TryClaim", report, settings, new Version(1, 3, 2)), "later or page navigation cannot repeat the same startup popup");
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Theme.SetDark(true);
            var dialogType = typeof(Core).Assembly.GetType("BabelManager.StartupUpdateDialog");
            Check(dialogType != null, "new versions have a real update notification dialog");
            using (var dialog = (Form)Activator.CreateInstance(dialogType, new object[] { report, settings, new Version(1, 3, 2) }))
            {
                var controls = All(dialog);
                Check(controls.OfType<Button>().Any(button => button.Text == "更新 Mod") && controls.OfType<Button>().Any(button => button.Text == "更新启动器"), "popup offers separate explicit installation actions for both updates");
                Check(dialog.CancelButton != null && ((Button)dialog.CancelButton).Text == "稍后" && !dialog.TopMost, "popup can be dismissed and never stays above other applications");
                Check(dialog.BackColor.R < 32 && controls.OfType<Label>().Any(label => label.Text.Contains("1.0.10")), "popup uses dark theme and displays the detected version");
            }
            Console.WriteLine("STARTUP RESULT " + passed + " passed; no popup shown or network called");
        }
        catch (Exception error) { Console.WriteLine("FAIL " + (error.InnerException ?? error).Message); Environment.ExitCode = 1; }
        finally { if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("BTStartup-")) Directory.Delete(root, true); }
    }
}
