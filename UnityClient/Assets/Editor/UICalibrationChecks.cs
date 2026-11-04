using System;
using System.Linq;
using System.IO;
using UnityEngine;

public static class UICalibrationChecks
{
    static int passed;
    static void Check(bool condition,string message){if(!condition)throw new Exception("UI calibration check: "+message);passed++;}
    static bool Close(Vector2 a,Vector2 b){return Vector2.Distance(a,b)<.001f;}
    public static void Run()
    {
        passed=0;string folder=Path.GetFullPath("../work/ui-checks");Directory.CreateDirectory(folder);
        string path=Path.Combine(folder,Guid.NewGuid()+".json");var c=new UICalibration(path);
        var rect=new Rect(50,100,320,480);var center=rect.center;
        Check(Close(c.Get("card.minion").Matrix(rect,Vector2.one).MultiplyPoint3x4(center),center),"no file keeps existing layout");
        c.Set(new UIAdjustment{id="card.minion/name",x=12,y=-9,angle=27,scaleX=1.5f,scaleY=.7f,flipY=true});
        Check(c.Get("card.spell/name").x==0,"templates isolated");
        var a=c.Get("card.minion/name");var matrix=a.Matrix(rect,Vector2.one);
        Check(Close(matrix.MultiplyPoint3x4(center),center+new Vector2(12,-9)),"translate around center");
        var point=new Vector2(123,245);Check(Close(matrix.inverse.MultiplyPoint3x4(matrix.MultiplyPoint3x4(point)),point),"rotated and flipped input inverse");
        var half=new Rect(50,100,160,240);Check(Close(a.Matrix(half,new Vector2(.5f,.5f)).MultiplyPoint3x4(half.center),half.center+new Vector2(6,-4.5f)),"canonical units scale with card preview");
        string error;Check(c.Save(out error)&&!c.Dirty,"save succeeds");
        var restored=new UICalibration(path);Check(restored.Load(out error)&&restored.Get(a.id).flipY&&restored.Get(a.id).angle==27,"disk round trip");
        a.x=50;c.Set(a);Check(c.Dirty,"dirty tracked");c.Undo();Check(c.Get(a.id).x==12,"undo restores prior values");c.Redo();Check(c.Get(a.id).x==50,"redo restores edit");
        Check(c.Save(out error)&&File.Exists(path+".bak"),"second save creates backup");
        Check(UICalibration.Parse(File.ReadAllText(path+".bak")).elements[0].x==12,"backup preserves previous save");
        string before=c.Serialize();bool invalid=false;try{c.Set(new UIAdjustment{id="bad",scaleX=0});}catch(InvalidDataException){invalid=true;}Check(invalid&&c.Serialize()==before,"invalid edit is atomic");
        string badPath=Path.Combine(folder,"bad-"+Guid.NewGuid()+".json");File.WriteAllText(badPath,"{\"version\":999,\"elements\":[]}");
        Check(!c.Import(badPath,out error)&&c.Serialize()==before,"bad import leaves current calibration intact");
        invalid=false;try{UICalibration.Parse("{\"version\":1,\"designWidth\":1920,\"designHeight\":1080,\"elements\":[{\"id\":\"x\",\"scaleX\":1,\"scaleY\":1},{\"id\":\"x\",\"scaleX\":1,\"scaleY\":1}]}");}catch(InvalidDataException){invalid=true;}Check(invalid,"duplicate ids rejected");
        c.Set(new UIAdjustment{id="battle.hero.own",x=20});c.Set(new UIAdjustment{id="scene.board",angle=180});c.ResetPage("scene");
        Check(c.Get("scene.board").angle==0&&c.Get("battle.hero.own").x==0&&c.Get("card.minion/name").x==50,"scene reset leaves card templates intact");
        c.Undo();Check(c.Get("scene.board").angle==180&&c.Get("battle.hero.own").x==20,"page reset can be undone");
        var clone=c.Get(a.id);clone.x=999;Check(c.Get(a.id).x==50,"inspector copy cannot mutate stored state");
        var target=new UIEditTarget{rect=rect,matrix=matrix};Check(target.Contains(matrix.MultiplyPoint3x4(center)),"input follows transformed visual");
        Check(!target.Contains(matrix.MultiplyPoint3x4(new Vector2(rect.xMin-20,rect.yMin))),"outside transformed visual rejected");
        var store=c.Serialize();UICalibration.Parse(store);Check(c.Serialize()==store,"deterministic serialization");
        RunQuestChecks(folder);
        File.WriteAllText("../work/ui-calibration-checks.json","{\"passed\":"+passed+"}");Debug.Log("UI CALIBRATION CHECKS PASSED: "+passed);
    }

