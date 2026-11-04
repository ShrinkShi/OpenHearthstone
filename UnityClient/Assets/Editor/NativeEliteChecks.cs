using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeEliteChecks
{
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original Actor Elite: "+message);passed++;};
        foreach(bool definition in new[]{false,true})foreach(int elite in new[]{-1,0,1,2})foreach(int type in new[]{4,23})foreach(int mercenary in new[]{-1,0,1})
        {
            var go=new GameObject("SourceEliteProbe");var actor=go.AddComponent<NativeActorIdentity>();
            try
            {
                actor.IsDiamondViewer=true;actor.PremiumType=3;
                actor.ConfigureEntityTags(type,elite,mercenary);
                if(definition)actor.ConfigureDefinitionTags(type,elite,mercenary);
                check(actor.IsElite()==(type!=23&&mercenary<=0&&elite>0),"original tag predicates and entity precedence");
                if(definition){actor.ConfigureEntityTags(4,99,0);check(actor.IsElite()==(type!=23&&mercenary<=0&&elite>0),"EntityDef overrides Entity and diamond viewer");}
            }
            finally{AssetRegistry.ReleaseTransient(go);}
        }
        var empty=new GameObject("NoEntityProbe");var plain=empty.AddComponent<NativeActorIdentity>();
        try{check(!plain.IsElite(),"no entity and no viewer false");plain.PremiumType=3;check(!plain.IsElite(),"diamond premium alone is not diamond viewer");plain.IsDiamondViewer=true;check(plain.IsElite(),"separate diamond viewer fallback");}
        finally{AssetRegistry.ReleaseTransient(empty);}
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var db=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        try
        {
            foreach(string file in new[]{"2ac223d9b583771c9eb24338","b0cbd0aa93fedbfc28ee3f8e"})
            foreach(string card in new[]{"CS2_179","EX1_116"})
            using(var live=visuals.CreateLiveActor(db.cards.First(c=>c.CardId==card),"minion",true))
            {
                var definition=db.cards.First(c=>c.CardId==card);
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
                var init=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="InitializeCustomSpawn");
                var elite=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="ActorEliteEventAction");
                var machine=program.machines[0];machine.start="Begin";machine.events=new NativeTransition[0];machine.states=new[]{
                    new NativeState{name="Begin",actions=new[]{init},transitions=new[]{new NativeTransition{@event="FINISHED",state="Check"}}},
                    new NativeState{name="Check",actions=new[]{elite},transitions=new[]{new NativeTransition{@event="On",state="Elite"},new NativeTransition{@event="Off",state="Normal"},new NativeTransition{@event="FINISHED",state="Missing"}}},
                    new NativeState{name="Elite",actions=new NativeAction[0],transitions=new NativeTransition[0]},
                    new NativeState{name="Normal",actions=new NativeAction[0],transitions=new NativeTransition[0]},
                    new NativeState{name="Missing",actions=new NativeAction[0],transitions=new NativeTransition[0]}};
                program.machines=new[]{machine};var parent=live.ActorObject.transform.parent;
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {
                    effect.AttachToOriginalParent(live.ActorObject);effect.Seek(.02f);
                    check(effect.CurrentStates[0]==(definition.EliteTag>0?"Elite":"Normal"),"real Actor original variable routes source event "+card);
                    check(effect.EliteEvents==1&&effect.UnboundEliteActions==0,"source event delivered once");
                    effect.Seek(0);effect.Seek(.02f);check(effect.EliteEvents==1,"rewind rebuilds event route");
                }
                check(live.ActorObject.transform.parent==parent,"borrowed Actor released");
                // With no Actor, the source action finishes without routing On/Off.
                machine.start="Check";
                using(var missing=new NativeEffectInstance(program,assets,0,32,32))
                {missing.Seek(.02f);check(missing.CurrentStates[0]=="Missing"&&missing.EliteEvents==0,"missing Actor uses FINISHED only");}
            }
        }
        finally{visuals.Clear();}
        Debug.Log("Original Actor Elite checks passed: "+passed);
    }
}
