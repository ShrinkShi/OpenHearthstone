using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeAreaPlayerChecks
{
    static int passed;
    static void Check(bool value,string reason){if(!value)throw new Exception("Original area player: "+reason);passed++;}
    public static void Run()
    {
        passed=0;var assets=new AssetRegistry(Path.GetFullPath("../local"));
        string folder=Path.GetFullPath("../tests/visual/current/source-area-player");Directory.CreateDirectory(folder);
        foreach(string card in new[]{"CS2_025","CS2_062"})foreach(int owner in new[]{0,1})
        using(var player=new NativeEffectPlayer(assets))
        {
            player.CardType=id=>4;var state=new StateView{seat=0,players=new PlayerView[0]};var evt=new SemanticEvent{card_id=card,owner=owner};
            string key=card+"-"+owner;
            Check(player.PrepareSource(key,evt,state,state,true,0)&&player.SourceAreaCount==1,"formal default player dispatches original child area "+key);
            var area=player.SourceArea(key);Check(area.Pending==1&&area.FinishedNotifications==0,"parent completion blocked by original live clone "+key);
            Check(area.Reference.Contains(card=="CS2_025"?"Arcane":owner==0?"Hellfire_BothSide.prefab":"Hellfire_BothSide_Opponent"),"original source-side prefab selection "+key);
            var first=area.Effect;player.PrepareSource(key,evt,state,state,true,.15f);
            Check(area.Pending!=0||area.FinishedNotifications==1,"parent completes only from original child finish notification "+key);
            var emission=area.RenderAffine(.15f);var old=RenderTexture.active;var image=new Texture2D(emission.width,emission.height,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=emission;image.ReadPixels(new Rect(0,0,emission.width,emission.height),0,0);image.Apply();
                int pixels=image.GetPixels32().Count(c=>Math.Max(c.r,Math.Max(c.g,c.b))>3);
                Check(pixels>32,"original child area GPU capture visible "+key);
                File.WriteAllBytes(Path.Combine(folder,key+".png"),image.EncodeToPNG());
            }
            finally{RenderTexture.active=old;AssetRegistry.ReleaseTransient(image);}
            Check(player.PrepareSource(key,evt,state,state,true,3),"formal source player follows original clone through completion "+key);
            Check(area.Pending==0&&area.FinishedNotifications==1&&area.StateFinishedNotifications==1,"parent notifications delivered once after original child completion "+key);
            Check(player.PrepareSource(key,evt,state,state,true,.1f),"backward playback remains prepared "+key);
            Check(player.SourceArea(key).Effect!=first,"backward playback creates a fresh original clone "+key);
            Check(player.SourceArea(key).Time==.1f,"fresh source clone samples requested age "+key);
            using(var control=new NativeEffectPlayer(assets))
            {
                control.CardType=id=>4;Check(control.PrepareSource(key,evt,state,state,true,.1f),"independent original clone prepares at same age "+key);
                Check(player.SourceArea(key).Pending==control.SourceArea(key).Pending&&player.SourceArea(key).FinishedNotifications==control.SourceArea(key).FinishedNotifications&&
                    player.SourceArea(key).Effect.CurrentStates.SequenceEqual(control.SourceArea(key).Effect.CurrentStates),"rewound controller matches independent source lifecycle "+key);
            }
        }
        Debug.Log("NATIVE AREA PLAYER: "+passed+" checks passed; formal reviewed area dispatch, full SuperSpell/scene/all-card fidelity remain incomplete");
        NativePowerTargetChecks.Run();
    }
}
