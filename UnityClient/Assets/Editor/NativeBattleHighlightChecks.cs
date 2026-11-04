using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

public static class NativeBattleHighlightChecks
{
    [Serializable] sealed class SourceTexture{public string png,raw,sha256;public int width,height,format,mips;}
    [Serializable] sealed class SourceTextures{public SourceTexture[] textures;}
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original battle highlight: "+message);passed++;};
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var actor=new EntityView{entity=1901,card_id="CS2_182"};
        float oldTime=Shader.GetGlobalFloat("_ShaderTime");var oldTarget=RenderTexture.active;
        string folder=Path.GetFullPath("../tests/visual/current/native-battle-highlight");Directory.CreateDirectory(folder);
        try
        {
            Shader.SetGlobalFloat("_ShaderTime",.05f);
            var textures=JsonUtility.FromJson<SourceTextures>(File.ReadAllText(Path.Combine(assets.Root,"original-highlight-flare.json")));
            foreach(var source in textures.textures)
            {
                var texture=assets.HighlightTextureFile(source.png);
                check(texture.width==source.width&&texture.height==source.height&&(int)texture.format==source.format&&texture.mipmapCount==source.mips,"source raw format/dimensions/mips "+source.png);
                using(var sha=SHA256.Create())check(BitConverter.ToString(sha.ComputeHash(texture.GetRawTextureData<byte>().ToArray())).Replace("-","").ToLowerInvariant()==source.sha256,"actual Texture2D payload matches source bytes "+source.png);
                check(texture.wrapModeU==TextureWrapMode.Repeat&&texture.wrapModeV==TextureWrapMode.Repeat&&texture.wrapModeW==TextureWrapMode.Repeat,"source flare repeats beyond normalized UV "+source.png);
                check(texture.filterMode==FilterMode.Trilinear&&texture.anisoLevel==1&&texture.mipMapBias==-.5f,"exact source flare filter/aniso/mip bias "+source.png);
                var legacy=assets.TextureFile(source.png);
                check(legacy!=texture&&legacy.wrapMode==TextureWrapMode.Clamp&&ReferenceEquals(texture,assets.HighlightTextureFile(source.png)),"native sampler cached without mutating other PNG consumers "+source.png);
            }
            using(var actors=new NativeBattleActors(visuals,id=>database.cards.FirstOrDefault(c=>c.CardId==id)))
            {
                var original=actors.Resolve(actor);var live=actors.Find(actor);
                var plane=NativeActorIdentity.FindChildBySubstring(original,"Highlight_Card_Plane").GetComponent<MeshRenderer>();
                foreach(var sample in new[]{
                    new{own=true,ready=false,over=false,selected=false,state=1},
                    new{own=true,ready=true,over=false,selected=false,state=6},
                    new{own=true,ready=true,over=true,selected=false,state=7},
                    new{own=true,ready=false,over=true,selected=false,state=2},
                    new{own=false,ready=false,over=true,selected=false,state=3},
                    new{own=true,ready=true,over=true,selected=true,state=5}})
                {
                    int state=NativeBattleActors.HighlightState(sample.own,sample.ready,sample.over,sample.selected);
                    check(state==sample.state&&actors.Highlight(actor,assets,state),"client interaction reaches source state "+sample.state);
                    check(plane.enabled==(state!=1),"source renderer visibility "+state);
                    if(state!=1)check(plane.sharedMaterial.mainTexture is RenderTexture&&plane.sharedMaterial.shader==Resources.Load<Shader>("OriginalActorHighlight"),"original Shader and generated mask "+state);
                    actors.Retain(new[]{original});check(ReferenceEquals(original,actors.Resolve(actor))&&ReferenceEquals(live,actors.Find(actor)),"highlight retains same actor and camera "+state);
                    var rendered=(RenderTexture)live.Render();var image=new Texture2D(rendered.width,rendered.height,TextureFormat.RGBA32,false);
                    try{RenderTexture.active=rendered;image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,"state-"+state+".png"),image.EncodeToPNG());}
                    finally{AssetRegistry.ReleaseTransient(image);}
                }
                actors.Invalidate();actors.RefreshUnborrowed(new[]{original});
                check(ReferenceEquals(actors.Resolve(actor),original),"calibration refresh preserves Actor borrowed by a running effect");
                actors.RefreshUnborrowed(new GameObject[0]);
                check(actors.Count==0&&!ReferenceEquals(actors.Resolve(actor),original),"unborrowed Actor rebuilt after calibration change");
                var keep=Enumerable.Range(0,14).Select(i=>actors.Resolve(new EntityView{entity=2000+i,card_id="CS2_182"})).ToArray();
                actors.Retain(keep);check(actors.Count==14&&actors.Find(actor)==null,"both complete seven-minion boards retained and prior entity released");
                foreach(var entity in Enumerable.Range(0,14).Select(i=>new EntityView{entity=2000+i,card_id="CS2_182"}))
                    check(actors.Highlight(entity,assets,6),"full board source highlights "+entity.entity);
                actors.Clear();check(actors.Count==0,"board clear releases highlight ownership before Actor materials");
            }
        }
        finally{RenderTexture.active=oldTarget;Shader.SetGlobalFloat("_ShaderTime",oldTime);visuals.Clear();}
        File.WriteAllText("../work/native-battle-highlight-checks.json","{\"passed\":"+passed+",\"frames\":6,\"production_minion_highlight\":true,\"all_actor_states\":false}");
        Debug.Log("NATIVE BATTLE HIGHLIGHT GPU CHECKS PASSED: "+passed);
    }
}
