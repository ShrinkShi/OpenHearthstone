using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeActorAnimationChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original Actor animation: "+message);passed++;}
    public static void Run(AssetRegistry assets)
    {
        passed=0;var scene=assets.Scene("MODEL_ACTOR_MINION");var actor=scene.GetComponentInChildren<NativeActorIdentity>(true);
        var initialPosition=actor.transform.localPosition;var initialRotation=actor.transform.localRotation;var initialScale=actor.transform.localScale;
        var parent=new GameObject("Original Actor animation table fixture");parent.transform.SetParent(actor.transform,false);
        try
        {
            var clip=actor.AnimationProgram.animations.First(a=>a.name=="AllyInPlaySpawn");
            Check(clip.tracks.Length==7&&Mathf.Abs(clip.duration-.48333338f)<.00001f,"exact original spawn Clip duration and transform channels");
            var row=actor.SpellDefinition.entries.First(e=>e.type==2);var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,row.file)));
            using(var instance=new NativeEffectInstance(program,assets,0,32,32))
            {
                instance.AttachToOriginalParent(parent);
                foreach(float time in new[]{0f,.1f,.25f,.4f,clip.duration,.6f})
                {
                    instance.Seek(time);float sample=Mathf.Min(time,clip.duration);Vector3 expected=Vector3.zero;Quaternion rotation=Quaternion.identity;
                    foreach(var track in clip.tracks)
                    {int axis="xyzw".IndexOf(track.property.Last());if(track.property.StartsWith("position."))expected[axis]=track.Sample(sample);else rotation[axis]=track.Sample(sample);}
                    Check(Vector3.Distance(actor.transform.localPosition,expected)<.0001f,"real source Actor position sampled at "+time);
                    Check(Quaternion.Angle(actor.transform.localRotation,Quaternion.Normalize(rotation))<.1f,"real source Actor rotation sampled at "+time);
                    Check(instance.UnboundAnimationActions==0&&instance.ActorObjectBindings==1,"source FSM resolves external Actor animation at "+time);
                }
                instance.Seek(.1f);var rewindPosition=actor.transform.localPosition;instance.Seek(.4f);instance.Seek(.1f);
                Check(Vector3.Distance(actor.transform.localPosition,rewindPosition)<.0001f,"backward replay restores and resamples original Actor pose");
                using(var competing=new NativeEffectInstance(program,assets,1,32,32))
                {competing.AttachToOriginalParent(parent);competing.Seek(0);Check(competing.UnboundAnimationActions==1,"concurrent external Animation writer is explicitly rejected");Check(actor.transform.localPosition==rewindPosition,"rejected writer cannot move borrowed Actor");}
                Check(actor.transform.localPosition==rewindPosition,"rejected writer disposal preserves active source pose");
            }
            Check(actor.transform.localPosition==initialPosition&&actor.transform.localRotation==initialRotation&&actor.transform.localScale==initialScale,"disposal restores borrowed Actor pose");
            Check(actor.GetComponent<Animation>()==null,"disposal removes only owned external legacy mixer");
            using(var next=new NativeEffectInstance(program,assets,0,32,32))
            {next.AttachToOriginalParent(parent);next.Seek(.1f);Check(next.UnboundAnimationActions==0,"released Actor can be animated by the next effect");}
        }
        finally{foreach(var r in scene.GetComponentsInChildren<Renderer>(true))foreach(var m in r.sharedMaterials)AssetRegistry.ReleaseTransient(m);AssetRegistry.ReleaseTransient(scene);}
        File.WriteAllText("../work/native-actor-animation-checks.json","{\"passed\":"+passed+",\"complete\":false}");
        Debug.Log("NATIVE ACTOR ANIMATION CHECKS PASSED: "+passed);
    }
}