    static void RunQuestChecks(string folder)
    {
        var fixtures=F026QuestCalibration.FixtureQuests;
        Check(fixtures.Count(q=>q.period=="daily")==4&&fixtures.Count(q=>q.period=="weekly")==4,"offline quest preview has four tasks per period");
        var previewAgain=F026QuestCalibration.FixtureQuests;
        fixtures[0].description="mutated local copy";
        Check(previewAgain[0].description!="mutated local copy","preview fixture getter returns isolated quest values");

        var targetIds=F026QuestCalibration.TargetIds;
        var targetSet=new System.Collections.Generic.HashSet<string>(targetIds);
        Check(targetIds.Length==76&&targetSet.Count==targetIds.Length,"quest page exposes root, background, section, eight tile, component and reroll targets");
        Check(targetSet.Contains(F026QuestCalibration.PageId)&&targetSet.Contains(F026QuestCalibration.ChildId(F026QuestCalibration.PageId,"background")),
            "quest page and background can be selected independently");
        Check(targetSet.Contains(F026QuestCalibration.TitleId(false))&&targetSet.Contains(F026QuestCalibration.TitleId(true)),
            "daily and weekly section headings have independent targets");
        string[] children={"icon","title","text","reward","progress","status","button"};
        for(int period=0;period<2;period++)
        {
            bool weekly=period==1;
            for(int i=0;i<4;i++)
            {
                string tile=F026QuestCalibration.TileId(weekly,i);
                Check(targetSet.Contains(tile),"quest tile target exists: " + tile);
                foreach(string child in children)
                    Check(targetSet.Contains(F026QuestCalibration.ChildId(tile,child)),"quest component target exists: " + tile + "/" + child);
                Check(targetSet.Contains(F026QuestCalibration.RerollId(fixtures[period*4+i].quest_id)),"quest reroll icon target exists");
            }
        }
        var daily0=F026QuestCalibration.TileRect(false,0);var daily1=F026QuestCalibration.TileRect(false,1);
        var weekly0=F026QuestCalibration.TileRect(true,0);
        Check(Mathf.Abs(daily0.x-70)<.001f&&Mathf.Abs(daily1.x-daily0.xMax-14)<.001f&&Mathf.Abs(weekly0.y-560)<.001f,
            "quest preview uses the production four-column coordinates");

        string path=Path.Combine(folder,"quests-"+Guid.NewGuid()+".json");var calibration=new UICalibration(path);
        string dailyTile=F026QuestCalibration.TileId(false,0),weeklyTile=F026QuestCalibration.TileId(true,1);
        string reroll=F026QuestCalibration.RerollId(F026QuestCalibration.FixtureQuests[0].quest_id);
        calibration.Set(new UIAdjustment{id=F026QuestCalibration.PageId,x=3,y=-4});
        calibration.Set(new UIAdjustment{id=F026QuestCalibration.ChildId(dailyTile,"icon"),x=11,y=-6});
        calibration.Set(new UIAdjustment{id=F026QuestCalibration.ChildId(weeklyTile,"button"),x=-9,y=5});
        calibration.Set(new UIAdjustment{id=reroll,x=6,y=8});
        calibration.Set(new UIAdjustment{id="card.minion/name",x=31});
        string error;Check(calibration.Save(out error),"temporary quest layout saves");
        var restored=new UICalibration(path);Check(restored.Load(out error),"temporary quest layout reloads");
        Check(restored.Get(F026QuestCalibration.PageId).x==3
            &&restored.Get(F026QuestCalibration.ChildId(dailyTile,"icon")).x==11
            &&restored.Get(F026QuestCalibration.ChildId(weeklyTile,"button")).y==5
            &&restored.Get(reroll).x==6,
            "page, icon and button reordering persist after reload");
        restored.ResetPage(F026QuestCalibration.PageId);
        Check(restored.Get(F026QuestCalibration.PageId).x==0
            &&restored.Get(F026QuestCalibration.ChildId(dailyTile,"icon")).x==0
            &&restored.Get(F026QuestCalibration.ChildId(weeklyTile,"button")).y==0
            &&restored.Get(reroll).x==0
            &&restored.Get("card.minion/name").x==31,"reset quest page removes quest adjustments and preserves card layout");
        Check(restored.Save(out error),"reset quest layout saves");
        var resetReload=new UICalibration(path);Check(resetReload.Load(out error)
            &&resetReload.Get(F026QuestCalibration.PageId).x==0
            &&resetReload.Get(F026QuestCalibration.ChildId(weeklyTile,"button")).y==0
            &&resetReload.Get(reroll).x==0
            &&resetReload.Get("card.minion/name").x==31,"reset survives disk reload without changing other pages");

        var account=new F024AccountSnapshot{gold=500,quests=new[]{new F024AccountQuest{quest_id="server-quest",period="daily",description="unchanged"}}};
        string accountBefore=JsonUtility.ToJson(account);int requestCount=0;
        bool requested=F026QuestCalibration.TryReroll(true,()=>{requestCount++;account.gold--;});
        Check(!requested&&requestCount==0&&JsonUtility.ToJson(account)==accountBefore,
            "preview reroll dispatches no request and leaves the account snapshot unchanged");
        Check(F026QuestCalibration.TryReroll(false,()=>requestCount++)&&requestCount==1,
            "real quest reroll reaches its request callback through the same gate");

        var editor=new UIEditor(new UICalibration(Path.Combine(folder,"quest-view-check.json")),new CardDef[0],null,null);
        editor.SelectView("card.minion");Check(editor.View=="card.minion","existing card preview remains selectable");
        editor.SelectView("scene");Check(editor.View=="scene"&&editor.SceneView,"existing scene preview remains camera-backed");
        editor.SelectView("quests");Check(editor.View=="quests"&&editor.Selected==F026QuestCalibration.PageId&&!editor.SceneView,
            "quest editor view selects the calibration root without enabling the battle camera");
    }
}
