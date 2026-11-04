using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable] public sealed class NativeImpactConfig{public NativeImpactInfo impact;}
[Serializable] public sealed class NativeImpactRecord{public string card_id,file;public NativeImpactConfig config;}
[Serializable] public sealed class NativeImpactAsset{public string reference,file;}
[Serializable] public sealed class NativeImpactCatalog{public NativeImpactRecord[] programs;public NativeImpactAsset[] prefabs;}

// Registry 内的独立原图工厂；宿主在回调栈退出后 FlushReleased，避免采样中销毁。
public sealed class NativeImpactFactory : IDisposable
{
    readonly AssetRegistry assets;readonly NativeImpactCatalog catalog;
    readonly Func<NativeAreaInstance,int,GameObject> locations,facings;
    readonly Dictionary<NativeAreaClone,NativeAreaInstance> instances=new Dictionary<NativeAreaClone,NativeAreaInstance>();
    readonly HashSet<NativeAreaClone> released=new HashSet<NativeAreaClone>();
    readonly int width,height;int nextSlot;bool disposed;
    public IReadOnlyDictionary<NativeAreaClone,NativeAreaInstance> Instances{get{return instances;}}
    public NativeImpactFactory(AssetRegistry assets,NativeImpactCatalog catalog,
        Func<NativeAreaInstance,int,GameObject> locations,Func<NativeAreaInstance,int,GameObject> facings=null,int width=512,int height=512)
    {
        this.assets=assets??throw new ArgumentNullException(nameof(assets));
        this.catalog=catalog??throw new ArgumentNullException(nameof(catalog));
        this.locations=locations;this.facings=facings;this.width=width;this.height=height;
    }
    public NativeImpactClone GetSpell(NativeImpactPrefab prefab)
    {
        if(disposed)throw new ObjectDisposedException(nameof(NativeImpactFactory));
        if(prefab==null||prefab.prefab_null||String.IsNullOrEmpty(prefab.prefab_reference))
            throw new NotSupportedException("Original impact prefab identity is unbound");
        var asset=Array.Find(catalog.prefabs,p=>p.reference==prefab.prefab_reference);
        if(asset==null)throw new NotSupportedException("Original impact prefab is absent from the registry: "+prefab.prefab_reference);
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,asset.file)));
        if(program.reference!=prefab.prefab_reference)throw new InvalidDataException("Original impact registry identity mismatch");
        NativeAreaInstance instance=null;
        instance=new NativeAreaInstance(program,assets,nextSlot++,locations:kind=>
        {
            if(locations==null)throw new NotSupportedException("Original impact location service is unbound");
            return locations(instance,kind);
        },facings:kind=>
        {
            if(facings==null)throw new NotSupportedException("Original impact facing service is unbound");
            return facings(instance,kind);
        },width:width,height:height,startAtFsmEntry:true);
        instances.Add(instance.Endpoint,instance);
        return new NativeImpactClone{Clone=instance.Endpoint,AddTarget=instance.Targets.AddTarget,
            Rotate=value=>instance.Endpoint.Object.transform.Rotate(value)};
    }
    public bool Release(NativeAreaClone clone,bool reset)
    {
        if(reset)throw new NotSupportedException("Impact clones only support original ReleaseSpell(false)");
        if(!instances.ContainsKey(clone))throw new InvalidOperationException("Impact clone is not owned by this factory");
        released.Add(clone);return true;
    }
    public void FlushReleased()
    {
        foreach(var clone in released){var instance=instances[clone];instances.Remove(clone);instance.Dispose();}
        released.Clear();
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        foreach(var instance in instances.Values)instance.Dispose();instances.Clear();released.Clear();
    }
}
