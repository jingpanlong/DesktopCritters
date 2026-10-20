using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace DesktopCritters {
// One icon on the desktop. Positions are in screen pixels; Offset is the vector
// from the item's top-left to the centre of the icon bitmap (the label sits
// below the bitmap), so an anchor point can be converted back into a position.
internal sealed class DeskIcon {
    internal int Index;
    internal string Name="";
    internal Point TopLeft;
    internal Point Offset=new Point(38,34);
    internal int Glyph=76;
    internal int Cell=76;
    internal Point Center{get{return new Point(TopLeft.X+Offset.X,TopLeft.Y+Offset.Y);}}
    internal Rectangle Bounds{get{return new Rectangle(Center.X-Glyph/2,Center.Y-Glyph/2,Glyph,Glyph);}}
}

// Real desktop icon access. The shell draws the desktop in a SysListView32 that
// lives inside explorer.exe, so both reading and writing a position has to go
// through that process: allocate a scratch buffer with VirtualAllocEx, point the
// list view at it with a window message, then read the result back.
// Everything is guarded by try/catch and by a short SendMessageTimeout so a busy
// or hostile shell can never freeze the desktop pet.
internal static class DesktopIcons {
    private const int LVM_FIRST=0x1000;
    private const int LVM_GETITEMCOUNT=LVM_FIRST+4;
    private const int LVM_GETITEMRECT=LVM_FIRST+14;
    private const int LVM_SETITEMPOSITION=LVM_FIRST+15;
    private const int LVM_GETITEMPOSITION=LVM_FIRST+16;
    private const int LVM_GETITEMTEXTW=LVM_FIRST+115;
    private const int LVIR_ICON=1;
    private const int Access=0x0008|0x0010|0x0020|0x0400;      // VM_OPERATION|VM_READ|VM_WRITE|QUERY_INFORMATION
    private const uint MemCommit=0x1000,MemReserve=0x2000,MemRelease=0x8000,PageReadWrite=0x04;
    private const uint SmtoBlock=0x0001,SmtoAbortIfHung=0x0002;

    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    private struct LVITEMW {
        public uint mask;public int iItem;public int iSubItem;public uint state;public uint stateMask;
        public IntPtr pszText;public int cchTextMax;public int iImage;public IntPtr lParam;
        public int iIndent;public int iGroupId;public uint cColumns;public IntPtr puColumns;public IntPtr piColFmt;public int iGroup;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    private static extern IntPtr SendMessageTimeout(IntPtr h,int msg,IntPtr w,IntPtr l,uint flags,uint timeout,out IntPtr result);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr h,ref Native.POINT p);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(int access,bool inherit,int pid);
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAllocEx(IntPtr p,IntPtr addr,IntPtr size,uint type,uint protect);
    [DllImport("kernel32.dll")] private static extern bool VirtualFreeEx(IntPtr p,IntPtr addr,IntPtr size,uint type);
    [DllImport("kernel32.dll")] private static extern bool ReadProcessMemory(IntPtr p,IntPtr addr,byte[] buffer,int size,out IntPtr read);
    [DllImport("kernel32.dll")] private static extern bool WriteProcessMemory(IntPtr p,IntPtr addr,byte[] buffer,int size,out IntPtr written);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);

    internal static bool Enabled=true;
    // Test/simulation mode: icons live in memory so suites can exercise the whole
    // carry loop without touching the real desktop.
    internal static bool Virtual;
    internal static string LastError="";
    private static readonly object Gate=new object();
    private static readonly List<DeskIcon> Cache=new List<DeskIcon>();
    private static IntPtr view=IntPtr.Zero;
    private static Point viewOrigin=Point.Empty;
    private static double lastScan=-1;
    private static int failures;

    internal static int Count{get{lock(Gate)return Cache.Count;}}
    internal static bool Available{get{return Enabled&&failures<3;}}
    // Cheap liveness/quantity probe for the panel: two messages, cached for 10s,
    // so the UI thread never pays for a full enumeration.
    internal static int ProbeCount{
        get{
            if(Virtual)return 12;
            double now=Environment.TickCount/1000.0;
            if(now-probeAt<10)return probeCount;
            probeAt=now;
            lock(Gate){
                IntPtr v=FindView();
                if(v==IntPtr.Zero){Fail("未找到桌面图标层");probeCount=0;return 0;}
                uint pid;Native.GetWindowThreadProcessId(v,out pid);
                IntPtr proc=OpenProcess(Access,false,(int)pid);
                if(proc==IntPtr.Zero){Fail("没有权限访问桌面进程");probeCount=0;return 0;}
                try{int n=(int)Send(v,LVM_GETITEMCOUNT,IntPtr.Zero,IntPtr.Zero);probeCount=n<0?0:n;if(probeCount>0)failures=0;}
                catch{probeCount=0;}
                finally{CloseHandle(proc);}
                return probeCount;
            }
        }
    }
    private static int probeCount;private static double probeAt=-1;
    internal static List<DeskIcon> Snapshot(){lock(Gate){var copy=new List<DeskIcon>(Cache.Count);foreach(var i in Cache)copy.Add(i);return copy;}}

    internal static bool Refresh(bool force){
        if(!Enabled)return false;
        if(Virtual)return BuildVirtual();
        lock(Gate){
            double now=Environment.TickCount/1000.0;
            if(!force&&Cache.Count>0&&now-lastScan<4)return true;
            lastScan=now;
            IntPtr v=FindView();
            if(v==IntPtr.Zero){Fail("未找到桌面图标层（可能是第三方桌面或开始菜单替换程序）");return false;}
            uint pid;Native.GetWindowThreadProcessId(v,out pid);
            IntPtr proc=OpenProcess(Access,false,(int)pid);
            if(proc==IntPtr.Zero){Fail("没有权限读取桌面进程，可能被安全软件拦截");return false;}
            IntPtr remote=IntPtr.Zero;
            try{
                int count=(int)Send(v,LVM_GETITEMCOUNT,IntPtr.Zero,IntPtr.Zero);
                if(count<0){Fail("桌面图标层没有响应");return false;}
                if(count==0){Fail("桌面上没有图标");return false;}
                if(count>512)count=512;
                remote=VirtualAllocEx(proc,IntPtr.Zero,new IntPtr(count*8+4096),MemCommit|MemReserve,PageReadWrite);
                if(remote==IntPtr.Zero){Fail("无法在桌面进程里分配临时内存");return false;}
                var origin=new Native.POINT(0,0);
                ClientToScreen(v,ref origin);
                view=v;viewOrigin=new Point(origin.X,origin.Y);
                Point offset;int glyph,cell;
                Measure(v,proc,remote,out offset,out glyph,out cell);
                var list=new List<DeskIcon>(count);
                for(int i=0;i<count;i++){
                    Point p;
                    if(!ReadPosition(v,proc,i,remote,out p))continue;
                    list.Add(new DeskIcon{Index=i,Name=ReadName(v,proc,i,remote),TopLeft=new Point(p.X+viewOrigin.X,p.Y+viewOrigin.Y),Offset=offset,Glyph=glyph,Cell=cell});
                }
                if(list.Count==0){Fail("没能读到任何图标位置");return false;}
                Cache.Clear();Cache.AddRange(list);failures=0;LastError="";
                return true;
            }catch(Exception ex){Fail(ex.Message);return false;}
            finally{if(remote!=IntPtr.Zero)VirtualFreeEx(proc,remote,IntPtr.Zero,MemRelease);CloseHandle(proc);}
        }
    }

    // Reads where the icon actually is right now; used to notice that the shell
    // refused our move (auto-arrange on) instead of dragging a stuck icon forever.
    internal static bool ReadBack(DeskIcon icon,out Point center){
        center=icon.Center;
        if(icon==null)return false;
        if(Virtual){center=icon.Center;return true;}
        lock(Gate){
            if(view==IntPtr.Zero)return false;
            uint pid;Native.GetWindowThreadProcessId(view,out pid);
            IntPtr proc=OpenProcess(Access,false,(int)pid);
            if(proc==IntPtr.Zero)return false;
            IntPtr remote=IntPtr.Zero;
            try{
                remote=VirtualAllocEx(proc,IntPtr.Zero,new IntPtr(1024),MemCommit|MemReserve,PageReadWrite);
                if(remote==IntPtr.Zero)return false;
                Point p;
                if(!ReadPosition(view,proc,icon.Index,remote,out p))return false;
                center=new Point(p.X+viewOrigin.X+icon.Offset.X,p.Y+viewOrigin.Y+icon.Offset.Y);
                return true;
            }catch{return false;}
            finally{if(remote!=IntPtr.Zero)VirtualFreeEx(proc,remote,IntPtr.Zero,MemRelease);CloseHandle(proc);}
        }
    }

    internal static bool Move(DeskIcon icon,Point center){
        if(icon==null)return false;
        int left=center.X-icon.Offset.X,top=center.Y-icon.Offset.Y;
        icon.TopLeft=new Point(left,top);
        if(Virtual)return true;
        lock(Gate){
            if(view==IntPtr.Zero)return false;
            uint pid;Native.GetWindowThreadProcessId(view,out pid);
            IntPtr proc=OpenProcess(Access,false,(int)pid);
            if(proc==IntPtr.Zero){LastError="没有权限移动桌面图标";return false;}
            try{
                int x=Clamp16(left-viewOrigin.X),y=Clamp16(top-viewOrigin.Y);
                long r=Send(view,LVM_SETITEMPOSITION,new IntPtr(icon.Index),new IntPtr(unchecked((int)((y<<16)|(x&0xFFFF)))));
                if(r<0){LastError="桌面图标层没有响应";return false;}
                return true;
            }catch(Exception ex){LastError=ex.Message;return false;}
            finally{CloseHandle(proc);}
        }
    }

    internal static void Fail(string reason){LastError=reason;failures++;if(failures>=3)Enabled=false;}

    private static int Clamp16(int v){return v<-32000?-32000:(v>32000?32000:v);}
    private static long Send(IntPtr h,int msg,IntPtr w,IntPtr l){
        IntPtr result;
        if(SendMessageTimeout(h,msg,w,l,SmtoBlock|SmtoAbortIfHung,350,out result)==IntPtr.Zero)return -1;
        return result.ToInt64();
    }
    private static bool Read(IntPtr proc,IntPtr addr,byte[] buffer,int size){
        IntPtr got;return ReadProcessMemory(proc,addr,buffer,size,out got)&&got.ToInt32()>=size;
    }
    private static bool Write(IntPtr proc,IntPtr addr,byte[] buffer,int size){
        IntPtr put;return WriteProcessMemory(proc,addr,buffer,size,out put);
    }
    private static bool ReadPosition(IntPtr v,IntPtr proc,int index,IntPtr remote,out Point p){
        p=Point.Empty;
        if(Send(v,LVM_GETITEMPOSITION,new IntPtr(index),remote)<0)return false;
        var buffer=new byte[8];
        if(!Read(proc,remote,buffer,8))return false;
        p=new Point(BitConverter.ToInt32(buffer,0),BitConverter.ToInt32(buffer,4));
        return true;
    }
    private static void Measure(IntPtr v,IntPtr proc,IntPtr remote,out Point offset,out int glyph,out int cell){
        offset=new Point(38,34);glyph=76;cell=76;
        var seed=new byte[16];BitConverter.GetBytes(LVIR_ICON).CopyTo(seed,0);
        if(!Write(proc,remote,seed,16))return;
        if(Send(v,LVM_GETITEMRECT,IntPtr.Zero,remote)<0)return;
        var rect=new byte[16];
        if(!Read(proc,remote,rect,16))return;
        int l=BitConverter.ToInt32(rect,0),t=BitConverter.ToInt32(rect,4),r=BitConverter.ToInt32(rect,8),b=BitConverter.ToInt32(rect,12);
        if(r<=l||b<=t)return;
        glyph=Math.Max(16,r-l);cell=Math.Max(48,b-t);
        Point p;
        if(ReadPosition(v,proc,0,remote,out p))offset=new Point(l-p.X+glyph/2,t-p.Y+glyph/2);
    }
    private static string ReadName(IntPtr v,IntPtr proc,int index,IntPtr remote){
        const int itemSize=88,chars=260;
        try{
            var item=new LVITEMW{iSubItem=0,cchTextMax=chars,pszText=new IntPtr(remote.ToInt64()+itemSize)};
            int size=Marshal.SizeOf(typeof(LVITEMW));
            IntPtr local=Marshal.AllocHGlobal(size);
            try{
                Marshal.StructureToPtr(item,local,false);
                var buffer=new byte[size];
                Marshal.Copy(local,buffer,0,size);
                if(!Write(proc,remote,buffer,size))return "";
            }finally{Marshal.FreeHGlobal(local);}
            if(Send(v,LVM_GETITEMTEXTW,new IntPtr(index),remote)<0)return "";
            var text=new byte[chars*2];
            if(!Read(proc,new IntPtr(remote.ToInt64()+itemSize),text,text.Length))return "";
            string s=Encoding.Unicode.GetString(text);
            int end=s.IndexOf('\0');
            return end>=0?s.Substring(0,end):s;
        }catch{return "";}
    }
    private static IntPtr FindView(){
        IntPtr found=IntPtr.Zero;
        Native.EnumWindows(delegate(IntPtr h,IntPtr unused){
            IntPtr def=Native.FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null);
            if(def==IntPtr.Zero)return true;
            IntPtr list=Native.FindWindowEx(def,IntPtr.Zero,"SysListView32",null);
            if(list==IntPtr.Zero)return true;
            found=list;return false;
        },IntPtr.Zero);
        return found;
    }

    // Deterministic 4x3 icon grid for the automated suites.
    private static bool BuildVirtual(){
        lock(Gate){
            if(Cache.Count>0)return true;
            Rectangle area=Screen.PrimaryScreen.WorkingArea;
            for(int i=0;i<12;i++){
                int x=area.Left+40+(i%4)*110,y=area.Top+40+(i/4)*120;
                Cache.Add(new DeskIcon{Index=i,Name="模拟图标 "+(i+1),TopLeft=new Point(x,y),Offset=new Point(38,34),Glyph=76,Cell=76});
            }
            return true;
        }
    }
    internal static void ResetForTest(){lock(Gate){Cache.Clear();view=IntPtr.Zero;lastScan=-1;failures=0;Enabled=true;LastError="";}}
}
}
