using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
namespace DesktopCritters {
internal sealed class ControlPanel:Form {
    private const int ContentX=276,ContentW=816,PageW=790;
    private readonly Habitat world;
    private Panel host;private Panel[] pages;private NavButton[] navs;private int activePage;
    private AnimalList list;private TextBox nameBox;private Slider sizeSlider;
    private readonly Dictionary<string,PillButton> speciesBtns=new Dictionary<string,PillButton>();
    private readonly Dictionary<string,PillButton> temperBtns=new Dictionary<string,PillButton>();
    private readonly Dictionary<string,PillButton> routeBtns=new Dictionary<string,PillButton>();
    private string routeSel="自由";
    private readonly Dictionary<string,Label> counters=new Dictionary<string,Label>();
    private PictureBox picture,logoBox;private Label previewName,previewMeta,statusLine,counts;
    private PillButton pauseBtn,hideBtn,peaceBtn;
    private TextBox events;private Label routeInfo;
    private Slider scaleSlider,speedSlider,aggrSlider,tripSlider,minSlider,maxSlider;
    private ToggleRow fps60,battery,names,social,peaceful,fear,follow,night,themeToggle,carryIcons,flight;
    private ToggleRow tb,menus,windows,icons,offscreen,hideFull,clickThrough,autoStart,allScreens;
    private Label iconStat;
    private Timer pulse;private bool loading=true;private string lastEvent="",previewKey="",selId;
    private static readonly string[] Routes={"自由","任务栏","菜单栏","窗口边缘","屏幕边缘","桌面图标"};
    private static readonly string[] Tempers={"均衡","胆小","好斗","好奇"};

