using System;
using System.IO;
using UnityEngine;

public static class FireblastCacheChecks
{
    static int checks;
    static void Rejected<T>(Action create) where T:Exception
    {
        int objects=Resources.FindObjectsOfTypeAll<GameObject>().Length,targets=Resources.FindObjectsOfTypeAll<RenderTexture>().Length;
        bool rejected=false;try{create();}catch(T){rejected=true;}
        if(!rejected)throw new Exception("Expected Fireblast cache rejection "+typeof(T).Name);checks++;
        if(objects!=Resources.FindObjectsOfTypeAll<GameObject>().Length||targets!=Resources.FindObjectsOfTypeAll<RenderTexture>().Length)
            throw new Exception("Partial Fireblast constructor retained objects/targets");checks++;
    }
    public static void Run()
    {
        checks=0;string folder=Path.GetFullPath("../work/f005-cache-"+Guid.NewGuid().ToString("N")),runtime=Path.Combine(folder,"runtime");Directory.CreateDirectory(runtime);
        File.WriteAllText(Path.Combine(runtime,"registry.json"),"{\"entries\":[]}");var assets=new AssetRegistry(folder);
        foreach(string header in new[]{"{}","{\"mesh_color_schema\":\"source-channel-v1\"}"})
        {
            File.WriteAllText(Path.Combine(runtime,"fireblast-ui.json"),header);
            Rejected<InvalidDataException>(()=>new NativePowerVisual(assets));Rejected<InvalidDataException>(()=>new NativeTargetArrow(assets));
        }
        File.WriteAllText(Path.Combine(runtime,"fireblast-ui.json"),"{\"mesh_color_schema\":\"source-channel-v2\",\"arrows\":[]}");
        File.Copy("../local/runtime/original-battle-cameras.json",Path.Combine(runtime,"original-battle-cameras.json"));
        Rejected<InvalidOperationException>(()=>new NativeTargetArrow(assets));
        Rejected<FileNotFoundException>(()=>new NativePowerVisual(assets));
        Debug.Log("FIREBLAST CACHE: "+checks+" checks passed; both independent entry points reject old schema before allocation; partial constructors release owned resources");
    }
    public static void RunAndBuild(){Run();BuildClient.Build();}
}
