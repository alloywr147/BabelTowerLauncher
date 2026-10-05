using System;using System.Linq;using System.Reflection;using System.Windows.Forms;using BabelManager;
class NavigationTests {
 static int passed;
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
 static object Field(MainWindow window,string name){return typeof(MainWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window);}
 static void Call(MainWindow window,string name,params object[] args){typeof(MainWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,args);}
 static void Click(Button button){typeof(Button).GetMethod("OnClick",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(button,new object[]{EventArgs.Empty});}
 static Panel Rail(MainWindow window){return ((Panel)Field(window,"body")).Controls.OfType<Panel>().Single(c=>Object.Equals(c.Tag,"navigation"));}
 static Button Nav(MainWindow window,string name){return Rail(window).Controls.OfType<Button>().Single(c=>c.Text==name);}
 [STAThread]static void Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var window=new MainWindow()){
  // Hidden window only. No live update, launch, bridge operation or settings save.
  Call(window,"ShowMain");var body=(Panel)Field(window,"body");
  Check(!body.Controls.Cast<Control>().Any(c=>c.AccessibleName=="版本更新")&&Field(window,"update")==null,"home has no duplicate update strip");
  Click(Nav(window,"更新"));Check((bool)Field(window,"importPage")&&((Button)Field(window,"update")).Text=="选择压缩包","left update opens the working archive import page");
  Check(body.Controls.Cast<Control>().Single(c=>c.AccessibleName=="新版压缩包拖放区域").AllowDrop,"archive page keeps its drag and drop target");
  Click((Button)Field(window,"settingsButton"));Check(!(bool)Field(window,"importPage")&&Field(window,"launch")!=null,"return from update restores game launch page");
  Click(Nav(window,"诊断"));Check((bool)Field(window,"diagnosticPage")&&Field(window,"repairButton")!=null,"diagnostics navigation reaches existing repair controls");
  Click((Button)Field(window,"settingsButton"));Click(Nav(window,"设置"));Check(body.Controls.OfType<ComboBox>().Count()==2,"settings navigation keeps both detectable path selectors");
  Call(window,"ShowMain");Call(window,"SetBusy",true);Check(!Rail(window).Enabled&&!((Button)Field(window,"launch")).Enabled,"all home navigation is disabled while operation is busy");
  Call(window,"SetBusy",false);Check(Rail(window).Enabled&&((Button)Field(window,"launch")).Enabled,"home navigation and launch reenable after operation");
  Check(((ListBox)Field(window,"logBox")).Height>=90,"running log gets the space recovered from duplicate update strip");
  Check(!((Timer)Field(window,"heartbeat")).Enabled,"navigation checks never start live bridge polling");
 }Console.WriteLine("NAVIGATION RESULT "+passed+" passed");}catch(Exception e){Console.WriteLine("FAIL "+(e.InnerException??e).Message);Environment.ExitCode=1;}}
}
