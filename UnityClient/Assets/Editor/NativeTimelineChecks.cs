using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeTimelineChecks
{
    public static void RunGpu()
    {
        NativeRandomizedParticleChecks.RunGpu();
        NativeColorSpeedChecks.RunGpu();
        NativeTimelineShakerChecks.RunGpu();
        NativeEliteChecks.RunGpu();
        NativeGeometryChecks.RunGpu();
        NativeRandomWaitChecks.RunGpu();
        NativeClipAudioChecks.RunGpu();
        NativeClipAudioChecks.RunAllSourceAudio();
        NativeEntryGraphChecks.RunGpu();
        int passed=0;Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception("Original Timeline: "+message);passed++;};
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        string folder=Path.GetFullPath("../tests/visual/current/native-timeline-control");Directory.CreateDirectory(folder);
        foreach(string file in new[]{"2ac223d9b583771c9eb24338","b0cbd0aa93fedbfc28ee3f8e"})
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
            var play=program.machines.SelectMany(m=>m.states).SelectMany(s=>s.actions).First(a=>a.name=="TimelinePlay");
            var timeline=program.timelines.Single();var track=timeline.tracks.Single(t=>t.kind=="AnimationTrack");var item=track.clips.Single();
            var clip=program.animations.Single(a=>a.id==item.animation);
            check(timeline.node==1&&track.node==2&&clip.tracks.Length==183,"exact nested director, Animator and source curves");
            check(play.Param("gameObject").node==1&&play.Param("finishOnTimelineStopped").number==0,"exact original action immediately finishes");
            program.machines=new[]{new NativeMachine{owner=1,owner_known=true,start="Play",events=new NativeTransition[0],states=new[]{
                new NativeState{name="Play",actions=new[]{play},transitions=new[]{new NativeTransition{@event="FINISHED",state="Done"}}},
                new NativeState{name="Done",actions=new NativeAction[0],transitions=new NativeTransition[0]}}}};
            using(var effect=new NativeEffectInstance(program,assets,0,64,64))
            {
                foreach(float age in new[]{0f,.2f,.7f,1.5f,4.7f,6f})
                {
                    effect.Seek(age);
                    check(effect.TimelineStarts==1&&effect.UnboundTimelineActions==0&&effect.UnsupportedTimelineTracks==timeline.tracks.Count(t=>!t.muted&&t.kind!="AnimationTrack"&&t.kind!="ControlRandomizedTrack"&&t.kind!="CameraShaker2Track"),"partial graph stays diagnosed");
                    check(effect.CurrentStates[0]==(age==0?"Play":"Done"),"Timeline animation survives action/state exit");
                    float time=Mathf.Clamp(age-item.start,0,item.duration)*item.speed+item.clip_in;
                    foreach(var curve in clip.tracks.Where(c=>c.property.StartsWith("position.")||c.property.StartsWith("scale.")))
                    {
                        var target=effect.Nodes[track.node].transform.Find(curve.path);
                        int axis=curve.property.EndsWith("x")?0:curve.property.EndsWith("y")?1:2;
                        float actual=curve.property.StartsWith("position.")?target.localPosition[axis]:target.localScale[axis];
                        check(Mathf.Abs(actual-curve.Sample(Mathf.Clamp(time,0,clip.duration)))<.001f,"source channel "+curve.path+"/"+curve.property+" at "+age);
                    }
                }
                effect.Nodes[track.node].transform.Find(clip.tracks.First(c=>c.property.StartsWith("position.")).path).localPosition=Vector3.one*123;
                effect.Seek(7);
                check(effect.Nodes[track.node].transform.Find(clip.tracks.First(c=>c.property.StartsWith("position.")).path).localPosition==Vector3.one*123,"stopped director never overwrites later state");
                effect.Seek(.2f);check(effect.TimelineStarts==1,"rewind rebuilds one deterministic timeline");
                var controls=timeline.tracks.Where(t=>t.kind=="ControlRandomizedTrack").SelectMany(t=>t.clips).ToArray();
                foreach(float age in new[]{.2f,1.2f,2.2f,4.7f})
                {
                    effect.Seek(age);
                    foreach(var control in controls)
                    {
                        bool active=age>=control.start&&age<control.start+control.duration;
                        var root=effect.Nodes[control.control.node];
                        check(root.activeSelf==active,"source control activation interval");
                        if(active)check(effect.Particles[control.control.particle_roots[0]].particleCount>0,"actual source smoke emits");
                    }
                    if(age==.2f||age==1.2f)
                    {
                        var output=effect.Render(age);var image=new Texture2D(output.width,output.height,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
                        try{RenderTexture.active=output;image.ReadPixels(new Rect(0,0,output.width,output.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(folder,file+"-"+age.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());}
                        finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
                    }
                }
                effect.Seek(.2f);var seed=effect.Particles[controls[0].control.particle_roots[0]].randomSeed;
                effect.Seek(0);effect.Seek(.2f);
                check(effect.Particles[controls[0].control.particle_roots[0]].randomSeed==seed,"source range deterministic seed adapter replays");
            }
            play.Param("finishOnTimelineStopped").number=1;
            using(var effect=new NativeEffectInstance(program,assets,0,32,32))
            {effect.Seek(1);check(effect.CurrentStates[0]=="Play","explicit wait holds until director duration");effect.Seek(6.1f);check(effect.CurrentStates[0]=="Done","explicit stopped wait finishes");}
        }
        Debug.Log("Original Timeline checks passed: "+passed);
    }
}
