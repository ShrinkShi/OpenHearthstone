using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class NativeZoneLayoutCard
{
    public GameObject Object;
    public Func<bool> DrawHandled,DoNotSort,ActorReady,ActorLoading;
}
public sealed class NativeZoneLoadingEntity
{public Func<bool> LoadingAssets;public Func<NativeZoneLayoutCard> GetCard;}
public sealed class NativeZoneLayoutServices
{
    public Func<bool> Busy,Cancelled;public bool DeathBlock;
    public Func<float> RemoveNextDeathDelay;
    // Actual scaled UniTask Delay is an explicit host service, not a guessed timer.
    public Action<float,Func<bool>,Action> DelayScaled;
    public Action<Exception> Error;
}

// Original UpdateDirtyZones and ZoneHand_UpdateLayout request/callback ordering.
// Scene layout and loading queries are explicit, so missing services stay visible.
public sealed class NativeZoneLayout
{
    sealed class HandWait{public NativeZoneBinding zone;public int eligible;}
    sealed class LoadingWait{public IEnumerator<NativeZoneLoadingEntity> iterator;public NativeZoneLoadingEntity entity;public NativeZoneLayoutCard card;public bool hasEntity;public int eligible;}
    readonly NativeZoneChangeList list;readonly NativeZoneLayoutServices services;
    readonly List<HandWait> hands=new List<HandWait>();
    readonly List<LoadingWait> loads=new List<LoadingWait>();int frame,lastTick=-1;
    public int Faults{get;private set;}public int Cancellations{get;private set;}
    public NativeZoneLayout(NativeZoneChangeList list,NativeZoneLayoutServices services){this.list=list;this.services=services;}
    bool Cancelled(){return services.Cancelled!=null&&services.Cancelled();}
    int NextUpdate(){return frame+(lastTick==frame?1:0);}
    void Fault(Exception error){Faults++;if(services.Error!=null)services.Error(error);else Debug.LogException(error);}
    public void UpdateDirtyZones(IEnumerable<NativeZoneLoadingEntity> entities,int presentationFrame)
    {
        frame=presentationFrame;var wait=new LoadingWait{iterator=entities.GetEnumerator()};loads.Add(wait);RunLoadingSafe(wait);
    }
    void StopLoading(LoadingWait wait){loads.Remove(wait);wait.iterator.Dispose();}
    void RunLoadingSafe(LoadingWait wait){try{RunLoading(wait);}catch(Exception error){if(loads.Contains(wait))StopLoading(wait);Fault(error);}}
    void RunLoading(LoadingWait wait)
    {
        while(true)
        {
            if(!wait.hasEntity)
            {
                if(!wait.iterator.MoveNext()){StopLoading(wait);Dispatch();return;}
                wait.entity=wait.iterator.Current;wait.card=wait.entity.GetCard();wait.hasEntity=true;
            }
            bool busy=wait.entity.LoadingAssets()||(wait.card!=null&&wait.card.Object!=null&&wait.card.ActorLoading());
            if(busy)
            {if(Cancelled()){Cancellations++;StopLoading(wait);return;}wait.eligible=NextUpdate();return;}
            wait.hasEntity=false;
        }
    }
    void Dispatch()
    {
        if(services.DeathBlock)
        {
            if(services.RemoveNextDeathDelay==null)throw new NotSupportedException("Original ZoneMgr death delay is unrestored");
            float delay=services.RemoveNextDeathDelay();
            if(delay>=0)
            {
                if(services.DelayScaled==null)throw new NotSupportedException("Original scaled death delay service is unrestored");
                services.DelayScaled(delay,services.Cancelled,()=>{try{DispatchDeath();}catch(Exception error){Fault(error);}});return;
            }
            DispatchDeath();return;
        }
        var zones=new NativeZoneBinding[list.DirtyCount];list.DirtyZones.CopyTo(zones);
        foreach(var zone in zones)
        {
            if(zone.IsHand)RunHandSafe(new HandWait{zone=zone});
            else {Register(zone);zone.UpdateLayout();}
        }
        list.FinishWhenPossible(services.Busy,services.Cancelled,frame);
    }
    void DispatchDeath()
    {
        foreach(var zone in list.DirtyZones)zone.UpdateLayout();
        list.DirtyZones.Clear();list.FinishWhenPossible(services.Busy,services.Cancelled,frame);
    }
    void Register(NativeZoneBinding zone)
    {
        if(zone.AddLayoutComplete==null||zone.UpdateLayout==null)throw new NotSupportedException("Original Zone layout endpoint is unrestored");
        zone.AddLayoutComplete(list.OnUpdateLayoutComplete); // Source ignores the return boolean.
    }
    bool RunHand(HandWait wait)
    {
        if(wait.zone.Cards==null)throw new NotSupportedException("Original hand Card list is unrestored");
        NativeZoneLayoutCard found=null;
        foreach(var candidate in wait.zone.Cards())
        {
            if(candidate.DrawHandled!=null&&candidate.DrawHandled())continue;
            if(candidate.DoNotSort())continue;
            if(!candidate.ActorReady()){found=candidate;break;}
        }
        if(found!=null&&found.Object!=null)
        {if(Cancelled()){Cancellations++;return true;}wait.eligible=NextUpdate();return false;}
        Register(wait.zone);wait.zone.UpdateLayout();return true;
    }
    void RunHandSafe(HandWait wait)
    {try{if(!RunHand(wait))hands.Add(wait);}catch(Exception error){Fault(error);}}
    public void Tick(int presentationFrame,int timing=8)
    {
        if(timing!=8||lastTick==presentationFrame)return;lastTick=presentationFrame;frame=presentationFrame;
        list.BeginUpdatePhase(frame);
        try
        {
        foreach(var wait in loads.ToArray())if(frame>=wait.eligible)
        {if(Cancelled()){Cancellations++;StopLoading(wait);}else RunLoadingSafe(wait);}
        foreach(var wait in hands.ToArray())
        {
            if(frame<wait.eligible)continue;
            if(Cancelled()){Cancellations++;hands.Remove(wait);continue;}
            try{if(RunHand(wait))hands.Remove(wait);}catch(Exception error){hands.Remove(wait);Fault(error);}
        }
        list.Tick(frame,timing);
        }
        finally{list.EndUpdatePhase();}
    }
}
