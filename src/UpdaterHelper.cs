using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using BabelManager;

class UpdaterHelper
{
    static void Restart(SelfUpdatePlan plan)
    {
        Process.Start(new ProcessStartInfo(Path.Combine(plan.TargetDirectory, plan.ExecutableName))
        {
            UseShellExecute = true,
            WorkingDirectory = plan.TargetDirectory
        });
    }

    [STAThread]
    static void Main(string[] args)
    {
        SelfUpdatePlan plan = null;
        bool parentExited = false;
        try
        {
            if (args.Length != 1) throw new Exception("更新计划缺失。");
            SelfUpdateFiles.NoLinks(args[0]);
            var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
            plan = serializer.Deserialize<SelfUpdatePlan>(File.ReadAllText(args[0]));
            SelfUpdateFiles.Validate(plan);
            string ownPath = typeof(UpdaterHelper).Assembly.Location;
            if (Path.GetFullPath(args[0]) != Path.Combine(plan.JobDirectory, "plan.json") ||
                ownPath != plan.HelperExecutable || Path.GetDirectoryName(ownPath) != plan.TargetDirectory ||
                !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(ownPath), @"^btu[0-9a-f]{6}\.exe$"))
                throw new Exception("更新程序与暂存目录不一致。");

            using (var mutex = new Mutex(false, "Local\\BabelTowerSelfUpdate-" +
                SelfUpdateFiles.Hash(Path.Combine(plan.TargetDirectory, plan.ExecutableName))))
            {
                bool owned;
                try { owned = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { owned = true; }
                if (!owned) throw new Exception("另一个后台更新正在运行。");
                try
                {
                    // The UI stays alive until identity is verified and this handle is acquired.
                    using (var parent = Process.GetProcessById(plan.ParentPid))
                    {
                        IntPtr handle = parent.Handle;
                        if (parent.StartTime.ToUniversalTime().Ticks != plan.ParentStartedUtc ||
                            !parent.MainModule.FileName.Equals(plan.ParentExecutable, StringComparison.OrdinalIgnoreCase))
                            throw new Exception("启动器进程身份变化，取消更新。");
                        File.WriteAllText(Path.Combine(plan.JobDirectory, "ready.txt"), "READY");
                        var wait = Stopwatch.StartNew();
                        while (!parent.WaitForExit(100))
                        {
                            if (File.Exists(Path.Combine(plan.JobDirectory, "cancel.txt"))) return;
                            if (wait.ElapsedMilliseconds > 90000) throw new Exception("启动器未退出，取消更新。");
                        }
                        if (File.Exists(Path.Combine(plan.JobDirectory, "cancel.txt"))) return;
                    }
                    parentExited = true;
                    SelfUpdateFiles.Apply(plan);
                    Restart(plan);
                }
                finally { mutex.ReleaseMutex(); }
            }
        }
        catch (Exception error)
        {
            if (!parentExited)
            {
                // Before handoff the launcher still owns its window and displays the failure.
                if (plan != null)
                    try { File.WriteAllText(Path.Combine(plan.JobDirectory, "helper-error.txt"), error.Message); }
                    catch { }
                return;
            }
            var failure = error as SelfUpdateFailure;
            if (failure != null && failure.RecoveryComplete)
                try { Restart(plan); }
                catch (Exception restart) { MessageBox.Show(restart.Message, "请手动打开启动器"); }
            MessageBox.Show(error.Message, "巴别塔启动器更新失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
