using System;
using UnityEngine;

// 原英雄头像分片飞散；无临时 GameObject，材质只有一个且随客户端释放。
public sealed class HeroFragments : IDisposable
{
    readonly Material material=new Material(Resources.Load<Shader>("HeroFragment"));
    public static Rect Fragment(Rect hero,int index,float progress)
    {
        int column=index%4,row=index/4;float t=Tween.Curve((progress-.14f)/.72f,Ease.In);
        var rect=new Rect(hero.x+column*hero.width/4,hero.y+row*hero.height/5,hero.width/4+.6f,hero.height/5+.6f);
        var offset=rect.center-hero.center;
        rect.position+=offset*t*2.7f+new Vector2(Mathf.Sin(index*17.3f)*70,Mathf.Cos(index*9.7f)*45)*t+Vector2.up*(250*t*t);
        return rect;
    }
    public void Draw(Rect hero,Texture portrait,float progress)
    {
        if(Event.current.type!=EventType.Repaint||portrait==null)return;
        Render(hero,portrait,progress,GUI.matrix,Screen.width,Screen.height);
    }
    public void Render(Rect hero,Texture portrait,float progress,Matrix4x4 parent,int width,int height)
    {
        for(int i=0;i<20;i++)
        {
            var rect=Fragment(hero,i,progress);
            var matrix=parent*BattleGeometry.Rotation(Mathf.Sin(i*3.7f)*150*Mathf.Clamp01((progress-.14f)/.86f),rect.center);
            material.mainTexture=portrait;material.SetVector("_UVRect",new Vector4(i%4/4f,1-(i/4+1)/5f,.25f,.2f));
            material.SetFloat("_Break",Mathf.Clamp01((progress-.12f)/.13f));
            material.SetColor("_Tint",new Color(1,1,1,1-Tween.Curve((progress-.42f)/.58f,Ease.In)));
            GL.PushMatrix();
            try
            {
                GL.LoadPixelMatrix(0,width,height,0);GL.MultMatrix(matrix);material.SetPass(0);GL.Begin(GL.QUADS);
                GL.TexCoord2(0,1);GL.Vertex3(rect.xMin,rect.yMin,0);GL.TexCoord2(1,1);GL.Vertex3(rect.xMax,rect.yMin,0);
                GL.TexCoord2(1,0);GL.Vertex3(rect.xMax,rect.yMax,0);GL.TexCoord2(0,0);GL.Vertex3(rect.xMin,rect.yMax,0);GL.End();
            }
            finally{GL.PopMatrix();}
        }
    }
    public void Dispose(){AssetRegistry.ReleaseTransient(material);}
}
