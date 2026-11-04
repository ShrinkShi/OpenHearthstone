using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

[Serializable] public sealed class HeroHealthAudioProbeReport
{
    public bool success,semantic_accepted,try_play_started,source_played;
    public string status,error,started_utc,source_manifest,profile,source_cab,source_pathid,action_cab,action_pathid;
    public string sound_cab,sound_pathid,clip_reference,clip_file,cue_file,loaded_clip_name,source_card_id,wav_path,wav_sha256;
    public string source_name;
    public int amount,card_type,source,target,visual_target,armor_absorbed;
    public int enabled_listeners_before,disabled_scene_listeners,disabled_scene_cameras,disabled_scene_audio_sources;
    public int clip_samples,clip_channels,clip_frequency,clip_load_state;
    public int hero_started,hero_failures,native_started,native_suppressed,native_active_voices;
    public int tap_rate,tap_channels,tap_dsp_buffer,tap_dsp_buffers,tap_callbacks,tap_overflows,exit_code;
    public long tap_frames,tap_pre_silence_frames,tap_post_drain_frames,tap_clipped_samples,wav_bytes;
    public float clip_length,profile_source_volume,profile_sound_volume_min,profile_sound_volume_max;
    public float cue_volume,cue_pitch,actual_source_volume,actual_source_pitch,actual_spatial_blend;
    public float wall_start,wall_silence_end,wall_cue_start,wall_cue_end,wall_stop;
    public float dsp_start,dsp_silence_end,dsp_cue_start,dsp_cue_end,dsp_stop,tap_peak,tap_rms;
}

public sealed class HeroHealthAudioProbe:MonoBehaviour
{
    HeroHealthAudioProbeReport report=new HeroHealthAudioProbeReport();
    string outputFolder,reportPath,wavPath;
    AssetRegistry assets;
    NativeAudioPlayback playback;
    NativeHeroDamageAudio healthAudio;
    NativeAudioCue playedCue;
    AudioSource playedSource;
    FireblastPcmTap tap;
    bool finished;

    public void Begin(string folder,string dataRoot)
    {
        try
        {
            outputFolder=Path.GetFullPath(folder);Directory.CreateDirectory(outputFolder);
            reportPath=Path.Combine(outputFolder,"report.json");wavPath=Path.Combine(outputFolder,"health-listener.wav");
            if(File.Exists(reportPath)||File.Exists(wavPath))
            {finished=true;Debug.LogError("Hero health audio probe output already exists; use a fresh output folder");Environment.ExitCode=1;Application.Quit();return;}
            Application.runInBackground=true;report.started_utc=DateTime.UtcNow.ToString("o");report.status="starting";
            foreach(var camera in FindObjectsOfType<Camera>().Where(item=>item!=null&&item.enabled).ToArray())
            {camera.enabled=false;report.disabled_scene_cameras++;}
            foreach(var listener in FindObjectsOfType<AudioListener>().Where(item=>item!=null&&item.enabled).ToArray())
            {report.enabled_listeners_before++;listener.enabled=false;report.disabled_scene_listeners++;}
            foreach(var source in FindObjectsOfType<AudioSource>().Where(item=>item!=null&&item.enabled).ToArray())
            {source.Stop();source.enabled=false;report.disabled_scene_audio_sources++;}

            var cameraObject=new GameObject("HeroHealthAudioProbeCamera");cameraObject.transform.SetParent(transform,false);
            var probeCamera=cameraObject.AddComponent<Camera>();probeCamera.clearFlags=CameraClearFlags.SolidColor;probeCamera.backgroundColor=Color.black;probeCamera.cullingMask=0;
            cameraObject.AddComponent<AudioListener>();tap=cameraObject.AddComponent<FireblastPcmTap>();
            if(FindObjectsOfType<Camera>().Count(item=>item!=null&&item.enabled)!=1)
                throw new InvalidOperationException("Probe scene does not have exactly one enabled Camera");
            if(FindObjectsOfType<AudioListener>().Count(item=>item!=null&&item.enabled)!=1)
                throw new InvalidOperationException("Probe scene does not have exactly one enabled AudioListener");
            assets=new AssetRegistry(dataRoot);
            string manifest=Path.Combine(assets.Root,NativeHeroDamageAudio.RuntimeFile);
            if(!File.Exists(manifest))throw new FileNotFoundException("Export the original hero damage audio profile first",manifest);
            var profile=JsonUtility.FromJson<NativeHeroDamageAudioProfile>(File.ReadAllText(manifest));
            if(profile==null||profile.sound==null||profile.sound.clips==null||profile.sound.clips.Length!=1)
                throw new InvalidDataException("Hero damage audio profile has no single source clip");
            report.source_manifest=Path.GetFullPath(manifest);report.profile=profile.profile;
            if(profile.source!=null){report.source_cab=profile.source.cab;report.source_pathid=profile.source.pathid;}
            if(profile.action_source!=null){report.action_cab=profile.action_source.cab;report.action_pathid=profile.action_source.pathid;}
            if(profile.sound_source!=null){report.sound_cab=profile.sound_source.cab;report.sound_pathid=profile.sound_source.pathid;}
            var sourceClip=profile.sound.clips[0];report.clip_reference=sourceClip.reference;report.clip_file=sourceClip.file;
            report.profile_source_volume=profile.audio_source.values.m_Volume;
            report.profile_sound_volume_min=profile.sound.values.m_RandomVolumeMin;
            report.profile_sound_volume_max=profile.sound.values.m_RandomVolumeMax;

            playback=new NativeAudioPlayback(assets.Root,transform);
            healthAudio=new NativeHeroDamageAudio(assets,cue=>{playedCue=cue;playback.Play(cue);});
            if(!healthAudio.Enabled)throw new InvalidDataException("NativeHeroDamageAudio rejected the source profile or cached clip");
            report.wav_path=Path.GetFullPath(wavPath);tap.Begin(wavPath);report.status="recording";
            report.tap_rate=tap.Rate;report.tap_channels=tap.Channels;report.tap_dsp_buffer=tap.DspBuffer;report.tap_dsp_buffers=tap.DspBuffers;
            report.wall_start=Time.realtimeSinceStartup;report.dsp_start=(float)AudioSettings.dspTime;WriteReport();
            StartCoroutine(RunProbe());
        }
        catch(Exception error){Finish(false,error.ToString());}
    }

