using System;
using System.IO;
using UnityEngine;

public static class NativeEffectPlacementChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original effect placement: "+message);passed++;}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));var source=new GameObject("Explicit original effect source");
        source.transform.position=new Vector3(7,4,-2);
        try
        {
            foreach(string file in new[]{"a484b427845ecdd3236cbf88.native-effect.json","1438c953f1f357de128e06d3.native-effect.json"})
            {
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file)));
                var root=Array.Find(program.spell_transforms,s=>s.node==0);
                Check(root!=null&&root.kind=="SuperSpell"&&root.location==2&&!root.parent_to_location,"actual source SuperSpell placement metadata "+file);
                using(var instance=new NativeEffectInstance(program,assets,0,32,32))
                {
                    instance.BindSourcePlacement(location=>location==2?source:null);
                    instance.SelectSourceSpellState(1,0);
                    Check(Vector3.Distance(instance.Nodes[0].transform.position,source.transform.position)<.00001f&&instance.SourcePositionUpdates==1,"original Birth places actual SuperSpell root at explicit SourceAuto "+file);
                    Check(instance.UnboundSourcePlacements==0&&instance.UnrestoredSourceActionSideEffects==1,"placement succeeds while missing full SuperSpell callbacks remain visible "+file);
                    source.transform.position+=Vector3.one;
                    instance.SelectSourceSpellState(3,.1f);
                    Check(instance.SourcePositionUpdates==1&&instance.Nodes[0].transform.position!=source.transform.position,"ordinary source location retains cleared dirty state on Action "+file);
                    instance.Seek(0);instance.SelectSourceSpellState(1,0);
                    Check(instance.SourcePositionUpdates==1&&Vector3.Distance(instance.Nodes[0].transform.position,source.transform.position)<.00001f,"rewind resets placement dirty state and re-resolves source "+file);
                }
            }
            var dynamicProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"1438c953f1f357de128e06d3.native-effect.json")));
            var dynamicRoot=Array.Find(dynamicProgram.spell_transforms,s=>s.node==0);dynamicRoot.location=18;dynamicRoot.facing=9;
            using(var instance=new NativeEffectInstance(dynamicProgram,assets,0,32,32))
            {
                int faces=0;instance.BindSourcePlacement(location=>location==18?source:null,face=>{if(face==9){faces++;return source;}return null;});
                instance.SelectSourceSpellState(1,0);source.transform.position+=Vector3.one;
                instance.SelectSourceSpellState(3,.1f);
                Check(instance.SourcePositionUpdates==2&&faces==2&&instance.UnboundSourcePlacements==0,"source SuperSpell Action invalidates location18 and facing9 only");
            }
        }
        finally{AssetRegistry.ReleaseTransient(source);}
        Debug.Log("NATIVE EFFECT SOURCE PLACEMENT: "+passed+" checks passed; full SuperSpell/child spell routing and battle capture composition remain incomplete");
        NativeITweenCompleteChecks.Run();
    }
}
