using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeColorSpeedChecks
{
    [Serializable] sealed class RawGradient
    {
        public Color key0,key1,key2,key3,key4,key5,key6,key7;
        public int ctime0,ctime1,ctime2,ctime3,ctime4,ctime5,ctime6,ctime7,atime0,atime1,atime2,atime3,atime4,atime5,atime6,atime7;
        public int m_Mode,m_NumColorKeys,m_NumAlphaKeys,m_ColorSpace;
        public Gradient Create()
        {
            var values=new[]{key0,key1,key2,key3,key4,key5,key6,key7};var ct=new[]{ctime0,ctime1,ctime2,ctime3,ctime4,ctime5,ctime6,ctime7};var at=new[]{atime0,atime1,atime2,atime3,atime4,atime5,atime6,atime7};
            var color=new GradientColorKey[m_NumColorKeys];var alpha=new GradientAlphaKey[m_NumAlphaKeys];
            for(int i=0;i<color.Length;i++)color[i]=new GradientColorKey(values[i],ct[i]/65535f);
            for(int i=0;i<alpha.Length;i++)alpha[i]=new GradientAlphaKey(values[i].a,at[i]/65535f);
            var result=new Gradient();result.SetKeys(color,alpha);result.mode=(GradientMode)m_Mode;return result;
        }
    }
    [Serializable] sealed class RawColor{public int minMaxState;public Color minColor,maxColor;public RawGradient minGradient,maxGradient;public ParticleSystem.MinMaxGradient Create(){switch(minMaxState){case 0:return new ParticleSystem.MinMaxGradient(maxColor);case 1:return new ParticleSystem.MinMaxGradient(maxGradient.Create());case 2:return new ParticleSystem.MinMaxGradient(minColor,maxColor);case 3:return new ParticleSystem.MinMaxGradient(minGradient.Create(),maxGradient.Create());default:throw new Exception("Untested original gradient mode");}}}
    [Serializable] sealed class RawModule{public bool enabled;public RawColor gradient;public Vector2 range;}
    [Serializable] sealed class Module{public int node;public RawModule original;}
    [Serializable] sealed class ProgramRecord{public string file;public Module[] modules;}
    [Serializable] sealed class Audit{public ProgramRecord[] programs;}
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original ColorBySpeed: "+message);passed++;};
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var audit=JsonUtility.FromJson<Audit>(File.ReadAllText("../manifests/native-color-speed-refresh.json"));
        foreach(var record in audit.programs)
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,record.file)));
            using(var effect=new NativeEffectInstance(program,assets,0,32,32))
            foreach(var row in record.modules)
            {
                var actual=effect.Particles[row.node];var module=actual.colorBySpeed;var raw=row.original;var expected=raw.gradient.Create();
                for(var parent=actual.transform;parent!=null;parent=parent.parent)parent.gameObject.SetActive(true);
                check(module.enabled&&module.range==raw.range,"actual production particle enables exact original range");
                check(module.color.mode==expected.mode,"original single/two gradient mode");
                check(!program.nodes[row.node].particle.unsupported.Contains("ColorBySpeedModule"),"only restored module leaves unsupported list");
                var oracleGo=new GameObject("Original source ColorBySpeed engine oracle");var oracle=oracleGo.AddComponent<ParticleSystem>();
                try
                {
                    var oracleModule=oracle.colorBySpeed;oracleModule.enabled=true;oracleModule.color=expected;oracleModule.range=raw.range;
                    foreach(var particle in new[]{actual,oracle}){particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);particle.useAutoRandomSeed=false;particle.randomSeed=482;var lifetime=particle.colorOverLifetime;lifetime.enabled=false;}
                    foreach(float speed in new[]{0f,raw.range.y*.25f,raw.range.y*.75f,raw.range.y,raw.range.y*2})
                    {
                        foreach(var particle in new[]{actual,oracle})
                        {
                            particle.Simulate(0,false,true,false);
                            var sample=new ParticleSystem.Particle{position=Vector3.zero,velocity=Vector3.right*speed,startLifetime=10,remainingLifetime=10,startColor=new Color(.6f,.8f,.4f,.7f),randomSeed=777};particle.SetParticles(new[]{sample},1);
                        }
                        var a=new ParticleSystem.Particle[actual.particleCount];var b=new ParticleSystem.Particle[oracle.particleCount];actual.GetParticles(a);oracle.GetParticles(b);
                        check(a.Length==1&&b.Length==1,"controlled probe emits one actual particle "+record.file+"/"+row.node+" "+a.Length+"/"+b.Length);
                        Color value=a[0].GetCurrentColor(actual),reference=b[0].GetCurrentColor(oracle);
                        check((new Vector4(value.r,value.g,value.b,value.a)-new Vector4(reference.r,reference.g,reference.b,reference.a)).sqrMagnitude<.0001f,"engine sampled original color at speed "+speed);
                    }
                }
                finally{AssetRegistry.ReleaseTransient(oracleGo);}
            }
        }
        Debug.Log("Original ColorBySpeed checks passed: "+passed);
    }
}
