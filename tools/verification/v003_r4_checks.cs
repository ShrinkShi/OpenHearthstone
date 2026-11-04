using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class V003R4Checks
{
    [Serializable] sealed class Catalog { public NativeSuperSpellConfig[] programs; }
    static int passed;
    static readonly List<string> failures=new List<string>();
    static void Check(bool ok,string label)
    {if(ok){passed++;Debug.Log("V003_R4_PASS "+label);}else{failures.Add(label);Debug.LogError("V003_R4_FAIL "+label);}}
    static void Case(string label,Action action)
    {try{action();}catch(Exception error){failures.Add(label+": "+error);Debug.LogError("V003_R4_CASE "+label+" "+error);}}

    sealed class Lease : IEnumerator,IDisposable
    {
        public readonly GameObject Object=new GameObject("V003R4 transferred child lease");
        public readonly RenderTexture Texture=new RenderTexture(8,8,0);
        public int Moves,Disposals;
        public Lease(){Texture.Create();}
        public bool MoveNext(){return ++Moves==1;}
        public object Current{get{return null;}}
        public void Reset(){throw new NotSupportedException();}
        public void Dispose(){Disposals++;Texture.Release();AssetRegistry.ReleaseTransient(Texture);AssetRegistry.ReleaseTransient(Object);}
    }
    sealed class YieldLease : IEnumerator,IDisposable
    {
        readonly IEnumerator inner;readonly Lease child;readonly Action stop;
        public int Moves,Reads,Disposals;
        public YieldLease(IEnumerator inner,Lease child,Action stop){this.inner=inner;this.child=child;this.stop=stop;}
        public bool MoveNext(){Moves++;return inner==null?Moves==1:inner.MoveNext();}
        public object Current{get{Reads++;stop?.Invoke();return child;}}
        public void Reset(){throw new NotSupportedException();}
        // Current 返回的子迭代器由宿主接管，父层不递归处置。
        public void Dispose(){Disposals++;(inner as IDisposable)?.Dispose();}
    }
    static void SchedulerLateChild()
    {
        var child=new Lease();NativePresentationCoroutines clock=null;
        using(clock=new NativePresentationCoroutines(error=>{throw error;}))
        {
            var parent=new YieldLease(null,child,()=>clock.Dispose());
            try
            {
                clock.Start(parent);
                Check(parent.Moves==1&&parent.Reads==1&&parent.Disposals==1,"direct scheduler reaches Current stop and closes parent");
                Check(child.Moves==0&&child.Disposals==1&&child.Object==null&&child.Texture==null&&clock.Active==0,
                    "direct scheduler owns late returned child and physically releases its lease");
            }
            finally{if(child.Disposals==0)child.Dispose();}
        }
    }
    static void HistoryLateChild(AssetRegistry assets,Catalog catalog,bool dispose,bool stop)
    {
        var root=new GameObject("V003R4 actual root");var source=new GameObject("V003R4 source");var target=new GameObject("V003R4 target");
        var child=new Lease();NativeSuperSpell flow=null;YieldLease parent=null;int creates=0,tasks=0,waitingAtRead=-1;bool allow=false;
        var input=new NativePowerTaskList{tasks=new[]{
            new NativePowerHistoryTask{type=4,entity=20,tag=49,tagChange=true,payloadType="HistTagChange"},
            new NativePowerHistoryTask{type=8,metaType=0,metaData=0,payloadType="HistMetaData"},
            new NativePowerHistoryTask{type=4,entity=20,tag=44,tagChange=true,payloadType="HistTagChange"}}};
        var services=new NativePowerServices{GameplayPresent=true,CanDoTask=task=>allow,MulliganBusy=()=>false,EntityType=id=>4,
            BeginZoneChanges=(list,start,end,callback)=>null,TimelineEnd=(id,time,end)=>{}};
        services.handlers[4]=power=>{tasks++;return true;};services.handlers[8]=power=>true;
        var host=new NativeSuperSpellTaskHost(input,services,new Dictionary<int,int>{{0,7}},new Dictionary<int,int>{{7,1}},
            new NativeSuperSpellZoneWait{VisualEntities=new HashSet<int>{20},Entity=task=>20,InDestinationZone=task=>true,ActorLoading=task=>false});
        var history=host.Bind();var until=history.CompleteUntil;
        history.CompleteUntil=index=>parent=new YieldLease(until(index),child,()=>{
            waitingAtRead=host.Execution.WaitingRequests;
            if(stop){if(dispose)flow.Dispose();else flow.Cancel();}});
        flow=new NativeSuperSpell(catalog.programs.Single(p=>p.card_id=="EX1_134"),new NativeSuperSpellServices{
            Root=root,Source=()=>source,Targets=new List<GameObject>{target},Friendly=true,History=history,Range=(a,b)=>a,
            Create=reference=>{creates++;throw new Exception("unexpected real missile during blocked task");},Release=clone=>clone.Dispose()});
        try
        {
            flow.Begin();for(int frame=1;frame<=5&&!flow.Cancelled&&parent==null;frame++)flow.Tick(frame/120f);
            string label="history Current child stop="+stop+" dispose="+dispose;
            Check(parent!=null&&parent.Moves==1&&parent.Reads==1&&waitingAtRead==1&&tasks==0&&creates==0,"real Until task is blocked before child delivery "+label);
            if(!stop)
            {
                Check(child.Moves==1&&host.Execution.WaitingRequests==1&&!flow.Cancelled,"normal adapter actually transfers child while true Host waits");
                flow.Cancel();
            }
            Debug.Log("V003_R4_LATE_CHILD stop="+stop+" dispose="+dispose+" host_waiting_at_read="+waitingAtRead+" child_moves="+child.Moves+" child_dispose="+child.Disposals+
                " parent_dispose="+parent.Disposals+" object_alive="+(child.Object!=null)+" rt_alive="+(child.Texture!=null)+
                " host_waiting="+host.Execution.WaitingRequests+" metadata_pending="+typeof(NativeSuperSpell).GetField("metadataPending",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(flow));
            Check(parent.Disposals==1&&flow.Cancelled&&!flow.Faulted&&!flow.Completed&&host.Execution.WaitingRequests==0,
                "parent and real Host stop cleanly without success "+label);
            Check(child.Disposals==1&&child.Object==null&&child.Texture==null&&(!stop||child.Moves==0),
                "returned nested child is disposed without execution after stop "+label);
            flow.Cancel();flow.Dispose();
            Check(child.Disposals==1&&child.Object==null&&child.Texture==null,"explicit repeated stop leaves no transferred child lease "+label);
        }
        finally
        {
            flow.Cancel();flow.Dispose();if(child.Disposals==0)child.Dispose();
            AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(source);AssetRegistry.ReleaseTransient(target);
        }
    }
    static void RuleVariants(AssetRegistry assets)
    {
        var types=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json")).cards.ToDictionary(c=>c.CardId,c=>c.Type);
        foreach(string name in new[]{"fan-empty","fan-lethal","agent-no-combo","agent-friendly-lethal"})foreach(int seat in new[]{0,1})
        using(var player=new NativeEffectPlayer(assets))
        {
            string folder="../work/v003-r4-rule-variants/";
            var before=JsonUtility.FromJson<StateView>(File.ReadAllText(folder+name+"-"+seat+"-before.json"));
            var after=JsonUtility.FromJson<StateView>(File.ReadAllText(folder+name+"-"+seat+"-after.json"));
            string card=name.StartsWith("fan")?"EX1_129":"EX1_134";
            var play=after.events.Single(e=>e.type=="CardPlay"&&e.card_id==card);
            var data=new SemanticEvent{type="SpellEffect",source=play.source,target=play.target,card_id=card,owner=play.owner};
            var hits=after.events.Where(e=>e.type=="Hit"&&e.source==play.source).Select(e=>e.target).Distinct().ToArray();
            var points=new Dictionary<int,Vector2>{{play.source,new Vector2(960,480)}};
            foreach(var owner in before.players)
            {
                bool own=owner.seat==seat;points[owner.hero.entity]=BattleLayout.Hero(own).center;points[owner.power.entity]=BattleLayout.Power(own).center;
                for(int i=0;i<owner.field.Length;i++)points[owner.field[i].entity]=BattleLayout.Minion(own,i,owner.field.Length).center;
            }
            player.CardType=id=>types.ContainsKey(id)?types[id]:0;
            player.SuperSpellScene=(evt,b,a,viewport,origin)=>{
                var scene=NativeSuperSpellBoard.FromPresentation(evt,b,a,id=>points.ContainsKey(id)?points[id]:(Vector2?)null,viewport,origin,player.CardType);
                scene.Range=(min,max)=>min;scene.RandomBool=()=>true;return scene;};
            string label=name+"/"+seat;
            Check(player.PrepareSource(label,data,before,after,false,0),"formal player accepts legal Match.apply variation "+label);
            var spell=player.SourceSpell(label);
            for(int frame=1;frame<=960;frame++)if(!player.PrepareSource(label,data,before,after,false,frame/120f))throw new Exception("formal forward sample rejected "+label);
            int expected=name=="fan-lethal"?2:name=="agent-friendly-lethal"?1:0;
            Check(hits.Length==expected&&spell.Flow.Flights.Count()==expected&&spell.Events.Count(line=>line.Contains(" Arrival "))==expected,
                "formal targets follow authoritative Hit set, including empty and departed targets "+label);
            Check(spell.Flow.Completed&&!spell.Flow.Faulted&&spell.Flow.Pending==0&&spell.Flow.ActiveClones==0&&spell.UnboundControllerActions==0,
                "formal variation completes with no physical clones or unbound actions "+label);
            if(name.EndsWith("lethal"))Check(hits.All(id=>!after.players.Any(p=>p.field.Any(e=>e.entity==id))),"actual hit targets departed authoritative board "+label);
            Debug.Log("V003_R4_RULE name="+name+" seat="+seat+" hits="+String.Join(",",hits)+" flights="+spell.Flow.Flights.Count()+" finished="+spell.Flow.FinishedNotifications+" active="+spell.Flow.ActiveClones);
        }
    }
    public static void Run()
    {
        passed=0;failures.Clear();var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        Case("scheduler late child",SchedulerLateChild);
        Case("normal history nested child",()=>HistoryLateChild(assets,catalog,false,false));
        foreach(bool dispose in new[]{false,true})Case("stopped history nested child "+dispose,()=>HistoryLateChild(assets,catalog,dispose,true));
        Case("legal rule variations",()=>RuleVariants(assets));
        Debug.Log("V003_R4_RESULT passed="+passed+" failures="+failures.Count);
        if(failures.Count>0)throw new Exception("Independent V003-R4 failures: "+String.Join(" | ",failures));
    }
}
