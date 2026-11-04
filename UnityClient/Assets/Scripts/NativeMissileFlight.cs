using System;
using UnityEngine;

// Concrete source Spell flight endpoint. The host supplies original cloning,
// visual targets, frame scheduling, LookUpdate and arrival/impact services.
public sealed class NativeMissileFlight : IDisposable
{
    readonly NativeAreaInstance clone;
    readonly NativeMissilePathInfo info;
    readonly Func<float,float,float> range;
    readonly Func<bool> randomBool,targetInHand;
    readonly Func<Vector3> target,handSlot;
    readonly Action arrived;
    readonly Action<Vector3> lookUpdate;
    readonly Transform joint;
    readonly float lookAhead;
    float time;
    bool disposed,armed;
    public bool Reached{get;private set;}
    public NativeMissilePath Path{get;private set;}
    public Transform MovementTransform{get;private set;}
    public NativeEffectInstance Effect{get{return clone.Effect;}}
    public Vector3 TargetPosition{get{return Path==null?target():Path.Points[2];}}
    public float FiredAt{get;private set;}
    public NativeMissileFlight(NativeAreaInstance clone,NativeMissilePathInfo info,Func<Vector3> target,
        Func<float,float,float> range,Func<bool> randomBool,Action arrived,Action<Vector3> lookUpdate=null,
        float lookAhead=.05f,Func<bool> targetInHand=null,Func<Vector3> handSlot=null,Func<Transform> targetTransform=null)
    {
        this.clone=clone;this.info=info;this.target=target;this.range=range;this.randomBool=randomBool;
        this.arrived=arrived;this.lookUpdate=lookUpdate;this.lookAhead=lookAhead;this.targetInHand=targetInHand;this.handSlot=handSlot;
        joint=String.IsNullOrEmpty(info.m_TargetJoint)?null:
            Array.Find(clone.Effect.Nodes,n=>n!=clone.Effect.Nodes[0]&&n.name.Contains(info.m_TargetJoint))?.transform;
        MovementTransform=joint??clone.Effect.Nodes[0].transform;
        if(joint!=null)
        {
            if(targetTransform==null||targetTransform()==null)throw new NotSupportedException("Original missile target joint has no target transform");
            clone.Effect.Nodes[0].transform.LookAt(targetTransform(),info.m_JointUpVector);
        }
        clone.Effect.SourceStateFinished+=OnStateFinished;time=clone.PlaybackTime;
    }
    public void Birth()
    {
        if(disposed)throw new ObjectDisposedException(nameof(NativeMissileFlight));
        if(armed||Path!=null)throw new InvalidOperationException("Original missile Birth already started");
        armed=true;clone.Effect.ChangeInitializedSourceState(1,time);
    }
    void OnStateFinished(int type,float at)
    {
        if(!armed||type!=1)return;
        armed=false;
        var points=NativeMissilePath.Generate(info,clone.Effect.Nodes[0].transform.position,target(),range,randomBool,targetInHand!=null&&targetInHand(),handSlot);
        if(joint!=null)points[2].y+=info.m_TargetHeightOffset;
        if(info.m_OrientToPath!=0&&!points[0].Equals(points[2])&&lookUpdate==null)
            throw new NotSupportedException("Original missile LookUpdate service is unbound");
        Path=new NativeMissilePath(points,range(info.m_PathDurationMin,info.m_PathDurationMax),info.m_PathEaseType);FiredAt=at;
    }
    public void Tick(float next)
    {
        if(disposed)throw new ObjectDisposedException(nameof(NativeMissileFlight));
        if(float.IsNaN(next)||float.IsInfinity(next)||next<time)throw new ArgumentOutOfRangeException(nameof(next));
        bool hadPath=Path!=null;
        if(hadPath&&!Reached)
        {
            float elapsed=next-FiredAt;
            MovementTransform.position=Path.Sample(elapsed);
            if(info.m_OrientToPath!=0&&!Path.Points[0].Equals(Path.Points[2]))lookUpdate(Path.LookPoint(elapsed,lookAhead));
        }
        clone.Seek(next);time=next;
        if(!hadPath&&Path!=null)MovementTransform.position=Path.Sample(next-FiredAt);
        if(Path!=null&&!Reached&&next-FiredAt>=Path.Duration)
        {
            Reached=true;
            // Original OnMissileTargetReached spawns impacts/chain/next missile
            // before activating Death on the arrived missile.
            if(arrived!=null)arrived();
            if(disposed)return;
            clone.Effect.ChangeInitializedSourceState(5,next);
        }
    }
    public void Dispose(){if(disposed)return;disposed=true;clone.Effect.SourceStateFinished-=OnStateFinished;}
}
