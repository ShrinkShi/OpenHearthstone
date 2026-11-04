using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Runs Unity's real legacy mixer in Play Mode. No official assembly is loaded.
public static class NativeLegacyBlendChecks
{
    const string Session="LocalLab.LegacyBlendOracle";
    [Serializable] sealed class Sample{public float time,a,b,x,singleWeight,singleX,sameLayerOther,allLayersOther;}
    [Serializable] sealed class Report{public string kind="unity-legacy-playmode-oracle";public int passed,stopAll=(int)PlayMode.StopAll,stopSameLayer=(int)PlayMode.StopSameLayer;public Sample[] samples;}
    static readonly List<Sample> samples=new List<Sample>();
    static readonly List<AnimationClip> owned=new List<AnimationClip>();
    static Animation mixed,single,sameLayer,allLayers;
    static float began,fadeAt=-1;
    static bool replayed;
    static float replayBefore,replayAfter;
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
        if(!SessionState.GetBool(Session,false))return;
        EditorApplication.update+=Tick;
    }
    static Animation Create(string name,float rest)
    {
        var go=new GameObject(name);go.transform.localPosition=new Vector3(rest,0,0);
        var animation=go.AddComponent<Animation>();animation.cullingType=AnimationCullingType.AlwaysAnimate;
        Add(animation,"A",0,0);Add(animation,"B",10,0);Add(animation,"Other",30,1);
        return animation;
    }
    static void Add(Animation animation,string name,float value,int layer)
    {
        var clip=new AnimationClip{legacy=true,wrapMode=WrapMode.Loop};
        clip.SetCurve("",typeof(Transform),"localPosition.x",AnimationCurve.Constant(0,2,value));
        owned.Add(clip);animation.AddClip(clip,name);animation[name].layer=layer;
    }
    static void Tick()
    {
        if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Session+".started",0)>30)
        {Debug.LogError("Legacy blend oracle timed out before collecting real frames");Finish(1);return;}
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling||Time.frameCount==frame)return;
        frame=Time.frameCount;
        try
        {
            if(mixed==null)
            {
                Application.targetFrameRate=120;
                mixed=Create("Mixed",7);single=Create("Single",7);sameLayer=Create("SameLayer",7);allLayers=Create("AllLayers",7);
                mixed.Play("A");sameLayer.Play("A");sameLayer.Play("Other",PlayMode.StopSameLayer);
                allLayers.Play("A");allLayers.Play("Other",PlayMode.StopSameLayer);
                began=Time.time;return;
            }
            if(fadeAt<0&&Time.time-began>=.15f)
            {
                mixed.CrossFade("B",.3f,PlayMode.StopSameLayer);
                single.CrossFade("B",.3f,PlayMode.StopSameLayer);
                sameLayer.CrossFade("B",.3f,PlayMode.StopSameLayer);
                allLayers.CrossFade("B",.3f,PlayMode.StopAll);
                fadeAt=Time.time;
            }
            if(fadeAt<0)return;
            float time=Time.time-fadeAt;
            if(!replayed&&time>=.4f)
            {
                replayBefore=mixed["B"].time;
                mixed.CrossFade("B",.3f,PlayMode.StopSameLayer);
                replayAfter=mixed["B"].time;replayed=true;
            }
            samples.Add(new Sample{time=time,a=mixed["A"].weight,b=mixed["B"].weight,x=mixed.transform.localPosition.x,
                singleWeight=single["B"].weight,singleX=single.transform.localPosition.x,
                sameLayerOther=sameLayer["Other"].weight,allLayersOther=allLayers["Other"].weight});
            if(time<.6f)return;
            File.WriteAllText("../work/native-legacy-blend-oracle.json",JsonUtility.ToJson(new Report{samples=samples.ToArray()},true));
            int passed=0;
            foreach(var sample in samples)
            {
                float weight=Mathf.Clamp01(sample.time/.3f);
                Require(Mathf.Abs(sample.b-weight)<.07f,"native incoming fade weight");passed++;
                Require(Mathf.Abs(sample.a-(1-weight))<.07f,"native outgoing fade weight");passed++;
                Require(Mathf.Abs(sample.x-10*sample.b)<.1f,"native mixer preserves weighted position");passed++;
                if(sample.singleWeight>.001f)
                {Require(Mathf.Abs(sample.singleX-10)<.01f,"single active clip normalizes its partial fade weight");passed++;}
            }
            Require(samples.Count>10,"native oracle captured enough actual frames");passed++;
            var last=samples[samples.Count-1];
            Require(last.sameLayerOther>.99f&&last.allLayersOther<.01f,"original PlayMode values select native layers");passed++;
            Require((int)PlayMode.StopAll==4&&(int)PlayMode.StopSameLayer==0,"source PlayMode numeric values match native Unity");passed++;
            Require(replayed&&replayBefore>.35f&&Mathf.Abs(replayBefore-replayAfter)<.001f,"CrossFade of already playing name preserves clip time");passed++;
            var report=new Report{passed=passed,samples=samples.ToArray()};
            File.WriteAllText("../work/native-legacy-blend-oracle.json",JsonUtility.ToJson(report,true));
            Debug.Log("NATIVE LEGACY BLEND ORACLE PASSED: "+passed);
            Finish(0);
        }
        catch(Exception error){Debug.LogException(error);Finish(1);}
    }
    static void Require(bool value,string message){if(!value)throw new Exception("Legacy blend oracle: "+message);}
    static void Finish(int code)
    {
        SessionState.EraseBool(Session);SessionState.EraseFloat(Session+".started");EditorApplication.update-=Tick;
        foreach(var clip in owned)UnityEngine.Object.DestroyImmediate(clip);
        EditorApplication.Exit(code);
    }
}
