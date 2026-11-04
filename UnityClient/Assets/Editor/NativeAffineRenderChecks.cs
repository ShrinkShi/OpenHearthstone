using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class NativeAffineRenderChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception(message);passed++;}
    static void Near(Color a,Color b,string message)
    {for(int channel=0;channel<4;channel++)Check(Mathf.Abs(a[channel]-b[channel])<.0001f,message+" channel "+channel+": "+a+" vs "+b);}
    public static void Run()
    {
        passed=0;
        Check(NativeAffineCapture.DiagonalAffine(0,3),"Multiply Zero/SrcColor is affine");
        Check(!NativeAffineCapture.DiagonalAffine(7,1)&&!NativeAffineCapture.DiagonalAffine(1,2)&&!NativeAffineCapture.DiagonalAffine(9,1),"cross-channel and nonlinear destination factors cannot be guessed");
        var root=new GameObject("AffineOriginalBlendChecks");root.transform.position=Vector3.one*80000;
        var camera=new GameObject("AffineCamera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.transform.localPosition=new Vector3(0,0,-5);
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.5f;camera.cullingMask=1<<29;camera.clearFlags=CameraClearFlags.SolidColor;
        var frame=new RenderTexture(32,32,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);frame.Create();camera.targetTexture=frame;
        var pixels=new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);var old=RenderTexture.active;
        var texture=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);texture.SetPixel(0,0,new Color(.2f,.4f,.6f,.25f));texture.Apply();
        var materials=new List<Material>();var meshes=new List<Mesh>();var sources=new Dictionary<Material,NativeMaterial>();
        var player=new NativeEffectPlayer(new AssetRegistry(Path.GetFullPath("../local")));
        using(var affine=new NativeAffineCapture(32,32))
        try
        {
            for(int index=0;index<2;index++)
            {
                var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform,false);quad.layer=29;
                var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);meshes.Add(mesh);quad.GetComponent<MeshFilter>().sharedMesh=mesh;
                mesh.colors=new[]{new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f),new Color(.5f,.7f,.9f,.8f)};
                var material=new Material(Resources.Load<Shader>("NativeEffect"));materials.Add(material);quad.GetComponent<Renderer>().sharedMaterial=material;quad.GetComponent<Renderer>().sortingOrder=index;
                material.SetTexture("_MainTex",texture);material.SetColor("_Color",new Color(.6f,.8f,1,.5f));material.SetFloat("_LightingBlend",0);
                material.SetFloat("_OriginalFamily",index==0?1:7);material.SetFloat("_ColorMul",1.75f);material.SetFloat("_Intensity",.75f);
                var source=new NativeMaterial{name="Original fixture "+index,src=index==0?5:0,dst=index==0?1:3,src_alpha=index==0?5:0,dst_alpha=index==0?1:3};sources[material]=source;
                material.SetFloat("_SrcBlend",source.src);material.SetFloat("_DstBlend",source.dst);material.SetFloat("_CaptureSrcAlpha",source.src_alpha);material.SetFloat("_CaptureDstAlpha",source.dst_alpha);
            }
            Func<RenderTexture,Color> read=target=>{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,32,32),0,0);pixels.Apply();return pixels.GetPixel(16,16);};
            foreach(float intensity in new[]{1.75f,30f})
            {
                materials[0].SetFloat("_ColorMul",intensity);affine.Render(camera,sources);
                Color c=read(affine.Color),k=read(affine.Transmission);
                Check(Mathf.Abs(k.r-k.b)>.001f,"actual colored transmission does not collapse to scalar alpha");
                if(intensity==30)Check(c.maxColorComponent>1,"HDR emission retained before extracting transmission");
                foreach(Color background in new[]{new Color(.15f,.1f,.05f,.8f),new Color(.75f,.25f,.6f,.2f),new Color(2,1,4,.4f)})
                {
                    camera.backgroundColor=background;camera.Render();var direct=read(frame);
                    Near(direct,c+background*k,"zero/one replay reproduces actual ordered original draws");
                    RenderTexture.active=frame;GL.Clear(true,true,background);
                    player.RenderAffine(new Rect(0,0,32,32),affine.Color,affine.Transmission,Matrix4x4.identity,32,32,Color.white);
                    Near(read(frame),direct,"production two-pass composite reproduces direct colored destination blend");
                    foreach(float fade in new[]{0f,.5f})
                    {
                        RenderTexture.active=frame;GL.Clear(true,true,background);
                        player.RenderAffine(new Rect(0,0,32,32),affine.Color,affine.Transmission,Matrix4x4.identity,32,32,new Color(1,1,1,fade));
                        Near(read(frame),Color.LerpUnclamped(background,direct,fade),"production fade preserves colored destination");
                    }
                }
            }
            materials[0].SetFloat("_ColorMul",1.75f);
            foreach(int sourceBlend in new[]{0,1,2,3,4,5,6,10})foreach(int destinationBlend in new[]{0,1,3,5,6,10})
            {
                var source=sources[materials[0]];source.src=sourceBlend;source.dst=destinationBlend;source.src_alpha=sourceBlend;source.dst_alpha=destinationBlend;
                materials[0].SetFloat("_SrcBlend",sourceBlend);materials[0].SetFloat("_DstBlend",destinationBlend);materials[0].SetFloat("_CaptureSrcAlpha",sourceBlend);materials[0].SetFloat("_CaptureDstAlpha",destinationBlend);affine.Render(camera,sources);
                foreach(Color background in new[]{new Color(.15f,.1f,.05f,.8f),new Color(.75f,.25f,.6f,.2f)})
                {
                    camera.backgroundColor=background;camera.Render();Color direct=read(frame);
                    RenderTexture.active=frame;GL.Clear(true,true,background);
                    player.RenderAffine(new Rect(0,0,32,32),affine.Color,affine.Transmission,Matrix4x4.identity,32,32,Color.white);
                    Near(read(frame),direct,"production affine source/destination enum pair "+sourceBlend+"/"+destinationBlend);
                }
            }
            var dynamicSource=sources[materials[0]];dynamicSource.src_property="_RgbSrcBlend";
            foreach(string property in new[]{"_OriginalSrcBlend","_OriginalDstBlend","_OriginalSrcAlpha","_OriginalDstAlpha","_SrcBlend","_DstBlend","_CaptureSrcAlpha","_CaptureDstAlpha"})materials[0].SetFloat(property,1);
            affine.Render(camera,sources);camera.backgroundColor=new Color(.15f,.1f,.05f,.8f);camera.Render();Color dynamicDirect=read(frame);
            RenderTexture.active=frame;GL.Clear(true,true,camera.backgroundColor);
            player.RenderAffine(new Rect(0,0,32,32),affine.Color,affine.Transmission,Matrix4x4.identity,32,32,Color.white);
            Near(read(frame),dynamicDirect,"affine capture follows live property-backed blends instead of frozen source enums");
            dynamicSource.src_property=null;
            sources[materials[0]].src=5;sources[materials[0]].dst=1;sources[materials[0]].src_alpha=5;sources[materials[0]].dst_alpha=1;materials[0].SetFloat("_SrcBlend",5);materials[0].SetFloat("_DstBlend",1);materials[0].SetFloat("_CaptureSrcAlpha",5);materials[0].SetFloat("_CaptureDstAlpha",1);
            Check(camera.targetTexture==frame&&camera.clearFlags==CameraClearFlags.SolidColor,"capture restores camera output");
            sources[materials[1]].src=7;bool rejected=false;
            try{affine.Render(camera,sources);}catch(NotSupportedException){rejected=true;}
            Check(rejected&&materials[0].GetFloat("_SrcBlend")==5&&camera.targetTexture==frame,"unrestored coupled blend fails without leaking render state");
            File.WriteAllText("../work/native-affine-checks.json","{\"passed\":"+passed+"}");Debug.Log("NATIVE AFFINE GPU CHECKS PASSED: "+passed);
        }
        finally
        {RenderTexture.active=old;camera.targetTexture=null;player.Dispose();foreach(var material in materials)AssetRegistry.ReleaseTransient(material);foreach(var mesh in meshes)AssetRegistry.ReleaseTransient(mesh);AssetRegistry.ReleaseTransient(texture);AssetRegistry.ReleaseTransient(pixels);AssetRegistry.ReleaseTransient(frame);AssetRegistry.ReleaseTransient(root);}
    }
}
