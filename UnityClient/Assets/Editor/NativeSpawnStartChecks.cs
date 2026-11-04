using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeSpawnStartChecks
{
    [Serializable] sealed class Config{public NativeStartInfo start;}
    [Serializable] sealed class Record{public string reference;public Config config;}
    [Serializable] sealed class Catalog{public Record[] programs;}
    [Serializable] sealed class Prefab{public string reference,file;}
    [Serializable] sealed class Prefabs{public Prefab[] prefabs;}
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original SpawnStart: "+reason);passed++;}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-start-config.json")));
        var prefabs=JsonUtility.FromJson<Prefabs>(File.ReadAllText(Path.Combine(assets.Root,"native-super-start-prefabs.json"))).prefabs;
        foreach(string name in new[]{"Holy_Targeted_BlessingOfWisdom_FX.prefab:","Holy_Targeted_BlessingOfKings_FX.prefab:"})
        {
            var info=catalog.programs.Single(p=>p.reference.StartsWith(name)).config.start;
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefabs.Single(p=>p.reference==info.prefab_reference).file)));
            var machine=program.machines.Single(m=>m.owner_known&&m.owner==0);
            var host=new GameObject("Original start parent");host.transform.position=new Vector3(20000,10000,20000);
            var source=new GameObject("Original start source");var target=new GameObject("Original start target");
            NativeSuperStart gate=null;NativeStartInstance instance=null;int actions=0,finished=0,released=0;
            var spawn=new NativeSuperSpawnStart{Root=host,Source=()=>source,Targets=()=>new[]{target,null,target},
                GetSpell=i=>{instance=new NativeStartInstance(program,assets,t=>NativeSpellLifecycle.Destination(machine,(NativeSpellStateType)t)!=null);return instance;},
                Release=(c,reset)=>{released++;return false;},FinishIfPossible=()=>finished++};
            try
            {
                gate=new NativeSuperStart(new NativeSuperStartServices{HasStart=()=>NativeSuperSpawnStart.HasStart(info),SpawnStart=()=>{spawn.Spawn(info);gate.Start=spawn.Start.Lifecycle;},DoActionNow=()=>{actions++;gate.DoStartSpellAction();}});
                Check(gate.CheckAndWaitForStartPrefab(),"actual original prefab activates Birth");
                Check(spawn.Pending==1&&spawn.ActiveClones.Count==1&&instance.Clone.Source==source,"actual clone ownership/source and pending");
                Check(instance.Clone.Targets.GetTargets().SequenceEqual(new[]{target,null,target}),"original target list preserves duplicates and nulls");
                Check(instance.Clone.Effect.Nodes[0].transform.parent==host.transform&&instance.Clone.Effect.Nodes[0].transform.position==host.transform.position,"source world position and original parent");
                var texture=instance.Render(.8f);var previous=RenderTexture.active;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
                try
                {
                    RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();
                    Directory.CreateDirectory("../tests/visual/current/native-start");File.WriteAllBytes("../tests/visual/current/native-start/"+(name.Contains("Wisdom")?"wisdom":"kings")+".png",image.EncodeToPNG());
                    Debug.Log("START DIAGNOSTIC: state="+instance.Clone.Effect.ActiveSourceSpellState+" particles="+instance.Clone.Effect.Particles.Where(p=>p!=null).Sum(p=>p.particleCount)+" position="+instance.Clone.Effect.Nodes[0].transform.position);
                    Check(image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3)>128,"actual start prefab is visible");
                }
                finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
                instance.Seek(1.5f);
                Check(actions==1&&gate.Start==null,"actual Birth finish callback starts Action once");
                instance.Seek(4);
                Check(finished==1&&spawn.Pending==0,"original finished callback releases pending once");
                Check(released==1&&spawn.ActiveClones.Count==0&&instance.Clone.Effect.ActiveSourceSpellState==0,"Death reaches None and separately releases clone");
            }
            finally{if(instance!=null)instance.Dispose();AssetRegistry.ReleaseTransient(host);AssetRegistry.ReleaseTransient(source);AssetRegistry.ReleaseTransient(target);}
        }
        Debug.Log("NATIVE SPAWN START: "+passed+" checks passed; actual original start prefab lifecycle, original Card/task/scene/Shader completion remains incomplete");
        NativeSuperStartChecks.Run();
    }
}
