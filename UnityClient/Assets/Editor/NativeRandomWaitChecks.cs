using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeRandomWaitChecks
{
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original RandomWait: "+message);passed++;};
        int calls=0;Func<float,float,float> sample=(a,b)=>{calls++;return .25f;};
        var zero=new NativeRandomWait(0,-1,false,0,sample);check(zero.Finished&&calls==0,"both nonpositive finish without sampling");
        var wait=new NativeRandomWait(.1f,.4f,false,9,sample);check(calls==1&&!wait.Finished,"one sample on enter");
        check(!wait.Step(.25f,100)&&!wait.Finished,"strict boundary does not finish");
        check(wait.Step(.001f,100)&&wait.Finished,"scaled delta exceeds threshold");
        check(!wait.Step(1,100)&&calls==1,"finished action neither resamples nor resends");
        var real=new NativeRandomWait(.1f,.4f,true,5,sample);
        check(!real.Step(100,5.25f),"realtime ignores scaled delta at exact boundary");
        check(real.Step(0,5.251f),"realtime elapsed exceeds threshold while scaled clock paused");
        var mixed=new NativeRandomWait(-1,1,false,0,sample);check(!mixed.Finished,"one positive bound still samples");
        var saved=UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(117);var expected=Enumerable.Range(0,64).Select(_=>UnityEngine.Random.Range(.1f,.4f)).ToArray();
            UnityEngine.Random.InitState(718);var unchanged=UnityEngine.Random.state;var sampler=new NativeRandomWaitSampler(117);
            var actual=Enumerable.Range(0,64).Select(_=>sampler.Range(.1f,.4f)).ToArray();
            for(int i=0;i<actual.Length;i++)check(actual[i]==expected[i],"original Unity float Range sample "+i);
            check(UnityEngine.Random.state.Equals(unchanged),"sampling preserves unrelated global state");
            sampler.Reset(117);check(sampler.Range(.1f,.4f)==expected[0],"rewind restarts isolated source sequence");
            check(actual.Distinct().Count()>32,"durations are random rather than midpoint");
        }
        finally{UnityEngine.Random.state=saved;}
        var assets=new AssetRegistry(Path.GetFullPath("../local"));int programs=0,actions=0;
        foreach(string path in Directory.GetFiles(Path.Combine(assets.Root),"*.native-effect.json"))
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(path));
            var originals=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(a=>a.name=="RandomWaitAction").ToArray();
            if(originals.Length==0)continue;programs++;
            foreach(var source in originals)
            {
                actions++;check(source.Param("m_RealTime").number==0,"actual source uses scaled clock");
                program.machines=new[]{new NativeMachine{owner=0,owner_known=true,start="Delay",events=new NativeTransition[0],states=new[]{
                    new NativeState{name="Delay",actions=new[]{source},transitions=new[]{new NativeTransition{@event=source.Param("m_FinishEvent").text,state="Done"}}},
                    new NativeState{name="Done",actions=new NativeAction[0],transitions=new NativeTransition[0]}}}};
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {
                    effect.Seek(0);check(effect.RandomWaitStarts==1&&effect.UnboundRandomWaitActions==0,"real source parameters start");
                    effect.Seek(2);check(effect.RandomWaitFinishes==1,"real source finish event once");
                    effect.Seek(0);check(effect.RandomWaitStarts==1,"production rewind resets clock and counts");
                    effect.Seek(2);check(effect.RandomWaitFinishes==1&&effect.TransitionLimitHits==0,"production replay completes once");
                }
            }
        }
        check(actions==37&&programs==36,"all current original RandomWait programs covered");
        File.WriteAllText("../work/native-random-wait-checks.json","{\"passed\":"+passed+",\"programs\":"+programs+",\"actions\":"+actions+",\"complete\":false}");
        Debug.Log("Original RandomWait checks passed: "+passed);
    }
}
