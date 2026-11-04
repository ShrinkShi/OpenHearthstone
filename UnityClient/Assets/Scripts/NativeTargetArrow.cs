using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// 原资源/控制公式；GUI端点经当前棋盘明确的平面投影输入，不冒称原hitbox已恢复。
public sealed class NativeTargetArrow:IDisposable
{
    [Serializable] sealed class Asset{public string reference,file;}
    [Serializable] sealed class Catalog{public Asset[] arrows;}
    readonly NativeEffectInstance head;readonly NativeEffectInstance[] links=new NativeEffectInstance[15];
    readonly NativeSourceCamera sourceCamera;readonly Camera projection;
    readonly Vector3 origin,sourceOrigin;readonly Transform root,arrow;readonly Renderer bullseye,headRenderer;
    readonly NativeEffectPlayer compositor;float offset,time;bool visible;
    public float Offset{get{return offset;}}
    public int VisibleLinks{get;private set;}
    public bool BullseyeVisible{get{return bullseye.enabled;}}
    public IEnumerable<GameObject> OwnedObjects{get{return new[]{head.Root}.Concat(links.Select(l=>l.Root)).Concat(new[]{projection.gameObject});}}
    public NativeTargetArrow(AssetRegistry assets)
    {
        var catalog=JsonUtility.FromJson<Catalog>(NativeFireblastCache.Read(assets));
        sourceCamera=JsonUtility.FromJson<NativeSourceCameras>(File.ReadAllText(Path.Combine(assets.Root,"original-battle-cameras.json"))).cameras.Single(c=>c.name=="Main Camera");
        sourceOrigin=new Vector3(sourceCamera.Position.x,0,0);origin=new Vector3(50000,10000,50000);
        NativeEffectInstance Create(Asset asset,int slot)
        {
            var program=NativeVisualProgram.Parse(File.ReadAllText(Path.Combine(assets.Root,asset.file)));
            if(program.reference!=asset.reference||program.nodes.Any(n=>n.particle!=null&&n.particle.restored)||program.machines.Length!=0)throw new InvalidDataException("Unreviewed targeting prefab graph");
            var instance=new NativeEffectInstance(program,assets,slot,1024,576);
            try{instance.Seek(0);instance.Root.transform.position=origin;return instance;}catch{instance.Dispose();throw;}
        }
        try
        {
            projection=new GameObject("Original targeting projection").AddComponent<Camera>();projection.enabled=false;
            sourceCamera.Apply(projection,sourceOrigin,origin,1920f/1080);
            head=Create(catalog.arrows.Single(a=>a.reference.StartsWith("Target_Arrow_Bullseye.prefab:")),60);head.ApplySourceCamera(sourceCamera,sourceOrigin);
            root=head.Nodes[0].transform;arrow=head.Nodes.Single(n=>n.name=="TargetArrow_ArrowMesh").transform;
            headRenderer=arrow.GetComponent<Renderer>();
            bullseye=head.Nodes.Single(n=>n.name=="TargetArrow_TargetMesh").GetComponent<Renderer>();
            var link=catalog.arrows.Single(a=>a.reference.StartsWith("Target_Arrow_Link.prefab:"));
            for(int i=0;i<links.Length;i++)links[i]=Create(link,61+i);
            compositor=new NativeEffectPlayer(assets);Hide();
        }
        catch{Dispose();throw;}
    }
    public Vector3 Project(Vector2 designPoint)
    {
        var ray=projection.ViewportPointToRay(new Vector3(designPoint.x/1920,1-designPoint.y/1080,0));float distance;
        if(!new Plane(Vector3.up,origin).Raycast(ray,out distance))throw new InvalidOperationException("Target cursor does not intersect the current board plane");return ray.GetPoint(distance);
    }
    public void Tick(float delta)
    {if(!visible)return;offset+=delta*.5f;if(offset>1.2f)offset-=1.2f;time+=delta;}
    static void Alpha(NativeEffectInstance instance,float alpha)
    {foreach(var material in instance.Nodes.SelectMany(n=>n.GetComponents<Renderer>()).SelectMany(r=>r.sharedMaterials)){var color=material.GetColor("_Color");color.a=alpha;material.SetColor("_Color",color);}}
    public void Show(Vector2 from,Vector2 to,bool legal)
    {
        visible=true;head.Root.SetActive(true);headRenderer.enabled=true;var start=Project(from);var target=Project(to);var delta=target-start;
        float distance=new Vector2(delta.x,delta.z).magnitude,yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
        root.position=target;root.localEulerAngles=new Vector3(0,yaw,0);bullseye.enabled=legal;
        int count=Mathf.FloorToInt(distance/1.2f)+1;if(count==1)count=0;VisibleLinks=Mathf.Min(count,15);
        float center=-distance/2,coefficient=count==0?0:-1.5f/(center*center);
        for(int i=0;i<links.Length;i++)
        {
            var link=links[i];link.Root.SetActive(i<count);if(i>=count)continue;
            link.Nodes[0].GetComponent<Renderer>().enabled=true;
            float z=-1.2f*(i+1)+offset,y=coefficient*(z-center)*(z-center)+1.5f;
            link.Nodes[0].transform.position=root.TransformPoint(new Vector3(0,y,z));
            link.Nodes[0].transform.eulerAngles=new Vector3(180-Mathf.Atan(2*coefficient*(z-center))*Mathf.Rad2Deg,yaw,0);
            float alpha=i==0&&z>-1.2f?Mathf.Pow(z/-1.2f,6):i==count-1?Mathf.Pow(offset/1.2f,2):1;Alpha(link,Mathf.Clamp01(alpha));
        }
        if(count==0){arrow.localEulerAngles=new Vector3(300,180,0);var point=arrow.localPosition;point.y=0;arrow.localPosition=point;}
        else{float z=arrow.localPosition.z;arrow.localPosition=new Vector3(0,coefficient*(z-center)*(z-center)+1.5f,z);float angle=Mathf.Atan(2*coefficient*(z-center))*Mathf.Rad2Deg;if(angle<0)angle+=360;arrow.localEulerAngles=new Vector3(angle,180,0);}
    }
    public RenderTexture Render()
    {return head.RenderTogether(new[]{head}.Concat(links.Where(l=>l.Root.activeSelf)));}
    public void Draw()
    {if(!visible||Event.current.type!=EventType.Repaint)return;var image=Render();compositor.RenderAffine(new Rect(0,0,1920,1080),image,head.Transmission,GUI.matrix,Screen.width,Screen.height,GUI.color);}
    public void Hide(){visible=false;if(head!=null)head.Root.SetActive(false);foreach(var link in links)if(link!=null)link.Root.SetActive(false);}
    public void Dispose(){compositor?.Dispose();head?.Dispose();foreach(var link in links)link?.Dispose();if(projection!=null)AssetRegistry.ReleaseTransient(projection.gameObject);}
}
