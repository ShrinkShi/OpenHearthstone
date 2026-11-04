using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class FireblastParticleChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);Debug.Log("FIREBLAST PARTICLE "+message);}
    public static void Run()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var missile=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"96a84893c7b182959b4ccb1f.fireblast-missile.json")));
        var flightParticles=new[]{"FireParticleLeft","FireParticleFill","FireParticleRight","Sparks01","Smoke01"};
        using(var effect=new NativeEffectInstance(missile,assets,sourceSimulationSpaceNodes:flightParticles))
        {
            foreach(string name in flightParticles)
            {
                int index=Array.FindIndex(missile.nodes,n=>n.name==name);
                Check(index>=0&&missile.nodes[index].particle.space==(int)ParticleSystemSimulationSpace.World,"source flight particle serializes World: "+name);
                Check(effect.Particles[index].main.simulationSpace==ParticleSystemSimulationSpace.World,"flight particle keeps source World simulation: "+name);
            }
            Debug.Log("FIREBLAST PARTICLE source flight World-space particles bound");
        }
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"601bc21dbfbb00c1dde00179.fireblast-impact.json")));
        GameObject root;
        using(var effect=new NativeEffectInstance(program,assets))
        {
            root=effect.Root;
            int index=Array.FindIndex(program.nodes,n=>n.name=="Rocks01");
            int ring=Array.FindIndex(program.nodes,n=>n.name=="FireImpactRing");
            Check(program.nodes[ring].particle.space==(int)ParticleSystemSimulationSpace.Local&&effect.Particles[ring].main.simulationSpace==ParticleSystemSimulationSpace.World,"unmodified impact ring preserves current runtime mapping");
            var particle=effect.Particles[index];var collision=particle.collision;
            var plane=effect.Nodes[program.nodes[index].particle.collision.planes[0]].transform;
            Check(collision.enabled&&collision.type==ParticleSystemCollisionType.Planes&&collision.GetPlane(0)==plane,"actual source Rocks01 plane reference bound");
            Check(Mathf.Abs(collision.dampen.constant-.4f)<.00001f&&collision.bounce.constant==1&&collision.lifetimeLoss.constant==0,"actual source damping/bounce/lifetime values");
            // Rocks01原始禁用；这里只激活受控粒子样本，不冒充控制器启用时刻。
            particle.gameObject.SetActive(true);
            var main=particle.main;var emission=particle.emission;emission.enabled=false;
            var origin=plane.position+plane.up*.1f;var velocity=-plane.up*10;
            if(main.simulationSpace==ParticleSystemSimulationSpace.Local){origin=particle.transform.InverseTransformPoint(origin);velocity=particle.transform.InverseTransformVector(velocity);}
            var sample=new ParticleSystem.Particle{position=origin,velocity=velocity,startLifetime=5,remainingLifetime=5,startSize=.001f,startColor=Color.white,randomSeed=123};
            var values=new ParticleSystem.Particle[8];
            float[] speeds=new float[2];
            for(int enabled=0;enabled<2;enabled++)
            {
                particle.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);collision.enabled=enabled==1;
                particle.Play(false);particle.Pause(false);
                particle.SetParticles(new[]{sample},1);particle.Simulate(.04f,false,false,true);
                Check(particle.GetParticles(values)==1,"controlled source particle survives sample "+enabled);
                var observed=values[0].velocity;if(main.simulationSpace==ParticleSystemSimulationSpace.Local)observed=particle.transform.TransformVector(observed);
                speeds[enabled]=Vector3.Dot(observed,plane.up);
            }
            Check(speeds[0]<0&&speeds[1]>0,"original plane causes real velocity bounce versus collision-disabled control");
            Debug.Log("FIREBLAST PARTICLE observed normal velocities "+speeds[0]+" -> "+speeds[1]);
        }
        Check(root==null,"source impact/particle/plane physically released");
        Debug.Log("FIREBLAST PARTICLE CHECKS passed; controlled native particle, no official frame comparison");
    }
}
