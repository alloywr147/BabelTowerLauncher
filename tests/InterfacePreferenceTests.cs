using System;
using System.IO;
using BabelManager;

class InterfacePreferenceTests {
    static int passed;
    static void Assert(bool ok, string label) { if (!ok) throw new Exception(label); passed++; Console.WriteLine("PASS " + label); }
    static void Main() {
        string folder = Path.Combine(Path.GetTempPath(), "BT-mode-" + Guid.NewGuid().ToString("N"));
        InterfacePreferences.Path = Path.Combine(folder, "interface-mode.txt");
        Assert(InterfacePreferences.LoadSimple(), "fresh install defaults to simple mode");
        Assert(!Directory.Exists(folder), "reading default mode does not create personal data");
        InterfacePreferences.SaveSimple(false);
        Assert(!InterfacePreferences.LoadSimple(), "existing full mode choice is retained");
        Assert(File.ReadAllText(InterfacePreferences.Path) == "full", "full mode saved as local preference");
        InterfacePreferences.SaveSimple(true);
        Assert(InterfacePreferences.LoadSimple(), "user can return to simple mode");
        Assert(!File.Exists(InterfacePreferences.Path + ".tmp"), "preference save leaves no temporary file");
        File.WriteAllText(InterfacePreferences.Path, "unexpected");
        Assert(InterfacePreferences.LoadSimple(), "invalid preference defaults to simple mode");
        File.WriteAllText(InterfacePreferences.Path, new string('x', 80));
        Assert(InterfacePreferences.LoadSimple(), "oversized preference defaults to simple mode");
        Console.WriteLine("TOTAL " + passed + " interface preference checks passed");
    }
}
