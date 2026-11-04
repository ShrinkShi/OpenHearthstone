using System;
using System.Collections.Generic;
using UnityEngine;

// Source component lookup is local to the OwnerDefault target. Borrowed and
// owned renderers share replacement semantics; replay restores the original.
public sealed class NativeGeometryBindings
{
    readonly Dictionary<MeshFilter,Mesh> meshes=new Dictionary<MeshFilter,Mesh>();
    readonly Dictionary<Renderer,Material[]> materials=new Dictionary<Renderer,Material[]>();
    readonly HashSet<Material> instances=new HashSet<Material>();
    readonly Action<Material,Material> created;
    readonly Action<Material> released;
    public NativeGeometryBindings(Action<Material,Material> onCreated=null,Action<Material> onReleased=null)
    {created=onCreated;released=onReleased;}
    public bool SetMesh(GameObject owner,Mesh value)
    {
        if(owner==null||owner.GetComponent<Renderer>()==null)return false;
        var filter=owner.GetComponent<MeshFilter>();if(filter==null)return false;
        if(!meshes.ContainsKey(filter))meshes.Add(filter,filter.sharedMesh);
        filter.mesh=value;return true;
    }
    public bool SetMaterial(GameObject owner,int index,Material value)
    {
        if(owner==null)return false;var renderer=owner.GetComponent<Renderer>();if(renderer==null)return false;
        var current=renderer.sharedMaterials;
        if(index!=0&&index>=current.Length)return false;
        if(index<0)throw new IndexOutOfRangeException("Original SetMaterial negative materialIndex");
        if(!materials.ContainsKey(renderer))materials.Add(renderer,current);
        if(index==0){renderer.material=value;return true;}
        var original=current;current=renderer.materials;
        for(int i=0;i<current.Length;i++)if(current[i]!=null&&current[i]!=original[i]&&instances.Add(current[i]))created?.Invoke(original[i],current[i]);
        current[index]=value;renderer.sharedMaterials=current;return true;
    }
    public void Restore()
    {
        foreach(var pair in meshes)if(pair.Key!=null)pair.Key.sharedMesh=pair.Value;
        foreach(var pair in materials)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
        foreach(var instance in instances){released?.Invoke(instance);AssetRegistry.ReleaseTransient(instance);}
        instances.Clear();
        meshes.Clear();materials.Clear();
    }
}
