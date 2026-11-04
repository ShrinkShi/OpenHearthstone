using System;
using System.IO;
using System.Linq;
using UnityEngine;

// V005-r1独立边界探针；仅临时复制至Editor运行。
public static class V005R1Checks
{
    static int checks;
    static void Check(bool value,string label){if(!value)throw new Exception("V005_R1_FAIL "+label);checks++;Debug.Log("V005_R1_PASS "+label);}
    static StateView State(string match="v005-independent")
    {
        return new StateView{match_id=match,version=8,phase="play",seat=0,active=0,
            events=new SemanticEvent[0],moves=new[]{new Move{kind="power",entity=2,target=3}},players=new[]{
            new PlayerView{seat=0,hero=new EntityView{entity=1,card_id="HERO_08"},power=new EntityView{entity=2,card_id="HERO_08bp"},hand=new EntityView[0],field=new EntityView[0]},
            new PlayerView{seat=1,hero=new EntityView{entity=3,card_id="HERO_08"},power=new EntityView{entity=4,card_id="HERO_08bp"},hand=new EntityView[0],field=new EntityView[0]}}};
    }
    static NativeEffectPlayer Player(AssetRegistry assets)
    {
        var p=new NativeEffectPlayer(assets){CardType=id=>id.EndsWith("bp")?10:3};
        p.SuperSpellScene=(e,b,a,viewport,origin)=>NativeSuperSpellBoard.FromPresentation(e,b,a,
            id=>id==1?BattleLayout.Hero(true).center:id==3?BattleLayout.Hero(false).center:BattleLayout.Power(id==2).center,viewport,origin,p.CardType);
        return p;
    }
    static int Objects(){return Resources.FindObjectsOfTypeAll<GameObject>().Length;}
    static int Targets(){return Resources.FindObjectsOfTypeAll<RenderTexture>().Length;}
    public static void Run()
    {
        checks=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));CardEffectRegistry.Load(assets.Root);
        using(var p=Player(assets))
        {
            var invalid=State();invalid.moves=new Move[0];
            Check(!p.SelectPower(invalid,2)&&!p.HasPowerSelection,"authority without power moves creates no root");
            Check(!p.SelectPower(State(),4)&&!p.HasPowerSelection,"enemy same CardID cannot select without authority");
            Check(p.SelectPower(State(),2),"real local source selected");var first=p.PowerSelection;
            Check(p.SelectPower(State(),2)&&ReferenceEquals(first,p.PowerSelection),"repeated selection reuses current owner");
            p.TickPowerSelection(.1f);float now=first.Time;p.TickPowerSelection(-4);
            Check(first.Time==now&&!first.Flow.CastRequested,"negative delta cannot rewind or cast");
            var nodes=first.Flow.Instances.Select(i=>i.Effect.Root).ToArray();
            var terminal=State();terminal.phase="finished";p.ObservePowerState(terminal);
            Check(!p.HasPowerSelection&&first.Flow.Cancelled&&nodes.All(n=>n==null),"terminal snapshot physically releases pending selection");
            p.CancelPowerSelection(false);p.RejectPowerSelection();p.TickPowerSelection(10);
            Check(!p.HasPowerSelection,"repeated cleanup stays empty");
            p.SelectPower(State(),2);var waiting=p.PowerSelection;p.SubmitPowerSelection(2);p.CancelPowerSelection(true);
            Check(ReferenceEquals(waiting,p.PowerSelection)&&!waiting.Flow.TargetingCancelled,"late graceful UI cancel cannot steal submitted owner");
            p.ObservePowerState(State("new-match"));
            Check(!p.HasPowerSelection&&waiting.Flow.Cancelled,"different match cannot adopt submitted old root");
        }
        int objects=Objects(),targets=Targets();
        using(var p=Player(assets))
        {
            p.AudioCue=cue=>{throw new IOException("V005 injected audio bind failure");};bool caught=false;
            p.SelectPower(State(),2);var root=p.PowerSelection;
            try{p.TickPowerSelection(.2f);}catch(IOException){caught=true;}
            Check(caught&&root.Flow.Faulted,"actual clock callback failure faults source flow");
            p.CancelPowerSelection(false);
        }
        Check(Objects()==objects&&Targets()==targets,"faulted audio clock hard cleanup releases original objects and RT");
        using(var p=Player(assets))
        {
            p.SelectPower(State(),2);var root=p.PowerSelection;var nodes=root.Flow.Instances.Select(i=>i.Effect.Root).ToArray();
            p.Dispose();p.Dispose();
            Check(root.Flow.Cancelled&&nodes.All(n=>n==null),"repeated player Dispose releases actual source clones");
        }
        using(var arrow=new NativeTargetArrow(assets))
        {
            arrow.Show(new Vector2(1210,860),new Vector2(960,250),false);
            Check(!arrow.BullseyeVisible&&arrow.VisibleLinks>0,"illegal target keeps independent original arrow body");
            arrow.Hide();arrow.Show(new Vector2(1210,860),new Vector2(1210,860),true);
            Check(arrow.VisibleLinks==0&&arrow.BullseyeVisible,"zero distance after hide remains finite");
        }
        Check(Objects()==objects&&Targets()==targets,"independent arrow and source cases release all GameObjects and RT");
        var shader=new Material(Resources.Load<Shader>("NativeEffect"));
        try
        {
            var diffuse=new NativeDiffuseInputs();diffuse.Apply(shader);
            Check(shader.GetFloat("_DiffuseAdditionalCount")==0&&shader.GetVector("_DiffuseMainColor")==Vector4.zero,"production explicit default lights are zero");
            bool rejected=false;try{new NativeDiffuseInputs{globalCount=1,rendererIndices=new[]{3}}.Apply(shader);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"invalid renderer light index is rejected");
        }
        finally{AssetRegistry.ReleaseTransient(shader);}
        Debug.Log("V005_R1_INDEPENDENT "+checks+" checks passed; controlled boundary fixtures, not original runtime inputs");
    }
    public static void All()
    {
        Run();FireblastShaderChecks.Run();FireblastParticleChecks.Run();FireblastCacheChecks.Run();FireblastRegressionChecks.Run();
    }
}
