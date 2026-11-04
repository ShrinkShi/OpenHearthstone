using UnityEngine;

public sealed class HeroSelection
{
    public float Lift{get;private set;}
    public void Reset(){Lift=0;}
    public void Tick(bool selecting,float delta)
    {Lift=Mathf.Lerp(Lift,selecting?1:0,1-Mathf.Exp(-Mathf.Max(0,delta)*18));}
    public Rect Pose(Rect rect)
    {
        var center=rect.center-Vector2.up*(44*Lift);rect.size*=1+.11f*Lift;rect.center=center;return rect;
    }
}
