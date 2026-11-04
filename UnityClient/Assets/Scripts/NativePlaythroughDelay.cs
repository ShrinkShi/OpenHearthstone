// Original m_DelayTime belongs to the action, shared by reentrant iterators.
public sealed class NativePlaythroughDelay
{
    public float Remaining{get;private set;}
    public void Reset(float delay){Remaining=delay;}
    public bool Step(float delta)
    {
        if(Remaining>0){Remaining-=delta;return false;}
        return true;
    }
}
