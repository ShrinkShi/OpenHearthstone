using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeMainShakeChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original main shake: "+reason);passed++;}
    static NativeAction Action()
    {return new NativeAction{name="MainCameraShakerAction",@params=new[]{new NativeParam{name="m_Amount",vector=new Vector3(1,2,3),none_known=true},new NativeParam{name="m_Time",number=.3f,none_known=true},new NativeParam{name="m_Delay",number=.2f,none_known=true},new NativeParam{name="m_FinishedEvent",text="DONE"}}};}
    public static void Run()
    {
        passed=0;var go=new GameObject("Original camera manager probe");go.transform.position=new Vector3(10,20,30);go.transform.rotation=Quaternion.Euler(0,90,0);
        var origin=go.transform.position;int draws=0;var camera=new NativeCameraShake(go.transform,(min,max)=>{draws++;return max;});
        try
        {
            camera.Play(new Vector3(1,2,3),1);camera.Tick(.2f);
            Check(go.transform.position==origin+new Vector3(1,2,3)&&draws==3,"world-axis offsets and three random draws before progress update");
            camera.Play(new Vector3(.1f,.2f,.3f),1);camera.Tick(.1f);Check(Vector3.Distance(camera.Offset,new Vector3(.8f,1.6f,2.4f))<1e-4f,"weaker shake does not replace current amount or reset progress");
            camera.Play(new Vector3(4,0,0),1);camera.Tick(1);Check(go.transform.position==origin+new Vector3(4,0,0),"stronger shake restarts intensity while preserving initial origin");
            camera.Tick(.01f);Check(!camera.Started&&go.transform.position==origin,"next update after duration restores original pose");
            camera.Play(Vector3.one,AnimationCurve.Linear(0,1,1,0),.1f,true);camera.Tick(.1f);camera.Play(Vector3.one*10,AnimationCurve.Linear(0,1,1,0),null,true);camera.Tick(5);
            Check(camera.Started&&Vector3.Distance(camera.Offset,Vector3.one*.9f)<1e-4f,"override lock and hold retain original curve/time");
            camera.Play(Vector3.zero,new AnimationCurve());Check(!camera.Started&&go.transform.position==origin,"zero-time curve stops even a locked override");
        }
        finally{camera.Stop();AssetRegistry.ReleaseTransient(go);}
        int fired=0;Vector3 amount=Vector3.zero;float duration=0;var action=Action();
        action.Param("m_Delay").number=.25f;action.Param("m_Time").number=.25f;
        var timer=new NativeMainShakeAction(action,p=>p.number,p=>p.vector,(v,t)=>{fired++;amount=v;duration=t;});
        Check(!timer.Step(.125f)&&fired==0,"delay waits on update rather than enter");
        action.Param("m_Amount").vector=Vector3.one*4;timer.Step(.125f);
        Check(fired==1&&amount==Vector3.one*4&&duration==.25f,"delay boundary reads current amount and duration once");
        action.Param("m_Time").number=.5f;action.Param("m_Delay").number=.125f;
        Check(timer.Step(.375f)&&timer.Finished&&fired==1,"live completion boundary and no repeated firing");
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"32eb0ac4072ba2d9753492b8.area-spell.json")));
        var original=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Single(a=>a.name=="MainCameraShakerAction");
        program.machines=new[]{new NativeMachine{start="A",events=new NativeTransition[0],states=new[]{new NativeState{name="A",actions=new[]{original},transitions=new[]{new NativeTransition{@event="FINISHED",state="B"}}},new NativeState{name="B",actions=new NativeAction[0],transitions=new NativeTransition[0]}}}};
        var target=new GameObject("Original independent camera shake");var physical=new NativeCameraShake(target.transform,(min,max)=>max);
        try
        {
            using(var effect=new NativeEffectInstance(program,assets,width:32,height:32))
            {
                effect.MainCameraShake=(v,t)=>physical.Play(v,t);effect.Seek(.01f);
                Check(effect.MainShakeStarts==1&&effect.MainShakeFinishes==0&&effect.UnboundMainShakeActions==0&&physical.Started,"actual original area action starts independent manager");
                effect.Seek(original.Param("m_Delay").number+original.Param("m_Time").number+.05f);
                Check(effect.CurrentStates.Single()=="B"&&effect.MainShakeFinishes==1,"FINISHED waits for original shake action timer");
            }
            physical.Tick(.01f);Check(physical.Started&&target.transform.position!=Vector3.zero,"effect disposal does not cancel already started camera manager");
        }
        finally{physical.Stop();AssetRegistry.ReleaseTransient(target);}
        var actualCamera=new GameObject("Original attached camera shake driver").AddComponent<Camera>();
        var sourceCurve=AnimationCurve.Linear(0,1,.6f,0);
        NativeCameraShakeDriver.Play(actualCamera,new Vector3(.1f,.1f,.1f),sourceCurve,null,false);
        Check(actualCamera.GetComponent<NativeCameraShakeDriver>()!=null,"full CameraShaker amount/curve/hold/override overload attaches to a camera");
        NativeCameraShakeDriver.Play(actualCamera,Vector3.one,sourceCurve,.1f,true);
        Check(actualCamera.GetComponent<NativeCameraShakeDriver>()!=null,"the same driver overload preserves explicit hold and override inputs");
        NativeCameraShakeDriver.Stop(actualCamera);AssetRegistry.ReleaseTransient(actualCamera.gameObject);
        Debug.Log("NATIVE MAIN SHAKE: "+passed+" checks passed; original action/manager, full camera/scene/UI fidelity remain incomplete");
        NativeAreaPlayerChecks.Run();
    }
}
