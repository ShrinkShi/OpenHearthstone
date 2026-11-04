using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

// 源状态时钟测试使用显式音源状态服务；实际DSP/录音另由正式Player验证。
public static class FireblastSelectionChecks
{
    static int passed;
    static void Check(bool condition,string message){if(!condition)throw new Exception("Fireblast selection: "+message);passed++;}
    static StateView State()
    {
        return new StateView{match_id="fireblast-selection",version=1,phase="play",seat=0,active=0,
            moves=new[]{new Move{kind="power",entity=2,target=3}},events=new SemanticEvent[0],players=new[]{
                new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="HERO_08"},power=new EntityView{entity=2,card_id="HERO_08bp"},hand=new EntityView[0],field=new EntityView[0]},
                new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="HERO_01"},power=new EntityView{entity=4,card_id="HERO_01bp"},hand=new EntityView[0],field=new EntityView[0]}}};
    }
    public sealed class Fixture:IDisposable
    {
        public readonly NativeEffectPlayer Player;public readonly StateView State=FireblastSelectionChecks.State();
        public readonly List<NativeAudioCue> Cues=new List<NativeAudioCue>();
        readonly NativeAudioPlayback audio;readonly GameObject audioRoot;float clock;public int Stops;
        public Fixture(AssetRegistry assets,bool sourceProjection=false)
        {
            audioRoot=new GameObject("Fireblast explicit audio state test");audio=new NativeAudioPlayback(assets.Root,audioRoot.transform,true);
            Player=new NativeEffectPlayer(assets){CardType=id=>id.EndsWith("bp")?10:3};
            Player.SuperSpellScene=(e,b,a,viewport,origin)=>{
                var sourceCamera=sourceProjection?JsonUtility.FromJson<NativeSourceCameras>(File.ReadAllText(Path.Combine(assets.Root,"original-battle-cameras.json"))).cameras.Single(c=>c.name=="Main Camera"):null;
                var scene=NativeSuperSpellBoard.FromPresentation(e,b,a,id=>id==1?BattleLayout.Hero(true).center:id==3?BattleLayout.Hero(false).center:BattleLayout.Power(id==2).center,viewport,origin,Player.CardType,sourceCamera);
                scene.Range=(min,max)=>min;scene.RandomBool=()=>true;return scene;};
            Player.AudioCue=cue=>{
                Cues.Add(cue);float start=clock;bool stopped=false;float length=audio.Load(cue.file).length/Mathf.Abs(cue.pitch);
                cue.playbackActive=()=>!stopped&&(cue.loop||clock<start+length);
                cue.stopPlayback=()=>{if(!stopped){stopped=true;Stops++;}};
            };
        }
        public void Selection(float time){for(;clock<time;){float step=Mathf.Min(1f/120,time-clock);clock+=step;Player.TickPowerSelection(step);}}
        public void CastToEnd(string key,float end,Action<NativeSuperSpellPlayback,float> sample=null)
        {
            var after=FireblastSelectionChecks.State();after.version=2;after.events=new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2},new SemanticEvent{type="Hit",source=2,target=3,amount=1}};
            var data=new SemanticEvent{type="SpellEffect",source=2,card_id="HERO_08bp",owner=0};
            Player.SubmitPowerSelection(2);Player.ObservePowerState(after);
            float start=clock;
            for(int frame=0;frame<=end*120;frame++)
            {
                clock=start+frame/120f;
                if(!Player.PrepareSource(key,data,State,after,false,frame/120f)||!Player.PrepareSource(key,data,State,after,false,frame/120f))throw new Exception("Authoritative same-frame Prepare/Draw sample failed");
                sample?.Invoke(Player.SourceSpell(key),frame/120f);
            }
        }
        public void Dispose(){Player.Dispose();audio.Dispose();AssetRegistry.ReleaseTransient(audioRoot);}
    }
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));CardEffectRegistry.Load(assets.Root);
        foreach(float wait in new[]{.05f,1.5f,3f})using(var fixture=new Fixture(assets))
        {
            Check(fixture.Player.SelectPower(fixture.State,2),"legal power creates selection");var root=fixture.Player.PowerSelection;var start=root.Flow.Instances.Single().Effect.Root;
            fixture.Selection(wait);Check(root.Flow.Flights.Count()==0&&!root.Flow.CastRequested&&!root.Flow.FirstArrivalTime.HasValue,"selection never flies or hits");
            Check(fixture.Cues.Count(c=>c.loop)==1,"one original precast loop during selection");
            fixture.CastToEnd("cast",7);var cast=fixture.Player.SourceSpell("cast");
            Check(System.Object.ReferenceEquals(root,cast)&&!fixture.Player.HasPowerSelection,"same instance transfers once");
            Check(cast.Flow.Completed&&cast.Flow.ActiveClones==0&&start==null,"cast releases physical source graph");
            Check(cast.Events.Count(e=>e.Contains(" Birth "))==1&&cast.Events.Count(e=>e.Contains(" Action "))==1&&cast.Events.Count(e=>e.Contains(" Arrival "))==1,"one root Birth/Action/Arrival");
            Check(fixture.Cues.Count(c=>c.loop)==1&&fixture.Cues.Count==6,"cast does not replay Birth audio");
            Check(cast.UnhandledActions==0&&cast.UnboundAudioActions==0&&cast.UnboundComponentActions==0&&cast.MissingAudioPlaybackServices==0,"actual invoked action/audio/component coverage");
            if(wait==.05f)Debug.Log("FIREBLAST ACTION COVERAGE "+String.Join(";",cast.ActionExecutions.OrderBy(p=>p.Key).Select(p=>p.Key+"="+p.Value)));
        }
        foreach(float wait in new[]{.01f,.6f,1.8f})using(var fixture=new Fixture(assets))
        {
            fixture.Player.SelectPower(fixture.State,2);var root=fixture.Player.PowerSelection;fixture.Selection(wait);
            fixture.Player.CancelPowerSelection(true);fixture.Player.CancelPowerSelection(true);
            Check(root.Flow.TargetingCancelled&&!root.Flow.CastRequested&&!root.Flow.Cancelled,"graceful cancellation differs from hard stop");
            fixture.Selection(wait+5);Check(!fixture.Player.HasPowerSelection&&root.Flow.ActiveClones==0,"cancel tail reaches real None and releases");
            Check(root.Flow.Flights.Count()==0&&!root.Flow.FirstArrivalTime.HasValue,"cancel does not cast or hit");
            Check(fixture.Cues.Count==3&&fixture.Cues.Count(c=>c.loop)==1&&fixture.Stops>=1,"cancel has Start/loop/fizzle once and stops source");
        }
        using(var fixture=new Fixture(assets))
        {
            Check(fixture.Player.Duration("HERO_08bp")>0,"formal Presentation enables reviewed native power without legacy cast_duration");
            var after=State();after.version=2;after.events=new[]{new SemanticEvent{type="PlayHeroPower",source=1,target=2,card_id=""},new SemanticEvent{type="Hit",source=2,target=3,amount=1}};
            var presentation=new Presentation{NativeDuration=fixture.Player.Duration};presentation.Accept(fixture.State,after,new Dictionary<int,Rect>());
            Check(presentation.Cues.Any(c=>c.data.type=="SpellEffect"&&c.data.card_id=="HERO_08bp"),"actual omitted card_id deserializes empty and resolves source power identity");
            fixture.Player.SelectPower(fixture.State,2);fixture.Selection(.2f);var pending=fixture.Player.PowerSelection;
            fixture.Player.SubmitPowerSelection(2);fixture.Player.CancelPowerSelection(true);
            Check(ReferenceEquals(pending,fixture.Player.PowerSelection)&&!pending.Flow.TargetingCancelled,"pending authority ownership survives late UI cancellation");
            fixture.Player.RejectPowerSelection();fixture.Player.ObservePowerState(fixture.State);
            Check(ReferenceEquals(pending,fixture.Player.PowerSelection)&&pending.Flow.TargetingCancelled,"unchanged rejection snapshot preserves original graceful tail");
            fixture.Selection(3);Check(!fixture.Player.HasPowerSelection,"rejected tail physically completes");
            fixture.Player.SelectPower(fixture.State,2);var other=State();other.match_id="another-match";var obsolete=fixture.Player.PowerSelection;
            fixture.Player.ObservePowerState(other);Check(!fixture.Player.HasPowerSelection&&obsolete.Flow.Cancelled,"different match hard releases obsolete selection");
        }
        using(var fixture=new Fixture(assets))
        {
            fixture.Player.SelectPower(fixture.State,2);fixture.Selection(1.5f);fixture.CastToEnd("rewind",.1f);
            var old=fixture.Player.SourceSpell("rewind");var roots=old.Flow.Instances.Select(c=>c.Effect.Root).ToArray();
            var after=State();after.version=2;after.events=new[]{new SemanticEvent{type="Hit",source=2,target=3,amount=1}};
            Check(fixture.Player.PrepareSource("rewind",new SemanticEvent{type="SpellEffect",source=2,card_id="HERO_08bp",owner=0},fixture.State,after,false,.01f),"strictly earlier presentation age replays source");
            Check(old.Flow.Cancelled&&roots.All(go=>go==null)&&!ReferenceEquals(old,fixture.Player.SourceSpell("rewind")),"true rewind releases active physical graph and creates new owner");
        }
        using(var fixture=new Fixture(assets))
        {
            fixture.Player.SelectPower(fixture.State,2);fixture.Selection(.2f);var root=fixture.Player.PowerSelection;int cues=fixture.Cues.Count;
            fixture.Player.CancelPowerSelection(false);fixture.Selection(4);
            Check(!fixture.Player.HasPowerSelection&&root.Flow.Cancelled&&fixture.Cues.Count==cues,"hard stop starts no late fizzle or delayed loop");
            Check(fixture.Player.SelectPower(fixture.State,2),"reselect after hard stop owns fresh graph");
        }
        Debug.Log("FIREBLAST SELECTION: "+passed+" checks passed; explicit source-audio state model, DSP/audio capture separate");
    }
}
