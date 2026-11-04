using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public static class FireblastChecks
{
    public static void Run()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        CardEffectRegistry.Load(assets.Root);
        var state=new StateView{match_id="fireblast-check",phase="play",seat=0,players=new[]{
            new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="HERO_08"},power=new EntityView{entity=2,card_id="HERO_08bp"},field=new EntityView[0],hand=new EntityView[0]},
            new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="HERO_01"},power=new EntityView{entity=4,card_id="HERO_01bp"},field=new EntityView[0],hand=new EntityView[0]}}};
        state.events=new[]{new SemanticEvent{type="Hit",source=2,target=3,amount=1}};
        var data=new SemanticEvent{type="SpellEffect",source=2,target=3,card_id="HERO_08bp",owner=0};
        using(var player=new NativeEffectPlayer(assets))
        {
            player.CardType=id=>id.EndsWith("bp")?10:3;
            player.SuperSpellScene=(e,b,a,viewport,origin)=>{
                var scene=NativeSuperSpellBoard.FromPresentation(e,b,a,id=>id==1?BattleLayout.Hero(true).center:id==3?BattleLayout.Hero(false).center:BattleLayout.Power(id==2).center,viewport,origin,player.CardType);
                scene.Range=(min,max)=>min;scene.RandomBool=()=>true;return scene;};
            player.AudioCue=cue=>Debug.Log("FIREBLAST AUDIO "+cue.file+" at="+cue.time+" volume="+cue.volume+" pitch="+cue.pitch);
            if(!player.PrepareSource("fireblast",data,state,state,false,0))throw new Exception("Fireblast formal preparation failed");
            var spell=player.SourceSpell("fireblast");
            var folder=Path.GetFullPath("../work/f005-gpu");Directory.CreateDirectory(folder);
            var objects=new HashSet<GameObject>();
            for(int frame=1;frame<=1200;frame++)
            {
                float time=frame/120f;
                if(!player.PrepareSource("fireblast",data,state,state,false,time))throw new Exception("Fireblast forward sample failed "+time);
                foreach(var clone in spell.Flow.Instances)
                {
                    objects.Add(clone.Effect.Root);
                    if(frame%60==0)Debug.Log("FIREBLAST CLONE "+clone.Effect.Program.reference+" state="+clone.Endpoint.ActiveState()+" fsm="+String.Join(",",clone.Effect.CurrentStates)+" audio="+clone.Effect.UnboundAudioActions+" property="+clone.Effect.UnboundPropertyActions+" component="+clone.Effect.UnboundComponentActions+" ignored="+clone.Effect.UnhandledActions);
                }
                if(new[]{1,6,18,36,60,120,240,600}.Contains(frame))
                {
                    var texture=spell.RenderAffine(time);var previous=RenderTexture.active;var image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true);
                    try{RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,frame+".png"),image.EncodeToPNG());}
                    finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
                }
            }
            File.WriteAllLines(Path.Combine(folder,"events.txt"),spell.Events);
            Debug.Log("FIREBLAST RESULT pending="+spell.Flow.Pending+" clones="+spell.Flow.ActiveClones+" completed="+spell.Flow.Completed+" unbound="+spell.UnboundControllerActions+" objects="+objects.Count);
            if(spell.Flow.Pending!=0||spell.Flow.ActiveClones!=0||!spell.Flow.Completed||spell.UnboundControllerActions!=0)throw new Exception("Fireblast lifecycle incomplete");
            if(objects.Any(o=>o!=null))throw new Exception("Fireblast physical clone retained");
        }
        Debug.Log("FIREBLAST CHECKS source lifecycle passed; official fidelity unverified");
    }
}
