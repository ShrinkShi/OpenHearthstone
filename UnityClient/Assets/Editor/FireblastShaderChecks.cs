using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

// 独立原DXBC数学夹具见work/r005-indicator/SHADER-SAMPLES.md；不是官方画面。
public static class FireblastShaderChecks
{
    static int checks;
    static void Near(Color actual,Color expected,string name)
    {for(int i=0;i<4;i++)if(Mathf.Abs(actual[i]-expected[i])>.0003f)throw new Exception(name+" channel="+i+" actual="+actual+" expected="+expected);checks++;}
    static Texture2D Texture(int width,int height,Color[] colors)
    {var t=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};t.SetPixels(colors);t.Apply();return t;}
    public static void Run()
    {
        checks=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));var root=new GameObject("Fireblast mathematical GPU samples");root.transform.position=new Vector3(70000,70000,70000);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform,false);quad.layer=30;
        var camera=new GameObject("Fireblast sample camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.transform.localPosition=new Vector3(0,0,-5);
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.5f;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        var frame=new RenderTexture(32,32,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);frame.Create();camera.targetTexture=frame;
        var image=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);var material=new Material(Resources.Load<Shader>("NativeEffect"));quad.GetComponent<Renderer>().sharedMaterial=material;
        var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);quad.GetComponent<MeshFilter>().sharedMesh=mesh;
        var textures=new List<Texture2D>();var previous=RenderTexture.active;float previousTime=Shader.GetGlobalFloat("_ShaderTime");
        void ColorFlow(Color value){mesh.colors=Enumerable.Repeat(value,4).ToArray();}
        void UV(Vector2 first,Vector2 second){mesh.uv=Enumerable.Repeat(first,4).ToArray();mesh.uv2=Enumerable.Repeat(second,4).ToArray();}
        Texture2D Make(Color value){var t=Texture(1,1,new[]{value});textures.Add(t);return t;}
        Color Pixel(RenderTexture target){RenderTexture.active=target;image.ReadPixels(new Rect(0,0,32,32),0,0);image.Apply();return image.GetPixel(16,16);}
        Color Sample(){camera.Render();return Pixel(frame);}
        void Blend(int source,int destination){material.SetInt("_SrcBlend",source);material.SetInt("_DstBlend",destination);material.SetInt("_CaptureSrcAlpha",source);material.SetInt("_CaptureDstAlpha",destination);}
        try
        {
            material.SetFloat("_LightingBlend",0);material.SetFloat("_Cull",0);material.SetFloat("_LuminanceAlpha",0);Blend(1,0);UV(new Vector2(.2f,.3f),new Vector2(.7f,.8f));
            material.SetTexture("_MainTex",Make(new Color(.8f,.4f,.2f,.6f)));ColorFlow(new Color(.25f,.5f,.75f,.8f));material.SetColor("_Color",new Color(.5f,1,.25f,.5f));
            material.SetFloat("_OriginalFamily",25);Near(Sample(),new Color(.1f,.2f,.0375f,.24f),"25 vertex/tint alpha");
            camera.backgroundColor=new Color(.2f,.4f,.6f,.7f);Blend(5,10);Near(Sample(),new Color(.176f,.352f,.465f,.5896f),"25 source alpha squared blend");Blend(1,0);camera.backgroundColor=Color.clear;
            material.SetFloat("_OriginalFamily",26);material.SetColor("_Color",Color.clear);Near(Sample(),new Color(.2f,.2f,.15f,.48f),"26 ignores material color");
            material.SetFloat("_OriginalFamily",29);material.SetColor("_Color",new Color(.5f,1,.25f,.5f));Near(Sample(),new Color(.1f,.2f,.0375f,.24f),"29 opaque color multiplication");
            material.SetFloat("_OriginalFamily",27);material.SetTexture("_MainTex",Make(new Color(.8f,.4f,.2f,.5f)));material.SetColor("_Color",new Color(.5f,1,.25f,1));material.SetFloat("_Cutoff",.5f);
            Near(Sample(),new Color(.1f,.2f,.0375f,0),"27 ceil equality retains RGB without discard");material.SetFloat("_Cutoff",.25f);Near(Sample(),new Color(.1f,.2f,.0375f,.8f),"27 ceil positive threshold");
            material.SetFloat("_OriginalFamily",28);ColorFlow(new Color(.25f,.5f,.75f,.5f));UV(new Vector2(.125f,.25f),new Vector2(.5f,.5f));material.SetColor("_Color",Color.white);
            material.SetTexture("_MainTex",Make(new Color(.8f,.4f,.2f,.1f)));material.SetTextureScale("_MainTex",new Vector2(2,1));
            var second=Texture(2,2,new[]{Color.clear,Color.clear,new Color(.4f,.8f,.2f,.6f),Color.clear});textures.Add(second);material.SetTexture("_SecondTex",second);material.SetTextureScale("_SecondTex",new Vector2(.5f,1));material.SetTextureOffset("_SecondTex",new Vector2(0,.25f));
            material.SetColor("_SecondTint",new Color(.5f,.5f,.5f,.5f));material.SetFloat("_BlendIntensity",2);Near(Sample(),new Color(.1655f,.251f,.104f,.575f),"28 distinct second UV and both min branches");
            material.SetFloat("_OriginalFamily",30);UV(new Vector2(.15625f,.3125f),Vector2.zero);ColorFlow(new Color(.9f,.2f,.1f,.01f));material.SetColor("_Color",Color.clear);
            material.SetTexture("_MainTex",Make(new Color(.2f,.3f,.4f,.5f)));var scrollColors=Enumerable.Repeat(Color.clear,16).ToArray();scrollColors[1]=new Color(.4f,.9f,.1f,.01f);var scroll=Texture(4,4,scrollColors);textures.Add(scroll);material.SetTexture("_ScrollingTex",scroll);
            material.SetFloat("_Clock",4);material.SetFloat("_XSpeed",5);material.SetFloat("_YSpeed",.2f);material.SetFloat("_ScaleX",2);material.SetFloat("_ScaleY",.5f);material.SetFloat("_Intensity",2);
            material.SetColor("_tint",new Color(.1f,.5f,1,.9f));material.SetColor("_Fade",new Color(.5f,.75f,1,.1f));material.SetColor("_TintColor",new Color(.8f,.5f,.25f,.6f));Near(Sample(),new Color(.104f,.225f,.25f,.6f),"30 Time.x and red scroll mask");
            material.SetTexture("_MainTex",Make(new Color(.2f,.3f,.4f,0)));Near(Sample(),new Color(.08f,.1125f,.1f,.6f),"30 main alpha controls glow only");
            material.SetTextureScale("_MainTex",Vector2.one);material.SetTexture("_MainTex",Make(new Color(.2f,.4f,.6f,.35f)));material.SetFloat("_OriginalFamily",31);Near(Sample(),new Color(.2f,.4f,.6f,.35f),"31 finite unlit B0 only");
            material.SetFloat("_OriginalFamily",32);if(Mathf.Abs(Sample().a-.35f)>.0003f)throw new Exception("32 source alpha invariant");checks++;
            var renderer=quad.GetComponent<Renderer>();var properties=new MaterialPropertyBlock();
            properties.CopySHCoefficientArraysFrom(new[]{new UnityEngine.Rendering.SphericalHarmonicsL2()});renderer.SetPropertyBlock(properties);
            var diffuse=new NativeDiffuseInputs{mainPosition=new Vector4(0,0,-1,0),mainColor=new Vector4(.2f,.4f,.6f,1),mainVisibility=.5f,globalCount=1,
                lights=new[]{new NativeDiffuseInputs.Light(),new NativeDiffuseInputs.Light{position=new Vector4(0,0,-2,0),color=new Vector4(.5f,.25f,.1f,1),attenuation=new Vector4(.01f,0,.5f,.1f),spotDirection=new Vector4(0,0,-1,0)}},rendererIndices=new[]{1}};
            material.SetColor("_Color",Color.clear);ColorFlow(Color.clear);diffuse.Apply(material);
            Near(Sample(),new Color(.038f,.098f,.1908f,.35f),"32 PS source indexed directional attenuation/spot/main ignores mesh tint");
            material.SetFloat("_OriginalFamily",31);material.SetFloat("_LightingBlend",.25f);Near(Sample(),new Color(.1595f,.3245f,.4977f,.35f),"31 source B blends texture with complete no-shadow lighting");
            diffuse.globalCount=0;diffuse.Apply(material);material.SetFloat("_LightingBlend",1);Near(Sample(),new Color(.02f,.08f,.18f,.35f),"31 count min excludes renderer light despite nonempty indices");
            diffuse.mainVisibility=0;diffuse.globalCount=1;diffuse.rendererIndices=new[]{0};diffuse.lights=new[]{new NativeDiffuseInputs.Light{position=new Vector4(70000,70000,69999,1),color=new Vector4(1,0,0,1),attenuation=new Vector4(.04f,0,0,1)}};
            material.SetTexture("_MainTex",Make(new Color(1,1,1,.35f)));material.SetFloat("_OriginalFamily",32);diffuse.Apply(material);
            float d2=1+2*.015625f*.015625f;float expectedPoint=1/(1+d2)/Mathf.Sqrt(d2);
            Near(Sample(),new Color(expectedPoint,0,0,.35f),"32 PS source point light evaluated at interpolated world position");
            diffuse.vertexLighting=true;diffuse.Apply(material);Near(Sample(),new Color(.4f/Mathf.Sqrt(1.5f),0,0,.35f),"32 VS source point light interpolated after per-vertex evaluation");
            new NativeDiffuseInputs().Apply(material);
            material.SetFloat("_OriginalFamily",33);UV(new Vector2(.2f,.3f),new Vector2(.7f,.8f));ColorFlow(new Color(.5f,.2f,.9f,.01f));material.SetTexture("_MainTex",Make(new Color(.5f,.9f,.1f,.02f)));
            var flareColors=Enumerable.Repeat(Color.clear,16).ToArray();flareColors[12]=new Color(.4f,.9f,.1f,.1f);var flare=Texture(4,4,flareColors);textures.Add(flare);material.SetTexture("_FlareTex",flare);
            material.SetFloat("_Intensity",.5f);material.SetFloat("_FlareIntensity",.5f);material.SetFloat("_ColorIntensity",2);material.SetFloat("_Seed",.4f);Shader.SetGlobalFloat("_ShaderTime",.6f);material.SetFloat("_UflareScale",.35f);material.SetFloat("_VflareScale",.2f);material.SetFloat("_VflareSpeed",2.7f);material.SetFloat("_TexCoordTwoWeight",.25f);material.SetFloat("_ColorAlphaWeight",1);
            material.SetColor("_Color",new Color(.1f,.2f,.3f,.01f));material.SetColor("_HiColor",new Color(.8f,.6f,.4f,.01f));material.SetColor("_Colorize",new Color(.5f,.75f,1,.8f));Near(Sample(),new Color(.103f,.27525f,.528f,.06f),"33 nonlinear vertex red and UV1 flare");
            camera.backgroundColor=new Color(.2f,.4f,.6f,.7f);Blend(5,10);Near(Sample(),new Color(.19418f,.392515f,.59568f,.6616f),"33 original framebuffer alpha blend");
            ColorFlow(new Color(0,.2f,.9f,1));Near(Sample(),camera.backgroundColor,"33 vertex alpha does not create white ring");
            material.SetFloat("_OriginalFamily",34);material.SetColor("_Color",new Color(0,0,0,1));material.SetTexture("_MainTex",Make(new Color(.8f,.5f,.25f,.6f)));camera.backgroundColor=new Color(.4f,.6f,.8f,.7f);Blend(2,0);
            Near(Sample(),new Color(.32f,.3f,.2f,0),"34 exact DstColor Zero");material.SetTexture("_MainTex",Make(new Color(1,1,1,0)));Near(Sample(),new Color(.4f,.6f,.8f,0),"34 white texture preserves destination RGB");
            using(var affine=new NativeAffineCapture(32,32))
            {
                affine.Render(camera,new Dictionary<Material,NativeMaterial>{{material,new NativeMaterial{name="shadow",src=2,dst=0,src_alpha=2,dst_alpha=0}}});
                Near(Pixel(affine.Color),Color.clear,"34 C zero");Near(Pixel(affine.Transmission),new Color(1,1,1,0),"34 K preserves white shadow outside");
            }
            camera.backgroundColor=Color.clear;Blend(1,0);ColorFlow(new Color(.25f,.5f,.75f,.8f));material.SetColor("_Color",new Color(.5f,1,.25f,.5f));
            material.SetTexture("_MainTex",Make(new Color(.8f,.4f,.2f,.6f)));material.SetFloat("_OriginalFamily",41);material.SetFloat("_Intensity",1.5f);material.SetFloat("_AlphaIntensity",.8f);material.SetFloat("_Cutoff",2);
            Near(Sample(),new Color(.15f,.3f,.05625f,.09216f),"41 source AlphaClip exponent and distinct RGB/alpha gains without discard");
            material.SetFloat("_OriginalFamily",42);material.SetFloat("_Intensity",2);Near(Sample(),new Color(.12f,.24f,.045f,.288f),"42 source additive texture alpha multiplies RGB and alpha");
            var bloom=new Material(Resources.Load<Shader>("NativeR2TBloom"));
            try
            {
                bloom.SetFloat("_Cutoff",2);bloom.SetFloat("_AlphaIntensity",.8f);bloom.SetFloat("_Threshold",.1f);bloom.SetFloat("_Intensity",.95f/.9f);bloom.SetVector("_Offset",new Vector4(1.5f/32,1.5f/32,0,0));
                var source=Make(new Color(.8f,.4f,.2f,.6f));Graphics.Blit(source,frame,bloom,0);Near(Pixel(frame),new Color(.7388889f,.3166667f,.1055556f,.1376444f),"R2T bloom threshold exponent and final overwritten gain");
                bloom.SetVector("_Offset",new Vector4(.04f,0,0,0));Graphics.Blit(source,frame,bloom,1);Near(Pixel(frame),new Color(.8f,.4f,.2f,.6f),"R2T bloom four-tap horizontal normalization");
                bloom.SetVector("_Offset",new Vector4(0,.04f,0,0));Graphics.Blit(source,frame,bloom,1);Near(Pixel(frame),new Color(.8f,.4f,.2f,.6f),"R2T bloom four-tap vertical normalization");
            }
            finally{AssetRegistry.ReleaseTransient(bloom);}
            Debug.Log("FIREBLAST SHADERS: "+checks+" mathematical GPU cases passed; dynamic diffuse RGB/official fidelity unverified");
            File.WriteAllText("../work/f005-shader-gpu.json","{\"cases\":"+checks+",\"dynamic_diffuse_rgb_verified\":false}");
        }
        finally
        {Shader.SetGlobalFloat("_ShaderTime",previousTime);RenderTexture.active=previous;camera.targetTexture=null;foreach(var t in textures)AssetRegistry.ReleaseTransient(t);AssetRegistry.ReleaseTransient(mesh);AssetRegistry.ReleaseTransient(material);AssetRegistry.ReleaseTransient(image);AssetRegistry.ReleaseTransient(frame);AssetRegistry.ReleaseTransient(root);}
    }
}
