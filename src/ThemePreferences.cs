using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BabelManager
{
    public static class ThemePreferences
    {
        public static string Path = System.IO.Path.Combine(Core.DataDir, "appearance.json");
        public static bool LoadDark()
        {
            try
            {
                Core.NoLinks(Path);
                if (!File.Exists(Path) || new FileInfo(Path).Length > 4096) return true;
                var record = Core.Json().Deserialize<Dictionary<string, string>>(File.ReadAllText(Path));
                string mode;
                return !record.TryGetValue("mode", out mode) || mode != "light";
            }
            catch { return true; }
        }
        public static void SaveDark(bool dark)
        {
            Core.NoLinks(Path);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string temporary = Path + ".tmp";
            Core.NoLinks(temporary);
            try
            {
                File.WriteAllText(temporary, Core.Json().Serialize(new { mode = dark ? "dark" : "light" }), new UTF8Encoding(false));
                if (File.Exists(Path)) File.Replace(temporary, Path, null);
                else File.Move(temporary, Path);
            }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }
    }
}
