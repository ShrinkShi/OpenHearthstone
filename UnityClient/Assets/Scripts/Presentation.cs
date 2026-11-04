using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 网络快照立即入队，视觉按事件追赶；服务器不参与任何动画计时。
public sealed class Presentation
{
    public sealed class Cue
    {
        public SemanticEvent data;
        public float start,duration,apply,impactOffset;
        public bool fired,applied;
        public Cue nativeImpact;
        public bool impactResolved;
        public bool damageDecorationCreated;
        public bool Ready{get{return nativeImpact==null||impactResolved;}}
        public float Progress(float time){return duration<=0?(time<start?0:1):Mathf.Clamp01((time-start)/duration);}
    }
    sealed class DamageDecoration
    {
        public int target;
        public string value;
        public float age,lifetime;
        public Vector2 lastPosition;
    }
    sealed class NativeVisualTail
    {
        public Batch batch;
        public Cue cue;
        public float age;
    }
    sealed class Batch
    {
        public StateView before,after;
        public readonly List<Cue> cues=new List<Cue>();
        public Cue[] steps;
        public bool incremental,sourceCanOutliveBatch;
        public float duration,resolve,inputDuration;
    }
    readonly Queue<Batch> queue=new Queue<Batch>();
    readonly Dictionary<int,Vector2> origins=new Dictionary<int,Vector2>();
    readonly List<DamageDecoration> damageDecorations=new List<DamageDecoration>();
    readonly List<NativeVisualTail> nativeVisualTails=new List<NativeVisualTail>();
    Batch current;
    float elapsed;
    bool resolved;
    public StateView Display{get;private set;}
    public Action<SemanticEvent,string> AudioCue;
    public Func<string,float> NativeDuration;
    public Func<string,float> NativeImpactDelay;
    public Func<string,SemanticEvent,StateView,StateView,Rect,float,bool> NativeDraw;
    public Func<string,SemanticEvent,StateView,StateView,Rect,float,bool> NativeButtonDraw;
    public Func<string,SemanticEvent,StateView,StateView,Rect,float,bool> NativeShamanSummonDraw;
    public Func<int,Rect?> ArmorBadgeBounds;
    public Action<string,SemanticEvent,StateView,StateView,bool,float> NativePrepare;
    public Func<SemanticEvent,StateView,StateView,bool> NativeSummonSuppressed;
    public Action<Rect,string,float> DamageSplatDraw;
    public Func<string,bool> NativeActive;
    public Func<string,bool> NativeTailActive;
    public Func<string,float?> NativeImpactTime;
    public Func<string,SemanticEvent,StateView,StateView,float,bool> NativeSummonPrepare;
    public Func<string,SemanticEvent,StateView,StateView,float,bool> NativeShamanSummonPrepare;
    public Func<string,SemanticEvent,float,bool> NativeShamanDeathPrepare;
    public Func<string,bool> NativeShamanSummonActive;
    public Func<string,bool> NativeSummonActive;
    public Func<SemanticEvent,StateView,float> NativeSummonDuration;
    public Action<StateView,StateView> BatchCompleted;
    public float Speed=1;
    public bool Paused;
    public int QueueCount{get{return queue.Count+(current==null?0:1);}}
    public bool Busy{get{return current!=null||queue.Count!=0||nativeVisualTails.Count!=0;}}
    public int NativeVisualTailCount{get{return nativeVisualTails.Count;}}
    public bool InputLocked
    {
        get
        {
            if(queue.Count!=0)return true;
            if(current==null)return false;
            if(elapsed<current.inputDuration||current.cues.Any(c=>c.nativeImpact!=null&&!c.impactResolved))return true;
            if(NativeActive==null)return false;
            foreach(var spell in current.cues.Where(c=>c.data.type=="SpellEffect"&&NativeActive(NativeKey(c))))
            {
                if(spell.data.card_id=="HERO_02bp"||spell.data.card_id=="HERO_09bp"||spell.data.card_id=="HERO_04bp"||spell.data.card_id=="HERO_07bp")continue;
                if(spell.data.card_id=="HERO_06bp"&&elapsed>=spell.start+spell.duration)continue;
                if(SourceCanOutliveBatch(spell.data.card_id))continue;
                bool resolvedHit=current.cues.Any(c=>c.nativeImpact==spell&&c.data.type=="Hit"&&c.impactResolved&&elapsed>=c.start+c.duration);
                if(!resolvedHit)return true;
            }
            return false;
        }
    }
    static bool SourceCanOutliveBatch(string card)
    {return card=="HERO_01bp"||card=="HERO_10bp"||card==NativeDeathKnightHeroPowerPlayback.HeroPowerId;}
    static bool IsReplacedWeaponDeath(SemanticEvent cue,StateView before,StateView after)
    {
        if(cue.type!="Death"||before.players==null||after.players==null)return false;
        bool wasWeapon=before.players.Any(player=>player.weapon!=null&&player.weapon.entity==cue.target);
        return wasWeapon&&!after.players.Any(player=>player.weapon!=null&&player.weapon.entity==cue.target);
    }
    public int DamageDecorationCount{get{return damageDecorations.Count;}}
    public float TimeInBatch{get{return elapsed;}}
    public IEnumerable<Cue> Cues{get{return current==null?Enumerable.Empty<Cue>():current.cues;}}
    public float SampleTime(string scene)
    {
        Cue cue=null;
        if(scene=="fan_triggers")cue=Cues.FirstOrDefault(c=>c.data.type=="Trigger");
        else if(scene=="death")cue=Cues.FirstOrDefault(c=>c.data.type=="Death");
        else if(scene.Contains("combat")||scene=="minion_attack")
        {cue=Cues.FirstOrDefault(c=>c.data.type=="Hit");if(cue!=null)return cue.start+.03f;}
        else if(scene=="minion_summon"||scene=="weapon_summon")cue=Cues.FirstOrDefault(c=>c.data.type=="Summon");
        else if(scene=="card_draw")cue=Cues.FirstOrDefault(c=>c.data.type=="Draw");
        if(cue==null)cue=Cues.FirstOrDefault(c=>c.data.type=="SpellEffect")??Cues.FirstOrDefault(c=>c.data.type=="PlayHeroPower");
        return cue==null?.48f:cue.start+cue.duration*.55f;
    }
    public StateView Before{get{return current==null?Display:current.before;}}
    public Vector2 Shake
    {
        get
        {
            var hit=Cues.FirstOrDefault(c=>c.Ready&&c.data.type=="Hit"&&!IsLifeTapSelfDamage(c)&&elapsed>=c.start&&elapsed<c.start+.16f);
            if(hit==null)return Vector2.zero;
            float t=elapsed-hit.start;return new Vector2(Mathf.Sin(t*130),Mathf.Cos(t*110))*3*(1-t/.16f);
        }
    }
    public void Reset(StateView state=null){queue.Clear();current=null;Display=state;origins.Clear();damageDecorations.Clear();nativeVisualTails.Clear();elapsed=0;}
    public void Accept(StateView before,StateView after,Dictionary<int,Rect> locations)
    {
        if(before==null||before.match_id!=after.match_id){Reset(after);return;}
        if(before.version==after.version)return;
        var b=new Batch{before=before,after=after,resolve=.12f,duration=.22f,incremental=before.phase=="play"};
        float cursor=0,impact=0,reactions=0,lastTrigger=-1,cardReveal=-1;int anchor=0;Cue nativePower=null;
        var combat=(after.events??new SemanticEvent[0]).FirstOrDefault(e=>e.type=="Attack");
        bool ordinaryCombat=combat!=null&&!(after.events??new SemanticEvent[0]).Any(e=>
            e.type=="Trigger"||e.type=="Summon"||e.type=="Draw"||e.type=="Give"||e.type=="Buff"||e.type=="Heal"||e.type=="Morph"||e.type=="GainArmor"||e.type=="EquipWeapon");
        foreach(var e in after.events??new SemanticEvent[0])
        {
            float start=cursor,duration=.32f;
            if(e.type=="Attack") {impact=start+.28f;duration=.78f;anchor=e.source;cursor=impact;}
            else if(e.type=="CardPlay")
            {
                anchor=e.source;duration=e.card_type==5?.72f:.84f;impact=start+.58f;cardReveal=start+duration;
                var profile=CardEffectRegistry.Find(e.card_id);
                float native=NativeDuration==null?0:NativeDuration(e.card_id);
                bool associated=native>0||profile!=null&&profile.visuals!=null&&profile.visuals.Length>0;
                if(e.card_type==5||associated)
                {
                    float cast=native>0?native:profile!=null&&profile.cast_duration>0?profile.cast_duration:.7f;
                    float effectStart=cardReveal+(e.card_type==5?0:.2f);
                    var spell=new SemanticEvent{type="SpellEffect",source=e.source,target=e.target!=0?e.target:e.card_type==5?0:e.source,card_id=e.card_id,owner=e.owner};
                    b.cues.Add(new Cue{data=spell,start=effectStart,duration=cast,apply=effectStart});
                    float delay=native>0&&NativeImpactDelay!=null?NativeImpactDelay(e.card_id):-1;
                    impact=effectStart+(delay>=0&&!float.IsNaN(delay)&&!float.IsInfinity(delay)?Mathf.Min(delay,cast):cast);
                }
                cursor=impact;
            }
            else if(e.type=="PlayHeroPower")
            {
                impact=start+.32f;duration=.65f;anchor=e.target;
                var power=BattleInteraction.Entity(before,e.target);string card=String.IsNullOrEmpty(e.card_id)?power?.card_id:e.card_id;
                if(card=="HERO_07bp")impact=start;
                if(card=="HERO_08bp"&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,card_id=card,owner=BattleInteraction.Owner(before,e.target)},
                        start=start,duration=NativeDuration(card),apply=float.MaxValue};
                    b.cues.Add(nativePower);duration=Mathf.Max(duration,nativePower.duration);
                }
                else if(card=="HERO_02bp"&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,card_id=card,owner=BattleInteraction.Owner(before,e.target)},
                        start=start,duration=NativeDuration(card),apply=float.MaxValue};
                    b.cues.Add(nativePower);
                }
                else if(card=="HERO_05bp"&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,card_id=card,owner=BattleInteraction.Owner(before,e.target)},
                        start=start,duration=NativeDuration(card),apply=start};
                    b.cues.Add(nativePower);duration=Mathf.Max(duration,nativePower.duration);
                }
                else if(card=="HERO_06bp"&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,card_id=card,owner=BattleInteraction.Owner(before,e.target)},
                        start=start,duration=NativeDuration(card),apply=start};
                    b.cues.Add(nativePower);duration=Mathf.Max(duration,nativePower.duration);
                }
                else if(card=="HERO_01bp"&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,card_id=card,owner=BattleInteraction.Owner(before,e.target)},
                        start=start,duration=NativeDuration(card),apply=start};
                    b.cues.Add(nativePower);
                }
                else if(card=="HERO_07bp"&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    // Life Tap plays only its authored source root; Hit and Draw remain the result events.
                    nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,card_id=card,owner=BattleInteraction.Owner(before,e.target)},
                        start=start,duration=NativeDuration(card),apply=float.MaxValue};
                    b.cues.Add(nativePower);
                }
                else if(card=="HERO_03bp"&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    // 匕首只由权威武器 Summon 呈现；source 重建不派发父级 Chain 或实体。
                    nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,card_id=card,owner=BattleInteraction.Owner(before,e.target)},
                        start=start,duration=NativeDuration(card),apply=float.MaxValue};
                    b.cues.Add(nativePower);
                }
                else if(card=="HERO_09bp"&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    // Priest Start/Impact are visual only; the server Heal event remains authoritative.
                    int target;
                    if(TryGetPriestTarget(e,before,after,out target))
                    {
                        nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,target=target,card_id=card,owner=BattleInteraction.Owner(before,e.target)},
                            start=start,duration=NativeDuration(card),apply=float.MaxValue};
                        b.cues.Add(nativePower);
                    }
                }
                else if(card=="HERO_04bp"&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    // Paladin's source-area reconstruction is visual only; the batch Summon owns the token.
                    nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,card_id=card,owner=BattleInteraction.Owner(before,e.target)},
                        start=start,duration=NativeDuration(card),apply=float.MaxValue};
                    b.cues.Add(nativePower);
                }
                else if(card==NativeDeathKnightHeroPowerPlayback.HeroPowerId&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    NativeDeathKnightHeroPowerPlayback.Pair pair;
                    if(NativeDeathKnightHeroPowerPlayback.TryGetPair(e,before,after,out pair))
                    {
                        nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,target=pair.ghoul.entity,
                            card_id=card,owner=pair.ownerIndex},start=start,duration=NativeDuration(card),apply=float.MaxValue};
                        b.cues.Add(nativePower);
                    }
                }
                else if(card==NativeDemonHunterHeroPowerPlayback.HeroPowerId&&NativeDuration!=null&&NativeDuration(card)>0)
                {
                    NativeDemonHunterHeroPowerPlayback.Pair pair;
                    if(NativeDemonHunterHeroPowerPlayback.TryGetPair(e,before,after,out pair))
                    {
                        nativePower=new Cue{data=new SemanticEvent{type="SpellEffect",source=e.target,target=pair.hero.entity,
                            card_id=card,owner=pair.ownerIndex},start=start,duration=NativeDuration(card),apply=float.MaxValue};
                        b.cues.Add(nativePower);
                    }
                }
                cursor=impact;
            }
            else if(e.type=="Hit"||e.type=="Heal")
            {
                bool weaponWear=combat!=null&&e.source==e.target&&before.players.Any(p=>p.hero.entity==combat.source&&p.weapon!=null&&p.weapon.entity==e.target);
                bool lifeTapDamage=nativePower!=null&&nativePower.data.card_id=="HERO_07bp"&&IsHeroPowerTarget(nativePower,before,e.target);
                bool exchange=combat!=null&&e.type=="Hit"&&(((e.source==combat.source&&e.target==combat.target)||(e.source==combat.target&&e.target==combat.source))||weaponWear);
                start=lifeTapDamage?nativePower.start:weaponWear?impact+.55f:(exchange||anchor!=0&&e.source==anchor?impact:cursor);
                duration=.55f;reactions=Mathf.Max(reactions,start+duration);
            }
            else if(e.type=="Morph"||e.type=="Buff"||e.type=="Silence")
            {
                bool druidPowerBuff=e.type=="Buff"&&nativePower!=null&&nativePower.data.card_id=="HERO_06bp"&&IsHeroPowerTarget(nativePower,before,e.target);
                start=druidPowerBuff?nativePower.start:Mathf.Max(cursor,impact);duration=druidPowerBuff?0:.55f;cursor=Mathf.Max(cursor,start+duration);
            }
            else if(e.type=="Trigger") {start=Mathf.Max(cursor,reactions)+.04f;duration=.5f;lastTrigger=start;cursor=start+duration+.06f;}
            else if(e.type=="GainArmor")
            {
                bool druidPowerArmor=nativePower!=null&&nativePower.data.card_id=="HERO_06bp"&&IsHeroPowerTarget(nativePower,before,e.target);
                start=druidPowerArmor?nativePower.start:lastTrigger>=0?lastTrigger+.24f:cursor;
                if(!druidPowerArmor)lastTrigger=-1;duration=.36f;cursor=Mathf.Max(cursor,start+duration+.04f);
            }
            else if(e.type=="Death")
            {
                // 普通交战的反击、扣血和双方死亡并行；真正的扳机链仍按事件次序播放。
                if(ordinaryCombat){start=impact+.14f;duration=.4f;}
                else {start=Mathf.Max(cursor,reactions)+.12f;duration=.64f;cursor=start+duration+.08f;}
            }
            else if(e.type=="Summon"||e.type=="EquipWeapon")
            {
                bool playedEntity=cardReveal>=0&&e.target==anchor;
                bool shamanPowerSummon=e.type=="Summon"&&nativePower!=null&&nativePower.data.card_id=="HERO_02bp"&&e.source==nativePower.data.source;
                float nativeDuration=e.type=="Summon"&&NativeSummonDuration!=null?NativeSummonDuration(e,after):0;
                start=playedEntity?cardReveal:shamanPowerSummon?nativePower.start:cursor;duration=nativeDuration>0?nativeDuration:.5f;cursor=Mathf.Max(cursor,start+duration+.08f);
            }
            else if(e.type=="Draw"||e.type=="Give") {start=Mathf.Max(cursor,reactions);duration=.62f;cursor=start+.28f;}
            else if(e.type=="Discard") {duration=.35f;cursor+=duration;}
            else if(e.type=="TURN_START") {start=cursor;duration=1.1f;cursor+=.22f;}
            else continue;
            float apply=e.type=="Death"?start+duration:e.type=="PlayHeroPower"?impact:start;
            bool rogueWeaponDeath=nativePower!=null&&nativePower.data.card_id=="HERO_03bp"&&IsReplacedWeaponDeath(e,before,after);
            b.cues.Add(new Cue{data=e,start=start,duration=duration,apply=apply,
                impactOffset=start-impact,nativeImpact=nativePower!=null&&nativePower.data.card_id!="HERO_07bp"&&nativePower.data.card_id!="HERO_09bp"&&
                    ((e.type=="Hit"||e.type=="Heal")&&e.source==anchor||e.type=="Death"&&!rogueWeaponDeath)?nativePower:null});b.duration=Mathf.Max(b.duration,start+duration+.06f);
            if(e.type=="CardPlay"||e.type=="PlayHeroPower")
                b.cues.Add(new Cue{data=new SemanticEvent{type="EffectLaunch",source=e.type=="PlayHeroPower"?e.target:e.source,card_id=e.card_id},start=start+(e.type=="CardPlay"?(e.card_type==5?.72f:.84f):.08f),duration=.05f,apply=float.MaxValue});
        }
        b.duration=Mathf.Max(b.duration,b.cues.Where(c=>c.data.type!="SpellEffect"||!SourceCanOutliveBatch(c.data.card_id))
            .Select(c=>c.start+c.duration+.06f).DefaultIfEmpty(.22f).Max());
        b.inputDuration=b.cues.Where(c=>c.data.type!="SpellEffect"&&c.data.type!="EffectLaunch")
            .Select(c=>c.start+c.duration).DefaultIfEmpty(b.resolve).Max();
        b.sourceCanOutliveBatch=b.cues.Any(c=>c.data.type=="SpellEffect"&&SourceCanOutliveBatch(c.data.card_id));
        if(b.sourceCanOutliveBatch)b.inputDuration=Mathf.Min(b.inputDuration,.2f);
        var ordered=b.cues.OrderBy(c=>c.start).ToArray();b.cues.Clear();b.cues.AddRange(ordered);
        b.steps=b.cues.OrderBy(c=>c.apply).ToArray();b.resolve=anchor!=0?impact:.15f;
        if(before.phase=="mulligan") {b.resolve=.62f;b.duration=Mathf.Max(b.duration,1.35f);}
        queue.Enqueue(b);
        // 掉帧后仍有有界恢复路径，不能积累无限队列或保留失效输入。
        if(queue.Count>64){Debug.LogWarning("Presentation queue overflow: resync");Reset(after);}
        if(current==null)Begin(locations);
    }
    void Begin(Dictionary<int,Rect> locations)
    {
        if(queue.Count==0)return;
        current=queue.Dequeue();elapsed=0;resolved=false;Display=Clone(current.before);
        origins.Clear();foreach(var pair in locations)origins[pair.Key]=pair.Value.center;
    }
    public void Tick(float delta,Dictionary<int,Rect> locations)
    {
        if(Paused)return;
        float advance=delta*Mathf.Clamp(Speed,.1f,8);
        foreach(var decoration in damageDecorations)decoration.age+=advance;
        damageDecorations.RemoveAll(decoration=>decoration.age>=decoration.lifetime);
        foreach(var tail in nativeVisualTails)tail.age+=advance;
        nativeVisualTails.RemoveAll(tail=>!NativeVisualTailActive(tail.batch,tail.cue));
        if(current==null){Begin(locations);if(current==null)return;}
        elapsed+=advance;
        foreach(var source in current.cues.Where(c=>c.nativeImpact!=null&&!c.impactResolved).Select(c=>c.nativeImpact).Distinct())
        {
            var at=NativeImpactTime?.Invoke(NativeKey(source));
            if(at.HasValue)
            {
                // 到达首次被表现层观察时才开始完整受击窗口；掉帧不吞掉反馈，死亡仍排在受击之后。
                float visibleArrival=Mathf.Max(elapsed,source.start+at.Value);
                foreach(var cue in current.cues.Where(c=>c.nativeImpact==source&&!c.impactResolved))
                {
                    float change=visibleArrival+cue.impactOffset-cue.start;cue.start+=change;cue.apply+=change;cue.impactResolved=true;
                    current.duration=Mathf.Max(current.duration,cue.start+cue.duration+.06f);
                    current.inputDuration=Mathf.Max(current.inputDuration,cue.start+cue.duration);
                }
            }
        }
        foreach(var cue in current.cues)
        {
            if(cue.nativeImpact!=null&&!cue.impactResolved)continue;
            if(!cue.fired&&elapsed>=cue.start)
            {
                cue.fired=true;
                if(AudioCue!=null)AudioCue(cue.data,CardId(cue.data.type=="Death"?cue.data.target:cue.data.source));
            }
            if(!cue.damageDecorationCreated&&elapsed>=cue.start&&cue.data.type=="Hit"&&cue.data.amount>0&&IsDamageSplatTarget(cue.data.target))
            {
                cue.damageDecorationCreated=true;
                damageDecorations.Add(new DamageDecoration{target=cue.data.target,value="-"+Mathf.RoundToInt(Mathf.Abs(cue.data.amount)),
                    age=Mathf.Max(0,elapsed-cue.start),lifetime=4.1f,lastPosition=Position(cue.data.target,locations)});
            }
        }
        if(current.incremental){foreach(var cue in current.steps)if(!cue.applied&&(cue.nativeImpact==null||cue.impactResolved)&&elapsed>=cue.apply){ApplyVisual(cue.data);cue.applied=true;}}
        else if(!resolved&&elapsed>=current.resolve){Display=current.after;resolved=true;}
        if(elapsed>=current.duration&&!HasBlockingNativeCue(current))
        {RetainNativeVisualTails(current);Complete(current);current=null;Begin(locations);}
    }
    bool HasBlockingNativeCue(Batch batch)
    {
        foreach(var cue in batch.cues)
        {
            if(cue.nativeImpact!=null&&!cue.impactResolved)return true;
            if(cue.data.type!="SpellEffect"||NativeActive==null)continue;
            string key=NativeKey(batch,cue);if(!NativeActive(key))continue;
            if(SourceCanOutliveBatch(cue.data.card_id))continue;
            // H06's post-duration native lifetime is cleanup-only; it is not retained as a visual tail.
            if(cue.data.card_id=="HERO_06bp"&&elapsed>=cue.start+cue.duration)continue;
            if(cue.data.card_id=="HERO_04bp"&&NativeTailActive!=null&&NativeTailActive(key))continue;
            return true;
        }
        return false;
    }
    void RetainNativeVisualTails(Batch batch)
    {
        foreach(var cue in batch.cues.Where(c=>c.data.type=="SpellEffect"&&
            (c.data.card_id=="HERO_04bp"||SourceCanOutliveBatch(c.data.card_id))))
        {
            string key=NativeKey(batch,cue);
            if(NativeVisualTailActive(batch,cue)&&!nativeVisualTails.Any(t=>NativeKey(t.batch,t.cue)==key))
                nativeVisualTails.Add(new NativeVisualTail{batch=batch,cue=cue,age=Mathf.Max(0,elapsed-cue.start)});
        }
    }
    bool NativeVisualTailActive(Batch batch,Cue cue)
    {
        string key=NativeKey(batch,cue);
        return NativeTailActive!=null&&NativeTailActive(key)||
            SourceCanOutliveBatch(cue.data.card_id)&&NativeActive!=null&&NativeActive(key);
    }
    static StateView Clone(StateView value){return JsonUtility.FromJson<StateView>(JsonUtility.ToJson(value));}
    static EntityView VisualCopy(EntityView entity){var copy=BattleInteraction.Copy(entity);if(copy!=null)copy.entity=entity.entity;return copy;}
    void Complete(Batch batch){Display=batch.after;if(BatchCompleted!=null)BatchCompleted(batch.before,batch.after);}
    void ApplyVisual(SemanticEvent e)
    {
        var target=BattleInteraction.Entity(Display,e.target);
        if(e.visual!=null&&target!=null)
        {
            var copy=BattleInteraction.Copy(e.visual);copy.entity=e.visual.entity;
            foreach(var p in Display.players)
            {
                if(p.hero.entity==copy.entity)p.hero=copy;
                if(p.power.entity==copy.entity)p.power=copy;
                if(p.weapon!=null&&p.weapon.entity==copy.entity)p.weapon=copy;
                int index=Array.FindIndex(p.field,c=>c.entity==copy.entity);if(index>=0)p.field[index]=copy;
            }
        }
        else if(target!=null&&(e.type=="Hit"||e.type=="Heal"||e.type=="GainArmor"))
        {
            if(e.type=="GainArmor")target.armor+=(int)e.amount;
            else if(e.type=="Heal")target.health+=(int)e.amount;
            else {int armor=Mathf.Min(target.armor,(int)e.amount);target.armor-=armor;target.health-=(int)e.amount-armor;}
        }
        if(e.type=="CardPlay")
        {
            int owner=BattleInteraction.Owner(Display,e.source);if(owner<0)owner=current.before.active;
            if(owner>=0&&owner<Display.players.Length)
            {var p=Display.players[owner];p.hand=p.hand.Where(c=>c.entity!=e.source).ToArray();p.hand_count=Mathf.Max(0,p.hand_count-1);p.mana=current.after.players[owner].mana;}
        }
        if(e.type=="PlayHeroPower")
        {
            int owner=BattleInteraction.Owner(Display,e.target);
            if(owner>=0){Display.players[owner].power.used=current.after.players[owner].power.used;Display.players[owner].mana=current.after.players[owner].mana;}
        }
        if(e.type=="Summon"&&target==null)
        {
            var entity=e.visual??BattleInteraction.Entity(current.after,e.target);
            int owner=e.owner>=0?e.owner:BattleInteraction.Owner(current.after,e.target);
            if(entity!=null&&owner>=0&&owner<Display.players.Length)
            {
                var copy=VisualCopy(entity);var player=Display.players[owner];var after=current.after.players[owner];
                if(e.card_type==7||(after.weapon!=null&&after.weapon.entity==e.target))
                {player.weapon=copy;player.hero.attack=after.hero.attack;}
                else if(e.card_type==3)player.hero=copy;
                else {var field=player.field.ToList();field.Insert(e.index<0?field.Count:Mathf.Clamp(e.index,0,field.Count),copy);player.field=field.ToArray();}
            }
        }
        if(e.type=="Draw"||e.type=="Give")
        {
            int owner=e.owner>=0?e.owner:BattleInteraction.Owner(current.after,e.target);
            if(owner>=0&&owner<Display.players.Length)
            {
                var p=Display.players[owner];var card=current.after.players[owner].hand.FirstOrDefault(c=>c.entity==e.target);
                if(!p.hand.Any(c=>c.entity==e.target))
                {if(card!=null)p.hand=p.hand.Concat(new[]{VisualCopy(card)}).ToArray();p.hand_count++;if(e.type=="Draw")p.deck_count=Mathf.Max(0,p.deck_count-1);}
            }
        }
        if(e.type=="Death")foreach(var p in Display.players)
        {p.field=p.field.Where(c=>c.entity!=e.target).ToArray();if(p.weapon!=null&&p.weapon.entity==e.target)p.weapon=null;}
        if(e.type=="EquipWeapon"&&e.owner>=0&&e.owner<Display.players.Length)
        {Display.players[e.owner].weapon=VisualCopy(current.after.players[e.owner].weapon);Display.players[e.owner].hero.attack=current.after.players[e.owner].hero.attack;}
        if(e.type=="TURN_START")
        {
            Display.active=current.after.active;Display.turn=current.after.turn;
            for(int i=0;i<Display.players.Length;i++){var p=Display.players[i];var after=current.after.players[i];p.mana=after.mana;p.max_mana=after.max_mana;p.power.used=after.power.used;}
        }
    }
    public int ArmorBefore(Cue cue)
    {
        if(current==null||cue==null)return 0;
        var entity=BattleInteraction.Entity(current.before,cue.data.target);
        int armor=entity==null?0:Mathf.Max(0,entity.armor);
        int index=current.cues.IndexOf(cue);
        for(int i=0;i<index;i++)
        {
            var prior=current.cues[i].data;
            if(prior.target==cue.data.target&&(prior.type=="GainArmor"||prior.type=="Hit"))
                armor=ArmorAfter(prior,armor);
        }
        return armor;
    }
    static int ArmorAfter(SemanticEvent e,int before)
    {
        if(e.visual!=null)return Mathf.Max(0,e.visual.armor);
        int amount=Mathf.RoundToInt(Mathf.Abs(e.amount));
        if(e.type=="GainArmor")return before+amount;
        if(e.type=="Hit")return Mathf.Max(0,before-amount);
        return before;
    }
    public int ArmorAfter(Cue cue){return cue==null?0:ArmorAfter(cue.data,ArmorBefore(cue));}
    public int ArmorAbsorbed(Cue cue)
    {
        if(cue==null||cue.data.type!="Hit")return 0;
        return Mathf.Clamp(ArmorBefore(cue)-ArmorAfter(cue),0,Mathf.RoundToInt(Mathf.Abs(cue.data.amount)));
    }
    public int ArmorAbsorbed(SemanticEvent data)
    {
        var cue=current==null?null:current.cues.FirstOrDefault(item=>ReferenceEquals(item.data,data));
        return ArmorAbsorbed(cue);
    }
    public bool IsInitialArmorGain(Cue cue)
    {return cue!=null&&cue.data.type=="GainArmor"&&ArmorBefore(cue)==0&&ArmorAfter(cue)>0;}
    bool IsDamageSplatTarget(int entity)
    {
        if(current==null||entity==0)return false;
        foreach(var state in new[]{current.before,current.after})
        {
            if(state==null||state.players==null)continue;
            foreach(var player in state.players)
            {
                if(player.hero!=null&&player.hero.entity==entity)return true;
                if(player.field!=null&&player.field.Any(card=>card!=null&&card.entity==entity))return true;
            }
        }
        return false;
    }
    public static Rect HeroPowerButtonBounds(int source,Dictionary<int,Rect> locations,Vector2 fallbackCenter)
    {
        Rect bounds;
        if(locations!=null&&locations.TryGetValue(source,out bounds)&&bounds.width>0&&bounds.height>0)return bounds;
        return new Rect(fallbackCenter.x-61,fallbackCenter.y-63,122,126);
    }
    public void Skip()
    {
        if(current!=null)Complete(current);
        while(queue.Count!=0)Complete(queue.Dequeue());
        current=null;elapsed=0;damageDecorations.Clear();
    }
    public void Seek(float time)
    {
        if(current==null)return;elapsed=time;resolved=time>=current.resolve;Display=Clone(current.before);
        if(!current.incremental){if(resolved)Display=current.after;return;}
        foreach(var cue in current.steps){cue.applied=time>=cue.apply;if(cue.applied)ApplyVisual(cue.data);}
        foreach(var cue in current.cues)cue.fired=time>=cue.start;
    }
    public string CardId(int entity)
    {
        foreach(var s in new[]{Before,current==null?Display:current.after})
        {
            if(s==null)continue;
            foreach(var p in s.players)
            {
                if(p.hero.entity==entity)return p.hero.card_id;
                if(p.power.entity==entity)return p.power.card_id;
                var e=p.field.Concat(p.hand).FirstOrDefault(c=>c.entity==entity);if(e!=null)return e.card_id;
            }
        }
        return "";
    }
    public Rect Animate(int entity,Rect final)
    {
        foreach(var cue in Cues)
        {
            var e=cue.data;float age=elapsed-cue.start;if(!cue.Ready||age<0||age>cue.duration)continue;
            if(e.type=="Attack"&&e.source==entity&&origins.ContainsKey(e.target))
            {
                Vector2 from=origins.ContainsKey(entity)?origins[entity]:final.center,to=origins[e.target];
                Vector2 offset=to-from;
                float amount=age<.12f?-.055f*Tween.Curve(age/.12f):age<.28f?Mathf.Lerp(-.055f,.78f,Tween.Curve((age-.12f)/.16f,Ease.In)):age<.35f?.78f:Mathf.Lerp(.78f,0,Tween.Curve((age-.35f)/.43f,Ease.Out));
                final.center=from+offset*amount;
            }
            else if(e.type=="Hit"&&e.target==entity&&!IsLifeTapSelfDamage(cue))final.x+=Mathf.Sin(age*110)*9*Mathf.Exp(-age*10);
            else if(e.type=="Summon"&&e.target==entity)
            {
                if(NativeSummonActive!=null&&NativeSummonActive(NativeKey(cue)))continue;
                var center=final.center;final.size*=Mathf.Lerp(.25f,1,Tween.Curve(age/.45f,Ease.Overshoot));final.center=center;
            }
        }
        return final;
    }
    public float Tint(int entity)
    {
        var hit=Cues.FirstOrDefault(c=>c.Ready&&c.data.type=="Hit"&&!IsLifeTapSelfDamage(c)&&c.data.target==entity&&elapsed>=c.start&&elapsed<c.start+.28f);
        return hit==null?0:Mathf.Sin((elapsed-hit.start)/.28f*Mathf.PI)*.65f;
    }
    static bool IsHeroPowerTarget(Cue spell,StateView before,int target)
    {
        return spell!=null&&before!=null&&target!=0&&before.players.Any(player=>
            player.power!=null&&player.power.entity==spell.data.source&&player.hero!=null&&player.hero.entity==target);
    }
    static bool TryGetPriestTarget(SemanticEvent power,StateView before,StateView after,out int target)
    {
        target=0;
        if(power==null||power.type!="PlayHeroPower"||power.target==0||before==null||after==null)return false;
        var owners=(before.players??new PlayerView[0]).Where(player=>player.power!=null&&player.power.entity==power.target).ToArray();
        if(owners.Length!=1||owners[0].power.card_id!="HERO_09bp"||
           !String.IsNullOrEmpty(power.card_id)&&power.card_id!="HERO_09bp")return false;
        int ownerSeat=owners[0].seat;
        var events=after.events??new SemanticEvent[0];
        var authorityTargets=events.Where(item=>item.type=="HeroPowerTarget").ToArray();
        var heals=events.Where(item=>item.type=="Heal"&&item.source==power.target&&item.target!=0).ToArray();
        if(authorityTargets.Length!=0)
        {
            if(authorityTargets.Length!=1)return false;
            var route=authorityTargets[0];
            if(route.source!=power.target||route.owner!=ownerSeat||route.card_id!="HERO_09bp"||route.target==0||route.target==power.target||
               !IsCharacterTarget(before,route.target)&&!IsCharacterTarget(after,route.target))return false;
            if(heals.Length!=0&&(heals.Length!=1||heals[0].target!=route.target))return false;
            target=route.target;return true;
        }
        if(heals.Length!=1||heals[0].target==power.target||
           !IsCharacterTarget(before,heals[0].target)&&!IsCharacterTarget(after,heals[0].target))return false;
        target=heals[0].target;return true;
    }
    static bool IsCharacterTarget(StateView state,int target)
    {
        return state!=null&&(state.players??new PlayerView[0]).Any(player=>
            player.hero!=null&&player.hero.entity==target||
            (player.field??new EntityView[0]).Any(entity=>entity!=null&&entity.entity==target));
    }
    bool IsLifeTapSelfDamage(Cue cue)
    {
        return cue!=null&&cue.data.type=="Hit"&&current!=null&&current.cues.Any(spell=>
            spell.data.type=="SpellEffect"&&spell.data.card_id=="HERO_07bp"&&IsHeroPowerTarget(spell,current.before,cue.data.target));
    }
    public bool Playing(int entity){return Cues.Any(c=>c.data.type=="CardPlay"&&c.data.source==entity&&elapsed>=c.start&&elapsed<c.start+c.duration);}
    public bool Dying(int entity){return Cues.Any(c=>c.Ready&&c.data.type=="Death"&&c.data.target==entity&&elapsed>=c.start&&elapsed<c.start+c.duration);}
    public float TriggerPulse(int entity)
    {
        var cue=Cues.LastOrDefault(c=>c.data.type=="Trigger"&&c.data.source==entity&&elapsed>=c.start&&elapsed<c.start+c.duration);
        return cue==null?0:Mathf.Sin(cue.Progress(elapsed)*Mathf.PI);
    }
    public float HandProgress(int entity)
    {
        var draw=Cues.FirstOrDefault(c=>(c.data.type=="Draw"||c.data.type=="Give")&&c.data.target==entity);
        return draw==null?1:Tween.Curve(draw.Progress(elapsed));
    }
    public float EnemyHandProgress(int index,int seat)
    {
        if(current==null||index<current.before.players[seat].hand_count)return 1;
        var draw=Cues.LastOrDefault(c=>c.data.type=="Draw"||c.data.type=="Give");
        return draw==null?1:Tween.Curve(draw.Progress(elapsed));
    }
    Vector2 Position(int entity,Dictionary<int,Rect> locations)
    {
        Rect r;if(locations.TryGetValue(entity,out r))return r.center;
        Vector2 point;return origins.TryGetValue(entity,out point)?point:new Vector2(960,570);
    }
    static bool TryShamanButtonBounds(int entity,StateView before,StateView after,Dictionary<int,Rect> locations,out Rect bounds)
    {
        bounds=default(Rect);
        if(locations!=null&&locations.TryGetValue(entity,out bounds)&&bounds.width>0&&bounds.height>0)return true;
        foreach(var state in new[]{before,after})
        {
            if(state==null)continue;
            foreach(var player in state.players??new PlayerView[0])
                if(player.power!=null&&player.power.entity==entity){bounds=BattleLayout.Power(player.seat==state.seat);return true;}
        }
        return false;
    }
    static bool TryShamanTokenBounds(int entity,StateView before,StateView after,Dictionary<int,Rect> locations,out Rect bounds)
    {
        bounds=default(Rect);
        if(locations!=null&&locations.TryGetValue(entity,out bounds)&&bounds.width>0&&bounds.height>0)return true;
        foreach(var state in new[]{after,before})
        {
            if(state==null)continue;
            foreach(var player in state.players??new PlayerView[0])
            {
                var field=player.field??new EntityView[0];
                int index=Array.FindIndex(field,item=>item.entity==entity);
                if(index>=0){bounds=BattleLayout.Minion(player.seat==state.seat,index,field.Length);return true;}
            }
        }
        return false;
    }
    public Vector2? NativePosition(int entity,Dictionary<int,Rect> locations)
    {Rect rect;if(locations.TryGetValue(entity,out rect))return rect.center;Vector2 point;return origins.TryGetValue(entity,out point)?point:(Vector2?)null;}
    bool NativeVisible(Cue cue)
    {
        string key=NativeKey(cue);
        bool active=NativeActive!=null&&NativeActive(key),tail=NativeTailActive!=null&&NativeTailActive(key);
        return elapsed>=cue.start&&(elapsed<=cue.start+cue.duration||active||tail);
    }
    string NativeKey(Cue cue){return NativeKey(current,cue);}
    string NativeKey(Batch batch,Cue cue){return batch.after.match_id+":"+batch.after.version+":"+cue.data.source+":"+cue.start+(cue.data.type=="Summon"?":Summon:"+cue.data.target:"");}
    public IEnumerable<string> NativeSummonKeys()
    {foreach(var cue in Cues)if(cue.data.type=="Summon"&&elapsed>=cue.start&&elapsed<=cue.start+cue.duration)yield return NativeKey(cue);}
    public IEnumerable<int> NativeDeathEntities()
    {foreach(var cue in Cues)if(cue.data.type=="Death")yield return cue.data.target;}
    public IEnumerable<string> NativeKeys()
    {
        foreach(var cue in Cues)if(cue.data.type=="SpellEffect"&&NativeVisible(cue))yield return NativeKey(cue);
        foreach(var tail in nativeVisualTails)
        {
            string key=NativeKey(tail.batch,tail.cue);
            if(NativeVisualTailActive(tail.batch,tail.cue))yield return key;
        }
    }
    public void PrepareNative()
    {
        if(NativePrepare!=null)foreach(var tail in nativeVisualTails.ToArray())
        {
            string key=NativeKey(tail.batch,tail.cue);
            if(!NativeVisualTailActive(tail.batch,tail.cue))continue;
            var spell=CardEffectRegistry.Find(tail.cue.data.card_id);
            NativePrepare(key,tail.cue.data,tail.batch.before,tail.batch.after,
                spell!=null&&(spell.kind=="area"||spell.kind=="fan"),tail.age);
        }
        if(current==null)return;
        // Prepare H11's source root before its paired Summon reaches the generic writer.
        var ordered=Cues.Where(cue=>cue.data.type=="SpellEffect"&&cue.data.card_id==NativeDeathKnightHeroPowerPlayback.HeroPowerId)
            .Concat(Cues.Where(cue=>cue.data.type!="SpellEffect"||cue.data.card_id!=NativeDeathKnightHeroPowerPlayback.HeroPowerId));
        foreach(var cue in ordered)
        {
            float age=elapsed-cue.start;if(age<0||age>cue.duration&&!(cue.data.type=="SpellEffect"&&NativeVisible(cue)))continue;
            if(cue.data.type=="Summon"&&NativeShamanSummonPrepare!=null&&NativeShamanSummonPrepare(NativeKey(cue),cue.data,current.before,current.after,age))continue;
            if(cue.data.type=="Death"&&NativeShamanDeathPrepare!=null){NativeShamanDeathPrepare(NativeKey(cue),cue.data,age);continue;}
            if(cue.data.type=="Summon"&&NativeSummonSuppressed!=null&&NativeSummonSuppressed(cue.data,current.before,current.after))continue;
            if(cue.data.type=="Summon"&&NativeSummonPrepare!=null){NativeSummonPrepare(NativeKey(cue),cue.data,current.before,current.after,age);continue;}
            if(cue.data.type!="SpellEffect"||NativePrepare==null)continue;
            var spell=CardEffectRegistry.Find(cue.data.card_id);
            NativePrepare(NativeKey(cue),cue.data,current.before,current.after,spell!=null&&(spell.kind=="area"||spell.kind=="fan"),age);
        }
    }
    public void Draw(Dictionary<int,Rect> locations,Action<Rect,EntityView> drawEntity,Action<Rect,EntityView> drawCard,Action<Rect,string> text,Action<Rect,string,Color> effect)
    {
        if(current==null&&damageDecorations.Count==0&&nativeVisualTails.Count==0)return;
        if(Event.current!=null&&Event.current.type==EventType.Repaint&&NativeDraw!=null)
        foreach(var tail in nativeVisualTails.ToArray())
        {
            string key=NativeKey(tail.batch,tail.cue);
            if(!NativeVisualTailActive(tail.batch,tail.cue))continue;
            if(tail.cue.data.card_id=="HERO_01bp")
            {
                var button=HeroPowerButtonBounds(tail.cue.data.source,locations,Position(tail.cue.data.source,locations));
                NativeButtonDraw?.Invoke(key,tail.cue.data,tail.batch.before,tail.batch.after,button,tail.age);
                continue;
            }
            var spell=CardEffectRegistry.Find(tail.cue.data.card_id);
            bool area=spell!=null&&(spell.kind=="area"||spell.kind=="fan");
            var center=area?new Vector2(960,545):tail.cue.data.target!=0?Position(tail.cue.data.target,locations):new Vector2(960,480);
            var nativeRect=area?new Rect(center.x-575,center.y-260,1150,520):new Rect(center.x-220,center.y-220,440,440);
            NativeDraw(key,tail.cue.data,tail.batch.before,tail.batch.after,nativeRect,tail.age);
            if(tail.cue.data.card_id==NativeDeathKnightHeroPowerPlayback.HeroPowerId&&NativeButtonDraw!=null)
            {
                Rect button;
                if(!locations.TryGetValue(tail.cue.data.source,out button))
                {var point=Position(tail.cue.data.source,locations);button=new Rect(point.x-60,point.y-60,120,120);}
                NativeButtonDraw(key,tail.cue.data,tail.batch.before,tail.batch.after,
                    NativeDeathKnightHeroPowerPlayback.ButtonEffectRect(button),tail.age);
            }
        }
        if(current==null&&damageDecorations.Count==0)return;
        Color old=GUI.color;
        foreach(var death in Cues.Where(c=>c.data.type=="Death"))
        {
            if(!death.Ready||elapsed<death.start||elapsed>death.start+death.duration)continue;
            var entity=BattleInteraction.Entity(current.before,death.data.target)??BattleInteraction.Entity(Display,death.data.target);
            if(entity==null)continue;
            if(current.before.players.Any(p=>p.hero.entity==entity.entity))continue;
            float t=death.Progress(elapsed);var center=Position(entity.entity,locations)+Vector2.down*35*t;
            GUI.color=new Color(.65f,.65f,.65f,1-Tween.Curve(t,Ease.In));
            var ghost=VisualCopy(entity);ghost.health=0;ghost.durability=0;
            bool weapon=current.before.players.Any(p=>p.weapon!=null&&p.weapon.entity==entity.entity);
            float size=Mathf.Lerp(134,44,Tween.Curve(t,Ease.In)),height=size*(weapon?1:1.28f);
            drawEntity(Animate(entity.entity,new Rect(center.x-size/2,center.y-height/2,size,height)),ghost);GUI.color=old;
            effect(new Rect(center.x-115,center.y-115,230,230),"UI_CARD_GLOW",new Color(.7f,.25f,.07f,(1-t)*.7f));
        }
        foreach(var cue in Cues)
        {
            var e=cue.data;float age=elapsed-cue.start;
            bool shamanSummonActive=e.type=="Summon"&&NativeShamanSummonActive!=null&&NativeShamanSummonActive(NativeKey(cue));
            if(cue.nativeImpact!=null&&!cue.impactResolved||age<0||age>cue.duration&&!(e.type=="SpellEffect"&&NativeVisible(cue))&&!shamanSummonActive)continue;
            Vector2 target=Position(e.target,locations);float t=cue.Progress(elapsed);
            if(e.type=="CardPlay")
            {
                EntityView entity=current.before.players.SelectMany(p=>p.hand).FirstOrDefault(c=>c.entity==e.source);
                if(entity==null)entity=new EntityView{card_id=e.card_id,entity=e.source};
                Vector2 from=Position(e.source,locations),at=Vector2.Lerp(from,new Vector2(960,480),Tween.Curve(t/.52f));
                float scale=Mathf.Lerp(.85f,1.25f,Tween.Curve(t/.5f));GUI.color=new Color(1,1,1,t<.72f?1:(1-t)/.28f);
                drawCard(new Rect(at.x-110*scale,at.y-165*scale,220*scale,330*scale),entity);GUI.color=old;
            }
            if(e.type=="SpellEffect")
            {
                var spell=CardEffectRegistry.Find(e.card_id);
                var at=new Vector2(960,480);
                bool area=spell!=null&&(spell.kind=="area"||spell.kind=="fan");
                var center=area?new Vector2(960,545):e.target!=0?target:at;
                var nativeRect=area?new Rect(center.x-575,center.y-260,1150,520):new Rect(center.x-220,center.y-220,440,440);
                string nativeKey=NativeKey(cue);
                if(e.card_id=="HERO_01bp"||e.card_id=="HERO_06bp")
                {
                    var button=HeroPowerButtonBounds(e.source,locations,Position(e.source,locations));
                    if(NativeButtonDraw!=null)NativeButtonDraw(nativeKey,e,current.before,current.after,button,age);
                    continue;
                }
                bool nativeDrawn;
                if(e.card_id=="HERO_02bp")
                {
                    bool routeActive=NativeActive!=null&&NativeActive(nativeKey);
                    Rect buttonBounds;
                    nativeDrawn=TryShamanButtonBounds(e.source,current.before,current.after,locations,out buttonBounds)&&NativeDraw!=null&&
                        NativeDraw(nativeKey,e,current.before,current.after,NativeShamanHeroPowerPlayback.ButtonEffectRect(buttonBounds),age);
                    if(routeActive)continue;
                }
                else nativeDrawn=NativeDraw!=null&&NativeDraw(nativeKey,e,current.before,current.after,nativeRect,age);
                if(e.card_id==NativeDeathKnightHeroPowerPlayback.HeroPowerId)
                {
                    Rect button;
                    if(!locations.TryGetValue(e.source,out button))
                    {var source=Position(e.source,locations);button=new Rect(source.x-60,source.y-60,120,120);}
                    var buttonEffect=NativeDeathKnightHeroPowerPlayback.ButtonEffectRect(button);
                    if(NativeButtonDraw!=null)NativeButtonDraw(nativeKey,e,current.before,current.after,buttonEffect,age);
                    continue;
                }
                if(nativeDrawn)continue;
                if(spell!=null&&spell.visuals!=null&&spell.visuals.Length>0)
                    DrawAssociatedEffect(spell,e,t,at,target,locations,effect);
                if(spell!=null&&spell.kind=="fan")
                {
                    foreach(int id in Cues.Where(c=>c.data.type=="Hit"&&c.data.source==e.source).Select(c=>c.data.target).Distinct())
                    {
                        var to=Position(id,locations);float flight=Tween.Curve(Mathf.Clamp01(t/.94f),Ease.In);
                        var point=Vector2.Lerp(at+new Vector2(0,100),to,flight);var matrix=GUI.matrix;
                        BattleGeometry.Rotate(Mathf.Atan2(to.y-at.y,to.x-at.x)*Mathf.Rad2Deg+90,point);
                        effect(new Rect(point.x-32,point.y-72,64,144),spell.projectile,new Color(.34f,.92f,.32f,Mathf.Sin(t*Mathf.PI)));
                        effect(new Rect(point.x-45,point.y-90,90,180),spell.impact,new Color(.3f,1,.3f,(1-t)*.55f));GUI.matrix=matrix;
                    }
                }
                else if(e.target!=0&&spell!=null&&spell.kind=="slash")
                {
                    float slash=Tween.Curve(t,Ease.Out);var matrix=GUI.matrix;
                    var point=target+Vector2.Lerp(new Vector2(-64,-82),new Vector2(64,60),slash);
                    BattleGeometry.Rotate(-38,point);
                    effect(new Rect(point.x-24,point.y-64,48,128),spell.projectile,new Color(.35f,1,.55f,Mathf.Sin(t*Mathf.PI)));
                    GUI.matrix=matrix;
                    float size=Mathf.Lerp(90,210,slash);
                    effect(new Rect(target.x-size/2,target.y-size/2,size,size),spell.impact,new Color(.4f,1,.5f,Mathf.Sin(t*Mathf.PI)));
                }
                else if(e.target!=0&&spell!=null&&spell.kind=="rage")
                {
                    float alpha=Mathf.Sin(t*Mathf.PI);var matrix=GUI.matrix;
                    BattleGeometry.Rotate(t*65,target);
                    effect(new Rect(target.x-100,target.y-125,200,250),spell.projectile,new Color(1,.08f,.03f,alpha*.5f));GUI.matrix=matrix;
                    effect(new Rect(target.x-80,target.y-85,160,170),spell.impact,new Color(1,.1f,.04f,alpha*.85f));
                }
                else if(e.target!=0&&(spell==null||spell.visuals==null||spell.visuals.Length==0))
                {
                    var point=Vector2.Lerp(at,target,Tween.Curve(t,Ease.In));
                    effect(new Rect(point.x-46,point.y-46,92,92),spell==null||String.IsNullOrEmpty(spell.projectile)?"VFX_FIREBALL_BOLT":spell.projectile,e.card_id=="CS2_024"?new Color(.45f,.85f,1):new Color(1,.6f,.1f));
                }
                if(spell!=null&&spell.kind=="draw"&&(spell.visuals==null||spell.visuals.Length==0))
                {
                    float alpha=Mathf.Sin(t*Mathf.PI);
                    effect(new Rect(at.x-110,at.y-120,220,240),spell.impact,new Color(.65f,.5f,1,alpha));
                    effect(new Rect(at.x-155,at.y-155,310,310),"VFX_INTELLECT_RUNES",new Color(.65f,.5f,1,alpha*.5f));
                }
            }
            if(e.type=="Hit"||e.type=="Heal"||e.type=="GainArmor")
            {
                if(e.type=="Heal"&&e.amount<=0)continue;
                var armorBadge=ArmorBadgeBounds==null?(Rect?)null:ArmorBadgeBounds(e.target);
                int absorbed=e.type=="Hit"?ArmorAbsorbed(cue):0;
                if(e.type=="Hit"&&absorbed>0&&armorBadge.HasValue)
                {
                    var badge=armorBadge.Value;float size=Mathf.Max(64,Mathf.Max(badge.width,badge.height)*1.35f);
                    effect(new Rect(badge.center.x-size/2,badge.center.y-size/2,size,size),"UI_CARD_GLOW",new Color(.72f,.9f,1,Mathf.Max(0,1-t*3)));
                }
                if(e.type=="Hit"&&IsDamageSplatTarget(e.target)&&DamageSplatDraw!=null)
                    continue;
                if(e.type=="GainArmor")
                {
                    bool firstArmor=IsInitialArmorGain(cue);
                    Vector2 numberPoint=armorBadge.HasValue?armorBadge.Value.center:target;
                    if(firstArmor)
                    {
                        float size=armorBadge.HasValue?Mathf.Max(64,Mathf.Max(armorBadge.Value.width,armorBadge.Value.height)*1.35f):110;
                        effect(new Rect(numberPoint.x-size/2,numberPoint.y-size/2,size,size),"UI_CARD_GLOW",new Color(.75f,.9f,1,Mathf.Max(0,1-t*3)));
                    }
                }
                else
                {
                    var profile=CardEffectRegistry.Find(CardId(e.source));
                    if(e.type=="Hit"&&profile!=null&&!String.IsNullOrEmpty(profile.impact))
                    {
                        float size=Mathf.Lerp(80,220,Tween.Curve(t));
                        effect(new Rect(target.x-size/2,target.y-size/2,size,size),profile.impact,new Color(1,1,1,(1-t)*(1-t)));
                    }
                    else if(profile!=null&&profile.visuals!=null&&profile.visuals.Length>0)
                    {
                        var sprite=profile.visuals[profile.visuals.Length-1];float size=Mathf.Lerp(65,170,Tween.Curve(t));
                        effect(new Rect(target.x-size/2,target.y-size/2,size,size),sprite.texture,sprite.tint*((1-t)*(1-t)));
                    }
                    effect(new Rect(target.x-90,target.y-90,180,180),"UI_CARD_GLOW",e.type=="Heal"?new Color(1,1,.82f,Mathf.Max(0,1-t*3)):new Color(1,.55f,.1f,Mathf.Max(0,1-t*3)));
                    GUI.color=e.type=="Heal"?new Color(1,1,1,1-t*t):new Color(1,.92f,.7f,1-t*t);
                    if(e.type=="Heal"&&DamageSplatDraw!=null)
                        DamageSplatDraw(new Rect(target.x-88,target.y-66,176,132),"+"+e.amount,1-t*t);
                    else text(new Rect(target.x-75,target.y-65-Tween.Curve(t)*45,150,85),(e.type=="Hit"?"-":"+")+e.amount);
                    GUI.color=old;
                }
            }
            if(e.type=="Summon")
            {
                if(Event.current!=null&&Event.current.type==EventType.Repaint&&NativeShamanHeroPowerPlayback.IsShamanSummon(e,current.before,current.after))
                {
                    Rect tokenBounds;
                    if(TryShamanTokenBounds(e.target,current.before,current.after,locations,out tokenBounds))
                    {
                        var token=NativeShamanHeroPowerPlayback.SummonEffectRect(tokenBounds);
                        NativeShamanSummonDraw?.Invoke(NativeKey(cue),e,current.before,current.after,token,age);
                    }
                }
                if(NativeSummonActive!=null&&NativeSummonActive(NativeKey(cue))||
                   NativeShamanSummonActive!=null&&NativeShamanSummonActive(NativeKey(cue))||
                   NativeSummonSuppressed!=null&&NativeSummonSuppressed(e,current.before,current.after))continue;
                float ring=Mathf.Lerp(95,260,Tween.Curve(t));
                effect(new Rect(target.x-ring/2,target.y-ring*.28f,ring,ring*.56f),"VFX_SUMMON_RING",new Color(1,1,1,Mathf.Sin(t*Mathf.PI)));
                for(int i=0;i<12;i++)
                {
                    float angle=i*Mathf.PI*2/12,travel=80*Tween.Curve(t);
                    var point=target+new Vector2(Mathf.Cos(angle)*travel,Mathf.Sin(angle)*travel*.55f-40*t);
                    effect(new Rect(point.x-15,point.y-15,30,30),"VFX_SUMMON_MOTE",new Color(1,1,1,(1-t)*.85f));
                }
                effect(new Rect(target.x-125,target.y-115,250,210),"VFX_SUMMON_BURST",new Color(1,1,1,(1-t)*.8f));
            }
            if(e.type=="PlayHeroPower"&&!Cues.Any(c=>c.data.type=="SpellEffect"&&c.data.source==e.target)||e.type=="Morph")
                effect(new Rect(target.x-125,target.y-125,250,250),"UI_CARD_GLOW",new Color(.6f,.7f,1,(1-t)*.7f));
            if(e.type=="PlayHeroPower"&&CardId(e.target)=="HERO_08bp"&&!Cues.Any(c=>c.data.type=="SpellEffect"&&c.data.source==e.target))
            {
                var hit=Cues.FirstOrDefault(c=>c.data.type=="Hit"&&c.data.source==e.target);
                if(hit!=null&&age<.38f)
                {
                    var point=Vector2.Lerp(target,Position(hit.data.target,locations),Tween.Curve((age-.08f)/.24f,Ease.In));
                    var profile=CardEffectRegistry.Find("HERO_08bp");
                    effect(new Rect(point.x-34,point.y-34,68,68),profile==null?"VFX_FIREBALL_BOLT":profile.projectile,new Color(1,.7f,.15f));
                }
            }
            if(e.type=="TURN_START"&&current.after.active==current.after.seat)
            {GUI.color=new Color(1,.88f,.5f,Mathf.Sin(t*Mathf.PI));text(new Rect(700,425,520,110),"你的回合");GUI.color=old;}
        }
        foreach(var decoration in damageDecorations)
        {
            Rect rect;
            if(locations.TryGetValue(decoration.target,out rect))decoration.lastPosition=rect.center;
            // Reference damage splats are already legible at impact; do not invent a one-second grow-in.
            float splatAge=decoration.age;float scale=1;float alpha=splatAge<3?1:Mathf.Clamp01(4-splatAge);
            float size=180*scale;var target=decoration.lastPosition;
            effect(new Rect(target.x-size/2,target.y-size/2,size,size),"e87287cfbd595e7ae7d1fe5f.png",new Color(1,1,1,alpha));
            float textWidth=176*scale,textHeight=132*scale;
            DamageSplatDraw?.Invoke(new Rect(target.x-textWidth/2,target.y-textHeight/2,textWidth,textHeight),decoration.value,alpha);
        }
        GUI.color=old;
    }
    void DrawAssociatedEffect(CardEffect spell,SemanticEvent e,float t,Vector2 origin,Vector2 target,Dictionary<int,Rect> locations,Action<Rect,string,Color> draw)
    {
        bool targeted=e.target!=0;
        var center=targeted?target:origin;
        if(spell.kind=="projectile"&&targeted)center=Vector2.Lerp(origin,target,Tween.Curve(t,Ease.In));
        if(spell.kind=="area")
        {
            // AOE 从中心扩散到实际受影响实体，目标集合来自权威事件而非客户端规则推断。
            float radius=Mathf.Lerp(40,580,Tween.Curve(t));
            foreach(var sprite in spell.visuals)
            {
                float r=radius*(!sprite.HasNative?1:sprite.native.Size(t));var matrix=GUI.matrix;
                if(sprite.HasNative)BattleGeometry.Rotate(sprite.native.Angle(t),origin);
                var tint=!sprite.HasNative?sprite.tint*Mathf.Sin(t*Mathf.PI):sprite.tint*sprite.native.Color(t);
                draw(new Rect(origin.x-r,origin.y-r*.38f,r*2,r*.76f),sprite.texture,tint);GUI.matrix=matrix;
            }
            return;
        }
        if(!targeted&&(spell.kind=="buff"||spell.kind=="heal"||spell.kind=="transform"))
        {
            var targets=Cues.Where(c=>c.data.type=="Hit"||c.data.type=="Heal"||c.data.type=="Buff"||c.data.type=="Morph").Select(c=>c.data.target).Distinct().ToArray();
            if(targets.Length>0){foreach(int id in targets)DrawEffectLayers(spell,Position(id,locations),t,draw);return;}
        }
        DrawEffectLayers(spell,center,t,draw);
    }
    static void DrawEffectLayers(CardEffect spell,Vector2 center,float t,Action<Rect,string,Color> draw)
    {
        int index=0;
        foreach(var sprite in spell.visuals)
        {
            float phase=Mathf.Clamp01((t-index*.055f)/.85f),size=Mathf.Lerp(70,spell.kind=="projectile"?140:280,Tween.Curve(phase));
            var matrix=GUI.matrix;
            var tint=sprite.tint*Mathf.Sin(phase*Mathf.PI);
            if(sprite.HasNative)
            {size*=sprite.native.Size(phase);tint=sprite.tint*sprite.native.Color(phase);BattleGeometry.Rotate(sprite.native.Angle(phase),center);}
            else if(index>0)BattleGeometry.Rotate((index%2==0?1:-1)*phase*80,center);
            draw(new Rect(center.x-size/2,center.y-size/2,size,size),sprite.texture,tint);
            GUI.matrix=matrix;index++;
        }
    }
}
