using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Automation;
using System.Threading;
namespace DesktopCritters {
internal sealed class Surface {
    internal Rectangle Bounds;
    internal string Kind;
    internal IntPtr Handle;
    internal bool Line,ClassicMenu;
    private Rectangle offset;
    internal Surface(Rectangle r,string kind,IntPtr h):this(r,kind,h,false){}
    internal Surface(Rectangle r,string kind,IntPtr h,bool line){Bounds=r;Kind=kind;Handle=h;Line=line;Native.RECT owner;if(h!=IntPtr.Zero&&Native.GetWindowRect(h,out owner))offset=new Rectangle(r.X-owner.Left,r.Y-owner.Top,r.Width,r.Height);}
    internal double Perimeter{get{return Line?Math.Max(1,Bounds.Width*2):Math.Max(1,2.0*(Bounds.Width+Bounds.Height));}}
    internal PointF At(double d){d=(d%Perimeter+Perimeter)%Perimeter;if(Line)return new PointF(Bounds.Left+(float)(d<=Bounds.Width?d:2*Bounds.Width-d),Bounds.Top);if(d<Bounds.Width)return new PointF(Bounds.Left+(float)d,Bounds.Top);d-=Bounds.Width;if(d<Bounds.Height)return new PointF(Bounds.Right,Bounds.Top+(float)d);d-=Bounds.Height;if(d<Bounds.Width)return new PointF(Bounds.Right-(float)d,Bounds.Bottom);d-=Bounds.Width;return new PointF(Bounds.Left,Bounds.Bottom-(float)d);}
    internal double Nearest(PointF p){if(Line)return Math.Max(0,Math.Min(Bounds.Width,p.X-Bounds.Left));double best=Double.MaxValue,at=0;double[] ds={Math.Max(0,Math.Min(Bounds.Width,p.X-Bounds.Left)),Bounds.Width+Math.Max(0,Math.Min(Bounds.Height,p.Y-Bounds.Top)),Bounds.Width+Bounds.Height+Math.Max(0,Math.Min(Bounds.Width,Bounds.Right-p.X)),2*Bounds.Width+Bounds.Height+Math.Max(0,Math.Min(Bounds.Height,Bounds.Bottom-p.Y))};foreach(double d in ds){PointF q=At(d);double dd=(q.X-p.X)*(q.X-p.X)+(q.Y-p.Y)*(q.Y-p.Y);if(dd<best){best=dd;at=d;}}return at;}
    internal bool Update(){if(Handle==IntPtr.Zero)return true;if(!Native.IsWindow(Handle)||!Native.IsWindowVisible(Handle)||Native.IsIconic(Handle)||Native.Cloaked(Handle))return false;Native.RECT r;if(!Native.GetWindowRect(Handle,out r))return false;
        if(ClassicMenu){Rectangle b;if(!Native.MenuBounds(Handle,out b))return false;Bounds=b;}
        else if(Kind=="窗口边缘"||Kind=="任务栏"||Kind=="展开菜单")Bounds=r.Rect;
        else if(Kind!="桌面图标")Bounds=new Rectangle(r.Left+offset.X,r.Top+offset.Y,offset.Width,offset.Height);
        return true;
    }
}
internal sealed class SurfaceMap {
    internal List<Surface> Items=new List<Surface>();
    private readonly object gate=new object();private List<Surface> accessible=new List<Surface>();private volatile bool scanning;private double lastScan;private bool lastIcons,lastMenus;
    internal int IconCount{get{int n=0;lock(gate)foreach(var s in accessible)if(s.Kind=="桌面图标")n++;return n;}}
    internal void Refresh(Settings s){
        bool forceScan=s.ClimbIcons!=lastIcons||s.ClimbMenus!=lastMenus;lastIcons=s.ClimbIcons;lastMenus=s.ClimbMenus;
        var list=new List<Surface>();var candidates=new List<IntPtr>();uint own=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        Native.EnumWindows(delegate(IntPtr h,IntPtr unused){uint pid;Native.GetWindowThreadProcessId(h,out pid);if(pid==own||!Native.IsWindowVisible(h)||Native.IsIconic(h)||Native.Cloaked(h)||Native.IsHungAppWindow(h))return true;
            string cls=Native.ClassName(h);if(cls=="Progman"||cls=="WorkerW")return true;Native.RECT rr;if(!Native.GetWindowRect(h,out rr))return true;Rectangle r=rr.Rect;if(r.Width<30||r.Height<15)return true;
            if(cls=="Shell_TrayWnd"||cls=="Shell_SecondaryTrayWnd"){if(s.ClimbTaskbar)list.Add(new Surface(r,"任务栏",h));return true;}
            if(cls=="#32768"){if(s.ClimbMenus)list.Add(new Surface(r,"展开菜单",h));return true;}
            if(r.Width<100||r.Height<40||list.Count>70)return true;
            if(s.ClimbWindows)list.Add(new Surface(r,"窗口边缘",h));
            if(s.ClimbMenus){Rectangle menu;if(Native.MenuBounds(h,out menu)){var surface=new Surface(menu,"菜单栏",h,true);surface.ClassicMenu=true;list.Add(surface);}if(candidates.Count<3)candidates.Add(h);}
            return true;
        },IntPtr.Zero);
        lock(gate)foreach(var a in accessible){if((a.Kind=="桌面图标"&&!s.ClimbIcons)||(a.Kind!="桌面图标"&&!s.ClimbMenus))continue;if(a.Update())list.Add(a);}
        foreach(Screen screen in Screen.AllScreens)if(s.AllScreens||screen.Primary){
            if(s.ClimbTaskbar&&!HasTaskbar(list,screen)){Rectangle b=screen.Bounds,w=screen.WorkingArea;if(w.Bottom<b.Bottom)list.Add(new Surface(Rectangle.FromLTRB(b.Left,w.Bottom,b.Right,b.Bottom),"任务栏",IntPtr.Zero));else if(w.Top>b.Top)list.Add(new Surface(Rectangle.FromLTRB(b.Left,b.Top,b.Right,w.Top),"任务栏",IntPtr.Zero));else if(w.Left>b.Left)list.Add(new Surface(Rectangle.FromLTRB(b.Left,b.Top,w.Left,b.Bottom),"任务栏",IntPtr.Zero));else if(w.Right<b.Right)list.Add(new Surface(Rectangle.FromLTRB(w.Right,b.Top,b.Right,b.Bottom),"任务栏",IntPtr.Zero));}
            list.Add(new Surface(screen.WorkingArea,"屏幕边缘",IntPtr.Zero));
        }
        Items=list;if(s.ClimbIcons||s.ClimbMenus)ScanAccessibility(s.ClimbIcons,s.ClimbMenus,candidates,forceScan);
    }
    private bool HasTaskbar(List<Surface> list,Screen screen){foreach(var r in list)if(r.Kind=="任务栏"&&r.Bounds.IntersectsWith(screen.Bounds))return true;return false;}
    private void ScanAccessibility(bool icons,bool menus,List<IntPtr> windows,bool force){int now=Environment.TickCount;if(scanning)return;if(!force&&now-lastScan<8000)return;lastScan=now;var thread=new Thread(delegate(){var result=new List<Surface>();try{
        if(icons){IntPtr view=IntPtr.Zero;Native.EnumWindows(delegate(IntPtr h,IntPtr p){IntPtr d=Native.FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null);if(d!=IntPtr.Zero)view=Native.FindWindowEx(d,IntPtr.Zero,"SysListView32",null);return view==IntPtr.Zero;},IntPtr.Zero);
            if(view!=IntPtr.Zero){var els=AutomationElement.FromHandle(view).FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem));for(int i=0;i<els.Count&&i<300;i++){var b=els[i].Current.BoundingRectangle;if(!b.IsEmpty&&b.Width>1&&b.Height>1)result.Add(new Surface(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height),"桌面图标",view));}}}
        if(menus)foreach(IntPtr h in windows){try{var queue=new Queue<Tuple<AutomationElement,int>>();queue.Enqueue(Tuple.Create(AutomationElement.FromHandle(h),0));int visited=0;
            while(queue.Count>0&&visited++<60){var node=queue.Dequeue();var children=node.Item1.FindAll(TreeScope.Children,Condition.TrueCondition);for(int i=0;i<children.Count&&i<25;i++){var e=children[i];var current=e.Current;if(current.IsOffscreen)continue;bool menu=current.ControlType==ControlType.MenuBar,bar=current.ControlType==ControlType.ToolBar;if(menu||bar){var b=current.BoundingRectangle;if(!b.IsEmpty&&b.Width>80&&b.Height>5&&b.Height<180)result.Add(new Surface(new Rectangle((int)b.X,(int)b.Y,(int)b.Width,(int)b.Height),menu?"菜单栏":"工具栏",h,true));}else if(node.Item2<2&&(current.ControlType==ControlType.Pane||current.ControlType==ControlType.Group))queue.Enqueue(Tuple.Create(e,node.Item2+1));}}
        }catch{}}
        lock(gate)accessible=result;
    }catch{}finally{scanning=false;}});thread.IsBackground=true;thread.SetApartmentState(ApartmentState.MTA);thread.Start();}
}
}
