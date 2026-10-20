using System;
using System.IO;
using System.Collections.Generic;
using System.Xml.Serialization;
using Microsoft.Win32;
namespace DesktopCritters {
public class AnimalProfile {
    public string Id=Guid.NewGuid().ToString("N");
    public string Species="Beetle",Name="小甲",Temperament="均衡",Route="自由";
    public int Size=100;
    public AnimalProfile(){}
    public AnimalProfile(string species,string name){Species=species;Name=name;}
}
// Where a desktop icon used to be, so the ant's housekeeping can be undone.
public class IconMove {
    public string Name="";public int X,Y;public string When="";
    public IconMove(){}
    public IconMove(string name,int x,int y,string when){Name=name;X=x;Y=y;When=when;}
}
public class Settings {
    public List<AnimalProfile> Animals=new List<AnimalProfile>();
    public List<IconMove> IconMoves=new List<IconMove>();
    public static Settings Defaults(){var s=new Settings();s.Animals.Add(new AnimalProfile("Beetle","青铜"));s.Animals.Add(new AnimalProfile("Ladybug","小红"));s.Animals.Add(new AnimalProfile("Ant","小黑"));s.Animals.Add(new AnimalProfile("Bee","蜜蜜"));s.Animals.Add(new AnimalProfile("Butterfly","彩蝶"));s.Animals.Add(new AnimalProfile("Spider","蛛蛛"));s.Animals.Add(new AnimalProfile("Mantis","小螳"));s.Animals.Add(new AnimalProfile("Snail","蜗蜗"));s.Animals.Add(new AnimalProfile("Caterpillar","毛毛"));return s;}
    public int Scale=100,Speed=35,Fps=30,Aggression=35,ReturnMin=4,ReturnMax=16,ExcursionInterval=90;
    public bool FollowMouse=false,MouseFear=true,ClimbWindows=true,ClimbTaskbar=true,ClimbMenus=true,ClimbIcons=true;
    public bool HideFullScreen=true,ClickThrough=false,AllScreens=true,AutoStart=false,Trails=false,NightSleep=true;
    public bool Social=true,Peaceful=false,AllowOffscreen=true,ShowNames=false,BatterySaver=true,LightTheme=true;
    public bool CarryIcons=true,Flight=true;
    public static string DirectoryPath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DesktopCritters");}}
    public static string FilePath{get{return Path.Combine(DirectoryPath,"settings-ecosystem.xml");}}
    public static int Limit(int value,int min,int max){return Math.Max(min,Math.Min(max,value));}
    public void Validate(){
        Scale=Limit(Scale,25,600);Speed=Limit(Speed,10,160);Fps=Fps==60?60:30;Aggression=Limit(Aggression,0,100);
        ReturnMin=Limit(ReturnMin,1,120);ReturnMax=Limit(ReturnMax,ReturnMin,180);ExcursionInterval=Limit(ExcursionInterval,20,600);
        if(Animals==null)Animals=new List<AnimalProfile>();if(Animals.Count>24)Animals.RemoveRange(24,Animals.Count-24);
        if(IconMoves==null)IconMoves=new List<IconMove>();
        if(IconMoves.Count>60)IconMoves.RemoveRange(0,IconMoves.Count-60);
        foreach(var m in IconMoves){if(m.Name==null)m.Name="";if(m.When==null)m.When="";}
        var ids=new HashSet<string>();foreach(var p in Animals){if(String.IsNullOrEmpty(p.Id)||!ids.Add(p.Id)){p.Id=Guid.NewGuid().ToString("N");ids.Add(p.Id);}if(!CreatureCatalog.Contains(p.Species))p.Species="Beetle";
            p.Size=Limit(p.Size,50,200);p.Name=(p.Name??"小动物").Trim();if(p.Name.Length==0)p.Name="小动物";if(p.Name.Length>16)p.Name=p.Name.Substring(0,16);
            if(p.Temperament!="均衡"&&p.Temperament!="胆小"&&p.Temperament!="好斗"&&p.Temperament!="好奇")p.Temperament="均衡";
            if(p.Route!="自由"&&p.Route!="任务栏"&&p.Route!="菜单栏"&&p.Route!="窗口边缘"&&p.Route!="屏幕边缘"&&p.Route!="桌面图标")p.Route="自由";
        }
    }
    public static Settings Load(){try{using(var f=File.OpenRead(FilePath)){var s=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(f);s.Validate();return s;}}catch{return Defaults();}}
    public void Save(){Validate();Directory.CreateDirectory(DirectoryPath);string temp=FilePath+".tmp";using(var f=File.Create(temp))new XmlSerializer(typeof(Settings)).Serialize(f,this);if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);}
    public void SetStartup(string executable){using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(AutoStart)k.SetValue("DesktopCritters","\""+executable+"\" --tray");else k.DeleteValue("DesktopCritters",false);}}
}
}
