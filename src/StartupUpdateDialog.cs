using System;
using System.Drawing;
using System.Windows.Forms;

namespace BabelManager
{
    public enum StartupUpdateChoice { Later, Mod, Launcher }

    public sealed class StartupUpdateDialog : Form
    {
        public StartupUpdateChoice Choice { get; private set; }

        Label Caption(string text, int x, int y, int width, int height, int size, bool bold, Color color)
        {
            return new Label { Text = text, Location = new Point(x, y), Size = new Size(width, height),
                BackColor = Color.Transparent, ForeColor = color,
                Font = new Font(Font.FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular) };
        }

        public StartupUpdateDialog(StartupUpdateReport report, Settings settings, Version launcherVersion)
        {
            bool mod = report.ModAvailable(settings), launcher = report.LauncherAvailable(launcherVersion);
            int count = (mod ? 1 : 0) + (launcher ? 1 : 0);
            Text = "发现新版本";
            Font = new Font("Microsoft YaHei UI", 10);
            ClientSize = new Size(640, 196 + count * 118);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowIcon = false;
            BackColor = Theme.Background;
            HandleCreated += (sender, args) => Theme.ApplyChrome(this);
            Controls.Add(Caption("发现新版本", 28, 23, 584, 39, 21, true, Theme.Text));
            Controls.Add(Caption("选择要更新的项目，或稍后再处理。", 30, 70, 584, 30, 10, false, Theme.Muted));
            int row = 0;
            if (mod) AddRelease("Mod 本体", OfficialUpdates.CurrentVersion(settings.CurrentFolder), report.Mod, StartupUpdateChoice.Mod, row++);
            if (launcher) AddRelease("启动器", launcherVersion, report.Launcher, StartupUpdateChoice.Launcher, row++);
            var later = new ModernButton { Text = "稍后", AccessibleName = "稍后再更新", Location = new Point(472, ClientSize.Height - 62),
                Size = new Size(140, 40), BackColor = Theme.Surface, ForeColor = Theme.Text, DialogResult = DialogResult.Cancel };
            Controls.Add(later);
            CancelButton = later;
        }

        void AddRelease(string name, Version installed, OfficialRelease release, StartupUpdateChoice choice, int row)
        {
            var card = new Card { Location = new Point(28, 111 + row * 118), Size = new Size(584, 98), AccessibleName = name + "新版本" };
            card.Controls.Add(Caption(name, 18, 14, 335, 27, 13, true, Theme.Text));
            card.Controls.Add(Caption((choice == StartupUpdateChoice.Launcher ? SelfUpdater.VersionLabel(installed) : installed.ToString()) + "  →  " + (choice == StartupUpdateChoice.Launcher ? SelfUpdater.VersionLabel(release.Version) : release.Version.ToString()), 18, 53, 335, 28, 11, false, Theme.Muted));
            var update = new ModernButton { Text = choice == StartupUpdateChoice.Mod ? "更新 Mod" : "更新启动器",
                AccessibleName = name + "下载并更新", Location = new Point(402, 27), Size = new Size(160, 44),
                Primary = row == 0, BackColor = Theme.Surface, ForeColor = row == 0 ? Theme.ButtonInk : Theme.Text };
            update.Click += (sender, args) => { Choice = choice; DialogResult = DialogResult.OK; Close(); };
            card.Controls.Add(update);
            Controls.Add(card);
        }
    }
}
