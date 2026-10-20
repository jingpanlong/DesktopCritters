using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace DesktopCritters {
internal sealed class HotkeyWindow:Form {
    private Habitat world;internal readonly List<string> Failed=new List<string>();
    internal HotkeyWindow(Habitat w){world=w;ShowInTaskbar=false;Register(1,Keys.P,"Ctrl+Alt+P");Register(2,Keys.O,"Ctrl+Alt+O");Register(3,Keys.F,"Ctrl+Alt+F");Register(4,Keys.H,"Ctrl+Alt+H");Register(5,Keys.M,"Ctrl+Alt+M");}
    private void Register(int id,Keys key,string name){if(!Native.RegisterHotKey(Handle,id,0x4003,(uint)key))Failed.Add(name);}
    protected override void WndProc(ref Message m){if(m.Msg==0x312){switch(m.WParam.ToInt32()){case 1:world.ToggleHidden();break;case 2:world.OpenPanel();break;case 3:world.Feed(Cursor.Position);break;case 4:world.Recall();break;case 5:world.SetPeaceful(!world.Config.Peaceful);break;}return;}base.WndProc(ref m);}
    protected override void Dispose(bool disposing){if(IsHandleCreated)for(int i=1;i<=5;i++)Native.UnregisterHotKey(Handle,i);base.Dispose(disposing);}
}
internal sealed class FoodMarker:Form {
    internal int Portions=6;internal IntPtr Host;
    private static readonly Point[] Crumbs={new Point(7,26),new Point(15,29),new Point(26,27),new Point(11,21),new Point(22,22),new Point(18,15)};
    internal FoodMarker(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=false;Size=new Size(40,40);BackColor=Color.Magenta;TransparencyKey=Color.Magenta;DoubleBuffered=true;}
    protected override bool ShowWithoutActivation{get{return true;}}
    protected override CreateParams CreateParams{get{var c=base.CreateParams;c.ExStyle|=0x80|0x20|0x08000000;return c;}}
    protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}base.WndProc(ref m);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using(var shadow=new SolidBrush(Color.FromArgb(26,0,0,0)))e.Graphics.FillEllipse(shadow,5,25,30,10);
        int n=Math.Max(0,Math.Min(Crumbs.Length,Portions));
        for(int i=0;i<n;i++){var c=Crumbs[i];using(var b=new SolidBrush(Color.FromArgb(220,176,74)))e.Graphics.FillEllipse(b,c.X,c.Y,9,6);
            using(var h=new SolidBrush(Color.FromArgb(245,226,168)))e.Graphics.FillEllipse(h,c.X+2,c.Y+1,3,2);}
        if(n==0)using(var b=new SolidBrush(Color.FromArgb(60,120,96)))e.Graphics.FillEllipse(b,14,28,12,4);}
}
internal sealed class FoodSpot:IDisposable {
    internal PointF Position;internal double Life=60;internal int Portions=6;internal FoodMarker Marker=new FoodMarker();internal bool Fresh{get{return Life>48;}}
    internal void OnPortionsChanged(){Marker.Portions=Portions;if(!Marker.IsDisposed)Marker.Invalidate();}
    public void Dispose(){Marker.Close();Marker.Dispose();}
}
internal sealed class Habitat:ApplicationContext {
    internal Settings Config;internal SurfaceMap Map=new SurfaceMap();internal readonly List<PetForm> Pets=new List<PetForm>();internal readonly List<FoodSpot> Foods=new List<FoodSpot>();
    internal readonly List<string> Events=new List<string>();internal int FeedCount,FightCount,ChaseCount,CompanionCount,CarryCount,FlightCount,FoodPushCount;
    internal string IconError="";
    private readonly Dictionary<string,double> iconCooldown=new Dictionary<string,double>();private bool hintShown;internal bool Paused,Hidden,FullScreenHidden,SessionLocked;
    internal readonly DateTime Started=DateTime.Now;internal HotkeyWindow Keys;internal readonly Random Rng=new Random(7731);
    private NotifyIcon tray;private Timer timer;private Stopwatch clock=new Stopwatch();private double previous,refresh,socialClock,lastSave;private ControlPanel panel;private Icon icon;private bool quitting,testing,savePending;
    private IntPtr mouseHook;private Native.HookProc hookRef;
    internal Habitat(bool trayOnly,bool test){testing=test;DesktopIcons.Virtual=test;Config=test?Settings.Defaults():Settings.Load();Config.Validate();Ui.Apply(Config.LightTheme);Keys=new HotkeyWindow(this);
        icon=AppIcon.Load(SystemInformation.SmallIconSize.Width);
        tray=new NotifyIcon{Icon=icon,Text="微野 · 多物种生态版",Visible=true};tray.DoubleClick+=delegate{OpenPanel();};var menu=new ContextMenuStrip();Ui.StyleMenu(menu);
        menu.Items.Add("打开动物管理器",null,delegate{OpenPanel();});menu.Items.Add("暂停 / 继续",null,delegate{Paused=!Paused;});menu.Items.Add("隐藏 / 显示   Ctrl+Alt+P",null,delegate{ToggleHidden();});menu.Items.Add("全部回家   Ctrl+Alt+H",null,delegate{Recall();});menu.Items.Add("和平模式开关   Ctrl+Alt+M",null,delegate{SetPeaceful(!Config.Peaceful);});menu.Items.Add("在鼠标处投喂",null,delegate{Feed(Cursor.Position);});menu.Items.Add("清理所有食物",null,delegate{ClearFood();});menu.Items.Add("还原被搬动的桌面图标",null,delegate{RestoreIcons();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("关于微野",null,delegate{new AboutWindow().Show();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("退出微野",null,delegate{ExitThread();});tray.ContextMenuStrip=menu;
        Map.Refresh(Config);Rebuild();clock.Start();Native.TimeBeginPeriod(1);timer=new Timer{Interval=1000/Config.Fps};timer.Tick+=Tick;timer.Start();
        // Pet windows never take focus, so a plain WM_MOUSEWHEEL over a pet is
        // delivered to whatever app sits in front. A low-level hook routes the
        // wheel to the pet under the cursor instead: zoom works everywhere.
        try{hookRef=MouseHook;mouseHook=Native.SetWindowsHookEx(14,hookRef,Native.GetModuleHandle(null),0);}catch{}
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;Microsoft.Win32.SystemEvents.SessionSwitch+=SessionChanged;
        Log("生态已启动："+Pets.Count+" 只动物");if(!trayOnly&&!test)OpenPanel();if(Keys.Failed.Count>0&&!test)Hint("快捷键被占用",String.Join("、",Keys.Failed.ToArray())+"，请使用托盘。");
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr h);
    private void DisplayChanged(object sender,EventArgs e){try{if(!Keys.IsDisposed&&Keys.IsHandleCreated)Keys.BeginInvoke((Action)delegate{Map.Refresh(Config);Recall();});}catch{}}
    private void SessionChanged(object sender,Microsoft.Win32.SessionSwitchEventArgs e){if(e.Reason==Microsoft.Win32.SessionSwitchReason.SessionLock)SessionLocked=true;else if(e.Reason==Microsoft.Win32.SessionSwitchReason.SessionUnlock)SessionLocked=false;}
    internal void Hint(string title,string text){if(testing)return;try{tray.ShowBalloonTip(4000,title,text,ToolTipIcon.None);}catch{}}
    internal void Log(string text){Events.Add(DateTime.Now.ToString("HH:mm:ss")+"  "+text);if(Events.Count>120)Events.RemoveAt(0);}
    internal Screen[] AllowedScreens(){return Config.AllScreens?Screen.AllScreens:new Screen[]{Screen.PrimaryScreen};}
    internal bool PointAllowed(PointF p){foreach(var s in AllowedScreens()){var b=s.Bounds;if(p.X>=b.Left&&p.X<=b.Right&&p.Y>=b.Top&&p.Y<=b.Bottom)return true;}return false;}
    internal Screen NearestScreen(PointF p){Screen best=AllowedScreens()[0];double score=Double.MaxValue;foreach(var s in AllowedScreens()){var b=s.Bounds;float x=Math.Max(b.Left,Math.Min(b.Right,p.X)),y=Math.Max(b.Top,Math.Min(b.Bottom,p.Y));double d=PetForm.Dist(p,new PointF(x,y));if(d<score){best=s;score=d;}}return best;}
    internal void Rebuild(){var old=Pets.ToArray();Pets.Clear();foreach(var p in old)if(!p.IsDisposed)p.Close();for(int i=0;i<Config.Animals.Count;i++){var p=new PetForm(this,Environment.TickCount+i*7919,Config.Animals[i]);Pets.Add(p);p.Show();p.DrawFrame();}UpdateVisibility();}
    internal void Save(){if(testing)return;try{Config.Save();Config.SetStartup(Application.ExecutablePath);lastSave=clock.Elapsed.TotalSeconds;}catch(Exception e){Log("设置保存失败："+e.Message);}}
    internal void SaveSoon(){savePending=true;}
    internal void Apply(bool rebuild){Config.Validate();Map.Refresh(Config);if(rebuild)Rebuild();foreach(var p in Pets){if(p.IsDisposed)continue;p.ApplyInputMode();if(!Config.AllowOffscreen)p.ReturnNow();p.Clamp();}if(Config.Peaceful||!Config.Social)EndConflicts();Save();}
    internal void AddAnimal(string species){if(Config.Animals.Count>=24)return;Config.Animals.Add(new AnimalProfile(species,CreatureCatalog.Find(species).Name+" "+(Config.Animals.Count+1)));Apply(true);Log("加入了新动物");}
    internal void RemoveAnimal(PetForm pet){Config.Animals.Remove(pet.Profile);Apply(true);Log("已移除一只动物");}
    internal void SetSpeciesCount(string species,int count){count=Settings.Limit(count,0,24);int existing=Config.Animals.Count(p=>p.Species==species);while(existing>count){var p=Config.Animals.Last(x=>x.Species==species);Config.Animals.Remove(p);existing--;}while(existing<count&&Config.Animals.Count<24){Config.Animals.Add(new AnimalProfile(species,CreatureCatalog.Find(species).Name+" "+(existing+1)));existing++;}Apply(true);}
    internal void SetPeaceful(bool value){Config.Peaceful=value;if(value)EndConflicts();Save();Log(value?"已开启和平模式":"已允许跨物种互动争斗");}
    internal void SetTheme(bool light){
        Config.LightTheme=light;Ui.Apply(light);Save();
        // Rebuilding the panel is the clean way to re-skin every cached control colour.
        Action rebuild=delegate{if(panel!=null&&!panel.IsDisposed)panel.Dispose();panel=null;OpenPanel();};
        var ctx=System.Threading.SynchronizationContext.Current;
        if(ctx!=null)ctx.Post(delegate{rebuild();},null);else rebuild();
    }
    private void EndConflicts(){foreach(var p in Pets)if(p.State=="争斗"||p.State=="追逐"||p.State=="躲避"||(!Config.Social&&p.State=="结伴")){p.Partner=null;p.SetState("休息",2);p.SocialCooldown=5;}}
    internal void Recall(){Rectangle b=NearestScreen(new PointF(Cursor.Position.X,Cursor.Position.Y)).WorkingArea;for(int i=0;i<Pets.Count;i++){float x=b.Left+b.Width/2+((i%5)-2)*Math.Min(90,b.Width/8),y=b.Top+b.Height/2+(i/5-1)*60;Pets[i].Recall(new PointF(x,y));}Hidden=false;Paused=false;UpdateVisibility();Log("全部动物已回家");}
    internal void ToggleHidden(){Hidden=!Hidden;UpdateVisibility();}
    internal void Feed(Point point){Rectangle b=NearestScreen(new PointF(point.X,point.Y)).WorkingArea;if(Foods.Count>=8){Foods[0].Dispose();Foods.RemoveAt(0);}var f=new FoodSpot{Position=new PointF(Math.Max(b.Left+20,Math.Min(b.Right-20,point.X)),Math.Max(b.Top+20,Math.Min(b.Bottom-20,point.Y)))};Foods.Add(f);Log("放下一份食物");UpdateVisibility();}
    internal FoodSpot NearestFood(PointF p){FoodSpot best=null;double distance=Double.MaxValue;foreach(var f in Foods)if(f.Portions>0){double d=PetForm.Dist(p,f.Position);if(d<distance){distance=d;best=f;}}return best;}
    internal bool Consume(PetForm p,FoodSpot food){if(!Foods.Contains(food)||food.Portions<=0)return false;food.Portions--;food.OnPortionsChanged();p.Hunger=Math.Max(0,p.Hunger-16);p.Energy=Math.Min(100,p.Energy+4);p.Trust=Math.Min(100,p.Trust+1);FeedCount++;Log(p.Profile.Name+" 吃掉一份，剩余 "+food.Portions+" 份");return true;}
    internal void ClearFood(){foreach(var f in Foods)f.Dispose();Foods.Clear();}
    internal void MoveFood(FoodSpot f,PointF p){if(f==null)return;Rectangle b=NearestScreen(p).WorkingArea;f.Position=new PointF(Math.Max(b.Left+20,Math.Min(b.Right-20,p.X)),Math.Max(b.Top+20,Math.Min(b.Bottom-20,p.Y)));if(!f.Marker.IsDisposed)f.Marker.Location=new Point((int)f.Position.X-20,(int)f.Position.Y-20);}

    // ---------- desktop icon chores ----------
    // Only one animal may be carrying at a time, otherwise two ants fight over one
    // icon and the shell sees contradictory positions.
    internal bool IconCarryFree(PetForm pet){foreach(var q in Pets){if(q.IsDisposed||q==pet)continue;if(q.State=="找图标"||q.State=="搬运"||q.State=="放下")return false;}return true;}
    internal bool FlightsFree(PetForm pet){int n=0;foreach(var q in Pets){if(q.IsDisposed||q==pet)continue;if(q.State=="起飞"||q.State=="飞行"||q.State=="降落")n++;}return n<2;}
    internal DeskIcon PickIconNear(PointF p,double max){
        if(!Config.CarryIcons||!DesktopIcons.Available)return null;
        if(CarryCount>=40){IconError="这次运行已经搬了 40 次，先歇一会儿";return null;}
        if(!DesktopIcons.Refresh(false)){IconError=DesktopIcons.LastError;return null;}
        double now=clock.Elapsed.TotalSeconds;
        var ranked=new List<KeyValuePair<double,DeskIcon>>();
        foreach(var i in DesktopIcons.Snapshot()){
            if(String.IsNullOrEmpty(i.Name))continue;
            double last;if(iconCooldown.TryGetValue(i.Name,out last)&&now-last<150)continue;
            double d=PetForm.Dist(p,new PointF(i.Center.X,i.Center.Y));
            if(d>max)continue;
            ranked.Add(new KeyValuePair<double,DeskIcon>(d+Rng.NextDouble()*150,i));
        }
        ranked.Sort(delegate(KeyValuePair<double,DeskIcon> a,KeyValuePair<double,DeskIcon> b){return a.Key.CompareTo(b.Key);});
        DeskIcon fallback=ranked.Count>0?ranked[0].Value:null;
        foreach(var kv in ranked){
            DeskIcon i=kv.Value;
            // prefer icons in the open: an ant shuffling icons behind a window
            // looks like "nothing happened"
            IntPtr cover=Native.WindowAt(new Point(i.Center.X,i.Center.Y));
            if(cover!=IntPtr.Zero){string cls=Native.ClassName(cover);if(cls!="Progman"&&cls!="WorkerW")continue;}
            return i;
        }
        IconError="想搬的图标都在窗口下面，先挑了个最近的";
        return fallback;
    }
    // Re-reads the shell so the returned object carries the current index/position
    // (the user may have rearranged the desktop a second ago).
    internal DeskIcon GrabIcon(DeskIcon icon){
        if(icon==null)return null;
        if(!DesktopIcons.Refresh(true)){IconError=DesktopIcons.LastError;return null;}
        DeskIcon fresh=null;
        foreach(var i in DesktopIcons.Snapshot())if(i.Name==icon.Name){fresh=i;break;}
        if(fresh==null){IconError="图标已经从桌面消失了";return null;}
        if(Config.IconMoves.Count>=60)Config.IconMoves.RemoveRange(0,Config.IconMoves.Count-60);
        Config.IconMoves.Add(new IconMove(fresh.Name,fresh.Center.X,fresh.Center.Y,DateTime.Now.ToString("MM-dd HH:mm")));
        iconCooldown[fresh.Name]=clock.Elapsed.TotalSeconds;
        SaveSoon();
        if(!hintShown&&!testing){hintShown=true;Hint("蚂蚁开始整理桌面","动物会真的搬动桌面图标。想恢复原样：托盘右键 → 还原被搬动的桌面图标。");}
        return fresh;
    }
    internal bool PushIcon(DeskIcon icon,PointF anchor){
        if(icon==null)return false;
        Rectangle area=NearestScreen(anchor).WorkingArea;
        int half=Math.Max(18,icon.Glyph/2);
        int x=(int)Math.Max(area.Left+half+4,Math.Min(area.Right-half-4,anchor.X));
        int y=(int)Math.Max(area.Top+half+4,Math.Min(area.Bottom-half-4,anchor.Y));
        if(!DesktopIcons.Move(icon,new Point(x,y))){IconError=DesktopIcons.LastError;return false;}
        return true;
    }
    internal void CompleteCarry(DeskIcon icon){if(icon==null)return;CarryCount++;SaveSoon();}
    internal void DropIcon(DeskIcon icon,string by){if(icon==null)return;iconCooldown[icon.Name]=clock.Elapsed.TotalSeconds;}
    internal PointF CarryDestination(PetForm pet,DeskIcon icon){
        Rectangle area=NearestScreen(pet.Position).WorkingArea;
        float mx=icon.Glyph*.5f+10,my=icon.Glyph*.5f+10;
        for(int i=0;i<18;i++){
            double a=Rng.NextDouble()*Math.PI*2,d=150+Rng.NextDouble()*190;
            PointF q=new PointF(icon.Center.X+(float)(Math.Cos(a)*d),icon.Center.Y+(float)(Math.Sin(a)*d));
            if(q.X<area.Left+mx||q.X>area.Right-mx||q.Y<area.Top+my||q.Y>area.Bottom-my)continue;
            bool clash=false;
            foreach(var other in DesktopIcons.Snapshot()){
                if(other==icon)continue;
                if(PetForm.Dist(q,new PointF(other.Center.X,other.Center.Y))<icon.Glyph+26){clash=true;break;}
            }
            if(clash)continue;
            return q;
        }
        // Fall back to pushing away from the closest screen edge instead of a
        // fixed direction, so icons parked in a corner still get somewhere to go.
        float dx=icon.Center.X-area.Left, dy=icon.Center.Y-area.Top;
        float rx=area.Right-icon.Center.X, ry=area.Bottom-icon.Center.Y;
        float min=Math.Min(Math.Min(dx,dy),Math.Min(rx,ry));
        PointF best=new PointF(icon.Center.X,icon.Center.Y+120);
        if(min==dy)best=new PointF(icon.Center.X,icon.Center.Y+150);
        else if(min==ry)best=new PointF(icon.Center.X,icon.Center.Y-150);
        else if(min==dx)best=new PointF(icon.Center.X+150,icon.Center.Y);
        else best=new PointF(icon.Center.X-150,icon.Center.Y);
        return new PointF(Math.Max(area.Left+mx,Math.Min(area.Right-mx,best.X)),Math.Max(area.Top+my,Math.Min(area.Bottom-my,best.Y)));
    }
    // Undo everything the animals moved, in one click.
    internal int RestoreIcons(){
        if(!DesktopIcons.Refresh(true)){Log("无法读取桌面图标："+DesktopIcons.LastError);return 0;}
        var left=new List<IconMove>();int done=0;
        foreach(var m in Config.IconMoves){
            DeskIcon hit=null;
            foreach(var i in DesktopIcons.Snapshot())if(i.Name==m.Name){hit=i;break;}
            if(hit==null)continue;
            if(DesktopIcons.Move(hit,new Point(m.X,m.Y)))done++;else left.Add(m);
        }
        Config.IconMoves.Clear();Config.IconMoves.AddRange(left);Save();
        Log(done>0?"已还原 "+done+" 个桌面图标":"没有可还原的图标（可能是自动排列模式）");
        return done;
    }
    internal bool BeginFight(PetForm a,PetForm b,bool forced){if(a==null||b==null||a==b||Config.Peaceful||!Config.Social||a.IsTravel||b.IsTravel||a.Species==b.Species)return false;if(!forced&&(a.State=="争斗"||b.State=="争斗"))return false;
        a.ResetRoute();b.ResetRoute();a.Partner=b;b.Partner=a;a.SetState("争斗",2.4);b.SetState("争斗",2.4);a.SocialCooldown=b.SocialCooldown=8;FightCount++;PetForm winner=a.Energy+Rng.Next(30)>b.Energy+Rng.Next(30)?a:b;winner.Wins++;Log(a.Profile.Name+" 与 "+b.Profile.Name+" 短暂争斗（无伤害）");return true;}
    private void SocialTick(){if(!Config.Social)return;for(int i=0;i<Pets.Count;i++)for(int j=i+1;j<Pets.Count;j++){var a=Pets[i];var b=Pets[j];if(a.IsDisposed||b.IsDisposed)continue;if(!a.CanSocial||!b.CanSocial||PetForm.Dist(a.Position,b.Position)>105*Math.Min(a.ScaleFactor,b.ScaleFactor))continue;a.SocialCooldown=b.SocialCooldown=7;
        if(a.Species==b.Species){b.Partner=a;b.ResetRoute();b.SetState("结伴",4);CompanionCount++;Log(b.Profile.Name+" 跟随同伴");continue;}
        double aggression=Config.Aggression/100.0;if(a.Profile.Temperament=="好斗"||b.Profile.Temperament=="好斗")aggression=Math.Min(1,aggression+.25);if(a.Profile.Temperament=="胆小"||b.Profile.Temperament=="胆小")aggression*=.5;
        double roll=Rng.NextDouble();if(!Config.Peaceful&&roll<aggression*.55){BeginFight(a,b,true);}else if(!Config.Peaceful&&roll<aggression){a.ResetRoute();b.ResetRoute();a.Partner=b;b.Partner=a;a.SetState("追逐",3);b.SetState("躲避",3);ChaseCount++;Log(a.Profile.Name+" 正在追逐 "+b.Profile.Name);}else{a.TargetHeading=Math.Atan2(a.Position.Y-b.Position.Y,a.Position.X-b.Position.X)*180/Math.PI;b.TargetHeading=a.TargetHeading+180;a.ResetRoute();b.ResetRoute();a.SetState("探索",2);b.SetState("探索",2);}}
    }
    internal void OpenPanel(){if(panel==null||panel.IsDisposed)panel=new ControlPanel(this);panel.Show();panel.WindowState=FormWindowState.Normal;panel.Activate();}
    internal ContextMenuStrip PetMenu(PetForm p){
        var m=new ContextMenuStrip();Ui.StyleMenu(m);
        // Menu item handlers run while the strip is still closing; WinForms'
        // ModalMenuFilter touches the strip (and the form that opened it)
        // after the click returns. Dispose-while-open or rebuild-while-open
        // raises ObjectDisposedException, so defer every handler and the
        // strip disposal to the next message-loop iteration.
        Action<Action> defer=a=>{var run=(Action)delegate{try{a();}catch(Exception ex){Log("菜单操作失败："+ex.Message);}};var ctx=System.Threading.SynchronizationContext.Current;if(ctx!=null)ctx.Post(delegate{run();},null);else run();};
        m.Items.Add(p.Profile.Name+" · "+p.State).Enabled=false;
        m.Items.Add("喂它",null,delegate{defer(delegate{Feed(Point.Round(p.Position));});});
        m.Items.Add("搬个桌面图标",null,delegate{defer(delegate{if(!p.TryStartCarry()){Log("现在没有可以搬的东西："+IconError);Hint("现在搬不了",IconError);}});});
        if(CreatureCatalog.CanFly(p.Species))m.Items.Add("飞一小段",null,delegate{defer(delegate{if(!p.TryFlight()){string why="这里太贴边，或已有同伴在飞";Log("它现在不想飞："+why);Hint("它现在不想飞",why);}});});
        m.Items.Add("推一推食物",null,delegate{defer(delegate{if(!p.TryPushFood()){Log("附近没有可推的食物");Hint("先投喂一点食物","右键桌面空白处 → 在鼠标处投喂，它就会去推。");}});});
        m.Items.Add("沿任务栏爬行",null,delegate{defer(delegate{p.PickRoute("任务栏");});});
        m.Items.Add("沿菜单栏爬行",null,delegate{defer(delegate{p.PickRoute("菜单栏");});});
        m.Items.Add("出去逛逛再回来",null,delegate{defer(delegate{p.BeginExcursion(0);});});
        m.Items.Add("休息",null,delegate{defer(delegate{p.ResetRoute();p.SetState("睡觉",20);});});
        m.Items.Add("移除这只动物",null,delegate{defer(delegate{RemoveAnimal(p);});});
        m.Items.Add("动物管理器",null,delegate{defer(OpenPanel);});
        m.Closed+=delegate{var ctx=System.Threading.SynchronizationContext.Current;if(ctx!=null)ctx.Post(delegate{if(!m.IsDisposed&&!m.Disposing)m.Dispose();},null);};
        return m;}
    internal void UpdateVisibility(){bool show=!Hidden&&!FullScreenHidden&&!SessionLocked;Pets.RemoveAll(p=>p.IsDisposed);foreach(var p in Pets){bool v=show&&!p.Offscreen;if(v&&!p.Visible){p.Show();p.DrawFrame();p.RefreshHost(false);}else if(!v&&p.Visible)p.Hide();}foreach(var f in Foods){f.Marker.Location=new Point((int)f.Position.X-20,(int)f.Position.Y-20);if(show&&!f.Marker.Visible){f.Marker.Host=Native.WindowAt(Point.Round(f.Position));f.Marker.Show();Native.PlaceAbove(f.Marker.Handle,f.Marker.Host);}else if(!show&&f.Marker.Visible)f.Marker.Hide();}}
    private void Tick(object sender,EventArgs e){double now=clock.Elapsed.TotalSeconds,dt=Math.Min(.08,now-previous);previous=now;refresh-=dt;
        if(savePending&&now-lastSave>2){savePending=false;Save();}
        if(refresh<=0){FullScreenHidden=Config.HideFullScreen&&Native.IsFullScreen();Map.Refresh(Config);bool battery=Config.BatterySaver&&SystemInformation.PowerStatus.PowerLineStatus==PowerLineStatus.Offline;timer.Interval=1000/(battery?24:Config.Fps);refresh=2.5;}
        UpdateVisibility();if(Paused||Hidden||FullScreenHidden||SessionLocked)return;
        for(int i=Foods.Count-1;i>=0;i--){Foods[i].Life-=dt;if(Foods[i].Life<=0||Foods[i].Portions<=0){Foods[i].Dispose();Foods.RemoveAt(i);}}
        foreach(var p in Pets){if(p.IsDisposed)continue;p.Tick(dt);}socialClock-=dt;if(socialClock<=0){SocialTick();socialClock=.75;}UpdateVisibility();
    }
    internal string StatusText(){return (Hidden?"已隐藏":SessionLocked?"锁屏暂停":FullScreenHidden?"全屏避让":Paused?"已暂停":Config.Peaceful?"和平生态":"生态活动中")+" · "+Pets.Count+" 只 / "+Pets.Select(p=>p.Species).Distinct().Count()+" 种\n屏外 "+Pets.Count(p=>p.Offscreen)+" · 已返回 "+Pets.Sum(p=>p.Returns)+"\n进食 "+FeedCount+" · 争斗 "+FightCount+"\n路线 "+Map.Items.Count+" · 纹理 "+(PhotoSpriteRenderer.CacheBytes/1048576)+" MB";}
    private IntPtr MouseHook(int code,IntPtr w,IntPtr l){
        try{
            if(code==0&&w==(IntPtr)0x020A){ // WM_MOUSEWHEEL
                var info=(Native.MSLLHOOKSTRUCT)System.Runtime.InteropServices.Marshal.PtrToStructure(l,typeof(Native.MSLLHOOKSTRUCT));
                int delta=(int)(info.mouseData>>16);
                Point pt=new Point(info.pt.X,info.pt.Y);
                foreach(var p in Pets){if(p.IsDisposed||p.Offscreen||!p.Visible)continue;
                    if(p.Contains(pt)){p.NudgeSize(delta);return (IntPtr)1;}}
            }
        }catch{}
        return Native.CallNextHookEx(IntPtr.Zero,code,w,l);
    }
    protected override void ExitThreadCore(){
        if(quitting)return;quitting=true;
        // The exit command runs while the tray/pet ContextMenuStrip is still
        // closing; tearing down synchronously raises ObjectDisposedException.
        // Defer the teardown to the next message-loop iteration.
        try{if(Keys!=null&&!Keys.IsDisposed&&Keys.IsHandleCreated){Keys.BeginInvoke((Action)Teardown);return;}}catch{}
        Teardown();}
    private void Teardown(){
        timer.Stop();timer.Dispose();
        if(mouseHook!=IntPtr.Zero){try{Native.UnhookWindowsHookEx(mouseHook);}catch{}mouseHook=IntPtr.Zero;hookRef=null;}
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;Microsoft.Win32.SystemEvents.SessionSwitch-=SessionChanged;
        foreach(var p in Pets){if(!p.IsDisposed)try{p.CancelChore();}catch{}}foreach(var p in Pets)p.Close();Pets.Clear();ClearFood();        if(panel!=null&&!panel.IsDisposed)panel.Dispose();Keys.Dispose();
        if(tray!=null){ContextMenuStrip strip=tray.ContextMenuStrip;if(strip!=null)tray.ContextMenuStrip=null;tray.Visible=false;tray.Dispose();if(strip!=null&&!strip.IsDisposed)strip.Dispose();}
        if(icon!=null)icon.Dispose();PhotoSpriteRenderer.ClearCache();Native.TimeEndPeriod(1);base.ExitThreadCore();}
}
}

