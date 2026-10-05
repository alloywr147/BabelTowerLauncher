using System;using System.IO;using System.Drawing;using System.Drawing.Imaging;using System.Linq;using System.Reflection;using System.Threading;using System.Windows.Forms;using BabelManager;
class OnlineUiTests {
 static int passed;static void Check(bool ok,string m){if(!ok)throw new Exception(m);passed++;Console.WriteLine("PASS "+m);}
 static object Field(MainWindow window,string name){return typeof(MainWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window);}
 static void Call(MainWindow window,string name,params object[] args){typeof(MainWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,args);}
 static Control[] All(Control c){return c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(All(x))).ToArray();}
 static void Hidden(Control c){typeof(Control).GetMethod("CreateControl",BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(bool)},null).Invoke(c,new object[]{true});foreach(Control child in c.Controls)Hidden(child);}
 [STAThread]static void Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var window=new MainWindow()){
  Call(window,"ShowImportPage");var controls=All(window);Check(controls.OfType<Button>().Any(b=>b.Text=="检查官方更新"),"update page exposes official repository update action");
  Check(controls.OfType<Button>().Any(b=>b.Text=="选择压缩包")&&controls.Any(c=>c.AccessibleName=="新版压缩包拖放区域"&&c.AllowDrop),"manual archive selection and drop remain available beside online updating");
  Check(!((Button)Field(window,"officialDownload")).Enabled,"download cannot start before a valid official release is selected");
  using(var cancellation=new CancellationTokenSource()){typeof(MainWindow).GetField("onlineCancellation",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,cancellation);Call(window,"SetBusy",true);Check(!((Button)Field(window,"officialCheck")).Enabled&&!((Button)Field(window,"update")).Enabled&&((Button)Field(window,"officialCancel")).Enabled,"in-progress online work blocks conflicting actions while cancellation stays available");typeof(MainWindow).GetField("onlineCancellation",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(window,null);Call(window,"SetBusy",false);Check(((Button)Field(window,"officialCheck")).Enabled&&!((Button)Field(window,"officialCancel")).Enabled,"completed online work restores check button and disables cancellation");}
  Hidden(window);window.PerformLayout();foreach(var button in All(window).OfType<Button>()){Check(button.Right<=button.Parent.ClientSize.Width&&button.Bottom<=button.Parent.ClientSize.Height,"update action fits parent: "+button.Text);}
  var body=(Panel)Field(window,"body");Check(body.Controls.Cast<Control>().All(c=>c.Bottom<=body.ClientSize.Height),"update log and all cards fit viewport without bottom clipping");
  if(args.Length>0){using(var bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(args[0],ImageFormat.Png);}}
  Check(!((System.Windows.Forms.Timer)Field(window,"heartbeat")).Enabled,"online UI verification does not start bridge polling or game operations");
 }Console.WriteLine("ONLINE UI RESULT "+passed+" passed");}catch(Exception e){Console.WriteLine("FAIL "+(e.InnerException??e).Message);Environment.ExitCode=1;}}
}
