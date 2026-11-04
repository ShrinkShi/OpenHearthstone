using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeTimelineShakerChecks
{
    // Independent source IL oracle, using midpoint random samples to expose
    // angle chaining, Z alternation, abs amplitude and interpolation exactly.
    static Vector3 Oracle(Transform target,Vector3 origin,NativeTimelineShakeData data,float duration,float time)
    {
        float n=Mathf.Clamp01(time/duration);if(Mathf.Approximately(n,0)||Mathf.Approximately(n,1))return origin;
        var points=new Vector3[30];float a=0;
        for(int j=0;j<30;j++)
        {
            a=j==0?6.283f/2:a+(1.745f+4.538f)/2;
            points[j]=new Vector3(Mathf.Cos(a)*Mathf.Abs(data.amount.x),Mathf.Sin(a)*Mathf.Abs(data.amount.y),
                (j==0||points[j-1].z<0?1:-1)*Mathf.Abs(data.amount.z)/2);
        }
        float phase=n*(data.interval<=0?500:duration/data.interval),f=phase-Mathf.Floor(phase);int i=Mathf.FloorToInt(phase);
        float t=(1-Mathf.Cos(f*Mathf.PI))/2;t=(1-Mathf.Cos(t*Mathf.PI))/2;
        var keys=data.curve.Select(k=>new Keyframe(k.time,k.value,k.inSlope,k.outSlope,k.inWeight,k.outWeight){weightedMode=(WeightedMode)k.weightedMode}).ToArray();
        var curve=new AnimationCurve(keys){preWrapMode=(WrapMode)data.pre_wrap,postWrapMode=(WrapMode)data.post_wrap};
        Vector3 d=(points[i%30]*(1-t)+points[(i+1)%30]*t)*curve.Evaluate(n);
        return origin+target.right*d.x+target.up*d.y+target.forward*d.z;
    }
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original Timeline shaker: "+message);passed++;};
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var random=UnityEngine.Random.state;
        var cameraObject=new GameObject("OriginalTimelineCameraProbe");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
        Vector3 origin=new Vector3(2,10,-3);camera.transform.SetPositionAndRotation(origin,Quaternion.Euler(75,13,4));
        try
        {
            foreach(string file in new[]{"2ac223d9b583771c9eb24338","b0cbd0aa93fedbfc28ee3f8e"})
            {
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
                var clip=program.timelines.Single().tracks.Single(t=>t.kind=="CameraShaker2Track").clips.Single();
                foreach(float interval in new[]{clip.shaker.interval,0f,-.1f})
                {
                    var data=JsonUtility.FromJson<NativeTimelineShakeData>(JsonUtility.ToJson(clip.shaker));data.interval=interval;data.amount=-data.amount;
                    using(var helper=new NativeTimelineShaker(camera.transform,data,clip.duration,(lo,hi)=>(lo+hi)/2))
                    {
                        foreach(float time in new[]{0f,.00001f,.01f,.075f,.13f,.3f,.55f,.8f,clip.duration,2f})
                        {helper.Sample(time);check((camera.transform.position-Oracle(camera.transform,origin,data,clip.duration,time)).sqrMagnitude<1e-8f,"source position "+interval+"/"+time);}
                        helper.Sample(.2f);var prior=camera.transform.position;helper.Sample(.3f,false);
                        check(camera.transform.position==prior,"disabled source Option 110 leaves intermediate pose");helper.Sample(0,false);
                        check(camera.transform.position==origin,"disabled endpoint still resets");helper.Sample(.2f);
                        using(var overlap=new NativeTimelineShaker(camera.transform,data,clip.duration,(lo,hi)=>(lo+hi)/2))
                        {overlap.Sample(.3f);check((camera.transform.position-Oracle(camera.transform,origin,data,clip.duration,.3f)).sqrMagnitude<1e-8f,"overlap shares original origin");}
                        check(camera.transform.position==origin,"helper kill resets target");helper.Sample(.1f);
                    }
                    check(camera.transform.position==origin,"helper dispose restores original pose");
                }
                var play=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="TimelinePlay");
                program.machines=new[]{new NativeMachine{owner=1,owner_known=true,start="Play",events=new NativeTransition[0],states=new[]{new NativeState{name="Play",actions=new[]{play},transitions=new NativeTransition[0]}}}};
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {
                    effect.BindTimelineCamera(camera);effect.Seek(.2f);check(camera.transform.position==origin,"camera does not shake before source start");
                    effect.Seek(.9f);check((camera.transform.position-origin).sqrMagnitude>1e-9f,"actual source Timeline moves bound camera");
                    check(effect.UnboundTimelineCameras==0&&effect.UnsupportedTimelineTracks==0,"all unmuted source tracks resolved");
                    effect.Seek(1.7f);check(camera.transform.position==origin,"source clip pause resets camera");
                    effect.Seek(.9f);check((camera.transform.position-origin).sqrMagnitude>1e-9f,"rewind recreates source helper");
                    effect.Seek(.2f);check(camera.transform.position==origin,"rewind release restores original camera");
                    effect.Seek(.9f);
                }
                check(camera.transform.position==origin,"production dispose restores borrowed camera");
            }
        }
        finally{UnityEngine.Random.state=random;AssetRegistry.ReleaseTransient(cameraObject);}
        Debug.Log("Original Timeline shaker checks passed: "+passed);
    }
}
