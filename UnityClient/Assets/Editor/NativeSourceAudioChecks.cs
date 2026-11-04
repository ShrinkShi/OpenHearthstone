using System;
using System.Linq;
using System.IO;
using UnityEngine;

public static class NativeSourceAudioChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original source audio: "+reason);passed++;}
    public static void Run(){Run(true);}
    public static void RunSourceOnly(){Run(false);}
    static void Run(bool runFollowup)
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var definition=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,"92c50d3e8ca46833c7a5dc25.native-audio.json")));
        var source=definition.nodes[0].audio_sources[0].values;
        var cue=NativeAudio.Resolve(definition,"",0,.5f,()=>.5f);
        Check(cue.template==source&&cue.inheritSpatialCurve,"actual source resolver carries original AudioSource template rather than discarding it");
        // Change template fields in memory to distinguish source from SDK defaults.
        source.Priority=41;source.MinDistance=3;source.MaxDistance=19;source.Pan2D=.25f;source.DopplerLevel=.4f;source.BypassReverbZones=true;
        source.rolloffMode=2;source.rolloffCustomCurve=new NativeAudioCurve{m_PreInfinity=2,m_PostInfinity=2,m_Curve=new[]{new NativeKey{time=0,value=1},new NativeKey{time=1,value=.2f}}};
        source.panLevelCustomCurve=new NativeAudioCurve{m_PreInfinity=2,m_PostInfinity=2,m_Curve=new[]{new NativeKey{time=0,value=.7f},new NativeKey{time=1,value=.3f}}};
        var root=new GameObject("Source audio staging");root.transform.position=new Vector3(50000,30000,0);
        var owner=new GameObject("Source emitter");owner.transform.SetParent(root.transform,false);owner.transform.localPosition=new Vector3(2,3,4);
        var nodes=new[]{owner};var stagingOrigin=root.transform.position;var stagingFrame=new NativeAudioStagingFrame(stagingOrigin);
        var playbackRoot=new GameObject("Silent source audio verification");
        GameObject host=null;
        using(var playback=new NativeAudioPlayback(assets.Root,playbackRoot.transform,true))
        {
            try
            {
                cue.keepSource=true;NativeAudioTemplate.BindPosition(cue,owner,nodes,stagingFrame,true);
                playback.Play(cue);var voice=playbackRoot.GetComponentInChildren<AudioSource>();
                Check(voice.priority==41&&voice.minDistance==3&&voice.maxDistance==19,"physical voice receives original priority and distance fields");
                Check(voice.panStereo==.25f&&voice.dopplerLevel==.4f&&voice.bypassReverbZones,"physical voice receives pan, Doppler and bypass settings");
                Check(voice.rolloffMode==AudioRolloffMode.Custom&&Mathf.Abs(voice.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(1)-.2f)<1e-5f,"original custom attenuation reaches SDK voice");
                Check(Mathf.Abs(voice.GetCustomCurve(AudioSourceCurveType.SpatialBlend).Evaluate(.5f)-.5f)<1e-4f,"original spatial curve is not overwritten by default zero blend");
                Check(voice.volume==cue.volume&&voice.pitch==cue.pitch&&voice.mute,"code volume/pitch products and silent verification remain applied");
                Check(voice.transform.position==new Vector3(2,3,4),"staging translation is excluded from emitter position");
                owner.transform.localPosition=new Vector3(5,6,7);playback.Update();Check(voice.transform.position==new Vector3(5,6,7),"existing source follows its moving original emitter");
                host=new GameObject("Reparented area clone host");host.transform.position=new Vector3(50000,30000,0);
                owner.transform.SetParent(host.transform,true);owner.transform.position=new Vector3(50008,30004,5);playback.Update();
                Check(voice.transform.position==new Vector3(8,4,5),"Nodes identity and captured origin survive clone reparenting");
                host.SetActive(false);owner.transform.position=new Vector3(50009,30005,6);playback.Update();
                Check(playback.StoppedInactiveSourceVoices==0&&playback.StartedVoices==1&&voice.transform.position==new Vector3(9,5,6),"hiding a visual ancestor does not stop an H11-style token or break position following");
                host.SetActive(true);playback.Update();
                owner.SetActive(false);playback.Update();Check(playback.StoppedInactiveSourceVoices==1,"deactivating the tracked source node stops its external voice");
                owner.SetActive(true);owner.transform.position=new Vector3(50010,30006,7);playback.Update();
                Check(playback.StoppedInactiveSourceVoices==1&&playback.StartedVoices==1&&voice.transform.position==new Vector3(10,6,7),"source reactivation does not replay a stopped voice and tracking remains positioned");
                var sharedOrigin=new Vector3(24000,10000,20000);host.transform.position=sharedOrigin;stagingFrame.SetOrigin(sharedOrigin);playback.Update();
                Check(voice.transform.position==new Vector3(10,6,7)&&cue.stagingOrigin==stagingOrigin,
                    "explicit staging-frame reanchor updates an existing clone voice while preserving its node identity and original frame snapshot");
                var external=new GameObject("External actor emitter");external.transform.SetParent(root.transform,false);external.transform.position=new Vector3(50011,30005,6);
                var externalCue=new NativeAudioCue{file=cue.file,volume=cue.volume,pitch=cue.pitch,sourceName="External actor source"};
                NativeAudioTemplate.BindPosition(externalCue,external,nodes,stagingFrame,true);
                Check(!externalCue.stagedCloneSource&&!externalCue.stopWhenSourceInactive&&externalCue.trackedSource==null&&externalCue.position==external.transform.position,"actor outside Nodes preserves world position and activation path despite sharing the staging hierarchy");
                playback.Play(externalCue);var externalVoice=playbackRoot.GetComponentsInChildren<AudioSource>(true).Single(v=>v.gameObject.name=="External actor source");
                Check(externalVoice.transform.position==external.transform.position,"external actor physical voice keeps its world position");
                AssetRegistry.ReleaseTransient(external);
                playback.StopAll();cue.inheritSpatialCurve=false;cue.spatialBlend=.15f;playback.Play(cue);
                voice=playbackRoot.GetComponentInChildren<AudioSource>();Check(Mathf.Abs(voice.spatialBlend-.15f)<1e-5f,"explicit ClipAudio-style spatial override remains supported");
                AssetRegistry.ReleaseTransient(owner);playback.Update();Check(playback.ActiveVoices==0,"destroying the emitter stops its owned physical source");
            }
            finally{AssetRegistry.ReleaseTransient(host);AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(playbackRoot);}
        }
        Debug.Log("NATIVE SOURCE AUDIO: "+passed+" checks passed; physical source template/curves/movement restored, full scene/listener/SoundManager remain incomplete");
        if(runFollowup)NativePlaythroughChecks.Run();
    }
}
