using System;

// 每次Update只采一份本地表现帧间隔；不依赖手动渲染期间异常的引擎delta。
public sealed class PresentationFrameClock
{
    double previous;bool sampled;
    public float Sample(double now)
    {
        if(double.IsNaN(now)||double.IsInfinity(now))throw new ArgumentOutOfRangeException(nameof(now));
        if(!sampled){previous=now;sampled=true;return 0;}
        double delta=now-previous;previous=now;
        if(delta<0)return 0;
        if(delta>float.MaxValue)throw new ArgumentOutOfRangeException(nameof(now));
        return (float)delta;
    }
    public void Reset(){sampled=false;}
}
