using UnityEngine;

// 原粒子图的黑色区域用加法混合；不会作为不透明矩形盖在棋盘上。
public sealed class SpellParticles
{
    readonly Material material=new Material(Resources.Load<Shader>("BattleParticle"));
    public void Draw(Rect rect,Texture texture,Color tint,bool softMask=false,CardEffectSprite settings=null)
    {
        if(texture==null||Event.current.type!=EventType.Repaint)return;
        Render(rect,texture,GUI.color*tint,GUI.matrix,Screen.width,Screen.height,softMask,settings);
    }
    public void Render(Rect rect,Texture texture,Color tint,Matrix4x4 matrix,int width,int height,bool softMask=false,CardEffectSprite settings=null)
    {
        if(texture==null)return;
        material.mainTexture=texture;material.SetColor("_Tint",tint);
        material.SetFloat("_Threshold",softMask?.94f:0);material.SetFloat("_SoftMask",softMask||settings!=null&&settings.soft_mask?1:0);
        material.SetFloat("_KeyBackground",settings!=null&&settings.key_background?1:0);
        material.SetColor("_Background",settings==null?Color.black:settings.background);
        GL.PushMatrix();
        try
        {
            GL.LoadPixelMatrix(0,width,height,0);GL.MultMatrix(matrix);material.SetPass(0);GL.Begin(GL.QUADS);
            GL.TexCoord2(0,1);GL.Vertex3(rect.xMin,rect.yMin,0);
            GL.TexCoord2(1,1);GL.Vertex3(rect.xMax,rect.yMin,0);
            GL.TexCoord2(1,0);GL.Vertex3(rect.xMax,rect.yMax,0);
            GL.TexCoord2(0,0);GL.Vertex3(rect.xMin,rect.yMax,0);GL.End();
        }
        finally{GL.PopMatrix();}
    }
    public void Dispose(){AssetRegistry.ReleaseTransient(material);}
}
