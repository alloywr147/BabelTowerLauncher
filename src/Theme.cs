using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;
namespace BabelManager {
 public static class Theme {
  // Endfield family, moderate depth. Native control behavior is retained.
  public static readonly Color Background=Color.FromArgb(232,232,227),Surface=Color.FromArgb(242,242,240),Border=Color.FromArgb(192,192,185),Text=Color.FromArgb(25,25,25),Muted=Color.FromArgb(92,92,86),Accent=Color.FromArgb(255,250,0),Danger=Color.FromArgb(166,49,42),Success=Color.FromArgb(46,114,78),Dark=Color.FromArgb(25,25,25),Light=Color.FromArgb(242,242,240),AccentTint=Color.FromArgb(244,242,184);
  public static GraphicsPath Cut(RectangleF r,float cut){var p=new GraphicsPath();float c=Math.Min(cut,Math.Min(r.Width,r.Height)/3);p.AddPolygon(new[]{new PointF(r.Left+c,r.Top),new PointF(r.Right,r.Top),new PointF(r.Right,r.Bottom-c),new PointF(r.Right-c,r.Bottom),new PointF(r.Left,r.Bottom),new PointF(r.Left,r.Top+c)});return p;}
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
     using(var p=new Pen(Color.FromArgb(213,213,204),1)){
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
   var fill=!Enabled?Color.FromArgb(211,211,204):Primary?(pressed?Color.FromArgb(222,218,0):hover?Theme.Dark:Theme.Accent):(pressed?Theme.Dark:hover?Theme.AccentTint:Theme.Surface);
   using(var path=Theme.Cut(new RectangleF(1,1,Width-3,Height-3),Primary?12:0)){using(var b=new SolidBrush(fill))g.FillPath(b,path);if(!Primary)using(var pen=new Pen(Theme.Border))g.DrawPath(pen,path);}
   var ink=!Enabled?Theme.Muted:Primary&&hover&&!pressed?Theme.Accent:!Primary&&pressed?Theme.Light:Theme.Text;
   TextRenderer.DrawText(g,Text,Font,ClientRectangle,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
   if(Focused&&ShowFocusCues){using(var pen=new Pen(ink,2))using(var path=Theme.Cut(new RectangleF(6,6,Width-13,Height-13),Primary?8:0))g.DrawPath(pen,path);}
  }
 }
}
