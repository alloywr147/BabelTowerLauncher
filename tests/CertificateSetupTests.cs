using System;
using System.IO;
using System.Diagnostics;
using BabelManager;

class CertificateSetupTests {
    static int passed;
    static void Ok(bool value, string message) {
        if (!value) throw new Exception(message);
        passed++; Console.WriteLine("PASS " + message);
    }
    static void Reject(Action action, string message) {
        try { action(); } catch (Exception) { Ok(true, message); return; }
        throw new Exception(message);
    }
    sealed class Fake : ICertificateSetupRuntime {
        public ChannelCertificate Before = new ChannelCertificate(), After = new ChannelCertificate();
        public int Runs, ExitCode;
        public bool Throw;
        public ChannelCertificate ReadCertificate() { return Runs == 0 ? Before : After; }
        public int Run(ProcessStartInfo info, Action<string> log) {
            Runs++; if (Throw) throw new IOException("fixture failure"); return ExitCode;
        }
    }
    static ChannelCertificate Ready() { return new ChannelCertificate { Present=true, Valid=true, Trusted=true, Thumbprint="fixture" }; }
    static void Main() {
        string root = Path.Combine(Path.GetTempPath(), "BT17-cert-" + Guid.NewGuid().ToString("N"));
        string install = Path.Combine(root, "BabelTower"), package = Path.Combine(install, "包 with spaces & 'quotes'");
        string[] names = { "BABEL_TOWER_STATE", "BABEL_TOWER_CERT_PFX", "BABEL_TOWER_CERT_PASS" };
        string[] previous = Array.ConvertAll(names, Environment.GetEnvironmentVariable);
        try {
            foreach (string name in names) Environment.SetEnvironmentVariable(name, null);
            foreach (string sub in new[] { "core", "portable-node", "scripts" }) Directory.CreateDirectory(Path.Combine(package, sub));
            foreach (string file in new[] { "core/bridge_server.js", "core/bridge_https.js", "portable-node/node.exe", "pak01_dir.vpk", "scripts/gen_bridge_cert.ps1" }) File.WriteAllText(Path.Combine(package, file), "fixture");
            var settings = new Settings { InstallRoot=install, CurrentFolder=package };
            var info = CertificateSetup.StartInfo(settings);
            Ok(info.FileName.EndsWith(@"WindowsPowerShell\v1.0\powershell.exe", StringComparison.OrdinalIgnoreCase) && Path.IsPathRooted(info.FileName), "certificate uses the fixed Windows PowerShell executable");
            Ok(!info.UseShellExecute && info.CreateNoWindow && info.RedirectStandardOutput && info.RedirectStandardError && String.IsNullOrEmpty(info.Verb), "certificate runs without console or administrator elevation");
            Ok(info.Arguments.Contains("-File " + Core.Quote(Path.Combine(package,"scripts","gen_bridge_cert.ps1"))) && info.Arguments.EndsWith(" -Trust") && !info.Arguments.Contains("-Command"), "script paths with spaces and shell punctuation remain a single File argument");
            var fake = new Fake { After=Ready() };
            Ok(CertificateSetup.Configure(settings,fake,delegate{}).Success && fake.Runs==1, "successful script is accepted only with a valid trusted private-key certificate");
            fake = new Fake { Before=Ready() };
            var result = CertificateSetup.Configure(settings,fake,delegate{});
            Ok(result.Success && result.AlreadyConfigured && fake.Runs==0, "already configured certificate is reused without executing the script");
            fake = new Fake { After=new ChannelCertificate { Present=true,Valid=true,Trusted=false,Thumbprint="fixture" } };
            result = CertificateSetup.Configure(settings,fake,delegate{});
            Ok(!result.Success && result.Message.Contains("未信任"), "declined Windows trust cannot be reported successful despite script exit zero");
            fake = new Fake { After=new ChannelCertificate { Present=true,Valid=false,Trusted=true } };
            Ok(!CertificateSetup.Configure(settings,fake,delegate{}).Success, "invalid or unreadable PFX cannot become configured");
            fake = new Fake { ExitCode=7, After=Ready() };
            Ok(!CertificateSetup.Configure(settings,fake,delegate{}).Success, "script nonzero exit remains a failure even if another certificate is trusted");
            fake = new Fake { Throw=true };
            Reject(()=>CertificateSetup.Configure(settings,fake,delegate{}), "script launch errors remain visible to the caller");
            File.Delete(Path.Combine(package,"scripts","gen_bridge_cert.ps1"));
            Reject(()=>CertificateSetup.StartInfo(settings), "missing official script is rejected before launching PowerShell");
            File.WriteAllText(Path.Combine(package,"scripts","gen_bridge_cert.ps1"),"fixture");
            settings.InstallRoot=Path.Combine(root,"other");
            Reject(()=>CertificateSetup.StartInfo(settings), "package outside the selected install root is rejected"); settings.InstallRoot=install;
            foreach (string name in names) {
                Environment.SetEnvironmentVariable(name,"custom-value");
                Reject(()=>CertificateSetup.StartInfo(settings), "custom certificate environment is not silently overwritten: " + name);
                Environment.SetEnvironmentVariable(name,null);
            }
            var output = new System.Collections.Generic.List<string>();
            File.WriteAllText(Path.Combine(package,"scripts","gen_bridge_cert.ps1"), "[Console]::Error.WriteLine('fixture-error'); Write-Output '证书测试输出'; exit 7", new System.Text.UTF8Encoding(true));
            int actualExit = new CertificateSetupRuntime().Run(CertificateSetup.StartInfo(settings),output.Add);
            Ok(actualExit==7 && output.Exists(x=>x.Contains("fixture-error")), "hidden runner drains stderr and preserves a harmless fixture script exit code");
            Ok(output.Exists(x=>x.Contains("证书测试输出")), "official PowerShell file invocation preserves Chinese log output");
            File.Delete(Path.Combine(package,"core","bridge_https.js"));
            Reject(()=>CertificateSetup.StartInfo(settings), "legacy package cannot invoke certificate setup");
            Console.WriteLine("CERTIFICATE RESULT " + passed + " passed; no certificate store writes or real bridge launched");
        } catch(Exception error) { Console.WriteLine("FAIL " + error); Environment.ExitCode=1; }
        finally {
            for(int i=0;i<names.Length;i++) Environment.SetEnvironmentVariable(names[i],previous[i]);
            if(Directory.Exists(root)&&Core.Under(Path.GetTempPath(),root)&&Path.GetFileName(root).StartsWith("BT17-cert-"))Directory.Delete(root,true);
        }
    }
}