    internal ControlPanel(Habitat w){
        world=w;Text="微野 · 桌面生态";ClientSize=new Size(1120,742);Icon=AppIcon.Load(32);
        FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        BackColor=Ui.Shell;ForeColor=Ui.Ink;Font=Ui.F(9.5f,FontStyle.Regular);AutoScaleMode=AutoScaleMode.Dpi;DoubleBuffered=true;KeyPreview=true;
        BuildSidebar();BuildHeader();BuildNav();BuildPages();
        pulse=new Timer{Interval=700};pulse.Tick+=delegate{RefreshLive();};pulse.Start();
        loading=false;ReloadList();SyncFromConfig();RefreshLive();
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
        KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape)Hide();};
        HandleCreated+=delegate{Ui.DarkTitleBar(Handle);};
    }

    // ---------- shell ----------
    private void BuildSidebar(){
        var mark=AppIcon.Mark();
        if(mark==null)mark=AppIcon.Render(46);
        logoBox=new PictureBox{Bounds=new Rectangle(26,14,48,48),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Ui.Shell,Image=mark};
        Controls.Add(logoBox);
        L(this,"微野",19,true,Ui.Ink,new Rectangle(82,20,150,30),Ui.Shell,ContentAlignment.MiddleLeft);
        L(this,"ECOSYSTEM  EDITION",7,false,Ui.Muted,new Rectangle(84,50,160,16),Ui.Shell,ContentAlignment.MiddleLeft);

        var card=new Card{Bounds=new Rectangle(20,96,208,214)};
        picture=new PictureBox{Bounds=new Rectangle(8,8,192,154),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Ui.Card};
        card.Controls.Add(picture);
        previewName=L(card,"",10.5f,true,Ui.Ink,new Rectangle(14,166,180,22),Ui.Card,ContentAlignment.MiddleLeft);
        previewMeta=L(card,"",8.5f,false,Ui.Muted,new Rectangle(14,188,180,18),Ui.Card,ContentAlignment.MiddleLeft);
        Controls.Add(card);

        var st=new Card{Bounds=new Rectangle(20,322,208,156)};
        L(st,"此刻",8.5f,false,Ui.Muted,new Rectangle(14,10,80,18),Ui.Card,ContentAlignment.MiddleLeft);
        statusLine=L(st,"启动中",10,true,Ui.Ink,new Rectangle(14,32,180,22),Ui.Card,ContentAlignment.MiddleLeft);
        counts=L(st,"",8.5f,false,Ui.Muted,new Rectangle(14,58,182,88),Ui.Card,ContentAlignment.TopLeft);
        Controls.Add(st);

        pauseBtn=new PillButton("暂停",PillKind.Ghost,delegate{world.Paused=!world.Paused;RefreshLive();}){Bounds=new Rectangle(20,490,208,38)};
        hideBtn=new PillButton("隐藏动物",PillKind.Ghost,delegate{world.ToggleHidden();RefreshLive();}){Bounds=new Rectangle(20,536,208,38)};
        peaceBtn=new PillButton("和平模式：关",PillKind.Ghost,delegate{world.SetPeaceful(!world.Config.Peaceful);SyncFromConfig();}){Bounds=new Rectangle(20,582,208,38)};
        var home=new PillButton("全部回家",PillKind.Dark,delegate{world.Recall();}){Bounds=new Rectangle(20,628,208,38)};
        Controls.Add(pauseBtn);Controls.Add(hideBtn);Controls.Add(peaceBtn);Controls.Add(home);
        L(this,"Ctrl+Alt+O 打开这里\nEsc 收起面板 · 右键动物有更多操作",8,false,Ui.Muted,new Rectangle(22,688,206,40),Ui.Shell,ContentAlignment.TopLeft);
    }

    private void BuildHeader(){
        L(this,"桌面生态",17,true,Ui.Ink,new Rectangle(ContentX,22,420,32),Ui.Shell,ContentAlignment.MiddleLeft);
        L(this,"混养 · 边缘路线 · 离屏归来 · 高清缩放 · 个体管理",9,false,Ui.Muted,new Rectangle(ContentX+2,56,460,22),Ui.Shell,ContentAlignment.MiddleLeft);
        var feed=new PillButton("在当前屏幕投喂",PillKind.Primary,delegate{var b=Screen.FromControl(this).WorkingArea;world.Feed(new Point(b.Left+b.Width/2,b.Top+b.Height/2));}){Bounds=new Rectangle(1092-132,26,132,36)};
        var tray=new PillButton("收起到托盘",PillKind.Ghost,delegate{Hide();}){Bounds=new Rectangle(1092-132-12-112,26,112,36)};
        var about=new PillButton("关于",PillKind.Ghost,delegate{new AboutWindow().Show(this);}){Bounds=new Rectangle(1092-132-12-112-12-84,26,84,36)};
        Controls.Add(feed);Controls.Add(tray);Controls.Add(about);
    }

    private void BuildNav(){
        navs=new NavButton[6];
        string[] titles={"动物","外观","行为","路线","记录","说明"};
        for(int i=0;i<titles.Length;i++){
            int index=i;
            navs[i]=new NavButton(titles[i],delegate{ShowPage(index);}){Bounds=new Rectangle(ContentX+i*104,96,96,40)};
            Controls.Add(navs[i]);
        }
        host=new Panel{Bounds=new Rectangle(ContentX,148,ContentW,742-148-20),BackColor=Ui.Shell,AutoScroll=true};
        Controls.Add(host);
    }

    private Panel NewPage(){
        var page=new Panel{Bounds=new Rectangle(0,0,PageW,560),BackColor=Ui.Shell,Visible=false};
        host.Controls.Add(page);
        return page;
    }

    private void BuildPages(){
        pages=new Panel[6];
        pages[0]=BuildAnimals();
        pages[1]=BuildLook();
        pages[2]=BuildBehaviour();
        pages[3]=BuildRoutes();
        pages[4]=BuildEvents();
        pages[5]=BuildGuide();
        ShowPage(0);
    }

    private void ShowPage(int index){
        activePage=index;
        for(int i=0;i<pages.Length;i++){if(pages[i]!=null)pages[i].Visible=i==index;if(navs!=null&&i<navs.Length)navs[i].Active=i==index;}
        host.AutoScrollPosition=new Point(0,0);
        if(navs!=null)foreach(var n in navs)n.Invalidate();
    }

    // ---------- control helpers ----------
    private Label L(Control parent,string text,float size,bool bold,Color color,Rectangle bounds,Color back,ContentAlignment align){
        var l=new Label{Text=text,Font=Ui.F(size,bold?FontStyle.Bold:FontStyle.Regular),ForeColor=color,BackColor=back,AutoSize=false,Bounds=bounds,TextAlign=align};
        parent.Controls.Add(l);return l;
    }
    private Card C(Control parent,int x,int y,int w,int h){var c=new Card{Bounds=new Rectangle(x,y,w,h)};parent.Controls.Add(c);return c;}
    private PillButton B(Control parent,string text,PillKind kind,int x,int y,int w,int h,Action click){
        var b=new PillButton(text,kind,click){Bounds=new Rectangle(x,y,w,h)};parent.Controls.Add(b);return b;
    }
    private ToggleRow T(Control parent,string caption,string hint,bool value,int x,int y,int w,Action<bool> change){
        var t=new ToggleRow(caption,hint,value){Bounds=new Rectangle(x,y,w,54)};
        t.Changed+=delegate{if(!loading)change(t.On);};
        parent.Controls.Add(t);return t;
    }
    private Slider S(Control parent,string caption,int min,int max,int value,string unit,int x,int y,int w,Action<int> live,Action<int> commit){
        var s=new Slider(min,max,value){Caption=caption,Unit=unit,Bounds=new Rectangle(x,y,w,58)};
        s.ValueChanged+=delegate{if(!loading)live(s.Value);};
        s.Done+=delegate{if(!loading)commit(s.Value);};
        parent.Controls.Add(s);return s;
    }

    // ---------- page: animals ----------
    private Panel BuildAnimals(){
        var page=NewPage();
        L(page,"物种数量",10.5f,true,Ui.Ink,new Rectangle(0,0,300,24),Ui.Shell,ContentAlignment.MiddleLeft);
        var add=new PillButton("+ 添加动物",PillKind.Dark,null){Bounds=new Rectangle(692,0,98,28)};
        add.Click+=delegate{ShowAddMenu(add);};
        page.Controls.Add(add);

        var defs=CreatureCatalog.All;
        for(int i=0;i<defs.Count;i++){
            var d=defs[i];int col=i%5,row=i/5;
            var chip=C(page,col*158,34+row*72,150,64);
            L(chip,d.Name,10,true,Ui.Ink,new Rectangle(10,6,92,22),Ui.Card,ContentAlignment.MiddleLeft);
            L(chip,"桌面活动",7.5f,false,Ui.Muted,new Rectangle(10,30,80,16),Ui.Card,ContentAlignment.MiddleLeft);
            counters[d.Id]=L(chip,"0",15,true,Ui.Species(d.Id),new Rectangle(94,4,46,28),Ui.Card,ContentAlignment.MiddleRight);
            B(chip,"-",PillKind.Dark,10,36,42,24,delegate{ChangeCount(d.Id,-1);});
            B(chip,"+",PillKind.Dark,56,36,42,24,delegate{ChangeCount(d.Id,1);});
        }

        L(page,"个体（点选后在下方编辑）",10.5f,true,Ui.Ink,new Rectangle(0,182,400,24),Ui.Shell,ContentAlignment.MiddleLeft);
        var listCard=C(page,0,210,PageW,182);
        list=new AnimalList{Bounds=new Rectangle(10,10,PageW-20,162)};
        list.SelectedIndexChanged+=delegate{if(!loading)LoadEditor();};
        listCard.Controls.Add(list);

        var edit=C(page,0,402,PageW,216);
        L(edit,"名字",8.5f,false,Ui.Muted,new Rectangle(16,10,80,18),Ui.Card,ContentAlignment.MiddleLeft);
        nameBox=new TextBox{Bounds=new Rectangle(16,28,180,30),BackColor=Ui.Shell,ForeColor=Ui.Ink,BorderStyle=BorderStyle.FixedSingle,Font=Ui.F(10,FontStyle.Regular),MaxLength=16};
        nameBox.KeyDown+=delegate(object s,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;SaveEdit();}};
        edit.Controls.Add(nameBox);
        nameBox.BackColor=Ui.Shell;nameBox.ForeColor=Ui.Ink;

        L(edit,"物种",8.5f,false,Ui.Muted,new Rectangle(210,10,60,18),Ui.Card,ContentAlignment.MiddleLeft);
        for(int i=0;i<defs.Count;i++){
            var d=defs[i];int col=i%5,row=i/5;
            speciesBtns[d.Id]=B(edit,d.Name,PillKind.Dark,210+col*80,28+row*36,74,30,delegate{SetSpecies(d.Id);});
        }

        L(edit,"性格",8.5f,false,Ui.Muted,new Rectangle(16,104,60,18),Ui.Card,ContentAlignment.MiddleLeft);
        for(int i=0;i<4;i++){
            string t=Tempers[i];
            temperBtns[t]=B(edit,t,PillKind.Dark,16+i*70,122,62,30,delegate{SetTemper(t);});
        }

        L(edit,"体型",8.5f,false,Ui.Muted,new Rectangle(320,104,60,18),Ui.Card,ContentAlignment.MiddleLeft);
        sizeSlider=new Slider(50,200,100){Compact=true,Unit="%",Bounds=new Rectangle(320,122,250,34)};
        sizeSlider.ValueChanged+=delegate{if(!loading)ApplySizeLive();};
        sizeSlider.Done+=delegate{if(!loading)world.SaveSoon();};
        edit.Controls.Add(sizeSlider);

        L(edit,"路线",8.5f,false,Ui.Muted,new Rectangle(16,162,60,18),Ui.Card,ContentAlignment.MiddleLeft);
        for(int i=0;i<Routes.Length;i++){
            string r=Routes[i];
            routeBtns[r]=B(edit,r,PillKind.Dark,16+i*68,180,66,28,delegate{SetRoute(r);});
        }

        B(edit,"保存修改",PillKind.Primary,556,178,110,32,delegate{SaveEdit();});
        B(edit,"移除",PillKind.Danger,674,178,98,32,delegate{RemoveSelected();});
        return page;
    }

    private void ShowAddMenu(Control anchor){
        var menu=new ContextMenuStrip();Ui.StyleMenu(menu);
        foreach(var d in CreatureCatalog.All)menu.Items.Add(d.Name,null,delegate{world.AddAnimal(d.Id);ReloadList();SyncFromConfig();});
        menu.Closed+=delegate{System.Threading.SynchronizationContext ctx=System.Threading.SynchronizationContext.Current;if(ctx!=null)ctx.Post(delegate{if(!menu.IsDisposed)menu.Dispose();},null);};
        menu.Show(anchor,new Point(0,anchor.Height+4));
    }

    // ---------- page: look ----------
    private Panel BuildLook(){
        var page=NewPage();
        var themeCard=C(page,0,0,PageW,92);
        themeToggle=T(themeCard,"浅色主题","关闭后使用深色界面（立即生效）",world.Config.LightTheme,16,6,PageW-32,delegate(bool v){world.SetTheme(v);});

        var c1=C(page,0,104,PageW,132);
        scaleSlider=S(c1,"全局大小",25,600,world.Config.Scale,"%",16,12,360,
            delegate(int v){world.Config.Scale=v;},delegate(int v){world.Config.Scale=v;world.Apply(false);});
        speedSlider=S(c1,"基础速度",10,160,world.Config.Speed," 像素/秒",400,12,374,
            delegate(int v){world.Config.Speed=v;},delegate(int v){world.Config.Speed=v;world.Apply(false);});

        var c2=C(page,0,248,PageW,196);
        fps60=T(c2,"60 FPS（默认 30）","画面更顺，占用略高",world.Config.Fps==60,16,6,PageW-32,delegate(bool v){world.Config.Fps=v?60:30;world.Apply(false);});
        battery=T(c2,"电池供电时降到 24 FPS","省电优先",world.Config.BatterySaver,16,62,PageW-32,delegate(bool v){world.Config.BatterySaver=v;world.Apply(false);});
        names=T(c2,"在动物下方显示名字和行为","调试或演示时更直观",world.Config.ShowNames,16,118,PageW-32,delegate(bool v){world.Config.ShowNames=v;world.Apply(false);});

        L(page,"清晰范围 25–600%。600% 仍然从高清母图重算，但不是无限无损放大，也不是原生 4K 素材。\n大量超大动物会增加绘制开销；帧率设置约 2.5 秒内热生效。",9,false,Ui.Muted,new Rectangle(4,456,PageW-8,80),Ui.Shell,ContentAlignment.TopLeft);
        return page;
    }

    // ---------- page: behaviour ----------
    private Panel BuildBehaviour(){
        var page=NewPage();
        var c=C(page,0,0,390,400);
        social=T(c,"启用动物之间的互动","相遇时试探、避让或追逐",world.Config.Social,16,6,358,delegate(bool v){world.Config.Social=v;world.Apply(false);});
        peaceful=T(c,"和平模式","禁止追逐与争斗",world.Config.Peaceful,16,62,358,delegate(bool v){world.SetPeaceful(v);});
        fear=T(c,"鼠标靠近时受惊","胆小的个体会先跑开",world.Config.MouseFear,16,118,358,delegate(bool v){world.Config.MouseFear=v;world.Apply(false);});
        follow=T(c,"跟随鼠标","关闭后按自己的路线探索",world.Config.FollowMouse,16,174,358,delegate(bool v){world.Config.FollowMouse=v;ClearRoutes();world.Apply(false);});
        night=T(c,"夜间更容易休息","23:00–06:00 更常睡觉",world.Config.NightSleep,16,230,358,delegate(bool v){world.Config.NightSleep=v;world.Apply(false);});
        carryIcons=T(c,"动物搬运桌面图标","任何动物都能把桌面图标搬到别处（可一键还原）",world.Config.CarryIcons,16,286,358,delegate(bool v){world.Config.CarryIcons=v;world.Apply(false);if(v)world.Log("动物们可以搬桌面图标了");});
        flight=T(c,"动物短距离飞行","会飞的振翅、不会飞的蹦跳飘移，落地再走",world.Config.Flight,16,342,358,delegate(bool v){world.Config.Flight=v;world.Apply(false);});

        var s=C(page,404,0,386,112);
        aggrSlider=S(s,"跨物种争斗倾向",0,100,world.Config.Aggression,"%",16,14,354,
            delegate(int v){world.Config.Aggression=v;},delegate(int v){world.Config.Aggression=v;world.Apply(false);});

        var presets=C(page,404,124,386,196);
        L(presets,"快捷预设",8.5f,false,Ui.Muted,new Rectangle(16,10,200,18),Ui.Card,ContentAlignment.MiddleLeft);
        B(presets,"安静办公",PillKind.Ghost,16,34,354,36,delegate{world.Config.Peaceful=true;world.Config.Speed=20;world.Config.AllowOffscreen=false;world.Config.MouseFear=false;world.Config.CarryIcons=false;world.Config.Scale=75;world.Apply(false);world.Log("已应用安静办公预设");SyncFromConfig();});
        B(presets,"活跃生态",PillKind.Ghost,16,80,354,36,delegate{world.Config.Social=true;world.Config.Peaceful=false;world.Config.AllowOffscreen=true;world.Config.Speed=45;world.Config.Aggression=65;world.Config.CarryIcons=true;world.Config.Scale=100;world.Apply(false);world.Log("已应用活跃生态预设");SyncFromConfig();});
        B(presets,"边缘漫游",PillKind.Ghost,16,126,354,36,delegate{world.Config.Peaceful=true;world.Config.ClimbMenus=world.Config.ClimbTaskbar=world.Config.ClimbWindows=true;for(int i=0;i<world.Pets.Count;i++)world.Pets[i].Profile.Route=i%2==0?"任务栏":"菜单栏";world.Apply(false);foreach(var animal in world.Pets)animal.PickRoute(animal.Profile.Route);world.Log("已应用边缘漫游预设");SyncFromConfig();});

        L(page,"争斗不造成伤害或死亡；性格和精力影响结果。食物最多八处，每处六份，动物会一口口吃掉。\n搬运图标是真实的桌面操作：只改变图标的显示位置，不打开、不读取、不移动任何文件。\n如果桌面开启了「自动排列图标」，图标会被系统立刻弹回，动物会自己放弃。",9,false,Ui.Muted,new Rectangle(4,412,PageW-8,86),Ui.Shell,ContentAlignment.TopLeft);
        return page;
    }

    // ---------- page: routes ----------
    private Panel BuildRoutes(){
        var page=NewPage();
        var c=C(page,0,0,PageW,290);
        tb=T(c,"沿任务栏边缘攀爬","",world.Config.ClimbTaskbar,16,6,370,delegate(bool v){world.Config.ClimbTaskbar=v;ClearRoutes();world.Apply(false);});
        menus=T(c,"菜单栏 / 工具栏 / 展开菜单","",world.Config.ClimbMenus,16,60,370,delegate(bool v){world.Config.ClimbMenus=v;ClearRoutes();world.Apply(false);});
        windows=T(c,"其他窗口边缘","",world.Config.ClimbWindows,16,114,370,delegate(bool v){world.Config.ClimbWindows=v;ClearRoutes();world.Apply(false);});
        icons=T(c,"桌面图标边缘","",world.Config.ClimbIcons,16,168,370,delegate(bool v){world.Config.ClimbIcons=v;ClearRoutes();world.Apply(false);});
        offscreen=T(c,"允许爬出屏幕再回来","",world.Config.AllowOffscreen,16,222,370,delegate(bool v){world.Config.AllowOffscreen=v;world.Apply(false);});

        hideFull=T(c,"全屏软件时自动隐藏","",world.Config.HideFullScreen,404,6,370,delegate(bool v){world.Config.HideFullScreen=v;world.Apply(false);});
        clickThrough=T(c,"鼠标完全穿透","",world.Config.ClickThrough,404,60,370,delegate(bool v){world.Config.ClickThrough=v;world.Apply(false);});
        autoStart=T(c,"登录 Windows 时启动","",world.Config.AutoStart,404,114,370,delegate(bool v){world.Config.AutoStart=v;world.Apply(false);});
        allScreens=T(c,"在所有显示器活动","",world.Config.AllScreens,404,168,370,delegate(bool v){world.Config.AllScreens=v;world.Recall();world.Apply(false);});

        var s=C(page,0,302,PageW,206);
        tripSlider=S(s,"外出间隔约",20,600,world.Config.ExcursionInterval," 秒",16,8,600,
            delegate(int v){world.Config.ExcursionInterval=v;},delegate(int v){world.Config.ExcursionInterval=v;foreach(var a in world.Pets)a.ResetExcursionClock();world.Apply(false);});
        minSlider=S(s,"屏外停留最短",1,120,world.Config.ReturnMin," 秒",16,70,600,
            delegate(int v){world.Config.ReturnMin=v;},delegate(int v){world.Config.ReturnMin=v;world.Apply(false);});
        maxSlider=S(s,"屏外停留最长",1,180,world.Config.ReturnMax," 秒",16,132,600,
            delegate(int v){world.Config.ReturnMax=v;},delegate(int v){world.Config.ReturnMax=v;world.Apply(false);});

        B(page,"全部沿任务栏",PillKind.Dark,0,520,150,38,delegate{foreach(var a in world.Pets)a.PickRoute("任务栏");});
        B(page,"全部沿菜单栏",PillKind.Dark,160,520,150,38,delegate{foreach(var a in world.Pets)a.PickRoute("菜单栏");});
        B(page,"全部出去逛逛",PillKind.Dark,320,520,150,38,delegate{foreach(var a in world.Pets)a.BeginExcursion(0);});
        routeInfo=L(page,"",8.5f,false,Ui.Muted,new Rectangle(484,516,306,44),Ui.Shell,ContentAlignment.MiddleRight);
        return page;
    }

    // ---------- page: events ----------
    private Panel BuildEvents(){
        var page=NewPage();
        var c=C(page,0,0,PageW,462);
        events=new TextBox{Bounds=new Rectangle(14,14,PageW-28,434),Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=Ui.Shell,ForeColor=Ui.Ink,BorderStyle=BorderStyle.None,Font=Ui.F(9.5f,FontStyle.Regular)};
        c.Controls.Add(events);
        events.BackColor=Ui.Shell;events.ForeColor=Ui.Ink;
        B(page,"清理食物",PillKind.Dark,0,474,140,38,delegate{world.ClearFood();world.Log("已清理所有食物");});
        B(page,"清空记录",PillKind.Dark,150,474,140,38,delegate{world.Events.Clear();events.Clear();lastEvent="";});
        B(page,"查看个体状态",PillKind.Dark,300,474,160,38,delegate{MessageBox.Show(this,String.Join("\n",world.Pets.Select(a=>a.Profile.Name+" / "+CreatureCatalog.Find(a.Species).Name+" / "+a.State+"   精力 "+(int)a.Energy+"   饥饿 "+(int)a.Hunger+"   信任 "+(int)a.Trust).ToArray()),"个体状态");});
        B(page,"还原被搬动的图标",PillKind.Primary,470,474,190,38,delegate{RestoreIcons();});
        iconStat=L(page,"",8.5f,false,Ui.Muted,new Rectangle(4,520,PageW-8,40),Ui.Shell,ContentAlignment.MiddleLeft);
        return page;
    }
    private void RestoreIcons(){
        int n=world.RestoreIcons();
        SyncFromConfig();
        MessageBox.Show(this,n>0?"已把 "+n+" 个图标放回原来的位置。":"没有可还原的图标。\n如果桌面开启了「自动排列图标」，系统会拒绝任意摆放，微野不会强行操作。","桌面图标",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    // ---------- page: guide ----------
    private Panel BuildGuide(){
        var page=NewPage();
        L(page,"让工具好用，也随时可控",13,true,Ui.Accent,new Rectangle(0,0,600,30),Ui.Shell,ContentAlignment.MiddleLeft);
        L(page,"左键轻点会受惊，按住可以拖动；滚轮放到任意动物身上就能放大缩小它的体型（不挑前后台窗口）；右键可指定路线、让它搬东西或安排外出。\n每只动物都能使用全部看家本领：搬桌面图标、腾空飞一段（会飞的振翅、不会飞的蹦跳飘移）、把食物推着走；此外还有嗅探、张望、抖动、晒太阳、蜘蛛装死、螳螂凝视这些日常小动作。托盘右键可一键还原被搬动的图标。\n动物不是永远置顶的贴纸：拖到某个窗口上，它就待在那个窗口里，窗口移动时一起走；被其他窗口盖住时会被遮挡。\n九种动物各有脾气：蜘蛛最快、螳螂爱凝视、蜗牛最慢，可在管理器里点「物种」按钮随时换形。",9f,false,Ui.Muted,new Rectangle(0,44,PageW-24,132),Ui.Shell,ContentAlignment.TopLeft);
        L(page,"快捷键",9.5f,true,Ui.Ink,new Rectangle(0,196,200,24),Ui.Shell,ContentAlignment.MiddleLeft);
        L(page,"Ctrl + Alt + P   隐藏 / 显示\nCtrl + Alt + O   打开管理器\nCtrl + Alt + F   鼠标处投喂\nCtrl + Alt + H   全部回家（含屏外）\nCtrl + Alt + M   和平模式开关",9.5f,false,Ui.Muted,new Rectangle(0,222,PageW-24,140),Ui.Shell,ContentAlignment.TopLeft);
        L(page,"退出与文件",9.5f,true,Ui.Ink,new Rectangle(0,376,200,24),Ui.Shell,ContentAlignment.MiddleLeft);
        L(page,"关闭面板后在托盘运行。彻底退出：托盘右键 → 退出微野。锁屏自动暂停，解锁恢复。\n配置保存在 LocalAppData\\DesktopCritters\\settings-ecosystem.xml，旧版配置会保留。",9.5f,false,Ui.Muted,new Rectangle(0,402,PageW-24,70),Ui.Shell,ContentAlignment.TopLeft);
        L(page,"高清素材由 AI 生成，属于二维网格形变动画，不是原生 4K 或真实昆虫录像；缩放范围有限。争斗、结伴、饥饿和精力都是游戏化行为。\n搬运图标通过桌面外壳的列表视图完成，只改显示位置，不注入应用、不打开或读取文件内容；桌面开启「自动排列图标」时系统会拒绝摆放，动物会自行放弃。\n不截图识别、不记录键盘，也没有网络请求。窗口遮挡、权限、混合 DPI 和自绘界面可能影响路线识别。",8.5f,false,Color.FromArgb(112,130,122),new Rectangle(0,490,PageW-24,86),Ui.Shell,ContentAlignment.TopLeft);
        L(page,"联系作者：微信公众号「景同志」 · 小红书「景同学」（右上角「关于」按钮可一键复制）",9.5f,true,Ui.Ink,new Rectangle(0,592,PageW-8,24),Ui.Shell,ContentAlignment.MiddleLeft);
        if(world.Keys.Failed.Count>0)L(page,"快捷键冲突："+String.Join("、",world.Keys.Failed.ToArray()),8.5f,false,Ui.Warm,new Rectangle(0,618,PageW-24,24),Ui.Shell,ContentAlignment.MiddleLeft);
        return page;
    }

    // ---------- data flow ----------
    private void ReloadList(){
        bool before=loading;loading=true;
        string keep=selId;
        list.BeginUpdate();list.Items.Clear();
        foreach(var p in world.Config.Animals)list.Items.Add(p);
        if(keep!=null)for(int i=0;i<list.Items.Count;i++){var p=list.Items[i] as AnimalProfile;if(p!=null&&p.Id==keep){list.SelectedIndex=i;break;}}
        if(list.SelectedIndex<0&&list.Items.Count>0)list.SelectedIndex=0;
        list.EndUpdate();
        loading=before;
        LoadEditor();
    }

    private void LoadEditor(){
        var p=list.SelectedItem as AnimalProfile;
        bool on=p!=null;
        nameBox.Enabled=on;sizeSlider.Enabled=on;
        foreach(var b in speciesBtns.Values)b.Enabled=on;
        foreach(var b in temperBtns.Values)b.Enabled=on;
        foreach(var b in routeBtns.Values)b.Enabled=on;
        if(!on){nameBox.Text="";selId=null;SetPreview(null);return;}
        selId=p.Id;
        nameBox.Text=p.Name;
        sizeSlider.SetValue(p.Size);
        foreach(var kv in speciesBtns.Values)kv.Kind=PillKind.Dark;
        if(speciesBtns.ContainsKey(p.Species))speciesBtns[p.Species].Kind=PillKind.Primary;
        foreach(var kv in temperBtns.Values)kv.Kind=PillKind.Dark;
        if(temperBtns.ContainsKey(p.Temperament))temperBtns[p.Temperament].Kind=PillKind.Primary;
        SetRoute(p.Route);
        foreach(var b in speciesBtns.Values)b.Invalidate();
        foreach(var b in temperBtns.Values)b.Invalidate();
        SetPreview(p);
    }

    private void SetRoute(string r){
        routeSel=r;
        foreach(var kv in routeBtns.Values)kv.Kind=PillKind.Dark;
        if(routeBtns.ContainsKey(r))routeBtns[r].Kind=PillKind.Primary;
        foreach(var b in routeBtns.Values)b.Invalidate();
    }

    private void SetSpecies(string id){
        foreach(var kv in speciesBtns.Values)kv.Kind=PillKind.Dark;
        if(speciesBtns.ContainsKey(id))speciesBtns[id].Kind=PillKind.Primary;
        foreach(var b in speciesBtns.Values)b.Invalidate();
    }
    // Dragging the size slider resizes the selected pet on the spot instead of
    // waiting for 保存修改 — the old slider was not wired to anything.
    private void ApplySizeLive(){
        var p=list.SelectedItem as AnimalProfile;
        if(p==null)return;
        p.Size=Settings.Limit(sizeSlider.Value,50,200);
        foreach(var pet in world.Pets){if(pet.IsDisposed)continue;if(pet.Profile==p)pet.DrawFrame();}
    }
    private void SetTemper(string t){
        foreach(var kv in temperBtns.Values)kv.Kind=PillKind.Dark;
        if(temperBtns.ContainsKey(t))temperBtns[t].Kind=PillKind.Primary;
        foreach(var b in temperBtns.Values)b.Invalidate();
    }

    private void SaveEdit(){
        var p=list.SelectedItem as AnimalProfile;
        if(p==null)return;
        string name=(nameBox.Text??"").Trim();if(name.Length==0)name="小动物";
        p.Name=name.Length>16?name.Substring(0,16):name;
        p.Size=sizeSlider.Value;
        p.Temperament=SelectedTemper();
        p.Route=routeSel;
        foreach(var kv in speciesBtns)if(kv.Value.Kind==PillKind.Primary)p.Species=kv.Key;
        selId=p.Id;world.Apply(true);ReloadList();SyncFromConfig();world.Log(p.Name+" 的个体设置已更新");
    }

    private string SelectedTemper(){foreach(var kv in temperBtns)if(kv.Value.Kind==PillKind.Primary)return kv.Key;return "均衡";}

    private void RemoveSelected(){
        var p=list.SelectedItem as AnimalProfile;
        if(p==null)return;
        if(MessageBox.Show(this,"移除「"+p.Name+"」？","个体管理",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        world.Config.Animals.Remove(p);selId=null;world.Apply(true);ReloadList();SyncFromConfig();world.Log("已移除一只动物");
    }

    private void ChangeCount(string id,int delta){
        int count=world.Config.Animals.Count(a=>a.Species==id);
        world.SetSpeciesCount(id,Settings.Limit(count+delta,0,24));
        ReloadList();SyncFromConfig();
    }

    private void ClearRoutes(){foreach(var a in world.Pets){a.ResetRoute();a.SetState("探索",2);}}

    private void SyncFromConfig(){
        bool before=loading;loading=true;
        var c=world.Config;
        if(scaleSlider!=null)scaleSlider.SetValue(c.Scale);
        if(speedSlider!=null)speedSlider.SetValue(c.Speed);
        if(aggrSlider!=null)aggrSlider.SetValue(c.Aggression);
        if(tripSlider!=null)tripSlider.SetValue(c.ExcursionInterval);
        if(minSlider!=null)minSlider.SetValue(c.ReturnMin);
        if(maxSlider!=null)maxSlider.SetValue(c.ReturnMax);
        if(fps60!=null)fps60.On=c.Fps==60;
        if(themeToggle!=null)themeToggle.On=c.LightTheme;
        if(battery!=null)battery.On=c.BatterySaver;
        if(names!=null)names.On=c.ShowNames;
        if(social!=null)social.On=c.Social;
        if(peaceful!=null)peaceful.On=c.Peaceful;
        if(fear!=null)fear.On=c.MouseFear;
        if(follow!=null)follow.On=c.FollowMouse;
        if(night!=null)night.On=c.NightSleep;
        if(carryIcons!=null)carryIcons.On=c.CarryIcons;
        if(flight!=null)flight.On=c.Flight;
        if(tb!=null)tb.On=c.ClimbTaskbar;
        if(menus!=null)menus.On=c.ClimbMenus;
        if(windows!=null)windows.On=c.ClimbWindows;
        if(icons!=null)icons.On=c.ClimbIcons;
        if(offscreen!=null)offscreen.On=c.AllowOffscreen;
        if(hideFull!=null)hideFull.On=c.HideFullScreen;
        if(clickThrough!=null)clickThrough.On=c.ClickThrough;
        if(autoStart!=null)autoStart.On=c.AutoStart;
        if(allScreens!=null)allScreens.On=c.AllScreens;
        loading=before;
        RefreshLive();
    }

    private void SetPreview(AnimalProfile p){
        if(picture.Image!=null){var old=picture.Image;picture.Image=null;old.Dispose();}
        previewKey="";
        if(p==null){previewName.Text="未选择";previewMeta.Text="";return;}
        using(var b=CreatureRenderer.Render(p.Species,1.1,-30,0.74f,"探索",0,false))picture.Image=(Bitmap)b.Clone();
        previewName.Text=p.Name;
        previewMeta.Text=CreatureCatalog.Find(p.Species).Name+" · "+p.Temperament+" · "+p.Route;
        previewKey=p.Id+p.Size+p.Species;
    }

    internal void RefreshLive(){
        if(pages==null)return;
        var c=world.Config;
        string state=world.Hidden?"已隐藏":world.SessionLocked?"锁屏暂停":world.FullScreenHidden?"全屏避让":world.Paused?"已暂停":c.Peaceful?"和平生态":"生态活动中";
        statusLine.Text=state;
        statusLine.ForeColor=world.Paused||world.Hidden?Ui.Warm:Ui.Accent;
        counts.Text=world.Pets.Count+" 只动物 · "+world.Pets.Select(p=>p.Species).Distinct().Count()+" 个物种\n屏外 "+world.Pets.Count(p=>p.Offscreen)+" · 已返回 "+world.Pets.Sum(p=>p.Returns)+"\n进食 "+world.FeedCount+" · 争斗 "+world.FightCount+" · 搬运 "+world.CarryCount+"\n路线 "+world.Map.Items.Count+" 条 · 纹理 "+(PhotoSpriteRenderer.CacheBytes/1048576)+" MB";
        foreach(var kv in counters)kv.Value.Text=Convert.ToString(c.Animals.Count(a=>a.Species==kv.Key));
        pauseBtn.Text=world.Paused?"继续活动":"暂停";
        hideBtn.Text=world.Hidden?"显示动物":"隐藏动物";
        peaceBtn.Text=c.Peaceful?"和平模式：开":"和平模式：关";
        peaceBtn.Kind=c.Peaceful?PillKind.Primary:PillKind.Ghost;peaceBtn.Invalidate();
        if(events!=null&&world.Events.Count>0&&world.Events.Last()!=lastEvent){
            lastEvent=world.Events.Last();events.Text=String.Join(Environment.NewLine,world.Events.ToArray());
            events.SelectionStart=events.TextLength;events.ScrollToCaret();
        }
        if(iconStat!=null){
            int moved=c.IconMoves==null?0:c.IconMoves.Count;
            string head=DesktopIcons.Virtual?"测试用的模拟图标层":(DesktopIcons.Available?"桌面图标可读写":"桌面图标不可用");
            iconStat.Text="桌面图标："+head+" · 识别 "+DesktopIcons.ProbeCount+" 个 · 已搬动记录 "+moved+" 条"+
                (String.IsNullOrEmpty(DesktopIcons.LastError)?"":"\n"+DesktopIcons.LastError)+
                (String.IsNullOrEmpty(world.IconError)?"":"\n"+world.IconError);
            iconStat.ForeColor=DesktopIcons.Available?Ui.Muted:Ui.Warm;
        }
        if(routeInfo!=null){
            var lines=world.Map.Items.GroupBy(s=>s.Kind).Select(g=>g.Key+" "+g.Count()).ToArray();
            routeInfo.Text="识别到 "+world.Map.Items.Count+" 条可攀爬边缘"+(lines.Length>0?"\n"+String.Join(" · ",lines):"");
        }
        var sel=list==null?null:list.SelectedItem as AnimalProfile;
        if(sel!=null&&(sel.Id+sel.Size+sel.Species)!=previewKey)SetPreview(sel);
    }

    internal void CapturePages(string directory){
        int old=activePage;
        for(int i=0;i<pages.Length;i++){
            ShowPage(i);Application.DoEvents();RefreshLive();
            using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(System.IO.Path.Combine(directory,"panel-"+i+".png"),ImageFormat.Png);}
        }
        ShowPage(old);
    }

    protected override void Dispose(bool disposing){
        if(disposing){
            if(pulse!=null)pulse.Dispose();
            if(picture!=null&&picture.Image!=null){var img=picture.Image;picture.Image=null;img.Dispose();}
            if(logoBox!=null&&logoBox.Image!=null){var img=logoBox.Image;logoBox.Image=null;img.Dispose();}
        }
        base.Dispose(disposing);
    }
}
}
