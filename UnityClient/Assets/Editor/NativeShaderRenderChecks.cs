using System;
using System.IO;
using UnityEngine;

// Frozen samples derived from original DXBC arithmetic, rendered by the GPU.
public static class NativeShaderRenderChecks
{
    static int passed;
    static void Near(Color actual,Color expected,string label)
    {
        for(int i=0;i<4;i++)
        {
            if(Mathf.Abs(actual[i]-expected[i])>.015f)throw new Exception(label+" channel "+i+": "+actual+" expected "+expected);
            passed++;
        }
    }
    public static void Run()
    {
        NativeAdditiveChecks.RunGpu();
        passed=0;
        var root=new GameObject("OriginalShaderSamples");root.transform.position=new Vector3(70000,70000,70000);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform,false);quad.layer=30;
        var camera=new GameObject("OriginalShaderCamera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.transform.localPosition=new Vector3(0,0,-5);
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.5f;camera.cullingMask=1<<30;camera.backgroundColor=Color.clear;camera.clearFlags=CameraClearFlags.SolidColor;
        var frame=new RenderTexture(32,32,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);frame.Create();camera.targetTexture=frame;
        var material=new Material(Resources.Load<Shader>("NativeEffect"));quad.GetComponent<Renderer>().sharedMaterial=material;
        var texture=new Texture2D(1,1,TextureFormat.RGBA32,false,true);texture.SetPixel(0,0,new Color(.2f,.4f,.6f,.25f));texture.Apply();
        var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);quad.GetComponent<MeshFilter>().sharedMesh=mesh;
        mesh.colors=new[]{new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f)};
        var pixels=new Texture2D(32,32,TextureFormat.RGBA32,false,true);var previous=RenderTexture.active;
        Func<Color> sample=()=>{camera.Render();RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();return pixels.GetPixel(16,16);};
        try
        {
            material.SetTexture("_MainTex",texture);material.SetColor("_Color",new Color(.6f,.8f,1,.5f));material.SetFloat("_ColorMul",1.75f);
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetFloat("_LuminanceAlpha",0);
            material.SetFloat("_OriginalFamily",1);Near(sample(),new Color(.105f,.392f,.945f,.175f),"Additive + 1 original RGBA multiplier");
            material.SetFloat("_Intensity",12.5f);Near(sample(),new Color(.105f,.392f,.945f,.175f),"Additive + 1 ignores undeclared Intensity");
            material.SetFloat("_OriginalFamily",2);Near(sample(),new Color(.12f,.448f,1,.2f),"Additive original vertex factor two");
            material.SetFloat("_OriginalFamily",3);material.SetFloat("_Intensity",.75f);Near(sample(),new Color(.09f,.336f,.81f,.075f),"AddMultiply original alpha subtraction");
            material.SetFloat("_SolidColor",1);Near(sample(),new Color(.6f,.8f,1,.5f),"UnlitColor ignores texture and vertex color");
            material.SetFloat("_SolidColor",0);material.SetFloat("_OriginalFamily",1);material.SetFloat("_LuminanceAlpha",1);
            Near(sample(),new Color(.105f,.392f,.945f,.165375f),"board alpha compatibility is distinct from original output");
            material.SetFloat("_OriginalFamily",20);material.SetFloat("_Intensity",1.25f);material.SetFloat("_Alpha",.6f);
            material.SetColor("_Color",new Color(.6f,.8f,1,.5f));material.SetColor("_Color2",new Color(.4f,.3f,.2f,.25f));
            Near(sample(),new Color(.12f,.1425f,.165f,.18f),"HolyRender original R/G decode, independent of vertex color and captured alpha");
            material.SetFloat("_Clip",0);Near(sample(),new Color(.12f,.1425f,.165f,.18f),"HolyRender ignores serialized Clip absent from compiled instructions");
            material.SetFloat("_Alpha",0);Near(sample(),Color.clear,"HolyRender zero Alpha removes RGB and alpha");
            material.SetFloat("_Alpha",.6f);material.SetFloat("_OriginalFamily",1);material.SetFloat("_Intensity",.75f);
            material.SetFloat("_OriginalFamily",21);
            Near(sample(),new Color(.6f,.76f,.92f,.84f),"HeroMask original alpha-weighted vertex RGBA, ignores texture and tint");
            camera.backgroundColor=new Color(.4f,.6f,.8f,1);material.SetInt("_SrcBlend",0);material.SetInt("_DstBlend",3);
            material.SetInt("_CaptureSrcAlpha",0);material.SetInt("_CaptureDstAlpha",3);
            Near(sample(),new Color(.24f,.456f,.736f,.84f),"HeroMask original destination modulation including alpha");
            material.SetFloat("_OriginalFamily",22);material.SetColor("_Color",new Color(.2f,.3f,.4f,.25f));
            camera.backgroundColor=Color.clear;material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);
            material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            Near(sample(),new Color(.2f,.42f,.72f,.4f),"AdditiveVertColor original doubled tinted vertex, no texture sample");
            camera.backgroundColor=new Color(.1f,.2f,.3f,1);material.SetInt("_SrcBlend",5);material.SetInt("_DstBlend",1);
            material.SetInt("_CaptureSrcAlpha",0);material.SetInt("_CaptureDstAlpha",1);
            Near(sample(),new Color(.18f,.368f,.588f,1),"AdditiveVertColor original source alpha contribution preserves capture destination opacity");
            camera.backgroundColor=Color.clear;material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);
            material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            material.SetColor("_Color",new Color(.6f,.8f,1,.5f));material.SetFloat("_OriginalFamily",1);
            var pattern=new Texture2D(2,1,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
            try
            {
                pattern.SetPixels(new[]{new Color(.2f,.4f,.6f,.25f),new Color(.6f,.2f,.1f,.75f)});pattern.Apply();
                material.SetTexture("_MainTex",pattern);material.SetTextureScale("_MainTex",Vector2.zero);material.SetTextureOffset("_MainTex",new Vector2(.1f,0));
                material.SetFloat("_LuminanceAlpha",0);material.SetFloat("_OriginalFamily",1);
                Near(sample(),new Color(.315f,.196f,.1575f,.525f),"Additive + 1 raw UV ignores material ST");
                material.SetFloat("_OriginalFamily",2);
                Near(sample(),new Color(.12f,.448f,1,.2f),"Additive compiled vertex applies material ST");
            }
            finally{AssetRegistry.ReleaseTransient(pattern);}
            material.SetTexture("_MainTex",texture);material.SetTextureScale("_MainTex",Vector2.one);material.SetTextureOffset("_MainTex",Vector2.zero);
            material.SetFloat("_OriginalFamily",2);material.SetInt("_SrcBlend",5);material.SetInt("_DstBlend",1);material.SetInt("_CaptureSrcAlpha",0);material.SetInt("_CaptureDstAlpha",1);
            Near(sample(),new Color(.024f,.0896f,.216f,0),"additive board capture carries energy with zero opacity");
            var composite=new Material(Resources.Load<Shader>("NativeEffectComposite"));
            var combined=new RenderTexture(32,32,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);combined.Create();
            Func<Color> overBackground=()=>
            {
                RenderTexture.active=combined;GL.Clear(false,true,new Color(.15f,.1f,.05f,1));
                Graphics.Blit(frame,combined,composite);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();return pixels.GetPixel(16,16);
            };
            try
            {
                Near(overBackground(),new Color(.174f,.1896f,.266f,1),"additive composite equals original destination-plus-source blend");
                material.SetFloat("_OriginalFamily",3);material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",10);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",10);
                Near(sample(),new Color(.09f,.336f,.81f,.075f),"AddMultiply capture keeps original premultiplied contribution");
                Near(overBackground(),new Color(.22875f,.4285f,.85625f,1),"AddMultiply composite equals original destination factor");
            }
            finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(composite);AssetRegistry.ReleaseTransient(combined);}
            var distorted=new Texture2D(2,1,TextureFormat.RGBA32,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
            var displacement=new Texture2D(2,1,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
            try
            {
                distorted.SetPixels(new[]{new Color(.2f,.4f,.6f,.25f),new Color(.6f,.2f,.1f,.75f)});distorted.Apply();
                displacement.SetPixels(new[]{new Color(1,.5f,0,1),new Color(.5f,.5f,0,1)});displacement.Apply();
                material.SetFloat("_OriginalFamily",4);material.SetFloat("_LightingBlend",0);material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);
                material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);material.SetFloat("_LuminanceAlpha",0);
                material.SetTexture("_MainTex",distorted);material.SetTextureScale("_MainTex",Vector2.zero);material.SetTextureOffset("_MainTex",new Vector2(.25f,0));
                material.SetTexture("_FxTex",displacement);material.SetTextureScale("_FxTex",Vector2.zero);material.SetTextureOffset("_FxTex",new Vector2(.75f,0));
                material.SetFloat("_FxIntensity",0);material.SetFloat("_Clock",0);
                Near(sample(),new Color(.045f,.168f,.405f,.0375f),"Distortion original extra tint alpha in vertex");
                material.SetFloat("_FxIntensity",2);
                Near(sample(),new Color(.135f,.084f,.0675f,0),"Distortion shifts UV using texel size and ignores FxTex ST");
                material.SetFloat("_FxScaleX",2);
                Near(sample(),new Color(.045f,.168f,.405f,.0375f),"Distortion has independent FX scale");
                material.SetFloat("_FxScaleX",1);material.SetFloat("_FxIntensity",0);material.SetFloat("_ScrollX",1);material.SetFloat("_Clock",10);
                Near(sample(),new Color(.135f,.084f,.0675f,0),"Distortion original time.x equals seconds divided by twenty");
                material.SetFloat("_ScrollX",0);material.SetFloat("_FxIntensity",2);material.SetFloat("_FxScrollX",1);
                Near(sample(),new Color(.045f,.168f,.405f,.0375f),"Distortion has independent FX scroll");
            }
            finally{AssetRegistry.ReleaseTransient(distorted);AssetRegistry.ReleaseTransient(displacement);}
            MultiplySamples(material,sample,camera);
            FXCoordinateSamples(material,sample,camera);
            DissolveSamples(material,sample);
            material.SetFloat("_OriginalFamily",11);material.SetColor("_Color",Color.magenta);
            Near(sample(),new Color(0,0,0,1),"Utility Black constant opaque output ignores texture and tint");
            ExtendedFXSamples(material,camera);
            CommonFXSamples(material,sample);
            CommonFXAddSamples(material,sample,mesh);
            CommonFXDissolveSamples(material,sample,mesh);
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;
            foreach(var value in new UnityEngine.Object[]{pixels,texture,material,mesh,frame,root})AssetRegistry.ReleaseTransient(value);
        }
        File.WriteAllText("../work/native-shader-render-checks.json","{\"passed\":"+passed+"}");Debug.Log("ORIGINAL SHADER GPU CHECKS PASSED: "+passed);
    }
    static void MultiplySamples(Material material,Func<Color> sample,Camera camera)
    {
        var texture=new Texture2D(2,1,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
        try
        {
            texture.SetPixels(new[]{new Color(.2f,.4f,.6f,.25f),new Color(.6f,.2f,.1f,.75f)});texture.Apply();
            material.SetTexture("_MainTex",texture);material.SetTextureScale("_MainTex",Vector2.zero);material.SetTextureOffset("_MainTex",new Vector2(.25f,0));
            material.SetFloat("_OriginalFamily",7);material.SetFloat("_LightingBlend",0);material.SetFloat("_Intensity",.75f);material.SetColor("_Color",new Color(.6f,.8f,1,.5f));
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            Near(sample(),new Color(.928375f,.9376f,.955375f,.930625f),"Multiply original per-channel factor and quadratic opacity");
            material.SetColor("_TintColor",Color.magenta);material.SetFloat("_ColorMul",17);Near(sample(),new Color(.928375f,.9376f,.955375f,.930625f),"Multiply ignores legacy tint and undeclared ColorMul");
            material.SetTextureOffset("_MainTex",new Vector2(.75f,0));Near(sample(),new Color(.805375f,.7939f,.7901875f,.825625f),"Multiply original vertex applies MainTex ST");
            material.SetTextureOffset("_MainTex",new Vector2(.25f,0));material.SetInt("_SrcBlend",0);material.SetInt("_DstBlend",3);material.SetInt("_CaptureSrcAlpha",0);material.SetInt("_CaptureDstAlpha",3);
            camera.backgroundColor=new Color(.15f,.1f,.05f,.8f);Near(sample(),new Color(.13925625f,.09376f,.04776875f,.7445f),"Multiply original Zero SrcColor destination blend RGBA");
            material.SetColor("_Color",new Color(.6f,.8f,1,0));Near(sample(),camera.backgroundColor,"Multiply zero opacity leaves destination intact");
        }
        finally{camera.backgroundColor=Color.clear;AssetRegistry.ReleaseTransient(texture);}
    }
    static void FXCoordinateSamples(Material material,Func<Color> sample,Camera camera)
    {
        var main=new Texture2D(2,1,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
        var effect=new Texture2D(2,1,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
        try
        {
            main.SetPixels(new[]{new Color(.6f,.2f,.1f,.75f),new Color(.2f,.4f,.6f,.25f)});main.Apply();
            effect.SetPixels(new[]{new Color(.25f,.5f,.75f,.8f),new Color(.5f,.25f,.125f,.4f)});effect.Apply();
            material.SetTexture("_MainTex",main);material.SetTextureScale("_MainTex",Vector2.zero);material.SetTextureOffset("_MainTex",new Vector2(.25f,0));
            material.SetTexture("_FxTex",effect);material.SetTextureScale("_FxTex",Vector2.zero);material.SetTextureOffset("_FxTex",new Vector2(.75f,0));
            material.SetColor("_Color",new Color(.6f,.8f,1,.5f));material.SetFloat("_LightingBlend",0);material.SetFloat("_Intensity",.75f);material.SetFloat("_FxIntensity",2);
            material.SetFloat("_Clock",0);material.SetFloat("_ScrollX",0);material.SetFloat("_ScrollY",0);material.SetFloat("_ScaleX",1);material.SetFloat("_ScaleY",1);
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            material.SetFloat("_OriginalFamily",8);
            Near(sample(),new Color(.045f,.336f,1,.12f),"FXWorld raw main UV, clip XY effect UV and RGBA saturation");
            material.SetFloat("_ScaleX",0);material.SetFloat("_ScrollX",1);material.SetFloat("_Clock",10);
            Near(sample(),new Color(.09f,.168f,.2025f,.06f),"FXWorld Time.x scroll ignores FxTex ST");
            material.SetFloat("_Clock",0);material.SetFloat("_ScrollX",0);material.SetFloat("_ScaleX",4);camera.orthographic=false;
            Near(sample(),new Color(.09f,.168f,.2025f,.06f),"FXWorld uses clip XY before perspective divide");
            camera.orthographic=true;
            material.SetFloat("_OriginalFamily",9);material.SetFloat("_Clock",0);material.SetFloat("_ScrollX",0);material.SetFloat("_ScaleX",1);
            Near(sample(),new Color(.135f,.042f,.016875f,0),"FXScrolling main ST, raw FX UV and extra vertex tint alpha");
            material.SetTextureScale("_MainTex",Vector2.one);material.SetTextureOffset("_MainTex",Vector2.zero);material.SetFloat("_ScaleX",0);
            Near(sample(),new Color(.0225f,.168f,.6075f,.06f),"FXScrolling independent effect scale and alpha subtraction");
            material.SetFloat("_ScrollX",1);material.SetFloat("_Clock",10);
            Near(sample(),new Color(.045f,.084f,.10125f,.03f),"FXScrolling original seconds/20 clock");
        }
        finally{camera.orthographic=true;AssetRegistry.ReleaseTransient(main);AssetRegistry.ReleaseTransient(effect);}
    }
    static void DissolveSamples(Material material,Func<Color> sample)
    {
        var texture=new Texture2D(2,1,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
        try
        {
            texture.SetPixels(new[]{new Color(.4f,.9f,.8f,.1f),new Color(.1f,.9f,.8f,1)});texture.Apply();
            material.SetTexture("_MainTex",texture);material.SetTextureScale("_MainTex",Vector2.zero);material.SetTextureOffset("_MainTex",new Vector2(.25f,0));
            material.SetFloat("_OriginalFamily",10);material.SetFloat("_Intensity",.75f);material.SetFloat("_Alpha",2);material.SetFloat("_DissolveIntensity",2);
            material.SetColor("_Color",new Color(.6f,.8f,1,.01f));material.SetFloat("_LightingBlend",1);
            Near(sample(),new Color(.072f,.1344f,.216f,.128f),"Dissolve red-only mask and preserved vertex alpha");
            material.SetColor("_Color",new Color(.6f,.8f,1,1));Near(sample(),new Color(.072f,.1344f,.216f,.128f),"Dissolve ignores tint alpha and ambient lighting");
            material.SetFloat("_DissolveIntensity",20);Near(sample(),new Color(.18f,.336f,.54f,.32f),"Dissolve edge upper bound precedes mask multiplication");
            material.SetTextureOffset("_MainTex",new Vector2(.75f,0));Near(sample(),Color.clear,"Dissolve below edge becomes transparent");
            material.SetFloat("_DissolveIntensity",-2);Near(sample(),new Color(.009f,.0168f,.027f,.016f),"Dissolve negative gain uses upper-only min rather than saturate");
            material.SetTextureOffset("_MainTex",new Vector2(.25f,0));material.SetFloat("_DissolveIntensity",2);material.SetFloat("_Alpha",0);
            Near(sample(),new Color(0,0,0,.128f),"Dissolve Alpha controls RGB, not destination opacity");
        }
        finally{material.SetFloat("_LightingBlend",0);AssetRegistry.ReleaseTransient(texture);}
    }
    static void ExtendedFXSamples(Material material,Camera camera)
    {
        var main=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        var effect=new Texture2D(2,2,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
        var frame=new RenderTexture(32,32,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);frame.Create();
        var pixels=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);var old=camera.targetTexture;var active=RenderTexture.active;
        Func<Color> sample=()=>{camera.Render();RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();return pixels.GetPixel(16,16);};
        try
        {
            camera.targetTexture=frame;main.SetPixel(0,0,new Color(.2f,.4f,.6f,.25f));main.Apply();
            effect.SetPixels(new[]{new Color(.25f,.5f,.75f,.8f),new Color(.5f,.25f,.125f,.4f),new Color(.1f,.2f,.3f,.6f),new Color(.8f,.6f,.4f,.2f)});effect.Apply();
            material.SetTexture("_MainTex",main);material.SetTextureScale("_MainTex",Vector2.zero);material.SetTextureOffset("_MainTex",new Vector2(.25f,.25f));
            material.SetTexture("_FxTex",effect);material.SetTextureScale("_FxTex",Vector2.zero);material.SetTextureOffset("_FxTex",Vector2.one*.75f);
            material.SetColor("_Color",new Color(.6f,.8f,1,.5f));material.SetFloat("_LightingBlend",0);material.SetFloat("_Intensity",.75f);material.SetFloat("_FxIntensity",2);
            material.SetFloat("_ScaleX",1);material.SetFloat("_ScaleY",1);material.SetFloat("_ScrollX",0);material.SetFloat("_ScrollY",0);material.SetFloat("_Clock",0);
            material.SetFloat("_OriginalFamily",12);
            Near(sample(),new Color(.045f,.336f,1.215f,.12f),"Manual FX uses main ST and preserves unsaturated HDR");
            material.SetFloat("_Clock",999);Near(sample(),new Color(.045f,.336f,1.215f,.12f),"Manual FX offsets do not use time or FxTex ST");
            material.SetFloat("_ScrollX",.5f);Near(sample(),new Color(.09f,.168f,.2025f,.06f),"Manual FX direct offset");
            material.SetFloat("_ScrollX",0);material.SetColor("_Color",new Color(.6f,.8f,1,.1f));
            Near(sample(),new Color(.045f,.336f,1.215f,-.072f),"Manual FX original negative alpha is not clamped");
            material.SetColor("_Color",new Color(.6f,.8f,1,.5f));material.SetFloat("_OriginalFamily",13);material.SetFloat("_Clock",0);material.SetFloat("_RotPivotX",.3f);material.SetFloat("_RotPivotY",.3f);
            Near(sample(),new Color(.0225f,.168f,.6075f,.06f),"Rotation raw UV and vertex tint alpha");
            material.SetFloat("_FxIntensity",8);Near(sample(),new Color(.09f,.672f,2.43f,.24f),"Rotation preserves unsaturated HDR output");material.SetFloat("_FxIntensity",2);
            material.SetFloat("_Clock",10);material.SetFloat("_RotSpeed",Mathf.PI);
            Near(sample(),new Color(.009f,.0672f,.243f,.045f),"Rotation radians at seconds/20 and original clockwise matrix");
            material.SetFloat("_RotSpeed",-Mathf.PI);Near(sample(),new Color(.045f,.084f,.10125f,.03f),"Rotation signed speed");
            material.SetFloat("_OriginalFamily",14);material.SetTexture("_FxTex1",effect);material.SetTexture("_FxTex2",effect);
            material.SetFloat("_FxIntensity1",2);material.SetFloat("_FxIntensity2",3);material.SetFloat("_Scale1X",0);material.SetFloat("_Scale1Y",0);material.SetFloat("_Scale2X",0);material.SetFloat("_Scale2Y",0);
            material.SetFloat("_Scroll1X",0);material.SetFloat("_Scroll1Y",1);material.SetFloat("_Scroll2X",0);material.SetFloat("_Scroll2Y",0);
            Near(sample(),new Color(.027f,.1008f,.18225f,.108f),"TwoLayer original second sampler uses first Y and second X");
            material.SetFloat("_Scroll2Y",999);material.SetFloat("_Scale2Y",99);
            Near(sample(),new Color(.027f,.1008f,.18225f,.108f),"TwoLayer compiled second Y coordinate is unused");
            material.SetTexture("_FxTex1",Texture2D.whiteTexture);material.SetTexture("_FxTex2",Texture2D.whiteTexture);material.SetFloat("_Intensity",100);
            Near(sample(),Color.white,"TwoLayer final RGBA saturation differs from Manual and Rotation");
        }
        finally{camera.targetTexture=old;RenderTexture.active=active;foreach(var value in new UnityEngine.Object[]{main,effect,frame,pixels})AssetRegistry.ReleaseTransient(value);}
    }
    static void CommonFXSamples(Material material,Func<Color> sample)
    {
        var main=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        var second=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        try
        {
            main.SetPixel(0,0,new Color(.2f,.4f,.6f,.25f));main.Apply();second.SetPixel(0,0,new Color(.25f,.5f,0,1));second.Apply();
            material.SetTexture("_MainTex",main);material.SetTexture("_SecondTex",second);
            material.SetTextureScale("_MainTex",Vector2.one);material.SetTextureOffset("_MainTex",Vector2.zero);
            material.SetFloat("_OriginalFamily",5);material.SetFloat("_SolidColor",0);material.SetColor("_Color",new Color(.6f,.8f,1,.5f));
            material.SetFloat("_AlphaMul",1.5f);material.SetFloat("_ColorMul",1);material.SetFloat("_ColorAdd",.1f);material.SetFloat("_PremultipliedRGBTexture",1);
            material.SetFloat("_MiddlePointPos",.5f);material.SetColor("_LastColor",Color.black);material.SetColor("_MidColor",new Color(.5f,.5f,.5f,1));material.SetColor("_WhiteColor",Color.white);
            material.SetVector("_SecondTexSpeed_NoiseForce",Vector4.zero);material.SetFloat("_ClampUV",0);material.SetFloat("_SecondTexClampUV",0);material.SetFloat("_SwapUV",0);material.SetFloat("_SecondTexSwapUV",0);
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            // Original VS: tint=(.3,.56,.9,.4), max HDR term=.8;
            // output vertex=(.36,.672,1.08,.12). PS ramp at .5 is gray .5.
            Near(sample(),new Color(.054f,.168f,.378f,.03f),"CommonFX original ramp/AlphaMul and HDR vertex transform");
            material.SetFloat("_OriginalFamily",19);
            Near(sample(),new Color(.108f,.336f,.756f,.024f),"CommonFX None PS1202 omits ramp, red drives alpha");
            main.SetPixel(0,0,new Color(.2f,.4f,.6f,.9f));main.Apply();
            Near(sample(),new Color(.108f,.336f,.756f,.024f),"CommonFX None ignores texture stored alpha");
            material.SetFloat("_PremultipliedRGBTexture",0);
            Near(sample(),new Color(.0216f,.0672f,.1512f,.024f),"CommonFX None red drives RGB premultiplied floor");
            material.SetFloat("_ColorMul",2);
            Near(sample(),new Color(.0432f,.1344f,.3024f,.024f),"CommonFX None ColorMul affects RGB only");
            material.SetColor("_MidColor",Color.magenta);material.SetColor("_WhiteColor",Color.red);material.SetColor("_LastColor",Color.green);
            material.SetFloat("_Intensity",99);material.SetFloat("_LightingBlend",1);
            Near(sample(),new Color(.0432f,.1344f,.3024f,.024f),"CommonFX None ignores ramp and undeclared intensity/lighting");
            material.SetFloat("_LightingBlend",0);material.SetFloat("_ColorMul",1);material.SetFloat("_PremultipliedRGBTexture",1);
            material.SetColor("_LastColor",Color.black);material.SetColor("_MidColor",new Color(.5f,.5f,.5f,1));material.SetColor("_WhiteColor",Color.white);
            main.SetPixel(0,0,new Color(.2f,.4f,.6f,.25f));main.Apply();
            material.SetFloat("_OriginalFamily",15);
            Near(sample(),new Color(.054f,.168f,.378f,.024f),"original RGBR PS 1206 takes alpha from red");
            main.SetPixel(0,0,new Color(.2f,.4f,.6f,.9f));main.Apply();
            Near(sample(),new Color(.054f,.168f,.378f,.024f),"original RGBR ignores stored texture alpha");
            material.SetFloat("_PremultipliedRGBTexture",0);
            Near(sample(),new Color(.00216f,.00672f,.01512f,.024f),"original RGBR red also drives premultiplied floor and ramp");
            material.SetFloat("_PremultipliedRGBTexture",1);material.SetFloat("_OriginalFamily",5);
            main.SetPixel(0,0,new Color(.2f,.4f,.6f,.25f));main.Apply();
            material.SetFloat("_AlphaMul",.5f);
            Near(sample(),new Color(.018f,.056f,.126f,.01f),"CommonFX original AlphaMul affects RGB and alpha");
            material.SetFloat("_AlphaMul",1.5f);material.SetFloat("_PremultipliedRGBTexture",0);
            Near(sample(),new Color(.003375f,.0105f,.023625f,.03f),"CommonFX original premultiplied floor also changes ramp input");
            material.SetFloat("_PremultipliedRGBTexture",1);material.SetFloat("_OriginalFamily",6);material.SetColor("_Color2",new Color(0,0,0,.8f));
            Near(sample(),new Color(.0216f,.0672f,.1512f,.012f),"CommonFX RGBR mask uses red and Color2 alpha");
            material.SetFloat("_ColorMul",2);material.SetFloat("_Intensity",20);
            Near(sample(),new Color(.0432f,.1344f,.3024f,.012f),"CommonFX ColorMul affects RGB; undeclared Intensity does not");
            var pattern=new Texture2D(2,1,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
            var noise=new Texture2D(2,1,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat};
            try
            {
                pattern.SetPixels(new[]{new Color(.2f,.4f,.6f,.25f),new Color(.6f,.2f,.1f,.75f)});pattern.Apply();
                noise.SetPixels(new[]{new Color(.25f,.5f,0,1),new Color(.5f,.5f,0,1)});noise.Apply();
                material.SetTexture("_MainTex",pattern);material.SetTexture("_SecondTex",noise);material.SetFloat("_OriginalFamily",5);
                material.SetColor("_Color",new Color(.25f,.25f,.25f,1));material.SetFloat("_AlphaMul",.5f);material.SetFloat("_ColorMul",1);material.SetFloat("_ColorAdd",0);
                foreach(string name in new[]{"_LastColor","_MidColor","_WhiteColor"})material.SetColor(name,Color.white);
                material.SetTextureScale("_MainTex",Vector2.zero);material.SetTextureOffset("_MainTex",new Vector2(.75f,.25f));
                material.SetTextureScale("_SecondTex",Vector2.zero);material.SetTextureOffset("_SecondTex",new Vector2(.25f,0));
                material.SetFloat("_Clock",0);material.SetVector("_SecondTexSpeed_NoiseForce",Vector4.zero);
                Near(sample(),new Color(.06f,.028f,.018f,.3f),"CommonFX original main ST");
                material.SetVector("_SecondTexSpeed_NoiseForce",new Vector4(0,0,1,0));
                Near(sample(),new Color(.02f,.056f,.108f,.1f),"CommonFX noise offsets main UV using signed second RG");
                material.SetTextureOffset("_SecondTex",new Vector2(.75f,0));
                Near(sample(),new Color(.06f,.028f,.018f,.3f),"CommonFX original second ST controls noise sample");
                material.SetTextureOffset("_SecondTex",new Vector2(.25f,0));material.SetVector("_SecondTexSpeed_NoiseForce",new Vector4(1,0,1,0));material.SetFloat("_Clock",.25f);
                Near(sample(),new Color(.06f,.028f,.018f,.3f),"CommonFX original time.z scrolls second UV at twice seconds");
                material.SetVector("_SecondTexSpeed_NoiseForce",Vector4.zero);material.SetFloat("_SwapUV",1);
                Near(sample(),new Color(.02f,.056f,.108f,.1f),"CommonFX original main UV swap");
            }
            finally{AssetRegistry.ReleaseTransient(pattern);AssetRegistry.ReleaseTransient(noise);}
        }
        finally{AssetRegistry.ReleaseTransient(main);AssetRegistry.ReleaseTransient(second);}
    }
    static void CommonFXAddSamples(Material material,Func<Color> sample,Mesh mesh)
    {
        var main=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        var second=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        try
        {
            var vertex=new Color(.5f,.5f,.5f,.8f);mesh.colors=new[]{vertex,vertex,vertex,vertex};
            main.SetPixel(0,0,new Color(.2f,.3f,.4f,.5f));main.Apply();
            second.SetPixel(0,0,new Color(.25f,.8f,.7f,.9f));second.Apply();
            material.SetTexture("_MainTex",main);material.SetTexture("_SecondTex",second);
            material.SetTextureScale("_MainTex",Vector2.one);material.SetTextureOffset("_MainTex",Vector2.zero);
            material.SetTextureScale("_SecondTex",Vector2.one);material.SetTextureOffset("_SecondTex",Vector2.zero);
            material.SetFloat("_OriginalFamily",46);material.SetFloat("_SolidColor",0);
            material.SetColor("_Color",new Color(.6f,.8f,1,.5f));material.SetColor("_Color2",new Color(.4f,.2f,.6f,.25f));
            material.SetFloat("_AlphaMul",1.5f);material.SetFloat("_ColorAdd",.1f);material.SetFloat("_ColorMul",2);material.SetFloat("_ColorMul2",2);
            material.SetFloat("_PremultipliedRGBTexture",.1f);material.SetFloat("_ClampUV",0);material.SetFloat("_SecondTexClampUV",0);
            material.SetFloat("_SwapUV",0);material.SetFloat("_SecondTexSwapUV",0);material.SetVector("_SecondTexSpeed_NoiseForce",Vector4.zero);material.SetFloat("_Clock",0);
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            Near(sample(),new Color(.1224f,.22272f,.4008f,.33f),"CommonFX RGBR additive uses premultiplied body and Color2 second-texture contribution");

            main.SetPixel(0,0,new Color(.8f,.6f,.4f,0));main.Apply();second.SetPixel(0,0,Color.clear);second.Apply();
            material.SetFloat("_PremultipliedRGBTexture",0);material.SetColor("_Color2",Color.clear);
            Near(sample(),Color.clear,"CommonFX RGBR additive transparent texels do not leak RGB under One/OneMinusSrcAlpha blending");
        }
        finally{AssetRegistry.ReleaseTransient(main);AssetRegistry.ReleaseTransient(second);}
    }
    static void CommonFXDissolveSamples(Material material,Func<Color> sample,Mesh mesh)
    {
        var main=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        var second=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        try
        {
            const float vertexAlpha=.8f,smoothness=.1f,factor=.43f,premultipliedFloor=.1f,colorMul=2,colorMul2=2,alphaMul=1;
            var vertex=new Color(.5f,.5f,.5f,vertexAlpha);mesh.colors=new[]{vertex,vertex,vertex,vertex};
            main.SetPixel(0,0,new Color(.2f,.3f,.4f,.5f));main.Apply();
            material.SetTexture("_MainTex",main);material.SetTextureScale("_MainTex",Vector2.one);material.SetTextureOffset("_MainTex",Vector2.zero);
            material.SetTexture("_SecondTex",second);material.SetTextureScale("_SecondTex",Vector2.one);material.SetTextureOffset("_SecondTex",Vector2.zero);
            material.SetFloat("_OriginalFamily",44);material.SetFloat("_SolidColor",0);
            material.SetColor("_Color",Color.white);material.SetColor("_Color2",new Color(.2f,.4f,.6f,1));
            material.SetFloat("_AlphaMul",alphaMul);material.SetFloat("_ColorAdd",.1f);material.SetFloat("_ColorMul",colorMul);material.SetFloat("_ColorMul2",colorMul2);
            material.SetFloat("_PremultipliedRGBTexture",premultipliedFloor);material.SetFloat("_DissolveFactor",factor);
            material.SetFloat("_DissolveSmoothness",smoothness);material.SetFloat("_MinAlpha",0);material.SetFloat("_IgnoreVertexAlpha",0);
            material.SetFloat("_ClampUV",0);material.SetFloat("_SwapUV",0);material.SetFloat("_SecondTexClampUV",0);material.SetFloat("_SecondTexSwapUV",0);
            material.SetVector("_SecondTexSpeed_NoiseForce",Vector4.zero);material.SetFloat("_Clock",0);
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);

            float vertexDissolve=Mathf.Max(1-vertexAlpha,0)*(2*smoothness+1)-smoothness;
            float threshold=2*(vertexDissolve+factor)-1;
            Func<float,Color> expected=mask=>
            {
                float dissolve=Mathf.Pow(Mathf.Clamp01(mask-threshold),10*smoothness);
                float edge=.5f*Mathf.Clamp01(dissolve*.5f*1000000)*Mathf.Pow(1-dissolve,10)*colorMul2*vertexAlpha;
                float alphaFloor=Mathf.Max(.5f,premultipliedFloor);
                return new Color((.2f+.1f)*alphaFloor*colorMul*dissolve+edge*.2f,
                    (.3f+.1f)*alphaFloor*colorMul*dissolve+edge*.4f,
                    (.4f+.1f)*alphaFloor*colorMul*dissolve+edge*.6f,
                    dissolve*.5f*alphaMul);
            };

            second.SetPixel(0,0,new Color(.25f,.8f,.7f,.9f));second.Apply();
            Near(sample(),expected(.25f),"CommonFX second-alpha dissolve RGBR uses mask R, original vertex varying, and colored edge");
            material.SetFloat("_OriginalFamily",45);
            second.SetPixel(0,0,new Color(.9f,.8f,.7f,.25f));second.Apply();
            Near(sample(),expected(.25f),"CommonFX second-alpha dissolve RGBA uses mask A");

            second.SetPixel(0,0,new Color(.25f,.8f,.7f,.9f));second.Apply();
            material.SetFloat("_OriginalFamily",44);material.SetFloat("_IgnoreVertexAlpha",.1f);
            vertexDissolve=Mathf.Max(1-vertexAlpha-.1f,0)*(2*smoothness+1)-smoothness;
            threshold=2*(vertexDissolve+factor)-1;
            Near(sample(),expected(.25f),"CommonFX second-alpha vertex threshold honors ignored vertex alpha");
            material.SetFloat("_MinAlpha",.4f);
            vertexDissolve=Mathf.Max(1-vertexAlpha-.1f,.4f*1.01f)*(2*smoothness+1)-smoothness;
            threshold=2*(vertexDissolve+factor)-1;
            Near(sample(),expected(.25f),"CommonFX second-alpha vertex threshold honors minimum vertex alpha");
        }
        finally{AssetRegistry.ReleaseTransient(main);AssetRegistry.ReleaseTransient(second);}
    }
}