    void Update(){if(playback!=null)playback.Update();}

    IEnumerator RunProbe()
    {
        report.status="silence_before";
        yield return new WaitForSecondsRealtime(.2f);
        report.wall_silence_end=Time.realtimeSinceStartup;report.dsp_silence_end=(float)AudioSettings.dspTime;
        report.tap_pre_silence_frames=tap==null?0:tap.SampleFrames;
        var sourcePower=new EntityView{entity=501,card_id="HERO_07bp"};
        var targetHero=new EntityView{entity=601,card_id="HERO_01",health=20,max_health=30,armor=0};
        var state=new StateView{match_id="isolated-hero-health-audio-probe",phase="probe",players=new[]
        {
            new PlayerView{seat=0,hero=new EntityView{entity=500,card_id="HERO_02",health=30,max_health=30},power=sourcePower},
            new PlayerView{seat=1,hero=targetHero}
        }};
        const string sourceCard="HERO_07bp";
        var hit=new SemanticEvent{type="Hit",card_id=sourceCard,source=sourcePower.entity,target=targetHero.entity,
            card_type=3,owner=1,amount=1,visual=new EntityView{entity=targetHero.entity,card_id=targetHero.card_id}};
        report.source_card_id=sourceCard;report.amount=(int)hit.amount;report.card_type=hit.card_type;
        report.source=hit.source;report.target=hit.target;report.visual_target=hit.visual.entity;report.armor_absorbed=0;
        report.semantic_accepted=NativeHeroDamageAudio.ShouldPlay(hit,sourceCard,0,state);
        if(!report.semantic_accepted){Finish(false,"Probe Hero Hit failed NativeHeroDamageAudio validation");yield break;}
        report.wall_cue_start=Time.realtimeSinceStartup;report.dsp_cue_start=(float)AudioSettings.dspTime;
        report.status="playing_health_cue";report.try_play_started=healthAudio.TryPlay(hit,sourceCard,0,state);
        if(!report.try_play_started||playedCue==null){Finish(false,"Production Health cue did not start");yield break;}
        playedSource=playback.Sources.LastOrDefault();
        if(playedSource==null||playedSource.clip==null){Finish(false,"NativeAudioPlayback created no AudioSource clip");yield break;}
        report.cue_file=playedCue.file;report.cue_volume=playedCue.volume;report.cue_pitch=playedCue.pitch;
        report.clip_samples=playedSource.clip.samples;report.clip_channels=playedSource.clip.channels;
        report.clip_frequency=playedSource.clip.frequency;report.clip_length=playedSource.clip.length;report.loaded_clip_name=playedSource.clip.name;
        report.clip_load_state=(int)playedSource.clip.loadState;report.source_name=playedSource.gameObject.name;
        report.actual_source_volume=playedSource.volume;report.actual_source_pitch=playedSource.pitch;
        report.actual_spatial_blend=playedSource.spatialBlend;
        yield return null;
        report.source_played=playedSource!=null&&playedSource.isPlaying;
        if(!report.source_played){Finish(false,"Health AudioSource did not enter isPlaying");yield break;}
        float deadline=Time.realtimeSinceStartup+Mathf.Max(4f,report.clip_length+3f);
        while(playedSource!=null&&playedSource.isPlaying&&Time.realtimeSinceStartup<deadline)yield return null;
        if(playedSource!=null&&playedSource.isPlaying){Finish(false,"Health AudioSource did not finish its original clip");yield break;}
        report.wall_cue_end=Time.realtimeSinceStartup;report.dsp_cue_end=(float)AudioSettings.dspTime;
        long cueEndFrames=tap.SampleFrames;
        yield return new WaitForSecondsRealtime(.2f);
        report.tap_post_drain_frames=Math.Max(0,tap.SampleFrames-cueEndFrames);
        report.wall_stop=Time.realtimeSinceStartup;report.dsp_stop=(float)AudioSettings.dspTime;
        report.hero_started=healthAudio.Started;report.hero_failures=healthAudio.Failures;
        report.native_started=playback.StartedVoices;report.native_suppressed=playback.SuppressedVoices;
        report.native_active_voices=playback.ActiveVoices;
        bool passed=report.semantic_accepted&&report.try_play_started&&report.source_played&&report.hero_started==1&&
            report.hero_failures==0&&report.native_started==1&&report.native_suppressed==0&&
            report.tap_pre_silence_frames>0&&report.tap_post_drain_frames>0;
        Finish(passed,passed?"":"Playback counts or silence/DSP timing windows did not match the isolated Health cue");
    }

