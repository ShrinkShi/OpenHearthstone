using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeRGBAChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original SetColorRGBA: "+reason);passed++;}
    public static void Run()
    {
        passed=0;string[] names={"red","green","blue","alpha"};var original=new Color(2,-1,.25f,.6f);
        for(int mask=0;mask<16;mask++)
        {
            var action=new NativeAction{name="SetColorRGBA",@params=names.Select((name,i)=>new NativeParam{name=name,none_known=true,is_none=(mask&(1<<i))==0,number=3+i}).ToArray()};
            int reads=0;var value=NativeRGBAAction.Apply(original,action,p=>{reads++;return p.number;}).Value;
            for(int i=0;i<4;i++)Check(value[i]==((mask&(1<<i))==0?original[i]:3+i),"None preserves channel, active channel retains HDR/unclamped value");
            Check(reads==names.Where((name,i)=>(mask&(1<<i))!=0).Count(),"None never reads float value");
        }
        Check(!NativeRGBAAction.Apply(null,new NativeAction(),p=>{throw new Exception("Null target read");}).HasValue,"null FsmColor returns before channel reads");
        bool rejected=false;try{NativeRGBAAction.Apply(original,new NativeAction{@params=new NativeParam[0]},p=>0);}catch(ArgumentException){rejected=true;}
        Check(rejected,"missing channel None data is not guessed");
        VerifySourceRuntime();
        Debug.Log("NATIVE RGBA: "+passed+" checks passed; original channel assignment restored, complete Shader/FSM/frame ordering remains incomplete");
        NativeSpawnStartChecks.Run();
    }
    static void VerifySourceRuntime()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        string file="../local/runtime/5a360810c62fabb080bc018b.start-spell.json";
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(file));
        var machine=program.machines.Single(m=>m.owner_known&&m.owner==0);
        var birth=machine.states.Single(s=>s.name=="BirthAnimation");
        var rgba=birth.actions.Single(a=>a.name=="SetColorRGBA");
        var initial=machine.colors.Single(c=>c.name=="CardHeroColor").value;
        var alpha=birth.actions.Single(a=>a.name=="AnimateFloat"&&a.Param("floatVariable").variable=="CardHeroAlpha").Param("animCurve");
        int cardNode=Array.FindIndex(program.nodes,n=>n.name=="Card_Hero");
        using(var instance=new NativeAreaInstance(program,assets))
        {
            instance.Effect.ChangeInitializedSourceState(1,0);instance.Render(.8f);
            var color=instance.Effect.Nodes[cardNode].GetComponent<Renderer>().sharedMaterial.GetColor("_TintColor");
            Check(Mathf.Abs(color.r-initial.r)<1e-6f&&Mathf.Abs(color.g-initial.g)<1e-6f&&Mathf.Abs(color.b-initial.b)<1e-6f,"real None RGB channels preserve authored card tint");
            Check(Mathf.Abs(color.a-alpha.SampleCurve(.8f))<1e-4f,"real everyFrame alpha reaches material after authored curve sampling");
            Check(instance.Effect.RGBAWrites>2,"real runtime everyFrame writes");
            Directory.CreateDirectory("../tests/visual/current/native-rgba");
            var texture=instance.Render(.8f);var previous=RenderTexture.active;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            try{RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes("../tests/visual/current/native-rgba/holy-start.png",image.EncodeToPNG());}
            finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
            Check(machine.colors.Single(c=>c.name=="CardHeroColor").value==initial,"runtime writes do not mutate shared source program colors");
        }
        var once=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(program));
        once.machines.Single(m=>m.owner_known&&m.owner==0).states.Single(s=>s.name=="BirthAnimation").actions.Single(a=>a.name=="SetColorRGBA").Param("everyFrame").number=0;
        using(var instance=new NativeAreaInstance(once,assets))
        {
            instance.Effect.ChangeInitializedSourceState(1,0);instance.Render(.2f);
            float first=instance.Effect.Nodes[cardNode].GetComponent<Renderer>().sharedMaterial.GetColor("_TintColor").a;
            instance.Render(.8f);
            float later=instance.Effect.Nodes[cardNode].GetComponent<Renderer>().sharedMaterial.GetColor("_TintColor").a;
            Check(first==later&&instance.Effect.RGBAWrites==1,"everyFrame false retains enter write while source float curve changes");
        }
        var held=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(program));
        var heldBirth=held.machines.Single(m=>m.owner_known&&m.owner==0).states.Single(s=>s.name=="BirthAnimation");
        heldBirth.actions=heldBirth.actions.Where(a=>a.name=="SetColorRGBA"||a.name=="AnimateFloat"||a.name=="SetMaterialColor").ToArray();
        using(var instance=new NativeAreaInstance(held,assets))
        {
            instance.Effect.ChangeInitializedSourceState(1,0);instance.Seek(1.5f);
            Check(instance.Effect.ActiveSourceSpellState==1&&instance.Effect.RGBAWrites>100,"unfinished everyFrame RGBA does not synthesize FINISHED without explicit event");
        }
    }
}
