using System;
using UnityEngine;

public static class NativeHandGeometryChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original hand geometry: "+message);passed++;}
    static bool Near(float a,float b){return Mathf.Abs(a-b)<.00001f;}
    public static void Run()
    {
        passed=0;var hand=new NativeHandGeometry{Center=new Vector3(3,4,5),MaxWidth=9,HeroWidth=2};
        for(int count=1;count<=10;count++)
        {
            hand.CardCount=count;float previous=float.NegativeInfinity;
            for(int slot=0;slot<count;slot++)
            {
                Vector3 p=hand.Position(slot);Check(!float.IsNaN(p.x)&&!float.IsNaN(p.z)&&p.y==4&&p.x>previous,"finite ordered source world positions count "+count);previous=p.x;
            }
            Check(Near((hand.Position(0).x+hand.Position(count-1).x)/2,3),"horizontal center count "+count);
            Check(Near(hand.Rotation(0).y,count>3?-(40+2*count)/2f:0),"original fan threshold and spread count "+count);
        }
        hand.CardCount=4;hand.CardsToDraw=1;
        Check(Near(hand.Rotation(0).y,-23),"scrunch uses visual count before pending draw subtraction");
        Check(Near(hand.Position(0).x,3-1.270804f),"pending draw affects spacing count");
        hand.Enemy=true;Check(Near(hand.Rotation(0).y,20),"enemy keeps pending draws and reverses fan");
        hand.Revealed=true;Check(Near(hand.Rotation(0).y,200),"revealed enemy adds 180 degrees");
        float revealedZ=hand.Position(0).z;hand.Revealed=false;Check(Near(hand.Position(0).z-revealedZ,.2f),"revealed enemy depth offset");
        hand.Enemy=false;hand.CardsToDraw=0;hand.Enlarged=true;hand.Targeting=true;
        Check(Near(hand.Spacing(10),.7f),"targeting reserves hero width before spacing clamp");
        Check(Near(hand.Position(0,2).x,3-1.270804f/2-1)&&Near(hand.Position(1,2).x,3+1.270804f/2+1),"even targeting splits hero gap");
        Check(Near(hand.Position(0,3).x,3-1.270804f-2)&&Near(hand.Position(1,3).x,1)&&Near(hand.Position(2,3).x,3+1.270804f),"odd targeting uses asymmetric source gap");
        hand.Phone=true;hand.PhoneEnlargedSpacing=.9f;hand.EnlargedScale=new Vector3(1,2,3);
        Check(Near(hand.Spacing(3),.9f)&&hand.Scale()==new Vector3(1,2,3),"phone source spacing and scale supplied explicitly");
        hand.Phone=false;Check(hand.Scale()==new Vector3(.62f,.125f,.62f),"desktop source scale");
        hand.Enemy=true;Check(hand.Scale()==new Vector3(.682f,.125f,.682f),"enemy source scale");
        hand.Reserving=true;hand.ReservedSlots=new[]{0,2};hand.CardCount=3;
        Check(hand.VisualCount==5&&hand.Slot(0)==1&&hand.Slot(1)==3,"reserved slots shift sequentially and count visually");
        Check(hand.Rotation(0,1).y==0,"explicit count overrides visual count");
        hand.HoverHeightZ=7;hand.SelectOffsetZ=.4f;hand.TinyHoverY=.2f;hand.TinyHoverZ=.3f;
        Check(hand.HoverPosition(0)==new Vector3(hand.Position(0).x,5,7.4f),"desktop hover uses explicit world height marker, not fan depth");
        hand.Phone=true;hand.Enlarged=false;
        Check(hand.HoverPosition(0)==new Vector3(hand.Position(0).x,5.2f,7.7f),"tiny phone hover applies source offsets");
        hand.Enlarged=true;
        Check(hand.HoverPosition(0)==new Vector3(hand.Position(0).x,5,7.4f),"enlarged phone hover excludes tiny offsets");
        Debug.Log("NATIVE HAND GEOMETRY: "+passed+" checks passed; original world geometry only, scene projection and layout Tweens remain unrestored");
        NativeZoneLayoutChecks.Run();
    }
}
