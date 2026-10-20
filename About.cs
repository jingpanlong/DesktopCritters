using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace DesktopCritters {
// Themed "about" dialog: product intro plus how to reach the author.
internal sealed class AboutWindow:Form {
    // Public repository: source code, builds and releases live here.
    internal const string GitHubUrl="https://github.com/USERNAME/DesktopCritters";
    private PictureBox art;
    internal AboutWindow(){
        Text="关于微野";Icon=AppIcon.Load(32);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;
        StartPosition=FormStartPosition.CenterParent;ClientSize=new Size(480,598);
        BackColor=Ui.Shell;ForeColor=Ui.Ink;Font=Ui.F(9.5f,FontStyle.Regular);
        HandleCreated+=delegate{Ui.DarkTitleBar(Handle);};
        var logo=AppIcon.Logo();
        if(logo!=null){
            art=new PictureBox{Bounds=new Rectangle(188,20,104,118),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Ui.Shell,Image=logo};
            Controls.Add(art);
        }
        var title=new Label{Text="微野 · DesktopCritters",Font=Ui.F(15.5f,FontStyle.Bold),ForeColor=Ui.Ink,BackColor=Ui.Shell,AutoSize=false,Bounds=new Rectangle(20,146,440,32),TextAlign=ContentAlignment.MiddleCenter};
        var ver=new Label{Text="v3.1  ·  九种动物桌面生态版",Font=Ui.F(9f,FontStyle.Regular),ForeColor=Ui.Accent,BackColor=Ui.Shell,AutoSize=false,Bounds=new Rectangle(20,178,440,22),TextAlign=ContentAlignment.MiddleCenter};
        Controls.Add(title);Controls.Add(ver);

        var intro=new Card{Bounds=new Rectangle(20,210,440,156)};
        intro.Controls.Add(new Label{Text="这是一群住在桌面上的小生命",Font=Ui.F(9.5f,FontStyle.Bold),ForeColor=Ui.Ink,BackColor=Ui.Card,Bounds=new Rectangle(16,12,410,22)});
        intro.Controls.Add(new Label{Text="九种高清网格形变动画的小生命：金属甲虫、七星瓢虫、林地蚂蚁、小蜜蜂、蝴蝶、幽灵蜘蛛、螳螂、蜗牛和毛毛虫。它们沿任务栏与菜单栏攀爬、在窗口之间穿行、结伴与追闹；每只都能把桌面图标搬到别处、腾空飞一段、把食物推着走。投喂、和平模式、个体管理随时可调。\n全部画面本地渲染：不联网、无广告、不读取文件内容。",Font=Ui.F(9f,FontStyle.Regular),ForeColor=Ui.Muted,BackColor=Ui.Card,Bounds=new Rectangle(16,38,410,108),TextAlign=ContentAlignment.TopLeft});
        Controls.Add(intro);

        var contact=new Card{Bounds=new Rectangle(20,378,440,150)};
        contact.Controls.Add(new Label{Text="联系作者",Font=Ui.F(9.5f,FontStyle.Bold),ForeColor=Ui.Ink,BackColor=Ui.Card,Bounds=new Rectangle(16,10,200,22)});
        contact.Controls.Add(new Label{Text="微信公众号",Font=Ui.F(9f,FontStyle.Regular),ForeColor=Ui.Muted,BackColor=Ui.Card,Bounds=new Rectangle(16,40,92,24)});
        contact.Controls.Add(new Label{Text="景同志",Font=Ui.F(10.5f,FontStyle.Bold),ForeColor=Ui.Ink,BackColor=Ui.Card,Bounds=new Rectangle(112,38,180,26)});
        contact.Controls.Add(FeedbackButton("复制",348,38,delegate{Clipboard.SetText("景同志");},"已复制"));
        contact.Controls.Add(new Label{Text="小红书",Font=Ui.F(9f,FontStyle.Regular),ForeColor=Ui.Muted,BackColor=Ui.Card,Bounds=new Rectangle(16,74,92,24)});
        contact.Controls.Add(new Label{Text="景同学",Font=Ui.F(10.5f,FontStyle.Bold),ForeColor=Ui.Ink,BackColor=Ui.Card,Bounds=new Rectangle(112,72,180,26)});
        contact.Controls.Add(FeedbackButton("复制",348,72,delegate{Clipboard.SetText("景同学");},"已复制"));
        contact.Controls.Add(new Label{Text="GitHub",Font=Ui.F(9f,FontStyle.Regular),ForeColor=Ui.Muted,BackColor=Ui.Card,Bounds=new Rectangle(16,108,92,24)});
        contact.Controls.Add(new Label{Text=GitHubShort(),Font=Ui.F(9.5f,FontStyle.Bold),ForeColor=Ui.Ink,BackColor=Ui.Card,Bounds=new Rectangle(112,106,150,26)});
        contact.Controls.Add(FeedbackButton("打开",276,106,delegate{try{Process.Start(GitHubUrl);}catch{try{Clipboard.SetText(GitHubUrl);}catch{}}},"已打开"));
        contact.Controls.Add(FeedbackButton("复制",348,106,delegate{Clipboard.SetText(GitHubUrl);},"已复制"));
        Controls.Add(contact);

        var close=new PillButton("知道了",PillKind.Primary,delegate{Close();}){Bounds=new Rectangle(170,540,140,36)};
        Controls.Add(close);
        FormClosed+=delegate{if(art!=null&&art.Image!=null){var img=art.Image;art.Image=null;img.Dispose();}};
    }
    // "github.com/owner/repo" without the scheme, so it fits the row.
    private static string GitHubShort(){return GitHubUrl.Replace("https://","");}
    private Control FeedbackButton(string text,int x,int y,Action click,string done){
        PillButton b=null;
        b=new PillButton(text,PillKind.Ghost,delegate{
            try{click();}catch{}
            if(b!=null&&!b.IsDisposed)b.Text=done;
            var t=new Timer{Interval=1400};
            t.Tick+=delegate{t.Stop();t.Dispose();if(b!=null&&!b.IsDisposed)b.Text=text;};
            t.Start();
        }){Bounds=new Rectangle(x,y,68,26)};
        return b;
    }
}
}
