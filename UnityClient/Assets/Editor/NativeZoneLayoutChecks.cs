using System;
using System.Collections.Generic;
using UnityEngine;

public static class NativeZoneLayoutChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original zone layout dispatch: "+message);passed++;}
    public static void Run()
    {
        passed=0;var model=new GameObject("Layout Card identity");
        try
        {
            var events=new List<string>();Action<NativeZoneBinding> complete=null;
            var zone=new NativeZoneBinding{AddLayoutComplete=callback=>{events.Add("register");complete=callback;return true;},UpdateLayout=()=>events.Add("update")};
            var list=new NativeZoneChangeList();list.MarkDirty(zone);bool assets=true,actorLoading=true;
            var card=new NativeZoneLayoutCard{Object=model,ActorLoading=()=>actorLoading};int cardQueries=0;
            var entity=new NativeZoneLoadingEntity{LoadingAssets=()=>assets,GetCard=()=>{cardQueries++;return card;}};
            var services=new NativeZoneLayoutServices{Busy=()=>false};var runner=new NativeZoneLayout(list,services);
            runner.UpdateDirtyZones(new[]{entity},10);Check(events.Count==0&&cardQueries==1,"entity assets loading delays zone update");
            assets=false;runner.Tick(10);Check(events.Count==0&&cardQueries==1,"captured Card kept while Actor loads");
            actorLoading=false;runner.Tick(11);Check(events.Count==2&&events[0]=="register"&&events[1]=="update"&&cardQueries==1,"non-hand registers completion before layout and does not re-fetch Card");
            Check(!list.IsComplete&&list.DirtyCount==1,"layout dispatch is not layout completion");complete(zone);runner.Tick(12);Check(list.IsComplete,"actual layout callback releases dirty and finish wait");
            bool ready=false,handled=false,noSort=false;int updates=0;
            var handCard=new NativeZoneLayoutCard{Object=model,DrawHandled=()=>handled,DoNotSort=()=>noSort,ActorReady=()=>ready};Action<NativeZoneBinding> handDone=null;
            var hand=new NativeZoneBinding{IsHand=true,Cards=()=>new[]{handCard},AddLayoutComplete=callback=>{handDone=callback;return false;},UpdateLayout=()=>updates++};
            var handList=new NativeZoneChangeList();handList.MarkDirty(hand);var handRunner=new NativeZoneLayout(handList,services);handRunner.UpdateDirtyZones(new NativeZoneLoadingEntity[0],20);
            Check(updates==0&&!handList.IsComplete,"unsorted unready hand Actor delays layout");ready=true;handRunner.Tick(20);Check(updates==1&&!handList.IsComplete,"same-frame next Update can start hand layout, callback result false is ignored");handDone(hand);handRunner.Tick(21);Check(handList.IsComplete,"hand callback controls finish timing");
            handled=true;ready=false;var handledList=new NativeZoneChangeList();handledList.MarkDirty(hand);new NativeZoneLayout(handledList,services).UpdateDirtyZones(new NativeZoneLoadingEntity[0],30);Check(updates==2,"turn-start handled draw bypasses readiness wait");
            handled=false;noSort=true;var unsorted=new NativeZoneChangeList();unsorted.MarkDirty(hand);new NativeZoneLayout(unsorted,services).UpdateDirtyZones(new NativeZoneLoadingEntity[0],31);Check(updates==3,"DoNotSort card bypasses readiness wait");
            int registrations=0,deathUpdates=0;Action delayed=null;float delayValue=0;
            var deathZone=new NativeZoneBinding{AddLayoutComplete=callback=>{registrations++;return true;},UpdateLayout=()=>deathUpdates++};var death=new NativeZoneChangeList();death.MarkDirty(deathZone);
            var deathServices=new NativeZoneLayoutServices{DeathBlock=true,Busy=()=>false,RemoveNextDeathDelay=()=>.15f,DelayScaled=(seconds,token,callback)=>{delayValue=seconds;delayed=callback;}};
            new NativeZoneLayout(death,deathServices).UpdateDirtyZones(new NativeZoneLoadingEntity[0],40);Check(delayed!=null&&Math.Abs(delayValue-.15f)<.0001f&&deathUpdates==0,"death delay is consumed and delegated as scaled time");delayed();Check(deathUpdates==1&&registrations==0&&death.DirtyCount==0&&death.IsComplete,"death branch updates directly and clears dirty without layout callbacks");
            deathServices.RemoveNextDeathDelay=()=>-1;var noDelay=new NativeZoneChangeList();noDelay.MarkDirty(deathZone);new NativeZoneLayout(noDelay,deathServices).UpdateDirtyZones(new NativeZoneLoadingEntity[0],41);Check(deathUpdates==2&&noDelay.IsComplete,"negative death delay skips timer");
            int failures=0;var missing=new NativeZoneChangeList();missing.MarkDirty(deathZone);var missingServices=new NativeZoneLayoutServices{DeathBlock=true,Busy=()=>false,RemoveNextDeathDelay=()=>.15f,Error=error=>failures++};var missingRunner=new NativeZoneLayout(missing,missingServices);missingRunner.UpdateDirtyZones(new NativeZoneLoadingEntity[0],42);missingRunner.Tick(43);Check(failures==1&&missingRunner.Faults==1&&!missing.IsComplete,"missing scaled delay fails without guessed completion");
            bool cancelled=false;assets=true;var abort=new NativeZoneChangeList();abort.MarkDirty(zone);var abortRunner=new NativeZoneLayout(abort,new NativeZoneLayoutServices{Busy=()=>false,Cancelled=()=>cancelled});abortRunner.UpdateDirtyZones(new[]{entity},50);cancelled=true;abortRunner.Tick(51);Check(abortRunner.Cancellations==1&&!abort.IsComplete,"asset-loading cancellation leaves list incomplete");
            bool stillLoading=true;var shared=new NativeZoneChangeList();shared.MarkDirty(zone);int queries=0;
            var sharedRunner=new NativeZoneLayout(shared,services);var sharedEntity=new NativeZoneLoadingEntity{LoadingAssets=()=>stillLoading,GetCard=()=>{queries++;return null;}};
            sharedRunner.UpdateDirtyZones(new[]{sharedEntity},60);sharedRunner.UpdateDirtyZones(new[]{sharedEntity},60);Check(queries==2,"parallel original loading calls retain independent iterators");
        }
        finally{AssetRegistry.ReleaseTransient(model);}
        Debug.Log("NATIVE ZONE LAYOUT DISPATCH: "+passed+" checks passed; real Card/Zone layouts, scaled delay implementation and source animations remain incomplete");
        NativeZoneChangeChecks.Run();
    }
}
