using System;
using System.IO;
using UnityEngine;

public static class NativeAdditiveChecks
{
    public static void RunAllGpu()
    {
        NativeEffectChecks.Run();
        NativeShaderRenderChecks.Run();
        NativeGlowRenderChecks.Run();
        NativeAffineRenderChecks.Run();
        NativeEffectRenderChecks.Run();
    }
    public static void RunGpu()
    {
        int passed=0;
        Action<Color,Color,string> near=(actual,expected,label)=>
        {
            for(int channel=0;channel<4;channel++)
            {if(Mathf.Abs(actual[channel]-expected[channel])>.002f)throw new Exception("Original Additive "+label+": "+actual+" expected "+expected);passed++;}
        };
        var root=new GameObject("OriginalAdditiveProbe");root.transform.position=new Vector3(95000,95000,95000);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform,false);quad.layer=30;
        var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);quad.GetComponent<MeshFilter>().sharedMesh=mesh;
        mesh.colors=new[]{new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f)};
        var cameraObject=new GameObject("OriginalAdditiveCamera");cameraObject.transform.SetParent(root.transform,false);
        var camera=cameraObject.AddComponent<Camera>();camera.transform.localPosition=new Vector3(0,0,-5);
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.5f;camera.cullingMask=1<<30;
        camera.backgroundColor=Color.clear;camera.clearFlags=CameraClearFlags.SolidColor;
        var frame=new RenderTexture(16,16,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);frame.Create();camera.targetTexture=frame;
        var texture=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);texture.SetPixel(0,0,new Color(.2f,.4f,.6f,.25f));texture.Apply();
        var material=new Material(Resources.Load<Shader>("NativeEffect"));quad.GetComponent<Renderer>().sharedMaterial=material;
        var pixels=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;
        var bias=Shader.GetGlobalVector("_GlobalMipBias");Shader.SetGlobalVector("_GlobalMipBias",Vector4.zero);
        Func<Color> sample=()=>{camera.Render();RenderTexture.active=frame;pixels.ReadPixels(new Rect(0,0,16,16),0,0);pixels.Apply();return pixels.GetPixel(8,8);};
        try
        {
            material.SetFloat("_OriginalFamily",16);material.SetTexture("_MainTex",texture);
            material.SetColor("_TintColor",new Color(.6f,.8f,1,.5f));material.SetColor("_Color",Color.clear);
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            near(sample(),new Color(.12f,.448f,1.08f,.2f),"unclipped RGBA and source TintColor");
            material.SetFloat("_Intensity",99);material.SetFloat("_ColorMul",99);material.SetFloat("_AlphaIntensity",99);
            near(sample(),new Color(.12f,.448f,1.08f,.2f),"undeclared legacy gains ignored");
            material.SetColor("_Color",new Color(9,8,7,6));
            near(sample(),new Color(.12f,.448f,1.08f,.2f),"base ignores Glow Color");
            material.SetInt("_SrcBlend",5);material.SetInt("_DstBlend",1);material.SetInt("_CaptureSrcAlpha",5);material.SetInt("_CaptureDstAlpha",1);
            camera.backgroundColor=new Color(.15f,.1f,.05f,.3f);
            near(sample(),new Color(.174f,.1896f,.266f,.34f),"original SrcAlpha One RGB and alpha blend");
            material.SetColor("_TintColor",new Color(.6f,.8f,1,0));
            near(sample(),camera.backgroundColor,"transparent source preserves destination");
            material.SetColor("_TintColor",new Color(.6f,.8f,1,.5f));
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            var pattern=new Texture2D(2,1,TextureFormat.RGBAFloat,false,true){filterMode=FilterMode.Point};
            try
            {
                pattern.SetPixels(new[]{new Color(.2f,.4f,.6f,.25f),new Color(.6f,.2f,.1f,.75f)});pattern.Apply();
                material.SetTexture("_MainTex",pattern);material.SetTextureScale("_MainTex",Vector2.zero);material.SetTextureOffset("_MainTex",new Vector2(.8f,0));
                near(sample(),new Color(.36f,.224f,.18f,.6f),"source ST selects second texel");
            }
            finally{AssetRegistry.ReleaseTransient(pattern);}
            material.SetTexture("_MainTex",texture);material.SetTextureScale("_MainTex",Vector2.one);material.SetTextureOffset("_MainTex",Vector2.zero);
            material.SetFloat("_OriginalFamily",17);material.SetFloat("_Intensity",.75f);material.SetFloat("_Alpha",.25f);
            near(sample(),new Color(.09f,.336f,.81f,.05f),"transparent RGB Intensity and independent Alpha");
            material.SetFloat("_Intensity",2);material.SetFloat("_Alpha",1);
            near(sample(),new Color(.24f,.896f,2.16f,.2f),"transparent preserves HDR without affecting alpha");
            material.SetInt("_SrcBlend",5);material.SetInt("_DstBlend",10);material.SetInt("_CaptureSrcAlpha",5);material.SetInt("_CaptureDstAlpha",10);
            near(sample(),new Color(.168f,.2592f,.472f,.28f),"transparent original SrcAlpha OneMinusSrcAlpha RGBA");
            material.SetFloat("_OriginalFamily",18);material.SetFloat("_Intensity",.75f);material.SetFloat("_AlphaIntensity",.5f);
            material.SetInt("_SrcBlend",1);material.SetInt("_DstBlend",0);material.SetInt("_CaptureSrcAlpha",1);material.SetInt("_CaptureDstAlpha",0);
            near(sample(),new Color(.45f,.84f,1.35f,.0543f),"color alpha source luminance and doubled tint RGB");
            texture.SetPixel(0,0,new Color(.2f,.4f,.6f,0));texture.Apply();
            near(sample(),new Color(.45f,.84f,1.35f,.0543f),"color alpha ignores texture alpha");
            material.SetFloat("_Intensity",2);material.SetFloat("_AlphaIntensity",10);
            near(sample(),new Color(1.2f,2.24f,3.6f,.8f),"color alpha saturates mask before vertex alpha, preserves HDR RGB");
            material.SetFloat("_AlphaIntensity",-1);
            near(sample(),new Color(1.2f,2.24f,3.6f,0),"color alpha negative mask clamps to zero without erasing RGB");
            material.SetFloat("_AlphaIntensity",.5f);material.SetFloat("_Intensity",.75f);
            material.SetInt("_SrcBlend",5);material.SetInt("_DstBlend",10);material.SetInt("_CaptureSrcAlpha",5);material.SetInt("_CaptureDstAlpha",10);
            near(sample(),new Color(.16629f,.140182f,.12059f,.28665949f),"color alpha original separate RGBA blend");
        }
        finally
        {
            Shader.SetGlobalVector("_GlobalMipBias",bias);RenderTexture.active=previous;camera.targetTexture=null;
            AssetRegistry.ReleaseTransient(pixels);AssetRegistry.ReleaseTransient(texture);AssetRegistry.ReleaseTransient(material);
            AssetRegistry.ReleaseTransient(frame);AssetRegistry.ReleaseTransient(mesh);AssetRegistry.ReleaseTransient(root);
        }
        File.WriteAllText("../work/native-additive-checks.json","{\"passed\":"+passed+",\"full_card_effects_verified\":false}");
        Debug.Log("Original Additive checks passed: "+passed);
    }
}
