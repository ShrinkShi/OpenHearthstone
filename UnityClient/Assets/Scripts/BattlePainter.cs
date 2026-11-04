using UnityEngine;

public static class BattlePainter
{
    static Texture2D glow,ring,shadow,arrowHead,buttonGlow;
    public static Texture ArrowTexture{get{if(arrowHead==null)arrowHead=Mask(3);return arrowHead;}}
    static Texture2D Mask(int kind)
    {
        var t=new Texture2D(128,128,TextureFormat.RGBA32,false);var pixels=new Color[128*128];
        for(int y=0;y<128;y++)for(int x=0;x<128;x++)
        {
            float d=new Vector2((x-63.5f)/63.5f,(y-63.5f)/63.5f).magnitude;
            float alpha=kind==0?Mathf.Exp(-d*d*5)*Mathf.Clamp01((1-d)*8):kind==1?Mathf.Exp(-Mathf.Pow((d-.83f)*35,2)):kind==3?Mathf.Clamp01(((1-y/127f)*.9f-Mathf.Abs((x-63.5f)/63.5f))*64):Mathf.Exp(-d*d*3)*Mathf.Clamp01((1-d)*5);
            pixels[y*128+x]=new Color(1,1,1,alpha);
        }
        t.SetPixels(pixels);t.Apply(false,true);return t;
    }
    public static void Glow(Rect r,Color color){if(glow==null)glow=Mask(0);Draw(r,glow,color);}
    public static void Ring(Rect r,Color color){if(ring==null)ring=Mask(1);Draw(r,ring,color);}
    public static void ButtonGlow(Rect r,Color color)
    {
        if(buttonGlow==null)
        {
            buttonGlow=new Texture2D(256,128,TextureFormat.RGBA32,false);var pixels=new Color[256*128];
            for(int y=0;y<128;y++)for(int x=0;x<256;x++)
            {
                var q=new Vector2(Mathf.Abs(x-127.5f)-94,Mathf.Abs(y-63.5f)-30);
                float d=new Vector2(Mathf.Max(q.x,0),Mathf.Max(q.y,0)).magnitude+Mathf.Min(Mathf.Max(q.x,q.y),0)-16;
                float a=Mathf.Exp(-d*d/42)*.68f+Mathf.Exp(-d*d/5)*.5f;
                pixels[y*256+x]=new Color(1,1,1,a);
            }
            buttonGlow.SetPixels(pixels);buttonGlow.Apply(false,true);
        }
        float xPad=r.width*18/220,yPad=r.height*18/92;
        Draw(new Rect(r.x-xPad,r.y-yPad,r.width+2*xPad,r.height+2*yPad),buttonGlow,color);
    }
    public static void Shadow(Rect r,float alpha=.65f){if(shadow==null)shadow=Mask(2);Draw(r,shadow,new Color(0,0,0,alpha));}
    public static void Draw(Rect r,Texture texture,Color color)
    {
        if(texture==null)return;var old=GUI.color;GUI.color=old*color;GUI.DrawTexture(r,texture,ScaleMode.StretchToFill,true);GUI.color=old;
    }
    public static void TargetArrow(Vector2 from,Vector2 to)
    {
        var delta=to-from;if(delta.magnitude<35)return;
        var head=ArrowTexture;
        var color=new Color(1,.19f,.055f,.92f);var matrix=GUI.matrix;
        int count=Mathf.Clamp(Mathf.FloorToInt(delta.magnitude/25),2,36);
        for(int i=1;i<count;i++)
        {
            float t=(float)i/count;var point=BattleGeometry.ArrowPoint(from,to,t);
            var tangent=BattleGeometry.ArrowTangent(from,to,t);
            BattleGeometry.Rotate(Mathf.Atan2(tangent.y,tangent.x)*Mathf.Rad2Deg,point);
            Draw(new Rect(point.x-9,point.y-5,18,10),Texture2D.whiteTexture,color);GUI.matrix=matrix;
        }
        Ring(new Rect(to.x-30,to.y-30,60,60),color);
        var end=BattleGeometry.ArrowTangent(from,to,1);
        BattleGeometry.Rotate(Mathf.Atan2(end.y,end.x)*Mathf.Rad2Deg+90,to);
        Draw(new Rect(to.x-20,to.y,40,38),head,color);GUI.matrix=matrix;
    }
}
