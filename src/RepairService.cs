using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace BabelManager {
    public sealed class RepairReport {
        public InstallationResult Installation;
        public CheckResult Check;
        public string Backup = "";
        public bool FilesChanged;
    }

    public static class RepairService {
        sealed class Change {
            public string Path;
            public byte[] Original, Updated;
        }

        static byte[] Read(string path) { return File.Exists(path) ? File.ReadAllBytes(path) : null; }
        static string Text(byte[] bytes) { using (var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true)) return reader.ReadToEnd(); }
        static bool Same(byte[] a, byte[] b) { return a == null ? b == null : b != null && a.SequenceEqual(b); }
        static void Write(string path, byte[] bytes) {
            Core.NoLinks(path);
            if (bytes == null) { if (File.Exists(path)) File.Delete(path); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".bt-repair-" + Guid.NewGuid().ToString("N");
            try {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        static Change Capture(string path, byte[] updated) {
            Core.NoLinks(path);
            return new Change { Path = path, Original = Read(path), Updated = updated };
        }
        internal static byte[] SnapshotVpk(string path, bool expectedExisting) {
            Core.NoLinks(path);
            byte[] original = Read(path);
            if (expectedExisting ? !Core.IsBabelVpk(original) : original != null)
                throw new Exception("VPK 槽位在准备期间发生变化，已保留当前文件，请重新检查。");
            return original;
        }

        // The install mutex is shared with PackageInstaller for this game directory.
        public static RepairReport Run(Settings settings, IBridgeRuntime runtime, Func<bool> gameRunning,
            Action<string> log, Action<bool, string> local, Action beforeCommit) {
            Core.ValidateSettings(settings);
            Core.NoLinks(settings.CurrentFolder);
            if (!Core.IsPackage(settings.CurrentFolder) || !Core.Under(settings.InstallRoot, settings.CurrentFolder))
                throw new Exception("当前完整包缺失或不完整，请在 Mod 更新页下载官方完整包，或重新导入压缩包。");
            string key;
            using (var sha = SHA256.Create())
                key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Core.Full(settings.GameRoot).ToUpperInvariant()))).Replace("-", "");
            using (var mutex = new Mutex(false, "Local\\BabelTowerInstall-" + key)) {
                bool owned;
                try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                if (!owned) throw new Exception("当前游戏目录正在安装或修复，请稍后重试。");
                try { return Repair(settings, runtime, gameRunning, log, local, beforeCommit); }
                finally { mutex.ReleaseMutex(); }
            }
        }

        static RepairReport Repair(Settings settings, IBridgeRuntime runtime, Func<bool> gameRunning,
            Action<string> log, Action<bool, string> local, Action beforeCommit) {
            var report = new RepairReport();
            report.Installation = Installation.Inspect(settings);
            if (gameRunning()) {
                if (!report.Installation.Ready)
                    throw new Exception("游戏正在运行，文件修复需先退出游戏。游戏内仅可重启所选桥。\n" + report.Installation.Summary);
                report.Check = Recovery.Run(settings, true, log, local, runtime);
                return report;
            }

            string source = Path.Combine(settings.CurrentFolder, "pak01_dir.vpk");
            if (!Core.IsBabelVpk(source)) throw new Exception("当前包的 VPK 不包含巴别塔资源，请重新导入官方完整包。");
            string gi = Path.Combine(settings.GameRoot, "game", "citadel", "gameinfo.gi");
            Core.NoLinks(gi);
            if (!File.Exists(gi) || new FileInfo(gi).Length > 2 * 1024 * 1024)
                throw new Exception("缺少有效 gameinfo.gi，请先使用 Steam 验证游戏文件。");
            byte[] originalGi = File.ReadAllBytes(gi);
            string originalText = Text(originalGi), correctedText = MountConfiguration.EnsureAddons(originalText);
            string cfg = Path.Combine(settings.CurrentFolder, "config", "config.json");
            Core.NoLinks(cfg);
            byte[] originalConfig = Read(cfg);
            string template = originalConfig != null ? cfg : Path.Combine(settings.CurrentFolder, "config", "config.example.json");
            Core.NoLinks(template);
            if (!File.Exists(template) || new FileInfo(template).Length > 1024 * 1024)
                throw new Exception("桥配置缺失或异常，请重新导入完整包。");
            var values = Core.Json().Deserialize<Dictionary<string, object>>(Text(originalConfig ?? File.ReadAllBytes(template)));
            if (values == null) throw new Exception("桥配置不是 JSON 对象，保留原内容，请先检查配置。");
            values["gameLogPath"] = Path.Combine(settings.GameRoot, "game", "citadel", "console.log");
            values["gameLogTail"] = true;
            string version = "";
            try { version = Core.VersionOf("", settings.CurrentFolder); } catch { }
            if (version == "107" || version == "108") values["port"] = 8791;
            object configuredPort;
            int port = values.TryGetValue("port", out configuredPort) ? Convert.ToInt32(configuredPort) : 8791;
            if (port < 1 || port > 65535) throw new Exception("桥端口无效，请先修正配置中的 port。");

            // Only known Babel VPKs may be consolidated; another Mod's slot is never overwritten.
            var targets = Core.GameVpks(settings);
            string slot = Core.ChooseVpk(settings);
            byte[] sourceBytes = File.ReadAllBytes(source);
            if (!Core.IsBabelVpk(sourceBytes)) throw new Exception("源 VPK 在准备期间发生变化，请重新导入完整包。");
            var changes = new List<Change> { new Change { Path = slot, Original = SnapshotVpk(slot, targets.Contains(slot, StringComparer.OrdinalIgnoreCase)), Updated = sourceBytes } };
            foreach (string target in targets)
                if (!target.Equals(slot, StringComparison.OrdinalIgnoreCase)) changes.Add(new Change { Path = target, Original = SnapshotVpk(target, true), Updated = null });
            if (correctedText != originalText) changes.Add(new Change { Path = gi, Original = originalGi, Updated = new UTF8Encoding(false).GetBytes(correctedText) });
            changes.Add(new Change { Path = cfg, Original = originalConfig, Updated = new UTF8Encoding(false).GetBytes(Core.Json().Serialize(values)) });
            changes = changes.Where(c => !Same(c.Original, c.Updated)).ToList();
            if (gameRunning()) throw new Exception("游戏正在启动，请退出游戏后修复。");

            string backup = Core.Child(Core.BackupsDir, "repair-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(backup);
            for (int i = 0; i < changes.Count; i++)
                if (changes[i].Original != null) File.WriteAllBytes(Path.Combine(backup, i + ".original"), changes[i].Original);
            File.WriteAllText(Path.Combine(backup, "recovery.json"),
                Core.Json().Serialize(changes.Select((c, i) => new { path = c.Path, original = c.Original == null ? null : i + ".original" })), Encoding.UTF8);
            report.Backup = backup;
            string oldSlot = settings.VpkPath;
            var changed = new List<Change>();
            bool stopped = false;
            try {
                if (beforeCommit != null) beforeCommit();
                // Validate every source snapshot before the first mutation.
                foreach (var change in changes)
                    if (!Same(Read(change.Path), change.Original)) throw new Exception("修复期间文件被其他程序修改，已保留当前内容，请重新检查。");
                if (gameRunning()) throw new Exception("游戏正在启动，请退出游戏后修复。");
                stopped = runtime.HasOwned(settings.CurrentFolder);
                if (stopped) runtime.Stop(settings.CurrentFolder);
                foreach (var change in changes) {
                    if (gameRunning()) throw new Exception("游戏正在启动，本次修复已撤回。");
                    Core.NoLinks(change.Path);
                    if (!Same(Read(change.Path), change.Original)) throw new Exception("目标文件发生变化，修复已停止。");
                    Write(change.Path, change.Updated);
                    changed.Add(change);
                }
                settings.VpkPath = slot;
                report.Installation = Installation.Inspect(settings);
                if (!report.Installation.Ready) throw new Exception(report.Installation.Summary);
                if (gameRunning()) throw new Exception("游戏正在启动，本次修复已撤回。");
                Core.Save(settings);
                report.FilesChanged = changed.Count > 0;
            } catch (Exception error) {
                var failures = new List<string>();
                foreach (var change in changed.AsEnumerable().Reverse()) {
                    try {
                        if (Core.Under(settings.GameRoot, change.Path) && gameRunning())
                            throw new Exception("游戏已启动，停止改写游戏文件；退出后可再次修复，原件保留在备份。");
                        if (!Same(Read(change.Path), change.Updated)) throw new Exception("文件被其他程序修改，保留其内容。");
                        Write(change.Path, change.Original);
                    } catch (Exception restore) { failures.Add(Path.GetFileName(change.Path) + ": " + restore.Message); }
                }
                settings.VpkPath = oldSlot;
                if (stopped) {
                    try { runtime.Start(settings.CurrentFolder, settings, log); }
                    catch (Exception restore) { log("原文件已恢复；原桥未能启动：" + restore.Message); }
                }
                throw new UpdateTransactionException(
                    failures.Count == 0 ? "修复未完成，已恢复原文件。\n" + error.Message :
                    "部分文件未能恢复，请查看备份：" + backup + "\n" + String.Join("\n", failures),
                    error, failures.Count == 0, backup);
            }
            log("Mod 文件、addons 加载路径与桥日志配置已检查；备份：" + backup);
            // An online outage is separate from successful filesystem repair.
            try { report.Check = Recovery.Run(settings, false, log, local, runtime); }
            catch (Exception error) { report.Check = new CheckResult { Error = error.Message }; }
            return report;
        }
    }
}
