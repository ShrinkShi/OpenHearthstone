using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Rebuild the source weapon audio branches and the two verified summon-glow clips.
public sealed class NativeBattleWeapons
{
    [Serializable] public sealed class Entry
    {
        public int slot;public string reference,state,action;public float volume,delay;
        public NativeAudioDefinition definition;public NativeSoundAsset sound;
        public SummonGlow summon_glow;
    }
    [Serializable] public sealed class Profile
    {public string actor_reference,table_reference,profile;public Entry[] entries;}
    [Serializable] public sealed class GlowKey
    {public float time,value,inSlope,outSlope;}
    [Serializable] public sealed class GlowCurves
    {public GlowKey[] r,g,b;}
    [Serializable] public sealed class SummonGlow
    {
        public string profile,texture_name,texture_file,material,shader_name;
        public float duration,tint_alpha;
        public bool shader_known,compiled_source_verified,texture_alpha_opaque;
        public NativeMaterial material_definition;
        public GlowCurves tint_curves;
    }
    sealed class Binding{public Entry entry;public string key;public int seat=-1,entity;}
    sealed class ActiveGlow{public int seat,entity;public Entry entry;public float age;public AnimationCurve r,g,b;}
    public sealed class GlowSample{public int seat,entity;public float age,duration;public Texture2D texture;public Color tint;}
    readonly Dictionary<int,Entry> entries=new Dictionary<int,Entry>();
    readonly Dictionary<SemanticEvent,Binding> bindings=new Dictionary<SemanticEvent,Binding>();
    readonly Dictionary<int,ActiveGlow> activeGlows=new Dictionary<int,ActiveGlow>();
    readonly AssetRegistry assets;
    Material glowComposite;
    string match="";int version=-1;
    public Action<NativeAudioCue> AudioCue;
    public int Started{get;private set;}
    public int Failures{get;private set;}
    public int BindingCount{get{return bindings.Count;}}
    public bool Enabled{get{return entries.Count==4;}}
    public int ActiveGlowCount{get{return activeGlows.Count;}}
    public NativeBattleWeapons(AssetRegistry assets)
    {
        this.assets=assets;if(assets==null)return;string folder=assets.Root;
        string path=Path.Combine(folder,"weapon-audio.json");if(!File.Exists(path))return;
        try
        {
            var profile=JsonUtility.FromJson<Profile>(File.ReadAllText(path));
            bool withGlow=profile.profile=="source-fsm-audio-and-summon-glow-v1";
            if((!withGlow&&profile.profile!="source-fsm-audio-only-v1")||profile.actor_reference!=ActorReference||profile.table_reference!=TableReference||profile.entries==null||profile.entries.Length!=4)
                throw new InvalidDataException("Weapon source profile mismatch");
            foreach(var entry in profile.entries)
            {
                string guid=entry.slot==41?"b142a0cfb212145268a4ec77aeb2912f":entry.slot==42?"eafae527ed9d24df3b7b34367d2361bc":
                    entry.slot==7?"f5881061a688d4095b81ca3d7a8c9cb7":entry.slot==5?"9583bd3b2ad5a4b9c90ed680d460a12e":null;
                float volume=entry.slot==7?.6f:entry.slot==5?1:.8f;
                string state=entry.slot==7?"Birth State 1":entry.slot==5?"Birth State 5":"State 3";
                if(guid==null||!entry.reference.EndsWith(":"+guid,StringComparison.Ordinal)||entry.state!=state||
                    entry.action!=(entry.slot==7?"AudioPlayAction":"AudioPlaythroughAction")||!Mathf.Approximately(entry.volume,volume)||
                    entry.delay!=0||entry.definition==null)
                    throw new InvalidDataException("Weapon slot action mismatch");
                string clip=entry.slot==7?"Weapon_Durability_Hit_01.wav:8cc86918ea7f78f4487be0f430b11119":
                    entry.slot==5?"Weapon_Destruction_01.wav:780a0d0d979a7684484209017ab3ddd9":"hero_weapon_draw.wav:e6f1e9d2da090c94fb2b0048acb84599";
                var cues=entry.slot==7?entry.definition.clips:entry.sound?.clips;
                if(cues==null||cues.Length!=1||cues[0].reference!=clip||Path.GetFileName(cues[0].file)!=cues[0].file||!File.Exists(Path.Combine(folder,cues[0].file))||Resolve(entry)==null)
                    throw new InvalidDataException("Weapon audio dependency mismatch");
                bool summon=entry.slot==41||entry.slot==42;
                // JsonUtility 为缺省嵌套类生成空对象，先还原无视觉配置的两个音频槽。
                if(entry.summon_glow!=null&&String.IsNullOrEmpty(entry.summon_glow.profile)&&
                   String.IsNullOrEmpty(entry.summon_glow.texture_file)&&String.IsNullOrEmpty(entry.summon_glow.material))entry.summon_glow=null;
                if(withGlow&&summon)ValidateGlow(entry,folder);
                else if(entry.summon_glow!=null)throw new InvalidDataException("Unexpected weapon glow profile");
                entries.Add(entry.slot,entry);
            }
        }
        catch(Exception error){entries.Clear();Debug.LogWarning("Original weapon audio unavailable: "+error.Message);}
    }
    static AnimationCurve Curve(GlowKey[] source)
    {
        if(source==null||source.Length!=4)return null;
        return new AnimationCurve(source.Select(key=>new Keyframe(key.time,key.value,key.inSlope,key.outSlope)).ToArray());
    }
    static void ValidateGlow(Entry entry,string folder)
    {
        var glow=entry.summon_glow;
        string texture=entry.slot==41?"WeaponSummonBlue":"WeaponSummonRed";
        string material=entry.slot==41?"FX_Weapon_SummonGlowblue":"FX_Weapon_SummonGlowRed";
        if(glow==null||glow.profile!="source-additive-tint-v2"||glow.texture_name!=texture||
            Path.GetFileName(glow.texture_file)!=glow.texture_file||!File.Exists(Path.Combine(folder,glow.texture_file))||
            glow.material!=material||!glow.shader_known||!glow.compiled_source_verified||!glow.texture_alpha_opaque||
            glow.shader_name!="Hero/Additive/Additive"||!SupportsFlatGlowMaterial(glow.material_definition,material,glow.texture_file)||
            !Mathf.Approximately(glow.duration,.68333334f)||
            !Mathf.Approximately(glow.tint_alpha,.5019608f)||glow.tint_curves==null||
            Curve(glow.tint_curves.r)==null||Curve(glow.tint_curves.g)==null||Curve(glow.tint_curves.b)==null)
            throw new InvalidDataException("Weapon summon glow source profile mismatch");
        foreach(var curve in new[]{glow.tint_curves.r,glow.tint_curves.g,glow.tint_curves.b})
            if(Mathf.Abs(curve[0].time)>1e-6f||Mathf.Abs(curve[0].value)>1e-6f||
                Mathf.Abs(curve[1].time-.21666667f)>1e-5f||Mathf.Abs(curve[2].time-.5f)>1e-5f||
                Mathf.Abs(curve[3].time-glow.duration)>1e-5f)
                throw new InvalidDataException("Weapon summon tint key profile mismatch");
    }
    static bool SupportsFlatGlowMaterial(NativeMaterial material,string name,string textureFile)
    {
        if(material==null||material.name!=name||material.shader!="Hero/Additive/Additive"||
            material.src!=5||material.dst!=1||material.src_alpha!=5||material.dst_alpha!=1||
            material.keywords==null||material.keywords.Length!=0||material.floats==null||material.textures==null)return false;
        foreach(string property in new[]{"_LightingBlend","_GlowIntensity","_GlowAddMul"})
        {
            var value=material.floats.Where(row=>row.name==property).ToArray();
            if(value.Length!=1||value[0].value!=0)return false;
        }
        var main=material.textures.Where(row=>row.name=="_MainTex").ToArray();
        return main.Length==1&&main[0].path==textureFile&&main[0].scale.x==1&&main[0].scale.y==1&&
            main[0].offset.x==0&&main[0].offset.y==0;
    }
    const string ActorReference="Card_Play_Weapon.prefab:71f767d4f10681a45ac853936d1db800";
    const string TableReference="Card_Play_Weapon_SpellTable.prefab:9eb63fe4f82064b50b443e1b15a4825d";
    static NativeAudioCue Resolve(Entry entry)
    {
        return entry.slot==7?NativeAudio.Resolve(entry.definition,"",0,entry.volume,()=>.5f):
            NativeAudio.ResolvePlaythroughSound(entry.definition,"",-1,entry.sound,0,entry.volume,()=>.5f);
    }
    public static int Slot(SemanticEvent data,StateView before,StateView after)
    {
        if(data==null||before?.players==null||after?.players==null)return -1;
        if(data.type=="Summon"&&data.card_type==7)
        {
            var owner=after.players.FirstOrDefault(p=>p.weapon!=null&&p.weapon.entity==data.target);
            if(owner==null||before.players.Any(p=>p.weapon!=null&&p.weapon.entity==data.target))return -1;
            return owner.seat==after.seat?41:42;
        }
        var old=before.players.FirstOrDefault(p=>p.weapon!=null&&p.weapon.entity==data.target);
        if(old==null)return -1;
        if(data.type=="Death"&&!after.players.Any(p=>p.weapon!=null&&p.weapon.entity==data.target))return 5;
        if(data.type!="Hit"||data.source!=data.target||data.amount<=0||
            !(after.events??new SemanticEvent[0]).Any(e=>e.type=="Attack"&&e.source==old.hero.entity))return -1;
        var shown=data.visual??after.players.Select(p=>p.weapon).FirstOrDefault(w=>w!=null&&w.entity==data.target);
        bool died=(after.events??new SemanticEvent[0]).Any(e=>e.type=="Death"&&e.target==data.target);
        return shown!=null&&shown.durability<old.weapon.durability||shown==null&&died?7:-1;
    }
    public void Observe(StateView before,StateView after)
    {
        if(after==null)return;
        if(before==null||before.match_id!=after.match_id){Clear();match=after.match_id;version=after.version;return;}
        if(match==after.match_id&&version==after.version)return;
        if(match!=after.match_id){Clear();match=after.match_id;}
        version=after.version;if(!Enabled)return;
        var events=after.events??new SemanticEvent[0];
        for(int i=0;i<events.Length;i++)
        {
            int slot=Slot(events[i],before,after);Entry entry;if(!entries.TryGetValue(slot,out entry))continue;
            var binding=new Binding{entry=entry,key=after.match_id+":"+after.version+":"+i,entity=events[i].target};
            if((slot==41||slot==42)&&entry.summon_glow!=null)
            {
                var owner=after.players.FirstOrDefault(player=>player.weapon!=null&&player.weapon.entity==events[i].target);
                if(owner!=null)
                    binding.seat=owner.seat;
            }
            bindings[events[i]]=binding;
        }
        if(bindings.Count>1024){bindings.Clear();Failures++;Debug.LogWarning("Weapon audio event binding overflow");}
    }
    // Presentation 回调使用原事件对象，避免读取已推进到后续批次的权威 state。
    public bool TryHandle(SemanticEvent data)
    {
        Binding binding;if(AudioCue==null||!bindings.TryGetValue(data,out binding))return false;
        bindings.Remove(data);
        // 网络快照只登记；召唤Cue真正开始时才启动装备光效，避免排队期间提前显示。
        if(binding.seat>=0&&binding.entry.summon_glow!=null)
        {
            var source=binding.entry.summon_glow;
            activeGlows[binding.seat]=new ActiveGlow{seat=binding.seat,entity=binding.entity,entry=binding.entry,age=0,
                r=Curve(source.tint_curves.r),g=Curve(source.tint_curves.g),b=Curve(source.tint_curves.b)};
        }
        Play(binding);
        return true;
    }
    void Play(Binding binding)
    {
        try
        {
            var cue=Resolve(binding.entry);if(cue==null)throw new InvalidDataException("Weapon cue unresolved");
            cue.sourceName="Weapon slot "+binding.entry.slot+" "+binding.key;
            AudioCue(cue);Started++;
            Debug.Log("NATIVE_WEAPON_AUDIO key="+binding.key+" slot="+binding.entry.slot+" file="+cue.file+" volume="+cue.volume);
        }
        catch(Exception error){Failures++;Debug.LogWarning("Original weapon audio: "+error.Message);}
    }
    public void Complete(StateView after)
    {foreach(var data in after.events??new SemanticEvent[0])bindings.Remove(data);}
    public void Advance(float delta)
    {
        if(delta<0||float.IsNaN(delta)||float.IsInfinity(delta))throw new ArgumentException("Invalid weapon glow delta");
        foreach(var seat in activeGlows.Keys.ToArray())
        {
            var glow=activeGlows[seat];glow.age=Mathf.Min(glow.age+delta,glow.entry.summon_glow.duration);
            if(glow.age>=glow.entry.summon_glow.duration)activeGlows.Remove(seat);
        }
    }
    public bool TrySummonGlow(int seat,out GlowSample sample)
    {
        ActiveGlow active;sample=null;
        if(!activeGlows.TryGetValue(seat,out active)||assets==null)return false;
        var source=active.entry.summon_glow;var texture=assets.TextureFile(source.texture_file);
        if(texture==null)return false;
        sample=new GlowSample{seat=seat,entity=active.entity,age=active.age,duration=source.duration,texture=texture,
            tint=new Color(active.r.Evaluate(active.age),active.g.Evaluate(active.age),active.b.Evaluate(active.age),source.tint_alpha)};
        return true;
    }
    public bool HasSummonGlow(int seat)
    {GlowSample sample;return TrySummonGlow(seat,out sample);}
    public bool DrawSummonGlow(int seat,Rect rect)
    {
        if(Event.current==null||Event.current.type!=EventType.Repaint)return false;
        return DrawSummonGlow(seat,rect,GUI.matrix,Screen.width,Screen.height,GUI.color);
    }
    public bool DrawSummonGlow(int seat,Rect rect,Matrix4x4 matrix,int width,int height,Color tint)
    {
        GlowSample sample;if(!TrySummonGlow(seat,out sample))return false;
        if(width<=0||height<=0||Mathf.Max(sample.tint.r,Mathf.Max(sample.tint.g,sample.tint.b))<=0)return false;
        // Verified family-16 base: 2*vertexRGBA*TintColor, then SrcAlpha One.
        // This opaque texture, white quad, zero lighting/glow profile reduces to 4*RGB*alpha.
        if(glowComposite==null)glowComposite=new Material(Resources.Load<Shader>("NativeEffectAffineComposite"));
        var emission=sample.tint;emission.r*=4;emission.g*=4;emission.b*=4;
        glowComposite.mainTexture=sample.texture;glowComposite.SetColor("_Tint",tint*emission);
        GL.PushMatrix();
        try
        {
            GL.LoadPixelMatrix(0,width,height,0);GL.MultMatrix(matrix);
            if(!glowComposite.SetPass(1))return false;
            GL.Begin(GL.QUADS);
            GL.TexCoord2(0,1);GL.Vertex3(rect.xMin,rect.yMin,0);GL.TexCoord2(1,1);GL.Vertex3(rect.xMax,rect.yMin,0);
            GL.TexCoord2(1,0);GL.Vertex3(rect.xMax,rect.yMax,0);GL.TexCoord2(0,0);GL.Vertex3(rect.xMin,rect.yMax,0);GL.End();
        }
        finally{GL.PopMatrix();}
        return true;
    }
    public void Clear(){bindings.Clear();activeGlows.Clear();match="";version=-1;AssetRegistry.ReleaseTransient(glowComposite);glowComposite=null;}
}
