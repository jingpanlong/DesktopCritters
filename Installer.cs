using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("微野安装程序")]
[assembly: AssemblyProduct("Desktop Critters Setup")]
[assembly: AssemblyVersion("3.0.0.0")]
internal static class Setup {
    internal static string Target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","DesktopCritters");
    internal static string AppPath{get{return Path.Combine(Target,"DesktopCritters.exe");}}
    internal static string Menu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"微野 Desktop Critters");
    private const string RegistryPath=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DesktopCritters";
    [STAThread] private static int Main(string[] args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try{
            if(args.Length>1&&args[0]=="--preview"){
                Directory.CreateDirectory(args[1]);using(var preview=new SetupForm()){preview.Show();Application.DoEvents();using(var b=new Bitmap(preview.Width,preview.Height)){preview.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));b.Save(Path.Combine(args[1],"installer.png"),System.Drawing.Imaging.ImageFormat.Png);}preview.Close();}return 0;
            }
            if(args.Length>1&&args[0]=="--verify-payload"){Directory.CreateDirectory(args[1]);Extract(Path.Combine(args[1],"DesktopCritters.exe"));return 0;}
            if(args.Length>0&&args[0]=="--uninstall"){
                if(MessageBox.Show("卸载微野？\n程序和快捷方式将被移除，个人配置将保留。","卸载微野",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return 0;
                string temp=Path.Combine(Path.GetTempPath(),"DesktopCritters-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Application.ExecutablePath,temp);Process.Start(temp,"--remove "+Process.GetCurrentProcess().Id);return 0;
            }
            if(args.Length>0&&args[0]=="--remove"){
                if(args.Length>1){int pid;if(Int32.TryParse(args[1],out pid))try{Process.GetProcessById(pid).WaitForExit(10000);}catch(ArgumentException){}}
                EnsureStopped();
                // Remove only files owned by this installer; never recursively delete the directory.
                foreach(string name in new string[]{"DesktopCritters.exe","Uninstall.exe"}){string f=Path.Combine(Target,name);if(File.Exists(f))File.Delete(f);}
                DeleteShortcut(Path.Combine(Menu,"微野.lnk"));DeleteShortcut(Path.Combine(Menu,"卸载微野.lnk"));DeleteShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"微野.lnk"));
                if(Directory.Exists(Target)&&Directory.GetFileSystemEntries(Target).Length==0)Directory.Delete(Target);
                if(Directory.Exists(Menu)&&Directory.GetFileSystemEntries(Menu).Length==0)Directory.Delete(Menu);
                using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))k.DeleteValue("DesktopCritters",false);
                Registry.CurrentUser.DeleteSubKeyTree(RegistryPath,false);
                MessageBox.Show("微野已卸载。\n个人设置保留在 LocalAppData\\DesktopCritters。","微野");return 0;
            }
            Application.Run(new SetupForm());return 0;
        }catch(Exception ex){MessageBox.Show("操作未完成：\n"+ex.Message,"微野安装程序",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
    private static void DeleteShortcut(string p){if(File.Exists(p))File.Delete(p);}
    internal static void EnsureStopped(){foreach(var p in Process.GetProcessesByName("DesktopCritters"))using(p){try{if(String.Equals(Path.GetFullPath(p.MainModule.FileName),Path.GetFullPath(AppPath),StringComparison.OrdinalIgnoreCase))throw new IOException("请先从系统托盘退出正在运行的微野，再重试。");}catch(System.ComponentModel.Win32Exception){throw new IOException("无法检查程序是否正在运行，请先退出微野。");}}}
    internal static void Extract(string path){using(Stream s=Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopCritters.Payload")){if(s==null)throw new IOException("安装包缺少程序资源。");using(var f=File.Create(path))s.CopyTo(f);}}
    internal static void Install(bool desktop){
        EnsureStopped();Directory.CreateDirectory(Target);string temp=Path.Combine(Target,"DesktopCritters.new");
        try{Extract(temp);if(File.Exists(AppPath))File.Replace(temp,AppPath,null);else File.Move(temp,AppPath);}finally{if(File.Exists(temp))File.Delete(temp);}
        File.Copy(Application.ExecutablePath,Path.Combine(Target,"Uninstall.exe"),true);
        Directory.CreateDirectory(Menu);Shortcut(Path.Combine(Menu,"微野.lnk"),AppPath,"");Shortcut(Path.Combine(Menu,"卸载微野.lnk"),Path.Combine(Target,"Uninstall.exe"),"--uninstall");
        if(desktop)Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"微野.lnk"),AppPath,"");
        using(var key=Registry.CurrentUser.CreateSubKey(RegistryPath)){
            key.SetValue("DisplayName","微野 · Desktop Critters");key.SetValue("DisplayVersion","3.0.0");key.SetValue("Publisher","Desktop Critters");key.SetValue("InstallLocation",Target);key.SetValue("DisplayIcon",AppPath);key.SetValue("UninstallString","\""+Path.Combine(Target,"Uninstall.exe")+"\" --uninstall");key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);key.SetValue("EstimatedSize",Math.Max(1,(int)(new FileInfo(AppPath).Length+new FileInfo(Application.ExecutablePath).Length)/1024),RegistryValueKind.DWord);
        }
    }
    private static void Shortcut(string file,string target,string args){object shell=null,link=null;try{Type type=Type.GetTypeFromProgID("WScript.Shell");shell=Activator.CreateInstance(type);link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{file});Type t=link.GetType();t.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});t.InvokeMember("Arguments",BindingFlags.SetProperty,null,link,new object[]{args});t.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Target});t.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);}finally{if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}}
}
internal sealed class SetupForm : Form {
    private CheckBox desktop,launch;
    private Button install;
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int v,int size);
    internal SetupForm(){
        Text="安装微野 · Desktop Critters";ClientSize=new Size(620,430);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.FromArgb(14,17,19);ForeColor=Color.FromArgb(233,238,229);Font=new Font("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;
        Add("微野",30,32,26,540,58,Color.FromArgb(184,216,140));Add("让一小片自然，住进你的桌面。",12.5f,34,92,540,30,ForeColor);
        Add("高清甲虫 · 瓢虫 · 蚂蚁，支持同时混养\n菜单栏与任务栏路线 / 屏外探索 / 追逐与争斗",10,34,146,540,60,Color.FromArgb(149,168,158));
        Add("安装到当前用户，无需管理员权限：\n"+Setup.Target,9,34,222,540,53,Color.FromArgb(149,168,158));
        desktop=new CheckBox{Text="创建桌面快捷方式",Checked=true,Bounds=new Rectangle(34,300,240,26),ForeColor=ForeColor,BackColor=BackColor};
        launch=new CheckBox{Text="完成后运行微野",Checked=true,Bounds=new Rectangle(310,300,240,26),ForeColor=ForeColor,BackColor=BackColor};
        Controls.Add(desktop);Controls.Add(launch);
        install=new Button{Text="安装并开始",Bounds=new Rectangle(404,352,180,44),BackColor=Color.FromArgb(184,216,140),ForeColor=Color.FromArgb(18,24,18),FlatStyle=FlatStyle.Flat,Font=new Font("Microsoft YaHei UI",10.5f,FontStyle.Bold),Cursor=Cursors.Hand};
        install.FlatAppearance.BorderSize=0;Controls.Add(install);install.Click+=Install;
        Add("v3.0 · Windows 10 / 11",9,34,366,300,30,Color.FromArgb(108,126,118));
        HandleCreated+=delegate{int one=1;try{DwmSetWindowAttribute(Handle,20,ref one,4);}catch{}try{DwmSetWindowAttribute(Handle,19,ref one,4);}catch{}};
    }
    private void Add(string text,float size,int x,int y,int w,int h,Color c){Controls.Add(new Label{Text=text,Font=new Font("Microsoft YaHei UI",size),ForeColor=c,BackColor=BackColor,Bounds=new Rectangle(x,y,w,h)});}
    private void Install(object sender,EventArgs e){install.Enabled=false;install.Text="安装中…";try{Setup.Install(desktop.Checked);if(launch.Checked)Process.Start(Setup.AppPath);else MessageBox.Show(this,"安装完成，可从开始菜单打开微野。","微野");Close();}catch(Exception ex){install.Enabled=true;install.Text="重试安装";MessageBox.Show(this,"安装未完成：\n"+ex.Message,"微野",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
}
