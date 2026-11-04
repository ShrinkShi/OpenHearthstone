using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeHighlightSilhouetteChecks
{
    [Serializable] sealed class State{public int state;public NativeMaterial material;}
    [Serializable] sealed class Definition{public State[] states;public int render_queue,queue_offset;}
    static float Sample(Texture2D texture,float u,float v)
    {return texture.GetPixel(Mathf.Clamp(Mathf.FloorToInt(u*texture.width),0,texture.width-1),Mathf.Clamp(Mathf.FloorToInt(v*texture.height),0,texture.height-1)).r;}
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original silhouette: "+message);passed++;};
        var source=new Texture2D(8,8,TextureFormat.RGBAFloat,false,true);var blend=new Texture2D(3,5,TextureFormat.RGBAFloat,false,true);
        source.filterMode=blend.filterMode=FilterMode.Point;source.wrapMode=blend.wrapMode=TextureWrapMode.Clamp;
        source.SetPixels(Enumerable.Range(0,64).Select(i=>new Color(i*.03f,.6f,.8f,.7f)).ToArray());source.Apply();
        blend.SetPixels(Enumerable.Range(0,15).Select(i=>new Color(i*.05f,.9f,.7f,.4f)).ToArray());blend.Apply();
        var target=new RenderTexture(8,8,0,RenderTextureFormat.ARGBFloat);target.Create();var image=new Texture2D(8,8,TextureFormat.RGBAFloat,false,true);
        var previous=RenderTexture.active;
        try
        {
            using(var filters=new NativeHighlightFilters())
            foreach(int pass in new[]{0,1,2})foreach(float blur in new[]{0f,.8f,1.25f,1.5f})
            {
                filters.Apply(source,blend,target,pass,blur);RenderTexture.active=target;image.ReadPixels(new Rect(0,0,8,8),0,0);image.Apply();
                for(int y=0;y<8;y++)for(int x=0;x<8;x++)
                {
                    float u=(x+.5f)/8,v=(y+.5f)/8,du=-blur/8,dv=-blur/8;
                    Func<Texture2D,float> sum=texture=>Sample(texture,u+du,v+dv)+Sample(texture,u-du,v-dv)+Sample(texture,u+du,v-dv)+Sample(texture,u-du,v+dv);
                    float expected=pass==0?sum(source)*.25f:pass==1?Mathf.Max(sum(source)*.25f,Sample(blend,u,v)):Mathf.Clamp01(.3f*(sum(source)-sum(blend)));
                    Color actual=image.GetPixel(x,y);
                    check(Mathf.Max(Mathf.Abs(actual.r-expected),Mathf.Abs(actual.g-expected),Mathf.Abs(actual.b-expected),Mathf.Abs(actual.a-expected))<.0005f,"source UV1/four-tap/max/subtract/saturate RGBA "+pass+"/"+blur+"/"+x+"/"+y);
                }
            }
            var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
            var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
            var definition=JsonUtility.FromJson<Definition>(File.ReadAllText(Path.Combine(assets.Root,"original-minion-highlight.json")));
            string folder=Path.GetFullPath("../tests/visual/current/native-highlight-silhouette");Directory.CreateDirectory(folder);
            foreach(string cardId in new[]{"CS2_182","CS2_172","CS2_179"})
            using(var live=visuals.CreateLiveActor(database.cards.First(c=>c.CardId==cardId),"minion",true))
            {
                var actor=live.Model.GetComponentInChildren<NativeActorIdentity>(true);var root=actor.transform;
                var sourcePlane=NativeActorIdentity.FindChildBySubstring(actor.gameObject,"Highlight_Card_Plane");
                var planeMesh=sourcePlane.GetComponent<MeshFilter>().sharedMesh;
                check(planeMesh.uv2.Length==planeMesh.vertexCount,"original highlight second UV reaches actual Mesh "+cardId);
                check(planeMesh.colors.Max(c=>c.r)==1&&planeMesh.colors.Min(c=>c.r)==0,"source UNorm8 red colors normalized rather than 255 "+cardId);
                root.localRotation=Quaternion.Euler(12,24,8);root.localScale=new Vector3(1.4f,.8f,1.2f);
                var parent=root.parent;var position=root.localPosition;var rotation=root.localRotation;var scale=root.localScale;
                using(var silhouette=new NativeHighlightSilhouette(actor))
                {
                    silhouette.Generate();
                    check(root.parent==parent&&root.localPosition==position&&root.localRotation==rotation&&root.localScale==scale,"source root transform restored "+cardId);
                    var mask=new Texture2D(silhouette.Texture.width,silhouette.Texture.height,TextureFormat.RGBA32,false);
                    try
                    {
                        RenderTexture.active=silhouette.Texture;mask.ReadPixels(new Rect(0,0,mask.width,mask.height),0,0);mask.Apply();
                        int covered=mask.GetPixels().Count(p=>p.r>.02f);
                        check(covered>40&&covered<mask.width*mask.height*.7f,"dynamic ring mask is populated and not a filled rectangle "+cardId);
                        File.WriteAllBytes(Path.Combine(folder,cardId+"-mask.png"),mask.EncodeToPNG());
                    }
                    finally{AssetRegistry.ReleaseTransient(mask);}
                    root.localRotation=Quaternion.identity;root.localScale=Vector3.one;
                    var plane=NativeActorIdentity.FindChildBySubstring(actor.gameObject,"Highlight_Card_Plane").GetComponent<MeshRenderer>();
                    var original=plane.sharedMaterial;
                    foreach(int stateId in new[]{2,6,10})
                    {
                        var material=NativeActorMaterial.Create(definition.states.First(s=>s.state==stateId).material,assets);
                        try
                        {
                            plane.sharedMaterial=material;material.mainTexture=silhouette.Texture;material.renderQueue=definition.render_queue+definition.queue_offset;
                            plane.gameObject.SetActive(true);var rendered=(RenderTexture)live.Render();
                            var output=new Texture2D(rendered.width,rendered.height,TextureFormat.RGBA32,false);
                            try{RenderTexture.active=rendered;output.ReadPixels(new Rect(0,0,output.width,output.height),0,0);output.Apply();File.WriteAllBytes(Path.Combine(folder,cardId+"-state-"+stateId+".png"),output.EncodeToPNG());}
                            finally{AssetRegistry.ReleaseTransient(output);}
                        }
                        finally{plane.sharedMaterial=original;plane.gameObject.SetActive(false);AssetRegistry.ReleaseTransient(material);}
                    }
                }
                var statePlane=sourcePlane.GetComponent<MeshRenderer>();var stateOwner=statePlane.transform.parent;
                var savedMaterial=statePlane.sharedMaterial;bool savedEnabled=statePlane.enabled,savedActive=sourcePlane.activeSelf;var savedPosition=stateOwner.localPosition;
                using(var controller=new NativeHighlightState(actor,assets,.375f))
                {
                    check(controller.CurrentState==1&&!statePlane.enabled,"source Setup idle hides renderer "+cardId);
                    foreach(int stateId in new[]{2,6,10,28})
                    {
                        check(controller.ChangeState(stateId)&&controller.CurrentState==stateId&&statePlane.enabled&&controller.VisibilityState,"source immediate state switch "+stateId);
                        check(controller.Material.GetFloat("_Seed")==.375f&&controller.Material.mainTexture is RenderTexture&&controller.Material.renderQueue==3000,"state binds source seed/dynamic mask/queue "+stateId);
                    }
                    check(stateOwner.localPosition==new Vector3(0,-.25f,0),"serialized history translation overrides constructor default");
                    var sameTexture=controller.Material.mainTexture;
                    check(controller.ChangeState(28)&&controller.Material.mainTexture==sameTexture,"same state preserves rendered texture");
                    controller.Hide();check(!statePlane.enabled&&controller.VisibilityState,"Hide retains requested visibility");
                    controller.ChangeState(6);check(!statePlane.enabled&&controller.VisibilityState,"state changes remain hidden");
                    controller.Show();check(statePlane.enabled,"Show restores requested visibility");
                    check(controller.ChangeState(29)&&!statePlane.enabled&&!controller.VisibilityState,"no-FSM actor idle disables renderer");
                    controller.Show();check(!statePlane.enabled,"Show cannot enable an idle state");
                    controller.ChangeState(6);check(controller.ChangeState(0)&&!statePlane.enabled,"zero state hides immediately");
                    controller.ChangeState(6);check(!controller.ChangeState(999)&&controller.CurrentState==1&&controller.PreviousState==6&&!statePlane.enabled,"unmatched ordinary state returns false and resets idle");
                }
                check(statePlane.sharedMaterial==savedMaterial&&statePlane.enabled==savedEnabled&&sourcePlane.activeSelf==savedActive&&stateOwner.localPosition==savedPosition,"disposing controller restores original actor ownership");
            }
            visuals.Clear();
        }
        finally
        {
            RenderTexture.active=previous;
            foreach(var obj in new UnityEngine.Object[]{source,blend,target,image})AssetRegistry.ReleaseTransient(obj);
        }
        File.WriteAllText("../work/native-highlight-silhouette-checks.json","{\"passed\":"+passed+",\"frames\":12,\"source_renderer_has_fsm\":false,\"production_routing\":false,\"complete\":false}");
        Debug.Log("NATIVE HIGHLIGHT SILHOUETTE GPU CHECKS PASSED: "+passed);
    }
}
