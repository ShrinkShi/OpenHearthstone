using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeOffscreenFXChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original OffScreenRenderFX: "+reason);passed++;}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText("../local/runtime/5a360810c62fabb080bc018b.start-spell.json"));
        Check(program.offscreen_fx.Length==1,"actual original component recovered");var data=program.offscreen_fx[0];
        using(var instance=new NativeAreaInstance(program,assets))
        {
            var effect=instance.Effect;var capture=effect.OffscreenFX.Single();
            Check(capture.Texture.width==256&&capture.Texture.height==256,"original temporary render size");
            Check(capture.CaptureCamera.rect.width==1&&capture.CaptureCamera.rect.height==1,"source viewport fields deserialize without zero-sized Unity Rect");
            var filter=effect.Nodes[data.owner].GetComponent<MeshFilter>();
            Check(Mathf.Abs(capture.Size-effect.Nodes[data.owner].transform.localScale.x*filter.sharedMesh.bounds.size.x/2)<1e-6f,"original local-scale and mesh X-bounds camera size");
            Check(effect.Nodes[data.owner].GetComponent<Renderer>().sharedMaterial.mainTexture==capture.Texture,"original owner mainTexture binding");
            Check(effect.Nodes[data.owner].GetComponent<Renderer>().sharedMaterial.GetFloat("_OriginalFamily")==20,"actual HolyRender material binds original channel decoder");
            var mask=effect.Nodes[6].GetComponent<Renderer>().sharedMaterial;
            var glow=effect.Nodes[7].GetComponent<Renderer>().sharedMaterial;
            Check(mask.GetFloat("_OriginalFamily")==21&&glow.GetFloat("_OriginalFamily")==22,"actual original mask/glow materials bind vertex-only programs");
            Check(mask.GetFloat("_HasSourceTexture")==1&&glow.GetFloat("_HasSourceTexture")==1,"texture-free source programs are not filtered as unresolved textures");
            Check(mask.GetFloat("_ZWrite")==1&&mask.GetFloat("_Cull")==2&&mask.GetInt("_CaptureDstAlpha")==3,"actual original mask retains depth, backface cull and destination alpha modulation");
            Check(!effect.Nodes[6].GetComponent<Renderer>().enabled&&!effect.Nodes[7].GetComponent<Renderer>().enabled,"restored programs preserve authored disabled renderers before source activation");
            var moved=effect.Nodes[data.source].transform.position;
            Check(Mathf.Abs(moved.x-(effect.Root.transform.position.x+250+program.nodes[1].position.x+program.nodes[data.source].position.x))<.01f,"original 250 X offset isolates source geometry");
            effect.ChangeInitializedSourceState(1,0);var composed=instance.Render(.8f);
            Directory.CreateDirectory("../tests/visual/current/native-offscreen-fx");
            Save(capture.Texture,"../tests/visual/current/native-offscreen-fx/source.png",true);
            Save(composed,"../tests/visual/current/native-offscreen-fx/composed.png",true);
            CheckHoly(composed);
            Check(effect.Nodes[data.source].transform.position==moved,"sampling does not repeatedly accumulate source offset");
            effect.Seek(0);
            Check(effect.Nodes[data.source].transform.position==moved,"rewind rebuild reapplies one original offset");
        }
        Debug.Log("NATIVE OFFSCREEN FX: "+passed+" checks passed; original capture component only, complete Shader/pass/scene integration remains incomplete");
        NativePropertyChecks.Run();
    }
    static void CheckHoly(RenderTexture target)
    {
        var previous=RenderTexture.active;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            var lit=image.GetPixels32().Where(p=>p.r>3).ToArray();
            double r=lit.Sum(p=>(double)p.r),g=lit.Sum(p=>(double)p.g),b=lit.Sum(p=>(double)p.b);
            Check(g>r*.5&&r>g&&g>b,"real original capture decodes into authored gold, rather than displaying red channel data");
        }
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
    }
    static void Save(RenderTexture target,string path,bool visible)
    {
        var previous=RenderTexture.active;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
        try
        {
            RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
            if(visible)Check(image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3)>128,"actual capture/composition is visible");
        }
        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
    }
}
