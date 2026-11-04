using System;
using UnityEngine;

public static class NativeRandomizedParticleChecks
{
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original randomized particle: "+message);passed++;};
        GameObject target=new GameObject("Controlled source clock"),oracle=new GameObject("Engine fresh time oracle");
        try
        {
            Func<GameObject,ParticleSystem> create=go=>{var p=go.AddComponent<ParticleSystem>();p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=p.main;main.duration=.5f;main.loop=true;main.startLifetime=.15f;main.startSpeed=1;main.startDelay=.1f;p.useAutoRandomSeed=false;p.randomSeed=482;return p;};
            var a=create(target);var b=create(oracle);var clock=new NativeRandomizedParticleClock(a);
            foreach(float time in new[]{0f,.2f,.3f,.3f,.01f,1.3f})
            {
                clock.Sample(time);b.Simulate(0,true,true);b.Simulate(time,true,false);
                check(Mathf.Abs(a.time-b.time)<.001f,"engine timeline time including start delay at "+time);
                check(a.particleCount==b.particleCount,"engine emission at "+time);
            }
            target.SetActive(false);float before=a.time;clock.Sample(2);check(a.time==before,"inactive hierarchy never advances");
            target.SetActive(true);clock.Play();int restarts=clock.Restarts;clock.Sample(.2f);check(clock.Restarts==restarts+1,"OnBehaviourPlay forces source restart");
            clock.Pause();restarts=clock.Restarts;clock.Sample(.2f);check(clock.Restarts==restarts+1,"OnBehaviourPause resets last playable time");
            check(a.randomSeed==482&&!a.useAutoRandomSeed,"particle clock does not invent seed or change adapter ownership");
        }
        finally{AssetRegistry.ReleaseTransient(target);AssetRegistry.ReleaseTransient(oracle);}
        Debug.Log("Original randomized particle checks passed: "+passed);
    }
}
