using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace DesktopCritters {
// Species-specific "chores" and the natural idle repertoire. Split out of
// PetForm.cs so the shared locomotion/tick code stays readable.
internal sealed partial class PetForm {
    private DeskIcon carryIcon;
    private FoodSpot pushFood;
    private double carryElapsed,carryPush,carryPause,carryVerify;
    private PointF choreTarget;
    private float lift;
    private float airBob;
    private bool leanForward;
    private int carryMiss;
    internal int Chores,CarrySteps,FlightSteps;
    internal bool IsCarrying{get{return carryIcon!=null;}}
    internal bool ChoreState{get{return State=="找图标"||State=="搬运"||State=="放下"||State=="起飞"||State=="飞行"||State=="降落"||State=="推食物";}}

    // ---------- decision ----------
    // Replaces the old flat random pick: each species first gets a chance at its
    // signature behaviour, then the shared repertoire of small natural actions.
    private void ChooseNext(){
        if(Energy<15){SetState("休息",10);return;}
        bool night=World.Config.NightSleep&&(DateTime.Now.Hour>=23||DateTime.Now.Hour<6);
        if(night&&Rng.Next(100)<34){SetState("睡觉",Rng.Next(8,20));return;}
        bool curious=Profile.Temperament=="好奇";
        int r=Rng.Next(100);
        // Keep chores within each animal's real body plan.
        if(CreatureCatalog.CarriesIcons(Species)&&World.Config.CarryIcons&&World.IconCarryFree(this)&&r<(curious?26:20)){if(TryStartCarry())return;}
        else if(CreatureCatalog.CanFly(Species)&&World.Config.Flight&&r<22){if(TryFlight())return;}
        else if(CreatureCatalog.PushesFood(Species)&&World.Config.CarryIcons&&r<18){if(TryPushFood())return;}
        else if(Species=="Bee"&&r<22){SetState("采蜜",Rng.Next(3,7));return;}
        else if(Species=="Butterfly"&&r<20){SetState("停翅晒暖",Rng.Next(5,10));return;}
        else if(Species=="Spider"&&r<20){SetState(Rng.Next(2)==0?"结网":"伏击",Rng.Next(4,9));return;}
        else if(Species=="Mantis"&&r<18){SetState(Rng.Next(2)==0?"潜伏":"凝视",Rng.Next(3,7));return;}
        else if(Species=="Snail"&&r<28){SetState("晒太阳",Rng.Next(6,12));return;}
        else if(Species=="Caterpillar"&&r<22){SetState("啃叶",Rng.Next(3,7));return;}
        int pick=Rng.Next(100);
        if(curious)pick=Math.Min(96,(int)(pick*0.82));
        if(World.Config.FollowMouse&&pick<28){SetState("探索",Rng.Next(3,7));return;}
        if(pick<18)SetState("休息",Rng.Next(2,5));
        else if(pick<28)SetState("梳理",Rng.Next(2,4));
        else if(pick<38)SetState("嗅探",Rng.Next(2,4));
        else if(pick<46)SetState("张望",Rng.Next(2,4));
        else if(pick<52)SetState("抖动",1.3);
        else if(pick<60)SetState("晒太阳",Rng.Next(4,9));
        else if(pick<87&&!World.Config.FollowMouse)PickRoute(Profile.Route);
        else{TargetHeading=Rng.Next(360);SetState("探索",Rng.Next(3,8));}
    }

    // ---------- chore: ant carries a desktop icon ----------
    internal bool TryStartCarry(){
        if(!World.Config.CarryIcons||IsTravel||Surface!=null||dragging)return false;
        if(!World.IconCarryFree(this))return false;
        DeskIcon icon=World.PickIconNear(Position,820);
        if(icon==null)return false;
        carryIcon=icon;carryElapsed=0;carryPush=0;carryMiss=0;leanForward=false;
        ResetRoute();Partner=null;
        SetState("找图标",26);
        return true;
    }
    private void TickSeek(double dt){
        if(carryIcon==null){SetState("探索",2);return;}
        carryElapsed+=dt;
        double reach=carryIcon.Glyph*.5+12*ScaleFactor;
        PointF c=new PointF(carryIcon.Center.X,carryIcon.Center.Y);
        TargetHeading=Math.Atan2(c.Y-Position.Y,c.X-Position.X)*180/Math.PI;
        Turn(dt);
        double d=Dist(Position,c);
        if(d<reach+6){Grab();return;}
        if(carryElapsed>24||StateLeft<=0){AbortCarry("没走到东西旁边");return;}
        // purposeful scurry: twice the ordinary stroll, or the trip reads as "nothing happened"
        AdvancePosition(dt,World.Config.Speed*2.2);
        Clamp();
        Phase+=dt*13;
    }
    private void Grab(){
        DeskIcon fresh=World.GrabIcon(carryIcon);
        if(fresh==null){AbortCarry(World.IconError);return;}
        carryIcon=fresh;
        choreTarget=World.CarryDestination(this,fresh);
        carryElapsed=0;carryPush=0;carryMiss=0;
        SetState("搬运",18);
        Chores++;
        World.Log(Profile.Name+" 举起「"+carryIcon.Name+"」");
    }
    private void TickCarry(double dt){
        if(carryIcon==null){SetState("探索",2);return;}
        carryElapsed+=dt;
        double d=Dist(Position,choreTarget);
        if(d<Math.Max(16,World.Config.Speed*dt*3)||carryElapsed>22){FinishCarry();return;}
        TargetHeading=Math.Atan2(choreTarget.Y-Position.Y,choreTarget.X-Position.X)*180/Math.PI;
        Turn(dt);
        AdvancePosition(dt,World.Config.Speed*.72);
        Clamp();
        Phase+=dt*13;
        leanForward=true;
        carryPush-=dt;
        if(carryPush<=0){
            carryPush=.075;
            // The icon is held in front of the mandibles, never under the body, so
            // the player can actually see it slide across the desktop.
            float gap=carryIcon.Glyph*.55f+14*ScaleFactor;
            double a=Heading*Math.PI/180;
            PointF anchor=new PointF(Position.X+(float)(Math.Cos(a)*gap),Position.Y+(float)(Math.Sin(a)*gap));
            if(!World.PushIcon(carryIcon,anchor)){AbortCarry(World.IconError);return;}
            CarrySteps++;
        }
        carryVerify-=dt;
        if(carryVerify<=0){
            carryVerify=.7;
            Point actual;
            if(DesktopIcons.ReadBack(carryIcon,out actual)&&Dist(new PointF(actual.X,actual.Y),new PointF(carryIcon.Center.X,carryIcon.Center.Y))>78){
                carryMiss++;
                if(carryMiss>=2){AbortCarry("桌面没有接受这个位置（可能开着「自动排列图标」）");return;}
            }else carryMiss=0;
        }
    }
    private void FinishCarry(){
        string name=carryIcon==null?"东西":carryIcon.Name;
        World.CompleteCarry(carryIcon);
        World.DropIcon(carryIcon,Profile.Name);
        carryIcon=null;leanForward=false;
        World.Log(Profile.Name+" 把「"+name+"」搬到了新位置");
        SetState("放下",1.5);carryPause=1.5;
    }
    private void AbortCarry(string reason){
        if(carryIcon!=null){World.DropIcon(carryIcon,Profile.Name);carryIcon=null;}
        leanForward=false;lift=0;
        if(!String.IsNullOrEmpty(reason))World.Log(Profile.Name+" 放下东西："+reason);
        SetState("受惊",1.1);
        carryMiss=0;
    }

    // ---------- chore: ladybug flight ----------
    internal bool TryFlight(){
        if(!World.Config.Flight||!CreatureCatalog.CanFly(Species)||IsTravel||Surface!=null||dragging)return false;
        if(!World.FlightsFree(this))return false;
        for(int i=0;i<14;i++){
            double a=Rng.NextDouble()*Math.PI*2,d=190+Rng.NextDouble()*230;
            PointF q=new PointF(Position.X+(float)(Math.Cos(a)*d),Position.Y+(float)(Math.Sin(a)*d));
            if(!World.PointAllowed(q))continue;
            Rectangle b=World.NearestScreen(q).WorkingArea;
            if(q.X<b.Left+50||q.X>b.Right-50||q.Y<b.Top+50||q.Y>b.Bottom-50)continue;
            choreTarget=q;FlightReady();
            World.FlightCount++;
            World.Log(Profile.Name+" "+FlightFlavor());
            return true;
        }
        return false;
    }
    // Winged insects take to the air; the rest leap, balloon or bounce their
    // way there — every species gets a line that fits its body plan.
    private string FlightFlavor(){
        if(Species=="Bee")return "振翅悬停，飞一小段";
        if(Species=="Butterfly")return "扇扇鳞翅，翩翩飞一段";
        if(Species=="Ladybug")return "展开鞘翅，飞一小段";
        if(Species=="Beetle")return "张开鞘翅，扑棱飞一段";
        if(Species=="Spider")return "喷出一根丝，乘风飘了一段";
        if(Species=="Mantis")return "后腿一蹬，腾空弹跳一段";
        if(Species=="Caterpillar")return "弓身一弹，蹦向前方";
        if(Species=="Snail")return "深吸一口气，蹦了起来";
        return "高举触角，急匆匆飞跑一段";
    }
    private void FlightReady(){
        ResetRoute();Partner=null;carryElapsed=0;lift=0;airBob=0;
        SetState("起飞",.5);
    }
    private void TickFlight(double dt){
        carryElapsed+=dt;
        if(State=="起飞"){
            lift=(float)Math.Min(1,carryElapsed/.5);
            TargetHeading=Math.Atan2(choreTarget.Y-Position.Y,choreTarget.X-Position.X)*180/Math.PI;
            Turn(dt);
            airBob=(float)(Math.Sin(Phase*2.2)*2.4*ScaleFactor)*lift;
            Phase+=dt*22;
            if(StateLeft<=0)SetState("飞行",3.2);
            return;
        }
        if(State=="飞行"){
            lift=1;
            bool flyer=CreatureCatalog.CanFly(Species);
            double d=Dist(Position,choreTarget);
            TargetHeading=Math.Atan2(choreTarget.Y-Position.Y,choreTarget.X-Position.X)*180/Math.PI;
            Turn(dt);
            AdvancePosition(dt,Math.Max(130,World.Config.Speed*(flyer?2.6:1.9)));
            Phase+=dt*(flyer?26:9);
            FlightSteps++;
            airBob=(float)(Math.Sin(Phase*2.2)*(flyer?2.6f:.9f)*ScaleFactor);
            if(d<Math.Max(14,currentSpeed*dt*3)||StateLeft<=0){Position=choreTarget;currentSpeed=0;carryElapsed=0;SetState("降落",.55);}
            return;
        }
        lift=(float)Math.Max(0,1-carryElapsed/.55f);
        airBob=(float)(Math.Sin(Phase*2.2)*2.6*ScaleFactor)*lift;
        Phase+=dt*12;
        if(StateLeft<=0){lift=0;airBob=0;SetState("梳理",1.6);}
    }

    // ---------- chore: beetle nudges food around ----------
    internal bool TryPushFood(){
        if(!World.Config.CarryIcons||IsTravel)return false;
        FoodSpot f=World.NearestFood(Position);
        if(f==null||Dist(Position,f.Position)>900)return false;
        Rectangle area=World.NearestScreen(Position).WorkingArea;
        for(int i=0;i<10;i++){
            double a=Rng.NextDouble()*Math.PI*2,d=110+Rng.NextDouble()*150;
            PointF q=new PointF(f.Position.X+(float)(Math.Cos(a)*d),f.Position.Y+(float)(Math.Sin(a)*d));
            if(q.X<area.Left+40||q.X>area.Right-40||q.Y<area.Top+40||q.Y>area.Bottom-40)continue;
            pushFood=f;choreTarget=q;carryElapsed=0;
            ResetRoute();Partner=null;
            SetState("推食物",16);
            Chores++;
            World.Log(Profile.Name+" 想把食物推到别处");
            return true;
        }
        return false;
    }
    private void TickPushFood(double dt){
        if(pushFood==null||!World.Foods.Contains(pushFood)||pushFood.Portions<=0){pushFood=null;SetState("探索",2);return;}
        carryElapsed+=dt;
        double ang=Math.Atan2(choreTarget.Y-pushFood.Position.Y,choreTarget.X-pushFood.Position.X);
        double d=Dist(pushFood.Position,choreTarget);
        if(d<10||carryElapsed>14){
            pushFood=null;World.FoodPushCount++;
            World.Log(Profile.Name+" 把食物挪到了新位置");
            SetState("梳理",1.6);
            return;
        }
        World.MoveFood(pushFood,new PointF(pushFood.Position.X+(float)(Math.Cos(ang)*38*dt),pushFood.Position.Y+(float)(Math.Sin(ang)*38*dt)));
        // stand just behind the food and lean on it
        float gap=24*ScaleFactor+8;
        PointF behind=new PointF(pushFood.Position.X-(float)(Math.Cos(ang)*gap),pushFood.Position.Y-(float)(Math.Sin(ang)*gap));
        TargetHeading=ang*180/Math.PI;
        Turn(dt);
        PointF delta=new PointF(behind.X-Position.X,behind.Y-Position.Y);
        double back=Math.Sqrt(delta.X*delta.X+delta.Y*delta.Y);
        if(back>4){
            double a=Math.Atan2(delta.Y,delta.X);
            Position=new PointF(Position.X+(float)(Math.Cos(a)*Math.Min(back,World.Config.Speed*.55*dt*2)),Position.Y+(float)(Math.Sin(a)*Math.Min(back,World.Config.Speed*.55*dt*2)));
        }
        Phase+=dt*12;
        leanForward=true;
    }

    // Releases anything the animal is holding without touching the state machine.
    // Used when the user grabs, scares or recalls it mid-chore.
    internal void CancelChore(){
        if(carryIcon!=null){World.DropIcon(carryIcon,Profile.Name);carryIcon=null;}
        pushFood=null;leanForward=false;lift=0;airBob=0;carryMiss=0;carryElapsed=0;
    }

    internal void TickChore(double dt){        switch(State){
            case "找图标":TickSeek(dt);break;
            case "搬运":TickCarry(dt);break;
            case "放下":carryPause-=dt;if(carryPause<=0)SetState("梳理",1.4);break;
            case "推食物":TickPushFood(dt);break;
            default:TickFlight(dt);break;
        }
        if(State!="搬运"&&State!="推食物")leanForward=false;
    }

    // ---------- rendering: lift, wings, lean ----------
    // The photo-mesh renderer only knows gait phases, so flight and carrying are
    // composed on top of its output: a ground shadow that shrinks with altitude,
    // hind wings that sweep with the body heading and visibly flap, and a small
    // body pitch while carrying. Returns source untouched for plain poses.
    internal static Bitmap ComposeFrame(Bitmap source,string species,double phase,float heading,float scale,string state,bool lean,float air){
        bool fly=air>0.5f;
        if(!fly&&!lean)return source;
        try{
            var canvas=new Bitmap(source.Width,source.Height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using(var g=Graphics.FromImage(canvas)){
                g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                float cx=canvas.Width/2f,cy=canvas.Height/2f;
                if(fly){
                    float maxAir=16*scale+12,t=Math.Max(0,Math.Min(1,air/Math.Max(1f,maxAir)));
                    // ground shadow: smaller and lighter the higher it flies
                    float w=(96-34*t)*scale,h=(27-9*t)*scale;
                    using(var path=new GraphicsPath()){
                        path.AddEllipse(cx-w/2,cy+h*.62f,w,h);
                        using(var pb=new PathGradientBrush(path)){
                            pb.CenterColor=Color.FromArgb((int)(56*(1-.5*t)),10,16,8);
                            pb.SurroundColors=new Color[]{Color.FromArgb(0,10,16,8)};
                            g.FillPath(pb,path);
                        }
                    }
                    if(CreatureCatalog.CanFly(species))Wings(g,cx,cy,air,phase,heading,scale,state,species,t);
                    g.TranslateTransform(cx,cy-air);g.RotateTransform(-7f);g.TranslateTransform(-cx,-(cy-air));
                    g.DrawImage(source,0,-air,source.Width,source.Height);
                }else{
                    g.TranslateTransform(cx,cy);g.RotateTransform(8f);g.TranslateTransform(-cx,-cy);
                    g.DrawImage(source,0,0);
                }
            }
            return canvas;
        }catch{return source;}
    }
    private Bitmap Compose(float air){
        Bitmap source=CreatureRenderer.Render(Species,Phase,(float)Heading,ScaleFactor,State,0,false);
        return ComposeFrame(source,Species,Phase,(float)Heading,ScaleFactor,State,leanForward,air);
    }
    // Two hind wings rooted near the wing base, swept sideways-back and flapping
    // around the body heading; long enough to clear the elytra silhouette.
    private static void Wings(Graphics g,float cx,float cy,float air,double phase,float heading,float scale,string state,string species,float t){
        double stroke=state=="降落"?(Math.Sin(phase*2.2)*.5+.5):(state=="起飞"?Math.Sin(phase*1.6):Math.Sin(phase*2.2));
        float amp=(16f+36f*t)*(state=="降落"?.5f:1f);          // wingbeat grows with altitude
        // Butterfly keeps its own patterned wings, so the composited pair stays
        // short and warm like a motion blur; ladybug/bee get the pale airfoils.
        bool moth=species=="Butterfly";
        float len=(moth?36f:52f+22f*t)*scale,wd=(moth?10f:7f+9f*t)*scale;
        Color far=moth?Color.FromArgb(150,238,216,182):Color.FromArgb(200,252,253,246);
        Color near=moth?Color.FromArgb(36,214,188,150):Color.FromArgb(40,203,220,198);
        for(int s=-1;s<=1;s+=2){
            g.ResetTransform();
            g.TranslateTransform(cx,cy-air);
            g.RotateTransform(heading);
            g.TranslateTransform(6*scale,0);
            g.RotateTransform(106f*s+(float)stroke*amp*s);
            var rect=new RectangleF(0,-wd/2f,len,wd);
            using(var brush=new LinearGradientBrush(rect,far,near,0f))
                g.FillEllipse(brush,rect);
        }
        g.ResetTransform();
    }
}
}

