using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DesktopCritters {
// One brand mark for everything: window icon, tray icon, installer and the
// sidebar logo. Drawn with GDI+ so it stays crisp at every size, then packed
// into a multi-resolution .ico (16-256 px) for Windows.
internal static class AppIcon {
    private static Bitmap logo,mark;
    private static GraphicsPath Rounded(RectangleF r,float radius){
        var p=new GraphicsPath();
        float d=Math.Max(2f,Math.Min(radius,Math.Min(r.Width,r.Height)/2f))*2f;
        p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);
        p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);
        p.CloseFigure();return p;
    }
    private static Bitmap Embedded(string name){
        try{
            using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){
                if(s==null)return null;
                using(var b=new Bitmap(s))return new Bitmap(b);
            }
        }catch{return null;}
    }
    // Full brand logo (gecko on a leaf plus the word mark), transparent PNG.
    internal static Bitmap Logo(){
        if(logo==null)logo=Embedded("Critters.logo.png");
        return logo==null?null:(Bitmap)logo.Clone();
    }
    // Gecko-only mark for small slots like the sidebar.
    internal static Bitmap Mark(){
        if(mark==null)mark=Embedded("Critters.mark.png");
        return mark==null?null:(Bitmap)mark.Clone();
    }
    internal static byte[] EmbeddedIco(){
        try{
            using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Critters.app.ico")){
                if(s==null)return null;
                var bytes=new byte[s.Length];s.Read(bytes,0,bytes.Length);return bytes;
            }
        }catch{return null;}
    }

    internal static Bitmap Render(int size){
        var bmp=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(bmp)){
            g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.Clear(Color.Transparent);
            float s=size;
            var outer=new RectangleF(s*.035f,s*.035f,s*.93f,s*.93f);
            using(var path=Rounded(outer,s*.245f)){
                using(var br=new LinearGradientBrush(outer,Color.FromArgb(128,178,88),Color.FromArgb(50,80,34),55f))g.FillPath(br,path);
                using(var pen=new Pen(Color.FromArgb(64,255,255,255),Math.Max(1f,s*.012f))){pen.Alignment=PenAlignment.Inset;g.DrawPath(pen,path);}
            }
            var glowArea=new RectangleF(s*.10f,s*.06f,s*.55f,s*.50f);
            using(var glowPath=new GraphicsPath()){glowPath.AddEllipse(glowArea);
                using(var pb=new PathGradientBrush(glowPath)){
                    pb.CenterColor=Color.FromArgb(56,255,255,255);
                    pb.SurroundColors=new[]{Color.FromArgb(0,255,255,255)};
                    pb.CenterPoint=new PointF(s*.30f,s*.22f);
                    g.FillPath(pb,glowPath);}}

            Color glyph=Color.FromArgb(250,252,242);
            using(var shadow=new SolidBrush(Color.FromArgb(38,12,26,8)))
                g.FillEllipse(shadow,s*.288f,s*.470f,s*.424f,s*.360f);
            using(var body=new SolidBrush(glyph))g.FillEllipse(body,s*.295f,s*.375f,s*.410f,s*.430f);
            using(var head=new SolidBrush(glyph))g.FillEllipse(head,s*.452f,s*.267f,s*.096f,s*.086f);
            if(size>=20){
                using(var pen=new Pen(glyph,Math.Max(1f,s*.024f))){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;
                    g.DrawBezier(pen,s*.487f,s*.292f,s*.462f,s*.246f,s*.431f,s*.229f,s*.409f,s*.209f);
                    g.DrawBezier(pen,s*.513f,s*.292f,s*.538f,s*.246f,s*.569f,s*.229f,s*.591f,s*.209f);}
            }
            using(var pen=new Pen(Color.FromArgb(58,94,40),Math.Max(1f,s*.030f))){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;
                g.DrawLine(pen,s*.500f,s*.404f,s*.500f,s*.772f);}
            using(var gloss=new SolidBrush(Color.FromArgb(70,255,255,255)))
                g.FillEllipse(gloss,s*.343f,s*.428f,s*.072f,s*.046f);
        }
        return bmp;
    }

    // 32bpp DIB used for the small icon entries; PNG is used for 128/256.
    private static byte[] Dib(Bitmap bmp){
        int w=bmp.Width,h=bmp.Height,stride=w*4,maskStride=((w+31)/32)*4;
        var data=new byte[40+stride*h+maskStride*h];
        using(var bw=new BinaryWriter(new MemoryStream(data))){
            bw.Write(40);bw.Write(w);bw.Write(h*2);bw.Write((short)1);bw.Write((short)32);
            bw.Write(0);bw.Write(stride*h);bw.Write(0);bw.Write(0);bw.Write(0);bw.Write(0);
            for(int y=h-1;y>=0;y--)for(int x=0;x<w;x++){Color c=bmp.GetPixel(x,y);bw.Write(c.B);bw.Write(c.G);bw.Write(c.R);bw.Write(c.A);}
        }
        return data;
    }

    // The shipped .ico is rebuilt from the embedded brand master (Assets/icon-
    // master.png): small entries as 32bpp DIB, 128/256 as PNG — a container format
    // we verified against System.Drawing's Icon decoder.
    internal static void SaveIco(string path){
        Bitmap master=Embedded("Critters.icon.png");
        if(master!=null){
            int[] sizes={16,20,24,32,40,48,64,128,256};
            var payloads=new List<byte[]>();
            foreach(int sz in sizes){
                using(var bmp=Resize(master,sz)){
                    if(sz>=128){using(var ms=new MemoryStream()){bmp.Save(ms,ImageFormat.Png);payloads.Add(ms.ToArray());}}
                    else payloads.Add(Dib(bmp));
                }
            }
            using(var f=File.Create(path))using(var bw=new BinaryWriter(f)){
                bw.Write((short)0);bw.Write((short)1);bw.Write((short)sizes.Length);
                int offset=6+16*sizes.Length;
                for(int i=0;i<sizes.Length;i++){
                    int sz=sizes[i];
                    bw.Write((byte)(sz>=256?0:sz));bw.Write((byte)(sz>=256?0:sz));
                    bw.Write((byte)0);bw.Write((byte)0);bw.Write((short)1);bw.Write((short)32);
                    bw.Write(payloads[i].Length);bw.Write(offset);offset+=payloads[i].Length;
                }
                foreach(var p in payloads)bw.Write(p);
            }
            return;
        }
        byte[] bytes=EmbeddedIco();
        if(bytes!=null){File.WriteAllBytes(path,bytes);return;}
        int[] legacy={16,20,24,32,40,48,64,128,256};
        var legacyPayloads=new List<byte[]>();
        foreach(int sz in legacy){
            using(var bmp=Render(sz)){
                if(sz>=128){using(var ms=new MemoryStream()){bmp.Save(ms,ImageFormat.Png);legacyPayloads.Add(ms.ToArray());}}
                else legacyPayloads.Add(Dib(bmp));
            }
        }
        using(var f=File.Create(path))using(var bw=new BinaryWriter(f)){
            bw.Write((short)0);bw.Write((short)1);bw.Write((short)legacy.Length);
            int offset=6+16*legacy.Length;
            for(int i=0;i<legacy.Length;i++){
                int sz=legacy[i];
                bw.Write((byte)(sz>=256?0:sz));bw.Write((byte)(sz>=256?0:sz));
                bw.Write((byte)0);bw.Write((byte)0);bw.Write((short)1);bw.Write((short)32);
                bw.Write(legacyPayloads[i].Length);bw.Write(offset);offset+=legacyPayloads[i].Length;
            }
            foreach(var p in legacyPayloads)bw.Write(p);
        }
    }
    private static Bitmap Resize(Bitmap src,int size){
        var b=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
        using(var g=Graphics.FromImage(b)){
            g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.SmoothingMode=SmoothingMode.AntiAlias;
            g.DrawImage(src,new Rectangle(0,0,size,size),new Rectangle(0,0,src.Width,src.Height),GraphicsUnit.Pixel);
        }
        return b;
    }

    internal static Icon Load(int size){
        try{
            using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Critters.app.ico")){
                if(s!=null)return new Icon(s,new Size(size,size));
            }
        }catch{}
        Bitmap fallback=Mark();
        if(fallback==null)fallback=Render(size);
        using(fallback){
            IntPtr h=fallback.GetHicon();
            try{using(var tmp=Icon.FromHandle(h))return (Icon)tmp.Clone();}
            finally{DestroyIcon(h);}
        }
    }

    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);
}
}
