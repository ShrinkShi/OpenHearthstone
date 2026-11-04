using System;
using System.IO;
using UnityEngine;

public static class NativeVisibilityChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original SetVisibility: "+reason);passed++;}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        foreach(var mode in new[]{0,1,2,3,4})
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/5a360810c62fabb080bc018b.start-spell.json"));
            int target=mode==0?1:7;bool initial=mode==3;program.nodes[7].visible=initial;
            var action=new NativeAction{name="SetVisibility",@params=new[]{
                new NativeParam{name="gameObject",node=target},new NativeParam{name="toggle",number=mode==2||mode==3?1:0},
                new NativeParam{name="visible",number=1},new NativeParam{name="resetOnExit",number=mode==4?0:1}}};
            program.machines=new[]{new NativeMachine{start="A",events=new NativeTransition[0],states=new[]{
                new NativeState{name="A",actions=new[]{action,new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=1},new NativeParam{name="finishEvent",text="FINISHED"}}}},transitions=new[]{new NativeTransition{@event="FINISHED",state="B"}}},
                new NativeState{name="B",actions=new NativeAction[0],transitions=new NativeTransition[0]}}}};
            using(var effect=new NativeEffectInstance(program,assets))
            {
                effect.Seek(.5f);var renderer=effect.Nodes[7].GetComponent<Renderer>();
                Check(renderer.enabled==(mode==0?initial:mode==3?false:true),"local renderer/toggle matches original entry, renderer-less parent does not recurse");
                Check(effect.Nodes[3].GetComponent<Renderer>().enabled==program.nodes[3].visible,"child GodRays renderer is untouched by parent SetVisibility");
                effect.Seek(1.1f);
                Check(renderer.enabled==(mode==4?true:initial),"resetOnExit restores original renderer enabled, disabled reset preserves changed state");
                effect.Seek(0);
                Check(renderer.enabled==(mode==0?initial:mode==3?false:true),"rewind reconstructs visibility before applying one original entry");
            }
        }
        Debug.Log("NATIVE VISIBILITY: "+passed+" checks passed; original local renderer toggle/reset restored, complete actor binding remains incomplete");
        NativeOffscreenFXChecks.Run();
    }
}
