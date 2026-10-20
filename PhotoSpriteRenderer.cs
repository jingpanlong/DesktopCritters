using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
namespace DesktopCritters {
internal static class PhotoSpriteRenderer {
    private sealed class Master{internal Bitmap Image;internal PointF Anchor;internal byte[] Pixels;internal int Stride;}
    private sealed class Entry{internal string Key;internal Bitmap Image;internal long Bytes;}
    private static readonly Dictionary<string,Master> masters=new Dictionary<string,Master>();
    private static readonly LinkedList<Entry> lru=new LinkedList<Entry>();
    private static long bytes;
    internal static long CacheBytes{get{return bytes;}}
    internal static int CanvasFor(float scale){return Math.Max(96,(int)Math.Ceiling(190*scale+40));}
    internal static int TextureLevel(float scale){double size=120*scale;return size<=128?128:size<=256?256:size<=512?512:1024;}
    private static Master Load(string species){Master m;if(masters.TryGetValue(species,out m))return m;
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Critters.Assets."+species+"-HD.png")){if(s==null)throw new InvalidOperationException("缺少高清母图："+species);using(var b=new Bitmap(s)){m=new Master{Image=b.Clone(new Rectangle(0,0,b.Width,b.Height),PixelFormat.Format32bppPArgb)};}}
        m.Anchor=BodyCenter(m.Image);var bits=m.Image.LockBits(new Rectangle(0,0,m.Image.Width,m.Image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);try{m.Stride=bits.Stride;m.Pixels=new byte[bits.Stride*m.Image.Height];Marshal.Copy(bits.Scan0,m.Pixels,0,m.Pixels.Length);}finally{m.Image.UnlockBits(bits);}masters.Add(species,m);return m;
    }
    private static PointF BodyCenter(Bitmap b){var d=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);try{
        byte[] px=new byte[d.Stride*b.Height];Marshal.Copy(d.Scan0,px,0,px.Length);double sx=0,sy=0,count=0;int r=b.Width/35;
        for(int y=r;y<b.Height-r;y+=4)for(int x=r;x<b.Width-r;x+=4){if(px[y*d.Stride+x*4+3]<240||px[(y-r)*d.Stride+x*4+3]<240||px[(y+r)*d.Stride+x*4+3]<240||px[y*d.Stride+(x-r)*4+3]<240||px[y*d.Stride+(x+r)*4+3]<240)continue;sx+=x;sy+=y;count++;}
        return count>0?new PointF((float)(sx/count),(float)(sy/count)):new PointF(b.Width/2f,b.Height/2f);
    }finally{b.UnlockBits(d);}}
    internal static int FrameIndex(double phase,string state){if(state=="睡觉"||state=="休息"||state=="装死"||state=="进食"||state=="被捧起"||state=="屏外探索")return 0;double p=(phase%(Math.PI*2)+Math.PI*2)%(Math.PI*2);return (int)(p/(Math.PI*2)*16)%16;}
    private static PointF Delta(double u,double v,double phase){
        double w=Math.Max(0,Math.Min(1,(Math.Abs(v)-.095)/.17));w=w*w*(3-2*w);
        double lane=Math.Cos((u+.02)*Math.PI*5),cycle=phase+(v>0?Math.PI:0)+(1-lane)*Math.PI/2;
        double dx=Math.Sin(cycle)*.027*w,dy=-Math.Sign(v)*Math.Max(0,Math.Cos(cycle))*.013*w;
        if(u>.32){dx*=.25;dy=Math.Sin(phase*.5+Math.Sign(v))*.005*Math.Min(1,(u-.32)*6);}
        return new PointF((float)dx,(float)dy);
    }
    private static Bitmap Texture(string species,int frame,int level){
        string key=species+":"+frame+":"+level;for(var n=lru.First;n!=null;n=n.Next)if(n.Value.Key==key){var e=n.Value;lru.Remove(n);lru.AddFirst(e);return e.Image;}
        Master m=Load(species);var bitmap=new Bitmap(level,level,PixelFormat.Format32bppPArgb);double phase=frame/16.0*Math.PI*2;
        // Inverse texture mapping avoids the cracks caused by separately clipped
        // triangles. Solve the displacement on a grid, interpolate source UVs,
        // then bilinearly sample the original premultiplied HD pixels once.
        const int grid=32;float[,] mx=new float[grid+1,grid+1],my=new float[grid+1,grid+1];
        for(int gy=0;gy<=grid;gy++)for(int gx=0;gx<=grid;gx++){
            double du=(gx/(double)grid-.5)/.78,dv=(gy/(double)grid-.5)/.78,u=du,v=dv;
            for(int j=0;j<4;j++){var delta=Delta(u,v,phase);u=du-delta.X;v=dv-delta.Y;}
            mx[gx,gy]=(float)(m.Anchor.X+u*m.Image.Width);my[gx,gy]=(float)(m.Anchor.Y+v*m.Image.Width);
        }
        var output=bitmap.LockBits(new Rectangle(0,0,level,level),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try{byte[] pixels=new byte[output.Stride*level];for(int y=0;y<level;y++){
            float fy=(y+.5f)*grid/level;int cy=Math.Min(grid-1,(int)fy);fy-=cy;
            for(int x=0;x<level;x++){float fx=(x+.5f)*grid/level;int cx=Math.Min(grid-1,(int)fx);fx-=cx;
                float ax=mx[cx,cy]+(mx[cx+1,cy]-mx[cx,cy])*fx,bx=mx[cx,cy+1]+(mx[cx+1,cy+1]-mx[cx,cy+1])*fx;
                float ay=my[cx,cy]+(my[cx+1,cy]-my[cx,cy])*fx,by=my[cx,cy+1]+(my[cx+1,cy+1]-my[cx,cy+1])*fx;
                float sx=ax+(bx-ax)*fy,sy=ay+(by-ay)*fy;if(sx<0||sy<0||sx>=m.Image.Width-1||sy>=m.Image.Height-1)continue;
                int ix=(int)sx,iy=(int)sy,p=iy*m.Stride+ix*4,q=y*output.Stride+x*4;float tx=sx-ix,ty=sy-iy;
                for(int c=0;c<4;c++){float top=m.Pixels[p+c]+(m.Pixels[p+4+c]-m.Pixels[p+c])*tx,bottom=m.Pixels[p+m.Stride+c]+(m.Pixels[p+m.Stride+4+c]-m.Pixels[p+m.Stride+c])*tx;pixels[q+c]=(byte)Math.Max(0,Math.Min(255,top+(bottom-top)*ty));}
            }
        }Marshal.Copy(pixels,0,output.Scan0,pixels.Length);}finally{bitmap.UnlockBits(output);}
        long cost=(long)level*level*4;while(bytes+cost>48L*1024*1024&&lru.Count>0){var old=lru.Last.Value;lru.RemoveLast();bytes-=old.Bytes;old.Image.Dispose();}
        lru.AddFirst(new Entry{Key=key,Image=bitmap,Bytes=cost});bytes+=cost;return bitmap;
    }
    internal static Bitmap Render(string species,double phase,float heading,float scale,string state,bool trails){
        scale=Math.Max(.25f,Math.Min(6,scale));int size=CanvasFor(scale);var result=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
        Bitmap texture=Texture(species,FrameIndex(phase,state),TextureLevel(scale));
        using(var g=Graphics.FromImage(result)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(size/2f,size/2f);g.RotateTransform(heading);
            float width=120*scale/.78f;
            for(int i=0;i<3;i++)using(var b=new SolidBrush(Color.FromArgb(5,0,0,0)))g.FillEllipse(b,(-28-i)*scale,(-9-i+2)*scale,(56+i*2)*scale,(18+i*2)*scale);
            g.DrawImage(texture,-width/2,-width/2,width,width);
        }return result;
    }
    internal static Size MasterSize(string species){return Load(species).Image.Size;}
    internal static void Verify(){foreach(var p in CreatureCatalog.All){Master m=Load(p.Id);if(m.Image.Width<1000||m.Image.Height<1000)throw new Exception("HD source too small");if(m.Image.GetPixel(0,0).A>2)throw new Exception("HD alpha missing");}if(bytes>48L*1024*1024)throw new Exception("Texture cache over budget");}
    internal static void ClearCache(){foreach(var e in lru)e.Image.Dispose();lru.Clear();bytes=0;}
}
}
