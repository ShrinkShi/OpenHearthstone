using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeITweenCompleteChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original iTween curves: "+message);passed++;}
    static bool Near(float a,float b){return Mathf.Abs(a-b)<.000005f;}
    // Quarter/midpoint/three-quarter values independently evaluated from source IL.
    static readonly float[,] values={
        {.0625f,.25f,.5625f},{.4375f,.75f,.9375f},{.125f,.5f,.875f},
        {.015625f,.125f,.421875f},{.578125f,.875f,.984375f},{.0625f,.5f,.9375f},
        {.00390625f,.0625f,.31640625f},{.68359375f,.9375f,.99609375f},{.03125f,.5f,.96875f},
        {.0009765625f,.03125f,.2373046875f},{.7626953125f,.96875f,.9990234375f},{.015625f,.5f,.984375f},
        {.076120466f,.2928932f,.6173166f},{.38268343f,.70710677f,.9238795f},{.14644661f,.5f,.85355339f},
        {.0055242717f,.03125f,.1767767f},{.8232233f,.96875f,.9944757f},{.015625f,.5f,.984375f},
        {.03175416f,.1339746f,.33856218f},{.6614378f,.8660254f,.9682458f},{.0669873f,.5f,.9330127f},
        {.25f,.5f,.75f},{.6633367f,1.0510158f,.98634514f},{.02734375f,.234375f,.52734375f},
        {.47265625f,.765625f,.97265625f},{.1171875f,.5f,.8828125f},
        {-.06413656f,-.0876975f,.1825903f},{.8174097f,1.0876975f,1.0641366f},
        {-.09968184f,.5f,1.09968184f},{-.0055242717f,-.015625f,.08838835f},
        {.91161165f,1.015625f,1.0055243f},{-.0078125f,.5f,1.0078125f},
        {.25f,.5f,.75f},{.8232233f,.96875f,.9944757f},{1.1530931f,.984375f,1}
    };
    public static void Run()
    {
        passed=0;
        for(int mode=0;mode<=34;mode++)
        {
            Check(NativeITween.SupportedEase(mode),"declared source enum "+mode);
            for(int i=0;i<3;i++)
            {
                float time=(i+1)*.25f;
                Check(Near(NativeITween.Sample(0,1,time,mode),values[mode,i]),"source value "+mode+"/"+time);
                if(mode!=33)Check(Near(NativeITween.Sample(2,-2,time,mode),2-4*values[mode,i]),"negative interval "+mode+"/"+time);
            }
            Check(NativeITween.Sample(0,1,-1,mode)==NativeITween.Sample(0,1,0,mode),"normalized clock lower bound "+mode);
            Check(NativeITween.Sample(0,1,2,mode)==NativeITween.Sample(0,1,1,mode),"normalized clock upper bound "+mode);
        }
        Check(!NativeITween.SupportedEase(-1)&&!NativeITween.SupportedEase(35),"unknown enum remains explicit error");
        Check(NativeITween.Sample(0,1,0,15)==.0009765625f&&NativeITween.Sample(0,1,1,16)==.9990234375f,"original expo residual endpoints are preserved");
        Check(NativeITween.Sample(0,1,0,17)==.00048828125f&&NativeITween.Sample(0,1,1,17)==.99951171875f,"original in/out expo residual endpoints");
        Check(Near(NativeITween.Sample(2,6,.5f,33),3.1715729f),"composite compares time against start/2 and chooses sine");
        Check(Near(NativeITween.Sample(1,5,.5f,33),2.1715729f)&&Near(NativeITween.Sample(1,5,.5001f,33),4.8750866f),"source composite keeps discontinuous equality boundary");
        Check(NativeITween.Sample(2,6,.25f,32)==3,"punch enum uses source linear dispatch");
        Check(NativeITween.Punch(3,0)==0&&NativeITween.Punch(3,1)==0,"separate punch has zero endpoints");
        Check(Near(NativeITween.Punch(1,.25f),-.1530931f)&&Near(NativeITween.Punch(1,.5f),-.027063294f),"separate original punch oscillates around zero");
        VerifyActualRuntime();
        Debug.Log("NATIVE COMPLETE ITWEEN CURVES: "+passed+" checks passed; all 35 declared easing dispatches and scalar Punch restored, full iTween/ZoneHand scripts remain incomplete");
        NativeHandGeometryChecks.Run();
    }
    static void VerifyActualRuntime()
    {
        string folder=Path.GetFullPath("../local/runtime");var assets=new AssetRegistry(Path.GetFullPath("../local"));
            for(int mode=0;mode<=34;mode++)
            {
                // Reuse the real source reticle graph, changing only declared easing
                // and fixed timing for a controlled executor check; no visual oracle.
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(folder,"1438c953f1f357de128e06d3.native-effect.json")));
                var birth=program.machines[0].states.First(s=>s.name=="BirthState");
                var scale=birth.actions.First(a=>a.name=="iTweenScaleTo");
                var fade=birth.actions.First(a=>a.name=="iTweenFadeToAction");
                birth.actions=birth.actions.Where(a=>a.name=="SetScale").Concat(new[]{scale,fade}).ToArray();
                foreach(var action in new[]{scale,fade})
                {
                    action.Param("easeType").number=mode;action.Param("time").number=.8f;
                    action.Param("delay").number=0;action.Param("finishEvent").text="";
                    action.Param("speed").is_none=true;
                }
                using(var instance=new NativeEffectInstance(program,assets,0,32,32,()=>Color.white,new NativeSpellContext(5,4,lifecycle:"Birth")))
                {
                    instance.Seek(.4f);var material=instance.Nodes[3].GetComponent<Renderer>().sharedMaterial;
                    Check(Near(instance.Nodes[2].transform.localScale.x,NativeITween.Sample(.1f,1.4f,.5f,mode)),"actual source graph scale executes curve "+mode);
                    Check(Near(material.GetColor("_Color").a,NativeITween.Sample(0,1,.5f,mode))&&instance.UnboundITweenActions==0,"actual material alpha curve bound "+mode);
                    instance.Seek(.8f);
                    Check(instance.Nodes[2].transform.localScale.x==1.4f&&material.GetColor("_Color").a==1&&instance.CompletedITweenActions==2,"source ApplyScale/Color snaps exact endpoint before completion "+mode);
                    instance.Seek(.4f);
                    Check(Near(instance.Nodes[2].transform.localScale.x,NativeITween.Sample(.1f,1.4f,.5f,mode))&&instance.CompletedITweenActions==0,"backward seek restarts original curve "+mode);
                }
            }
    }
}
