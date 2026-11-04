using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeStaticActorChecks
{
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Static Actor: "+message);passed++;};
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var staticImage=new Texture2D(256,320,TextureFormat.RGBA32,false);var liveImage=new Texture2D(512,640,TextureFormat.RGBA32,false);
        var previous=RenderTexture.active;string folder="../tests/visual/current/native-static-actor";Directory.CreateDirectory(folder);
        try
        {
            foreach(string id in new[]{"CS2_182","CS2_172","CS2_179"})
            {
                var card=database.cards.First(c=>c.CardId==id);var model=visuals.CreateActor(card,"minion");
                try
                {
                    var identity=model.GetComponentInChildren<NativeActorIdentity>(true);
                    check(identity!=null&&identity.AnimationProgram!=null,"static model retains original Actor identity and clips "+id);
                    check(identity.MeshRenderer.sharedMaterials[identity.PortraitMaterialIndex].shader.name=="LocalLab/OriginalActorPortrait","static original shader "+id);
                    check(identity.MeshRenderer.sharedMaterials[identity.PortraitMaterialIndex].mainTexture==assets.Texture("CARD_"+id),"static current card portrait binding "+id);
                }
                finally
                {
                    foreach(var r in model.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials.Distinct())AssetRegistry.ReleaseTransient(m);
                    AssetRegistry.ReleaseTransient(model);
                }
                var cached=visuals.Render(card,"minion");check(visuals.Render(card,"minion")==cached,"static capture cache is reused "+id);
                RenderTexture.active=(RenderTexture)cached;staticImage.ReadPixels(new Rect(0,0,256,320),0,0);staticImage.Apply();
                using(var live=visuals.CreateLiveActor(card,"minion",true))
                {
                    RenderTexture.active=(RenderTexture)live.Render();liveImage.ReadPixels(new Rect(0,0,512,640),0,0);liveImage.Apply();
                    float error=0;int pixels=0;
                    for(int y=4;y<316;y+=4)for(int x=4;x<252;x+=4)
                    {
                        Color a=staticImage.GetPixel(x,y),b=liveImage.GetPixel(x+128,y+160);
                        error+=Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)+Mathf.Abs(a.a-b.a);pixels++;
                    }
                    check(error/(pixels*4)<.02f,"resting live/static source composition agrees "+id);
                    Color center=staticImage.GetPixel(128,160);check(Mathf.Max(center.r,center.g,center.b)>.02f,"static portrait itself is visible "+id);
                    File.WriteAllBytes(Path.Combine(folder,id+"-static.png"),staticImage.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(folder,id+"-live.png"),liveImage.EncodeToPNG());
                }
            }
            visuals.Invalidate("*");check(visuals.RenderCount==0,"calibration invalidation releases original static captures");
        }
        finally{RenderTexture.active=previous;visuals.Clear();AssetRegistry.ReleaseTransient(staticImage);AssetRegistry.ReleaseTransient(liveImage);}
        File.WriteAllText("../work/native-static-actor-checks.json","{\"passed\":"+passed+",\"frames\":6,\"static_live_unified\":true,\"complete\":false}");
        Debug.Log("NATIVE STATIC ACTOR GPU CHECKS PASSED: "+passed);
    }
}
