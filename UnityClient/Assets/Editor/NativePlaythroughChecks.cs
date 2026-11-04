using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativePlaythroughChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original AudioPlaythrough: "+reason);passed++;}
    public static void Run()
    {
        passed=0;var timer=new NativePlaythroughDelay();timer.Reset(.2f);
        Check(!timer.Step(.1f),"subtracting remaining time yields rather than plays");
        timer.Reset(.2f);Check(!timer.Step(.1f)&&!timer.Step(.1f),"reentrant iterators decrement the action's shared reset timer");
        Check(timer.Step(0)&&timer.Step(0),"each iterator independently plays after shared timer is zero");
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.actor-spell.json")));
        var action=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlaythroughAction");
        action.Param("m_Delay").number=.2f;action.Param("m_FinishedEvent").text="SOUND_DONE";
        var volume=action.Param("m_VolumeScale");volume.variable="volume";volume.is_none=false;volume.none_known=true;
        action.Param("m_OneShotClip").object_null=false;action.Param("m_OneShotClip").unresolved=true;
        program.machines=new[]{new NativeMachine{start="A",floats=new[]{new NativeFloat{name="volume",value=.2f}},events=new NativeTransition[0],states=new[]{
            new NativeState{name="A",actions=new[]{action,new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=.1f},new NativeParam{name="finishEvent",text="NEXT"}}}},transitions=new[]{new NativeTransition{@event="NEXT",state="B"}}},
            new NativeState{name="B",actions=new[]{new NativeAction{name="AnimateFloat",@params=new[]{new NativeParam{name="floatVariable",variable="volume"},new NativeParam{name="animCurve",curve=new[]{new NativeKey{time=0,value=.8f},new NativeKey{time=1,value=.8f}}}}}},transitions=new[]{new NativeTransition{@event="SOUND_DONE",state="done"}}},
            new NativeState{name="done",actions=new NativeAction[0],transitions=new NativeTransition[0]}}}};
        int calls=0;
        using(var effect=new NativeEffectInstance(program,assets,audioCallback:cue=>calls++))
        {
            effect.Seek(.15f);Check(effect.CurrentStates[0]=="B"&&calls==0,"source wait changes state while delayed coroutine remains pending");
            effect.Seek(.23f);Check(calls==1&&effect.AudioCues.Count==1,"delayed sound survives state exit and ignored legacy clip field");
            Check(effect.CurrentStates[0]=="done","delayed finished event is delivered to current FSM state");
            Check(Mathf.Abs(effect.AudioCues[0].volume-.6f)<1e-5f,"volume is read at Play after the next state changes it");
            Check(effect.PlaythroughStarts==1&&effect.PlaythroughFinishes==1&&effect.UnboundAudioActions==0,"one source action starts and finishes once");
            effect.Seek(.05f);effect.Seek(.23f);Check(calls==1&&effect.AudioCues.Count==1,"rewind reconstructs timer without duplicate physical playback");
        }
        action.Param("m_FinishedEvent").text="";volume.variable="";volume.number=1;
        program.machines[0].states=new[]{new NativeState{name="A",actions=new[]{action},transitions=new[]{new NativeTransition{@event="FINISHED",state="done"}}},new NativeState{name="done",actions=new NativeAction[0],transitions=new NativeTransition[0]}};
        using(var effect=new NativeEffectInstance(program,assets))
        {
            effect.Seek(.2f);Check(effect.CurrentStates[0]=="A"&&effect.PlaythroughFinishes==0,"automatic FINISHED waits for coroutine resume rather than predicted deadline");
            effect.Seek(.23f);Check(effect.CurrentStates[0]=="done"&&effect.PlaythroughFinishes==1,"automatic FINISHED follows Play completion");
        }
        var wait=new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=.1f},new NativeParam{name="finishEvent",text="NEXT"}}};
        program.machines[0].states=new[]{new NativeState{name="A",actions=new[]{action,wait},transitions=new[]{new NativeTransition{@event="NEXT",state="B"}}},new NativeState{name="B",actions=new[]{wait},transitions=new[]{new NativeTransition{@event="NEXT",state="A"}}}};
        using(var effect=new NativeEffectInstance(program,assets))
        {
            effect.Seek(.35f);Check(effect.PlaythroughStarts==2&&effect.PlaythroughFinishes==2,"reentry starts two iterators and both finish using shared action timer");
            Check(effect.AudioCues.Count==2&&effect.AudioCues.All(c=>c.time<.35f),"shared timer yields two sounds earlier than two independent delay snapshots");
        }
        Debug.Log("NATIVE PLAYTHROUGH: "+passed+" checks passed; original coroutine continuation/live reads restored, full SoundManager/Actor policy remains incomplete");
        NativeColorInterpolationChecks.Run();
    }
}
