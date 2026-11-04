using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

// Exact source template, position snapshot and named-instance quota bridge.
// The full original mixer, category service and ducking remain unrestored.
public sealed class NativeAudioPlayback : IDisposable
{
    readonly string root;readonly Transform parent;readonly bool silent;
    readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
    readonly List<AudioSource> voices=new List<AudioSource>();bool disposed;
    readonly Dictionary<string,AudioSource> existingSources=new Dictionary<string,AudioSource>();
    readonly Dictionary<AudioSource,NativeAudioCue> trackedSources=new Dictionary<AudioSource,NativeAudioCue>();
    readonly NativeSoundLimits limits=new NativeSoundLimits();int limitFrame=-1;
    public Func<float> DeltaTime;float FrameDelta{get{return DeltaTime==null?Time.deltaTime:DeltaTime();}}
    AssetBundle mixerBundle;AudioMixer mixer;
    public int SuppressedVoices{get;private set;}
    public int StartedVoices{get;private set;}
    public int StoppedInactiveSourceVoices{get;private set;}
    public int LimitedCount(string name){return limits.Count(name);}
    public int CachedClips{get{return clips.Count;}}
    public int ActiveVoices{get{return voices.Count;}}
    public IEnumerable<AudioSource> Sources{get{return voices.ToArray();}}
    public NativeAudioPlayback(string root,Transform parent,bool silent=false){this.root=root;this.parent=parent;this.silent=silent;}
    public AudioClip Load(string file)
    {
        if(disposed)throw new ObjectDisposedException("NativeAudioPlayback");
        if(Path.GetFileName(file)!=file||!file.EndsWith(".wav",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Expected local WAV cache filename");
        AudioClip clip;if(clips.TryGetValue(file,out clip))return clip;
        if(clips.Count>=32)throw new InvalidOperationException("Native audio cache limit reached");
        using(var stream=File.OpenRead(Path.Combine(root,file)))using(var reader=new BinaryReader(stream))
        {
            if(reader.ReadUInt32()!=0x46464952)throw new InvalidDataException("Expected RIFF WAV");
            uint length=reader.ReadUInt32();if(length+8L>stream.Length||reader.ReadUInt32()!=0x45564157)throw new InvalidDataException("Invalid WAV envelope");
            int channels=0,rate=0,bits=0,format=0,align=0;byte[] pcm=null;long end=length+8L;
            while(stream.Position+8<=end)
            {
                uint id=reader.ReadUInt32(),size=reader.ReadUInt32();long next=stream.Position+size+(size&1);
                if(next>end)throw new InvalidDataException("Invalid WAV chunk size");
                if(id==0x20746d66)
                {
                    if(size<16)throw new InvalidDataException("Invalid WAV format chunk");
                    format=reader.ReadUInt16();channels=reader.ReadUInt16();rate=reader.ReadInt32();reader.ReadUInt32();align=reader.ReadUInt16();bits=reader.ReadUInt16();
                }
                else if(id==0x61746164)
                {if(size>16*1024*1024)throw new InvalidDataException("Native PCM clip too large");pcm=reader.ReadBytes((int)size);}
                stream.Position=next;
            }
            if(format!=1||bits!=16||channels<1||channels>2||rate<8000||rate>192000||align!=channels*2||pcm==null||pcm.Length==0||pcm.Length%align!=0)
                throw new InvalidDataException("Unsupported source WAV; expected bounded PCM16 mono/stereo");
            var samples=new float[pcm.Length/2];for(int i=0;i<samples.Length;i++)samples[i]=(short)(pcm[2*i]|pcm[2*i+1]<<8)/32768f;
            clip=AudioClip.Create(file,samples.Length/channels,channels,rate,false);
            if(!clip.SetData(samples,0)){AssetRegistry.ReleaseTransient(clip);throw new InvalidDataException("PCM upload failed");}
            clips.Add(file,clip);return clip;
        }
    }
    public void Play(NativeAudioCue cue)
    {
        if(disposed)throw new ObjectDisposedException("NativeAudioPlayback");
        Update();AudioSource voice=null;
        if(!String.IsNullOrEmpty(cue.restartKey))
        {
            existingSources.TryGetValue(cue.restartKey,out voice);
        }
        if(voice==null&&voices.Count>=16)throw new InvalidOperationException("Native audio voice limit reached");
        var clip=Load(cue.file);var outputGroup=String.IsNullOrEmpty(cue.mixer)?null:Group(cue.mixer,cue.group);
        bool reuse=voice!=null;GameObject go;
        if(reuse){go=voice.gameObject;}
        else{go=new GameObject(cue.sourceName??"NativeAudioVoice");go.transform.SetParent(cue.sourceParent!=null?cue.sourceParent:parent,false);voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;}
        if(voice.clip!=clip)voice.clip=clip;
        if(cue.template!=null){if(cue.copyTemplate)NativeAudioTemplate.Copy(voice,cue.template);else NativeAudioTemplate.Apply(voice,cue.template);}
        if(outputGroup!=null)voice.outputAudioMixerGroup=outputGroup;
        if(cue.positionKnown)go.transform.position=cue.position;
        trackedSources.Remove(voice);if(cue.trackedSource!=null)trackedSources[voice]=cue;
        if(!cue.inheritSpatialCurve)voice.spatialBlend=cue.spatialBlend;
        voice.volume=cue.volume;voice.pitch=cue.pitch;voice.loop=cue.loop;voice.mute=silent||cue.mute;
        if(!reuse)voices.Add(voice);
        if(!String.IsNullOrEmpty(cue.restartKey))existingSources[cue.restartKey]=voice;
        cue.playbackActive=()=>voice!=null&&voice.isPlaying;
        cue.stopPlayback=()=>{if(voice!=null)voice.Stop();};
        if(!limits.Acquire(go.name,cue.options,clip.length,FrameDelta,FinishGenerated))
        {
            SuppressedVoices++;
            // Original PlayImpl rejects before AudioSource.Play; an existing
            // receiver and its currently playing sound survive rejection.
            if(String.IsNullOrEmpty(cue.restartKey)){voices.Remove(voice);ForgetSource(voice);AssetRegistry.ReleaseTransient(go);}
            return;
        }
        if(voice!=null){voice.Stop();if(cue.randomizeStart)voice.time=cue.randomStart*clip.length;voice.Play();StartedVoices++;}
    }
    void FinishGenerated(string name)
    {for(int i=voices.Count-1;i>=0;i--)if(voices[i]!=null&&voices[i].gameObject.name==name&&!existingSources.Values.Contains(voices[i])){ForgetSource(voices[i]);AssetRegistry.ReleaseTransient(voices[i].gameObject);voices.RemoveAt(i);return;}}
    void ForgetSource(AudioSource voice)
    {trackedSources.Remove(voice);foreach(var key in existingSources.Where(p=>p.Value==voice).Select(p=>p.Key).ToArray())existingSources.Remove(key);}
    public void Update()
    {
        foreach(var pair in trackedSources)
        {
            if(pair.Key==null)continue;
            if(pair.Value.trackedSource==null||pair.Value.sourceDestroyed!=null&&pair.Value.sourceDestroyed())pair.Key.Stop();
            else if(pair.Value.stopWhenSourceInactive&&!pair.Value.trackedSource.activeSelf)
            {
                // 隐藏视觉祖先时保留H11等由独立源节点持有的音尾。
                if(!pair.Value.sourceInactiveStopSent){pair.Key.Stop();pair.Value.sourceInactiveStopSent=true;StoppedInactiveSourceVoices++;}
            }
            else
            {
                pair.Value.sourceInactiveStopSent=false;
                var frame=pair.Value.stagingFrame;var origin=frame==null?pair.Value.stagingOrigin:frame.Origin;
                pair.Key.transform.position=NativeAudioTemplate.Position(pair.Value.trackedSource,pair.Value.stagingRoot,pair.Value.stagingBindingKnown,pair.Value.stagedCloneSource,origin);
                if(pair.Value.currentCodeVolume!=null)pair.Key.volume=pair.Value.volumeBase*pair.Value.currentCodeVolume();
                if(pair.Value.sourceStopped!=null&&pair.Value.sourceStopped())pair.Key.Stop();
            }
        }
        if(limitFrame!=Time.frameCount){limits.Tick(FrameDelta);limitFrame=Time.frameCount;}
        for(int i=voices.Count-1;i>=0;i--)
        {
            var voice=voices[i];NativeAudioCue binding;
            bool owned=voice!=null&&trackedSources.TryGetValue(voice,out binding)&&binding.keepSource&&binding.trackedSource!=null&&(binding.sourceDestroyed==null||!binding.sourceDestroyed());
            if(voice==null||!voice.isPlaying&&!owned){ForgetSource(voice);if(voice!=null)AssetRegistry.ReleaseTransient(voice.gameObject);voices.RemoveAt(i);}
        }
    }
    public void StopAll(){foreach(var voice in voices)if(voice!=null){voice.Stop();AssetRegistry.ReleaseTransient(voice.gameObject);}voices.Clear();existingSources.Clear();trackedSources.Clear();}
    AudioMixerGroup Group(string mixerName,string name)
    {
        if(mixerBundle==null)
        {
            mixerBundle=AssetBundle.LoadFromFile(Path.Combine(root,"native-audio-mixer.assetbundle"));
            if(mixerBundle==null)throw new InvalidDataException("Original reconstructed mixer bundle missing");
            var loaded=mixerBundle.LoadAllAssets<AudioMixer>();if(loaded.Length!=1)throw new InvalidDataException("Expected single original mixer");mixer=loaded[0];
        }
        if(mixer.name!=mixerName)throw new InvalidDataException("Original mixer identity mismatch");
        var groups=mixer.FindMatchingGroups(name);if(groups.Length!=1||groups[0].name!=name)throw new InvalidDataException("Original mixer group mismatch");return groups[0];
    }
    public void Dispose(){if(disposed)return;disposed=true;StopAll();limits.Clear();foreach(var clip in clips.Values)AssetRegistry.ReleaseTransient(clip);clips.Clear();if(mixerBundle!=null)mixerBundle.Unload(true);}
}
