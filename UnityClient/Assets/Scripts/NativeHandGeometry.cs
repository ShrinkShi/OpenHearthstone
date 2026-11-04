using System.Collections.Generic;
using UnityEngine;

// ZoneHand geometry in source world coordinates. Host scene values are explicit;
// this is not a conversion to the client's calibrated GUI coordinate space.
public sealed class NativeHandGeometry
{
    public Vector3 Center,EnlargedScale;
    public float MaxWidth,HeroWidth,PhoneEnlargedSpacing;
    public float HoverHeightZ,SelectOffsetZ,TinyHoverY,TinyHoverZ;
    public bool Enemy,Revealed,Enlarged,Targeting,Phone;
    public int CardCount,CardsToDraw;
    public IList<int> ReservedSlots;
    public bool Reserving;

    public int VisualCount { get { return CardCount+(Reserving?ReservedSlots.Count:0); } }
    public int Slot(int cardIndex)
    {
        if(Reserving)foreach(int reserved in ReservedSlots)if(reserved<=cardIndex)cardIndex++;
        return cardIndex;
    }
    void CountAndScrunch(int requested,out int count,out bool scrunched)
    {
        count=requested>=0?requested:VisualCount;
        scrunched=count>3;
        if(!Enemy)count-=CardsToDraw;
    }
    public float Spacing(int count)
    {
        float spacing=Phone&&Enlarged?PhoneEnlargedSpacing:1.270804f;
        float width=MaxWidth-(Enlarged&&Targeting?HeroWidth:0);
        if(spacing*count>width)spacing=width/count;
        return spacing;
    }
    float RotationY(int slot,int count,bool scrunched)
    {
        float rotation=Enemy&&Revealed?180:0;
        if(count<=1||!scrunched)return rotation;
        float spread=40+(Enemy?0:count*2);
        float offset=spread/(count-1)*slot-spread/2;
        return rotation+(Enemy?-offset:offset);
    }
    public Vector3 Rotation(int slot,int requested=-1)
    {
        int count;bool scrunched;CountAndScrunch(requested,out count,out scrunched);
        return new Vector3(0,RotationY(slot,count,scrunched),352.5f);
    }
    public Vector3 Position(int slot,int requested=-1)
    {
        int count;bool scrunched;CountAndScrunch(requested,out count,out scrunched);
        float halfSpacing=Spacing(count)/2;
        float x=Center.x+(slot*2-(count-1))*halfSpacing;
        if(Enlarged&&Targeting)
        {
            if(count%2>0){if(slot<(count+1)/2)x-=HeroWidth;}
            else x+=slot<count/2?-HeroWidth/2:HeroWidth/2;
        }
        float bend=Mathf.Pow(slot+.5f-count/2f,2)/(6f*count);
        if(!scrunched)bend=0;
        float rotation=RotationY(slot,count,scrunched),correction=0;
        if((Enemy&&rotation<0)||(!Enemy&&rotation>0))
            correction=Mathf.Sin(Mathf.Abs(rotation*0.017453292f))*halfSpacing;
        float z=Center.z+(Enemy?bend+correction:-(bend+correction));
        if(Enemy&&Revealed)z-=.2f;
        return new Vector3(x,Center.y,z);
    }
    public Vector3 Scale()
    {
        if(Enemy)return new Vector3(.682f,.125f,.682f);
        return Phone?EnlargedScale:new Vector3(.62f,.125f,.62f);
    }
    public Vector3 HoverPosition(int slot)
    {
        bool tiny=Phone&&!Enlarged;
        return new Vector3(Position(slot).x,Center.y+1+(tiny?TinyHoverY:0),
            HoverHeightZ+SelectOffsetZ+(tiny?TinyHoverZ:0));
    }
}
