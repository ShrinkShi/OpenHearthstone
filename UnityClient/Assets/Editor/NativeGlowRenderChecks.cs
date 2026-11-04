using System;
using System.IO;
using UnityEngine;

public static class NativeGlowRenderChecks
{
    static int passed;
    static void Near(Color actual,Color expected,string label,float tolerance=.0003f)
    {
        for(int channel=0;channel<4;channel++)
        {
            if(Mathf.Abs(actual[channel]-expected[channel])>tolerance)throw new Exception(label+" channel "+channel+": "+actual+" expected "+expected);
            passed++;
        }
    }
    public static void Run()
    {
        passed=0;
        var root=new GameObject("OriginalGlowSamples");root.transform.position=new Vector3(90000,90000,90000);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform,false);
        var camera=new GameObject("OriginalGlowCamera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);
        camera.transform.localPosition=new Vector3(0,0,-5);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.5f;
        var material=new Material(Resources.Load<Shader>("NativeEffect"));quad.GetComponent<Renderer>().sharedMaterial=material;
        var texture=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);texture.SetPixel(0,0,new Color(.2f,.4f,.6f,.25f));texture.Apply();
        var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);quad.GetComponent<MeshFilter>().sharedMesh=mesh;
        mesh.colors=new[]{new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f)};
        var pixels=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);var previous=RenderTexture.active;var previousBias=Shader.GetGlobalVector("_GlobalMipBias");Shader.SetGlobalVector("_GlobalMipBias",Vector4.zero);
        var program=new NativeProgram{nodes=new[]{new NativeNode{materials=new[]{new NativeMaterial{shader="Hero/Particle/Particle Additive + 1",passes=new[]{new NativeShaderPass{index=1,light_mode="GlowTransparent"}}}}}}};
        try
        {
            material.SetTexture("_MainTex",texture);material.SetColor("_Color",new Color(.6f,.8f,1,.5f));
            material.SetColor("_GlowColor",new Color(.8f,.6f,.4f,.01f));material.SetFloat("_GlowIntensity",2);material.SetFloat("_GlowAddMul",.25f);
            using(var capture=new NativeEffectGlowCapture(program,new[]{quad},32,32,r=>true))
            {
                Func<Color> sample=()=>{RenderTexture.active=capture.Render(camera);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();return pixels.GetPixel(16,16);};
                var expected=new Color(.00980352f,.020587392f,.026469504f,.0061272f);
                Near(sample(),expected,"original Glow RGBA");
                program.nodes[0].materials[0].shader="FX/CommonFX";
                using(var common=new NativeEffectGlowCapture(program,new[]{quad},32,32,r=>true))
                {
                    RenderTexture.active=common.Render(camera);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();
                    Near(pixels.GetPixel(16,16),expected,"CommonFX original shared GlowTransparent program");
                    material.SetFloat("_AlphaMul",12);
                    RenderTexture.active=common.Render(camera);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();
                    Near(pixels.GetPixel(16,16),expected,"CommonFX Glow ignores base-pass AlphaMul");
                }
                program.nodes[0].materials[0].shader="Hero/Particle/Particle Multiply";
                using(var multiply=new NativeEffectGlowCapture(program,new[]{quad},32,32,r=>true))
                {
                    RenderTexture.active=multiply.Render(camera);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();Near(pixels.GetPixel(16,16),expected,"Multiply original shared GlowTransparent program");
                    material.SetFloat("_Intensity",15);RenderTexture.active=multiply.Render(camera);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();Near(pixels.GetPixel(16,16),expected,"Multiply Glow ignores base-pass Intensity");
                }
                program.nodes[0].materials[0].shader="Hero/Particle/Particle Additive + 1";
                foreach(string family in new[]{"Hero/AddMultiply/AddMultiplyFXScrolling","Hero/AddMultiply/AddMultiplyFXScrolling2Layer","Hero/Additive/Additive","Hero/Particle/Particles_Transparent","Hero/Particle/Particles_Transparent_ColorAsAlpha"})
                {
                    program.nodes[0].materials[0].shader=family;
                    using(var scrolling=new NativeEffectGlowCapture(program,new[]{quad},32,32,r=>true))
                    {
                        RenderTexture.active=scrolling.Render(camera);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();
                        Near(pixels.GetPixel(16,16),expected,"FX scrolling original shared GlowTransparent program");
                        material.SetFloat("_Clock",999);material.SetFloat("_ScrollX",999);material.SetFloat("_FxIntensity",999);
                        material.SetFloat("_FxIntensity1",999);material.SetFloat("_FxIntensity2",999);
                        RenderTexture.active=scrolling.Render(camera);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();
                        Near(pixels.GetPixel(16,16),expected,"FX scrolling Glow ignores base FX coordinates and gain");
                        if(family=="Hero/Additive/Additive")
                        {
                            material.SetColor("_TintColor",Color.clear);
                            RenderTexture.active=scrolling.Render(camera);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();
                            Near(pixels.GetPixel(16,16),expected,"Additive Glow ignores base TintColor including its zero alpha");
                            material.SetColor("_TintColor",Color.white);
                        }
                    }
                }
                program.nodes[0].materials[0].shader="Hero/Particle/Particle Additive + 1";
                material.SetColor("_Color",new Color(12,8,4,.5f));Near(sample(),expected,"Glow ignores tint RGB");
                material.SetFloat("_GlowIntensity",8);Near(sample(),expected*4,"animated Glow intensity reaches capture");
                material.SetFloat("_GlowAddMul",1);var alpha=expected*4;alpha.a*=4;Near(sample(),alpha,"GlowAddMul changes alpha only");
                material.SetFloat("_GlowIntensity",0);Near(sample(),Color.clear,"zero intensity is not fabricated glow");
                material.SetFloat("_GlowIntensity",2);material.SetColor("_Color",new Color(1,1,1,0));Near(sample(),Color.clear,"zero tint alpha suppresses glow");
                material.SetColor("_Color",new Color(1,1,1,.5f));material.SetFloat("_GlowAddMul",.25f);
                quad.SetActive(false);Near(sample(),Color.clear,"inactive entity omitted");quad.SetActive(true);
                Near(sample(),expected,"reappearing entity restores original Glow");
                var mipmapped=new Texture2D(32,32,TextureFormat.RGBAFloat,true,true){filterMode=FilterMode.Point};
                try
                {
                    for(int level=0;level<mipmapped.mipmapCount;level++)
                    {
                        int size=Mathf.Max(1,32>>level);var colors=new Color[size*size];
                        for(int p=0;p<colors.Length;p++)colors[p]=level==2?new Color(.8f,.2f,.1f,.5f):new Color(.2f,.4f,.6f,.25f);
                        mipmapped.SetPixels(colors,level);
                    }
                    mipmapped.Apply(false);material.SetTexture("_MainTex",mipmapped);
                    Shader.SetGlobalVector("_GlobalMipBias",new Vector4(2,0,0,0));
                    Near(sample(),new Color(.05370624f,.014097888f,.006041952f,.0167832f),"original global mip bias selects the compiled sampler level");
                    Shader.SetGlobalVector("_GlobalMipBias",Vector4.zero);material.SetTexture("_MainTex",texture);
                }
                finally{AssetRegistry.ReleaseTransient(mipmapped);}
            }
            program.nodes[0].materials[0].shader="Hero/AddMultiply/AddMultiplyDistortion";
            using(var capture=new NativeEffectGlowCapture(program,new[]{quad},32,32,r=>true))
            {
                RenderTexture.active=capture.Render(camera);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();
                Near(pixels.GetPixel(16,16),new Color(.00980352f,.020587392f,.026469504f,.0061272f),"Distortion Glow has the same original undistorted sampling program");
            }
            program.nodes[0].materials[0].passes=new[]{new NativeShaderPass{index=0,light_mode="SRPDefaultUnlit"}};
            using(var capture=new NativeEffectGlowCapture(program,new[]{quad},32,32,r=>true))
            {
                RenderTexture.active=capture.Render(camera);pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();
                Near(pixels.GetPixel(16,16),Color.clear,"unavailable Glow pass is excluded");
            }
        }
        finally
        {
            RenderTexture.active=previous;Shader.SetGlobalVector("_GlobalMipBias",previousBias);
            foreach(var value in new UnityEngine.Object[]{material,texture,mesh,pixels,root})AssetRegistry.ReleaseTransient(value);
        }
        var lighting=new Material(UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/NativeLightingCheck.shader"));
        var lightingFrame=new RenderTexture(8,8,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);lightingFrame.Create();
        var lightingPixels=new Texture2D(8,8,TextureFormat.RGBAFloat,false,true);
        try
        {
            Action<Color,string> sample=(expected,label)=>{Graphics.Blit(Texture2D.whiteTexture,lightingFrame,lighting);RenderTexture.active=lightingFrame;lightingPixels.ReadPixels(new Rect(0,0,8,8),0,0);lightingPixels.Apply();Near(lightingPixels.GetPixel(4,4),expected,label,.001f);};
            lighting.SetVector("_SH",new Vector4(-.0031308f,.18f,2,0));lighting.SetFloat("_Blend",0);
            sample(Color.white,"lighting zero leaves original vertex RGB");
            lighting.SetFloat("_Blend",1);sample(new Color(.0404497f,.4613551f,1,1),"original absolute, gamma and saturation");
            lighting.SetFloat("_Blend",.5f);sample(new Color(.52022485f,.73067755f,1,1),"lighting blend interpolates before texture math");
            lighting.SetVector("_MainLight",new Vector4(.1f,.2f,.3f,0));
            sample(new Color(.57022485f,.83067755f,1.15f,1),"AddMultiply main-light term is not saturated after addition");
        }
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(lighting);AssetRegistry.ReleaseTransient(lightingFrame);AssetRegistry.ReleaseTransient(lightingPixels);}
        File.WriteAllText("../work/native-glow-render-checks.json","{\"passed\":"+passed+"}");Debug.Log("ORIGINAL GLOW GPU CHECKS PASSED: "+passed);
    }
}
