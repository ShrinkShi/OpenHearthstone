using System;
using System.Linq;

// Exact original enum and GuessNextStateType branches; usability comes from the caller.
public enum NativeSpellStateType { None=0,Birth=1,Idle=2,Action=3,Cancel=4,Death=5,Reset=6 }
public static class NativeSpellLifecycle
{
    public static NativeSpellStateType Next(NativeSpellStateType current,Func<NativeSpellStateType,bool> usable)
    {
        if(current==NativeSpellStateType.None)
        {foreach(var type in new[]{NativeSpellStateType.Birth,NativeSpellStateType.Idle,NativeSpellStateType.Action,NativeSpellStateType.Death,NativeSpellStateType.Cancel})if(usable(type))return type;}
        else
        {
            var next=current==NativeSpellStateType.Birth?NativeSpellStateType.Idle:current==NativeSpellStateType.Idle?NativeSpellStateType.Action:current==NativeSpellStateType.Action?NativeSpellStateType.Death:NativeSpellStateType.None;
            if(next!=NativeSpellStateType.None&&usable(next))return next;
        }
        return NativeSpellStateType.None;
    }
    public static NativeTransition Destination(NativeMachine machine,NativeSpellStateType type)
    {
        return (machine.events??new NativeTransition[0]).FirstOrDefault(e=>String.Equals(e.@event,type.ToString(),StringComparison.OrdinalIgnoreCase)&&
            machine.states.Any(s=>s.name==e.state&&s.actions.Any(a=>a.name=="SpellStateAction")));
    }
    public static NativeTransition Entry(NativeMachine machine)
    {
        var next=Next(NativeSpellStateType.None,type=>Destination(machine,type)!=null);
        return next==NativeSpellStateType.None?null:Destination(machine,next);
    }
}
public sealed class NativeLifecycleEvent
{
    public readonly int machine;
    public readonly float time;
    public readonly NativeSpellStateType type;
    public NativeLifecycleEvent(int machine,float time,NativeSpellStateType type){this.machine=machine;this.time=time;this.type=type;}
}
