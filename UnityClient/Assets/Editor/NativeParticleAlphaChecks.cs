using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeParticleAlphaChecks
{
    [Serializable] sealed class TextureRow{public string png,raw;public int width,height,format,mips,filter,wrap_u,wrap_v;}
    [Serializable] sealed class TextureAudit{public TextureRow[] textures;}
    static int passed;
    static void Near(Color actual,Color expected,string label)
    {
        for(int i=0;i<4;i++){if(Mathf.Abs(actual[i]-expected[i])>.003f)throw new Exception("Original particle alpha "+label+": "+actual+" expected "+expected);passed++;}
    }
    public static void Run()
    {
        passed=0;var root=new GameObject("Original alpha noise probe");root.transform.position=new Vector3(96000,96000,96000);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform,false);quad.layer=30;
        var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);quad.GetComponent<MeshFilter>().sharedMesh=mesh;
        Action<Color> vertex=c=>mesh.colors=Enumerable.Repeat(c,mesh.vertexCount).ToArray();vertex(new Color(.2f,.9f,.1f,.5f));
        var camera=new GameObject("Original particle alpha camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.transform.localPosition=new Vector3(0,0,-5);
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.5f;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        var frame=new RenderTexture(32,32,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);frame.Create();camera.targetTexture=frame;
        var material=new Material(Resources.Load<Shader>("NativeEffect"));quad.GetComponent<Renderer>().sharedMaterial=material;
        var main=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);main.SetPixel(0,0,new Color(.1f,.2f,.3f,.4f));main.Apply();
        var noise=new Texture2D(2,2,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
        noise.SetPixels(new[]{new Color(.2f,1,1,1),new Color(.4f,0,0,0),new Color(.6f,0,0,0),new Color(.8f,0,0,0)});noise.Apply();
        var pixels=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;var bias=Shader.GetGlobalVector("_GlobalMipBias");Shader.SetGlobalVector("_GlobalMipBias",Vector4.zero);
        Func<Color> sample=()=>{camera.Render();RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();return pixels.GetPixel(16,16);};
        try
        {
            material.SetFloat("_OriginalFamily",23);material.SetTexture("_MainTex",main);material.SetTexture("_AlphaNoise",noise);
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            material.SetColor("_Color",new Color(.8f,.6f,.4f,.1f));material.SetFloat("_AlphaIntensity",.5f);material.SetFloat("_USpeed",.25f);material.SetFloat("_VSpeed",.75f);
            Near(sample(),new Color(.8f,.6f,.4f,.04f),"vertex R/A drives second sample; output RGB ignores textures");
            material.SetFloat("_Clock",1000);material.SetColor("_TintColor",Color.clear);material.SetFloat("_Intensity",100);
            Near(sample(),new Color(.8f,.6f,.4f,.04f),"clock and undeclared legacy gains do not alter original formula");
            material.SetFloat("_USpeed",1.25f);Near(sample(),new Color(.8f,.6f,.4f,.02f),"speed multiplies vertex alpha rather than time");
            material.SetTextureScale("_AlphaNoise",new Vector2(.1f,.1f));material.SetTextureOffset("_AlphaNoise",new Vector2(.9f,.9f));material.SetColor("_Color",new Color(.8f,.6f,.4f,1));
            Near(sample(),new Color(.8f,.6f,.4f,.02f),"noise ST and Color alpha are unused");
            material.SetTextureOffset("_MainTex",new Vector2(.5f,0));Near(sample(),new Color(.8f,.6f,.4f,.04f),"main ST also shifts alpha noise coordinates");
            material.SetTextureOffset("_MainTex",Vector2.zero);material.SetFloat("_Scale",2);Near(sample(),new Color(.8f,.6f,.4f,.06f),"scale applies after vertex red and alpha offsets");
            material.SetFloat("_Scale",1);vertex(new Color(.7f,.1f,.9f,.5f));Near(sample(),new Color(.8f,.6f,.4f,.08f),"particle red drives distortion while green and blue do not tint output");
            vertex(new Color(.2f,.9f,.1f,.5f));material.SetFloat("_AlphaIntensity",50);material.SetColor("_Color",new Color(1.6f,.6f,.4f,0));
            Near(sample(),new Color(1.6f,.6f,.4f,2),"original output is HDR and alpha is not saturated");
            material.SetFloat("_AlphaIntensity",.5f);material.SetColor("_Color",new Color(.8f,.6f,.4f,.1f));material.SetInt("_SrcBlend",5);material.SetInt("_DstBlend",1);material.SetInt("_CaptureSrcAlpha",5);material.SetInt("_CaptureDstAlpha",1);camera.backgroundColor=new Color(.1f,.2f,.3f,.4f);
            Near(sample(),new Color(.116f,.212f,.308f,.4004f),"original SrcAlpha One blend on all channels");
            vertex(new Color(.2f,.9f,.1f,0));Near(sample(),camera.backgroundColor,"zero particle alpha preserves destination");
        }
        finally
        {
            Shader.SetGlobalVector("_GlobalMipBias",bias);RenderTexture.active=previous;camera.targetTexture=null;
            foreach(var item in new UnityEngine.Object[]{pixels,main,noise,material,frame,mesh,root})AssetRegistry.ReleaseTransient(item);
        }
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var originals=JsonUtility.FromJson<TextureAudit>(File.ReadAllText(Path.Combine(assets.Root,"native-smoke-source-textures.json")));
        foreach(var row in originals.textures)
        {
            var texture=assets.SmokeTextureFile(row.png);
            if(texture.width!=row.width||texture.height!=row.height||(int)texture.format!=row.format||texture.mipmapCount!=row.mips||
                (int)texture.filterMode!=row.filter||(int)texture.wrapModeU!=row.wrap_u||(int)texture.wrapModeV!=row.wrap_v||
                !texture.GetRawTextureData().SequenceEqual(File.ReadAllBytes(Path.Combine(assets.Root,row.raw))))throw new Exception("Original smoke texture bytes/mips/sampler differ: "+row.png);
            passed++;
            if(assets.SmokeTextureFile(row.png)!=texture)throw new Exception("Original smoke texture cache did not reuse texture");passed++;
        }
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"228864b1f3e778228ee15fa2.native-effect.json")));
        using(var effect=new NativeEffectInstance(program,assets,width:32,height:32))
        {
            var restored=effect.Nodes.SelectMany(n=>n.GetComponents<Renderer>()).SelectMany(r=>r.sharedMaterials).First(m=>m.GetFloat("_OriginalFamily")==23);
            if(restored.GetTexture("_AlphaNoise")==null)throw new Exception("Original alpha noise texture was not bound");passed++;
            if(restored.GetTexture("_AlphaNoise").wrapModeU!=TextureWrapMode.Repeat||restored.GetTexture("_AlphaNoise").wrapModeV!=TextureWrapMode.Repeat)throw new Exception("Original alpha noise repeat sampler was lost");passed++;
        }
        File.WriteAllText("../work/native-particle-alpha-checks.json","{\"passed\":"+passed+",\"whole_effects_verified\":false}");
        Debug.Log("NATIVE PARTICLE ALPHA: "+passed+" GPU checks passed; full scene lighting and all-card playback remain incomplete");
        NativeShaderRenderChecks.Run();
    }
}
