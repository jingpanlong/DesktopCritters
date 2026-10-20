using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopCritters {
// A small design system so every surface (panel, menus, installer) shares the
// same palette, radii and typography instead of mixing default WinForms grey.
internal static class Ui {
    internal static bool Light=true;
    internal static Color Shell=Color.FromArgb(245,247,243);
    internal static Color Side=Color.FromArgb(236,240,233);
    internal static Color Card=Color.FromArgb(255,255,255);
    internal static Color CardHi=Color.FromArgb(233,240,226);
    internal static Color Line=Color.FromArgb(221,228,216);
    internal static Color Ink=Color.FromArgb(31,38,32);
    internal static Color Muted=Color.FromArgb(108,123,111);
    internal static Color Accent=Color.FromArgb(95,139,59);
    internal static Color OnAccent=Color.FromArgb(255,255,255);
    internal static Color Warm=Color.FromArgb(184,121,42);
    internal static Color Danger=Color.FromArgb(192,86,74);
    internal static Color Track=Color.FromArgb(223,230,218);
    internal static Color KnobFill=Color.FromArgb(255,255,255);
    internal static Color Soft=Color.FromArgb(241,244,238);
    internal static Color SoftHi=Color.FromArgb(231,237,225);
    internal static Color DangerSoft=Color.FromArgb(251,237,234);
    internal static Color DangerSoftHi=Color.FromArgb(246,223,218);
    internal static Color Popup=Color.FromArgb(255,255,255);
    internal static Color PopupHot=Color.FromArgb(231,239,222);

    internal static void Apply(bool light){
        Light=light;
        if(light){
            Shell=Color.FromArgb(245,247,243);Side=Color.FromArgb(236,240,233);Card=Color.FromArgb(255,255,255);
            CardHi=Color.FromArgb(233,240,226);Line=Color.FromArgb(221,228,216);Ink=Color.FromArgb(31,38,32);
            Muted=Color.FromArgb(108,123,111);Accent=Color.FromArgb(95,139,59);OnAccent=Color.FromArgb(255,255,255);
            Warm=Color.FromArgb(184,121,42);Danger=Color.FromArgb(192,86,74);Track=Color.FromArgb(223,230,218);
            KnobFill=Color.FromArgb(255,255,255);Soft=Color.FromArgb(241,244,238);SoftHi=Color.FromArgb(231,237,225);
            DangerSoft=Color.FromArgb(251,237,234);DangerSoftHi=Color.FromArgb(246,223,218);
            Popup=Color.FromArgb(255,255,255);PopupHot=Color.FromArgb(231,239,222);
        }else{
            Shell=Color.FromArgb(14,17,19);Side=Color.FromArgb(21,27,30);Card=Color.FromArgb(28,36,40);
            CardHi=Color.FromArgb(37,47,52);Line=Color.FromArgb(47,60,65);Ink=Color.FromArgb(233,238,229);
            Muted=Color.FromArgb(149,168,158);Accent=Color.FromArgb(184,216,140);OnAccent=Color.FromArgb(22,30,16);
            Warm=Color.FromArgb(224,166,92);Danger=Color.FromArgb(226,124,108);Track=Color.FromArgb(45,56,61);
            KnobFill=Color.FromArgb(233,238,229);Soft=Color.FromArgb(33,42,47);SoftHi=Color.FromArgb(42,54,59);
            DangerSoft=Color.FromArgb(44,34,33);DangerSoftHi=Color.FromArgb(62,42,38);
            Popup=Color.FromArgb(26,33,37);PopupHot=Color.FromArgb(52,68,60);
        }
    }

    internal static Font F(float size,FontStyle style){return new Font("Microsoft YaHei UI",size,style);}
    internal static Color Mix(Color a,Color b,double t){return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
    internal static Color Species(string id){
        if(id=="Ladybug")return Color.FromArgb(226,110,88);
        if(id=="Ant")return Color.FromArgb(198,152,96);
        if(id=="Bee")return Color.FromArgb(232,186,74);
        if(id=="Butterfly")return Color.FromArgb(176,132,206);
        if(id=="Spider")return Color.FromArgb(112,128,152);
        if(id=="Mantis")return Color.FromArgb(126,178,110);
        if(id=="Snail")return Color.FromArgb(196,148,104);
        if(id=="Caterpillar")return Color.FromArgb(158,190,96);
        return Color.FromArgb(168,204,124);}
    internal static Color Alpha(Color c,int a){return Color.FromArgb(a,c.R,c.G,c.B);}

    internal static GraphicsPath Round(Rectangle r,int radius){
        var p=new GraphicsPath();int d=Math.Max(2,Math.Min(radius,Math.Min(r.Width,r.Height)/2))*2;
        if(d<=2){p.AddRectangle(r);p.CloseFigure();return p;}
        p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);
        p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);
        p.CloseFigure();return p;
    }
    internal static void Fill(Graphics g,Rectangle r,int radius,Color c){using(var p=Round(r,radius))using(var b=new SolidBrush(c))g.FillPath(b,p);}
    internal static void Stroke(Graphics g,Rectangle r,int radius,Color c){using(var p=Round(r,radius))using(var pen=new Pen(c))g.DrawPath(pen,p);}

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
    internal static void DarkTitleBar(IntPtr h){int one=1;try{DwmSetWindowAttribute(h,20,ref one,4);}catch{}try{DwmSetWindowAttribute(h,19,ref one,4);}catch{}}

    // One shared renderer keeps tray and pet menus consistent with the panel.
    internal static void StyleMenu(ContextMenuStrip menu){
        menu.Renderer=new DarkMenuRenderer();
        menu.BackColor=Ui.Popup;
        menu.ForeColor=Ink;
        menu.Font=F(9.5f,FontStyle.Regular);
        menu.ShowImageMargin=false;
        menu.Padding=new Padding(3,5,3,5);
    }
}

