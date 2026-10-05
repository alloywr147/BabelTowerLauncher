using System;using System.IO;using System.Linq;using System.Drawing;using System.Windows.Forms;
namespace BabelManager {
 public static class ArchiveDrop {
  static string[] Files(IDataObject data){return data!=null&&data.GetDataPresent(DataFormats.FileDrop)?data.GetData(DataFormats.FileDrop) as string[]:null;}
  static bool Valid(string[] files){return files!=null&&files.Length==1&&File.Exists(files[0])&&new[]{".zip",".7z",".rar"}.Contains(Path.GetExtension(files[0]).ToLowerInvariant());}
  public static void Attach(Control zone,Func<bool> busy,Action<string> import,Action<string> error,Color highlight){Color normal=zone.BackColor;Wire(zone,zone,()=>normal,busy,import,error,()=>highlight);}
  public static void AttachThemed(Control zone,Func<bool> busy,Action<string> import,Action<string> error){Wire(zone,zone,()=>Theme.Surface,busy,import,error,()=>Theme.AccentTint);}
  static void Wire(Control target,Control zone,Func<Color> normal,Func<bool> busy,Action<string> import,Action<string> error,Func<Color> highlight){
   target.AllowDrop=true;DragEventHandler enter=(s,e)=>{bool accepted=!busy()&&Valid(Files(e.Data));e.Effect=accepted?DragDropEffects.Copy:DragDropEffects.None;zone.BackColor=accepted?highlight():normal();};target.DragEnter+=enter;target.DragOver+=enter;target.DragLeave+=(s,e)=>zone.BackColor=normal();
   target.DragDrop+=(s,e)=>{zone.BackColor=normal();if(busy())return;var files=Files(e.Data);if(!Valid(files)){error("请一次拖入一个 ZIP、7Z 或 RAR 压缩包。");return;}import(files[0]);};
   foreach(Control child in target.Controls)Wire(child,zone,normal,busy,import,error,highlight);
  }
 }
}
