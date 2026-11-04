using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeShaderTimeChecks
{
    public static void Run()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original ShaderTime: "+message);passed++;};
        float originalTime=Shader.GetGlobalFloat("_ShaderTime");var originalGyro=Shader.GetGlobalVector("_Gyroscope");
        try
        {
            var clock=new NativeShaderTime();var gravity=new Vector3(.2f,-.7f,.3f);
            check(clock.Enabled&&clock.Value==0,"source constructor defaults");
            clock.Update(.02f,gravity);check(Mathf.Abs(clock.Value-.001f)<1e-7f,"source delta divided by twenty");
            check(Shader.GetGlobalFloat("_ShaderTime")==clock.Value,"global time is bound");
            check(Shader.GetGlobalVector("_Gyroscope")==new Vector4(.2f,-.7f,.3f,0),"implicit gravity vector has zero w");
            clock.Update(20,Vector3.zero);check(Mathf.Abs(clock.Value-1.001f)<1e-6f,"scaled Unity delta advances independently of cue time");
            clock.Enabled=false;clock.Update(123,gravity);check(clock.Value==1&&Shader.GetGlobalFloat("_ShaderTime")==1,"disabled source time forced to one");
            clock.Update(0,gravity);check(clock.Value==1,"disabled remains one");
            clock.Enabled=true;clock.Update(.01f,gravity);check(Mathf.Abs(clock.Value-1.0005f)<1e-6f,"reenable preserves source accumulator");
            var boundary=new NativeShaderTime();boundary.Update(65535f*20,gravity);check(boundary.Value==65535,"strict source wrap keeps exact boundary");
            boundary.Update(20,gravity);check(boundary.Value==1,"source wrap subtracts maximum");
            var large=new NativeShaderTime();large.Update(3*65535f*20,gravity);check(large.Value==2*65535f,"source performs one subtraction rather than modulo");
            var repeated=new NativeShaderTime();for(int i=0;i<100;i++)repeated.Update(.02f,gravity);
            check(Mathf.Abs(repeated.Value-.1f)<1e-6f,"finite frame accumulation");
            repeated.Shutdown();check(Shader.GetGlobalFloat("_ShaderTime")==0,"source shutdown clears global time");
            check(Shader.GetGlobalVector("_Gyroscope")==new Vector4(.2f,-.7f,.3f,0),"source shutdown does not clear gyro");
            check(repeated.Value!=0,"source shutdown does not rewrite accumulator");
        }
        finally{Shader.SetGlobalFloat("_ShaderTime",originalTime);Shader.SetGlobalVector("_Gyroscope",originalGyro);}
        File.WriteAllText("../work/native-shader-time-checks.json","{\"passed\":"+passed+",\"complete\":false}");
        Debug.Log("NATIVE SHADER TIME CHECKS PASSED: "+passed);
    }
    public static void RunGpu()
    {
        Run();
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        string folder=Path.GetFullPath("../tests/visual/current/native-shader-time");Directory.CreateDirectory(folder);
        float oldTime=Shader.GetGlobalFloat("_ShaderTime");var oldGyro=Shader.GetGlobalVector("_Gyroscope");var oldTarget=RenderTexture.active;
        int passed=0;
        try
        {
            using(var live=visuals.CreateLiveActor(database.cards.First(c=>c.CardId=="CS2_182"),"minion",true))
            using(var state=new NativeHighlightState(live.Model.GetComponentInChildren<NativeActorIdentity>(true),assets,.375f))
            {
                state.ChangeState(6);Color32[] initial=null;var clock=new NativeShaderTime();
                foreach(float delta in new[]{0f,1f,1f,40f})
                {
                    clock.Update(delta,Vector3.zero);var rendered=(RenderTexture)live.Render();
                    var image=new Texture2D(rendered.width,rendered.height,TextureFormat.RGBA32,false);
                    try
                    {
                        RenderTexture.active=rendered;image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();var pixels=image.GetPixels32();
                        if(initial==null)initial=pixels;
                        else if(pixels.Zip(initial,(a,b)=>a.r!=b.r||a.g!=b.g||a.b!=b.b).Count(v=>v)<20)throw new Exception("Original global time did not animate actual source flare");
                        passed++;
                        File.WriteAllBytes(Path.Combine(folder,"CS2_182-time-"+clock.Value.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());
                    }
                    finally{AssetRegistry.ReleaseTransient(image);}
                }
            }
        }
        finally{RenderTexture.active=oldTarget;Shader.SetGlobalFloat("_ShaderTime",oldTime);Shader.SetGlobalVector("_Gyroscope",oldGyro);visuals.Clear();}
        File.WriteAllText("../work/native-shader-time-gpu-checks.json","{\"passed\":"+passed+",\"frames\":4,\"production_highlight\":true}");
        Debug.Log("NATIVE SHADER TIME GPU CHECKS PASSED: "+passed);
    }
}
