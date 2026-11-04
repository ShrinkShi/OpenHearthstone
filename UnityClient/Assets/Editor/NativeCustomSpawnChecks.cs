using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeCustomSpawnChecks
{
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original custom spawn: "+message);passed++;};
        var parent=new GameObject("Custom spawn test parent");var actor=new GameObject("Actor");actor.transform.SetParent(parent.transform,false);
        var root=new GameObject("Spell root");root.transform.SetParent(actor.transform,false);
        var owner=new GameObject("Exact FSM Owner");owner.transform.SetParent(root.transform,false);
        var token=new GameObject("tokenRoot");token.transform.SetParent(owner.transform,false);
        var loan=new NativeCustomSpawn();
        try
        {
            parent.transform.position=new Vector3(2,3,4);parent.transform.rotation=Quaternion.Euler(7,13,19);parent.transform.localScale=Vector3.one*2;
            actor.transform.localPosition=new Vector3(3,4,5);actor.transform.localRotation=Quaternion.Euler(11,17,23);actor.transform.localScale=Vector3.one*.7f;
            owner.transform.localPosition=new Vector3(.3f,.4f,.5f);token.transform.localScale=Vector3.one*3;
            var actorPosition=actor.transform.localPosition;var actorRotation=actor.transform.localRotation;var actorScale=actor.transform.localScale;
            var ownerPosition=owner.transform.localPosition;var ownerRotation=owner.transform.localRotation;var ownerScale=owner.transform.localScale;
            for(int bits=0;bits<8;bits++)
            {
                bool p=(bits&1)!=0,r=(bits&2)!=0,s=(bits&4)!=0;var worldPosition=actor.transform.position;var worldRotation=actor.transform.rotation;var worldScale=actor.transform.lossyScale;
                check(loan.Initialize(owner,actor,token,p,r,s),"source initialization flags "+bits);
                check(owner.transform.parent==parent.transform&&actor.transform.parent==token.transform,"source reparent order avoids cycle "+bits);
                check(p?actor.transform.localPosition==Vector3.zero:Vector3.Distance(actor.transform.position,worldPosition)<.0001f,"position flag "+bits);
                check(r?Quaternion.Angle(actor.transform.localRotation,Quaternion.identity)<.0001f:Quaternion.Angle(actor.transform.rotation,worldRotation)<.001f,"rotation flag "+bits);
                check(s?Vector3.Distance(actor.transform.localScale,Vector3.one)<.0001f:Vector3.Distance(actor.transform.lossyScale,worldScale)<.0001f,"scale flag "+bits);
                check(loan.Finalize(owner,actor,p,r,s)&&actor.transform.parent==parent.transform,"source finalization parent "+bits);
                check(!p||actor.transform.localPosition==Vector3.zero,"final position reset "+bits);
                check(!r||Quaternion.Angle(actor.transform.localRotation,Quaternion.identity)<.0001f,"final rotation reset "+bits);
                check(!s||actor.transform.localScale==Vector3.one,"final scale reset "+bits);
                loan.Restore();check(actor.transform.parent==parent.transform&&owner.transform.parent==root.transform&&actor.transform.localPosition==actorPosition&&Quaternion.Angle(actor.transform.localRotation,actorRotation)<.001f&&actor.transform.localScale==actorScale&&owner.transform.localPosition==ownerPosition&&Quaternion.Angle(owner.transform.localRotation,ownerRotation)<.001f&&owner.transform.localScale==ownerScale,"cancel restores complete hierarchy and poses "+bits);
            }
            check(!loan.Initialize(owner,actor,actor,false,false,false)&&actor.transform.parent==parent.transform,"unsafe token rejects without mutation");
            check(loan.Initialize(owner,actor,token,false,false,false),"claim actor before overlapping source effect");
            var competing=new NativeCustomSpawn();check(!competing.Finalize(owner,actor,true,true,true)&&actor.transform.parent==token.transform,"concurrent instance cannot take an owned actor");competing.Restore();loan.Restore();
        }
        finally{loan.Restore();AssetRegistry.ReleaseTransient(parent);}
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var db=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        string folder=Path.GetFullPath("../tests/visual/current/native-custom-spawn");Directory.CreateDirectory(folder);var oldTarget=RenderTexture.active;
        try
        {
            foreach(string file in new[]{"2ac223d9b583771c9eb24338","b0cbd0aa93fedbfc28ee3f8e"})
            using(var live=visuals.CreateLiveActor(db.cards.First(c=>c.CardId=="CS2_179"),"minion",true))
            {
                var source=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
                var init=source.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="InitializeCustomSpawn");
                var finish=source.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="FinalizeCustomSpawn");
                var identity=live.ActorObject.GetComponent<NativeActorIdentity>();var originalParent=identity.transform.parent;var pose=identity.transform.localPosition;
                var program=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(source));var machine=program.machines[0];
                machine.start="Begin";machine.events=new NativeTransition[0];machine.states=new[]{
                    new NativeState{name="Begin",actions=new[]{init,new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=.2f}}}},transitions=new[]{new NativeTransition{@event="FINISHED",state="Finish"}}},
                    new NativeState{name="Finish",actions=new[]{finish},transitions=new NativeTransition[0]}};
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {
                    effect.AttachToOriginalParent(identity.gameObject);effect.Seek(0);
                    check(effect.CustomSpawnInitializations==1&&effect.UnboundCustomSpawnActions==0&&identity.transform.parent==effect.Nodes[init.Param("tokenRoot").node].transform,"exact source action reparents actual Actor "+file);
                    check(effect.MachineOwner(0).transform.parent==originalParent,"exact source FSM Owner moved to Actor original parent "+file);
                    effect.Seek(.3f);check(effect.CustomSpawnFinalizations==1&&effect.UnboundCustomSpawnActions==0&&identity.transform.parent==originalParent,"serialized Actor output reaches actual finalizer "+file);
                    effect.Seek(0);check(effect.CustomSpawnInitializations==1&&effect.CustomSpawnFinalizations==0&&identity.transform.parent==effect.Nodes[init.Param("tokenRoot").node].transform,"rewind rebuilds loan and output "+file);
                    var rendered=(RenderTexture)live.Render();var image=new Texture2D(rendered.width,rendered.height,TextureFormat.RGBA32,false);
                    try{RenderTexture.active=rendered;image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,file+"-actor.png"),image.EncodeToPNG());}
                    finally{AssetRegistry.ReleaseTransient(image);}
                }
                check(identity!=null&&identity.transform.parent==originalParent&&identity.transform.localPosition==pose,"dispose preserves live Actor and initial parent "+file);
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {effect.Seek(.3f);check(effect.UnboundCustomSpawnActions==2&&effect.CustomSpawnInitializations==0&&effect.CustomSpawnFinalizations==0,"absent Actor cannot mutate guessed objects "+file);}
                init.Param("tokenRoot").is_none=true;
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {effect.AttachToOriginalParent(identity.gameObject);effect.Seek(0);check(effect.UnboundCustomSpawnActions==1&&identity.transform.parent==originalParent,"source IsNone rejects even with a stale token node "+file);}
                init.Param("tokenRoot").is_none=false;finish.Param("actor").is_none=true;
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {effect.AttachToOriginalParent(identity.gameObject);effect.Seek(.3f);check(effect.CustomSpawnInitializations==1&&effect.CustomSpawnFinalizations==0&&effect.UnboundCustomSpawnActions==1,"source IsNone finalizer does not consume a stale Actor output "+file);}
                finish.Param("actor").is_none=false;
                check(identity!=null&&identity.transform.parent==originalParent,"invalid finalizer cleanup restores Actor "+file);
            }
        }
        finally{RenderTexture.active=oldTarget;visuals.Clear();}
        File.WriteAllText("../work/native-custom-spawn-checks.json","{\"passed\":"+passed+",\"frames\":2,\"full_timeline_complete\":false}");
        Debug.Log("NATIVE CUSTOM SPAWN CHECKS PASSED: "+passed);
    }
}