    void Finish(bool success,string error)
    {
        if(finished)return;finished=true;
        if(tap!=null)
        {
            try
            {
                tap.StopCapture();report.tap_rate=tap.Rate;report.tap_channels=tap.Channels;report.tap_dsp_buffer=tap.DspBuffer;
                report.tap_dsp_buffers=tap.DspBuffers;report.tap_callbacks=tap.Callbacks;report.tap_overflows=tap.Overflows;
                report.tap_frames=tap.SampleFrames;report.tap_peak=tap.Peak;report.tap_rms=tap.Rms;report.tap_clipped_samples=tap.ClippedSamples;
            }
            catch(Exception captureError){success=false;error+="; Listener capture: "+captureError.Message;}
        }
        if(playback!=null)
        {
            report.hero_started=healthAudio==null?0:healthAudio.Started;report.hero_failures=healthAudio==null?0:healthAudio.Failures;
            report.native_started=playback.StartedVoices;report.native_suppressed=playback.SuppressedVoices;
            report.native_active_voices=playback.ActiveVoices;playback.Dispose();playback=null;
        }
        if(File.Exists(wavPath))
        {
            var info=new FileInfo(wavPath);report.wav_bytes=info.Length;
            using(var sha=SHA256.Create())using(var stream=File.OpenRead(wavPath))report.wav_sha256=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
            if(report.tap_frames<=0||report.tap_callbacks<=0||report.tap_overflows!=0||report.tap_peak<=0||report.tap_rms<=0)
            {success=false;error+="; Listener WAV was empty, silent, or overflowed";}
        }
        else{success=false;error+="; Listener WAV was not created";}
        report.success=success;report.status=success?"passed":"failed";report.error=error;report.exit_code=success?0:1;
        if(!WriteReport())report.exit_code=1;
        Environment.ExitCode=report.exit_code;Application.Quit();
    }

    bool WriteReport()
    {
        if(String.IsNullOrEmpty(reportPath))return false;
        try{File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));return true;}
        catch(Exception error){Debug.LogError("Hero health audio probe report: "+error);return false;}
    }

    void OnDestroy()
    {if(!finished){if(tap!=null)tap.StopCapture();if(playback!=null)playback.Dispose();}}
}
