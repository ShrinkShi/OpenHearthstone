using System;
using System.IO;
using System.Linq;
using UnityEngine;

// 原按钮网格和原 AnimationClip 四元数采样，独立相机实时渲染翻转。
public sealed class TurnButtonVisuals : IDisposable
{
    [Serializable] public class Key {public float time;public Quaternion value;}
    [Serializable] public class Rotation {public string path;public Key[] keys;}
    [Serializable] public class Clip {public string name;public float duration;public Rotation[] rotations;}
    [Serializable] public class Clips {public Clip[] clips;}
    readonly Camera camera;
    readonly GameObject root;
    readonly Transform button;
    readonly RenderTexture texture;
    readonly Clips clips;
    bool initialized,own;
    float elapsed=10;
    Clip playing;
    Quaternion rotation=Quaternion.identity;
    int lastFrame=-1;
    Color lastColor;
    public bool Flipping{get{return playing!=null&&elapsed<Duration(playing);}}
    public float LabelVisibility{get{return Flipping?Mathf.Pow(Mathf.Abs((rotation*Vector3.up).y),3):1;}}
    public TurnButtonVisuals(AssetRegistry assets)
    {
        clips=JsonUtility.FromJson<Clips>(File.ReadAllText(assets.PathFor("ANIM_END_TURN")));
        root=assets.Scene("MODEL_END_TURN");root.transform.position=new Vector3(0,-1500,0);
        button=root.GetComponentsInChildren<MeshRenderer>(true).First().transform;
        button.localPosition=Vector3.zero;button.localRotation=Quaternion.identity;
        foreach(var node in root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=29;
        var bounds=button.GetComponent<Renderer>().bounds;
        camera=new GameObject("TurnButtonCamera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<29;
        camera.nearClipPlane=.01f;camera.farClipPlane=30;camera.aspect=2.5f;
        camera.orthographicSize=Mathf.Max(bounds.size.z,bounds.size.x/2.5f)*.57f;
        camera.transform.position=bounds.center+Vector3.up*8;camera.transform.rotation=Quaternion.Euler(90,0,0);
        texture=new RenderTexture(500,200,24,RenderTextureFormat.ARGB32){antiAliasing=4};texture.Create();
    }
    public static float Duration(Clip clip)
    {return clip.rotations.SelectMany(c=>c.keys).Select(k=>k.time).DefaultIfEmpty(0).Max();}
    public static Quaternion Sample(Clip clip,float time)
    {
        var keys=clip.rotations.FirstOrDefault(c=>c.path=="")?.keys;
        if(keys==null||keys.Length==0)return Quaternion.identity;
        for(int i=1;i<keys.Length;i++)if(time<keys[i].time)
            return Quaternion.Slerp(keys[i-1].value,keys[i].value,Mathf.InverseLerp(keys[i-1].time,keys[i].time,time));
        return keys[keys.Length-1].value;
    }
    public void Observe(bool yourTurn)
    {
        if(!initialized){initialized=true;own=yourTurn;rotation=yourTurn?Quaternion.identity:Quaternion.Euler(180,0,0);return;}
        if(own==yourTurn)return;own=yourTurn;
        playing=clips.clips.First(c=>c.name==(yourTurn?"ENDTURN_YOUR_TURN":"ENDTURN_WAITING"));elapsed=0;
    }
    public void Tick(float delta){elapsed+=delta;if(playing!=null)rotation=Sample(playing,elapsed);}
    public Texture Render(Color color)
    {
        if(lastFrame==Time.frameCount&&lastColor==color)return texture;
        // 静止时复用，翻转时每帧渲染一次；不创建临时网格或材质。
        if(!Flipping&&lastFrame>=0&&lastColor==color&&Quaternion.Angle(button.localRotation,rotation)<.001f)return texture;
        button.localRotation=rotation;lastFrame=Time.frameCount;lastColor=color;
        foreach(var material in button.GetComponent<Renderer>().sharedMaterials)material.SetColor("_Tint",color);
        camera.targetTexture=texture;camera.Render();camera.targetTexture=null;return texture;
    }
    public Texture RenderSample(string clip,float time,Color color)
    {playing=clips.clips.First(c=>c.name==clip);elapsed=time;rotation=Sample(playing,time);lastFrame=-1;return Render(color);}
    public void Dispose()
    {
        if(root!=null)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer==null)continue;
                foreach(var material in renderer.sharedMaterials)AssetRegistry.ReleaseTransient(material);
            }
            AssetRegistry.ReleaseTransient(root);
        }
        if(camera!=null)
        {
            camera.targetTexture=null;
            AssetRegistry.ReleaseTransient(camera.gameObject);
        }
        AssetRegistry.ReleaseTransient(texture);
    }
}
