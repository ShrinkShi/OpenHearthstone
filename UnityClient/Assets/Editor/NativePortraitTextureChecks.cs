using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativePortraitTextureChecks
{
    public static void RunGpu()
    {
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original portrait texture: "+message);passed++;};
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var db=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        string folder=Path.GetFullPath("../tests/visual/current/native-portrait-texture");Directory.CreateDirectory(folder);
        var previous=RenderTexture.active;
        using(var live=visuals.CreateLiveActor(db.cards.First(c=>c.CardId=="CS2_179"),"minion",true))
        try
        {
            var identity=live.ActorObject.GetComponent<NativeActorIdentity>();var portrait=assets.Texture("CARD_CS2_179");
            check(identity.HasCardDef&&ReferenceEquals(identity.GetStaticPortraitTexture(),portrait),"actual card definition has static source portrait");
            foreach(int premium in new[]{0,1,2,4}){identity.PremiumType=premium;check(ReferenceEquals(identity.GetStaticPortraitTexture(),portrait),"source action normalizes premium "+premium);}
            identity.PremiumType=3;check(identity.GetStaticPortraitTexture()==null,"missing Diamond portrait never substitutes normal");identity.PremiumType=0;
            foreach(string file in new[]{"2ac223d9b583771c9eb24338","b0cbd0aa93fedbfc28ee3f8e"})
            {
                var original=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
                using(var effect=new NativeEffectInstance(original,assets,0,256,256))
                {
                    effect.AttachToOriginalParent(live.ActorObject);
                    foreach(float age in new[]{.01f,.12f,.3f})
                    {
                        var rendered=effect.Render(age);
                        check(effect.PortraitTextureBindings>0&&effect.UnboundPortraitTextureActions==0,"real source owner/action at "+file+"/"+age);
                        check(effect.TextureBindings>0&&effect.UnboundTextureActions==0,"real texture variable consumer at "+file+"/"+age);
                        var image=new Texture2D(rendered.width,rendered.height,TextureFormat.RGBA32,false);
                        try{RenderTexture.active=rendered;image.ReadPixels(new Rect(0,0,image.width,image.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,file+"-"+age.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());}
                        finally{AssetRegistry.ReleaseTransient(image);}
                    }
                }
                var getter=original.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="GetCardPortraitTextureAction");
                var setter=original.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="SetMaterialTexture"&&a.Param("texture").variable==getter.Param("storedTexture").variable);
                var program=JsonUtility.FromJson<NativeProgram>(JsonUtility.ToJson(original));
                check(original.machines.SelectMany(m=>m.textures??new NativeTextureVariable[0]).Any(v=>v.name==getter.Param("storedTexture").variable&&v.object_null&&!v.unresolved),"source declares null PortraitTexture before the getter");
                var machine=new NativeMachine{owner=0,owner_known=true,start="Read",textures=new[]{new NativeTextureVariable{name=getter.Param("storedTexture").variable,object_null=true}},events=new NativeTransition[0],states=new[]{
                    new NativeState{name="Read",actions=new[]{getter,new NativeAction{name="Wait",@params=new[]{new NativeParam{name="time",number=.1f}}}},transitions=new[]{new NativeTransition{@event="FINISHED",state="Write"}}},
                    new NativeState{name="Write",actions=new[]{setter},transitions=new NativeTransition[0]}}};program.machines=new[]{machine};
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {
                    effect.AttachToOriginalParent(live.ActorObject);effect.Seek(.2f);
                    var material=effect.Nodes[setter.Param("gameObject").node].GetComponent<Renderer>().sharedMaterials[(int)setter.Param("materialIndex").number];
                    check(ReferenceEquals(material.mainTexture,portrait)&&effect.TextureBindings==1,"source variable survives state exit, actual material uses portrait");
                    effect.Seek(0);check(effect.TextureBindings==0&&effect.PortraitTextureBindings==1&&!ReferenceEquals(material.mainTexture,portrait),"backward replay restores material and clears consumer count");
                    effect.Seek(.2f);check(ReferenceEquals(material.mainTexture,portrait)&&effect.TextureBindings==1,"forward replay binds exact texture again");
                }
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {effect.Seek(.2f);check(effect.UnboundPortraitTextureActions==1&&effect.UnboundTextureActions==0&&effect.TextureBindings==1&&effect.Nodes[setter.Param("gameObject").node].GetComponent<Renderer>().sharedMaterials[(int)setter.Param("materialIndex").number].mainTexture==null,"failed getter leaves declared null texture for the consumer");}
                identity.PremiumType=3;
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {effect.AttachToOriginalParent(live.ActorObject);effect.Seek(.2f);check(effect.UnboundPortraitTextureActions==1&&effect.UnboundTextureActions==0&&effect.TextureBindings==1,"missing Diamond portrait leaves declared null and remains diagnosed");}
                identity.PremiumType=0;
                machine.states[0].transitions=new NativeTransition[0];
                program.machines=new[]{machine,new NativeMachine{owner=0,owner_known=true,start="Other",events=new NativeTransition[0],states=new[]{new NativeState{name="Other",actions=new[]{setter},transitions=new NativeTransition[0]}}}};
                using(var effect=new NativeEffectInstance(program,assets,0,32,32))
                {effect.AttachToOriginalParent(live.ActorObject);effect.Seek(0);check(effect.PortraitTextureBindings==1&&effect.UnboundTextureActions==1&&effect.TextureBindings==0,"same named variable is isolated across source FSM owners");}
            }
        }
        finally{RenderTexture.active=previous;visuals.Clear();}
        File.WriteAllText("../work/native-portrait-texture-checks.json","{\"passed\":"+passed+",\"frames\":6,\"full_effects_complete\":false}");
        Debug.Log("NATIVE PORTRAIT TEXTURE CHECKS PASSED: "+passed);
    }
}
