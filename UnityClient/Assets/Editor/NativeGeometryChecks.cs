using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeGeometryChecks
{
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original Geometry: "+message);passed++;};
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var owner=new GameObject("OriginalGeometryOwner");var renderer=owner.AddComponent<MeshRenderer>();var filter=owner.AddComponent<MeshFilter>();
        var geometry=new NativeGeometryBindings();var a=new Material(Resources.Load<Shader>("NativeEffect"));var b=new Material(a);var c=new Material(a);
        Mesh mesh=assets.MeshFile("dedb2ef7021512f896afde86.mesh.json"),replacement=assets.MeshFile("5bceb64a9367150c76040597.mesh.json");
        try
        {
            filter.sharedMesh=mesh;renderer.sharedMaterials=new[]{a,b};
            check(geometry.SetMesh(owner,replacement)&&filter.sharedMesh==replacement,"source mesh property uses exact resource");
            check(geometry.SetMesh(owner,null)&&filter.sharedMesh==null,"null original mesh clears filter");geometry.Restore();check(filter.sharedMesh==mesh,"restore original mesh reference");
            geometry.SetMaterial(owner,0,c);check(renderer.sharedMaterials[0]==c&&renderer.sharedMaterials[1]==b,"slot zero setter preserves other slots");geometry.Restore();
            geometry.SetMaterial(owner,1,c);check(renderer.sharedMaterials[1]==c&&renderer.sharedMaterials[0]!=a,"nonzero getter instances materials");geometry.Restore();
            check(renderer.sharedMaterials.SequenceEqual(new[]{a,b}),"material instances released and original slots restored");
            check(!geometry.SetMaterial(owner,2,c)&&renderer.sharedMaterials.SequenceEqual(new[]{a,b}),"out of range source no-op");
            geometry.SetMaterial(owner,0,null);check(renderer.sharedMaterials[0]==null,"null original material clears slot");geometry.Restore();
            var child=new GameObject("NoRenderer");child.transform.SetParent(owner.transform,false);
            check(!geometry.SetMesh(child,mesh)&&!geometry.SetMaterial(child,0,a),"source component cache does not search children or parents");
            foreach(string file in new[]{"2ac223d9b583771c9eb24338","b0cbd0aa93fedbfc28ee3f8e","93313b7c2aed67a49667c4c4","96d6edb199ef520b5b3430f4"})
            {
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
                var actions=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).Where(x=>x.name=="SetMesh"||x.name=="SetMaterial").ToArray();
                program.machines=new[]{new NativeMachine{owner=0,owner_known=true,start="Wait",events=new NativeTransition[0],states=new[]{
                    new NativeState{name="Wait",actions=new[]{new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=.1f}}}},transitions=new[]{new NativeTransition{@event="FINISHED",state="Replace"}}},
                    new NativeState{name="Replace",actions=actions,transitions=new NativeTransition[0]}}}};
                using(var effect=new NativeEffectInstance(program,assets,0,64,64))
                {
                    effect.Seek(.01f);var filters=actions.Where(x=>x.name=="SetMesh").Select(x=>effect.Nodes[x.Param("gameObject").node].GetComponent<MeshFilter>()).ToArray();
                    var originals=filters.Select(x=>x.sharedMesh).ToArray();var targets=actions.Where(x=>x.name=="SetMaterial").Select(x=>effect.Nodes[x.Param("gameObject").node].GetComponent<Renderer>()).ToArray();
                    var originalMaterials=targets.Select(x=>x.sharedMaterials).ToArray();effect.Seek(.2f);
                    check(effect.UnboundGeometryActions==0&&effect.MeshReplacements==filters.Length&&effect.MaterialReplacements==targets.Length,"actual original replacement actions "+file);
                    foreach(var action in actions)
                    {
                        var go=effect.Nodes[action.Param("gameObject").node];var value=action.Param(action.name=="SetMesh"?"mesh":"material");
                        if(action.name=="SetMesh")check(go.GetComponent<MeshFilter>().sharedMesh==assets.MeshFile(value.asset_path),"exact original source mesh pointer");
                        else
                        {
                            var material=go.GetComponent<Renderer>().sharedMaterials[0];var source=JsonUtility.FromJson<NativeMaterial>(File.ReadAllText(Path.Combine(assets.Root,value.asset_path)));
                            check(material.name==source.name,"exact original source material definition");
                            foreach(var texture in source.textures)check(material.GetTexture(texture.name)==assets.TextureFile(texture.path),"source material texture binding");
                        }
                    }
                    effect.Seek(.01f);
                    for(int i=0;i<filters.Length;i++)check(filters[i].sharedMesh==originals[i],"production rewind restores mesh");
                    for(int i=0;i<targets.Length;i++)check(targets[i].sharedMaterials.SequenceEqual(originalMaterials[i]),"production rewind restores original material array");
                    effect.Seek(.2f);check(effect.UnboundGeometryActions==0,"production replacement replays after rewind");
                }
            }
        }
        finally{geometry.Restore();AssetRegistry.ReleaseTransient(owner);AssetRegistry.ReleaseTransient(a);AssetRegistry.ReleaseTransient(b);AssetRegistry.ReleaseTransient(c);}
        Debug.Log("Original Geometry checks passed: "+passed);
    }
}
