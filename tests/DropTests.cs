using System;using System.IO;using System.Drawing;using System.Windows.Forms;using BabelManager;
class DropLabel:Label{
 public DragEventArgs SimulateEnter(string[] files){var d=new DataObject();d.SetData(DataFormats.FileDrop,files);var a=new DragEventArgs(d,0,0,0,DragDropEffects.Copy,DragDropEffects.None);OnDragEnter(a);return a;}
 public void Drop(string[] files){var d=new DataObject();d.SetData(DataFormats.FileDrop,files);OnDragDrop(new DragEventArgs(d,0,0,0,DragDropEffects.Copy,DragDropEffects.Copy));}
}
class DropTests{
 static void Ok(bool yes,string message){if(!yes)throw new Exception(message);Console.WriteLine("PASS "+message);}
 [STAThread]static void Main(){string dir=Path.Combine(Path.GetTempPath(),"BTDrop-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);try{
  string zip=Path.Combine(dir,"新版.zip");File.WriteAllText(zip,"fixture");bool busy=false;int imported=0,errors=0;string picked="";
  using(var panel=new Panel{BackColor=Color.White})using(var label=new DropLabel()){panel.Controls.Add(label);ArchiveDrop.Attach(panel,()=>busy,p=>{imported++;picked=p;},e=>errors++,Color.Lavender);
   Ok(label.SimulateEnter(new[]{zip}).Effect==DragDropEffects.Copy,"archive accepted over child label");label.Drop(new[]{zip});Ok(imported==1&&picked==zip,"child label routes drop to same import operation");Ok(panel.BackColor==Color.White,"drop clears highlight");
   label.Drop(new[]{zip,zip});Ok(imported==1&&errors==1,"multiple files rejected without importing");label.Drop(new[]{dir});Ok(imported==1&&errors==2,"folder drop rejected without importing");
   busy=true;Ok(label.SimulateEnter(new[]{zip}).Effect==DragDropEffects.None,"busy page refuses drag");label.Drop(new[]{zip});Ok(imported==1,"busy page cannot start a second update");
  }Console.WriteLine("ALL DROP TESTS PASSED");
 }catch(Exception ex){Console.WriteLine("FAIL "+ex.Message);Environment.ExitCode=1;}finally{{string cleanup=Path.GetFullPath(dir);string temporary=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!cleanup.StartsWith(temporary,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(cleanup).StartsWith("BTDrop-",StringComparison.Ordinal))throw new Exception("unsafe fixture cleanup");Directory.Delete(cleanup,true);}}}
}
