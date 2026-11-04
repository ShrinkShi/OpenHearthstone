using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeActorMaterialChecks
{
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Actor material: "+message);passed++;};
        var shader=Resources.Load<Shader>("OriginalActorPortrait");check(shader!=null&&shader.isSupported,"source shader compiles");
        var material=new Material(shader);var mesh=new Mesh();var target=new RenderTexture(16,16,24,RenderTextureFormat.ARGBFloat);
        var image=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);var portrait=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);
        var second=new Texture2D(2,1,TextureFormat.RGBAFloat,false,true);second.filterMode=FilterMode.Point;second.wrapMode=TextureWrapMode.Clamp;
        var go=new GameObject("Original portrait arithmetic fixture");go.layer=30;go.transform.position=new Vector3(55000,0,0);
        var camera=new GameObject("Original portrait arithmetic camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1;
        camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
        camera.transform.position=go.transform.position+Vector3.up*10;camera.transform.rotation=Quaternion.Euler(90,0,0);target.Create();camera.targetTexture=target;
        mesh.vertices=new[]{new Vector3(-1,0,-1),new Vector3(1,0,-1),new Vector3(-1,0,1),new Vector3(1,0,1)};
        mesh.triangles=new[]{0,2,1,2,3,1};mesh.uv=Enumerable.Repeat(new Vector2(.25f,.5f),4).ToArray();mesh.uv2=Enumerable.Repeat(new Vector2(.75f,.5f),4).ToArray();
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        Color main=new Color(.4f,.6f,.8f,.7f),detail=new Color(.8f,.3f,.5f,.4f),tint=new Color(.7f,.9f,.2f,.8f),dark=new Color(.1f,.2f,.3f,.4f),color=new Color(.8f,.6f,.9f,.7f),average=new Color(.3f,.4f,.5f,.6f);
        portrait.SetPixel(0,0,main);portrait.Apply();second.SetPixels(new[]{new Color(.1f,.9f,.2f,.8f),detail});second.Apply();
        material.SetTexture("_MainTex",portrait);material.SetTexture("_SecondTex",second);material.SetColor("_SecondTint",tint);
        material.SetColor("_DarkColor",dark);material.SetColor("_Color",color);material.SetColor("_AvgLum",average);material.SetFloat("_LightingBlend",0);
        var previous=RenderTexture.active;
        try
        {
            foreach(float blend in new[]{0f,2f,5f})foreach(float desaturate in new[]{0f,.4f,1f})foreach(float contrast in new[]{0f,.3f})
            {
                Color vertex=new Color(.2f,.5f,.8f,.6f);mesh.colors=Enumerable.Repeat(vertex,4).ToArray();
                material.SetFloat("_BlendIntensity",blend);material.SetFloat("_Desaturate",desaturate);material.SetFloat("_Contrast",contrast);material.SetFloat("_Intensity",1.3f);
                Color layer=detail*tint*blend;Color expected=Color.LerpUnclamped(main,main*layer,layer.a);
                float luminance=expected.r*.3f+expected.g*.59f+expected.b*.11f;
                expected=Color.LerpUnclamped(expected,Color.LerpUnclamped(dark,color,luminance),desaturate);expected=Color.LerpUnclamped(expected,average,contrast);
                Color limit=(expected+new Color(.15f,.15f,.15f,.15f))*(vertex*color);
                expected=new Color(Mathf.Min(expected.r,limit.r),Mathf.Min(expected.g,limit.g),Mathf.Min(expected.b,limit.b),Mathf.Min(expected.a,limit.a))*1.3f;
                camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,16,16),0,0);image.Apply();Color actual=image.GetPixel(8,8);
                check(Mathf.Max(Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g),Mathf.Abs(actual.b-expected.b),Mathf.Abs(actual.a-expected.a))<.004f,"DXBC RGBA/UV2 arithmetic "+blend+"/"+desaturate+"/"+contrast);
            }
            var fixtureRenderer=go.GetComponent<MeshRenderer>();fixtureRenderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.CustomProvided;
            var input=new MaterialPropertyBlock();input.SetVector("unity_SHAr",new Vector4(0,0,0,.1f));input.SetVector("unity_SHAg",new Vector4(0,0,0,.2f));input.SetVector("unity_SHAb",new Vector4(0,0,0,.4f));fixtureRenderer.SetPropertyBlock(input);
            Color mainLight=new Color(.4f,.6f,.8f,1),vertexColor=new Color(.2f,.5f,.8f,.6f);
            material.SetColor("_MainLightColor",mainLight);material.SetFloat("_OriginalLightDataZ",.5f);
            material.SetFloat("_BlendIntensity",0);material.SetFloat("_Desaturate",0);material.SetFloat("_Contrast",0);material.SetFloat("_Intensity",1);
            foreach(float lightBlend in new[]{0f,.4f,1f})
            {
                material.SetFloat("_LightingBlend",lightBlend);
                Func<float,float> gamma=x=>Mathf.Clamp01(1.055f*Mathf.Pow(Mathf.Abs(x),1f/2.4f)-.055f);
                Color lighting=new Color(gamma(.1f)+mainLight.r*.5f,gamma(.2f)+mainLight.g*.5f,gamma(.4f)+mainLight.b*.5f,1);
                Color limit=(main+new Color(.15f,.15f,.15f,.15f))*(Color.LerpUnclamped(vertexColor,lighting,lightBlend)*color);
                Color expected=new Color(Mathf.Min(main.r,limit.r),Mathf.Min(main.g,limit.g),Mathf.Min(main.b,limit.b),Mathf.Min(main.a,limit.a));
                camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,16,16),0,0);image.Apply();Color actual=image.GetPixel(8,8);
                check(Mathf.Max(Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g),Mathf.Abs(actual.b-expected.b),Mathf.Abs(actual.a-expected.a))<.004f,"source SH gamma/main-light/vertex-color blend "+lightBlend);
            }
            var frameShader=Resources.Load<Shader>("OriginalActorFrame");check(frameShader!=null&&frameShader.isSupported,"source frame shader compiles");
            var frame=new Material(frameShader);var shadow=new Material(UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>("Assets/Editor/OriginalFrameShadowProbe.shader"));
            try
            {
                check(frame.passCount==2&&frame.FindPass("ShadowCaster")==0,"source frame retains both passes");
                frame.SetTexture("_MainTex",portrait);frame.SetColor("_MainLightColor",mainLight);fixtureRenderer.sharedMaterial=frame;
                foreach(float lightBlend in new[]{0f,.4f,1f})foreach(bool reverse in new[]{false,true})
                {
                    frame.SetFloat("_LightingBlend",lightBlend);mesh.triangles=reverse?new[]{0,1,2,2,1,3}:new[]{0,2,1,2,3,1};
                    Func<float,float> gamma=x=>Mathf.Clamp01(1.055f*Mathf.Pow(Mathf.Abs(x),1f/2.4f)-.055f);
                    Color light=new Color(gamma(.1f)+mainLight.r,gamma(.2f)+mainLight.g,gamma(.4f)+mainLight.b,1);
                    Color expected=main*Color.LerpUnclamped(vertexColor,light,lightBlend);
                    camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,16,16),0,0);image.Apply();Color actual=image.GetPixel(8,8);
                    check(Mathf.Max(Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g),Mathf.Abs(actual.b-expected.b),Mathf.Abs(actual.a-expected.a))<.004f,"frame source RGBA, SH/main light and Cull Off "+lightBlend+"/"+reverse);
                }
                fixtureRenderer.sharedMaterial=shadow;
                foreach(float depth in new[]{.2f,2f})foreach(float normalBias in new[]{0f,.15f,.4f})
                {
                    var world=new Vector3(.1f,.3f,depth);var normal=new Vector3(1,2,0);var direction=new Vector3(0,1,0);
                    shadow.SetVector("_World",world);shadow.SetVector("_Normal",normal);shadow.SetVector("_Direction",direction);shadow.SetVector("_Bias",new Vector4(.12f,normalBias,0,0));shadow.SetMatrix("_VP",Matrix4x4.identity);
                    Vector3 expected=world+direction*.12f+normal.normalized*((1-Mathf.Clamp01(Vector3.Dot(direction,normal.normalized)))*normalBias);expected.z=Mathf.Min(1,expected.z);
                    camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,16,16),0,0);image.Apply();Color actual=image.GetPixel(8,8);
                    check(Mathf.Max(Mathf.Abs(actual.r-expected.x),Mathf.Abs(actual.g-expected.y),Mathf.Abs(actual.b-expected.z),Mathf.Abs(actual.a-1))<.004f,"source shadow bias/normalization/depth clamp "+depth+"/"+normalBias);
                }
            }
            finally{fixtureRenderer.sharedMaterial=material;AssetRegistry.ReleaseTransient(frame);AssetRegistry.ReleaseTransient(shadow);}
            var assets=new AssetRegistry(Path.GetFullPath("../local"));var actor=assets.Scene("MODEL_ACTOR_MINION");
            try
            {
                var identity=actor.GetComponentInChildren<NativeActorIdentity>(true);var renderer=identity.MeshRenderer;
                check(renderer.sharedMaterials[0].shader==shader,"production Actor uses restored portrait shader");
                check(renderer.sharedMaterials[1].shader==frameShader,"production Actor uses restored frame shader");
                check(renderer.GetComponent<MeshFilter>().sharedMesh.uv2.Length==renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount,"original second UV channel reaches production Mesh");
                check(renderer.sharedMaterials[0].GetTexture("_SecondTex")!=null,"exact original second texture reaches production material");
                check(renderer.sharedMaterials[0].GetFloat("_BlendIntensity")==2,"original blend intensity preserved");
                check(renderer.lightProbeUsage==UnityEngine.Rendering.LightProbeUsage.CustomProvided,"source ambient probe is explicitly supplied");
                var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);
                check(block.GetVector("unity_SHAr").w>0,"original ambient input is nonzero rather than inherited black preview lighting");
                foreach(var r in actor.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)AssetRegistry.ReleaseTransient(m);
            }
            finally{AssetRegistry.ReleaseTransient(actor);}
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;
            foreach(var obj in new UnityEngine.Object[]{go,camera.gameObject,mesh,material,target,image,portrait,second})AssetRegistry.ReleaseTransient(obj);
        }
        File.WriteAllText("../work/native-actor-material-checks.json","{\"passed\":"+passed+",\"original_fragment_math\":true,\"full_original_lighting\":false,\"complete\":false}");
        Debug.Log("NATIVE ACTOR MATERIAL GPU CHECKS PASSED: "+passed);
    }
}
