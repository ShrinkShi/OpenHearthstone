using System;
using UnityEngine;

// Verified against Blizzard.T5.iTween IL; enum values differ from EaseFloat.
public static class NativeITween
{
    public static bool SupportedEase(int ease){return ease>=0&&ease<=34;}
    public static float Sample(float from,float to,float time,int ease)
    {
        if(!SupportedEase(ease))throw new ArgumentException("Unrestored original iTween easing "+ease);
        float t=Mathf.Clamp01(time);
        float delta=to-from,u;
        switch(ease)
        {
            case 0:return delta*t*t+from;
            case 1:return -delta*t*(t-2)+from;
            case 2:
                u=t/.5f;if(u<1)return delta/2*u*u+from;
                u-=1;return -delta/2*(u*(u-2)-1)+from;
            case 3:return delta*t*t*t+from;
            case 4:u=t-1;return delta*(u*u*u+1)+from;
            case 5:
                u=t/.5f;if(u<1)return delta/2*u*u*u+from;
                u-=2;return delta/2*(u*u*u+2)+from;
            case 6:return delta*t*t*t*t+from;
            case 7:u=t-1;return -delta*(u*u*u*u-1)+from;
            case 8:
                u=t/.5f;if(u<1)return delta/2*u*u*u*u+from;
                u-=2;return -delta/2*(u*u*u*u-2)+from;
            case 9:return delta*t*t*t*t*t+from;
            case 10:u=t-1;return delta*(u*u*u*u*u+1)+from;
            case 11:
                u=t/.5f;if(u<1)return delta/2*u*u*u*u*u+from;
                u-=2;return delta/2*(u*u*u*u*u+2)+from;
            case 12:return -delta*Mathf.Cos(t*1.5707964f)+delta+from;
            case 13:return delta*Mathf.Sin(t*1.5707964f)+from;
            case 14:return -delta/2*(Mathf.Cos(3.1415927f*t)-1)+from;
            // Original exponential functions deliberately have residual endpoints.
            // ApplyScaleTo/ApplyColorTo, rather than the curve, snap completion.
            case 15:return delta*Mathf.Pow(2,10*(t-1))+from;
            case 16:return delta*(-Mathf.Pow(2,-10*t)+1)+from;
            case 17:
                u=t/.5f;if(u<1)return delta/2*Mathf.Pow(2,10*(u-1))+from;
                u-=1;return delta/2*(-Mathf.Pow(2,-10*u)+2)+from;
            case 18:return -delta*(Mathf.Sqrt(1-t*t)-1)+from;
            case 19:u=t-1;return delta*Mathf.Sqrt(1-u*u)+from;
            case 20:
                u=t/.5f;if(u<1)return -delta/2*(Mathf.Sqrt(1-u*u)-1)+from;
                u-=2;return delta/2*(Mathf.Sqrt(1-u*u)+1)+from;
            // GetEasingFunction maps enum punch to linear; Punch APIs are separate.
            case 21:case 32:return from+delta*t;
            case 22:
                u=(Mathf.Sin(t*3.1415927f*(.2f+2.5f*t*t*t))*Mathf.Pow(1-t,2.2f)+t)*(1+1.2f*(1-t));
                return from+delta*u;
            case 23:return delta-Sample(0,delta,1-t,24)+from;
            case 24:
                if(t<.36363637f)return delta*(7.5625f*t*t)+from;
                if(t<.72727275f){u=t-.54545456f;return delta*(7.5625f*u*u+.75f)+from;}
                if((double)t<.9090909090909091){u=t-.8181818f;return delta*(7.5625f*u*u+.9375f)+from;}
                u=t-.95454544f;return delta*(7.5625f*u*u+.984375f)+from;
            case 25:
                return t<.5f?Sample(0,delta,t*2,23)*.5f+from:
                    Sample(0,delta,t*2-1,24)*.5f+delta*.5f+from;
            case 26:return delta*t*t*((1.70158f+1)*t-1.70158f)+from;
            case 27:u=t-1;return delta*(u*u*((1.70158f+1)*u+1.70158f)+1)+from;
            case 28:
                float s=1.70158f*1.525f;u=t/.5f;
                if(u<1)return delta/2*(u*u*((s+1)*u-s))+from;
                u-=2;return delta/2*(u*u*((s+1)*u+s)+2)+from;
            case 29:
                if(t==0)return from;if(t==1)return to;u=t-1;
                return -(delta*Mathf.Pow(2,10*u)*Mathf.Sin((u-.075f)*6.2831855f/.3f))+from;
            case 30:
                if(t==0)return from;if(t==1)return to;
                return delta*Mathf.Pow(2,-10*t)*Mathf.Sin((t-.075f)*6.2831855f/.3f)+delta+from;
            case 31:
                if(t==0)return from;if(t==1)return to;u=t/.5f-1;
                if(t<.5f)return -.5f*(delta*Mathf.Pow(2,10*u)*Mathf.Sin((u-.075f)*6.2831855f/.3f))+from;
                return delta*Mathf.Pow(2,-10*u)*Mathf.Sin((u-.075f)*6.2831855f/.3f)*.5f+delta+from;
            // Preserve the source's comparison against start/2, not time/2.
            case 33:return t>from/2?Sample(from,to,t,16):Sample(from,to,t,12);
            default:
                if(t==0)return from;if(t==1)return to;
                return delta*Mathf.Pow(2,-10*t)*Mathf.Sin((t-.15f)*6.2831855f/.6f)+delta+from;
        }
    }
    public static float Punch(float amplitude,float time)
    {
        if(time==0||time==1)return 0;
        return amplitude*Mathf.Pow(2,-10*time)*Mathf.Sin(time*6.2831855f/.3f);
    }
}
