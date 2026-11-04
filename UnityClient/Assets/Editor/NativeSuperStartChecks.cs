using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEngine;

public static class NativeSuperStartChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original SuperSpell start: "+reason);passed++;}
    public static void Run()
    {
        passed=0;var calls=new List<string>();int pending=0,state=0;float min=.2f,max=.4f;IEnumerator wait=null;
        bool gameWait=false,hasStart=false,usableAction=true;Action<int> stateFinished=null;Action finished=null;NativeSuperStart flow=null;
        var spell=new NativeStartSpell{IsConcreteSpell=true,ActiveState=()=>state,HasUsableState=t=>usableAction,
            SafeActivateState=t=>{calls.Add("safe "+t);state=t;},ActivateState=t=>{calls.Add("activate "+t);state=t;},UpdateTransform=()=>calls.Add("transform"),
            AddStateFinished=c=>{calls.Add("add state callback");stateFinished=c;},RemoveStateFinished=c=>{Check(c==stateFinished,"remove exact callback");calls.Add("remove state callback");stateFinished=null;},
            AddFinished=c=>{calls.Add("add finished callback");finished=c;}};
        var services=new NativeSuperStartServices{CheckGameEventWait=()=>{calls.Add("game wait");return gameWait;},
            DelayMin=()=>min,DelayMax=()=>max,AddPending=n=>{pending+=n;calls.Add("pending "+n);},RandomRange=(a,b)=>{Check(a==min&&b==max,"sample source delay bounds");calls.Add("random");return .3f;},
            StartCoroutine=e=>{wait=e;Check(e.MoveNext(),"coroutine yields");},HasStart=()=>{calls.Add("has start");return hasStart;},
            SpawnStart=()=>{calls.Add("spawn");flow.Start=spell;},DoActionNow=()=>calls.Add("action now")};
        flow=new NativeSuperStart(services);gameWait=true;flow.DoAction();
        Check(calls.SequenceEqual(new[]{"game wait"})&&pending==0,"game event wait precedes all start gates");
        calls.Clear();gameWait=false;flow.DoAction();
        Check(calls.SequenceEqual(new[]{"game wait","pending 1","random"})&&pending==1,"pending increments before random sample and wait");
        Check(wait.Current is WaitForSeconds,"actual Unity scaled WaitForSeconds, no synthetic timeline");
        var seconds=(float)typeof(WaitForSeconds).GetField("m_Seconds",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(wait.Current);
        Check(seconds==.3f,"yield preserves exact sampled duration");calls.Clear();
        Check(!wait.MoveNext()&&pending==0&&calls.SequenceEqual(new[]{"pending -1","has start","action now"}),"resume decrements before prefab gate and never rechecks game events or delay");
        foreach(float value in new[]{0f,-1f,Mathf.Epsilon})
        {calls.Clear();min=value;max=1;Check(!flow.CheckAndWaitForStartDelay()&&calls.Count==0,"original minimum threshold avoids waiting");}
        min=float.NaN;max=1;services.RandomRange=(a,b)=>.3f;calls.Clear();
        Check(flow.CheckAndWaitForStartDelay()&&pending==1,"unordered minimum comparison enters wait");wait.MoveNext();
        min=max=0;hasStart=true;state=0;calls.Clear();flow.Start=null;
        Check(flow.CheckAndWaitForStartPrefab()&&state==1&&calls.SequenceEqual(new[]{"has start","spawn","add state callback","safe 1"}),"spawn then register before Birth activation");
        calls.Clear();stateFinished(2);Check(calls.Count==0&&stateFinished!=null,"non-Birth completion ignored");
        stateFinished(1);Check(calls.SequenceEqual(new[]{"remove state callback","action now"})&&stateFinished==null,"Birth callback removes itself before action");
        state=2;calls.Clear();Check(!flow.CheckAndWaitForStartPrefab()&&calls.SequenceEqual(new[]{"has start"}),"existing Idle bypasses wait without new callback");
        state=1;calls.Clear();Check(flow.CheckAndWaitForStartPrefab()&&calls.SequenceEqual(new[]{"has start","add state callback"}),"existing Birth retained without reactivation");
        state=0;flow.Start=spell;spell.SafeActivateState=t=>state=0;calls.Clear();
        Check(!flow.CheckAndWaitForStartPrefab()&&flow.Start==null,"missing usable Birth clears start and continues action");
        spell.SafeActivateState=t=>{calls.Add("safe "+t);state=t;};flow.Start=spell;state=2;calls.Clear();
        flow.DoStartSpellAction();Check(flow.Start==null&&state==3&&calls.SequenceEqual(new[]{"add finished callback","activate 3"}),"usable Action callback precedes activation and releases start reference");
        calls.Clear();finished();Check(calls.SequenceEqual(new[]{"safe 5"})&&state==5,"Action finished transitions to Death");
        calls.Clear();finished();Check(calls.Count==0,"finished outside Action does not retrigger Death");
        flow.Start=spell;usableAction=false;calls.Clear();flow.DoStartSpellAction();
        Check(flow.Start==null&&calls.SequenceEqual(new[]{"transform","safe 5"}),"no Action updates transform and safely starts Death");
        foreach(var sample in new[]{("32e839f264392a2fd7b69e3e.native-effect.json",1.5f),("38ddc05994c8a8f377263c1e.native-effect.json",.5f)})
        {
            var source=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/"+sample.Item1));
            services.BindSourceDelays(source);services.RandomRange=(a,b)=>a;pending=0;calls.Clear();
            Check(services.DelayMin()==sample.Item2&&services.DelayMax()==sample.Item2,"actual authored AvengingWrath/Suckin delay");
            Check(flow.CheckAndWaitForStartDelay()&&pending==1,"real card source enters original coroutine");
            seconds=(float)typeof(WaitForSeconds).GetField("m_Seconds",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(wait.Current);
            Check(seconds==sample.Item2,"real card yields authored delay without arbitrary clamp");
            hasStart=false;wait.MoveNext();Check(pending==0,"real card wait releases pending count on resume");
        }
        Debug.Log("NATIVE SUPER START: "+passed+" checks passed; original gate/coroutine/start-state order only, concrete start spawning/history/scene binding remains incomplete");
        NativeSuperActionChecks.Run();
    }
}
