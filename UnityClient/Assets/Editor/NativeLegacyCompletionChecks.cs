using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Observe engine clocks before changing source action completion semantics.
public static class NativeLegacyCompletionChecks
{
    const string Session="LocalLab.LegacyCompletionOracle";
    [Serializable] sealed class Sample
    {
        public float elapsed,onceTime,clampTime,loopTime,variableTime,variableSpeed;
        public bool onceEnabled,clampEnabled,loopEnabled,variableEnabled;
    }
    [Serializable] sealed class Report
    {
        public string kind="unity-legacy-completion-playmode-oracle";
        public int passed,once=(int)WrapMode.Once,loop=(int)WrapMode.Loop,clampForever=(int)WrapMode.ClampForever;
        public float pausedAt,resumedAt,pausedTime,resumedTime;
        public Sample[] samples;
    }
    static readonly List<Sample> samples=new List<Sample>();
    static readonly List<AnimationClip> owned=new List<AnimationClip>();
    static Animation once,clamp,loop,variable;
    static float began,pausedAt=-1,resumedAt=-1,pausedTime,resumedTime;
    static int frame=-1;
    public static void Run()
    {
        SessionState.SetBool(Session,true);
        SessionState.SetFloat(Session+".started",(float)EditorApplication.timeSinceStartup);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        if(SessionState.GetBool(Session,false))EditorApplication.update+=Tick;
    }
    static Animation Create(string name,WrapMode wrap)
    {
        var go=new GameObject(name);
        var animation=go.AddComponent<Animation>();animation.cullingType=AnimationCullingType.AlwaysAnimate;
        var clip=new AnimationClip{legacy=true,wrapMode=wrap};
        clip.SetCurve("",typeof(Transform),"localPosition.x",AnimationCurve.Linear(0,0,.2f,10));
        owned.Add(clip);animation.AddClip(clip,"Test");animation.Play("Test");return animation;
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Session+".started",0)>30)
        {Debug.LogError("Legacy completion oracle timed out");Finish(1);return;}
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||Time.frameCount==frame)return;
        frame=Time.frameCount;
        try
        {
            if(once==null)
            {
                Application.targetFrameRate=120;
                once=Create("Once",WrapMode.Once);clamp=Create("Clamp",WrapMode.ClampForever);
                loop=Create("Loop",WrapMode.Loop);variable=Create("Variable",WrapMode.ClampForever);
                began=Time.time;return;
            }
            float elapsed=Time.time-began;
            if(pausedAt<0&&elapsed>=.08f)
            {pausedAt=elapsed;pausedTime=variable["Test"].time;variable["Test"].speed=0;}
            if(resumedAt<0&&elapsed>=.3f)
            {resumedAt=elapsed;resumedTime=variable["Test"].time;variable["Test"].speed=2;}
            samples.Add(new Sample{elapsed=elapsed,onceTime=once["Test"].time,clampTime=clamp["Test"].time,
                loopTime=loop["Test"].time,variableTime=variable["Test"].time,variableSpeed=variable["Test"].speed,
                onceEnabled=once["Test"].enabled,clampEnabled=clamp["Test"].enabled,
                loopEnabled=loop["Test"].enabled,variableEnabled=variable["Test"].enabled});
            if(elapsed<.75f)return;
            var report=new Report{pausedAt=pausedAt,resumedAt=resumedAt,pausedTime=pausedTime,resumedTime=resumedTime,samples=samples.ToArray()};
            Save(report);
            int passed=0;
            Require(samples.Count>10,"enough real engine frames");passed++;
            Require(report.clampForever==8,"original ClampForever enum");passed++;
            var last=samples[samples.Count-1];
            Require(!last.onceEnabled,"Once disables after completion");passed++;
            Require(last.clampEnabled&&last.clampTime>.2f,"ClampForever remains enabled with unbounded time");passed++;
            Require(last.loopEnabled&&last.loopTime>.4f,"Loop time crosses length without wrapping its clock");passed++;
            Require(Mathf.Abs(pausedTime-resumedTime)<.001f,"zero speed preserves engine time");passed++;
            Require(last.variableEnabled&&Mathf.Abs(last.variableTime-(resumedTime+2*(last.elapsed-resumedAt)))<.025f,"speed change resumes from retained time");passed++;
            bool observedPause=false;
            foreach(var sample in samples)
            {
                if(sample.elapsed>pausedAt&&sample.elapsed<resumedAt)
                {Require(sample.variableEnabled&&Mathf.Abs(sample.variableTime-pausedTime)<.001f,"paused animation stays enabled with constant time");observedPause=true;passed++;}
                if(sample.elapsed>.25f)
                {Require(!sample.onceEnabled&&sample.clampEnabled&&sample.loopEnabled,"wrap completion distinction persists");passed++;}
            }
            Require(observedPause,"captured paused interval");passed++;
            report.passed=passed;Save(report);
            Debug.Log("NATIVE LEGACY COMPLETION ORACLE PASSED: "+passed);Finish(0);
        }
        catch(Exception error){Debug.LogException(error);Finish(1);}
    }
    static void Save(Report report)
    {File.WriteAllText("../work/native-legacy-completion-oracle.json",JsonUtility.ToJson(report,true));}
    static void Require(bool value,string message)
    {if(!value)throw new Exception("Legacy completion oracle: "+message);}
    static void Finish(int code)
    {
        SessionState.EraseBool(Session);SessionState.EraseFloat(Session+".started");EditorApplication.update-=Tick;
        foreach(var clip in owned)UnityEngine.Object.DestroyImmediate(clip);
        EditorApplication.Exit(code);
    }
}
