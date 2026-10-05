using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;
namespace BabelManager {
 public sealed class ThemePalette {
  public readonly bool IsDark;
  public readonly Color Background,Surface,Border,Text,Muted,Accent,Danger,Success,Dark,Light,AccentTint,Contour,Disabled,ButtonInk;
  public ThemePalette(bool dark){
   IsDark=dark;Accent=Color.FromArgb(255,250,0);ButtonInk=Color.FromArgb(20,21,22);Light=Color.FromArgb(242,242,240);
   Background=dark?Color.FromArgb(16,17,18):Color.FromArgb(232,232,227);
   Surface=dark?Color.FromArgb(25,27,29):Color.FromArgb(242,242,240);
   Border=dark?Color.FromArgb(108,114,118):Color.FromArgb(192,192,185);
   Text=dark?Color.FromArgb(235,237,237):Color.FromArgb(25,25,25);
   Muted=dark?Color.FromArgb(164,171,176):Color.FromArgb(92,92,86);
   Danger=dark?Color.FromArgb(255,141,127):Color.FromArgb(166,49,42);
   Success=dark?Color.FromArgb(123,207,158):Color.FromArgb(46,114,78);
   Dark=dark?Color.FromArgb(10,11,12):Color.FromArgb(25,25,25);
   AccentTint=dark?Color.FromArgb(52,50,26):Color.FromArgb(244,242,184);
   Contour=dark?Color.FromArgb(46,51,55):Color.FromArgb(213,213,204);
   Disabled=dark?Color.FromArgb(45,49,52):Color.FromArgb(211,211,204);
  }
 }
 public static class Theme {
  // Both palettes share the same flat industrial shapes and restrained accent.
  public static ThemePalette Current=new ThemePalette(false);
  public static bool IsDark{get{return Current.IsDark;}}
  public static Color Background{get{return Current.Background;}}public static Color Surface{get{return Current.Surface;}}public static Color Border{get{return Current.Border;}}public static Color Text{get{return Current.Text;}}public static Color Muted{get{return Current.Muted;}}public static Color Accent{get{return Current.Accent;}}public static Color Danger{get{return Current.Danger;}}public static Color Success{get{return Current.Success;}}public static Color Dark{get{return Current.Dark;}}public static Color Light{get{return Current.Light;}}public static Color AccentTint{get{return Current.AccentTint;}}public static Color Contour{get{return Current.Contour;}}public static Color Disabled{get{return Current.Disabled;}}public static Color ButtonInk{get{return Current.ButtonInk;}}
  public static void SetDark(bool dark){Current=new ThemePalette(dark);}
  static Color BackgroundColor(Color value,ThemePalette old){if(value==old.Background)return Background;if(value==old.Surface)return Surface;if(value==old.AccentTint)return AccentTint;if(value==old.Dark)return Dark;return value;}
  static Color ForegroundColor(Color value,ThemePalette old){if(value==old.Text)return Text;if(value==old.Muted)return Muted;if(value==old.Success)return Success;if(value==old.Danger)return Danger;return value;}
  public static void Recolor(Control root,ThemePalette old){
   // Change colors in place. Recreating controls would lose editable input and async references.
   if(root.BackColor!=Color.Transparent)root.BackColor=root is Form?Background:BackgroundColor(root.BackColor,old);
   var button=root as ModernButton;root.ForeColor=button!=null?(button.Primary?ButtonInk:Text):ForegroundColor(root.ForeColor,old);
   foreach(Control child in root.Controls)Recolor(child,old);root.Invalidate();
  }
  [System.Runtime.InteropServices.DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
  public static void ApplyChrome(Form form){if(!form.IsHandleCreated)return;try{int dark=IsDark?1:0;if(DwmSetWindowAttribute(form.Handle,20,ref dark,4)!=0)DwmSetWindowAttribute(form.Handle,19,ref dark,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
  public static GraphicsPath Cut(RectangleF r,float cut){var p=new GraphicsPath();float c=Math.Min(cut,Math.Min(r.Width,r.Height)/3);p.AddPolygon(new[]{new PointF(r.Left+c,r.Top),new PointF(r.Right,r.Top),new PointF(r.Right,r.Bottom-c),new PointF(r.Right-c,r.Bottom),new PointF(r.Left,r.Bottom),new PointF(r.Left,r.Top+c)});return p;}
 }
 public class ModernProgressBar:ProgressBar {
  public ModernProgressBar(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Theme.Disabled);double fraction=(Value-Minimum)/(double)Math.Max(1,Maximum-Minimum);using(var brush=new SolidBrush(Theme.Accent))e.Graphics.FillRectangle(brush,0,0,(int)(Width*fraction),Height);}
 }
 public class Card:Panel {
  public bool Hero,Terrain;
  public Card(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Theme.Surface;}
  protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Background:Parent.BackColor);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var state=g.Save();
   using(var path=Theme.Cut(new RectangleF(1,1,Width-3,Height-3),0)){using(var brush=new SolidBrush(Hero?Theme.Dark:BackColor))g.FillPath(brush,path);if(!Hero&&!Terrain)using(var pen=new Pen(Theme.Border))g.DrawPath(pen,path);g.SetClip(path);
    if(Terrain){
     // Original abstract terrain: a quiet backdrop, never connection telemetry.
     g.SetClip(new Rectangle(Width*3/5,0,Width*2/5,Height),CombineMode.Intersect);
     float cx=Width-70,cy=Height/2;
     using(var p=new Pen(Theme.Contour,1)){
      for(int i=0;i<15;i++){var points=new PointF[40];for(int j=0;j<points.Length;j++){double angle=j*Math.PI*2/points.Length;double radius=(26+i*13)*(1+0.13*Math.Sin(angle*3+0.3)+0.09*Math.Cos(angle*5-0.6));points[j]=new PointF(cx+(float)(Math.Cos(angle)*radius*1.12),cy+(float)(Math.Sin(angle)*radius*0.9));}using(var contour=new GraphicsPath()){contour.AddClosedCurve(points,0.6f);g.DrawPath(p,contour);}}
     }
    }
   }g.Restore(state);base.OnPaint(e);
  }
 }
 public class ModernButton:Button {
  bool primary,hover,pressed;
  public bool Primary{get{return primary;}set{primary=value;UpdateShape();Invalidate();}}
  public ModernButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
  void UpdateShape(){if(Width>4&&Height>4){var previous=Region;using(var path=Theme.Cut(new RectangleF(1,1,Width-3,Height-3),Primary?12:0))Region=new Region(path);if(previous!=null)previous.Dispose();}}
  protected override void OnResize(EventArgs e){base.OnResize(e);UpdateShape();}
  protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
  protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
  protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;if(Parent==null)g.Clear(Theme.Background);else{var state=g.Save();g.TranslateTransform(-Left,-Top);var parentPaint=new PaintEventArgs(g,new Rectangle(Left,Top,Width,Height));InvokePaintBackground(Parent,parentPaint);InvokePaint(Parent,parentPaint);g.Restore(state);}g.SmoothingMode=SmoothingMode.AntiAlias;
   var fill=!Enabled?Theme.Disabled:Primary?(pressed?Color.FromArgb(222,218,0):hover?Theme.Dark:Theme.Accent):(pressed?Theme.Dark:hover?Theme.AccentTint:Theme.Surface);
   using(var path=Theme.Cut(new RectangleF(1,1,Width-3,Height-3),Primary?12:0)){using(var b=new SolidBrush(fill))g.FillPath(b,path);if(!Primary)using(var pen=new Pen(Theme.Border))g.DrawPath(pen,path);}
   var ink=!Enabled?Theme.Muted:Primary?(hover&&!pressed?Theme.Accent:Theme.ButtonInk):pressed?Theme.Light:Theme.Text;
   TextRenderer.DrawText(g,Text,Font,ClientRectangle,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
   if(Focused&&ShowFocusCues){using(var pen=new Pen(ink,2))using(var path=Theme.Cut(new RectangleF(6,6,Width-13,Height-13),Primary?8:0))g.DrawPath(pen,path);}
  }
 }
}
