using System;using System.Reflection;using System.Windows.Forms;using BabelManager;
class UIStateTests {
 static int count;static void Assert(bool ok,string message){if(!ok)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
 static object Field(MainWindow window,string name){return typeof(MainWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window);}
 static void Call(MainWindow window,string name,params object[] args){typeof(MainWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,args);}
 [STAThread]static void Main(){try{Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var window=new MainWindow()){
  // No Show, no message loop, no timer tick or live bridge/game operation.
  Call(window,"ShowDiagnostics");var state=(Connectivity)Field(window,"connectivity");state.RecordCheck(new CheckResult{Local=true,Key="fixture",Translation=false,Error="API quota exceeded"},DateTime.UtcNow);state.ApplyProbe(new ProbeResult{Local=true,Key="fixture"},state.Revision,DateTime.UtcNow);Call(window,"RenderChecks");Assert(((Label)Field(window,"translation")).Text.Contains("未通过"),"healthy poll does not hide failed API result on screen");
  Call(window,"ShowMain");Assert(((Label)Field(window,"translation")).Text.Contains("未通过"),"navigation preserves latest translation failure");
  Call(window,"ShowDiagnostics");Call(window,"SetBusy",true);Assert(!((Button)Field(window,"update")).Enabled&&!((Button)Field(window,"repairButton")).Enabled&&!((Button)Field(window,"settingsButton")).Enabled,"check repair and navigation are blocked during an operation");Call(window,"SetBusy",false);Assert(((Button)Field(window,"update")).Enabled&&((Button)Field(window,"repairButton")).Enabled,"controls reenable after operation");
  Assert(!((Timer)Field(window,"heartbeat")).Enabled,"hidden preview never activates service polling");
 }Console.WriteLine("UI RESULT "+count+" passed");}catch(Exception e){Console.WriteLine("FAIL "+(e.InnerException??e).Message);Environment.ExitCode=1;}}
}
