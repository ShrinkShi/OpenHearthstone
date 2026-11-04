using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeHighlightMaterialChecks
{
    [Serializable] sealed class State {public int state;public NativeMaterial material;}
    [Serializable] sealed class Definition {public State[] states;}
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original highlight: "+message);passed++;};
        var shader=Resources.Load<Shader>("OriginalActorHighlight");check(shader!=null&&shader.isSupported,"source shader compiles");
        var material=new Material(shader);var mesh=new Mesh();var target=new RenderTexture(16,16,24,RenderTextureFormat.ARGBFloat);
        var pixels=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);var mask=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        var flare=new Texture2D(4,4,TextureFormat.RGBAFloat,false,true);flare.filterMode=FilterMode.Point;flare.wrapMode=TextureWrapMode.Repeat;
        flare.SetPixels(Enumerable.Range(0,16).Select(i=>new Color((i+1)*.05f,.9f,.8f,.7f)).ToArray());flare.Apply();
        var go=new GameObject("Original highlight arithmetic fixture");go.layer=30;go.transform.position=new Vector3(65000,0,0);
        var camera=new GameObject("Original highlight arithmetic camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;
        camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;Color background=new Color(.07f,.09f,.11f,.13f);camera.backgroundColor=background;
        camera.transform.position=go.transform.position+Vector3.up*10;camera.transform.rotation=Quaternion.Euler(90,0,0);target.Create();camera.targetTexture=target;
        mesh.vertices=new[]{new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1)};
        mesh.triangles=new[]{0,2,1,2,3,1};mesh.uv=Enumerable.Repeat(new Vector2(.23f,.61f),4).ToArray();mesh.uv2=Enumerable.Repeat(new Vector2(.73f,.19f),4).ToArray();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        material.SetTexture("_MainTex",mask);material.SetTexture("_FlareTex",flare);material.SetTextureScale("_MainTex",new Vector2(.8f,.6f));material.SetTextureOffset("_MainTex",new Vector2(.07f,.11f));
        Color basis=new Color(.2f,.4f,.7f,.8f),hot=new Color(.8f,.6f,.3f,.2f),colorize=new Color(.7f,.9f,.6f,.8f);
        material.SetColor("_Color",basis);material.SetColor("_HiColor",hot);material.SetColor("_Colorize",colorize);
        material.SetFloat("_Intensity",.7f);material.SetFloat("_FlareIntensity",.4f);material.SetFloat("_ColorIntensity",1.3f);
        material.SetFloat("_VflareSpeed",.17f);material.SetFloat("_UflareScale",.8f);material.SetFloat("_VflareScale",.6f);material.SetFloat("_ColorAlphaWeight",.6f);material.SetFloat("_Seed",.23f);
        var previous=RenderTexture.active;float previousTime=Shader.GetGlobalFloat("_ShaderTime");
        try
        {
            foreach(float red in new[]{0f,.3f,1f})foreach(float weight in new[]{0f,.4f,1f})foreach(float time in new[]{0f,.4f,1.2f})foreach(float silhouette in new[]{.12f,.24f})
            {
                mesh.colors=Enumerable.Repeat(new Color(red,.9f,.8f,.7f),4).ToArray();mask.SetPixel(0,0,new Color(silhouette,.7f,.8f,.9f));mask.Apply();
                material.SetFloat("_TexCoordTwoWeight",weight);Shader.SetGlobalFloat("_ShaderTime",time);
                float m=silhouette*.7f*1.2f,t=time+.23f,u=Mathf.LerpUnclamped(.23f*.8f+.07f,.73f*.8f+.07f,weight)*.8f+t*.05f,v=m*.6f+t*.17f;
                int x=Mathf.FloorToInt((u-Mathf.Floor(u))*4),y=Mathf.FloorToInt((v-Mathf.Floor(v))*4);
                float f=(y*4+x+1)*.05f,s=2*(f*red+m)*m*.4f;
                var source=new Color((basis.r+s*(s*hot.r-basis.r))*1.3f,(basis.g+s*(s*hot.g-basis.g))*1.3f,(basis.b+s*(s*hot.b-basis.b))*1.3f,(1+.6f*(red-1))*s)*colorize;
                Color expected=source*source.a+background*(1-source.a);
                camera.Render();RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,16,16),0,0);pixels.Apply();Color actual=pixels.GetPixel(8,8);
                check(Mathf.Max(Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g),Mathf.Abs(actual.b-expected.b),Mathf.Abs(actual.a-expected.a))<.001f,"DXBC RGBA/UV2/ST/seed/time/alpha blend "+red+"/"+weight+"/"+time+"/"+silhouette);
            }
            var assets=new AssetRegistry(Path.GetFullPath("../local"));var definition=JsonUtility.FromJson<Definition>(File.ReadAllText(Path.Combine(assets.Root,"original-minion-highlight.json")));
            check(definition.states.Length==21,"all original Actor state slots preserved");
            check(definition.states.Count(s=>s.material!=null&&!String.IsNullOrEmpty(s.material.shader))==20,"exactly twenty nonempty original state materials");
            foreach(var state in definition.states.Where(s=>s.material!=null&&!String.IsNullOrEmpty(s.material.shader)))
            {
                var source=NativeActorMaterial.Create(state.material,assets);
                try{check(source.shader==shader&&source.GetTexture("_FlareTex")!=null,"exact source state material and flare "+state.state);}
                finally{AssetRegistry.ReleaseTransient(source);}
            }
        }
        finally
        {
            Shader.SetGlobalFloat("_ShaderTime",previousTime);RenderTexture.active=previous;camera.targetTexture=null;
            foreach(var obj in new UnityEngine.Object[]{go,camera.gameObject,mesh,material,target,pixels,mask,flare})AssetRegistry.ReleaseTransient(obj);
        }
        File.WriteAllText("../work/native-highlight-material-checks.json","{\"passed\":"+passed+",\"production_driver\":false,\"complete\":false}");
        Debug.Log("NATIVE HIGHLIGHT MATERIAL GPU CHECKS PASSED: "+passed);
    }
}
