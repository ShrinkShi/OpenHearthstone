using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeActorAttackSoundChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original attack audio/wait: "+message);passed++;}
    public static void Run()
    {
        passed=0;int calls=0,previous=-1;
        var wait=new NativeActorAttackWait();
        wait.Begin(2,1,0,(p,t)=>{calls++;previous=p;});wait.Resume(1,0,(p,t)=>calls++);
        Check(calls==0&&wait.Pending==1,"waiting action yields");
        wait.StopWaiting();wait.Resume(1,0,(p,t)=>calls++);Check(calls==0,"same frame cannot resume yielded action");
        wait.Resume(2,.016f,(p,t)=>{calls++;previous=p;});Check(calls==1&&previous==2&&wait.Pending==0,"next frame forwards captured previous state");
        wait.Begin(4,2,.02f,(p,t)=>{calls++;previous=p;});Check(calls==2&&previous==4,"released action starts immediately");
        wait.Birth();wait.Begin(3,2,.02f,(p,t)=>calls++);wait.Reset();wait.StopWaiting();wait.Resume(3,.03f,(p,t)=>calls++);
        Check(calls==2,"reset clears source component coroutines");
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var visuals=new CardVisuals(assets);
        var database=JsonUtility.FromJson<CardDB>(File.ReadAllText("../local/card-database.json"));
        var audioParent=new GameObject("Silent source event audio verification");
        var playback=new NativeAudioPlayback(assets.Root,audioParent.transform,true);
        try
        {
            foreach(string file in new[]{"23d38186cee691b6a6171c7a","2a965e276d24b2db14e2483b","bf21da970c07c866f15b2302","c7109e87ad46c02a4a7e74ed"})
            using(var live=visuals.CreateLiveActor(database.cards.First(c=>c.CardId=="CS2_179"),"minion",true))
            {
                var source=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".actor-spell.json")));
                var definition=JsonUtility.FromJson<NativeAudioDefinition>(File.ReadAllText(Path.Combine(assets.Root,source.event_audio_file)));
                var cue=NativeAudio.ResolveExistingSource(definition,"ACTION_STATE",0,()=>.5f);
                Check(cue!=null&&cue.file=="2670b50038652a0dec74cfb8.wav"&&cue.volume==1&&cue.pitch==1&&cue.category==1,"original minion attack-launch PCM and source parameters");
                var pcm=playback.Load(cue.file);var samples=new float[pcm.samples*pcm.channels];
                Check(pcm.GetData(samples,0)&&samples.Any(s=>Mathf.Abs(s)>.01f),"actual original attack PCM decoded");
                using(var birth=new NativeEffectInstance(source,assets,0,32,32))
                {birth.BindSourcePlacement(location=>location==2?live.ActorObject:null);birth.AttachToOriginalParent(live.ActorObject);birth.SelectSourceSpellState(1,0);birth.Seek(.6f);Check(birth.SourceWaitingToAct,"source Birth arms attack wait");Check(birth.SourcePositionUpdates==1&&birth.UnboundSourcePlacements==0,"original Birth placement resolved");}
                using(var effect=new NativeEffectInstance(source,assets,0,32,32,audioCallback:playback.Play))
                {
                    effect.BindSourcePlacement(location=>location==2?live.ActorObject:null);effect.AttachToOriginalParent(live.ActorObject);
                    effect.Seek(.6f);int actionPrevious=effect.ActiveSourceSpellState;
                    effect.SelectSourceSpellState(3,.6f);effect.Seek(.61f);
                    if(source.spell_states.First(s=>s.type==3).animations.Any(a=>a.enabled))
                    {
                        Check(!effect.SourceWaitingToAct&&effect.SourceAttackActionsStarted==0,"original animation releases wait without resuming in same frame");
                        effect.Seek(.61f);Check(effect.SourceAttackActionsStarted==1&&effect.SourceAttackPreviousState==actionPrevious&&effect.SourceAttackStartedTime==.61f,"base action callback resumes on the next presentation frame at the same sample time: "+file+" calls="+effect.SourceAttackActionsStarted+" previous="+effect.SourceAttackPreviousState+" expected="+actionPrevious+" at="+effect.SourceAttackStartedTime);
                        effect.Seek(.62f);Check(effect.SourceAttackActionsStarted==1,"same-age source callback is not replayed on the following sample");
                        Check(effect.SourceEventAudioStarts==1&&effect.UnboundAnimationEvents==0,"actual original callbacks both resolved");
                        var sound=effect.AudioCues.Last();Check(Mathf.Abs(sound.time-.6f)<.00001f&&sound.sourceParent!=null,"source audio starts at authored zero-time action event");
                        var voice=sound.sourceParent.GetComponentInChildren<AudioSource>();
                        Check(voice!=null&&voice.clip==pcm&&voice.mute&&playback.ActiveVoices==1,"actual silent voice remains owned by source GameObject");
                        playback.Play(sound);Check(sound.sourceParent.GetComponentInChildren<AudioSource>()==voice&&playback.ActiveVoices==1,"existing source replay restarts one voice");
                        Check(effect.UnrestoredSourceActionSideEffects==0&&effect.SourcePositionUpdates==1&&effect.UnboundSourcePlacements==0,"original Action placement resolved without remaining transform gap");
                        effect.HideSourceSpellStates();playback.Update();Check(playback.ActiveVoices==0,"source deactivation stops and cleans voice");
                    }
                    else Check(effect.SourceWaitingToAct&&effect.SourceEventAudioStarts==0,"disabled source action bindings are not forced to play");
                    Debug.Log("ORIGINAL ATTACK AUDIO/WAIT "+file+": audio="+effect.SourceEventAudioStarts+" action_callbacks="+effect.SourceAttackActionsStarted+" unbound_events="+effect.UnboundAnimationEvents+" transform_gaps="+effect.UnrestoredSourceActionSideEffects);
                }
                playback.Update();Check(playback.ActiveVoices==0,"source disposal has no voice leak");
            }
        }
        finally{playback.Dispose();visuals.Clear();AssetRegistry.ReleaseTransient(audioParent);}
        Debug.Log("NATIVE ACTOR ATTACK AUDIO/WAIT: "+passed+" checks passed; full controller, transforms and SoundManager remain incomplete");
        NativeSpellStateEventChecks.Run();
    }
}
