using System;using System.IO;using System.Drawing;using System.Drawing.Imaging;using System.Linq;using System.Reflection;using System.Windows.Forms;using BabelManager;
class LayoutTests {
 static void CreateHidden(Control c){typeof(Control).GetMethod("CreateControl",BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{typeof(bool)},null).Invoke(c,new object[]{true});foreach(Control child in c.Controls)CreateHidden(child);}
 [STAThread] static void Main(string[] args){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var window=new MainWindow()){
  typeof(MainWindow).GetMethod("ShowSetup",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null);window.PerformLayout();CreateHidden(window);
  using(var bitmap=new Bitmap(window.Width,window.Height)){window.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(args[0],ImageFormat.Png);}
  var body=(Panel)typeof(MainWindow).GetField("body",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window);
  int checkedButtons=0,blocked=0;foreach(Button button in body.Controls.OfType<Button>().Where(b=>b.Text=="自动检测")){
   checkedButtons++;int index=body.Controls.GetChildIndex(button);foreach(Control other in body.Controls){if(other==button||body.Controls.GetChildIndex(other)>=index)continue;Rectangle overlap=Rectangle.Intersect(button.Bounds,other.Bounds);if(overlap.Width>0&&overlap.Height>0){blocked++;Console.WriteLine("OCCLUDED "+button.Text+" by "+other.Text+" "+overlap.Width+"x"+overlap.Height);}}
  }
  if(checkedButtons!=2||blocked!=0)throw new Exception("Automatic detection buttons overlap foreground controls: "+blocked);
  if(((Timer)typeof(MainWindow).GetField("heartbeat",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window)).Enabled)throw new Exception("Hidden test activated polling");
  Console.WriteLine("PASS both automatic detection buttons are unobstructed; no live polling");
 }}catch(Exception e){Console.WriteLine("FAIL "+(e.InnerException??e).Message);Environment.ExitCode=1;}}
}
