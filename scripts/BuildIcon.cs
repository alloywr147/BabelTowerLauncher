using System;using System.IO;using System.Drawing;using System.Drawing.Drawing2D;using System.Drawing.Imaging;using System.Collections.Generic;
class BuildEndfieldIcon {
 // New original geometric asset. The same vector geometry is drawn at every size.
 static readonly Color Ink=Color.FromArgb(25,25,25),Paper=Color.FromArgb(242,242,240),Signal=Color.FromArgb(255,250,0);
 static PointF[] Points(params float[] xy){var result=new PointF[xy.Length/2];for(int i=0;i<result.Length;i++)result[i]=new PointF(xy[2*i],xy[2*i+1]);return result;}
 static Bitmap Draw(int size){using(var large=new Bitmap(size*4,size*4,PixelFormat.Format32bppArgb)){
  using(var g=Graphics.FromImage(large)){g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size*4/512f,size*4/512f);
   using(var b=new SolidBrush(Ink))g.FillPolygon(b,Points(80,24,488,24,488,432,432,488,24,488,24,80));
   using(var shape=new GraphicsPath(FillMode.Alternate)){
    shape.AddPolygon(Points(104,148,208,148,244,184,244,218,218,244,244,270,244,312,208,348,104,348));
    shape.AddPolygon(Points(136,180,198,180,212,194,212,212,198,228,136,228));
    shape.AddPolygon(Points(136,268,198,268,212,282,212,302,198,316,136,316));
    using(var b=new SolidBrush(Paper))g.FillPath(b,shape);
   }
   using(var b=new SolidBrush(Signal))g.FillPolygon(b,Points(266,148,410,148,410,184,356,184,356,348,320,348,320,184,266,184));
  }
  var small=new Bitmap(size,size,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(small)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(large,new Rectangle(0,0,size,size));}return small;
 }}
 static void Ico(string filename){var sizes=new[]{16,20,24,32,40,48,64,96,128,256};var images=new List<byte[]>();foreach(int size in sizes){using(var bitmap=Draw(size))using(var stream=new MemoryStream()){bitmap.Save(stream,ImageFormat.Png);images.Add(stream.ToArray());}}
  using(var writer=new BinaryWriter(File.Create(filename))){writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)(sizes[i]==256?0:sizes[i]));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(images[i].Length);writer.Write(offset);offset+=images[i].Length;}foreach(var data in images)writer.Write(data);}
  using(var icon=new Icon(filename)){if(icon.Width<=0)throw new Exception("Generated ICO cannot be opened by Windows drawing APIs");}
 }
 static void Preview(string filename){using(var bitmap=new Bitmap(640,320,PixelFormat.Format32bppArgb))using(var g=Graphics.FromImage(bitmap)){
  g.Clear(Paper);using(var background=new SolidBrush(Ink))g.FillRectangle(background,320,0,320,320);
  using(var full=Draw(200)){g.DrawImageUnscaled(full,60,34);g.DrawImageUnscaled(full,380,34);}
  int[] sizes={16,24,32,48};int[] offsets={32,90,154,230};foreach(int size in sizes){int x=offsets[Array.IndexOf(sizes,size)];using(var icon=Draw(size)){g.DrawImageUnscaled(icon,x,260-size/2);g.DrawImageUnscaled(icon,x+320,260-size/2);}}
  bitmap.Save(filename,ImageFormat.Png);
 }}
 static void Main(string[] args){Directory.CreateDirectory(args[0]);Ico(Path.Combine(args[0],"app-endfield-1.3.1.ico"));using(var bitmap=Draw(512))bitmap.Save(Path.Combine(args[0],"app-endfield.png"),ImageFormat.Png);using(var bitmap=Draw(128))bitmap.Save(Path.Combine(args[0],"desktop-icon-preview.png"),ImageFormat.Png);Preview(args[1]);Console.WriteLine("Original BT icon created; 10 sizes from 16 through 256; native ICO load passed");}
}
