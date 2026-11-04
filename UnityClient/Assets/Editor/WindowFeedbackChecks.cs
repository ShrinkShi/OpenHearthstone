using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class WindowFeedbackChecks
{
    static int passed;
    static void Check(bool value,string label){if(!value)throw new Exception("Window feedback: "+label);passed++;}
    public static void Run()
    {
        passed=0;
        foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1280,720),new Vector2(1220,1110),new Vector2(2335,515),new Vector2(3840,1080)})
        {
            foreach(bool battle in new[]{true,false})
            {
                var viewport=new DesignViewport(size.x,size.y,battle?1920:1600,battle?1080:1000,battle?1520:1120);
                Check(Mathf.Abs(viewport.Matrix.m00-viewport.Matrix.m11)<.0001f,"uniform scaling "+size);
                Check(viewport.Pixels.width/viewport.Pixels.height<=2.5001f,"finite background "+size);
                Check(Mathf.Abs(viewport.Matrix.MultiplyPoint3x4(new Vector2(viewport.Width/2,viewport.Height/2)).x-size.x/2)<.001f,"center stays at window center");
                var camera=new GameObject("ViewportCheck").AddComponent<Camera>();camera.enabled=false;
                try
                {
                    viewport.Apply(camera);camera.fieldOfView=43;camera.transform.position=new Vector3(0,0,-20);
                    Check(Vector2.Distance(viewport.Project(camera,Vector3.zero),new Vector2(viewport.Width/2,viewport.Height/2))<.01f,"3D center and UI center match");
                    var point=new Vector3(3,2,0);var design=viewport.Project(camera,point);
                    var baseline=new DesignViewport(battle?1920:1600,battle?1080:1000,battle?1920:1600,battle?1080:1000,battle?1520:1120);
                    baseline.Apply(camera);
                    Check(Vector2.Distance(design,baseline.Project(camera,point))<.02f,"projected core does not drift when resized");
                }
                finally{UnityEngine.Object.DestroyImmediate(camera.gameObject);}
            }
        }
        var badges=MinionStatusVisuals.Badges(new[]{"deathrattle","poisonous","trigger"});
        Check(badges.SequenceEqual(new[]{"deathrattle","trigger","poisonous"}),"stable badge order");
        Check(MinionStatusVisuals.Badges(new[]{"silenced"}).Length==0,"no inactive mechanics");
        var a=MinionStatusVisuals.Badge(new Rect(0,0,134,172),0,3);var b=MinionStatusVisuals.Badge(new Rect(0,0,134,172),1,3);
        Check(a.xMax<=b.xMin,"status icons do not overlap");
        Check(MinionStatusVisuals.Flash(a,1).width>a.width*1.5f,"trigger flash expands clearly");
        foreach(string name in new[]{"hero_combat","hero_combat_trigger"})
        {
            var before=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/"+name+".json"));
            var after=JsonUtility.FromJson<StateView>(File.ReadAllText("../local/visual/states/"+name+"-after.json"));
            var positions=new Dictionary<int,Rect>();
            foreach(var p in before.players){positions[p.hero.entity]=BattleLayout.Hero(p.seat==before.seat);if(p.weapon!=null)positions[p.weapon.entity]=BattleLayout.Weapon(p.seat==before.seat);for(int i=0;i<p.field.Length;i++)positions[p.field[i].entity]=BattleLayout.Minion(p.seat==before.seat,i,p.field.Length);}
            var presentation=new Presentation();presentation.Accept(before,after,positions);
            var hits=presentation.Cues.Where(c=>c.data.type=="Hit").ToArray();
            var exchange=hits.Where(c=>c.data.source!=c.data.target).ToArray();
            var weaponWear=hits.SingleOrDefault(c=>c.data.source==c.data.target);
            Check(hits.Length==3&&exchange.Length==2&&exchange.All(c=>Mathf.Approximately(c.start,exchange[0].start))&&
                weaponWear!=null&&Mathf.Approximately(weaponWear.start,exchange[0].start+.55f),
                "hero and retaliation resolve together before weapon durability wear: "+name);
            presentation.Seek(exchange[0].start+.01f);
            Check(presentation.Display.players[0].hero.health==after.players[0].hero.health,"hero health settles at impact");
            presentation.Seek(weaponWear.start+.01f);
            Check(presentation.Display.players[0].weapon.durability==after.players[0].weapon.durability,"weapon wear at same impact");
            if(name.EndsWith("trigger"))Check(presentation.Cues.First(c=>c.data.type=="Trigger").start>exchange[0].start,"trigger keeps separate highlight interval");
            presentation.Skip();Check(presentation.Display.public_hash==after.public_hash,"skip converges to authority");
        }
        File.WriteAllText("../work/window-feedback-checks.json","{\"passed\":"+passed+"}");Debug.Log("WINDOW FEEDBACK CHECKS PASSED: "+passed);
    }
}
