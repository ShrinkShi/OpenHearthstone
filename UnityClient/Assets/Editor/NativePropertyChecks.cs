using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativePropertyChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original property action: "+message);passed++;}
    static NativePropertyDescriptor Float(string name,float value)
    {return new NativePropertyDescriptor{target_type="UnityEngine.ParticleSystem",property_name=name,values=new[]{new NativeParam{name="FloatParameter",none_known=true,number=value}}};}
    public static void Run()
    {
        passed=0;var root=new GameObject("Original property setter probe");var particle=root.AddComponent<ParticleSystem>();particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        try
        {
            var speed=Float("playbackSpeed",2.25f);
            Check(NativePropertyAction.Target(speed,root)==particle,"exact SDK component selected");
            Check(NativePropertyAction.Set(speed,particle,(p,t)=>p.number)&&particle.main.simulationSpeed==2.25f,"source legacy playbackSpeed setter reaches actual SDK main module");
            speed.values[0].is_none=true;
            Check(!NativePropertyAction.Set(speed,particle,(p,t)=>{throw new Exception("None read");})&&particle.main.simulationSpeed==2.25f,"None does not read or write");
            var main=particle.main;main.startSize=new ParticleSystem.MinMaxCurve(2f,4f);var size=Float("startSize",9f);
            var restore=NativePropertyAction.Restore(particle,"startSize");NativePropertyAction.Set(size,particle,(p,t)=>p.number);restore();
            Check(particle.main.startSize.mode==ParticleSystemCurveMode.TwoConstants&&particle.main.startSize.constantMin==2&&particle.main.startSize.constantMax==4,"restoration preserves full curve mode and range instead of scalar approximation");
            var scale=new NativePropertyDescriptor{target_type="UnityEngine.Transform",property_name="localScale",values=new[]{new NativeParam{name="Vector3Parameter",none_known=true,vector=new Vector3(2,3,4)}}};
            Check(NativePropertyAction.Set(scale,root.transform,(p,t)=>p.vector)&&root.transform.localScale==new Vector3(2,3,4),"actual transform vector setter");
            bool rejected=false;try{NativePropertyAction.Target(new NativePropertyDescriptor{target_type="Official.CustomScript"},root);}catch(NotSupportedException){rejected=true;}
            Check(rejected,"original custom scripts are not loaded or substituted");
            rejected=false;try{NativePropertyAction.Target(new NativePropertyDescriptor{target_type="UnityEngine.Light"},root);}catch(NotSupportedException){rejected=true;}
            Check(rejected,"missing actual component is reported");
            speed.property_name="main.simulationSpeed";rejected=false;
            try{NativePropertyAction.Set(speed,particle,(p,t)=>p.number);}catch(NotSupportedException){rejected=true;}
            Check(rejected,"unrestored nested member semantics not guessed");
        }
        finally{AssetRegistry.ReleaseTransient(root);}
        VerifyOriginalPrefab();
        Debug.Log("NATIVE PROPERTY SDK: "+passed+" checks passed; source binding/mutable particle history and full property value types remain incomplete");
        NativeRGBAChecks.Run();
    }
    static void VerifyOriginalPrefab()
    {
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/5a360810c62fabb080bc018b.start-spell.json"));
        var machine=program.machines.Single(m=>m.owner_known&&m.owner==0);
        var birth=machine.states.Single(s=>s.name=="BirthAnimation");
        var descriptor=birth.actions.First(a=>a.name=="SetProperty").Param("targetProperty").property_data;
        var curve=birth.actions.First(a=>a.name=="AnimateFloat"&&a.Param("floatVariable").variable=="BirthRaysPlaySpeed").Param("animCurve");
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        using(var instance=new NativeAreaInstance(program,assets))
        {
            instance.Effect.ChangeInitializedSourceState(1,0);var texture=instance.Render(.8f);
            Check(instance.Effect.PropertyWrites>100&&instance.Effect.UnboundPropertyActions==0,"actual original property actions bind and update");
            Check(Mathf.Abs(instance.Effect.Particles[descriptor.target_node].main.simulationSpeed-curve.SampleCurve(.8f))<1e-4f,"authored float curve reaches actual particle playback speed");
            var previous=RenderTexture.active;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();Directory.CreateDirectory("../tests/visual/current/native-property");File.WriteAllBytes("../tests/visual/current/native-property/holy-start.png",image.EncodeToPNG());}
            finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
        }
    }
}
