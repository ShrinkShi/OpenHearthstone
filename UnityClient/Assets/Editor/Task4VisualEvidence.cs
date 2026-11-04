using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// 仅采集证据；不改变生产时钟、根调度或原资源。
public static class Task4VisualEvidence
{
    [Serializable] sealed class CameraRecord
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
        public float orthographicSize,near,far,aspect;
        public int width,height,cullingMask;
        public string colorSpace,format;
    }
    [Serializable] sealed class CloneRecord
    {
        public string prefab;
        public Vector3 position,scale;
        public Quaternion rotation;
        public int state,particles,trailPoints;
        public float[] trailTimes;
    }
    [Serializable] sealed class AudioRecord
    {
        public string file;
        public float callbackTime,clipTime,volume,pitch,spatialBlend;
    }
    [Serializable] sealed class Frame
    {
        public string card,phase,file,emissionFile,transmissionFile,trigger;
        public int seat,step,particles,trailPoints,pending;
        public float time,flightFraction;
        public bool completed;
        public CloneRecord[] clones;
    }
    [Serializable] sealed class Sequence
    {
        public string card,before,after;
        public int seat;
        public CameraRecord camera;
        public Frame[] frames;
        public string[] events;
        public AudioRecord[] audio;
        public bool completed,released;
    }
    [Serializable] sealed class ShaderSample
    {
        public string name,file;
        public Color expected,actual;
        public float maxError;
    }
    [Serializable] sealed class Report
    {
        public string unity,gpu,api,colorSpace;
        public string clock="explicit monotonic 1/240 second; quantization <= 1 step; no wall clock";
        public string random="root System.Random(117), uniform Range and Next(2); particles 117+node; audio/random-wait/timeline 117";
        public string output="autonomous root C+D*K via production RenderAffine on fixed RGBA(.18,.18,.18,1); separate C/K; no board, glow/postprocess or audible mix";
        public bool officialReference=false,wholeEffectVerified=false;
        public Sequence[] sequences;
        public ShaderSample[] shaderSamples;
    }
    static string Folder(string[] args,string option,string fallback)
    {int index=Array.IndexOf(args,option);return Path.GetFullPath(index>=0?args[index+1]:fallback);}
    public static void Run()
    {
        var args=Environment.GetCommandLineArgs();
        string folder=Folder(args,"-task4Output","../work/task4/capture-a");Directory.CreateDirectory(folder);
        string inputs=Folder(args,"-task4Inputs","../work/task4/scenes");
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var types=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json")).cards.ToDictionary(c=>c.CardId,c=>c.Type);
        var report=new Report{unity=Application.unityVersion,gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceVersion,colorSpace=QualitySettings.activeColorSpace.ToString()};
        var sequences=new List<Sequence>();
        foreach(string card in new[]{"EX1_129","EX1_134"})foreach(int seat in new[]{0,1})
            sequences.Add(Capture(assets,types,inputs,folder,card,seat));
        report.sequences=sequences.ToArray();report.shaderSamples=ShaderProbe(folder);
        File.WriteAllText(Path.Combine(folder,"capture.json"),JsonUtility.ToJson(report,true));
        Debug.Log("TASK4 VISUAL: 4 sequences / 28 event frames; 7 shader samples; official comparison Unknown");
    }
    static Sequence Capture(AssetRegistry assets,Dictionary<string,int> types,string inputs,string folder,string card,int seat)
    {
        string beforeFile=Path.Combine(inputs,card+"-"+seat+"-before.json"),afterFile=Path.Combine(inputs,card+"-"+seat+"-after.json");
        var before=JsonUtility.FromJson<StateView>(File.ReadAllText(beforeFile));var after=JsonUtility.FromJson<StateView>(File.ReadAllText(afterFile));
        var play=after.events.First(e=>e.type=="CardPlay"&&e.card_id==card);
        var data=new SemanticEvent{type="SpellEffect",source=play.source,target=play.target,card_id=card,owner=play.owner};
        var positions=new Dictionary<int,Vector2>{{data.source,new Vector2(960,480)}};
        foreach(var p in before.players)
        {
            bool own=p.seat==seat;positions[p.hero.entity]=BattleLayout.Hero(own).center;positions[p.power.entity]=BattleLayout.Power(own).center;
            for(int i=0;i<p.field.Length;i++)positions[p.field[i].entity]=BattleLayout.Minion(own,i,p.field.Length).center;
        }
        var frames=new List<Frame>();var audio=new List<AudioRecord>();var phases=new HashSet<string>();
        var objects=new HashSet<UnityEngine.Object>();float now=0;
        var sequence=new Sequence{card=card,seat=seat,before=beforeFile,after=afterFile};
        using(var player=new NativeEffectPlayer(assets))
        {
            var random=new System.Random(117);player.CardType=id=>types.ContainsKey(id)?types[id]:0;
            player.AudioCue=cue=>audio.Add(new AudioRecord{file=cue.file,callbackTime=now,clipTime=cue.time,volume=cue.volume,pitch=cue.pitch,spatialBlend=cue.spatialBlend});
            player.SuperSpellScene=(evt,b,a,viewport,origin)=>
            {
                var scene=NativeSuperSpellBoard.FromPresentation(evt,b,a,id=>positions.ContainsKey(id)?positions[id]:(Vector2?)null,viewport,origin,player.CardType);
                scene.Range=(min,max)=>min+(max-min)*(float)random.NextDouble();scene.RandomBool=()=>random.Next(2)==0;return scene;
            };
            NativeSuperSpellPlayback spell=null;
            void Frame(string phase,int step,string trigger,float fraction=-1)
            {
                if(!phases.Add(phase))return;
                string file=card+"-seat"+seat+"-"+phase+".png";var texture=spell.RenderAffine(now);
                objects.Add(texture);objects.Add(spell.Effect.Transmission);
                string emission=file.Replace(".png","-C.png"),transmission=file.Replace(".png","-K.png");
                Save(texture,Path.Combine(folder,emission));Save(spell.Effect.Transmission,Path.Combine(folder,transmission));
                var previous=RenderTexture.active;var canvas=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
                try
                {
                    RenderTexture.active=canvas;GL.Clear(true,true,new Color(.18f,.18f,.18f,1));
                    player.RenderAffine(new Rect(0,0,texture.width,texture.height),texture,spell.Effect.Transmission,Matrix4x4.identity,texture.width,texture.height,Color.white);
                    Save(canvas,Path.Combine(folder,file));
                }
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(canvas);}
                var clones=spell.Flow.Instances.Select(clone=>
                {
                    var effect=clone.Effect;var node=effect.Nodes[0].transform;
                    var trails=effect.Nodes.Where(n=>n!=null).SelectMany(n=>n.GetComponents<TrailRenderer>()).ToArray();
                    return new CloneRecord{prefab=effect.Program.reference,position=node.position-spell.Effect.Root.transform.position,
                        rotation=node.rotation,scale=node.lossyScale,state=effect.ActiveSourceSpellState,particles=effect.ParticleCount,
                        trailPoints=trails.Sum(t=>t.positionCount),trailTimes=trails.Select(t=>t.time).ToArray()};
                }).ToArray();
                frames.Add(new Frame{card=card,seat=seat,phase=phase,file=file,emissionFile=emission,transmissionFile=transmission,step=step,time=now,trigger=trigger,flightFraction=fraction,
                    clones=clones,particles=clones.Sum(c=>c.particles),trailPoints=clones.Sum(c=>c.trailPoints),pending=spell.Flow.Pending,completed=spell.Flow.Completed});
            }
            for(int step=0;step<=2400;step++)
            {
                now=step/240f;if(!player.PrepareSource("task4",data,before,after,false,now))throw new Exception("Task4 source failed: "+card+"/"+seat);
                spell=player.SourceSpell("task4");
                if(sequence.camera==null)
                {
                    var cam=spell.Effect.Root.GetComponentInChildren<Camera>(true);var rt=cam.targetTexture;
                    sequence.camera=new CameraRecord{localPosition=cam.transform.localPosition,localRotation=cam.transform.localRotation,orthographicSize=cam.orthographicSize,
                        near=cam.nearClipPlane,far=cam.farClipPlane,aspect=cam.aspect,width=rt.width,height=rt.height,cullingMask=cam.cullingMask,colorSpace=QualitySettings.activeColorSpace.ToString(),format=rt.format.ToString()};
                    objects.Add(spell.Effect.Root);objects.Add(rt);
                }
                foreach(var clone in spell.Flow.Instances)
                {
                    objects.Add(clone.Effect.Root);
                    foreach(var cam in clone.Effect.Root.GetComponentsInChildren<Camera>(true))if(cam.targetTexture!=null)objects.Add(cam.targetTexture);
                }
                if(spell.Flow.Flights.Any())
                {
                    Frame("Birth",step,"first observed MissileBirth; includes source Birth before flight");
                    var flight=spell.Flow.Flights.First();
                    if(flight.Path!=null)
                    {
                        string line=spell.Events.First(e=>e.EndsWith(" MissileBirth 0"));float born=float.Parse(line.Split(' ')[0],System.Globalization.CultureInfo.InvariantCulture);
                        float fraction=(now-born-flight.FiredAt)/flight.Path.Duration;
                        if(fraction>=.1f)Frame("Early",step,"first flight >=10% of absolute path duration",fraction);
                        if(fraction>=.5f)Frame("Mid",step,"first flight >=50% of absolute path duration",fraction);
                    }
                }
                if(spell.Events.Any(e=>e.Contains(" Arrival ")))Frame("Arrival",step,"first Arrival callback");
                if(spell.Events.Any(e=>e.Contains(" ImpactState5 ")))Frame("Hit",step,"first actual impact State5");
                if(spell.Events.Any(e=>e.Contains(" MissileDeath ")))Frame("Death",step,"first MissileDeath activation; may share Arrival step, not forced later");
                if(spell.Flow.Completed&&spell.Flow.ActiveClones==0){Frame("Cleanup",step,"completed and zero active clones");break;}
            }
            if(phases.Count!=7||!spell.Flow.Completed||spell.Flow.ActiveClones!=0)throw new Exception("Incomplete Task4 phases: "+card+"/"+seat);
            sequence.frames=frames.ToArray();sequence.events=spell.Events.ToArray();sequence.audio=audio.ToArray();sequence.completed=spell.Flow.Completed;
        }
        sequence.released=objects.All(o=>o==null);if(!sequence.released)throw new Exception("Task4 owned resource remains");
        return sequence;
    }
    static void Save(RenderTexture texture,string file)
    {
        var previous=RenderTexture.active;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true);
        try{RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes(file,image.EncodeToPNG());}
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
    }
    static ShaderSample[] ShaderProbe(string folder)
    {
        var root=new GameObject("Task4 family24 audit");root.transform.position=new Vector3(97000,97000,97000);
        var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.transform.SetParent(root.transform,false);quad.layer=30;
        var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);quad.GetComponent<MeshFilter>().sharedMesh=mesh;
        var camera=new GameObject("Task4 Shader GPU camera").AddComponent<Camera>();camera.transform.SetParent(root.transform,false);camera.transform.localPosition=new Vector3(0,0,-5);
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.5f;camera.aspect=1;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;
        var rt=new RenderTexture(16,16,24,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();camera.targetTexture=rt;
        var material=new Material(Resources.Load<Shader>("NativeEffect"));quad.GetComponent<Renderer>().sharedMaterial=material;
        var tex=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);var pixels=new Texture2D(16,16,TextureFormat.RGBAFloat,false,true);
        var samples=new List<ShaderSample>();var previous=RenderTexture.active;var bias=Shader.GetGlobalVector("_GlobalMipBias");Shader.SetGlobalVector("_GlobalMipBias",Vector4.zero);
        void Sample(string name,Color tint,Color second,Color vertex,Color texture,float intensity,Color background,bool blend)
        {
            mesh.colors=Enumerable.Repeat(vertex,mesh.vertexCount).ToArray();tex.SetPixel(0,0,texture);tex.Apply();
            material.SetFloat("_OriginalFamily",24);material.SetTexture("_MainTex",tex);material.SetColor("_Color",tint);material.SetColor("_SecondColor",second);material.SetFloat("_Intensity",intensity);
            material.SetInt("_SrcBlend",blend?5:1);material.SetInt("_DstBlend",blend?10:0);material.SetInt("_CaptureSrcAlpha",blend?5:1);material.SetInt("_CaptureDstAlpha",blend?10:0);
            camera.backgroundColor=background;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,16,16),0,0);pixels.Apply();Color actual=pixels.GetPixel(8,8);
            float mask=texture.r*vertex.a*tint.a;Color expected=Color.LerpUnclamped(tint,second,mask)*intensity;expected.a=mask*intensity;
            if(blend)expected=expected*expected.a+background*(1-expected.a);
            float error=Enumerable.Range(0,4).Max(i=>Mathf.Abs(expected[i]-actual[i]));string file="shader-"+name+".png";Save(rt,Path.Combine(folder,file));
            samples.Add(new ShaderSample{name=name,file=file,actual=actual,expected=expected,maxError=error});
            if(error>.003f)throw new Exception("Task4 Shader mismatch: "+name+"/"+error);
        }
        try
        {
            var a=new Color(.2f,.4f,.6f,.8f);var b=new Color(.8f,.6f,.4f,.1f);
            Sample("mask",a,b,new Color(.2f,.8f,.4f,.5f),new Color(.25f,.99f,.7f,.9f),2,Color.clear,false);
            material.SetFloat("_Clock",1000);material.SetFloat("_AlphaIntensity",100);material.SetFloat("_ColorMul",100);
            Sample("ignored-channels-clock",a,new Color(.8f,.6f,.4f,1),new Color(1,0,0,.5f),new Color(.25f,0,0,0),2,Color.clear,false);
            Sample("zero-alpha",new Color(.2f,.4f,.6f,0),b,Color.white,new Color(.25f,0,0,0),2,Color.clear,false);
            Sample("hdr-unclamped",new Color(.2f,.4f,.6f,1),b,Color.white,new Color(3,0,0,0),1,Color.clear,false);
            Sample("source-rgba-blend",a,b,new Color(1,0,0,.5f),new Color(.25f,0,0,0),2,new Color(.1f,.2f,.3f,.4f),true);
            Sample("zero-intensity",a,b,Color.white,Color.white,0,new Color(.1f,.2f,.3f,.4f),true);
            Sample("negative-extrapolation",a,b,Color.white,new Color(-1,0,0,0),1,Color.clear,false);
        }
        finally
        {
            Shader.SetGlobalVector("_GlobalMipBias",bias);RenderTexture.active=previous;camera.targetTexture=null;
            foreach(var item in new UnityEngine.Object[]{pixels,tex,material,rt,mesh,root})AssetRegistry.ReleaseTransient(item);
        }
        return samples.ToArray();
    }
}
