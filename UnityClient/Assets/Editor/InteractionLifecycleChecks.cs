using System;
using UnityEngine;

public static class InteractionLifecycleChecks
{
    static int passed;
    static void Check(bool ok,string message){if(!ok)throw new Exception("Interaction lifecycle: "+message);passed++;}
    static bool Near(Vector2 a,Vector2 b){return Vector2.Distance(a,b)<.02f;}
    public static void Run()
    {
        passed=0;
        foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1440,1024),new Vector2(1220,1110),new Vector2(2335,515)})
        {
            var view=new DesignViewport(size.x,size.y,1920,1080,1520);
            for(int count=1;count<=10;count++)for(int i=0;i<count;i++)
            {
                var pose=HandLayout.Pose(i,count);var matrix=view.Matrix*BattleGeometry.Rotation(pose.angle,pose.center);
                Check(Near(matrix.MultiplyPoint3x4(pose.center),view.Matrix.MultiplyPoint3x4(pose.center)),"card pivot must survive scaled viewport rotation");
                var corner=pose.Rect(182,273).position;
                Check(Near(matrix.inverse.MultiplyPoint3x4(matrix.MultiplyPoint3x4(corner)),corner),"card hit coordinates invert transformed drawing");
            }
            var menu=BattleGeometry.EdgeRect(view,true,1014,77,53);var name=BattleGeometry.EdgeRect(view,false,195,168,40);
            Check(Mathf.Abs(view.Matrix.MultiplyPoint3x4(new Vector2(menu.xMax,0)).x-(view.WindowWidth-22*view.Scale))<.02f,"menu stays at right window edge");
            Check(Mathf.Abs(view.Matrix.MultiplyPoint3x4(name.position).x-22*view.Scale)<.02f,"names stay at left window edge");
            var from=new Vector2(960,820);var to=new Vector2(1030,180);
            Check(Near(view.Matrix.MultiplyPoint3x4(BattleGeometry.ArrowPoint(from,to,1)),view.Matrix.MultiplyPoint3x4(to)),"arrow curve and head meet in each viewport");
            for(int n=0;n<20;n++)
            {
                float t=n/19f;var point=BattleGeometry.ArrowPoint(from,to,t);var tangent=BattleGeometry.ArrowTangent(from,to,t);
                var m=view.Matrix*BattleGeometry.Rotation(Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg,point);
                Check(Near(m.MultiplyPoint3x4(point),view.Matrix.MultiplyPoint3x4(point)),"arrow segment pivot cannot drift");
            }
        }
        var selecting=new HeroSelection();selecting.Tick(true,.3f);var baseHero=BattleLayout.Hero(true);var lifted=selecting.Pose(baseHero);
        Check(lifted.center.y<baseHero.center.y-40&&lifted.width>baseHero.width,"selected hero lifts and enlarges");
        selecting.Tick(false,1);Check(Near(selecting.Pose(baseHero).center,baseHero.center),"hero returns on cancellation");
        var life=new BattleLifecycle();var state=new StateView{match_id="A",phase="mulligan"};life.Observe(state);
        Check(life.Entering&&life.HeroPose(baseHero,true).center.y>baseHero.center.y+100,"hero starts off board");
        life.Tick(state,false,2);Check(!life.Entering&&Near(life.HeroPose(baseHero,true).center,baseHero.center),"entrance settles");
        state.phase="finished";life.Tick(state,true,4);Check(!life.Ending,"result waits for presentation queue");
        life.Tick(state,false,.1f);Check(life.Ending&&!life.ResultReady,"death precedes result");
        life.Tick(state,false,.8f);Check(life.DeathProgress>0&&!life.ResultReady,"death has a visible interval");
        life.Tick(state,false,2);Check(life.ResultReady&&life.ResultAlpha>.99f,"result settles with input enabled");
        life.Observe(new StateView{match_id="B",phase="mulligan"});Check(!life.Ending&&life.Entering,"new match clears prior result");
        life.Reset();Check(!life.Ending&&!life.Entering,"menu reset clears lifecycle");
        var hero=new PlayerView{result="LOST",hero=new EntityView{health=30}};
        life.Observe(new StateView{match_id="C",phase="finished"});life.Tick(new StateView{phase="finished"},false,0);
        Check(life.DeadHero(hero),"concede shatters losing hero even if health nonzero");
        for(int i=0;i<20;i++)Check(HeroFragments.Fragment(baseHero,i,.8f)!=HeroFragments.Fragment(baseHero,i,0),"death shards move independently");
        Check(BattleInteraction.PlayOffersTarget(new StateView{moves=new[]{new Move{kind="play",entity=1,target=2},new Move{kind="play",entity=1}}},1),"mixed legal choice still offers target indicator");
        Check(!BattleInteraction.PlayOffersTarget(state,0),"no picked card means no target indicator");
        var colorTrack=new OriginalColorTrack{mode=1,colors=new[]{new OriginalColorKey{time=0,value=Color.red},new OriginalColorKey{time=1,value=Color.blue}},
            alpha=new[]{new OriginalAlphaKey{time=0,value=0},new OriginalAlphaKey{time=.5f,value=1},new OriginalAlphaKey{time=1,value=0}}};
        Check(colorTrack.Sample(.5f).a==1&&colorTrack.Sample(.5f).r>.49f&&colorTrack.Sample(.5f).b>.49f,"original color and alpha keys sample on separate times");
        Check(colorTrack.Sample(0).a==0&&colorTrack.Sample(1).a==0,"native gradient fades at boundaries");
        var scalar=new OriginalScalarTrack{scalar=2,mode=1,keys=new[]{new OriginalCurveKey{time=0,value=0,outSlope=1},new OriginalCurveKey{time=1,value=1,inSlope=1}}};
        Check(Mathf.Abs(scalar.Sample(.5f)-1)<.001f,"original Hermite curve preserves slopes and multiplier");
        var missing=JsonUtility.FromJson<CardEffectSprite>("{\"texture\":\"legacy\"}");Check(!missing.HasNative,"old JSON must not activate default nested native settings");
        var absentModules=JsonUtility.FromJson<OriginalParticleTracks>("{\"restored\":true}");
        Check(absentModules.Color(.5f)==Color.white&&absentModules.Size(.5f)==1,"missing native modules preserve existing appearance");
        int audioCount=0;life.AudioCue=id=>audioCount++;life.Reset();life.Observe(new StateView{match_id="D",phase="finished"});
        life.Tick(new StateView{phase="finished"},false,0);life.Tick(new StateView{phase="finished"},false,2);life.Tick(new StateView{phase="finished"},false,3);
        Check(audioCount==2,"death and result sounds fire exactly once per match");
        Debug.Log("INTERACTION LIFECYCLE CHECKS PASSED: "+passed);
    }
}
