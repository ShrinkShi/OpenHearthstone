using UnityEngine;

// Source ControlRandomizedParticleControlPlayable time branches; not Unity's default particle playable.
public sealed class NativeRandomizedParticleClock
{
    readonly ParticleSystem particle;
    float lastTime=-1,systemTime;
    public int Restarts{get;private set;}
    public NativeRandomizedParticleClock(ParticleSystem source)
    {particle=source;particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);}
    public void Play(){lastTime=-1;}
    public void Pause(){lastTime=-1;}
    public void Sample(float time)
    {
        if(particle==null||!particle.gameObject.activeInHierarchy)return;
        if(!Mathf.Approximately(lastTime,-1)&&Mathf.Approximately(lastTime,time))return;
        float halfStep=Time.fixedDeltaTime*.5f,delta=time-lastTime;
        var main=particle.main;
        float delay=main.startDelay.Evaluate(particle.randomSeed),cycle=main.duration+delay;
        float previous=time<=cycle?systemTime-delay:systemTime;
        bool restart=time<lastTime||time<halfStep||Mathf.Approximately(lastTime,-1)||delta>main.duration||Mathf.Abs(previous-particle.time)>=Time.maximumParticleDeltaTime;
        if(restart)
        {particle.Simulate(0,true,true);particle.Simulate(time,true,false);systemTime=time;Restarts++;}
        else
        {
            float length=time<=cycle?cycle:main.duration;
            float target=time%length,step=target-systemTime;
            if(step<-halfStep)step=target+cycle-systemTime;
            particle.Simulate(step,true,false);systemTime=target;
        }
        lastTime=time;
    }
}
