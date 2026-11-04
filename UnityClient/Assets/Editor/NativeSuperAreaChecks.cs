using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeSuperAreaChecks
{
    [Serializable] sealed class Prefab{public string reference,file;}
    [Serializable] sealed class Prefabs{public Prefab[] prefabs;}
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original SuperSpell area: "+message);passed++;}
    public static void Run()
    {
        passed=0;var friendly=new NativeAreaInfo{enabled=true};var opponent=new NativeAreaInfo{enabled=true};
        var config=new NativeSuperAreaConfig{friendly=friendly,opponent=opponent};
        Check(NativeSuperArea.Determine(config,true)==friendly&&NativeSuperArea.Determine(config,false)==opponent,"source controller selects one matching side");
        Check(NativeSuperArea.Determine(config,null)==friendly,"missing source/controller falls back to friendly");friendly.prefab_null=true;
        Check(NativeSuperArea.Determine(config,true)==opponent,"unavailable friendly prefab falls back to opponent");opponent.enabled=false;
        Check(NativeSuperArea.Determine(config,null)==null,"disabled and null prefab are unavailable");
        var root=new GameObject("Original SuperSpell host");var source=new GameObject("Original source identity");var model=new GameObject("Area clone order probe");
        root.transform.position=new Vector3(2,3,4);model.transform.position=new Vector3(6,7,8);
        try
        {
            var events=new List<string>();Action finish=null,start=null,delayed=null;int state=1;NativeSuperArea flow=null;
            var endpoint=new NativeAreaClone{Object=model,AddStateStarted=c=>{events.Add("state callback");start=c;},AddFinished=c=>{events.Add("finish callback");finish=c;},ActiveState=()=>state,
                SetSource=s=>events.Add(s==source?"source":"wrong source"),AttachTasks=t=>{events.Add("tasks");return false;},SetPosition=p=>{events.Add("position");model.transform.position=p;},
                SetLocation=(l,p)=>events.Add("location"),UpdatePosition=()=>events.Add("update position"),SetFacing=(f,x,y)=>events.Add("facing"),UpdateOrientation=()=>events.Add("update orientation"),Activate=()=>events.Add("activate")};
            int completions=0,releases=0;
            var services=new NativeSuperAreaServices{Root=root,Source=()=>source,Tasks=new NativePowerTaskList(),RandomRange=(min,max)=>{events.Add("random");return min;},
                GetSpell=info=>{events.Add("get spell");return endpoint;},ReleaseSpell=(clone,reset)=>{Check(!reset,"source release uses false");releases++;return false;},
                FinishIfPossible=()=>completions++,BeforeActivate=clone=>{events.Add("before");Check(flow.ActiveArea==null,"active area assignment occurs after Activate");}};
            flow=new NativeSuperArea(services);flow.Spawn(new NativeAreaInfo{use_super_location=true,facing=7});
            Check(String.Join(",",events)=="random,get spell,state callback,finish callback,source,tasks,position,facing,update orientation,before,activate","original clone/setup/callback/activation order");
            Check(model.transform.parent==root.transform&&model.transform.position==root.transform.position&&flow.Pending==1&&flow.ActiveClones.Count==1&&flow.ActiveArea==endpoint,"actual parent and source position, pending stays until completion");
            finish();Check(flow.Pending==0&&completions==1&&flow.ActiveClones.Count==1,"finished callback unblocks but does not release active clone");start();Check(releases==0,"non-None state retains clone");state=0;start();Check(releases==1&&flow.ActiveClones.Count==0,"None removes and releases clone separately");
            events.Clear();services.BeforeActivate=null;services.Tasks=null;flow=new NativeSuperArea(services);
            flow.Spawn(new NativeAreaInfo{location=6,parent_to_location=true,facing=1});
            Check(events.Contains("location")&&events.Contains("update position")&&!events.Contains("position")&&!events.Contains("tasks"),"non-super location override and no task attachment");
            events.Clear();services.WaitScaled=(time,callback)=>{Check(time==.25f,"exact sampled scaled delay");delayed=callback;};flow=new NativeSuperArea(services);flow.Spawn(new NativeAreaInfo{delay_min=.25f});
            Check(events.Count==1&&flow.Pending==1&&flow.ActiveArea==null,"positive wait defers cloning, pending begins before delay");delayed();Check(flow.ActiveArea==endpoint,"scaled host resumes clone");
            services.WaitScaled=null;flow=new NativeSuperArea(services);bool rejected=false;try{flow.Spawn(new NativeAreaInfo{delay_min=.25f});}catch(NotSupportedException){rejected=true;}
            Check(rejected&&flow.Pending==1,"missing scaled host does not fabricate completion");
        }
        finally{AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(source);if(model!=null)AssetRegistry.ReleaseTransient(model);}
        VerifyOriginalClones();
        Debug.Log("NATIVE SUPER AREA: "+passed+" checks passed; original area path only, full SuperSpell/history/pool/scene integration remains incomplete");
        NativeSourceCameraChecks.Run();
    }
    static void VerifyOriginalClones()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var catalog=JsonUtility.FromJson<NativeSuperAreaCatalog>(File.ReadAllText(Path.Combine(assets.Root,"native-super-area-config.json")));
        var prefabs=JsonUtility.FromJson<Prefabs>(File.ReadAllText(Path.Combine(assets.Root,"native-super-area-prefabs.json"))).prefabs;
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var root=new GameObject("Source area parent");root.transform.position=new Vector3(20000,10000,20000);var source=new GameObject("Source area caster");
        try
        {
            foreach(string card in new[]{"CS2_025","CS2_062"})foreach(bool own in new[]{true,false})
            {
                var file=index.entries.Single(e=>e.card_id==card).file;var config=catalog.programs.Single(p=>p.file==file).config;
                var info=NativeSuperArea.Determine(config,own);var prefab=prefabs.Single(p=>p.reference==info.prefab_reference);
                NativeAreaInstance clone=null;int finishes=0;
                var flow=new NativeSuperArea(new NativeSuperAreaServices{Root=root,Source=()=>source,
                    GetSpell=i=>{clone=new NativeAreaInstance(JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,prefab.file))),assets);return clone.Endpoint;},
                    ReleaseSpell=(c,reset)=>false,FinishIfPossible=()=>finishes++});
                try
                {
                    flow.Spawn(info);Check(flow.ActiveClones.Count==1&&clone.Source==source&&clone.Effect.Nodes[0].transform.parent==root.transform,"one actual independently cloned area selected "+card+"/"+own);
                    var capture=clone.Render(.3f);var previous=RenderTexture.active;var image=new Texture2D(capture.width,capture.height,TextureFormat.RGBA32,false);
                    try
                    {
                        RenderTexture.active=capture;image.ReadPixels(new Rect(0,0,capture.width,capture.height),0,0);image.Apply();
                        Check(image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3)>128,"selected original area graph is visible "+card+"/"+own);
                        Directory.CreateDirectory("../tests/visual/current/native-super-area");File.WriteAllBytes("../tests/visual/current/native-super-area/"+card+"-"+(own?"friendly":"opponent")+".png",image.EncodeToPNG());
                    }
                    finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
                    int pendingBefore=flow.Pending,finishesBefore=finishes;
                    bool rewindRejected=false;try{clone.Seek(.2f);}catch(ArgumentOutOfRangeException){rewindRejected=true;}
                    Check(rewindRejected&&flow.Pending==pendingBefore&&finishes==finishesBefore,"rewind cannot erase original clone setup or fabricate completion "+card+"/"+own);
                    clone.Seek(3);Check(finishes==1&&flow.Pending==0,"original area completion callbacks delivered once and unblocked "+card+"/"+own);
                }
                finally{if(clone!=null)clone.Dispose();}
            }
        }
        finally{AssetRegistry.ReleaseTransient(root);AssetRegistry.ReleaseTransient(source);}
    }
}
