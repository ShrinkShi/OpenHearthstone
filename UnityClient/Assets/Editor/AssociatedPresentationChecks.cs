using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class AssociatedPresentationChecks
{
    static int passed;
    static void Check(bool value,string label){if(!value)throw new Exception("Associated presentation: "+label);passed++;}
    public static void Run()
    {
        passed=0;string root=Path.GetFullPath("../local/runtime");CardEffectRegistry.Load(root);
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var index=JsonUtility.FromJson<CardEffectIndex>(File.ReadAllText(Path.Combine(root,"effect-index.json")));
        foreach(var profile in index.entries)
        {
            foreach(var sprite in profile.visuals??new CardEffectSprite[0])
            {
                Check(File.Exists(assets.PathFor(sprite.texture)),"source texture is cached: "+profile.card_id);
                Check(sprite.tint.a>0&&Mathf.Max(sprite.tint.r,Mathf.Max(sprite.tint.g,sprite.tint.b))>0,"invisible runtime-only material is excluded");
            }
        }
        foreach(string id in CardEffectRegistry.AudioIds)Check(File.Exists(assets.PathFor(id)),"associated audio is cached: "+id);
        foreach(string name in new[]{"arcane_explosion","blizzard","flamestrike","polymorph","holy_nova","nightblade","si7_agent","big_game_hunter"})
        {
            var before=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/"+name+".json"));
            var after=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/"+name+"-after.json"));
            var locations=new Dictionary<int,Rect>();
            foreach(var player in before.players){locations[player.hero.entity]=BattleLayout.Hero(player.seat==before.seat);for(int i=0;i<player.field.Length;i++)locations[player.field[i].entity]=BattleLayout.Minion(player.seat==before.seat,i,player.field.Length);}
            var presentation=new Presentation();int history=0;presentation.BatchCompleted=(a,b)=>history++;
            presentation.Accept(before,after,locations);
            var card=presentation.Cues.First(c=>c.data.type=="CardPlay");var effect=presentation.Cues.First(c=>c.data.type=="SpellEffect");
            Check(effect.start>=card.start+card.duration,"original effect follows card reveal: "+name);
            Check(effect.duration>=.5f,"effect has a visible interval: "+name);
            Check(presentation.SampleTime(name)>effect.start&&presentation.SampleTime(name)<effect.start+effect.duration,"visual capture actually samples effect: "+name);
            Check(presentation.Cues.Where(c=>c.data.type=="Hit"||c.data.type=="Morph"||c.data.type=="Buff"||c.data.type=="Death").All(c=>c.start+.0001f>=effect.start+effect.duration),"impact waits for cast: "+name+" "+String.Join(",",presentation.Cues.Select(c=>c.data.type+"@"+c.start))+" effectEnd="+(effect.start+effect.duration));
            if(card.data.card_type!=5)Check(presentation.Cues.First(c=>c.data.type=="Summon").start<effect.start,"battlecry source appears before its effect: "+name);
            presentation.Seek(effect.start+.1f);Check(history==0,"history is deferred: "+name);
            presentation.Skip();Check(presentation.Display.public_hash==after.public_hash&&history==1,"skip converges and commits once: "+name);
        }
        File.WriteAllText("../work/associated-presentation-checks.json","{\"passed\":"+passed+"}");
        Debug.Log("ASSOCIATED PRESENTATION CHECKS PASSED: "+passed);
    }
}