internal sealed class DarkColorTable:ProfessionalColorTable {
    private static Color Bg{get{return Ui.Popup;}}
    private static Color Hot{get{return Ui.PopupHot;}}
    private static Color Edge{get{return Ui.Line;}}
    public override Color ToolStripDropDownBackground{get{return Bg;}}
    public override Color ImageMarginGradientBegin{get{return Bg;}}
    public override Color ImageMarginGradientMiddle{get{return Bg;}}
    public override Color ImageMarginGradientEnd{get{return Bg;}}
    public override Color MenuBorder{get{return Edge;}}
    public override Color MenuItemBorder{get{return Hot;}}
    public override Color MenuItemSelected{get{return Hot;}}
    public override Color MenuItemSelectedGradientBegin{get{return Hot;}}
    public override Color MenuItemSelectedGradientEnd{get{return Hot;}}
    public override Color SeparatorDark{get{return Edge;}}
    public override Color SeparatorLight{get{return Edge;}}
}

internal sealed class DarkMenuRenderer:ToolStripProfessionalRenderer {
    internal DarkMenuRenderer():base(new DarkColorTable()){}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){
        e.TextColor=e.Item.Enabled?Ui.Ink:Ui.Muted;
        base.OnRenderItemText(e);
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){
        var g=e.Graphics;using(var pen=new Pen(Color.FromArgb(46,57,62))){int y=e.Item.Height/2;g.DrawLine(pen,6,y,e.Item.Width-8,y);}
    }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){
        using(var pen=new Pen(Color.FromArgb(48,60,65)))e.Graphics.DrawRectangle(pen,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);
    }
}

internal enum PillKind{Primary,Ghost,Dark,Danger}

