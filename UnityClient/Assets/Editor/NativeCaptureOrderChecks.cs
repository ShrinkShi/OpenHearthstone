using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class NativeCaptureOrderChecks
{
    static int passed;
    static void Require(bool value,string name){if(!value)throw new Exception(name);passed++;}
    public static void Run()
    {
        passed=0;
        var selected=NativeEffectCapture.SourcePasses(new[]{
            new NativeShaderPass{index=0,light_mode="GlowTransparent"},
            new NativeShaderPass{index=1,light_mode="UniversalForward"},
            new NativeShaderPass{index=2,light_mode=""},
            new NativeShaderPass{index=3,light_mode="SRPDefaultUnlit"},
            new NativeShaderPass{index=4,light_mode="LightweightForward"}});
        Require(selected.Length==3,"original LightMode selection count");
        Require(selected[0]==2&&selected[1]==1&&selected[2]==4,"first pass per LightMode, in original mode order");
        Require(NativeEffectCapture.SourcePasses(new NativeShaderPass[0]).Length==0,"no eligible pass is not a base fallback");
        var root=new GameObject("OriginalCaptureOrdering");root.transform.position=new Vector3(80000,80000,80000);
        var source=new GameObject("CaptureSource");source.transform.SetParent(root.transform,false);
        var target=new GameObject("CaptureTarget");target.transform.SetParent(root.transform,false);
        var materialData=new Dictionary<Material,NativeMaterial>();
        var renderers=new List<Renderer>();var materials=new List<Material>();
        var pixels=new Texture2D(32,32,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;
        try
        {
            // Deliberately reverse hierarchy order. All quads occupy the same pixels.
            foreach(var color in new[]{Color.red,Color.green,Color.blue})
            {
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(source.transform,false);
                quad.transform.localRotation=Quaternion.Euler(90,0,0);quad.transform.localScale=Vector3.one*2;
                var renderer=quad.GetComponent<Renderer>();renderer.enabled=false;renderers.Add(renderer);
                var material=new Material(Resources.Load<Shader>("NativeEffect"));materials.Add(material);renderer.sharedMaterial=material;
                material.SetFloat("_SolidColor",1);material.SetColor("_Color",color);
                material.SetFloat("_OriginalSrcBlend",1);material.SetFloat("_OriginalDstBlend",0);
                material.SetFloat("_OriginalSrcAlpha",1);material.SetFloat("_OriginalDstAlpha",0);
                materialData[material]=new NativeMaterial{passes=new[]{new NativeShaderPass{index=0,light_mode="SRPDefaultUnlit"}}};
            }
            materials[0].renderQueue=3100;materials[1].renderQueue=3050;materials[2].renderQueue=2000;
            renderers[0].sortingOrder=0;renderers[1].sortingOrder=1;renderers[2].sortingOrder=100;
            var data=new NativeCapture{owner=0,source=1,target=2,width=2,height=2,resolution=32,near=-1,far=1,clear=Color.clear};
            using(var capture=new NativeEffectCapture(data,new[]{root,source,target},r=>true,m=>materialData[m]))
            {
                Action<Color,string> sample=(expected,label)=>
                {
                    capture.Render();RenderTexture.active=capture.Texture;
                    pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();var actual=pixels.GetPixel(16,16);
                    for(int channel=0;channel<4;channel++)Require(Mathf.Abs(actual[channel]-expected[channel])<.02f,label+" channel "+channel);
                };
                sample(Color.green,"opaque group first, sortingOrder before queue");
                renderers[0].sortingOrder=1;
                sample(Color.red,"equal sortingOrder uses queue");
                materials[0].renderQueue=2999;
                sample(Color.green,"3000 boundary is distinct from 2500");
                renderers[1].gameObject.SetActive(false);
                sample(Color.blue,"inactive source omitted and opaque sorting applies");
                renderers[2].sortingOrder=-1;
                sample(Color.red,"sorting changes are applied without rebuilding source hierarchy");
                Require(materials[0].GetFloat("_SrcBlend")==5,"capture restores board RGB blend");
                Require(materials[0].GetFloat("_CaptureDstAlpha")==10,"capture restores board alpha blend");
            }
        }
        finally
        {
            RenderTexture.active=previous;
            foreach(var material in materials)AssetRegistry.ReleaseTransient(material);
            AssetRegistry.ReleaseTransient(pixels);AssetRegistry.ReleaseTransient(root);
        }
        File.WriteAllText("../work/native-capture-order-checks.json","{\"passed\":"+passed+"}");
        Debug.Log("ORIGINAL CAPTURE ORDER GPU CHECKS PASSED: "+passed);
    }
}
