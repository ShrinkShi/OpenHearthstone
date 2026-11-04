using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeMissilePathChecks
{
    [Serializable] sealed class Missile{public NativeMissilePathInfo raw;public Prefab prefab;}
    [Serializable] sealed class Config{public Missile missile;}
    [Serializable] sealed class Record{public string file;public Config config;}
    [Serializable] sealed class Catalog{public Record[] programs;}
    [Serializable] sealed class Prefab{public string prefab_reference,reference,file;}
    [Serializable] sealed class Prefabs{public Prefab[] prefabs;}
    static int passed;
    static void Check(bool ok,string message){if(!ok)throw new Exception("Original missile path: "+message);passed++;}
    static void Near(Vector3 actual,Vector3 expected,string message){Check(Vector3.Distance(actual,expected)<1e-4f,message+": "+actual+" expected "+expected);}
    public static void Run()
    {
        passed=0;var info=new NativeMissilePathInfo{m_CenterOffsetPercent=50,m_DistanceScaleFactor=8,m_CenterPointHeightMin=1,m_CenterPointHeightMax=1,m_RightMin=1,m_RightMax=1,m_LeftMin=2,m_LeftMax=2};
        int draws=0,bools=0;Func<float,float,float> range=(a,b)=>{draws++;return (a+b)*.5f;};Func<bool> right=()=>{bools++;return true;};
        var points=NativeMissilePath.Generate(info,Vector3.zero,new Vector3(0,0,8),range,right);
        Near(points[1],new Vector3(1,1,4),"distance-scaled center height and right offset");Check(draws==1&&bools==1,"equal width skips range but still samples height and side");
        Near(NativeMissilePath.Generate(info,new Vector3(0,0,8),Vector3.zero,range,right)[1],new Vector3(-1,1,4),"reverse Z direction flips right offset");
        Near(NativeMissilePath.Generate(info,Vector3.zero,new Vector3(0,0,8),range,()=>false)[1],new Vector3(-2,1,4),"left uses independent extent");
        info.m_CenterOffsetPercent=115;Near(NativeMissilePath.Generate(info,Vector3.zero,new Vector3(0,0,8),range,right)[1],new Vector3(1,1,9.2f),"center percentage is not clamped to segment");
        info.m_CenterPointHeightMin=info.m_CenterPointHeightMax=0;Near(NativeMissilePath.Generate(info,new Vector3(2,3,4),new Vector3(2,3,4),range,right)[1],new Vector3(2,5,4),"same-point zero-height fallback adds two units");
        info.m_CenterPointHeightMin=2;info.m_CenterPointHeightMax=4;Near(NativeMissilePath.Generate(info,Vector3.zero,Vector3.zero,range,right)[1],new Vector3(0,3,0),"same-point height range is not distance-scaled");
        info.m_RightMin=info.m_RightMax=0;Check(NativeMissilePath.Generate(info,Vector3.zero,new Vector3(0,0,8),range,right)[1].x<0,"zero right forces left");
        info.m_LeftMin=info.m_LeftMax=0;Near(NativeMissilePath.Generate(info,Vector3.zero,new Vector3(0,0,8),range,()=>false)[1],new Vector3(0,3,9.2f),"both zero ranges preserve original force-order result");
        info.m_RightMin=1;info.m_RightMax=3;info.m_DebugForceMax=1;Check(NativeMissilePath.Generate(info,Vector3.zero,new Vector3(0,0,8),range,right)[1].x==3,"debug force max skips width randomness");
        var hand=NativeMissilePath.Generate(info,Vector3.zero,Vector3.one,range,right,true,()=>new Vector3(0,0,8));Near(hand[2],new Vector3(0,0,8),"hand target uses original slot resolver");
        info.m_UseTargetCardPositionInsteadOfHandSlot=1;Near(NativeMissilePath.Generate(info,Vector3.zero,Vector3.one,range,right,true,()=>Vector3.zero)[2],Vector3.one,"explicit target card position bypasses slot");
        var path=new NativeMissilePath(new[]{Vector3.zero,new Vector3(0,2,4),new Vector3(0,0,8)},1,21);
        Near(path.Sample(0),Vector3.zero,"spline begins at actual start");Near(path.Sample(.5f),new Vector3(0,2,4),"spline passes source center control");Near(path.Sample(1),new Vector3(0,0,8),"spline ends at target");
        Near(path.Sample(.25f),new Vector3(0,1.25f,2),"Catmull-Rom differs from a quadratic Bezier midpoint");
        Near(path.LookPoint(.2f,.05f),path.Sample(.25f),"lookahead uses percentage then source easing");
        var closed=NativeMissilePath.ControlPoints(new[]{Vector3.zero,Vector3.one,Vector3.zero});Near(closed[0],Vector3.one,"closed spline preceding control wraps");Near(closed[4],Vector3.one,"closed spline next control wraps");
        ProbeOriginalKnife(range,right);
        Debug.Log("NATIVE MISSILE PATH: "+passed+" checks passed; full root missile/impact controller and LookUpdate remain incomplete");
        NativeOwnedSoundLimitChecks.Run();
    }
    static void ProbeOriginalKnife(Func<float,float,float> range,Func<bool> randomBool)
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var root=index.entries.First(e=>e.card_id=="EX1_129");
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-missile-config.json")));
        var record=catalog.programs.Single(r=>r.file==root.file);
        var prefabs=JsonUtility.FromJson<Prefabs>(File.ReadAllText(Path.Combine(assets.Root,"native-super-missile-prefabs.json")));
        var prefab=prefabs.prefabs.Single(p=>p.reference==record.config.missile.prefab.prefab_reference);
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file)));
        var target=new GameObject("Original missile target");target.transform.position=new Vector3(0,0,6);
        try
        {
            using(var clone=new NativeAreaInstance(program,assets,locations:kind=>target,facings:kind=>target,width:64,height:64))
            {
                clone.Effect.SetOriginalPosition(Vector3.zero);int arrived=0,looks=0;bool arrivedBeforeDeath=false;
                using(var flight=new NativeMissileFlight(clone,record.config.missile.raw,()=>target.transform.position,range,randomBool,
                    ()=>{arrived++;arrivedBeforeDeath=clone.Effect.ActiveSourceSpellState!=5;},look=>looks++))
                {
                    Check(clone.Effect.ActiveSourceSpellState==0,"cloned source initializes dormant before Birth");
                    flight.Birth();flight.Tick(.01f);Check(flight.Path!=null,"actual original Birth completion starts flight");
                    int startPixels=CaptureFrame(clone,"knife-start",.01f);
                    float at=flight.FiredAt,duration=flight.Path.Duration;
                    // The existing offscreen staging parent is at 20000 units;
                    // subtracting it costs float precision. Pure path math above
                    // stays strict; allow only that known sub-pixel staging error.
                    flight.Tick(at+duration*.5f);Check(Vector3.Distance(clone.Effect.Nodes[0].transform.position,flight.Path.Points[1])<.002f,"source clone follows original center within isolation float precision");
                    int middlePixels=CaptureFrame(clone,"knife-middle",at+duration*.5f);
                    Check(startPixels>0||middlePixels>0,"original missile mesh renders while following source path");
                    Check(looks>0,"source orient-to-path requests original look service");
                    flight.Tick(at+duration);Check(arrived==1&&arrivedBeforeDeath&&flight.Reached,"arrival invokes impact host before Death exactly once");
                    Near(clone.Effect.Nodes[0].transform.position,target.transform.position,"source clone arrives at target");
                    CaptureFrame(clone,"knife-arrived",at+duration);
                    flight.Tick(at+duration+.2f);Check(arrived==1,"continued frames cannot duplicate impact callback");
                }
            }
        }
        finally{AssetRegistry.ReleaseTransient(target);}
    }
    static int CaptureFrame(NativeAreaInstance clone,string name,float time)
    {
        clone.RenderAffine(time);
        var cameraObject=new GameObject("Fixed source missile diagnostic camera");var camera=cameraObject.AddComponent<Camera>();
        camera.transform.position=new Vector3(0,20,3);camera.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=5;camera.cullingMask=1<<30;camera.backgroundColor=new Color(.08f,.06f,.04f,1);camera.clearFlags=CameraClearFlags.SolidColor;
        var frame=new RenderTexture(960,540,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);frame.Create();camera.targetTexture=frame;
        var pixels=new Texture2D(960,540,TextureFormat.RGBA32,false,true);var previous=RenderTexture.active;
        try
        {
            camera.Render();RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,960,540),0,0);pixels.Apply();
            string folder="../tests/visual/current/native-missile-flight";Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,name+".png"),pixels.EncodeToPNG());
            var all=pixels.GetPixels32();var background=all[0];int visible=all.Count(c=>Math.Abs(c.r-background.r)+Math.Abs(c.g-background.g)+Math.Abs(c.b-background.b)>6);
            var position=clone.Effect.Nodes[0].transform.position;
            File.WriteAllText(Path.Combine(folder,name+".json"),JsonUtility.ToJson(new Frame{time=time,position=position,visible_pixels=visible,whole_effect_verified=false}));
            return visible;
        }
        finally{RenderTexture.active=previous;camera.targetTexture=null;AssetRegistry.ReleaseTransient(pixels);AssetRegistry.ReleaseTransient(frame);AssetRegistry.ReleaseTransient(cameraObject);}
    }
    [Serializable] sealed class Frame{public float time;public Vector3 position;public int visible_pixels;public bool whole_effect_verified;}
}
