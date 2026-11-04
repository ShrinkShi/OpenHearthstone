using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NativeLegacyWrapChecks
{
    const string Session="LocalLab.LegacyWrapOracle";
    [Serializable] sealed class Case
    {
        public int clipMode,componentMode,stateMode,observedMode;
        public float time,position;
        public bool enabled;
        [NonSerialized] public Animation animation;
    }
    [Serializable] sealed class Report
    {
        public string kind="unity-legacy-wrap-inheritance-oracle";
        public int passed;public float elapsed;public Case[] cases;
    }
    static readonly List<Case> cases=new List<Case>();
    static readonly List<AnimationClip> owned=new List<AnimationClip>();
    static float began;static int frame=-1;
    public static void Run()
    {
        SessionState.SetBool(Session,true);
        SessionState.SetFloat(Session+".started",(float)EditorApplication.timeSinceStartup);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void Resume()
    {if(SessionState.GetBool(Session,false))EditorApplication.update+=Tick;}
    static void Create(WrapMode clipMode,WrapMode componentMode,WrapMode stateMode)
    {
        var go=new GameObject("Wrap-"+clipMode+"-"+componentMode+"-"+stateMode);
        var animation=go.AddComponent<Animation>();animation.cullingType=AnimationCullingType.AlwaysAnimate;
        animation.wrapMode=componentMode;
        var clip=new AnimationClip{legacy=true,wrapMode=clipMode};
        clip.SetCurve("",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,0,.2f,10));
        owned.Add(clip);animation.AddClip(clip,"Test");
        // Leave the source Clip's inherited state untouched unless explicitly overridden.
        if(stateMode!=WrapMode.Default)animation["Test"].wrapMode=stateMode;
        animation.Play("Test");
        cases.Add(new Case{clipMode=(int)clipMode,componentMode=(int)componentMode,stateMode=(int)stateMode,animation=animation});
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Session+".started",0)>30)
        {Debug.LogError("Legacy wrap oracle timed out");Finish(1);return;}
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||Time.frameCount==frame)return;
        frame=Time.frameCount;
        try
        {
            if(cases.Count==0)
            {
                Application.targetFrameRate=120;
                foreach(var clip in new[]{WrapMode.Default,WrapMode.Once,WrapMode.Loop,WrapMode.PingPong,WrapMode.ClampForever})
                    foreach(var component in new[]{WrapMode.Default,WrapMode.Once,WrapMode.Loop})
                        Create(clip,component,WrapMode.Default);
                Create(WrapMode.Loop,WrapMode.Loop,WrapMode.Once);
                Create(WrapMode.Once,WrapMode.Once,WrapMode.Loop);
                began=Time.time;return;
            }
            float elapsed=Time.time-began;if(elapsed<.55f)return;
            foreach(var item in cases)
            {
                var state=item.animation["Test"];
                item.observedMode=(int)state.wrapMode;item.enabled=state.enabled;item.time=state.time;
                item.position=item.animation.transform.localPosition.x;
            }
            var report=new Report{elapsed=elapsed,cases=cases.ToArray()};Save(report);
            int passed=0;
            foreach(var item in cases)
            {
                int effective=item.stateMode!=0?item.stateMode:item.clipMode!=0?item.clipMode:item.componentMode!=0?item.componentMode:1;
                Require(item.enabled==(effective!=1),"enabled mode "+item.clipMode+"/"+item.componentMode+"/"+item.stateMode);passed++;
                if(effective!=1)
                {
                    Require(Mathf.Abs(item.time-elapsed)<.025f,"unbounded animation time");passed++;
                    float sampled=effective==2?Mathf.Repeat(item.time,.2f):effective==4?Mathf.PingPong(item.time,.2f):.2f;
                    Require(Mathf.Abs(item.position-50*sampled)<.05f,"native wrap pose");passed++;
                }
            }
            report.passed=passed;Save(report);Debug.Log("NATIVE LEGACY WRAP ORACLE PASSED: "+passed);Finish(0);
        }
        catch(Exception error){Debug.LogException(error);Finish(1);}
    }
    static void Save(Report report)
    {File.WriteAllText("../work/native-legacy-wrap-oracle.json",JsonUtility.ToJson(report,true));}
    static void Require(bool value,string message)
    {if(!value)throw new Exception("Legacy wrap oracle: "+message);}
    static void Finish(int code)
    {
        SessionState.EraseBool(Session);SessionState.EraseFloat(Session+".started");EditorApplication.update-=Tick;
        foreach(var clip in owned)UnityEngine.Object.DestroyImmediate(clip);
        EditorApplication.Exit(code);
    }
}
