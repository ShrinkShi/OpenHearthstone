using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeSneakySmokeChecks
{
    static int passed;
    static void Near(Color actual,Color expected,string message)
    {for(int i=0;i<4;i++){if(Mathf.Abs(actual[i]-expected[i])>.003f)throw new Exception("Original sneaky smoke "+message+": "+actual+" expected "+expected);passed++;}}
    public static void Run()
    {
        passed=0;var root=new GameObject("Original sneaky smoke probe");root.transform.position=new Vector3(97000,97000,97000);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform,false);quad.layer=30;
        var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);quad.GetComponent<MeshFilter>().sharedMesh=mesh;
        Action<Color> vertex=c=>mesh.colors=Enumerable.Repeat(c,mesh.vertexCount).ToArray();vertex(new Color(.2f,.8f,.4f,.5f));
        var camera=new GameObject("Original sneaky smoke camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.transform.localPosition=new Vector3(0,0,-5);
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.5f;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        var frame=new RenderTexture(16,16,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);frame.Create();camera.targetTexture=frame;
        var material=new Material(Resources.Load<Shader>("NativeEffect"));quad.GetComponent<Renderer>().sharedMaterial=material;
        var main=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);main.SetPixel(0,0,new Color(.25f,.99f,.7f,.9f));main.Apply();
        var pixels=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;var bias=Shader.GetGlobalVector("_GlobalMipBias");Shader.SetGlobalVector("_GlobalMipBias",Vector4.zero);
        Func<Color> sample=()=>{camera.Render();RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,16,16),0,0);pixels.Apply();return pixels.GetPixel(8,8);};
        try
        {
            material.SetFloat("_OriginalFamily",24);material.SetTexture("_MainTex",main);material.SetColor("_Color",new Color(.2f,.4f,.6f,.8f));material.SetColor("_SecondColor",new Color(.8f,.6f,.4f,.1f));material.SetFloat("_Intensity",2);
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            Near(sample(),new Color(.52f,.84f,1.16f,.2f),"red-mask color interpolation and independent intensity");
            main.SetPixel(0,0,new Color(.25f,0,0,0));main.Apply();vertex(new Color(1,0,0,.5f));material.SetColor("_SecondColor",new Color(.8f,.6f,.4f,1));material.SetFloat("_Clock",1000);material.SetFloat("_AlphaIntensity",100);material.SetFloat("_ColorMul",100);
            Near(sample(),new Color(.52f,.84f,1.16f,.2f),"texture GBA, vertex RGB, second alpha and legacy gains are unused");
            material.SetColor("_Color",new Color(.2f,.4f,.6f,0));Near(sample(),new Color(.4f,.8f,1.2f,0),"zero tint alpha retains shader RGB with transparent mask");
            material.SetColor("_Color",new Color(.2f,.4f,.6f,1));vertex(Color.white);main.SetPixel(0,0,new Color(3,0,0,0));main.Apply();material.SetFloat("_Intensity",1);
            Near(sample(),new Color(2,1,0,3),"unclamped mask extrapolates and preserves HDR alpha");
            material.SetColor("_Color",new Color(.2f,.4f,.6f,.8f));vertex(new Color(1,0,0,.5f));main.SetPixel(0,0,new Color(.25f,0,0,0));main.Apply();material.SetFloat("_Intensity",2);
            material.SetInt("_SrcBlend",5);material.SetInt("_DstBlend",10);material.SetInt("_CaptureSrcAlpha",5);material.SetInt("_CaptureDstAlpha",10);camera.backgroundColor=new Color(.1f,.2f,.3f,.4f);
            Near(sample(),new Color(.184f,.328f,.472f,.36f),"original alpha blend uses alpha-squared source contribution");
        }
        finally
        {
            Shader.SetGlobalVector("_GlobalMipBias",bias);RenderTexture.active=previous;camera.targetTexture=null;
            foreach(var item in new UnityEngine.Object[]{pixels,main,material,frame,mesh,root})AssetRegistry.ReleaseTransient(item);
        }
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var entry=index.entries.First(e=>e.card_id=="EX1_129");var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,entry.file)));
        using(var effect=new NativeEffectInstance(program,assets,width:32,height:32))
        {if(!effect.Nodes.SelectMany(n=>n.GetComponents<Renderer>()).SelectMany(r=>r.sharedMaterials).Any(m=>m.GetFloat("_OriginalFamily")==24))throw new Exception("Original sneaky smoke family is not bound");passed++;}
        Debug.Log("NATIVE SNEAKY SMOKE: "+passed+" GPU checks passed; all-card composition remains incomplete");
        NativeParticleAlphaChecks.Run();
    }
}
