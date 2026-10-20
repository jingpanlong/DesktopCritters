using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
namespace DesktopCritters {
internal sealed partial class PetForm:Form {
    internal readonly Habitat World;internal readonly Random Rng;internal readonly AnimalProfile Profile;
    internal string Species{get{return Profile.Species;}}
    internal PointF Position{get;set;}
    internal string State="探索";
    internal double Phase,Heading,TargetHeading,StateLeft=3,SurfaceDistance,SocialCooldown,Energy=85,Hunger=35,Trust=25;
    internal Surface Surface;internal int Direction=1,Frames,LayerErrors,Clicks,Trips,Returns,Wins;
    internal double Distance;internal bool Offscreen;internal PetForm Partner;
    internal float ScaleFactor{get{return Settings.Limit(World.Config.Scale*Profile.Size/100,25,600)/100f;}}
    internal bool IsTravel{get{return Offscreen||State=="离屏外出"||State=="返回桌面";}}
    internal bool CanSocial{get{return !dragging&&!IsTravel&&!ChoreState&&SocialCooldown<=0&&State!="睡觉"&&State!="进食"&&State!="装死"&&State!="受惊"&&State!="争斗";}}
    private bool dragging,routeLocked;private Point down,dragOffset;private double decision,currentSpeed,fearCooldown,tripWait,tripCountdown;
    private PointF tripOutside,tripInside;private FoodSpot food;private string lastSig;
    private IntPtr host;private Rectangle hostRect;private double hostClock,eatTimer;
    internal PetForm(Habitat world,int seed,AnimalProfile profile){World=world;Rng=new Random(seed);Profile=profile;FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=false;Text=profile.Name+" · 微野";Size=new Size(230,230);
        Screen[] screens=world.AllowedScreens();Rectangle b=screens[Rng.Next(screens.Length)].WorkingArea;Position=new PointF(b.Left+b.Width*.3f+(float)Rng.NextDouble()*b.Width*.4f,b.Top+b.Height*.3f+(float)Rng.NextDouble()*b.Height*.4f);Heading=TargetHeading=Rng.Next(360);tripCountdown=30+Rng.Next(90);
    }
    protected override bool ShowWithoutActivation{get{return true;}}
    protected override CreateParams CreateParams{get{var c=base.CreateParams;c.ExStyle|=0x80000|0x80|0x08000000;if(World!=null&&World.Config.ClickThrough)c.ExStyle|=0x20;return c;}}
    internal void ApplyInputMode(){if(!IsHandleCreated)return;int s=Native.GetWindowLong(Handle,-20);Native.SetWindowLong(Handle,-20,World.Config.ClickThrough?s|0x20:s&~0x20);}
    protected override void WndProc(ref Message m){if(m.Msg==0x84){long v=m.LParam.ToInt64();Point p=PointToClient(new Point((short)(v&65535),(short)((v>>16)&65535)));double dx=(p.X-Width/2f)/ScaleFactor,dy=(p.Y-Height/2f)/ScaleFactor,a=-Heading*Math.PI/180,x=dx*Math.Cos(a)-dy*Math.Sin(a),y=dx*Math.Sin(a)+dy*Math.Cos(a);m.Result=new IntPtr(World.Config.ClickThrough||x*x/(62*62)+y*y/(44*44)>1?-1:1);return;}if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
    internal void SetState(string state,double seconds){State=state;StateLeft=seconds;}
    internal void ResetRoute(){Surface=null;routeLocked=false;}
    // Pets no longer float above everything: each one is bound to the window it
    // is standing on and is inserted directly above it in z-order. If the window
    // is dragged, the pet is carried along by the same delta.
    internal void RefreshHost(bool force){
        if(IsDisposed||!IsHandleCreated||Offscreen)return;
        IntPtr desired;
        if(routeLocked&&Surface!=null&&Surface.Handle!=IntPtr.Zero&&Native.IsWindow(Surface.Handle))desired=Native.Root(Surface.Handle);
        else{
            Point pt=Point.Round(Position);
            if(!force&&host!=IntPtr.Zero&&Native.Bindable(host)){Native.RECT r;if(Native.GetWindowRect(host,out r)&&r.Rect.Contains(pt)){SyncHostRect();return;}desired=Native.WindowBelow(Handle,pt);}
            else desired=force?Native.WindowAt(pt):Native.WindowBelow(Handle,pt);
        }
        if(desired==host){SyncHostRect();return;}
        host=desired;hostRect=Rectangle.Empty;SyncHostRect();Native.PlaceAbove(Handle,host);
    }
    private void SyncHostRect(){
        if(host==IntPtr.Zero){hostRect=Rectangle.Empty;return;}
        Native.RECT r;if(!Native.GetWindowRect(host,out r)){host=IntPtr.Zero;hostRect=Rectangle.Empty;return;}
        Rectangle b=r.Rect;
        if(!hostRect.IsEmpty&&!dragging){int dx=b.X-hostRect.X,dy=b.Y-hostRect.Y;if(dx!=0||dy!=0){Position=new PointF(Position.X+dx,Position.Y+dy);lastSig=null;}}
        hostRect=b;
    }
    internal void Scare(){if(IsTravel)return;CancelChore();ResetRoute();Partner=null;TargetHeading=Math.Atan2(Position.Y-Cursor.Position.Y,Position.X-Cursor.Position.X)*180/Math.PI;SetState("受惊",1.3);fearCooldown=4;SocialCooldown=4;}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){dragging=true;Capture=true;CancelChore();CancelTrip();ResetRoute();Partner=null;down=Cursor.Position;dragOffset=new Point((int)Position.X-down.X,(int)Position.Y-down.Y);}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging){Position=new PointF(Cursor.Position.X+dragOffset.X,Cursor.Position.Y+dragOffset.Y);Clamp();SetState("被捧起",1);DrawFrame();}}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Right){World.PetMenu(this).Show(Cursor.Position);return;}if(!dragging)return;dragging=false;Capture=false;Clicks++;Trust=Math.Min(100,Trust+1);RefreshHost(true);if(Math.Abs(Cursor.Position.X-down.X)+Math.Abs(Cursor.Position.Y-down.Y)<5)Scare();else SetState("梳理",2);}
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);NudgeSize(e.Delta);}
    // True when a screen point sits inside the pet's clickable body ellipse —
    // the same maths the hit-test in WndProc uses, expressed in screen space.
    // The global mouse hook asks this to decide which pet a wheel turn belongs to.
    internal bool Contains(Point screen){
        if(IsDisposed||Offscreen)return false;
        double dx=(screen.X-Position.X)/ScaleFactor,dy=(screen.Y-Position.Y)/ScaleFactor,a=-Heading*Math.PI/180;
        double x=dx*Math.Cos(a)-dy*Math.Sin(a),y=dx*Math.Sin(a)+dy*Math.Cos(a);
        return x*x/(62*62)+y*y/(44*44)<=1;
    }
    // Wheel zoom for this pet: works for every species, whether the message
    // arrives through the form or through the global hook.
    internal void NudgeSize(int delta){
        if(IsDisposed||dragging)return;
        Profile.Size=Settings.Limit(Profile.Size+(delta>0?10:-10),50,200);
        World.SaveSoon();DrawFrame();
    }
    protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(Visible&&!IsDisposed)lastSig=null;}
    internal void PickRoute(string kind){CancelChore();ResetRoute();Surface best=null;double score=Double.MaxValue;foreach(var s in World.Map.Items){if(kind!="自由"&&!RouteMatches(s,kind))continue;PointF q=s.At(s.Nearest(Position));if(!World.PointAllowed(q))continue;double dist=Dist(Position,q);double value=dist+Rng.NextDouble()*100;if(s.Kind=="任务栏"||s.Kind=="菜单栏"||s.Kind=="工具栏")value-=80;if(value<score){best=s;score=value;}}
        if(best==null&&kind!="自由"){World.Log(Profile.Name+" 暂未找到"+kind+"，继续探索");SetState("探索",5);return;}if(best!=null){Surface=best;SurfaceDistance=best.Nearest(Position);Direction=Rng.Next(2)==0?1:-1;SetState("前往"+best.Kind,20);}}
    private bool RouteMatches(Surface s,string kind){return kind==s.Kind||(kind=="菜单栏"&&(s.Kind=="工具栏"||s.Kind=="展开菜单"));}
    internal void AttachRoute(Surface route,double distance){Surface=route;SurfaceDistance=distance;routeLocked=true;Position=route.At(distance);SetState("沿"+route.Kind+"攀爬",20);}
    internal void BeginExcursion(int waitSeconds){
        if(dragging||Offscreen)return;CancelChore();Rectangle b=Screen.FromPoint(Point.Round(Position)).Bounds;float radius=85*ScaleFactor+24;
        var outside=new PointF[]{new PointF(b.Left-radius,Math.Max(b.Top+40,Math.Min(b.Bottom-40,Position.Y))),new PointF(b.Right+radius,Math.Max(b.Top+40,Math.Min(b.Bottom-40,Position.Y))),new PointF(Math.Max(b.Left+40,Math.Min(b.Right-40,Position.X)),b.Top-radius),new PointF(Math.Max(b.Left+40,Math.Min(b.Right-40,Position.X)),b.Bottom+radius)};
        double best=Double.MaxValue;int index=-1;for(int i=0;i<outside.Length;i++){if(World.PointAllowed(outside[i]))continue;double d=Dist(Position,outside[i]);if(d<best){best=d;index=i;}}
        if(index<0){ResetExcursionClock();return;}ResetRoute();Partner=null;tripOutside=outside[index];tripInside=new PointF(Math.Max(b.Left+radius,Math.Min(b.Right-radius,tripOutside.X)),Math.Max(b.Top+radius,Math.Min(b.Bottom-radius,tripOutside.Y)));
        // Large pets can exceed a small display; the return point is always inside it.
        tripInside=new PointF(Math.Max(b.Left+10,Math.Min(b.Right-10,tripInside.X)),Math.Max(b.Top+10,Math.Min(b.Bottom-10,tripInside.Y)));
        tripWait=waitSeconds>0?waitSeconds:Rng.Next(World.Config.ReturnMin,World.Config.ReturnMax+1);SetState("离屏外出",30);World.Log(Profile.Name+" 准备外出");
    }
    internal void ReturnNow(){if(!IsTravel)return;Offscreen=false;SetState("返回桌面",30);}
    internal void ResetExcursionClock(){tripCountdown=World.Config.ExcursionInterval*(.65+Rng.NextDouble()*.7)*(Profile.Temperament=="好奇"?.7:1);}
    internal void CancelTrip(){Offscreen=false;ResetExcursionClock();}
    internal void Recall(PointF point){CancelTrip();CancelChore();ResetRoute();Partner=null;Position=point;Clamp();SetState("探索",3);SocialCooldown=4;currentSpeed=0;}
    internal void Tick(double dt){
        if(dragging)return;StateLeft-=dt;SocialCooldown=Math.Max(0,SocialCooldown-dt);fearCooldown=Math.Max(0,fearCooldown-dt);decision-=dt;tripCountdown-=dt;Hunger=Math.Min(100,Hunger+dt*.075);
        hostClock-=dt;if(hostClock<=0){hostClock=.3;RefreshHost(false);}else if(host!=IntPtr.Zero)SyncHostRect();
        if(Offscreen){tripWait-=dt;if(tripWait<=0)ReturnNow();return;}
        if(IsTravel){TickTrip(dt);return;}
        // Chores own their own locomotion (they carry objects or fly), so they run
        // before the shared wandering/feeding logic.
        if(ChoreState){TickChore(dt);DrawFrame();return;}
        if(World.Config.AllowOffscreen&&tripCountdown<=0&&State!="争斗"&&State!="进食"&&State!="睡觉"){BeginExcursion(0);return;}
        if(Partner!=null&&(Partner.IsDisposed||Partner.IsTravel)){Partner=null;SetState("探索",2);}
        if(State=="争斗"){TickFight(dt);DrawFrame();return;}
        if(State=="追逐"||State=="躲避"||State=="结伴"){
            if(StateLeft<=0||Partner==null){Partner=null;SetState("探索",3);}else{double a=Math.Atan2(Partner.Position.Y-Position.Y,Partner.Position.X-Position.X)*180/Math.PI;TargetHeading=State=="躲避"?a+180:a;if(State=="追逐"&&Dist(Position,Partner.Position)<28*ScaleFactor)World.BeginFight(this,Partner,false);}
        }
        if(StateLeft<=0){ResetRoute();Partner=null;if(State=="进食")food=null;ChooseNext();}
        Point mouse=Cursor.Position;double md=Dist(Position,new PointF(mouse.X,mouse.Y));
        double fearFactor=Profile.Temperament=="胆小"?1.4:Profile.Temperament=="好斗"?.7:1;
        if(World.Config.MouseFear&&!World.Config.FollowMouse&&fearCooldown<=0&&md<(55-Trust*.15)*ScaleFactor*fearFactor&&State!="进食"&&State!="睡觉"&&State!="装死")Scare();
        bool social=State=="追逐"||State=="躲避"||State=="结伴";
        if(State=="进食"){
            // One visible bite every ~1.15s; each bite removes one portion and the
            // crumb pile on the marker shrinks until the food is gone.
            if(food==null||!World.Foods.Contains(food)||food.Portions<=0||Hunger<=0){food=null;SetState("梳理",1.6);}
            else{TargetHeading=Math.Atan2(food.Position.Y-Position.Y,food.Position.X-Position.X)*180/Math.PI;Phase+=dt*8.5;eatTimer-=dt;
                while(eatTimer<=0&&food!=null&&food.Portions>0){eatTimer+=1.15;World.Consume(this,food);}
                if(food!=null&&food.Portions<=0){food=null;SetState("梳理",2.2);World.Log(Profile.Name+" 吃完了");}}
        }
        else if(!social&&State!="受惊"&&State!="装死"&&State!="睡觉"){
            FoodSpot nearest=World.NearestFood(Position);
            if(nearest!=null&&(Hunger>25||nearest.Fresh)){
                if(food!=nearest){food=nearest;ResetRoute();SetState("觅食",8);}
                TargetHeading=Math.Atan2(food.Position.Y-Position.Y,food.Position.X-Position.X)*180/Math.PI;
                if(Dist(Position,food.Position)<16*ScaleFactor&&Math.Abs(TargetHeading-Heading)<70){SetState("进食",12);eatTimer=.35;Phase=0;}
            }
        }
        bool still=State=="睡觉"||State=="休息"||State=="梳理"||State=="进食"||State=="装死"||State=="张望"||State=="凝视"||State=="抖动"||State=="晒太阳"||(State=="结伴"&&Partner!=null&&Dist(Position,Partner.Position)<48*ScaleFactor);
        Energy=Math.Max(0,Math.Min(100,Energy+dt*(still?.5:-.025)));
        if(Surface!=null){if(!Surface.Update()){ResetRoute();SetState("探索",2);}else{PointF q=Surface.At(SurfaceDistance);if(!World.PointAllowed(q)){ResetRoute();SetState("探索",2);}else if(routeLocked){double advance=World.Config.Speed*CreatureCatalog.Find(Species).SpeedFactor*dt;PointF old=Position;SurfaceDistance+=Direction*advance;Position=Surface.At(SurfaceDistance);PointF next=Surface.At(SurfaceDistance+Direction*2);TargetHeading=Math.Atan2(next.Y-Position.Y,next.X-Position.X)*180/Math.PI;Turn(dt);AdvanceGait(Dist(old,Position));State="沿"+Surface.Kind+"攀爬";DrawFrame();return;}else{TargetHeading=Math.Atan2(q.Y-Position.Y,q.X-Position.X)*180/Math.PI;if(Dist(Position,q)<8){routeLocked=true;Position=q;State="沿"+Surface.Kind+"攀爬";}}}}
        if(World.Config.FollowMouse&&!still&&!social&&State!="受惊"&&State!="觅食"&&Surface==null){TargetHeading=Math.Atan2(mouse.Y-Position.Y,mouse.X-Position.X)*180/Math.PI;if(md<40*ScaleFactor)still=true;}
        else if(State=="张望"){// curious inspection: keep the eyes on the pointer, slowly
            TargetHeading=Math.Atan2(mouse.Y-Position.Y,mouse.X-Position.X)*180/Math.PI;}
        else if(State=="嗅探"&&decision<=0){TargetHeading+=Rng.Next(-38,39);decision=.35+Rng.NextDouble()*.35;}
        else if(State=="探索"&&decision<=0){TargetHeading+=Rng.Next(-45,46);decision=.9+Rng.NextDouble()*2;}
        double factor=State=="受惊"?2.8:State=="追逐"||State=="躲避"?1.7:State=="嗅探"?.45:1;
        if(!still){Turn(dt);AdvancePosition(dt,World.Config.Speed*CreatureCatalog.Find(Species).SpeedFactor*factor);Clamp();}else currentSpeed=0;
        if(State=="梳理")Phase+=dt;DrawFrame();
    }
    private void Turn(double dt){double rate=State=="张望"?85:(State=="嗅探"?150:(State=="搬运"?170:240));double turn=((TargetHeading-Heading+540)%360+360)%360-180;Heading=(Heading+Math.Max(-dt*rate,Math.Min(dt*rate,turn))+360)%360;}
    private void AdvancePosition(double dt,double target){currentSpeed+=(target-currentSpeed)*Math.Min(1,dt*9);PointF old=Position;double a=Heading*Math.PI/180;Position=new PointF(Position.X+(float)(Math.Cos(a)*currentSpeed*dt),Position.Y+(float)(Math.Sin(a)*currentSpeed*dt));AdvanceGait(Dist(old,Position));}
    private void AdvanceGait(double step){Distance+=step;Phase+=step/(24*ScaleFactor)*Math.PI*2;}
    private void TickTrip(double dt){PointF target=State=="离屏外出"?tripOutside:tripInside;double d=Dist(Position,target);TargetHeading=Math.Atan2(target.Y-Position.Y,target.X-Position.X)*180/Math.PI;Turn(dt);AdvancePosition(dt,Math.Max(130,World.Config.Speed*2.4));
        if(d<Math.Max(8,currentSpeed*dt+2)){Position=target;currentSpeed=0;if(State=="离屏外出"){Offscreen=true;Trips++;SetState("屏外探索",tripWait);Hide();World.Log(Profile.Name+" 已离屏，稍后返回");}else{Returns++;CancelTrip();SetState("探索",4);Heading=TargetHeading;World.Log(Profile.Name+" 回到桌面");}}
        if(StateLeft<=0){World.Log(Profile.Name+" 外出路线超时，已安全召回");Recall(new PointF(Screen.PrimaryScreen.WorkingArea.Left+100,Screen.PrimaryScreen.WorkingArea.Top+100));}if(!Offscreen)DrawFrame();
    }
    private void TickFight(double dt){if(World.Config.Peaceful||!World.Config.Social||Partner==null||StateLeft<=0){PetForm other=Partner;Partner=null;SocialCooldown=8;Energy=Math.Max(0,Energy-3);if(other!=null){TargetHeading=Math.Atan2(Position.Y-other.Position.Y,Position.X-other.Position.X)*180/Math.PI;SetState("躲避",2);Partner=other;}else SetState("休息",2);return;}
        TargetHeading=Math.Atan2(Partner.Position.Y-Position.Y,Partner.Position.X-Position.X)*180/Math.PI;Turn(dt);double jitter=Math.Sin(StateLeft*17)*8*dt;Position=new PointF(Position.X+(float)(Math.Cos(Heading*Math.PI/180)*jitter),Position.Y+(float)(Math.Sin(Heading*Math.PI/180)*jitter));Phase+=dt*12;
    }
    internal void Clamp(){if(IsTravel)return;if(World.PointAllowed(Position))return;Screen nearest=World.NearestScreen(Position);Rectangle b=nearest.Bounds;Position=new PointF(Math.Max(b.Left+5,Math.Min(b.Right-5,Position.X)),Math.Max(b.Top+5,Math.Min(b.Bottom-5,Position.Y)));TargetHeading+=160;ResetRoute();}
    internal static double Dist(PointF a,PointF b){double x=a.X-b.X,y=a.Y-b.Y;return Math.Sqrt(x*x+y*y);}
    internal void DrawFrame(){if(Offscreen||IsDisposed)return;
        // Skip identical frames: static pets no longer burn CPU re-rendering.
        int frame=PhotoSpriteRenderer.FrameIndex(Phase,State);
        float bob=0f;
        if(State=="进食")bob=(float)((Math.Sin(Phase)+1)*.5*3*ScaleFactor);
        else if(State=="抖动")bob=(float)(Math.Sin(Phase*9)*1.7);
        else if(State=="晒太阳")bob=(float)(Math.Sin(Phase*.7)*.9);
        float air=lift*(16*ScaleFactor+12)+airBob;
        string sig=frame+"|"+Math.Round(Heading/2f)+"|"+Math.Round(ScaleFactor,2)+"|"+State+"|"+World.Config.ShowNames+"|"+Math.Round(Position.X)+"|"+Math.Round(Position.Y+bob-air)+"|"+Math.Round(lift,2)+"|"+(leanForward?"L":"");
        if(sig==lastSig)return;lastSig=sig;
        Bitmap bmp=Compose(air);
        try{
        if(Size!=bmp.Size)Size=bmp.Size;
        if(World.Config.ShowNames)using(var g=Graphics.FromImage(bmp))using(var f=new Font("Microsoft YaHei UI",9))using(var b=new SolidBrush(Color.FromArgb(225,220,232,200))){string name=Profile.Name+" · "+State;SizeF s=g.MeasureString(name,f);g.DrawString(name,f,b,(bmp.Width-s.Width)/2,bmp.Height-24);}
        IntPtr dc=Native.GetDC(IntPtr.Zero),mem=Native.CreateCompatibleDC(dc),hb=IntPtr.Zero,old=IntPtr.Zero;try{hb=bmp.GetHbitmap(Color.FromArgb(0));old=Native.SelectObject(mem,hb);var p=new Native.POINT((int)Position.X-bmp.Width/2,(int)(Position.Y+bob-air)-bmp.Height/2);var size=new Native.SIZE(bmp.Width,bmp.Height);var origin=new Native.POINT(0,0);var blend=new Native.BLEND{Alpha=255,Format=1};if(!Native.UpdateLayeredWindow(Handle,dc,ref p,ref size,mem,ref origin,0,ref blend,2)){LayerErrors++;lastSig=null;}Frames++;}
        finally{if(old!=IntPtr.Zero)Native.SelectObject(mem,old);if(hb!=IntPtr.Zero)Native.DeleteObject(hb);if(mem!=IntPtr.Zero)Native.DeleteDC(mem);if(dc!=IntPtr.Zero)Native.ReleaseDC(IntPtr.Zero,dc);}}
        finally{bmp.Dispose();}
    }
}
}
