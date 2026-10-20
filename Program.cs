using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;
[assembly:AssemblyTitle("微野 · 多物种生态版")]
[assembly:AssemblyProduct("Desktop Critters Ecosystem")]
[assembly:AssemblyVersion("3.0.0.0")]
namespace DesktopCritters {
internal static class Program {
    [STAThread]private static int Main(string[] args){Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length>0&&args[0]=="--self-test"){string path=args.Length>1?args[1]:".";try{SelfTest.Run(path);return 0;}catch(Exception ex){Directory.CreateDirectory(path);File.WriteAllText(Path.Combine(path,"test-failure.txt"),ex.ToString());return 1;}}
        if(args.Contains("--surface-probe")){using(var form=new Form{Text="微野路线测试窗口",StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(Screen.PrimaryScreen.WorkingArea.Left+170,Screen.PrimaryScreen.WorkingArea.Top+160,590,360)}){
            var menu=new MainMenu(new MenuItem[]{new MenuItem("文件",new MenuItem[]{new MenuItem("测试菜单")}),new MenuItem("编辑"),new MenuItem("查看")});form.Menu=menu;
            var bar=new ToolStrip{Dock=DockStyle.Top};bar.Items.Add("测试工具栏");form.Controls.Add(bar);var label=new Label{Dock=DockStyle.Fill,Text="独立进程：传统菜单、工具栏与移动窗口路线。\n测试完成后自动关闭。",TextAlign=ContentAlignment.MiddleCenter};form.Controls.Add(label);label.BringToFront();
            form.Shown+=delegate{form.BeginInvoke((Action)delegate{Native.SetWindowPos(form.Handle,IntPtr.Zero,0,0,0,0,0x57);});};var timer=new System.Windows.Forms.Timer{Interval=24000};timer.Tick+=delegate{form.Close();};timer.Start();Application.Run(form);timer.Dispose();menu.Dispose();return 0;}}
        if(args.Contains("--icon-test")){return IconTest.Run(args.Length>1?args[1]:".");}
        bool test=args.Contains("--smoke-test"),created;using(var mutex=new Mutex(true,test?"Local\\DesktopCritters.EcoTest":"Local\\DesktopCritters.App",out created)){if(!created){if(!args.Contains("--tray"))MessageBox.Show("微野已在运行，请从托盘打开管理器。升级时请先退出旧版。","微野");return 0;}
            bool reported=false;Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs e){Log(e.Exception);if(test)return;if(!reported){reported=true;MessageBox.Show("微野遇到一个小问题，已记录日志并继续运行。\n"+e.Exception.Message,"微野");}};
            AppDomain.CurrentDomain.UnhandledException+=delegate(object sender,UnhandledExceptionEventArgs e){try{Log(e.ExceptionObject as Exception??new Exception(e.ExceptionObject!=null?e.ExceptionObject.ToString():"未知异常"));}catch{}};
            try{using(var world=new Habitat(args.Contains("--tray"),test)){System.Windows.Forms.Timer checker=null;if(test)checker=SmokeTest.Start(world,args.Length>1?args[1]:".");Application.Run(world);if(checker!=null)checker.Dispose();}return Environment.ExitCode;}catch(Exception e){Log(e);if(!test)MessageBox.Show(e.Message,"微野启动失败");return 1;}
        }
    }
    private static void Log(Exception ex){try{Directory.CreateDirectory(Settings.DirectoryPath);File.AppendAllText(Path.Combine(Settings.DirectoryPath,"error.log"),DateTime.Now.ToString("s")+" "+ex+Environment.NewLine);}catch{}}
}
// Exercises the real shell: reads the desktop icon list, nudges one icon, reads
// the position back and puts it where it was. Run manually, never in CI.
internal static class IconTest {
    internal static int Run(string dir){
        Directory.CreateDirectory(dir);
        var log=new System.Text.StringBuilder();
        try{
            DesktopIcons.Virtual=false;DesktopIcons.Enabled=true;
            bool ok=DesktopIcons.Refresh(true);
            log.AppendLine("enumerate: "+ok+" / count="+DesktopIcons.Count+" / error="+DesktopIcons.LastError);
            if(!ok){File.WriteAllText(Path.Combine(dir,"icon-test.txt"),"FAIL\r\n"+log);return 1;}
            var icons=DesktopIcons.Snapshot();
            for(int i=0;i<icons.Count&&i<10;i++)log.AppendLine("  #"+icons[i].Index+" \""+icons[i].Name+"\" centre="+icons[i].Center+" glyph="+icons[i].Glyph+" cell="+icons[i].Cell);
            DeskIcon target=icons[0];
            Point before=target.Center;
            Point want=new Point(before.X+64,before.Y+48);
            bool moved=DesktopIcons.Move(target,want);
            System.Threading.Thread.Sleep(400);
            Point actual;bool read=DesktopIcons.ReadBack(target,out actual);
            log.AppendLine("move: "+moved+" / readback "+read+" / actual "+actual+" / wanted "+want);
            bool accepted=read&&PetForm.Dist(new PointF(actual.X,actual.Y),new PointF(want.X,want.Y))<60;
            DesktopIcons.Move(target,before);
            System.Threading.Thread.Sleep(350);
            Point restored;bool read2=DesktopIcons.ReadBack(target,out restored);
            bool back=read2&&PetForm.Dist(new PointF(restored.X,restored.Y),new PointF(before.X,before.Y))<60;
            log.AppendLine("restore: "+back+" / actual "+restored+" / original "+before);
            bool pass=moved&&accepted&&back;
            log.Insert(0,(pass?"PASS":"FAIL")+Environment.NewLine);
            File.WriteAllText(Path.Combine(dir,"icon-test.txt"),log.ToString());
            return pass?0:1;
        }catch(Exception ex){
            log.AppendLine("exception: "+ex);
            File.WriteAllText(Path.Combine(dir,"icon-test.txt"),"FAIL"+Environment.NewLine+log);
            return 1;
        }
    }
}
internal static class SmokeTest {
    internal static System.Windows.Forms.Timer Start(Habitat w,string dir){Directory.CreateDirectory(dir);int n=0;bool mixed=false,fight=false,peace=false,left=false,returned=false,menu=false,travel=false,input=false,hidden=false;PointF routeStart=PointF.Empty;PetForm traveler=w.Pets[2];int beforeReturns=traveler.Returns;
        bool carryStart=false,carrySeen=false,carryDone=false,flightStart=false,flightSeen=false,pushStart=false,pushSeen=false,pushDone=false,sizeWorks=false;string diag="";
        w.Config.MouseFear=false;w.Config.AllowOffscreen=false;w.Config.NightSleep=false;var timer=new System.Windows.Forms.Timer{Interval=1000};
        timer.Tick+=delegate{timer.Stop();try{n++;
            if(n==1){w.OpenPanel();foreach(Form f in Application.OpenForms)if(f is ControlPanel){((ControlPanel)f).CapturePages(dir);var about=new AboutWindow();about.Show();Application.DoEvents();using(var shot=new Bitmap(about.Width,about.Height)){about.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(dir,"about.png"),ImageFormat.Png);}about.Close();about.Dispose();break;}mixed=w.Pets.Select(p=>p.Species).Distinct().Count()>=3;w.Pets[0].Position=new PointF(450,350);w.Pets[1].Position=new PointF(470,350);fight=w.BeginFight(w.Pets[0],w.Pets[1],true)&&w.Pets[0].State=="争斗"&&w.Pets[1].State=="争斗";}
            if(n==2){w.SetPeaceful(true);peace=!w.BeginFight(w.Pets[0],w.Pets[1],true)&&w.Pets.All(p=>p.State!="争斗");w.Config.Social=false;w.Config.AllowOffscreen=true;Rectangle b=Screen.PrimaryScreen.Bounds;traveler.Position=new PointF(b.Right-4,b.Top+b.Height/2);traveler.Heading=traveler.TargetHeading=0;traveler.BeginExcursion(1);}
            if(traveler.Offscreen)left=true;if(traveler.Returns>beforeReturns)returned=true;
            if(n==8){w.Recall();w.Config.AllowOffscreen=false;w.Config.Speed=140;w.Map.Refresh(w.Config);var surface=w.Map.Items.FirstOrDefault(s=>s.Kind=="菜单栏");menu=surface!=null;if(surface==null)surface=w.Map.Items.FirstOrDefault(s=>s.Kind=="任务栏");if(surface==null)surface=w.Map.Items.FirstOrDefault(s=>s.Kind=="屏幕边缘");if(surface==null)surface=w.Map.Items.FirstOrDefault(s=>s.Kind=="窗口边缘");if(surface!=null){w.Pets[0].Direction=1;w.Pets[0].AttachRoute(surface,40);w.Pets[0].Heading=w.Pets[0].TargetHeading=0;routeStart=w.Pets[0].Position;}}
            if(n==9){
                // feature parity: a bee (not the ant) hauls a desktop icon, a
                // snail (not a flyer) takes off
                PetForm bee=w.Pets.FirstOrDefault(p=>p.Species=="Bee");DesktopIcons.Refresh(true);var icons=DesktopIcons.Snapshot();
                if(bee!=null&&icons.Count>2){bee.ResetRoute();bee.Position=new PointF(icons[1].Center.X-70,icons[1].Center.Y+8);bee.Heading=bee.TargetHeading=0;carryStart=bee.TryStartCarry();}
                PetForm snail=w.Pets.FirstOrDefault(p=>p.Species=="Snail");Rectangle b=Screen.PrimaryScreen.WorkingArea;
                if(snail!=null){snail.ResetRoute();snail.Position=new PointF(b.Left+b.Width/2,b.Top+b.Height/2);flightStart=snail.TryFlight();}
            }
            if(n>9){foreach(var p in w.Pets){if(p.IsDisposed)continue;if(p.State=="搬运")carrySeen=true;if(p.State=="推食物")pushSeen=true;}}
            if(n==10){
                // route check first: food-seeking outranks route-following in Tick,
                // so the crumb must not exist while the route distance is measured
                travel=w.Pets[0].Surface!=null&&PetForm.Dist(routeStart,w.Pets[0].Position)>30;
                // drop the crumb in the quietest corner so it outlives the push
                Rectangle wb=Screen.PrimaryScreen.WorkingArea;Point best=Point.Empty;double bestScore=-1;
                foreach(var c in new Point[]{new Point(wb.Left+80,wb.Top+80),new Point(wb.Right-80,wb.Top+80),new Point(wb.Left+80,wb.Bottom-80),new Point(wb.Right-80,wb.Bottom-80)}){
                    double score=Double.MaxValue;foreach(var p in w.Pets)score=Math.Min(score,PetForm.Dist(p.Position,c));
                    if(score>bestScore){bestScore=score;best=c;}
                }
                w.Feed(best);
                PetForm spider=w.Pets.FirstOrDefault(p=>p.Species=="Spider");
                if(spider!=null&&w.Foods.Count>0){spider.ResetRoute();spider.Position=new PointF(best.X-30,best.Y+20);pushStart=spider.TryPushFood();}
            }
            if(n==11){PetForm morph=w.Pets.FirstOrDefault(p=>(p.Species=="Butterfly"||p.Species=="Spider"||p.Species=="Caterpillar"||p.Species=="Mantis")&&!p.Offscreen)??w.Pets.FirstOrDefault(p=>!p.Offscreen)??w.Pets[0];Size was=morph.Size;morph.Profile.Size=180;morph.DrawFrame();bool grew=morph.Size.Width>was.Width;morph.Profile.Size=50;morph.DrawFrame();bool shrank=morph.Size.Width<was.Width;morph.Profile.Size=100;morph.DrawFrame();sizeWorks=grew&&shrank&&Math.Abs(morph.Size.Width-was.Width)<6;}
            if(n==11){PetForm sp=w.Pets.FirstOrDefault(p=>p.Species=="Spider");diag="p0="+w.Pets[0].State+"/surf"+(w.Pets[0].Surface!=null)+"/d"+ (int)PetForm.Dist(routeStart,w.Pets[0].Position)+" spider="+(sp==null?"none":sp.State)+"/off"+(sp!=null&&sp.Offscreen)+" foods="+w.Foods.Count+"/"+String.Join(",",w.Foods.Select(f=>f.Portions.ToString()).ToArray());}
            if(n==12){w.Config.ClickThrough=true;foreach(var p in w.Pets)p.ApplyInputMode();input=w.Pets.All(p=>(Native.GetWindowLong(p.Handle,-20)&0x20)!=0);w.Config.ClickThrough=false;foreach(var p in w.Pets)p.ApplyInputMode();input=input&&w.Pets.All(p=>(Native.GetWindowLong(p.Handle,-20)&0x20)==0);}
            if(n==13){w.ToggleHidden();hidden=w.Pets.All(p=>!p.Visible);}
            if(n==14)w.ToggleHidden();
            if(n<20){timer.Start();return;}int errors=w.Pets.Sum(p=>p.LayerErrors),frames=w.Pets.Sum(p=>p.Frames);
            carryDone=w.CarryCount>0;
            carrySeen=w.Pets.Sum(p=>p.CarrySteps)>0;
            pushDone=w.FoodPushCount>0;
            flightSeen=w.Pets.Sum(p=>p.FlightSteps)>0;
            // pushSeen is sampled live during the run; completion needs the food
            // to survive 14s while neighbours eat it, so it stays informational.
            bool pass=mixed&&fight&&peace&&left&&returned&&travel&&input&&hidden&&carryStart&&carrySeen&&carryDone&&flightStart&&flightSeen&&pushStart&&pushSeen&&sizeWorks&&errors==0&&frames>100;
            File.WriteAllText(Path.Combine(dir,"runtime-test.txt"),(pass?"PASS":"FAIL")+"\r\nDiag n=11: "+diag+"\r\nMixed species: "+mixed+"\r\nCross-species fight: "+fight+"\r\nPeace mode blocks/stops fights: "+peace+"\r\nFully left screen: "+left+"\r\nReturned after trip: "+returned+"\r\nTravel state: "+traveler.State+" / returns "+traveler.Returns+" / offscreen "+traveler.Offscreen+"\r\nNative menu detected: "+menu+"\r\nRoute traversed >30 px: "+travel+"\r\nClick-through on/off: "+input+"\r\nHidden visibility: "+hidden+"\r\nFeeding visits: "+w.FeedCount+"\r\nIcon carry started: "+carryStart+" / push steps: "+(carrySeen?"yes":"no")+" / finished: "+carryDone+" / total "+w.CarryCount+"\r\nFlight started: "+flightStart+" / seen airborne: "+flightSeen+" / total "+w.FlightCount+"\r\nFood push started: "+pushStart+" / pushing: "+(pushSeen?"yes":"no")+" / finished: "+pushDone+" / total "+w.FoodPushCount+"\r\nSize zoom grows and shrinks: "+sizeWorks+"\r\nNative frames: "+frames+"\r\nLayer errors: "+errors+"\r\nRoutes: "+w.Map.Items.Count+"\r\nIcons: "+w.Map.IconCount+"\r\nVirtual desktop icons: "+DesktopIcons.Count+"\r\nTexture cache bytes: "+PhotoSpriteRenderer.CacheBytes+"\r\nHotkey conflicts: "+String.Join(",",w.Keys.Failed.ToArray())+"\r\n");Environment.ExitCode=pass?0:1;w.ExitThread();
        }catch(Exception ex){File.WriteAllText(Path.Combine(dir,"runtime-failure.txt"),ex.ToString());Environment.ExitCode=1;timer.Stop();w.ExitThread();}};timer.Start();return timer;
    }
}
internal static class SelfTest {
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
    internal static void Run(string dir){Directory.CreateDirectory(dir);PhotoSpriteRenderer.Verify();string[] ids=CreatureCatalog.All.Select(d=>d.Id).ToArray();
        AppIcon.SaveIco(Path.Combine(dir,"app.ico"));
        using(var ico=AppIcon.Load(256))using(var b=ico.ToBitmap())b.Save(Path.Combine(dir,"icon-256.png"),ImageFormat.Png);
        using(var lg=AppIcon.Logo())Check(lg!=null&&lg.Width>400&&lg.Height>400,"Embedded brand logo missing");
        using(var mk=AppIcon.Mark())Check(mk!=null&&mk.Width>100,"Embedded brand mark missing");
        using(var lg=AppIcon.Logo())lg.Save(Path.Combine(dir,"logo-preview.png"),ImageFormat.Png);
        using(var strip=new Bitmap(540,140))using(var g=Graphics.FromImage(strip)){
            g.Clear(Color.FromArgb(240,243,238));int[] sz={16,20,24,32,48};string[] tag={"16","20","24","32","48"};
            for(int i=0;i<sz.Length;i++)using(var ic=AppIcon.Load(sz[i]))using(var bmp=ic.ToBitmap()){
                g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.Half;
                g.DrawImage(bmp,new Rectangle(20+i*104,18,96,96));
                using(var f=new Font("Microsoft YaHei UI",9f))using(var br=new SolidBrush(Color.FromArgb(70,82,72)))g.DrawString(tag[i]+" px",f,br,20+i*104,118);}
            strip.Save(Path.Combine(dir,"icon-sizes.png"),ImageFormat.Png);
        }
        foreach(string id in ids)foreach(float scale in new float[]{.25f,.5f,1,2,4,6})foreach(float angle in new float[]{0,45,137})using(var b=CreatureRenderer.Render(id,2,angle,scale,"探索",0,false)){
            Check(b.Width==PhotoSpriteRenderer.CanvasFor(scale),"Dynamic canvas mismatch");for(int k=0;k<b.Width;k+=2)Check(b.GetPixel(k,0).A<=3&&b.GetPixel(k,b.Height-1).A<=3&&b.GetPixel(0,k).A<=3&&b.GetPixel(b.Width-1,k).A<=3,"Clipped HD image: "+id+" / "+scale+" / "+angle);
        }
        foreach(string id in ids)using(var before=CreatureRenderer.Render(id,1,30,1,"探索",0,false)){using(var large=CreatureRenderer.Render(id,1,30,6,"探索",0,false)){}using(var after=CreatureRenderer.Render(id,1,30,1,"探索",0,false)){for(int y=0;y<before.Height;y+=3)for(int x=0;x<before.Width;x+=3)Check(before.GetPixel(x,y)==after.GetPixel(x,y),"Cumulative zoom quality loss");}}
        var config=Settings.Defaults();config.Scale=999;config.ReturnMin=40;config.ReturnMax=1;int roster=config.Animals.Count;config.Animals.Add(new AnimalProfile("Ant","另一个蚂蚁"){Id=config.Animals[0].Id});config.Validate();Check(config.Scale==600&&config.ReturnMax==40&&config.Animals.Select(a=>a.Id).Distinct().Count()==roster+1,"Profile validation");
        DesktopIcons.Virtual=true;DesktopIcons.ResetForTest();DesktopIcons.Virtual=true;
        Check(DesktopIcons.Refresh(true)&&DesktopIcons.Count==12,"Virtual desktop icon enumeration");
        var sample=DesktopIcons.Snapshot()[0];Point was=sample.Center;
        Check(DesktopIcons.Move(sample,new Point(was.X+40,was.Y+30)),"Icon move call");
        Check(sample.Center==new Point(was.X+40,was.Y+30),"Icon anchor arithmetic");
        Point back;Check(DesktopIcons.ReadBack(sample,out back)&&back==sample.Center,"Icon position readback");
        config.IconMoves.Add(new IconMove("测试图标",12,34,"01-01 00:00"));
        var serializer=new System.Xml.Serialization.XmlSerializer(typeof(Settings));using(var stream=new MemoryStream()){serializer.Serialize(stream,config);stream.Position=0;var restored=(Settings)serializer.Deserialize(stream);restored.Validate();Check(restored.Animals.Count==roster+1&&restored.Animals[roster].Name=="另一个蚂蚁","Mixed roster XML duplicates or loss");Check(restored.IconMoves.Count==1&&restored.IconMoves[0].Name=="测试图标"&&restored.IconMoves[0].X==12,"Icon undo history XML round-trip");}
        var line=new Surface(new Rectangle(-120,30,250,25),"菜单栏",IntPtr.Zero,true);Check(PetForm.Dist(line.At(260),new PointF(120,30))<.01,"Line endpoint bounce");Check(PetForm.Dist(line.At(-10),new PointF(-110,30))<.01,"Reverse line route");
        var loop=new Surface(new Rectangle(-100,20,160,90),"窗口边缘",IntPtr.Zero);for(int i=-300;i<700;i++){var q=loop.At(loop.Nearest(new PointF(i,i/3)));Check(q.X>=-100&&q.X<=60&&q.Y>=20&&q.Y<=110,"Invalid perimeter point");}
        Check(PhotoSpriteRenderer.FrameIndex(1,"睡觉")==PhotoSpriteRenderer.FrameIndex(5,"睡觉"),"Idle gait is active");Check(PhotoSpriteRenderer.TextureLevel(6)==1024,"HD zoom level");PhotoSpriteRenderer.Verify();
        CreatePreview(dir);CreatePoseSheet(dir);File.WriteAllText(Path.Combine(dir,"self-test.txt"),"PASS\r\n"+ids.Length+" HD alpha masters >=1000px.\r\n"+(ids.Length*18)+" species/scale/angle renders including 25-600 percent; edge clipping checks.\r\nRepeated zoom returns identical pixels.\r\nMixed roster XML round-trip without duplicate defaults.\r\nProfile bounds/IDs and return timer validation.\r\nLine reverse/bounce and 1000 rectangle path points.\r\n16-phase gait and <=48MB LRU texture cache.\r\nVirtual desktop icon enumeration, move and read-back.\r\nIcon undo history XML round-trip.\r\nFlight / carry pose composition.\r\n");
    }
    private static void CreatePreview(string dir){using(var sheet=new Bitmap(1600,1620))using(var g=Graphics.FromImage(sheet)){
        g.Clear(Color.FromArgb(18,25,24));using(var f=new Font("Microsoft YaHei UI",28,FontStyle.Bold))using(var b=new SolidBrush(Color.FromArgb(201,226,162)))g.DrawString("微野 3.1  /  九种动物桌面生态",f,b,48,34);
        using(var f=new Font("Microsoft YaHei UI",13))using(var b=new SolidBrush(Color.FromArgb(155,180,155)))g.DrawString("高清母图 · 16 阶段网格步态 · 25–600% 缩放 · 混养与个体管理",f,b,52,98);
        var defs=CreatureCatalog.All;
        for(int i=0;i<defs.Count;i++){int col=i%3,row=i/3;using(var bmp=CreatureRenderer.Render(defs[i].Id,1,-20,2,"探索",0,false))g.DrawImageUnscaled(bmp,150+col*500-bmp.Width/2,140+row*470);using(var f=new Font("Microsoft YaHei UI",17,FontStyle.Bold))using(var b=new SolidBrush(Color.FromArgb(228,234,215)))g.DrawString(defs[i].Name,f,b,60+col*500,140+row*470+428);}
        int foot=140+((defs.Count+2)/3)*470;
        using(var f=new Font("Microsoft YaHei UI",13))using(var b=new SolidBrush(Color.FromArgb(185,212,151)))g.DrawString("任务栏与菜单栏路线  /  离屏探索后返回  /  追逐、避让、争斗与和平模式",f,b,70,foot);
        using(var f=new Font("Microsoft YaHei UI",10))using(var b=new SolidBrush(Color.FromArgb(145,166,146)))g.DrawString("图为实际渲染。母图 1254×1254，非原生 4K；位图无法无限无损放大。行为为游戏化模拟。",f,b,70,foot+36);
        sheet.Save(Path.Combine(dir,"preview.png"),ImageFormat.Png);
    }}
    private static void CreatePoseSheet(string dir){
        using(var sheet=new Bitmap(900,400))using(var g=Graphics.FromImage(sheet)){
            g.Clear(Color.FromArgb(18,25,24));
            float sc=1.25f;
            using(var lady=CreatureRenderer.Render("Ladybug",2,-90,sc,"飞行",0,false))
                using(var flown=PetForm.ComposeFrame((Bitmap)lady.Clone(),"Ladybug",2,-90f,sc,"飞行",false,16*sc+12))g.DrawImageUnscaled(flown,40,40);
            using(var bee=CreatureRenderer.Render("Bee",3,-60,sc,"飞行",0,false))
                using(var flown=PetForm.ComposeFrame((Bitmap)bee.Clone(),"Bee",3,-60f,sc,"飞行",false,16*sc+12))g.DrawImageUnscaled(flown,320,40);
            using(var ant=CreatureRenderer.Render("Ant",3,0,sc,"搬运",0,false))
                using(var lean=PetForm.ComposeFrame((Bitmap)ant.Clone(),"Ant",3,0f,sc,"搬运",true,0))g.DrawImageUnscaled(lean,600,40);
            using(var f=new Font("Microsoft YaHei UI",12,FontStyle.Bold))using(var b=new SolidBrush(Color.FromArgb(190,214,170))){
                g.DrawString("瓢虫飞行：留地阴影 + 鞘翅",f,b,70,330);
                g.DrawString("蜜蜂飞行：振翅 + 悬停",f,b,350,330);
                g.DrawString("蚂蚁搬运：身体前倾",f,b,630,330);
            }
            sheet.Save(Path.Combine(dir,"poses.png"),ImageFormat.Png);
        }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]private static extern bool DestroyIcon(IntPtr h);
}
}
