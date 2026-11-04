using System;
using System.IO;
using UnityEngine;

public static class NativeColorInterpolationChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original ColorInterpolate: "+reason);passed++;}
    static void Near(Color a,Color b,string label){for(int i=0;i<4;i++)Check(Mathf.Abs(a[i]-b[i])<.001f,label+" channel "+i);}
    static NativeAction Action(int count)
    {
        var p=new NativeParam[count+4];p[0]=new NativeParam{name="colors",kind=12,array_known=true,array_type="HutongGames.PlayMaker.FsmColor",array_count=count};
        for(int i=0;i<count;i++)p[i+1]=new NativeParam{kind=25};
        p[count+1]=new NativeParam{name="time",number=1};p[count+2]=new NativeParam{name="storeColor"};p[count+3]=new NativeParam{name="realTime"};
        return new NativeAction{name="ColorInterpolate",@params=p};
    }
    public static void Run()
    {
        passed=0;var action=Action(3);var first=new Color(2,-1,.25f,.6f);var middle=new Color(.5f,.7f,.9f,.8f);var last=new Color(.1f,.2f,.3f,.4f);
        action.@params[1].color=first;action.@params[2].color=middle;action.@params[3].color=last;
        var animation=new NativeColorInterpolation(action,p=>p.number,p=>p.color);
        Near(animation.Value,first,"OnEnter first original color including HDR");
        animation.Step(.25f);Near(animation.Value,Color.Lerp(first,middle,.5f),"first equal interval");
        animation.Step(.25f);Near(animation.Value,middle,"exact interval boundary");
        animation.Step(.5f);Near(animation.Value,last,"exact duration last color");Check(!animation.Finished,"original strict greater-than completion");
        animation.Step(.01f);Check(animation.Finished,"completes beyond duration");Near(animation.Value,last,"completion endpoint");
        var live=new NativeColorInterpolation(action,p=>p.number,p=>p.color);live.Step(.25f);
        action.@params[2].color=Color.magenta;live.Step(.25f);Near(live.Value,Color.magenta,"live color read after entry");
        action.@params[2].color=Color.green;live.Step(0);Near(live.Value,Color.green,"zero delta update still rereads live color");action.@params[2].color=Color.magenta;
        action.Param("time").number=2;live.Step(.25f);Near(live.Value,Color.Lerp(first,Color.magenta,.75f),"live duration changes current interval");
        var empty=new NativeColorInterpolation(Action(0),p=>p.number,p=>p.color);Check(empty.Finished&&!empty.InitialWrite,"empty array finishes without store write");
        var one=Action(1);one.@params[1].color=first;var single=new NativeColorInterpolation(one,p=>p.number,p=>p.color);Check(single.Finished&&single.InitialWrite,"single color writes and finishes in OnEnter");Near(single.Value,first,"single color exact");
        var zero=Action(2);zero.@params[2].color=last;zero.Param("time").number=0;var immediate=new NativeColorInterpolation(zero,p=>p.number,p=>p.color);immediate.Step(.01f);Check(immediate.Finished,"zero time finishes on positive update");Near(immediate.Value,last,"zero time final endpoint");
        var invalid=Action(2);invalid.@params[0].array_known=false;bool rejected=false;
        try{new NativeColorInterpolation(invalid,p=>p.number,p=>p.color);}catch(ArgumentException){rejected=true;}Check(rejected,"missing original array span is not guessed");
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/4fc0fe4fe4824ecbe3db289c.native-effect.json"));
        using(var effect=new NativeEffectInstance(program,assets,context:new NativeSpellContext(0,lifecycle:"Death")))
        {
            effect.Seek(.9f);var material=effect.Nodes[3].GetComponent<Renderer>().sharedMaterial;
            var state=Array.Find(program.machines[0].states,s=>s.name=="Death Poof");var source=Array.Find(state.actions,a=>a.name=="ColorInterpolate");
            Near(material.GetColor("_TintColor"),Color.Lerp(source.@params[1].color,source.@params[2].color,.4f),"actual original book intermediate material color");
            effect.Seek(1.1f);Near(material.GetColor("_TintColor"),source.@params[2].color,"actual original book final material color");
            Check(effect.ColorInterpolationStarts==1&&effect.ColorInterpolationFinishes==1&&effect.UnboundColorInterpolationActions==0,"real source starts and finishes once without unbound array");
        }
        Debug.Log("NATIVE COLOR INTERPOLATION: "+passed+" checks passed; original scaled array interpolation, realtime and complete full-card playback remain incomplete");
        NativeDelayedVisibilityChecks.Run();
    }
}
