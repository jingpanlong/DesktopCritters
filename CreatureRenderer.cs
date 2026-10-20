using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace DesktopCritters {
internal static class CreatureRenderer {
    internal const int Canvas=384;
    internal static Bitmap IconBitmap(){
        using(var source=Render("Ladybug",1,-35,1,"探索",0,false)){
            var icon=new Bitmap(64,64,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(icon)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(source,new Rectangle(0,0,64,64),new Rectangle(source.Width/2-80,source.Height/2-80,160,160),GraphicsUnit.Pixel);}return icon;
        }
    }
    private static void Line(Graphics g,Color c,float w,params PointF[] p){using(var pen=new Pen(c,w)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawLines(pen,p);}}
    private static void Ellipse(Graphics g,RectangleF r,Color a,Color b,float angle){using(var brush=new LinearGradientBrush(r,a,b,angle))g.FillEllipse(brush,r);using(var pen=new Pen(Color.FromArgb(210,17,20,12),.75f))g.DrawEllipse(pen,r);}
    internal static Bitmap Render(string species,double phase,float heading,float scale,string state,int mood,bool trails){
        return CreatureCatalog.Find(species).Paint(phase,heading,scale,state,mood,trails);
    }
    internal static Bitmap RenderBuiltIn(string species,double phase,float heading,float scale,string state,int mood,bool trails){
        return PhotoSpriteRenderer.Render(species,phase,heading,scale,state,trails);
    }
    internal static Bitmap RenderProcedural(string species,double phase,float heading,float scale,string state,int mood,bool trails){
        var bmp=new Bitmap(Canvas,Canvas,PixelFormat.Format32bppPArgb);
        using(Graphics g=Graphics.FromImage(bmp)){
            g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(Canvas/2,Canvas/2);g.RotateTransform(heading);g.ScaleTransform(scale,scale);
            if(trails){for(int i=0;i<7;i++)using(var b=new SolidBrush(Color.FromArgb(45-i*5,154,190,120)))g.FillEllipse(b,-55-i*5,((i%2)*2-1)*9,2,2);}
            for(int i=0;i<4;i++)using(var b=new SolidBrush(Color.FromArgb(6,0,0,0)))g.FillEllipse(b,-28-i, -13-i+4,56+i*2,26+i*2);
            bool ant=species=="Ant",lady=species=="Ladybug",flip=state=="翻身"||state=="装死";
            Color leg=ant?Color.FromArgb(55,30,18):Color.FromArgb(53,47,28);
            float motion=(state=="休息"||state=="睡觉"||state=="装死")?.06f:1f;
            for(int side=-1;side<=1;side+=2){for(int j=0;j<3;j++){
                double cycle=phase+(j%2==0?0:Math.PI)+(side==1?Math.PI:0);
                float swing=(float)Math.Sin(cycle)*7*motion, lift=(float)Math.Max(0,Math.Cos(cycle))*4*motion;
                float x=13-j*11;PointF a=new PointF(x,side*(ant?4:8));PointF b=new PointF(x+8-j*5+swing*.5f,side*(19+lift));PointF c=new PointF(x+14-j*10+swing,side*(30-lift));PointF d=new PointF(c.X-5,side*(34-lift));
                if(state=="梳理"&&j==0){b=new PointF(24,side*10);c=new PointF(34+(float)Math.Sin(phase*2)*3,side*5);d=new PointF(38,side*4);}
                if(flip){b.Y*=.6f;c.Y*=.65f;d.Y*=.6f;c.X+=(float)Math.Sin(phase*2+j)*6;}
                Line(g,Color.FromArgb(45,0,0,0),3,new PointF(a.X+2,a.Y+3),new PointF(b.X+2,b.Y+3),new PointF(c.X+2,c.Y+3));
                Line(g,leg,2.5f,a,b);Line(g,Color.FromArgb(89,74,44),1.6f,b,c);Line(g,leg,.9f,c,d,new PointF(d.X-2,d.Y-side*1.5f));
                using(var brush=new SolidBrush(Color.FromArgb(30,25,16)))g.FillEllipse(brush,b.X-1.6f,b.Y-1.6f,3.2f,3.2f);
                for(int k=1;k<=3;k++){float t=k/4f;PointF p=new PointF(b.X+(c.X-b.X)*t,b.Y+(c.Y-b.Y)*t);Line(g,Color.FromArgb(160,42,33,20),.55f,p,new PointF(p.X-2,p.Y+side*2));}
            }}
            if(ant){
                Ellipse(g,new RectangleF(-29,-10,25,20),Color.FromArgb(117,65,30),Color.FromArgb(25,15,12),65);
                Ellipse(g,new RectangleF(-7,-3,9,6),Color.FromArgb(112,63,32),Color.FromArgb(36,20,13),90);
                Ellipse(g,new RectangleF(0,-6,17,12),Color.FromArgb(143,76,33),Color.FromArgb(43,25,14),90);
                Ellipse(g,new RectangleF(18,-8,17,16),Color.FromArgb(146,80,40),Color.FromArgb(40,20,12),80);
                for(int i=0;i<3;i++)Line(g,Color.FromArgb(100,11,8,5),.65f,new PointF(-23+i*5,-7),new PointF(-25+i*5,0),new PointF(-23+i*5,7));
            }else{
                Ellipse(g,new RectangleF(-29,-(lady?17:13),43,lady?34:26),lady?Color.FromArgb(255,93,48):Color.FromArgb(125,140,67),lady?Color.FromArgb(108,13,15):Color.FromArgb(28,40,17),75);
                if(flip){for(int k=0;k<6;k++)Ellipse(g,new RectangleF(-25+k*6,-11,8,22),Color.FromArgb(109,77,38),Color.FromArgb(39,28,17),90);}
                else {
                    Line(g,Color.FromArgb(215,13,21,12),1,new PointF(-28,0),new PointF(12,0));
                    if(lady){for(int side=-1;side<=1;side+=2)foreach(PointF p in new PointF[]{new PointF(-18,side*8),new PointF(-6,side*11),new PointF(4,side*6)})using(var b=new SolidBrush(Color.FromArgb(230,16,18,17)))g.FillEllipse(b,p.X-3,p.Y-2.8f,6,5.6f);using(var b=new SolidBrush(Color.FromArgb(230,16,18,17)))g.FillEllipse(b,7,-3,6,6);}
                    else {for(int i=0;i<8;i++){float yy=-10+i*2.8f;using(var pen=new Pen(Color.FromArgb(60,191,184,104),.5f))g.DrawBezier(pen,-25,yy,-17,yy-2,0,yy-1,9,yy);}
                        var rnd=new Random(71);for(int i=0;i<80;i++){float x=(float)rnd.NextDouble()*35-25,y=(float)rnd.NextDouble()*22-11;if((x+8)*(x+8)/420+y*y/130<.9)using(var b=new SolidBrush(Color.FromArgb(65,8,16,6)))g.FillEllipse(b,x,y,.8f,.8f);}}
                    using(var path=new GraphicsPath()){path.AddEllipse(-23,-10,25,7);using(var b=new PathGradientBrush(path)){b.CenterColor=Color.FromArgb(90,255,255,211);b.SurroundColors=new Color[]{Color.Transparent};g.FillPath(b,path);}}
                }
                Ellipse(g,new RectangleF(9,-11,17,22),lady?Color.FromArgb(64,66,59):Color.FromArgb(89,100,46),Color.FromArgb(19,25,15),80);
                if(lady){using(var b=new SolidBrush(Color.FromArgb(242,234,192))){g.FillEllipse(b,17,-9,5,5);g.FillEllipse(b,17,4,5,5);}}
                Ellipse(g,new RectangleF(22,-7,12,14),Color.FromArgb(67,65,40),Color.FromArgb(16,20,14),90);
            }
            for(int side=-1;side<=1;side+=2){
                float tremor=(float)Math.Sin(phase*.55+side)*3;
                PointF a=new PointF(ant?31:30,side*4),b=new PointF(ant?42:39,side*(8+tremor*.3f)),c=new PointF(ant?53:47,side*(ant?22:14)+tremor);
                Line(g,leg,ant?1:1.15f,a,b,c);
                using(var brush=new SolidBrush(leg))g.FillEllipse(brush,c.X-1.3f,c.Y-1.3f,2.6f,2.6f);
                using(var brush=new SolidBrush(Color.FromArgb(11,13,10)))g.FillEllipse(brush,ant?29:28,side==1?4:-6,3,2);
                Line(g,leg,1.2f,new PointF(33,side*2),new PointF(37,side*3),new PointF(36,side*.5f));
            }
            g.ResetTransform();
            if(mood>0||state=="睡觉"){
                string mark=state=="睡觉"?"z Z":(state=="受惊"?"!":(state=="进食"?"♥":"·"));
                using(var f=new Font("Segoe UI",12,FontStyle.Bold))using(var b=new SolidBrush(state=="受惊"?Color.FromArgb(239,162,67):Color.FromArgb(134,191,155)))g.DrawString(mark,f,b,Canvas/2+30,Canvas/2-52);
            }
        }return bmp;
    }
}
}
