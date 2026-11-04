using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[Serializable] public sealed class UIAdjustment
{
    public string id;
    public float x,y,angle,scaleX=1,scaleY=1;
    public bool flipX,flipY;
    public UITextStyle text;
    public UIAdjustment Copy(){var copy=(UIAdjustment)MemberwiseClone();copy.text=text==null?null:text.Copy();return copy;}
    public Matrix4x4 Matrix(Rect rect,Vector2 units)
    {
        var offset=new Vector2(x*units.x,y*units.y);
        return Matrix4x4.TRS(rect.center+offset,Quaternion.Euler(0,0,angle),new Vector3(scaleX*(flipX?-1:1),scaleY*(flipY?-1:1),1))
            *Matrix4x4.Translate(-rect.center);
    }
}
[Serializable] public sealed class UICalibrationFile
{
    public int version=1;
    public int designWidth=1920,designHeight=1080;
    public UIAdjustment[] elements=new UIAdjustment[0];
}
public sealed class UIEditTarget
{
    public string id,label;
    public Rect rect;
    public Matrix4x4 parent,matrix;
    public Vector2 units;
    public bool Contains(Vector2 screen){return rect.Contains(matrix.inverse.MultiplyPoint3x4(screen));}
    public Vector2 Center{get{return matrix.MultiplyPoint3x4(rect.center);}}
}
public sealed class UICalibration
{
    // 调整值始终是设计坐标；同一个模板可用于小手牌、放大预览和换牌。
    public static UICalibration Current;
    readonly Dictionary<string,UIAdjustment> entries=new Dictionary<string,UIAdjustment>();
    readonly Stack<string> undo=new Stack<string>(),redo=new Stack<string>();
    readonly Dictionary<int,UIEditTarget> hitRegions=new Dictionary<int,UIEditTarget>();
    public readonly List<UIEditTarget> Targets=new List<UIEditTarget>();
    public Action<string> Changed;
    public int Revision{get;private set;}
    public bool Collecting;
    public string DrawingId{get;private set;}
    public Vector2 DrawingUnits{get;private set;}=Vector2.one;
    public UITextStyle DrawingTextStyle{get;private set;}
    public Vector2 DrawingTextUnits{get;private set;}=Vector2.one;
    public string PathName{get;private set;}
    public bool Dirty{get{return Serialize()!=saved;}}
    public bool CanUndo{get{return undo.Count>0;}}
    public bool CanRedo{get{return redo.Count>0;}}
    string saved;
    public UICalibration(string path){PathName=path;saved=Serialize();}
    public static string CardTemplate(CardDef card){return card.Type==5?"card.spell":card.Type==7?"card.weapon":"card.minion";}
    public UIAdjustment Get(string id){UIAdjustment item;return entries.TryGetValue(id,out item)?item.Copy():new UIAdjustment{id=id};}
    public UITextStyle TextStyle(string id)
    {
        while(!string.IsNullOrEmpty(id))
        {
            UIAdjustment item;if(entries.TryGetValue(id,out item)&&item.text!=null)return item.text;
            int slash=id.LastIndexOf('/');if(slash<0)break;id=id.Substring(0,slash);
        }
        return null;
    }
    public void Checkpoint(){undo.Push(Serialize());if(undo.Count>80){var keep=undo.Take(80).Reverse().ToArray();undo.Clear();foreach(var item in keep)undo.Push(item);}redo.Clear();}
    public void Set(UIAdjustment value,bool record=true)
    {
        Validate(value);if(record)Checkpoint();entries[value.id]=value.Copy();Notify(value.id);
    }
    public void Reset(string id,bool children=false)
    {
        Checkpoint();foreach(string key in entries.Keys.Where(k=>k==id||(children&&k.StartsWith(id+"/",StringComparison.Ordinal))).ToArray())entries.Remove(key);
        Notify(children?"*":id);
    }
    public void ResetPage(string page)
    {
        Checkpoint();foreach(string id in entries.Keys.ToArray())
        {
            bool match=page=="scene"?(id.StartsWith("scene.",StringComparison.Ordinal)||id.StartsWith("battle.",StringComparison.Ordinal)):
                page=="hand"?id.StartsWith("battle.hand.",StringComparison.Ordinal):id==page||id.StartsWith(page+"/",StringComparison.Ordinal);
            if(match)entries.Remove(id);
        }
        Notify("*");
    }
    public void Undo(){if(!CanUndo)return;redo.Push(Serialize());Restore(undo.Pop());}
    public void Redo(){if(!CanRedo)return;undo.Push(Serialize());Restore(redo.Pop());}
    void Notify(string id){Revision++;if(Changed!=null)Changed(id);}
    void Restore(string json){Replace(Parse(json));Notify("*");}
    public string Serialize(){return JsonUtility.ToJson(new UICalibrationFile{elements=entries.Values.OrderBy(e=>e.id,StringComparer.Ordinal).ToArray()},true);}
    static bool Finite(float f){return !float.IsNaN(f)&&!float.IsInfinity(f);}
    static void Validate(UIAdjustment e)
    {
        if(e==null||string.IsNullOrEmpty(e.id)||e.id.Length>512||!Finite(e.x)||!Finite(e.y)||!Finite(e.angle)||!Finite(e.scaleX)||!Finite(e.scaleY)
            ||Mathf.Abs(e.x)>10000||Mathf.Abs(e.y)>10000||Mathf.Abs(e.angle)>36000||e.scaleX<.05f||e.scaleY<.05f||e.scaleX>10||e.scaleY>10)
            throw new InvalidDataException("UI 校对值无效：位置 ±10000、缩放 0.05~10，且必须是有限数值。");
        if(e.text!=null)e.text.Validate();
    }
    public static UICalibrationFile Parse(string json)
    {
        if(string.IsNullOrWhiteSpace(json)||json.Length>2000000)throw new InvalidDataException("UI 校对文件为空或过大。");
        var file=JsonUtility.FromJson<UICalibrationFile>(json);
        if(file==null||file.version!=1||file.designWidth!=1920||file.designHeight!=1080||file.elements==null||file.elements.Length>3000)
            throw new InvalidDataException("不支持的 UI 校对文件格式。");
        var ids=new HashSet<string>();foreach(var e in file.elements){Validate(e);if(!ids.Add(e.id))throw new InvalidDataException("重复的 UI 元素 ID："+e.id);}return file;
    }
    void Replace(UICalibrationFile file){entries.Clear();foreach(var e in file.elements)entries.Add(e.id,e.Copy());}
    public bool Load(out string error)
    {
        error="";try{if(File.Exists(PathName))Replace(Parse(File.ReadAllText(PathName)));else entries.Clear();saved=Serialize();Notify("*");return true;}
        catch(Exception e){error=e.Message;return false;}
    }
    public bool Import(string path,out string error)
    {
        error="";try{var parsed=Parse(File.ReadAllText(path));Checkpoint();Replace(parsed);Notify("*");return true;}
        catch(Exception e){error=e.Message;return false;}
    }
    static void AtomicWrite(string path,string text,bool backup)
    {
        var full=System.IO.Path.GetFullPath(path);Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
        string temp=full+".tmp";File.WriteAllText(temp,text);
        if(File.Exists(full))File.Replace(temp,full,backup?full+".bak":null);else File.Move(temp,full);
    }
    public bool Save(out string error)
    {
        error="";try{string json=Serialize();AtomicWrite(PathName,json,true);saved=json;return true;}
        catch(Exception e){error=e.Message;return false;}
    }
    public bool Export(string path,out string error)
    {
        error="";try{AtomicWrite(path,Serialize(),true);return true;}catch(Exception e){error=e.Message;return false;}
    }
    public void BeginFrame(){hitRegions.Clear();if(Collecting)Targets.Clear();}
    public UIEditTarget Register(string id,string label,Rect rect,Vector2 units,bool apply=true)
    {
        var parent=GUI.matrix;var target=new UIEditTarget{id=id,label=label,rect=rect,units=units,parent=parent,matrix=apply?parent*Get(id).Matrix(rect,units):parent};
        if(Collecting)Targets.Add(target);return target;
    }
    public void Draw(string id,string label,Rect rect,Vector2 units,Action draw,int entity=0)
    {
        var target=Register(id,label,rect,units);var old=GUI.matrix;var parentId=DrawingId;var parentUnits=DrawingUnits;
        var parentText=DrawingTextStyle;var parentTextUnits=DrawingTextUnits;
        var text=TextStyle(id);DrawingTextStyle=text??parentText;DrawingTextUnits=text==null?parentTextUnits:units;
        GUI.matrix=target.matrix;DrawingId=id;DrawingUnits=units;
        if(entity!=0)hitRegions[entity]=target;
        try{draw();}finally{GUI.matrix=old;DrawingId=parentId;DrawingUnits=parentUnits;DrawingTextStyle=parentText;DrawingTextUnits=parentTextUnits;}
    }
    public void Draw(string id,string label,Rect rect,Action draw){Draw(id,label,rect,Vector2.one,draw);}
    public bool Hit(int entity,Vector2 designMouse)
    {
        UIEditTarget region;return hitRegions.TryGetValue(entity,out region)&&region.Contains(BattleLayout.Matrix.MultiplyPoint3x4(designMouse));
    }
    public Rect VisualBounds(int entity,Rect fallback)
    {
        UIEditTarget target;if(!hitRegions.TryGetValue(entity,out target))return fallback;
        var m=BattleLayout.Matrix.inverse*target.matrix;
        var corners=new[]{new Vector2(target.rect.xMin,target.rect.yMin),new Vector2(target.rect.xMax,target.rect.yMin),new Vector2(target.rect.xMax,target.rect.yMax),new Vector2(target.rect.xMin,target.rect.yMax)};
        var points=corners.Select(p=>(Vector2)m.MultiplyPoint3x4(p)).ToArray();
        return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
    }
    public Rect VisualBounds(string id,Rect rect,Vector2 units)
    {
        var m=BattleLayout.Matrix.inverse*GUI.matrix*Get(id).Matrix(rect,units);
        var corners=new[]{new Vector2(rect.xMin,rect.yMin),new Vector2(rect.xMax,rect.yMin),new Vector2(rect.xMax,rect.yMax),new Vector2(rect.xMin,rect.yMax)};
        var points=corners.Select(p=>(Vector2)m.MultiplyPoint3x4(p)).ToArray();
        return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
    }
    public CardPose Pose(string id,CardPose pose,Vector2 pivot)
    {
        var e=Get(id);pose.center=e.Matrix(new Rect(pivot.x,pivot.y,0,0),Vector2.one).MultiplyPoint3x4(pose.center);
        pose.angle+=e.angle;pose.scale*=Mathf.Sqrt(e.scaleX*e.scaleY);return pose;
    }
    public static string NodePath(Transform node,Transform root)
    {
        var parts=new List<string>();while(node!=null&&node!=root){parts.Add(node.name+"["+node.GetSiblingIndex()+"]");node=node.parent;}parts.Reverse();return string.Join("/",parts);
    }
}
