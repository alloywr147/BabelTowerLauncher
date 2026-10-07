using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BabelManager {
    public static class ReportExport {
        public static string Redact(string value, Settings settings) {
            string text = value ?? "";
            foreach (string path in new[] { settings.CurrentFolder, settings.InstallRoot, settings.GameRoot,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Core.DataDir })
                if (!String.IsNullOrWhiteSpace(path))
                    text = Regex.Replace(text, Regex.Escape(path), "<本地路径>", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"(?i)(api[-_]?key|access[-_]?token|authorization|password|secret|token)\s*[:=]\s*[^\s;,]+", "$1=<已隐藏>");
            text = Regex.Replace(text, @"(?i)Bearer\s+[A-Za-z0-9._\-]+", "Bearer <已隐藏>");
            text = Regex.Replace(text, @"\bsk-[A-Za-z0-9_-]+", "<已隐藏>");
            text = Regex.Replace(text, @"[A-Za-z]:[\\/][^\r\n;,]*", "<本地路径>");
            return text.Length > 2000 ? text.Substring(0, 2000) + "…" : text;
        }
        public static string Build(Settings settings, InstallationResult install, Connectivity state, GameEvidence game) {
            var output = new StringBuilder();
            output.AppendLine("巴别塔启动器 1.5 — 诊断报告");
            output.AppendLine("导出时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            output.AppendLine("系统：" + Environment.OSVersion.VersionString);
            output.AppendLine("启动器：" + SelfUpdater.VersionLabel(SelfUpdater.CurrentVersion));
            output.AppendLine("Mod：" + (OfficialUpdates.CurrentVersion(settings.CurrentFolder) == null ? "未识别" : OfficialUpdates.CurrentVersion(settings.CurrentFolder).ToString()));
            output.AppendLine();
            output.AppendLine("游戏目录有效：" + Core.IsGame(settings.GameRoot));
            output.AppendLine("完整包可用：" + Core.IsPackage(settings.CurrentFolder));
            output.AppendLine("安装文件：" + (install != null && install.Ready ? "通过" : "未通过"));
            output.AppendLine("VPK 一致：" + (install != null && install.Vpk));
            output.AppendLine("addons 加载路径：" + (install != null && install.Mount));
            output.AppendLine("本地桥：" + (state.LocalFresh(DateTime.UtcNow) ? "正常" : "离线或检查已过期"));
            output.AppendLine("在线翻译：" + (state.TranslationFresh(DateTime.UtcNow) ? "最近检查通过" : "未通过或需复检"));
            output.AppendLine("游戏运行：" + game.Running);
            output.AppendLine();
            if (install != null) output.AppendLine("安装详情：" + (install.Ready ? "VPK 与加载配置通过。" : "安装检查未全部通过；详细错误请在启动器诊断页查看。"));
            output.AppendLine("连接详情：" + (state.TranslationFresh(DateTime.UtcNow) ? "最近在线测试通过。" : "尚未通过有效在线检查；详细错误请在启动器中查看。"));
            output.AppendLine("游戏详情：" + Redact(game.Message, settings));
            output.AppendLine();
            output.AppendLine("报告不包含配置原文、API 密钥、聊天日志和完整本地路径。");
            output.AppendLine("本地服务和在线测试通过不等于已确认游戏内连接，请在 /tr 面板测试。");
            return output.ToString();
        }
    }
}
