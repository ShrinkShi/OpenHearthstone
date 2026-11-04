using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeActorSpellRenderChecks
{
    public static void Run()
    {
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var scene=assets.Scene("MODEL_ACTOR_MINION");
        scene.transform.position=new Vector3(49000,-1000,0);var actor=scene.GetComponentInChildren<NativeActorIdentity>(true);
        // The source prefab stores an editor placement; the animation is local to its board slot.
        actor.transform.localPosition=Vector3.zero;
        foreach(var r in scene.GetComponentsInChildren<Renderer>(true)){r.gameObject.layer=30;r.enabled=r==actor.MeshRenderer;}
        actor.MeshRenderer.sharedMaterials[actor.PortraitMaterialIndex].mainTexture=assets.Texture("CARD_CS2_179");
        var parent=new GameObject("Source SpellTable attachment fixture");parent.transform.SetParent(actor.transform,false);
        var table=new NativeActorSpellTable<NativeEffectInstance>(actor.SpellDefinition,e=>new NativeEffectInstance(JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,e.file))),assets,0,32,32),
            (s,t)=>s.SetSpellType(t),s=>s.AttachToOriginalParent(parent),s=>s.Dispose());
        var camera=new GameObject("Original SpellTable GPU test camera").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.cullingMask=1<<30;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.nearClipPlane=.01f;camera.farClipPlane=100;
        camera.transform.rotation=Quaternion.Euler(90,0,0);var bounds=actor.MeshRenderer.bounds;camera.aspect=.8f;
        camera.orthographicSize=Mathf.Max(bounds.size.z/2,bounds.size.x/2/camera.aspect)*2;
        camera.transform.position=new Vector3(bounds.center.x,bounds.max.y+20,bounds.center.z);
        var output=new RenderTexture(256,320,24,RenderTextureFormat.ARGB32);output.Create();camera.targetTexture=output;
        var image=new Texture2D(256,320,TextureFormat.RGBA32,false);var previous=RenderTexture.active;int passed=0,frames=0,animationGaps=0;
        string folder=Path.GetFullPath("../tests/visual/current/native-spell-table");Directory.CreateDirectory(folder);
        try
        {
            // Source SUMMON_IN (2) prefab/FSM is unchanged. The table parent is an explicit test fixture.
            var instance=table.GetSpell(2,true);foreach(var go in instance.Nodes)go.layer=30;
            foreach(float time in new[]{0f,.15f,.3f,.6f})
            {
                instance.Seek(time);
                if(instance.ActorObjectBindings!=1||instance.UnboundActorInitializationActions!=0)throw new Exception("Original SUMMON_IN initializer did not reach parent Actor");passed++;
                if(time>0&&instance.ParticleCount==0)throw new Exception("Original SUMMON_IN source particles did not start");passed++;
                camera.Render();RenderTexture.active=output;image.ReadPixels(new Rect(0,0,256,320),0,0);image.Apply();
                if(image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3)<32)throw new Exception("Original SUMMON_IN Actor capture is blank");passed++;
                File.WriteAllBytes(Path.Combine(folder,"SUMMON_IN-CS2_179-"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());frames++;
            }
            animationGaps=instance.UnboundAnimationActions;
            if(animationGaps!=0)throw new Exception("Original Actor AllyInPlaySpawn animation failed to bind");passed++;
        }
        finally
        {
            table.ReleaseAll();RenderTexture.active=previous;camera.targetTexture=null;AssetRegistry.ReleaseTransient(output);AssetRegistry.ReleaseTransient(image);AssetRegistry.ReleaseTransient(camera.gameObject);
            foreach(var r in scene.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)AssetRegistry.ReleaseTransient(m);AssetRegistry.ReleaseTransient(scene);
        }
        File.WriteAllText("../work/native-spell-table-render-checks.json","{\"passed\":"+passed+",\"frames\":"+frames+",\"unbound_actor_animation\":"+animationGaps+",\"complete\":false,\"production_parenting\":false}");
        Debug.Log("NATIVE SPELL TABLE GPU CHECKS PASSED: "+passed);
    }
}
