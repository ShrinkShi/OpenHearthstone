using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// 独立验收入口，退出前移至 tools/verification，不能进入正式构建。
public static class V003R1Checks
{
    [Serializable] sealed class Asset { public string reference,file; }
    [Serializable] sealed class Catalog { public NativeSuperSpellConfig[] programs; public Asset[] prefabs; }
    static readonly List<string> failures=new List<string>();
    static int passed;
    static void Check(bool value,string label)
    {
        if(value){passed++;Debug.Log("V003_R1_PASS "+label);}
        else{failures.Add(label);Debug.LogError("V003_R1_FAIL "+label);}
    }
    static void Case(string name,Action body)
    {try{body();}catch(Exception error){failures.Add(name+": "+error);Debug.LogError("V003_R1_CASE "+name+" "+error);}}
    public static void Run()
    {
        failures.Clear();passed=0;
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        Case("area factory cancellation",()=>FactoryCancel(assets,"EX1_129"));
        Case("missile factory cancellation",()=>FactoryCancel(assets,"EX1_134"));
        Case("formal presentation lifetime",()=>FormalPresentation(assets));
        Case("formal history refusal",()=>HistoryRefusal(assets));
        Debug.Log("V003_R1_RESULT passed="+passed+" failures="+failures.Count);
        if(failures.Count>0)throw new Exception("Independent V003-R1 failures: "+String.Join(" | ",failures));
    }
    static void FactoryCancel(AssetRegistry assets,string card)
    {
        var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-spell.json")));
        var config=catalog.programs.Single(p=>p.card_id==card);
        var root=new GameObject("V003 root");root.transform.position=new Vector3(20000,10000,20000);
        var source=new GameObject("V003 source");source.transform.position=root.transform.position;
        var target=new GameObject("V003 target");target.transform.position=root.transform.position+new Vector3(3,0,4);
        NativeSuperSpell flow=null;NativeAreaInstance created=null;
        int sources=0,activations=0,releases=0;RenderTexture[] outputTextures=null;
        try
        {
            flow=new NativeSuperSpell(config,new NativeSuperSpellServices {
                Root=root,Source=()=>source,Targets=new List<GameObject>{target},Friendly=true,
                Range=(a,b)=>a,RandomBool=()=>true,
                Create=reference=> {
                    var file=catalog.prefabs.Single(p=>p.reference==reference).file;
                    var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file)));
                    created=new NativeAreaInstance(program,assets,42,locations:kind=>source,facings:kind=>target,
                        context:new NativeSpellContext(5,4,lifecycle:"None",actorObjects:role=>role==1?target:source),startAtFsmEntry:true);
                    outputTextures=created.Effect.Root.GetComponentsInChildren<Camera>(true).Select(camera=>camera.targetTexture).Where(texture=>texture!=null).ToArray();
                    var setSource=created.Endpoint.SetSource;created.Endpoint.SetSource=obj=>{sources++;setSource(obj);};
                    var activate=created.Endpoint.Activate;created.Endpoint.Activate=()=>{activations++;activate();};
                    // 模拟取消从宿主工厂回入，返回的实例此时尚未被根登记。
                    flow.Cancel();return created;
                },
                Release=clone=>{releases++;clone.Dispose();}
            });
            flow.Begin();if(card=="EX1_134")flow.Tick(.01f);
            Check(created!=null&&flow.Cancelled,"factory cancellation is actually reached "+card);
            Debug.Log("V003_R1_FACTORY "+card+" active="+flow.ActiveClones+" pending="+flow.Pending+" source_writes="+sources+" activations="+activations+" releases="+releases+" object_alive="+(created!=null&&created.Effect.Root!=null));
            Check(flow.ActiveClones==0&&created.Effect.Root==null&&releases==1,"cancelled factory return physically released before public call returns "+card);
            Check(sources==0&&activations==0,"cancelled factory return never configured or activated "+card);
            Check(!flow.Completed&&flow.FinishedNotifications==0,"factory cancellation never manufactures success "+card);
            flow.Tick(8);
            Debug.Log("V003_R1_FACTORY_AFTER_TICK "+card+" active="+flow.ActiveClones+" rt_total="+outputTextures.Length+" rt_alive="+outputTextures.Count(texture=>texture!=null));
            Check(flow.ActiveClones==0&&outputTextures.Length>0&&outputTextures.All(texture=>texture==null),"cancelled normal sampling retains no GPU resource "+card);
            flow.Cancel();
            Check(flow.ActiveClones==0&&created.Effect.Root==null,"explicit repeated cancellation remains a recovery path "+card);
        }
        finally{flow?.Dispose();created?.Dispose();AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(source);AssetRegistry.ReleaseTransient(target);}
    }
    static Dictionary<int,Rect> Locations(StateView state)
    {
        var result=new Dictionary<int,Rect>();
        foreach(var player in state.players)
        {
            bool own=player.seat==state.seat;result[player.hero.entity]=BattleLayout.Hero(own);result[player.power.entity]=BattleLayout.Power(own);
            for(int i=0;i<player.field.Length;i++)result[player.field[i].entity]=BattleLayout.Minion(own,i,player.field.Length);
            for(int i=0;i<player.hand.Length;i++)result[player.hand[i].entity]=new Rect(800+i*40,900,80,110);
        }
        return result;
    }
    static void FormalPresentation(AssetRegistry assets)
    {
        CardEffectRegistry.Load(assets.Root);
        var types=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json")).cards.ToDictionary(c=>c.CardId,c=>c.Type);
        foreach(string card in new[]{"EX1_129","EX1_134"})foreach(int seat in new[]{0,1})
        using(var player=new NativeEffectPlayer(assets))
        {
            var before=JsonUtility.FromJson<StateView>(File.ReadAllText("../work/f003-rule-scenes/"+card+"-"+seat+"-before.json"));
            var after=JsonUtility.FromJson<StateView>(File.ReadAllText("../work/f003-rule-scenes/"+card+"-"+seat+"-after.json"));
            var locations=Locations(before);var presentation=new Presentation();
            player.CardType=id=>types.ContainsKey(id)?types[id]:0;
            player.SuperSpellScene=(data,b,a,viewport,origin)=> {
                var scene=NativeSuperSpellBoard.FromPresentation(data,b,a,
                    id=>id==data.source?(Vector2?)new Vector2(960,480):presentation.NativePosition(id,locations),viewport,origin,player.CardType);
                scene.Range=(min,max)=>min;scene.RandomBool=()=>true;return scene;
            };
            // 缩短宿主声明的区间，独立触发 NativeActive 延长分支；原图与时钟不变。
            presentation.NativeDuration=id=>.1f;presentation.NativeImpactDelay=player.ImpactDelay;
            presentation.NativePrepare=(key,data,b,a,area,age)=>player.PrepareSource(key,data,b,a,area,age);
            presentation.NativeActive=player.SourceBusy;
            presentation.Reset(before);presentation.Accept(before,after,locations);
            NativeSuperSpellPlayback spell=null;bool heldPastProfile=false,sampled=false;
            GameObject[] cloneObjects=null;RenderTexture[] cloneTextures=null;
            for(int frame=1;frame<2400&&presentation.Busy;frame++)
            {
                presentation.Tick(1/120f,locations);presentation.PrepareNative();
                foreach(var key in presentation.NativeKeys()) {
                    spell=player.SourceSpell(key);if(spell==null)continue;
                    if(spell.Time>.1f&&spell.Flow.ActiveClones>0)heldPastProfile=true;
                    if(spell.Flow.Instances.Any()&&cloneObjects==null) {
                        cloneObjects=spell.Flow.Instances.Select(c=>c.Effect.Root).ToArray();
                        cloneTextures=spell.Flow.Instances.SelectMany(c=>c.Effect.Root.GetComponentsInChildren<Camera>(true)).Select(c=>c.targetTexture).Where(t=>t!=null).ToArray();
                    }
                    if(!sampled) {
                        int count=spell.Events.Count;spell.Seek(spell.Time);spell.RenderAffine(spell.Time);
                        Check(spell.Events.Count==count,"same age render does not redrive callbacks "+card+seat);sampled=true;
                    }
                }
            }
            Check(spell!=null&&spell.Flow.Completed&&spell.Flow.Pending==0&&!presentation.Busy,"formal Presentation actual batch drains "+card+seat);
            Check(heldPastProfile&&spell.Flow.ActiveClones==0,"formal batch retains actual None lifetime beyond shortened interval "+card+seat);
            Check(cloneObjects!=null&&cloneObjects.All(o=>o==null)&&cloneTextures.Length>0&&cloneTextures.All(t=>t==null),"formal batch physically releases recorded clone and RT "+card+seat);
            player.Dispose();Check(spell.Effect.Root==null,"formal player Dispose releases root "+card+seat);
        }
    }
    static void HistoryRefusal(AssetRegistry assets)
    {
        var before=JsonUtility.FromJson<StateView>(File.ReadAllText("../work/f003-rule-scenes/EX1_134-0-before.json"));
        var after=JsonUtility.FromJson<StateView>(File.ReadAllText("../work/f003-rule-scenes/EX1_134-0-after.json"));
        var play=after.events.First(e=>e.type=="CardPlay"&&e.card_id=="EX1_134");
        var data=new SemanticEvent{type="SpellEffect",source=play.source,target=play.target,card_id=play.card_id,owner=play.owner};
        var objects=new List<GameObject>();var actorNodes=new Dictionary<int,GameObject>();
        using(var player=new NativeEffectPlayer(assets)) {
            player.CardType=id=>id=="EX1_134"?5:3;
            player.ActorObject=entity=> {
                GameObject obj;if(!actorNodes.TryGetValue(entity.entity,out obj))actorNodes[entity.entity]=obj=new GameObject("V003 history entity "+entity.entity);
                return obj;
            };
            player.OriginalPowerTasks=(e,b,a)=>new NativePowerTaskList{hasBlockStart=true,sourceEntities=new[]{e.source},endOfBlock=true};
            player.SuperSpellScene=(e,b,a,viewport,origin)=> {
                var scene=NativeSuperSpellBoard.FromPresentation(e,b,a,id=>new Vector2(960,480),viewport,origin,player.CardType);
                objects.Add(scene.Placement.source.Object);objects.AddRange(scene.Placement.Targets.GetTargets());return scene;
            };
            Check(!player.PrepareSource("explicit-missing-host",data,before,after,false,0)&&player.ActiveCount==0,"formal explicit original tasks refuse a missing metadata host");
            Check(objects.Count>0&&objects.All(o=>o==null),"formal refused scene releases its projection entities");
        }
        foreach(var node in actorNodes.Values)AssetRegistry.ReleaseTransient(node);
    }
}
