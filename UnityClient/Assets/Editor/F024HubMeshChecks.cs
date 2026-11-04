using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class F024HubMeshChecks
{
    static int checks;

    static void Check(bool value,string message)
    {
        if(!value)throw new Exception("F024 hub mesh check: "+message);
        checks++;
    }

    static bool Empty<T>(T[] values){return values==null||values.Length==0;}
    static string Shape<T>(T[] values){return values==null?"null":"len="+values.Length;}

    public static void Run()
    {
        checks=0;
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        const string bookBack="e87ce42115767ba32abc5fa5.mesh.json";
        var bookBackJson=File.ReadAllText(Path.Combine(assets.Root,bookBack));
        var bookBackData=JsonUtility.FromJson<MeshData>(bookBackJson);
        Debug.Log("F024 HUB MESH PARSE "+bookBack+" weights="+Shape(bookBackData.bone_weights)+
            " indices="+Shape(bookBackData.bone_indices)+" bindposes="+Shape(bookBackData.bindposes));
        Check(bookBackData.vertices!=null&&bookBackData.vertices.Length==48&&
            Empty(bookBackData.bone_weights)&&Empty(bookBackData.bone_indices)&&Empty(bookBackData.bindposes),
            "the indexed BookBack source JSON has vertices but no skin payload");

        var minimal=JsonUtility.FromJson<MeshData>("{\"vertices\":[0,0,0],\"uv\":[0,0],\"submeshes\":[],\"bone_indices\":null,\"bone_weights\":null,\"bindposes\":null}");
        Debug.Log("F024 HUB MESH PARSE minimal-null weights="+Shape(minimal.bone_weights)+
            " indices="+Shape(minimal.bone_indices)+" bindposes="+Shape(minimal.bindposes));
        Check(Empty(minimal.bone_weights)&&Empty(minimal.bone_indices)&&Empty(minimal.bindposes),
            "JsonUtility null bone arrays deserialize as absent or empty arrays");

        var bookBackMesh=assets.MeshFile(bookBack);
        try
        {
            Check(bookBackMesh.vertexCount==bookBackData.vertices.Length/3&&
                Empty(bookBackMesh.boneWeights)&&Empty(bookBackMesh.bindposes),
                "the source BookBack static mesh loads without fabricated skin data");
        }
        finally{AssetRegistry.ReleaseTransient(bookBackMesh);}

        const string arrowFile="f1d8d82aaebc7b41b161aa10.mesh.json";
        var arrowData=JsonUtility.FromJson<MeshData>(File.ReadAllText(Path.Combine(assets.Root,arrowFile)));
        Check(arrowData.vertices.Length==210&&arrowData.bone_weights.Length==280&&
            arrowData.bone_indices.Length==280&&arrowData.bindposes.Length==64,
            "the source Hunter Arrow mesh has complete 70-vertex weights and four bindposes");
        var arrowMesh=assets.MeshFile(arrowFile);
        try
        {
            Check(arrowMesh.vertexCount==70&&arrowMesh.boneWeights.Length==70&&arrowMesh.bindposes.Length==4,
                "a complete source-skinned mesh still loads with all weights and bindposes");
        }
        finally{AssetRegistry.ReleaseTransient(arrowMesh);}

        string probeRoot=Path.GetFullPath("../work/f024-hub-mesh-checks");
        string probeRuntime=Path.Combine(probeRoot,"runtime");
        Directory.CreateDirectory(probeRuntime);
        File.WriteAllText(Path.Combine(probeRuntime,"registry.json"),"{\"entries\":[]}");
        string partialFile="partial-"+Guid.NewGuid().ToString("N")+".mesh.json";
        File.WriteAllText(Path.Combine(probeRuntime,partialFile),
            "{\"vertices\":[0,0,0],\"uv\":[0,0],\"submeshes\":[],\"bone_weights\":[1,0,0,0]}");
        var probe=new AssetRegistry(probeRoot);
        bool rejected=false;
        Mesh partialMesh=null;
        try{partialMesh=probe.MeshFile(partialFile);}
        catch(InvalidDataException error){rejected=error.Message.Contains("Skinned mesh data is incomplete");}
        finally
        {
            if(partialMesh!=null)AssetRegistry.ReleaseTransient(partialMesh);
            var orphan=Resources.FindObjectsOfTypeAll<Mesh>().FirstOrDefault(mesh=>mesh!=null&&mesh.name==partialFile);
            if(orphan!=null)AssetRegistry.ReleaseTransient(orphan);
        }
        Check(rejected,"a mesh with partial nonempty skin data is rejected");
        Debug.Log("F024 HUB MESH CHECKS PASSED: "+checks+"; BookBack JSON null/empty arrays, static load, full skin and partial rejection verified");
    }
}