internal sealed class PillButton:Control {
    internal PillKind Kind=PillKind.Dark;
    private bool hover,pressed;
    internal PillButton(string text,PillKind kind,Action click){
        Text=text;Kind=kind;Font=Ui.F(9.5f,FontStyle.Regular);Cursor=Cursors.Hand;Height=34;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;
        if(click!=null)Click+=delegate{click();};
    }
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        Color back,fore,edge;
        switch(Kind){
            case PillKind.Primary:back=hover?Ui.Mix(Ui.Accent,Ui.Light?Color.Black:Color.White,.14):Ui.Accent;fore=Ui.OnAccent;edge=back;break;
            case PillKind.Danger:back=hover?Ui.DangerSoftHi:Ui.DangerSoft;fore=Ui.Danger;edge=Ui.Line;break;
            case PillKind.Ghost:back=hover?Ui.CardHi:Ui.Card;fore=Ui.Ink;edge=Ui.Line;break;
            default:back=hover?Ui.SoftHi:Ui.Soft;fore=Ui.Ink;edge=Ui.Line;break;
        }
        if(pressed)back=Ui.Mix(back,Color.Black,.18);
        var r=new Rectangle(0,0,Width-1,Height-1);
        Ui.Fill(g,r,10,back);if(Kind!=PillKind.Primary)Ui.Stroke(g,r,10,edge);
        TextRenderer.DrawText(g,Text,Font,r,fore,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}

internal sealed class NavButton:Control {
    internal bool Active;
    private bool hover;
    internal NavButton(string text,Action click){
        Text=text;Font=Ui.F(10,FontStyle.Regular);Cursor=Cursors.Hand;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
        BackColor=Color.Transparent;
        if(click!=null)Click+=delegate{click();};
    }
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        if(hover&&!Active)Ui.Fill(g,new Rectangle(4,4,Width-8,Height-10),9,Ui.Card);
        TextRenderer.DrawText(g,Text,Font,new Rectangle(0,0,Width,Height-6),Active?Ui.Ink:Ui.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        if(Active)Ui.Fill(g,new Rectangle(Width/2-16,Height-6,32,3),2,Ui.Accent);
    }
}

internal sealed class Slider:Control {
    internal string Caption="",Unit="";
    internal int Min,Max;
    internal int Value;
    internal bool Compact;
    internal event EventHandler ValueChanged;
    internal event EventHandler Done;
    private bool drag;
    internal Slider(int min,int max,int value){Min=min;Max=max;Value=value;Height=Compact?34:58;Cursor=Cursors.Hand;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor|ControlStyles.Selectable,true);BackColor=Color.Transparent;}
    internal void SetValue(int v){v=Math.Max(Min,Math.Min(Max,v));if(v==Value)return;Value=v;Invalidate();}
    private void FromMouse(int x){
        float t=Math.Max(0f,Math.Min(1f,(x-8f)/Math.Max(1f,Width-16f)));
        int v=Min+(int)Math.Round(t*(Max-Min));
        if(v==Value){Invalidate();return;}
        Value=v;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);
    }
    protected override void OnMouseDown(MouseEventArgs e){drag=true;Focus();FromMouse(e.X);base.OnMouseDown(e);}
    protected override void OnMouseMove(MouseEventArgs e){if(drag)FromMouse(e.X);base.OnMouseMove(e);}
    protected override void OnMouseUp(MouseEventArgs e){if(drag){drag=false;if(Done!=null)Done(this,EventArgs.Empty);}base.OnMouseUp(e);}
    protected override void OnMouseWheel(MouseEventArgs e){Value=Math.Max(Min,Math.Min(Max,Value+(e.Delta>0?(Max-Min>60?5:1):(Max-Min>60?-5:-1))));Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);if(Done!=null)Done(this,EventArgs.Empty);base.OnMouseWheel(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        int trackY=Compact?14:36,trackH=6;
        if(!Compact)TextRenderer.DrawText(g,Caption,Ui.F(9f,FontStyle.Regular),new Rectangle(0,0,Width-90,20),Ui.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
        string val=Value+Unit;
        TextRenderer.DrawText(g,val,Ui.F(9.5f,FontStyle.Bold),new Rectangle(Width-110,Compact?0:0,110,Compact?26:20),Ui.Accent,TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
        float t=(float)(Value-Min)/Math.Max(1,Max-Min);
        int left=8,right=Width-8,knob=left+(int)((right-left)*t);
        var back=new Rectangle(left,trackY+ (Compact?5:0),Math.Max(2,right-left),trackH);
        Ui.Fill(g,back,trackH/2,Ui.Track);
        var fill=new Rectangle(left,back.Y,Math.Max(2,knob-left),trackH);
        if(fill.Width>2)Ui.Fill(g,fill,trackH/2,Ui.Accent);
        int ky=back.Y+trackH/2;
        using(var b=new SolidBrush(Ui.KnobFill))g.FillEllipse(b,knob-7,ky-7,14,14);
        using(var pen=new Pen(Ui.Accent,2))g.DrawEllipse(pen,knob-10,ky-10,20,20);
    }
}

internal sealed class ToggleRow:Control {
    internal string Caption="",Hint="";
    private bool on,hover;
    internal bool On{get{return on;}set{if(on==value)return;on=value;Invalidate();}}
    internal event EventHandler Changed;
    internal ToggleRow(string caption,string hint,bool value){Caption=caption;Hint=hint;on=value;Height=hint.Length>0?54:44;Cursor=Cursors.Hand;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnClick(EventArgs e){on=!on;Invalidate();if(Changed!=null)Changed(this,EventArgs.Empty);base.OnClick(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        if(hover)Ui.Fill(g,new Rectangle(0,2,Width,Height-4),10,Ui.Alpha(Ui.CardHi,120));
        int textW=Width-64;
        TextRenderer.DrawText(g,Caption,Ui.F(9.5f,FontStyle.Regular),new Rectangle(10,4,textW,22),Ui.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
        if(Hint.Length>0)TextRenderer.DrawText(g,Hint,Ui.F(8.5f,FontStyle.Regular),new Rectangle(10,25,textW,20),Ui.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
        var track=new Rectangle(Width-52,(Height-22)/2,42,22);
        Ui.Fill(g,track,11,on?Ui.Accent:Ui.Track);
        int kx=on?track.Right-19:track.X+3;
        using(var b=new SolidBrush(on?Ui.OnAccent:Ui.KnobFill))g.FillEllipse(b,kx,track.Y+3,16,16);
        if(!on)using(var pen=new Pen(Ui.Line))g.DrawEllipse(pen,kx,track.Y+3,15,15);
    }
}

internal sealed class AnimalList:ListBox {
    internal AnimalList(){
        DoubleBuffered=true;DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=64;BorderStyle=BorderStyle.None;
        BackColor=Ui.Card;ForeColor=Ui.Ink;IntegralHeight=false;Font=Ui.F(9.5f,FontStyle.Regular);
    }
    protected override void OnDrawItem(DrawItemEventArgs e){
        if(e.Index<0)return;
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        bool sel=(e.State&DrawItemState.Selected)!=0;
        var r=new Rectangle(2,e.Bounds.Top+2,e.Bounds.Width-22,e.Bounds.Height-4);
        Ui.Fill(g,r,10,sel?Ui.CardHi:Ui.Card);
        var p=Items[e.Index] as AnimalProfile;
        if(p==null)return;
        var def=CreatureCatalog.Find(p.Species);
        using(var b=new SolidBrush(Ui.Species(p.Species)))g.FillEllipse(b,r.X+14,r.Y+r.Height/2-5,10,10);
        if(sel)Ui.Fill(g,new Rectangle(r.X+2,r.Y+8,3,r.Height-16),2,Ui.Accent);
        TextRenderer.DrawText(g,p.Name,Ui.F(10.5f,FontStyle.Bold),new Rectangle(r.X+34,r.Y+8,r.Width-140,24),Ui.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,def.Name,Ui.F(8.5f,FontStyle.Regular),new Rectangle(r.X+34+TextRenderer.MeasureText(p.Name,Ui.F(10.5f,FontStyle.Bold)).Width+10,r.Y+10,160,20),Ui.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
        string sub="体型 "+p.Size+"%   ·   "+p.Temperament+"   ·   "+p.Route;
        TextRenderer.DrawText(g,sub,Ui.F(8.5f,FontStyle.Regular),new Rectangle(r.X+34,r.Y+32,r.Width-120,22),Ui.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        TextRenderer.DrawText(g,"编辑",Ui.F(8.5f,FontStyle.Regular),new Rectangle(r.Right-52,r.Y+20,44,22),sel?Ui.Accent:Ui.Muted,TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
    }
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);
        if(Items.Count==0){
            e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
            TextRenderer.DrawText(e.Graphics,"还没有动物，点右上角「添加动物」开始",Ui.F(9.5f,FontStyle.Regular),ClientRectangle,Ui.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        }
    }
}

internal sealed class Card:Panel {
    internal int Radius=14;
    internal Color FillColor=Ui.Card;
    internal bool Outline=true;
    internal Card(){
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
        BackColor=Ui.Card;
    }
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        var r=new Rectangle(0,0,Width-1,Height-1);
        Ui.Fill(g,r,Radius,FillColor);
        if(Outline)Ui.Stroke(g,r,Radius,Ui.Line);
    }
}
}
