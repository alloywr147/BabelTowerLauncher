using System;
using System.IO;
using System.Text;
using System.Reflection;
using BabelManager;

class RepairTests {
    class Fixture : IDisposable {
        public string Root = Path.Combine(Path.GetTempPath(), "BT15-repair-" + Guid.NewGuid().ToString("N"));
        public Settings Settings;
        public string Addons, Gi, PackageVpk;
        public Fixture() {
            string game = Path.Combine(Root, "Deadlock"), install = Path.Combine(Root, "BabelTower"), package = Path.Combine(install, "BabelTower-108");
            Directory.CreateDirectory(Path.Combine(game, "game", "bin", "win64"));
            File.WriteAllText(Path.Combine(game, "game", "bin", "win64", "deadlock.exe"), "");
            Addons = Path.Combine(game, "game", "citadel", "addons");
            Directory.CreateDirectory(Addons);
            Directory.CreateDirectory(Path.Combine(package, "core"));
            Directory.CreateDirectory(Path.Combine(package, "portable-node"));
            Directory.CreateDirectory(Path.Combine(package, "config"));
            File.WriteAllText(Path.Combine(package, "core", "bridge_server.js"), "");
            File.WriteAllText(Path.Combine(package, "portable-node", "node.exe"), "");
            File.WriteAllText(Path.Combine(package, "VERSION"), "1.0.8");
            File.WriteAllText(Path.Combine(package, "config", "config.json"), "{\"gameLogTail\":false,\"port\":9000,\"apiKey\":\"do-not-export-this-secret\"}");
            PackageVpk = Path.Combine(package, "pak01_dir.vpk");
            Vpk(PackageVpk, 1);
            Gi = Path.Combine(game, "game", "citadel", "gameinfo.gi");
            File.WriteAllText(Gi, "GameInfo { FileSystem { SearchPaths { Game citadel Game other-mod } } } // preserve me");
            Settings = new Settings { GameRoot = game, InstallRoot = install, CurrentFolder = package };
            Core.SettingsPath = Path.Combine(Root, "data", "settings.json");
            Core.BackupsDir = Path.Combine(Root, "data", "backups");
        }
        public void Dispose() {
            string full = Path.GetFullPath(Root);
            if (!full.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("BT15-repair-")) throw new Exception("Unsafe cleanup");
            Directory.Delete(full, true);
        }
    }
    class Fake : IBridgeRuntime {
        public string Started = "", Stopped = "";
        public CheckResult Result = new CheckResult { Local = true, Translation = true, Key = "fixture", Text = "测试成功" };
        public bool HasOwned(string p) { return true; }
        public void Stop(string p) { Stopped = p; }
        public void Start(string p, Settings s, Action<string> log) { Started = p; }
        public CheckResult Check(string p, Action<string> log, Action<bool, string> local) { return Result; }
    }
    static void Vpk(string file, byte extra) {
        byte[] tree = Encoding.UTF8.GetBytes("vjs_c\0panorama/scripts\0lingua_chat\0");
        using (var b = new BinaryWriter(File.Create(file))) { b.Write((uint)0x55AA1234); b.Write((uint)1); b.Write((uint)tree.Length); b.Write(tree); b.Write(extra); }
    }
    static object Repair(Fixture f, Fake runtime, Func<bool> running, Action before) {
        Type t = typeof(Core).Assembly.GetType("BabelManager.RepairService");
        Assert(t != null, "one-click repair service missing");
        try { return t.GetMethod("Run").Invoke(null, new object[] { f.Settings, runtime, running, new Action<string>(s => {}), null, before }); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static int passed, failed;
    static void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e.Message); } }
    static void Main() {
        Test("missing VPK restored from selected complete package, other mods preserved", () => {
            using (var f = new Fixture()) {
                string other = Path.Combine(f.Addons, "pak01_dir.vpk"); File.WriteAllText(other, "another mod");
                Repair(f, new Fake(), () => false, null);
                Assert(Installation.Inspect(f.Settings).Ready, "not ready after repair");
                Assert(File.ReadAllText(other) == "another mod", "other mod changed");
                Assert(File.Exists(Path.Combine(f.Addons, "pak02_dir.vpk")), "free slot not used");
                Assert(File.ReadAllText(f.Gi).Contains("preserve me") && File.ReadAllText(f.Gi).Contains("other-mod"), "gameinfo unrelated content lost");
                Assert(File.ReadAllText(Path.Combine(f.Settings.CurrentFolder, "config", "config.json")).Contains("do-not-export-this-secret"), "provider credentials changed");
                Assert(Directory.GetDirectories(Core.BackupsDir).Length == 1, "backup missing");
            }
        });
        Test("duplicates consolidated without deleting source package", () => {
            using (var f = new Fixture()) { Vpk(Path.Combine(f.Addons, "pak03_dir.vpk"), 3); Vpk(Path.Combine(f.Addons, "pak05_dir.vpk"), 5); Repair(f, new Fake(), () => false, null); Assert(Installation.Inspect(f.Settings).Ready, "duplicates remain"); Assert(Core.IsPackage(f.Settings.CurrentFolder), "source removed"); }
        });
        Test("game running prevents file repair without touching package config or game files", () => {
            using (var f = new Fixture()) { byte[] gi = File.ReadAllBytes(f.Gi); string cfg = File.ReadAllText(Path.Combine(f.Settings.CurrentFolder, "config", "config.json")); bool refused = false; try { Repair(f, new Fake(), () => true, null); } catch { refused = true; } Assert(refused, "running game accepted"); Assert(Convert.ToBase64String(gi) == Convert.ToBase64String(File.ReadAllBytes(f.Gi)), "gi changed"); Assert(cfg == File.ReadAllText(Path.Combine(f.Settings.CurrentFolder, "config", "config.json")), "config changed"); Assert(Directory.GetFiles(f.Addons).Length == 0, "VPK created"); }
        });
        Test("profile save failure rolls back VPK, config and gameinfo", () => {
            using (var f = new Fixture()) { string gi = File.ReadAllText(f.Gi), cfg = File.ReadAllText(Path.Combine(f.Settings.CurrentFolder, "config", "config.json")); Directory.CreateDirectory(Core.SettingsPath); bool refused = false; try { Repair(f, new Fake(), () => false, null); } catch { refused = true; } Assert(refused, "save failure accepted"); Assert(gi == File.ReadAllText(f.Gi) && cfg == File.ReadAllText(Path.Combine(f.Settings.CurrentFolder, "config", "config.json")), "rollback changed originals"); Assert(Directory.GetFiles(f.Addons).Length == 0 && f.Settings.VpkPath == "", "VPK/profile not restored"); }
        });
        Test("concurrent loading-config edit aborts without overwriting it", () => {
            using (var f = new Fixture()) { bool refused = false; try { Repair(f, new Fake(), () => false, () => File.AppendAllText(f.Gi, "\n// external edit")); } catch { refused = true; } Assert(refused && File.ReadAllText(f.Gi).Contains("external edit"), "external edit lost"); Assert(Directory.GetFiles(f.Addons).Length == 0, "partial repair committed"); }
        });
        Test("translation outage does not undo a successful file repair", () => {
            using (var f = new Fixture()) { Repair(f, new Fake { Result = new CheckResult { Local = true, Error = "provider unavailable" } }, () => false, null); Assert(Installation.Inspect(f.Settings).Ready, "file repair undone by online outage"); }
        });
        Test("game starting during repair prevents further writes and unsafe rollback", () => {
            using (var f = new Fixture()) {
                string gi = File.ReadAllText(f.Gi);
                string cfg = File.ReadAllText(Path.Combine(f.Settings.CurrentFolder, "config", "config.json"));
                bool refused = false;
                try { Repair(f, new Fake(), () => Directory.GetFiles(f.Addons).Length > 0, null); }
                catch (UpdateTransactionException error) { refused = !error.RecoveryComplete && Directory.Exists(error.Backup); }
                Assert(refused, "incomplete recovery must expose retained backup");
                Assert(Directory.GetFiles(f.Addons).Length == 1, "running game file was rewritten during rollback");
                Assert(gi == File.ReadAllText(f.Gi) && cfg == File.ReadAllText(Path.Combine(f.Settings.CurrentFolder, "config", "config.json")), "repair continued after game started");
                Assert(f.Settings.VpkPath == "", "uncommitted selection persisted");
            }
        });
        Test("concurrent provider-config edit is preserved before any mutation", () => {
            using (var f = new Fixture()) {
                string cfg = Path.Combine(f.Settings.CurrentFolder, "config", "config.json");
                string external = "{\"apiKey\":\"new-external-key\",\"port\":8791}";
                bool refused = false;
                try { Repair(f, new Fake(), () => false, () => File.WriteAllText(cfg, external)); } catch { refused = true; }
                Assert(refused && File.ReadAllText(cfg) == external && Directory.GetFiles(f.Addons).Length == 0, "concurrent credentials overwritten");
            }
        });
        Test("saved VPK slot occupied by another mod is preserved", () => {
            using (var f = new Fixture()) {
                f.Settings.VpkPath = Path.Combine(f.Addons, "pak04_dir.vpk");
                File.WriteAllText(f.Settings.VpkPath, "other-mod");
                string oldSlot = f.Settings.VpkPath;
                Repair(f, new Fake(), () => false, null);
                Assert(File.ReadAllText(oldSlot) == "other-mod" && f.Settings.VpkPath != oldSlot && Installation.Inspect(f.Settings).Ready, "saved slot overwrote other mod");
            }
        });
        Test("empty slot occupied before snapshot cannot become a repair target", () => {
            using (var f = new Fixture()) {
                string slot = Core.ChooseVpk(f.Settings);
                File.WriteAllText(slot, "concurrent-other-mod");
                bool refused = false;
                try { RepairService.SnapshotVpk(slot, false); } catch { refused = true; }
                Assert(refused && File.ReadAllText(slot) == "concurrent-other-mod", "new occupant accepted as backup original");
            }
        });
        Test("duplicate replaced before snapshot cannot be scheduled for deletion", () => {
            using (var f = new Fixture()) {
                string slot = Path.Combine(f.Addons, "pak03_dir.vpk"); Vpk(slot, 3);
                Assert(Core.GameVpks(f.Settings).Contains(slot), "fixture not enumerated");
                File.WriteAllText(slot, "replacement-other-mod");
                bool refused = false;
                try { RepairService.SnapshotVpk(slot, true); } catch { refused = true; }
                Assert(refused && File.ReadAllText(slot) == "replacement-other-mod", "replacement accepted as Babel duplicate");
            }
        });
        Test("incomplete source directs reimport without creating game files", () => {
            using (var f = new Fixture()) { File.Delete(f.PackageVpk); bool refused = false; try { Repair(f, new Fake(), () => false, null); } catch { refused = true; } Assert(refused && Directory.GetFiles(f.Addons).Length == 0, "incomplete source used"); }
        });
        Test("malformed gameinfo remains untouched", () => {
            using (var f = new Fixture()) { File.WriteAllText(f.Gi, "broken {"); bool refused = false; try { Repair(f, new Fake(), () => false, null); } catch { refused = true; } Assert(refused && File.ReadAllText(f.Gi) == "broken {" && Directory.GetFiles(f.Addons).Length == 0, "broken GI modified"); }
        });
        Test("export is read-only and excludes credentials and absolute private paths", () => {
            using (var f = new Fixture()) {
                Type t = typeof(Core).Assembly.GetType("BabelManager.ReportExport"); Assert(t != null, "report export missing");
                string before = File.ReadAllText(f.Gi);
                var c = new Connectivity(); c.RecordCheck(new CheckResult { Error = "apiKey=do-not-export-this-secret; " + f.Settings.CurrentFolder }, DateTime.UtcNow);
                string report = (string)t.GetMethod("Build").Invoke(null, new object[] { f.Settings, Installation.Inspect(f.Settings), c, new GameEvidence() });
                Assert(report.Contains("巴别塔") && report.Contains("1.5"), "no identifying summary");
                Assert(!report.Contains("do-not-export-this-secret") && !report.Contains(f.Root), "secret/path leaked");
                Assert(before == File.ReadAllText(f.Gi) && Directory.GetFiles(f.Addons).Length == 0, "report mutated files");
            }
        });
        Test("untrusted provider and installation errors never enter shared report", () => {
            using (var f = new Fixture()) {
                string raw = "{\"apiKey\":\"json-private-value\",\"nested\":\"\\\"token\\\":\\\"nested-private-value\\\"\"} Authorization: Basic dXNlcjpwYXNz \\\\private-server\\private-share\\file";
                var c = new Connectivity(); c.RecordCheck(new CheckResult { Error = raw }, DateTime.UtcNow);
                var install = new InstallationResult(); install.Errors.Add(raw);
                string report = ReportExport.Build(f.Settings, install, c, new GameEvidence());
                Assert(!report.Contains("json-private-value") && !report.Contains("nested-private-value") && !report.Contains("dXNlcjpwYXNz") && !report.Contains("private-server"), "opaque raw error leaked");
            }
        });
        Console.WriteLine("REPAIR RESULT " + passed + " passed, " + failed + " failed"); Environment.ExitCode = failed == 0 ? 0 : 1;
    }
}
