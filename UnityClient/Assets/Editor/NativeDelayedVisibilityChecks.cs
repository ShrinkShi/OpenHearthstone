using System;
using System.IO;
using UnityEngine;

public static class NativeDelayedVisibilityChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original delayed visibility: "+reason);passed++;}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        string file="../local/runtime/4fc0fe4fe4824ecbe3db289c.native-effect.json";
        for(int mode=0;mode<6;mode++)
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(file));program.nodes[4].active=false;
            var action=new NativeAction{name="SetVisibilityRecursiveDelayAction",@params=new[]{
                new NativeParam{name="gameObject",node=mode==0?2:mode==4?-1:3},
                new NativeParam{name="visible",number=0},new NativeParam{name="includeChildren",number=mode==0?1:0},
                new NativeParam{name="m_Delay",number=.25f,none_known=mode!=3,is_none=mode==2}}};
            var actions=mode==5?new[]{action,new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=.1f},new NativeParam{name="finishEvent",text="FINISHED"}}}}:new[]{action};
            program.machines=new[]{new NativeMachine{start="A",events=new NativeTransition[0],states=new[]{
                new NativeState{name="A",actions=actions,transitions=new[]{new NativeTransition{@event="FINISHED",state="B"}}},
                new NativeState{name="B",actions=new NativeAction[0],transitions=new NativeTransition[0]}}}};
            using(var effect=new NativeEffectInstance(program,assets))
            {
                effect.Seek(.05f);var child=effect.Nodes[3].GetComponent<Renderer>();
                Check(child.enabled==(mode!=2),"delayed operation waits, None delay runs on first update");
                effect.Seek(.3f);
                Check(child.enabled==(mode==3||mode==4||mode==5),"completed operation hides, unknown delay/null target/early state exit preserve renderer");
                Check(effect.Nodes[4].GetComponent<Renderer>().enabled,"inactive descendants are not included by original default recursion");
                Check(effect.DelayedVisibilityFinishes==(mode==3||mode==5?0:1),"Finish happens once, including null target; incomplete/aborted action never finishes");
                Check(effect.UnboundDelayedVisibilityActions==(mode==3?1:0),"missing None metadata is audited instead of guessed");
                effect.Seek(.5f);Check(effect.DelayedVisibilityFinishes==(mode==3||mode==5?0:1),"no repeated writes after finishing or leaving state");
            }
        }
        var source=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(file));
        using(var effect=new NativeEffectInstance(source,assets,context:new NativeSpellContext(0,lifecycle:"Death")))
        {
            // Original Death Init waits .3, Death Card waits .5, then Poof delays .25.
            effect.Seek(1.04f);Check(effect.Nodes[3].GetComponent<Renderer>().enabled,"actual original book remains visible before Poof delay");
            effect.Seek(1.10f);Check(!effect.Nodes[3].GetComponent<Renderer>().enabled,"actual original book hides after .3 + .5 + .25 source sequence");
            Check(effect.DelayedVisibilityStarts==1&&effect.DelayedVisibilityFinishes==1&&effect.UnboundDelayedVisibilityActions==0,"real source action binds and completes once");
        }
        Debug.Log("NATIVE DELAYED VISIBILITY: "+passed+" checks passed; original update/finish/hierarchy timing, complete full-card playback remains incomplete");
        NativeVisibilityChecks.Run();
    }
}
