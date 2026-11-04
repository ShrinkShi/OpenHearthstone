using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeClipAudioChecks
{
    [Serializable] sealed class SourceRow{public string file;}
    [Serializable] sealed class SourceAudit{public SourceRow[] programs;}
    public static void RunAllSourceAudio()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var audioParent=new GameObject("Silent source audio census");
        var audit=JsonUtility.FromJson<SourceAudit>(File.ReadAllText("../manifests/native-clip-audio-refresh.json"));
        int starts=0,finishes=0,unbound=0,cues=0,programs=0,suppressed=0,played=0;
        try
        {
            foreach(var row in audit.programs)
            {
                var original=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,row.file)));
                // Isolate audio actions while retaining their actual owner indices,
                // hierarchy and source poses. This does not certify whole effects.
                original.nodes=original.nodes.Select(n=>new NativeNode{name=n.name,path=n.path,parent=n.parent,position=n.position,rotation=n.rotation,scale=n.scale,active=n.active,materials=new NativeMaterial[0]}).ToArray();
                original.captures=new NativeCapture[0];original.timelines=new NativeTimeline[0];
                original.machines=original.machines.Where(m=>m.states.Any(s=>s.actions.Any(a=>a.name=="AudioPlayClipAction"))).Select(m=>new NativeMachine{owner=m.owner,owner_known=m.owner_known,start="Audio",events=new NativeTransition[0],states=new[]{new NativeState{name="Audio",transitions=new NativeTransition[0],actions=m.states.SelectMany(s=>s.actions).Where(a=>a.name=="AudioPlayClipAction").ToArray()}}}).ToArray();
                using(var playback=new NativeAudioPlayback(assets.Root,audioParent.transform,true))
                using(var effect=new NativeEffectInstance(original,assets,0,16,16,audioCallback:c=>{if(playback.Load(c.file).samples<=0)throw new Exception("Empty original PCM "+row.file);playback.Play(c);cues++;}))
                {
                    effect.Seek(1.1f);starts+=effect.ClipAudioStarts;finishes+=effect.ClipAudioFinishes;unbound+=effect.UnboundAudioActions;programs++;
                    if(effect.ClipAudioStarts!=effect.ClipAudioFinishes)throw new Exception("Unfinished source audio "+row.file);
                    if(effect.AudioCues.Any(c=>!c.positionKnown||c.template==null))throw new Exception("Missing source template/position "+row.file);
                    suppressed+=playback.SuppressedVoices;played+=playback.StartedVoices;
                }
            }
        }
        finally{AssetRegistry.ReleaseTransient(audioParent);}
        if(programs!=183||starts!=347||finishes!=347||cues!=347||unbound!=0)throw new Exception("Original audio census mismatch: "+programs+"/"+starts+"/"+finishes+"/"+cues+"/"+unbound);
        File.WriteAllText("../work/native-audio-source-census.json","{\"programs\":"+programs+",\"starts\":"+starts+",\"finishes\":"+finishes+",\"cues\":"+cues+",\"unbound\":"+unbound+",\"started_voices\":"+played+",\"suppressed_voices\":"+suppressed+",\"whole_effects_verified\":false,\"audible_playback_verified\":false}");
        Debug.Log("Original audio source census passed: "+programs+" programs, "+cues+" cues, "+unbound+" diagnosed unsupported actions");
    }
    public static void RunMath(){Debug.Log("Original ClipAudio math checks passed: "+MathChecks());}
    static int MathChecks()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original ClipAudio: "+message);passed++;};
        var delay=new NativeClipAudioDelay(.5f);
        check(!delay.Step(.25f)&&delay.Remaining==.25f,"source subtract then yield");
        check(!delay.Step(.25f)&&delay.Remaining==0,"zero remaining still yields");
        check(delay.Step(0)&&delay.Finished,"next coroutine iteration plays");
        check(!delay.Step(2),"finished coroutine plays once");
        check(new NativeClipAudioDelay(0).Step(0),"zero delay plays on enter");
        check(new NativeClipAudioDelay(-1).Step(0),"negative delay plays on enter");
        var synthetic=new NativeSoundAsset{values=new NativeSoundValues{m_AudioClip="primary",m_Category=3,m_RandomClips=new NativeSoundChoice[0],m_RandomVolumeMin=.5f,m_RandomVolumeMax=1,m_RandomPitchMin=.8f,m_RandomPitchMax=1.2f},
            clips=new[]{new NativeAudioClipRef{reference="primary",file="primary.wav"},new NativeAudioClipRef{reference="random",file="random.wav"}}};
        var cue=NativeAudio.ResolveClip(synthetic,1,.8f,2,1,()=>.5f);
        check(cue.file=="primary.wav"&&Mathf.Abs(cue.volume-.6f)<.00001f&&cue.pitch==2&&cue.category==1,"source override then def volume/pitch product");
        synthetic.values.m_RandomClips=new[]{new NativeSoundChoice{m_Clip="random",m_Weight=1}};
        check(NativeAudio.ResolveClip(synthetic,0,1,1,0,()=>.5f).file=="random.wav","random list takes precedence over primary");
        check(NativeAudio.ResolveClip(synthetic,0,1,1,0,()=>.5f).category==3,"zero category retains def category");
        synthetic.values.m_InstanceLimited=1;check(NativeAudio.ResolveClip(synthetic,0,1,1,0,()=>.5f).options.limited,"source def options carried when no action override");
        var limits=new NativeSoundLimits();var options=new NativeSoundOptions{limited=true,duration=.5f,maximum=0,maxOut=0};int replaced=0;
        check(limits.Acquire("source",options,2,.25f,n=>replaced++)&&limits.Count("source")==1,"first named slot and source coroutine initial delta");
        check(!limits.Acquire("source",options,2,0,n=>replaced++)&&limits.Count("source")==1,"maximum below one clamps, full slot rejects");
        limits.Tick(.25f);check(limits.Count("source")==1,"release subtract then yield at zero");
        limits.Tick(0);check(limits.Count("source")==0,"next coroutine iteration releases slot");
        check(limits.Acquire("other",options,2,0,n=>replaced++)&&limits.Count("source")==0,"independent generated names");
        limits.Clear();options.duration=0;
        limits.Acquire("fallback",options,.3f,0,n=>replaced++);limits.Tick(.3f);check(limits.Count("fallback")==1,"zero option duration uses unscaled PCM length before coroutine scaling");
        limits.Tick(0);check(limits.Count("fallback")==0,"fallback duration releases independently of voice lifetime");
        options.duration=1;options.maximum=2;
        limits.Acquire("two",options,2,0,n=>replaced++);limits.Acquire("two",options,2,0,n=>replaced++);
        check(limits.Count("two")==2&&!limits.Acquire("two",options,2,0,n=>replaced++),"source maximum count admission");
        options.maxOut=1;check(limits.Acquire("two",options,2,0,n=>replaced++)&&replaced==1&&limits.Count("two")==2,"source replacement callback does not increment count");
        limits.Tick(1);limits.Tick(0);check(limits.Count("two")==0,"independent replacement release coroutine does not leave negative count");
        options.maxOut=9;limits.Acquire("unknown",options,1,0,n=>replaced++);limits.Acquire("unknown",options,1,0,n=>replaced++);
        check(!limits.Acquire("unknown",options,1,0,n=>replaced++),"source unknown max-out rejects");
        options.limited=false;check(limits.Acquire("unlimited",options,1,0,n=>replaced++)&&limits.Count("unlimited")==0,"explicit disabled options do not acquire def slots");
        limits.Clear();check(limits.Count("unknown")==0,"client service disposal clears counters");
        return passed;
    }
    public static void RunGpu()
    {
        int passed=MathChecks();Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original ClipAudio: "+message);passed++;};
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var audioParent=new GameObject("Silent original clip audio");
        try
        {
            var template=JsonUtility.FromJson<NativeAudioConfig>(File.ReadAllText(Path.Combine(assets.Root,"native-audio-config.json"))).values;
            var templateObject=new GameObject("Original template check");
            try
            {
                var voice=templateObject.AddComponent<AudioSource>();NativeAudioTemplate.Apply(voice,template);
                check(voice.minDistance==100&&voice.maxDistance==500&&voice.rolloffMode==AudioRolloffMode.Linear,"original distance and linear rolloff");
                check(voice.priority==128&&voice.dopplerLevel==1&&voice.panStereo==0,"original priority Doppler and pan");
                check(!voice.loop&&!voice.mute&&!voice.spatialize&&!voice.spatializePostEffects,"original playback flags");
                check(!voice.bypassEffects&&!voice.bypassListenerEffects&&!voice.bypassReverbZones,"original bypass flags");
                foreach(var pair in new[]{Tuple.Create(AudioSourceCurveType.CustomRolloff,template.rolloffCustomCurve),Tuple.Create(AudioSourceCurveType.SpatialBlend,template.panLevelCustomCurve),Tuple.Create(AudioSourceCurveType.Spread,template.spreadCustomCurve),Tuple.Create(AudioSourceCurveType.ReverbZoneMix,template.reverbZoneMixCustomCurve)})
                {
                    var curve=voice.GetCustomCurve(pair.Item1);foreach(var key in pair.Item2.m_Curve)check(Mathf.Abs(curve.Evaluate(key.time)-key.value)<.00001f,"original source custom curve key");
                }
                templateObject.transform.position=new Vector3(20000,10000,20000);
                var target=new GameObject("Source audio parent");target.transform.SetParent(templateObject.transform,false);target.transform.localPosition=new Vector3(3,4,5);
                check(NativeAudioTemplate.Position(target,templateObject.transform)==new Vector3(3,4,5),"offscreen staging translation removed");
                check(NativeAudioTemplate.Position(null,templateObject.transform)==Vector3.zero,"source null parent emits at world zero");
                target.transform.SetParent(null,true);check(NativeAudioTemplate.Position(target,templateObject.transform)==target.transform.position,"external source world position retained");
                AssetRegistry.ReleaseTransient(target);
            }
            finally{AssetRegistry.ReleaseTransient(templateObject);}
            using(var playback=new NativeAudioPlayback(assets.Root,audioParent.transform,true))
            foreach(string file in new[]{"2ac223d9b583771c9eb24338","b0cbd0aa93fedbfc28ee3f8e"})
            {
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
                var actions=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(a=>a.name=="AudioPlayClipAction").ToArray();
                program.machines=new[]{new NativeMachine{owner=0,owner_known=true,start="Audio",events=new NativeTransition[0],states=new[]{
                    new NativeState{name="Audio",actions=actions,transitions=new[]{new NativeTransition{@event="FINISHED",state="Done"}}},
                    new NativeState{name="Done",actions=new NativeAction[0],transitions=new NativeTransition[0]}}}};
                int calls=0;
                using(var effect=new NativeEffectInstance(program,assets,0,32,32,audioCallback:c=>{calls++;playback.Play(c);}))
                {
                    effect.Seek(0);check(effect.UnboundAudioActions==0&&effect.ClipAudioStarts==2&&effect.ClipAudioFinishes==1&&calls==1,"original first sound starts immediately "+file+" unbound="+effect.UnboundAudioActions+" starts="+effect.ClipAudioStarts+" finishes="+effect.ClipAudioFinishes+" callbacks="+calls);
                    effect.Seek(.79f);check(calls==1&&effect.CurrentStates[0]=="Audio","delayed impact holds source action completion");
                    effect.Seek(.85f);check(calls==2&&effect.ClipAudioFinishes==2&&effect.CurrentStates[0]=="Done","impact starts before FSM completes, without waiting for clip length");
                    check(effect.AudioCues[0].time==0&&effect.AudioCues[1].time>=.8f&&effect.AudioCues[1].time<.83f,"original delay coroutine time quantization");
                    foreach(var original in effect.AudioCues)
                    {
                        float expectedVolume=file=="2ac223d9b583771c9eb24338"?.8f:.7f;
                        check(Mathf.Abs(original.volume-expectedVolume)<.00001f&&original.pitch==1&&original.category==1,"actual original cast and impact parameters "+file);
                        var pcm=playback.Load(original.file);check(pcm.samples>0&&pcm.frequency>0,"actual decoded source PCM loads");
                    }
                    effect.Seek(0);effect.Seek(1);check(calls==2&&effect.AudioCues.Count==2,"rewind reconstructs cues without replaying audible callbacks");
                    effect.Seek(2);check(calls==2,"completed original clip action does not resend");
                }
                playback.StopAll();check(playback.ActiveVoices==0,"test voices released");
            }
            var spatialProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"000ef02b501e96de0896959a.native-effect.json")));
            var spatialActions=spatialProgram.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(a=>a.name=="AudioPlayClipAction").ToArray();
            spatialProgram.machines=new[]{new NativeMachine{owner=1,owner_known=true,start="Audio",events=new NativeTransition[0],states=new[]{new NativeState{name="Audio",actions=spatialActions,transitions=new NativeTransition[0]}}}};
            using(var playback=new NativeAudioPlayback(assets.Root,audioParent.transform,true))
            using(var effect=new NativeEffectInstance(spatialProgram,assets,9,32,32,audioCallback:c=>playback.Play(c)))
            {
                effect.Seek(0);check(effect.UnboundAudioActions==0&&effect.AudioCues.Count==1,"actual original Upgrade spatial audio action");
                var cue=effect.AudioCues[0];check(cue.spatialBlend==1&&cue.positionKnown&&cue.position==Vector3.zero,"source spatial override and actual owner position");
                check(Mathf.Abs(cue.volume-.7f)<.00001f&&cue.pitch==1,"actual Upgrade source volume and pitch");
                var voice=audioParent.GetComponentInChildren<AudioSource>();check(voice!=null&&voice.spatialBlend==1&&voice.minDistance==100&&voice.maxDistance==500&&voice.mute,"silent bridge applies original spatial template");
                check(voice.transform.position==cue.position&&voice.rolloffMode==AudioRolloffMode.Linear,"actual emitter transform and rolloff");
                effect.Nodes[1].transform.localPosition=new Vector3(2,3,4);check(voice.transform.position==cue.position,"original source snapshots without following parent");
                cue.options=new NativeSoundOptions{limited=true,duration=60,maximum=1,maxOut=0};playback.Play(cue);
                check(playback.LimitedCount(cue.sourceName)==1,"actual shared service acquires source-name quota");
                int played=playback.StartedVoices;playback.Play(cue);
                check(playback.SuppressedVoices==1&&playback.StartedVoices==played,"actual bridge suppresses duplicate named voice");
                playback.StopAll();check(playback.LimitedCount(cue.sourceName)==1,"stopping PCM does not release source coroutine quota");
            }
            foreach(string file in new[]{"0a68d36d48a0f777097c01e6","2c575b7037eb26b115a9f5fd"})
            {
                var original=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
                var source=original.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="AudioPlayClipAction"&&!a.Param("m_TemplateSource").object_null);
                original.nodes=original.nodes.Select(n=>new NativeNode{name=n.name,path=n.path,parent=n.parent,position=n.position,rotation=n.rotation,scale=n.scale,active=n.active,materials=new NativeMaterial[0]}).ToArray();
                original.captures=new NativeCapture[0];original.timelines=new NativeTimeline[0];
                original.machines=new[]{new NativeMachine{owner=0,owner_known=true,start="Audio",events=new NativeTransition[0],states=new[]{new NativeState{name="Audio",actions=new[]{source},transitions=new NativeTransition[0]}}}};
                using(var playback=new NativeAudioPlayback(assets.Root,audioParent.transform,true))
                using(var effect=new NativeEffectInstance(original,assets,0,16,16,audioCallback:c=>playback.Play(c)))
                {
                    effect.Seek(1.1f);check(effect.UnboundAudioActions==0&&effect.ClipAudioFinishes==1&&playback.StartedVoices==1,"actual explicit source template plays "+file);
                    var cue=effect.AudioCues.Single();var voice=audioParent.GetComponentInChildren<AudioSource>();
                    check(cue.copyTemplate&&cue.mixer=="LegendaryDucking"&&cue.group=="L_SFX","source explicit template identity");
                    check(voice.outputAudioMixerGroup!=null&&voice.outputAudioMixerGroup.name=="L_SFX"&&voice.outputAudioMixerGroup.audioMixer.name=="LegendaryDucking","actual original mixer routing");
                    check(voice.minDistance==cue.template.MinDistance&&voice.maxDistance==cue.template.MaxDistance&&voice.rolloffMode==(AudioRolloffMode)cue.template.rolloffMode,"actual explicit source distance and rolloff");
                    check(voice.reverbZoneMix==cue.template.reverbZoneMixCustomCurve.m_Curve[0].value&&voice.dopplerLevel==cue.template.DopplerLevel,"actual copied source reverb and Doppler");
                    check(voice.mute&&!voice.spatialize&&!voice.spatializePostEffects,"silent verification and default noncopied flags");
                }
            }
        }
        finally{AssetRegistry.ReleaseTransient(audioParent);}
        File.WriteAllText("../work/native-clip-audio-checks.json","{\"passed\":"+passed+",\"complete\":false,\"audible_playback_verified\":false}");
        Debug.Log("Original ClipAudio checks passed: "+passed);
    }
}
