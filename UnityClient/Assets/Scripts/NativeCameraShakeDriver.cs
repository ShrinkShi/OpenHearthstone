using System;
using UnityEngine;

// Run after the client's board layout resets its base camera pose.
[DefaultExecutionOrder(100)]
public sealed class NativeCameraShakeDriver : MonoBehaviour
{
    NativeCameraShake playback;
    Func<float> deltaTime;
    public static void UseFrameDelta(Camera camera,Func<float> delta)
    {
        if(camera==null)return;var driver=camera.GetComponent<NativeCameraShakeDriver>()??camera.gameObject.AddComponent<NativeCameraShakeDriver>();driver.deltaTime=delta;
    }
    public static void Play(Camera camera,Vector3 amount,float time)
    {Play(camera,amount,AnimationCurve.Linear(0,1,time,0),null,false);}
    public static void Play(Camera camera,Vector3 amount,AnimationCurve curve,float? holdAt,bool isOverride)
    {
        if(camera==null)return;
        var driver=camera.GetComponent<NativeCameraShakeDriver>()??camera.gameObject.AddComponent<NativeCameraShakeDriver>();
        if(driver.playback==null)driver.playback=new NativeCameraShake(camera.transform);
        driver.playback.Play(amount,curve,holdAt,isOverride);
    }
    void LateUpdate(){if(playback!=null)playback.Tick(deltaTime==null?Time.deltaTime:deltaTime());}
    public static void Stop(Camera camera)
    {if(camera==null)return;var driver=camera.GetComponent<NativeCameraShakeDriver>();if(driver!=null&&driver.playback!=null)driver.playback.Stop();}
    void OnDisable(){if(playback!=null)playback.Stop();}
}
