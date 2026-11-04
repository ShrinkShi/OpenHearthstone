using System;
using System.IO;
using System.Linq;
using UnityEngine;

// 独立观察采集入口返回后的实际对象与浮点RT，不改采集器和生产实现。
public static class V004R1Checks
{
    [Serializable] sealed class ResourcesRecord
    {
        public int newNativeObjects,newCameras,newFloatRenderTextures;
        public bool renderTargetRestored,mipBiasRestored;
        public string[] nativeObjects,cameras,floatRenderTextures;
    }
    public static void Run()
    {
        var cameraIds=Resources.FindObjectsOfTypeAll<Camera>().Select(o=>o.GetInstanceID()).ToArray();
        var textureIds=Resources.FindObjectsOfTypeAll<RenderTexture>().Select(o=>o.GetInstanceID()).ToArray();
        var objectIds=Resources.FindObjectsOfTypeAll<GameObject>().Select(o=>o.GetInstanceID()).ToArray();
        var target=RenderTexture.active;var bias=Shader.GetGlobalVector("_GlobalMipBias");
        Task4VisualEvidence.Run();
        var native=Resources.FindObjectsOfTypeAll<GameObject>().Where(o=>!objectIds.Contains(o.GetInstanceID())&&
            (o.name=="NativeEffect"||o.name=="NativeEffectCamera"||o.name.StartsWith("SuperSpell scene entity ")||o.name.StartsWith("Task4 "))).ToArray();
        var cameras=Resources.FindObjectsOfTypeAll<Camera>().Where(o=>!cameraIds.Contains(o.GetInstanceID())).ToArray();
        var textures=Resources.FindObjectsOfTypeAll<RenderTexture>().Where(o=>!textureIds.Contains(o.GetInstanceID())&&o.format==RenderTextureFormat.ARGBFloat).ToArray();
        var record=new ResourcesRecord{newNativeObjects=native.Length,newCameras=cameras.Length,newFloatRenderTextures=textures.Length,
            renderTargetRestored=RenderTexture.active==target,mipBiasRestored=Shader.GetGlobalVector("_GlobalMipBias")==bias,
            nativeObjects=native.Select(o=>o.name).ToArray(),cameras=cameras.Select(o=>o.name).ToArray(),floatRenderTextures=textures.Select(o=>o.name+"/"+o.IsCreated()).ToArray()};
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-task4Output");
        File.WriteAllText(Path.Combine(args[index+1],"independent-resources.json"),JsonUtility.ToJson(record,true));
        if(native.Length!=0||cameras.Length!=0||textures.Length!=0||!record.renderTargetRestored||!record.mipBiasRestored)
            throw new Exception("V004 independent resource check failed");
        Debug.Log("V004 INDEPENDENT RESOURCES PASS: native objects0/cameras0/float RT0; active RT and mip bias restored");
    }
}
