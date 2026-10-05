using System;using System.IO;using System.Drawing;using System.Drawing.Imaging;using System.Reflection;using System.Windows.Forms;using BabelManager;
class PublicPreview {
 static void Hidden(Control c){typeof(Control).GetMethod("CreateControl",BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(bool)},null).Invoke(c,new object[]{true});foreach(Control child in c.Controls)Hidden(child);}
 [STAThread]static void Main(string[] args){Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  // Force a new empty profile; no local settings or personal paths are loaded.
  Core.SettingsPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"public-preview-no-profile.json");if(File.Exists(Core.SettingsPath))throw new Exception("Public preview profile must not exist");
  Directory.CreateDirectory(args[0]);
  foreach(string page in new[]{"main","settings"})using(var window=new MainWindow()){
   typeof(MainWindow).GetMethod(page=="main"?"ShowMain":"ShowSetup",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null);window.PerformLayout();Hidden(window);
   using(var bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(args[0],"preview-"+page+".png"),ImageFormat.Png);}
   if(((Timer)typeof(MainWindow).GetField("heartbeat",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window)).Enabled||(bool)typeof(MainWindow).GetField("detecting",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window))throw new Exception("Public capture started service polling or path scanning");
   Console.WriteLine("PUBLIC PREVIEW "+page+"; blank profile; no polling or scanning");
  }
 }
}
