using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace BabelManager {
    public sealed class CertificateSetupResult {
        public bool Success, AlreadyConfigured;
        public string Message = "";
    }

    // Tests substitute this boundary; they never generate keys or change Windows trust.
    public interface ICertificateSetupRuntime {
        ChannelCertificate ReadCertificate();
        int Run(ProcessStartInfo info, Action<string> log);
    }

    public sealed class CertificateSetupRuntime : ICertificateSetupRuntime {
        public ChannelCertificate ReadCertificate() { return LowLatencyChannel.ReadCertificate(); }

        public int Run(ProcessStartInfo info, Action<string> log) {
            using (var process = new Process { StartInfo = info }) {
                object gate = new object();
                int remaining = 12000;
                DataReceivedEventHandler receive = (sender, args) => {
                    if (String.IsNullOrEmpty(args.Data)) return;
                    lock (gate) {
                        if (remaining <= 0) return;
                        string line = args.Data.Substring(0, Math.Min(remaining, args.Data.Length));
                        remaining -= line.Length;
                        log("证书脚本：" + line);
                    }
                };
                process.OutputDataReceived += receive;
                process.ErrorDataReceived += receive;
                if (!process.Start()) throw new Exception("无法启动 Mod 证书脚本。");
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                // Runs on a worker. Do not suppress or auto-accept the Windows trust dialog.
                // The user may need time to find that dialog; do not time out mid-install.
                process.WaitForExit();
                return process.ExitCode;
            }
        }
    }

    public static class CertificateSetup {
        public static ProcessStartInfo StartInfo(Settings settings) {
            if (settings == null || String.IsNullOrWhiteSpace(settings.InstallRoot) ||
                !Core.IsPackage(settings.CurrentFolder) || !Core.Under(settings.InstallRoot, settings.CurrentFolder))
                throw new Exception("请先在设置中选择安装目录内的完整 Mod 版本。");
            Core.NoLinks(settings.InstallRoot);
            Core.NoLinks(settings.CurrentFolder);
            if (!LowLatencyChannel.Supported(settings.CurrentFolder))
                throw new Exception("此 Mod 版本不支持低延迟通道，请先更新官方完整包。");
            string script = Path.Combine(settings.CurrentFolder, "scripts", "gen_bridge_cert.ps1");
            Core.NoLinks(script);
            if (!File.Exists(script)) throw new Exception("所选 Mod 缺少官方证书脚本 scripts\\gen_bridge_cert.ps1，请重新下载官方完整包。");
            // The official script uses its own fixed state directory/password. Refuse custom
            // environments rather than generating an unrelated certificate and claiming success.
            foreach (string name in new[] { "BABEL_TOWER_STATE", "BABEL_TOWER_CERT_PFX", "BABEL_TOWER_CERT_PASS" }) {
                if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)))
                    throw new Exception("检测到自定义证书环境 " + name + "。请按 Mod 文档手动配置，以免覆盖自定义证书。");
            }
            Core.NoLinks(LowLatencyChannel.CertificatePath());
            return new ProcessStartInfo(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"),
                "-NoLogo -NoProfile -ExecutionPolicy Bypass -File " + Core.Quote(script) + " -Trust") {
                WorkingDirectory = settings.CurrentFolder,
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
                StandardErrorEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)
            };
        }

        public static CertificateSetupResult Configure(Settings settings, ICertificateSetupRuntime runtime, Action<string> log) {
            var info = StartInfo(settings);
            var before = runtime.ReadCertificate();
            if (Configured(before)) return new CertificateSetupResult {
                Success=true, AlreadyConfigured=true,
                Message="本机证书已有效并受信任，无需重复配置。若刚完成首次配置，请完全退出 Steam（包括托盘）后重新打开，再在诊断页点击“重启桥”。"
            };
            int exitCode = runtime.Run(info, log);
            var after = runtime.ReadCertificate();
            if (exitCode != 0) return new CertificateSetupResult {
                Message="证书脚本未成功完成（退出码 " + exitCode + "）。请查看运行记录，处理后重试。"
            };
            if (!Configured(after)) return new CertificateSetupResult {
                Message=after.Present && after.Valid
                    ? "证书已生成，但 Windows 仍未信任。若刚才取消了安全警告，请重新点击“配置证书”，确认 BabelTower-localhost 后选择“是”。"
                    : "证书配置未通过校验：未找到可读取、有效且含私钥的受信任证书。请查看运行记录后重试。"
            };
            return new CertificateSetupResult {
                Success=true,
                Message="证书已配置并验证信任成功。请完全退出 Steam（包括托盘）后重新打开，再在诊断页点击“重启桥”。\n仅重启游戏不够；启动器不会替你退出 Steam。"
            };
        }

        static bool Configured(ChannelCertificate value) {
            return value != null && value.Present && value.Valid && value.Trusted && !String.IsNullOrEmpty(value.Thumbprint);
        }
    }
}
