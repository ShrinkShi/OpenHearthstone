using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// Source SUMMON_IN uses the actual minion Actor as its local Spell parent.
// Both sides share the source Actor; opponent SUMMON_IN is original SpellTable slot 42.
public sealed class NativeBattleSummons : IDisposable
{
    sealed class Definition{public string file;public float duration,blocking;}
    sealed class Entry{public GameObject actor;public int entity;public string card;public float age;public bool tail;public Definition definition;public NativeActorSpellTable<NativeEffectInstance> table;public NativeEffectInstance spell;}
    readonly AssetRegistry assets;readonly NativeBattleActors actors;
    readonly Dictionary<string,Entry> active=new Dictionary<string,Entry>();
    readonly HashSet<string> failed=new HashSet<string>();
    public Action<NativeAudioCue> AudioCue;
    public Func<SemanticEvent,StateView,StateView,bool> SkipSpell;
    public Camera TimelineCamera;
    public int ActiveCount{get{return active.Count;}}
    public int TailCount{get{return active.Values.Count(e=>e.tail);}}
    public int Failures{get;private set;}
    readonly Dictionary<int,Definition> definitions=new Dictionary<int,Definition>();
    public readonly float SourceDuration;
    public readonly float BlockingDuration;
    public IEnumerable<GameObject> BorrowedActors{get{return active.Values.Select(e=>e.actor);}}
    public NativeBattleSummons(AssetRegistry assets,NativeBattleActors actors)
    {
        this.assets=assets;this.actors=actors;
        LoadDefinition(2,"92c50d3e8ca46833c7a5dc25.actor-spell.json");
        LoadDefinition(42,"d8a6fae1472426f61e3f501e.actor-spell.json");
        Definition friendly;if(definitions.TryGetValue(2,out friendly)){SourceDuration=friendly.duration;BlockingDuration=friendly.blocking;}
    }
    void LoadDefinition(int type,string file)
    {
        string path=Path.Combine(assets.Root,file);
        if(File.Exists(path))
        {
            var sourceProgram=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(path));
            if(String.IsNullOrEmpty(sourceProgram.audio_file)||!File.Exists(Path.Combine(assets.Root,sourceProgram.audio_file)))return;
            var machine=sourceProgram.machines[0];var initial=NativeSpellLifecycle.Entry(machine);
            var state=initial==null?null:machine.states.First(s=>s.name==initial.state);var visited=new HashSet<string>();float duration=0,finish=float.PositiveInfinity;
            while(state!=null&&visited.Add(state.name))
            {
                foreach(var action in state.actions.Where(a=>a.name=="SpellFinishAction"))
                {
                    var delay=action.Param("m_Delay");
                    if(delay.unresolved||!String.IsNullOrEmpty(delay.variable))throw new InvalidDataException("Dynamic original summon finish time is not restored");
                    finish=Mathf.Min(finish,duration+Mathf.Max(0,delay.number));
                }
                duration+=state.actions.Where(a=>a.name=="Wait").Select(a=>a.Param("time").number).DefaultIfEmpty(0).Max();
                var next=state.transitions.FirstOrDefault(t=>t.@event=="FINISHED");state=next==null?null:machine.states.FirstOrDefault(s=>s.name==next.state);
            }
            if(float.IsInfinity(finish))throw new InvalidDataException("Original summon has no SpellFinished boundary");
            definitions.Add(type,new Definition{file=file,duration=duration+.06f,blocking=finish+.06f});
        }
    }
    int SpellType(SemanticEvent data,StateView before,StateView after)
    {
        if(data==null||data.type!="Summon"||after==null)return -1;
        if(SkipSpell!=null&&SkipSpell(data,before,after))return -1;
        var owner=(after.players??new PlayerView[0]).FirstOrDefault(p=>(p.field??new EntityView[0]).Any(e=>e.entity==data.target));
        return owner==null?-1:owner.seat==after.seat?2:42;
    }
    public bool Handles(SemanticEvent data,StateView after){return definitions.ContainsKey(SpellType(data,null,after));}
    public bool Handles(SemanticEvent data,StateView before,StateView after){return definitions.ContainsKey(SpellType(data,before,after));}
    public float Duration(SemanticEvent data,StateView after){Definition definition;return definitions.TryGetValue(SpellType(data,null,after),out definition)?definition.blocking:0;}
    public float Duration(SemanticEvent data,StateView before,StateView after){Definition definition;return definitions.TryGetValue(SpellType(data,before,after),out definition)?definition.blocking:0;}
    public bool Has(string key){return active.ContainsKey(key);}
    public void BeginFrame(IEnumerable<string> keys)
    {
        var keep=new HashSet<string>(keys);var remove=active.Keys.Where(k=>!keep.Contains(k)).ToArray();
        foreach(var key in remove)
        {
            var entry=active[key];if(entry.tail)continue;
            // OnSpellFinished unblocks zones; the original FSM can still run its cleanup Wait.
            entry.age=Mathf.Max(entry.age,entry.definition.blocking);entry.spell.Seek(entry.age);
            if(!entry.spell.FinishNotifications.Any(n=>n.delivered))
            {entry.table.ReleaseAll();active.Remove(key);Failures++;continue;}
            entry.tail=true;
        }
    }
    public void Advance(float delta,StateView shown)
    {
        if(delta<0||float.IsNaN(delta)||float.IsInfinity(delta))throw new ArgumentException("Invalid summon tail delta");
        foreach(var key in active.Keys.ToArray())
        {
            var entry=active[key];if(!entry.tail)continue;
            bool visible=shown!=null&&(shown.players??new PlayerView[0]).Any(p=>(p.field??new EntityView[0]).Any(e=>e.entity==entry.entity&&e.card_id==entry.card));
            if(!visible){entry.table.ReleaseAll();active.Remove(key);continue;}
            entry.age+=delta;
            entry.spell.Seek(Mathf.Min(entry.age,entry.definition.duration));
            if(entry.age>=entry.definition.duration){entry.table.ReleaseAll();active.Remove(key);}
        }
    }
    public bool Prepare(string key,SemanticEvent data,StateView before,StateView after,float age)
    {
        if(!Handles(data,before,after)||age<0||failed.Contains(key))return false;
        Entry entry;
        try
        {
            if(!active.TryGetValue(key,out entry))
            {
                var owner=(after.players??new PlayerView[0]).FirstOrDefault(p=>(p.field??new EntityView[0]).Any(e=>e.entity==data.target));
                if(owner==null)return false;
                int type=SpellType(data,before,after);var definition=definitions[type];
                var entity=owner.field.First(e=>e.entity==data.target);var actor=actors.Resolve(entity);
                var identity=actor==null?null:actor.GetComponent<NativeActorIdentity>();
                if(identity==null||identity.SpellDefinition==null||identity.AnimationProgram==null)return false;
                if(active.Values.Any(e=>e.actor==actor))return false;
                entry=new Entry{actor=actor,entity=entity.entity,card=entity.card_id,definition=definition};
                var slot=(identity.SpellDefinition.entries??new NativeActorSpellEntry[0]).FirstOrDefault(s=>s.type==type);
                if(slot==null||slot.file!=definition.file)return false;
                entry.table=new NativeActorSpellTable<NativeEffectInstance>(identity.SpellDefinition,
                    source=>new NativeEffectInstance(JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,source.file))),assets,active.Count,32,32,context:new NativeSpellContext(4,placement:NativeSpellObjectSelection.ForActor(actor,4)),audioCallback:AudioCue),
                    (spell,type)=>{spell.SetSpellType(type);spell.BindTimelineCamera(TimelineCamera);},spell=>spell.AttachToOriginalParent(actor),spell=>spell.Dispose());
                entry.spell=entry.table.GetSpell(type,true);
                if(entry.spell==null){entry.table.ReleaseAll();return false;}
                active.Add(key,entry);
            }
            if(entry.tail)return true;
            entry.age=age;entry.spell.Seek(age);
            if(entry.spell.UnboundActorInitializationActions!=0||entry.spell.UnboundAnimationActions!=0||entry.spell.UnboundAudioActions!=0)
                throw new InvalidOperationException("Source SUMMON_IN has unbound Actor/animation/audio actions");
            return true;
        }
        catch(Exception error)
        {
            Failures++;failed.Add(key);
            if(active.TryGetValue(key,out entry)){entry.table.ReleaseAll();active.Remove(key);}
            Debug.LogWarning("Original summon fallback: "+error.Message);return false;
        }
    }
    public void Dispose(){foreach(var entry in active.Values)entry.table.ReleaseAll();active.Clear();failed.Clear();}
}
