using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using BabelManager;

class ThemeTests
{
    static int passed;
    static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
        passed++;
        Console.WriteLine("PASS " + message);
    }
    static object Get(MainWindow window, string name)
    {
        return typeof(MainWindow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
    }
    static void Call(MainWindow window, string method, params object[] args)
    {
        typeof(MainWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, args);
    }
    static Control[] All(Control control)
    {
        return control.Controls.Cast<Control>().SelectMany(child => new[] { child }.Concat(All(child))).ToArray();
    }
    static double Luminance(Color color)
    {
        Func<double, double> channel = c => c <= .04045 ? c / 12.92 : Math.Pow((c + .055) / 1.055, 2.4);
        return .2126 * channel(color.R / 255d) + .7152 * channel(color.G / 255d) + .0722 * channel(color.B / 255d);
    }
    static double Contrast(Color a, Color b)
    {
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05);
    }
    [STAThread]
    static void Main()
    {
        string fixture = Path.Combine(Path.GetTempPath(), "BTTheme-" + Guid.NewGuid().ToString("N"));
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var preferences = typeof(Core).Assembly.GetType("BabelManager.ThemePreferences");
            Check(preferences != null, "appearance preference is separate from game and Mod settings");
            preferences.GetField("Path").SetValue(null, Path.Combine(fixture, "appearance.json"));
            Core.SettingsPath = Path.Combine(fixture, "no-game-profile.json");
            using (var window = new MainWindow())
            {
                var toggle = All(window).OfType<Button>().SingleOrDefault(button => button.AccessibleName != null && button.AccessibleName.StartsWith("切换主题"));
                Check(toggle != null && toggle.Parent != Get(window, "body"), "theme toggle is available in persistent top-right header");
                Check(window.BackColor.R < 32 && window.BackColor.G < 32, "default dark appearance uses a near-black background");
                var fields = All(window).OfType<ComboBox>().ToArray();
                fields[0].Text = "user-selected game path";
                typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(toggle, new object[] { EventArgs.Empty });
                Check(window.BackColor.R > 200, "theme toggle restores light appearance immediately");
                Check(All(window).OfType<ComboBox>().First() == fields[0] && fields[0].Text == "user-selected game path", "theme changes preserve unsaved path fields without rebuilding the page");
                typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(toggle, new object[] { EventArgs.Empty });
                Check(Contrast(Theme.Text, Theme.Surface) >= 4.5 && Contrast(Theme.Muted, Theme.Surface) >= 4.5, "dark text and secondary text remain readable");
                Check(Contrast(Theme.Success, Theme.Surface) >= 4.5 && Contrast(Theme.Danger, Theme.Surface) >= 4.5, "dark health and failure colors remain readable");
                var buttonInk = typeof(Theme).GetProperty("ButtonInk");
                Check(buttonInk != null && Contrast((Color)buttonInk.GetValue(null, null), Theme.Accent) >= 4.5, "yellow primary buttons retain dark readable text");
                Call(window, "ShowImportPage");
                var status = (Label)Get(window, "downloadStatus");
                status.Text = "download status in progress";
                Call(window, "SetBusy", true);
                var manual = (Button)Get(window, "update");
                typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(toggle, new object[] { EventArgs.Empty });
                Check(status == Get(window, "downloadStatus") && status.Text == "download status in progress" && !manual.Enabled, "theme toggle preserves download state and disabled operation controls");
                var drop = All(window).Single(control => control.AccessibleName == "新版压缩包拖放区域");
                typeof(Control).GetMethod("OnDragLeave", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(drop, new object[] { EventArgs.Empty });
                Check(drop.BackColor == Theme.Surface, "leaving archive drop after a theme change restores the current palette");
                Call(window, "SetBusy", false);
                typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(toggle, new object[] { EventArgs.Empty });
                Check(!((System.Windows.Forms.Timer)Get(window, "heartbeat")).Enabled, "theme verification never activates live game or bridge polling");
            }
            using (var reopened = new MainWindow())
            {
                Check(reopened.BackColor.R < 32, "dark preference is restored when launcher reopens");
                Check(!File.Exists(Core.SettingsPath), "appearance changes never create or overwrite game settings");
            }
            Console.WriteLine("THEME RESULT " + passed + " passed");
        }
        catch (Exception error)
        {
            Console.WriteLine("FAIL " + (error.InnerException ?? error).Message);
            Environment.ExitCode = 1;
        }
        finally
        {
            if (Directory.Exists(fixture) && Path.GetFileName(fixture).StartsWith("BTTheme-")) Directory.Delete(fixture, true);
        }
    }
}
