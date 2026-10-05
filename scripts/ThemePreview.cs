using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using BabelManager;

class ThemePreview
{
    static void Call(MainWindow window, string method)
    {
        typeof(MainWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
    }
    static void Hidden(Control control)
    {
        typeof(Control).GetMethod("CreateControl", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(bool) }, null).Invoke(control, new object[] { true });
        foreach (Control child in control.Controls) Hidden(child);
    }
    static void Render(MainWindow window, string destination)
    {
        Hidden(window);
        window.PerformLayout();
        using (var bitmap = new Bitmap(window.Width, window.Height))
        {
            window.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(destination, ImageFormat.Png);
        }
    }
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Directory.CreateDirectory(args[0]);
        ThemePreferences.Path = Path.Combine(args[0], "preview-appearance.json");
        ThemePreferences.SaveDark(true);
        Core.SettingsPath = Path.Combine(args[0], "no-profile.json");
        string package = Path.Combine(args[0], "BabelTower-108");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "VERSION"), "1.0.8");
        using (var window = new MainWindow())
        {
            typeof(MainWindow).GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(window,
                new Settings { GameRoot = @"D:\SteamLibrary\steamapps\common\Deadlock", InstallRoot = @"F:\BabelTower", CurrentFolder = package });
            Call(window, "ShowMain");
            var log = (ListBox)typeof(MainWindow).GetField("logBox", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window);
            log.Items.Clear();
            log.Items.Add("示例预览  /  本地桥与翻译接口尚未检查");
            Render(window, Path.Combine(args[0], "theme-dark-home.png"));
            Call(window, "ToggleTheme");
            Render(window, Path.Combine(args[0], "theme-light-home.png"));
            Call(window, "ToggleTheme");
            Call(window, "ShowImportPage");
            Render(window, Path.Combine(args[0], "theme-dark-update.png"));
            Call(window, "ShowSetup");
            Render(window, Path.Combine(args[0], "theme-dark-settings.png"));
            if (((Timer)typeof(MainWindow).GetField("heartbeat", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(window)).Enabled)
                throw new Exception("Preview activated polling");
        }
        Console.WriteLine("Rendered native light/dark previews; no visible windows, game or real bridge started.");
    }
}
