using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Windows.Forms;

namespace DesktopCritters {
internal static class Native {
    [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X,Y; public POINT(int x,int y){X=x;Y=y;} }
    [StructLayout(LayoutKind.Sequential)] internal struct SIZE { public int X,Y; public SIZE(int x,int y){X=x;Y=y;} }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left,Top,Right,Bottom; public Rectangle Rect {get{return Rectangle.FromLTRB(Left,Top,Right,Bottom);}} }
    [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct BLEND { public byte Op,Flags,Alpha,Format; }
    internal delegate bool EnumProc(IntPtr h,IntPtr p);
    internal delegate IntPtr HookProc(int code,IntPtr w,IntPtr l);
    [StructLayout(LayoutKind.Sequential)] internal struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData,flags,time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll",SetLastError=true)] internal static extern IntPtr SetWindowsHookEx(int hook,HookProc callback,IntPtr mod,uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr w,IntPtr l);
    [DllImport("kernel32.dll")] internal static extern IntPtr GetModuleHandle(string name);
    [StructLayout(LayoutKind.Sequential)] internal struct MENUBARINFO {public uint Size;public RECT Bar;public IntPtr Menu,MenuWindow;public uint Flags;}
    [DllImport("user32.dll")] internal static extern bool GetMenuBarInfo(IntPtr h,int obj,int item,ref MENUBARINFO info);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool IsHungAppWindow(IntPtr h);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr h,int attribute,out int value,int size);
    internal static bool Cloaked(IntPtr h){try{int v;return DwmGetWindowAttribute(h,14,out v,4)==0&&v!=0;}catch{return false;}}
    internal static bool MenuBounds(IntPtr h,out Rectangle bounds){var info=new MENUBARINFO{Size=(uint)Marshal.SizeOf(typeof(MENUBARINFO))};if(GetMenuBarInfo(h,-3,0,ref info)&&info.Bar.Right>info.Bar.Left&&info.Bar.Bottom>info.Bar.Top){bounds=info.Bar.Rect;return true;}bounds=Rectangle.Empty;return false;}
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc callback,IntPtr p);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr h,out RECT r);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr h,System.Text.StringBuilder name,int count);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
    [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr h,IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dst,ref POINT pos,ref SIZE size,IntPtr src,ref POINT origin,int color,ref BLEND blend,int flags);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] internal static extern IntPtr GetTopWindow(IntPtr h);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr h,uint cmd);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr h,uint flags);
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(POINT p);
    internal static readonly IntPtr HwndBottom=new IntPtr(1);
    internal static void PlaceAbove(IntPtr win,IntPtr host){if(win==IntPtr.Zero||!IsWindow(win))return;SetWindowPos(win,host==IntPtr.Zero?HwndBottom:host,0,0,0,0,0x0001|0x0002|0x0010);}
    internal static bool Bindable(IntPtr h){if(h==IntPtr.Zero||!IsWindow(h)||!IsWindowVisible(h)||IsIconic(h)||Cloaked(h))return false;string c=ClassName(h);return c.Length>0&&c[0]!='#'&&c!="SysShadow"&&c!="ToolTip"&&c!="NotifyIconOverflowWindow";}
    // Topmost foreign window that contains the point (used when a pet is dropped
    // onto a page; skips our own layered windows).
    internal static IntPtr WindowAt(Point p){uint own=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;for(IntPtr h=GetTopWindow(IntPtr.Zero);h!=IntPtr.Zero;h=GetWindow(h,2)){uint pid;GetWindowThreadProcessId(h,out pid);if(pid==own)continue;if(!Bindable(h))continue;RECT r;if(!GetWindowRect(h,out r))continue;var b=r.Rect;if(b.Width<40||b.Height<30)continue;if(b.Contains(p))return h;}return IntPtr.Zero;}
    // First foreign window below the given window in z-order that contains the
    // point. Pets therefore never pop above a window that is already in front.
    internal static IntPtr WindowBelow(IntPtr pet,Point p){uint own=(uint)System.Diagnostics.Process.GetCurrentProcess().Id;bool passed=false;for(IntPtr h=GetTopWindow(IntPtr.Zero);h!=IntPtr.Zero;h=GetWindow(h,2)){if(h==pet){passed=true;continue;}if(!passed)continue;uint pid;GetWindowThreadProcessId(h,out pid);if(pid==own)continue;if(!Bindable(h))continue;RECT r;if(!GetWindowRect(h,out r))continue;var b=r.Rect;if(b.Width<40||b.Height<30)continue;if(b.Contains(p))return h;}return IntPtr.Zero;}
    internal static IntPtr Root(IntPtr h){IntPtr r=GetAncestor(h,2);return r==IntPtr.Zero?h:r;}
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] internal static extern int GetWindowLong(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")] internal static extern int SetWindowLong(IntPtr h,int index,int value);
    [DllImport("winmm.dll",EntryPoint="timeBeginPeriod")] private static extern int _timeBeginPeriod(int ms);
    [DllImport("winmm.dll",EntryPoint="timeEndPeriod")] private static extern int _timeEndPeriod(int ms);
    internal static void TimeBeginPeriod(int ms){try{_timeBeginPeriod(ms);}catch{}}
    internal static void TimeEndPeriod(int ms){try{_timeEndPeriod(ms);}catch{}}
    internal static string ClassName(IntPtr h){var s=new System.Text.StringBuilder(256);GetClassName(h,s,256);return s.ToString();}
    internal static bool IsFullScreen(){
        IntPtr h=GetForegroundWindow(); if(h==IntPtr.Zero)return false;
        string c=ClassName(h);if(c=="Progman"||c=="WorkerW"||c=="Shell_TrayWnd")return false;
        RECT r;if(!GetWindowRect(h,out r))return false;
        Rectangle b=Screen.FromHandle(h).Bounds;
        return r.Left<=b.Left&&r.Top<=b.Top&&r.Right>=b.Right&&r.Bottom>=b.Bottom;
    }
}
}
