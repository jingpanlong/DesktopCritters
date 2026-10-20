using System;
using System.Collections.Generic;
using System.Drawing;

namespace DesktopCritters {
// Register additional species here. Movement, feeding, dragging and surface traversal
// are shared by all species. A painter can draw any new animal into a transparent canvas.
internal delegate Bitmap CreaturePainter(double phase,float heading,float scale,string state,int mood,bool trails);
internal sealed class CreatureDefinition {
    internal string Id,Name,Description;
    internal double SpeedFactor;
    internal CreaturePainter Paint;
    internal CreatureDefinition(string id,string name,string description,double speed,CreaturePainter painter){Id=id;Name=name;Description=description;SpeedFactor=speed;Paint=painter;}
}
internal static class CreatureCatalog {
    internal static readonly List<CreatureDefinition> All=new List<CreatureDefinition>{
        new CreatureDefinition("Beetle","金属甲虫","细纹鞘翅与触须",1,delegate(double p,float h,float s,string state,int m,bool t){return CreatureRenderer.RenderBuiltIn("Beetle",p,h,s,state,m,t);}),
        new CreatureDefinition("Ladybug","七星瓢虫","红色斑点与圆润背甲",.78,delegate(double p,float h,float s,string state,int m,bool t){return CreatureRenderer.RenderBuiltIn("Ladybug",p,h,s,state,m,t);}),
        new CreatureDefinition("Ant","林地蚂蚁","分节身体与快速步态",1.25,delegate(double p,float h,float s,string state,int m,bool t){return CreatureRenderer.RenderBuiltIn("Ant",p,h,s,state,m,t);}),
        new CreatureDefinition("Bee","小蜜蜂","振翅悬停与条纹肚腩",1.15,delegate(double p,float h,float s,string state,int m,bool t){return CreatureRenderer.RenderBuiltIn("Bee",p,h,s,state,m,t);}),
        new CreatureDefinition("Butterfly","蝴蝶","斑斓鳞翅与慢舞翩跹",.85,delegate(double p,float h,float s,string state,int m,bool t){return CreatureRenderer.RenderBuiltIn("Butterfly",p,h,s,state,m,t);}),
        new CreatureDefinition("Spider","幽灵蜘蛛","八足轻移与伏击装死",1.35,delegate(double p,float h,float s,string state,int m,bool t){return CreatureRenderer.RenderBuiltIn("Spider",p,h,s,state,m,t);}),
        new CreatureDefinition("Mantis","螳螂","镰刀前肢与静默凝视",.9,delegate(double p,float h,float s,string state,int m,bool t){return CreatureRenderer.RenderBuiltIn("Mantis",p,h,s,state,m,t);}),
        new CreatureDefinition("Snail","蜗牛","螺旋背壳与从容慢行",.28,delegate(double p,float h,float s,string state,int m,bool t){return CreatureRenderer.RenderBuiltIn("Snail",p,h,s,state,m,t);}),
        new CreatureDefinition("Caterpillar","毛毛虫","弓背蠕动与步步生风",.45,delegate(double p,float h,float s,string state,int m,bool t){return CreatureRenderer.RenderBuiltIn("Caterpillar",p,h,s,state,m,t);})
    };
    // Species capabilities: who can take to the air and who hauls desktop icons.
    internal static bool CanFly(string id){return id=="Ladybug"||id=="Bee"||id=="Butterfly";}
    internal static bool CarriesIcons(string id){return id=="Ant";}
    internal static bool PushesFood(string id){return id=="Beetle";}
    internal static CreatureDefinition Find(string id){foreach(var d in All)if(d.Id==id)return d;return All[0];}
    internal static bool Contains(string id){foreach(var d in All)if(d.Id==id)return true;return false;}
}
}

