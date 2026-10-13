using System;
using System.IO;
using System.Text;

namespace BabelManager {
    public static class InterfacePreferences {
        public static string Path = System.IO.Path.Combine(Core.DataDir, "interface-mode.txt");
        public static bool LoadSimple() {
            try {
                Core.NoLinks(Path);
                return !File.Exists(Path) || new FileInfo(Path).Length > 64 || File.ReadAllText(Path).Trim() != "full";
            } catch { return true; }
        }
        public static void SaveSimple(bool simple) {
            Core.NoLinks(Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string temporary = Path + ".tmp";
            Core.NoLinks(temporary);
            try {
                File.WriteAllText(temporary, simple ? "simple" : "full", new UTF8Encoding(false));
                if (File.Exists(Path)) File.Replace(temporary, Path, null);
                else File.Move(temporary, Path);
            } finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
