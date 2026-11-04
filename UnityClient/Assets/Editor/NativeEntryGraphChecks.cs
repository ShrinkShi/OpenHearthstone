using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Exercise the unmodified source graph, not an isolated action surrogate.
public static class NativeEntryGraphChecks
{
    [Serializable] sealed class Probe
    {
        public string program,card,state,second_audio_state;
        public float second_audio_time;
        public int timelines,portrait_bindings,mesh_replacements,material_replacements;
        public int initialized,finalized,audio_cues,finished,unbound,visibility_side_effects;
        public bool actor_restored;
    }
    [Serializable] sealed class Report {public Probe[] probes;public bool full_effects_verified;}
    public static void RunGpu()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var cameraObject=new GameObject("OriginalEntryGraphCamera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
        var probes=new List<Probe>();
        try
        {
            foreach(string file in new[]{"2ac223d9b583771c9eb24338","b0cbd0aa93fedbfc28ee3f8e"})
            foreach(string card in new[]{"CS2_179","EX1_116"})
            using(var live=visuals.CreateLiveActor(database.cards.First(c=>c.CardId==card),"minion",true))
            {
                var actor=live.ActorObject;var parent=actor.transform.parent;var pose=actor.transform.localPosition;
                var source=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
                var probe=new Probe{program=file,card=card};
                using(var effect=new NativeEffectInstance(source,assets,0,32,32))
                {
                    effect.AttachToOriginalParent(actor);effect.BindTimelineCamera(camera);
                    // Fixed cadence preserves coroutine yield boundaries and source waits.
                    for(int frame=0;frame<=600;frame++)
                    {
                        effect.Seek(frame/60f);
                        if(effect.AudioCues.Count==2&&probe.second_audio_state==null)
                        {probe.second_audio_state=effect.CurrentStates[0];probe.second_audio_time=effect.AudioCues[1].time;}
                    }
                    probe.state=String.Join(" | ",effect.CurrentStates);
                    probe.timelines=effect.TimelineStarts;probe.portrait_bindings=effect.PortraitTextureBindings;
                    probe.mesh_replacements=effect.MeshReplacements;probe.material_replacements=effect.MaterialReplacements;
                    probe.initialized=effect.CustomSpawnInitializations;probe.finalized=effect.CustomSpawnFinalizations;
                    probe.audio_cues=effect.AudioCues.Count;probe.finished=effect.FinishNotifications.Count(n=>n.delivered);
                    probe.visibility_side_effects=effect.UnrestoredActorVisibilitySideEffects;
                    probe.unbound=effect.UnboundActorActions+effect.UnboundActorInitializationActions+effect.UnboundMachineOwnerActions+
                        effect.UnboundActorVisibilityActions+effect.UnboundEliteActions+effect.UnboundCustomSpawnActions+
                        effect.UnboundPortraitTextureActions+effect.UnboundGeometryActions+effect.UnboundTextureActions+
                        effect.UnboundTimelineActions+effect.UnboundTimelineCameras+effect.UnsupportedTimelineTracks+
                        effect.UnboundRandomWaitActions+effect.UnboundAudioActions+effect.UnboundFinishActions+
                        effect.UnboundAnimationActions+effect.UnboundScaleActions+effect.UnboundColorActions+
                        effect.UnboundEaseActions+effect.UnboundITweenActions+effect.UnboundLifecycleEvents+effect.TransitionLimitHits;
                }
                probe.actor_restored=actor!=null&&actor.transform.parent==parent&&actor.transform.localPosition==pose;
                if(probe.unbound!=0||probe.state!="NoneState"||probe.timelines!=1||probe.initialized!=1||probe.finalized!=1||probe.finished!=1||probe.audio_cues!=2||probe.second_audio_state!="Actor Visible")
                    throw new Exception("Original entry graph did not complete source sequence: "+JsonUtility.ToJson(probe));
                if(!probe.actor_restored)throw new Exception("Original entry graph leaked borrowed Actor: "+file+"/"+card);
                probes.Add(probe);
            }
        }
        finally{visuals.Clear();AssetRegistry.ReleaseTransient(cameraObject);}
        File.WriteAllText("../work/native-entry-graphs.json",JsonUtility.ToJson(new Report{probes=probes.ToArray(),full_effects_verified=false},true));
        foreach(var probe in probes)Debug.Log("NATIVE ENTRY GRAPH: "+JsonUtility.ToJson(probe));
    }
}
