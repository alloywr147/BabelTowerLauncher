using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace BabelManager {
    public static class RhineRuntime {
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryEx(string path, IntPtr reserved, uint flags);
        public static void Prepare() {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "lib");
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => {
                string name = new AssemblyName(args.Name).Name;
                if (name != "Microsoft.Web.WebView2.Core" && name != "Microsoft.Web.WebView2.WinForms") return null;
                string file = Path.Combine(folder, name + ".dll");
                return File.Exists(file) ? Assembly.LoadFrom(file) : null;
            };
            string loader = Path.Combine(folder, "WebView2Loader.dll");
            if (File.Exists(loader) && LoadLibraryEx(loader, IntPtr.Zero, 0x00000008) == IntPtr.Zero)
                throw new Exception("无法载入 WebView2 x64 组件，请完整解压启动器。");
        }
    }
}
