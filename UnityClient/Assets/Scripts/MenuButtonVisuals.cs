using System.Collections.Generic;
using UnityEngine;

// 图集中心 alpha 是原 Shader 参数；普通 GUI 透明混合会把按钮正文掏空。
public sealed class MenuButtonVisuals
{
    readonly Dictionary<string,Texture2D> cache=new Dictionary<string,Texture2D>();
    public Texture2D Image(AssetRegistry assets,bool red)
    {
        string id=red?"UI_CONCEDE_BUTTON":"UI_MENU_ACTION";
        Texture2D result;if(cache.TryGetValue(id,out result))return result;
        var source=assets.Texture(id);var uv=red?new Rect(.26f,.47f,.5f,.21f):new Rect(.025f,.535f,.48f,.21f);
        int x=Mathf.RoundToInt(uv.x*source.width),y=Mathf.RoundToInt(uv.y*source.height);
        int w=Mathf.RoundToInt(uv.width*source.width),h=Mathf.RoundToInt(uv.height*source.height);
        var pixels=source.GetPixels(x,y,w,h);
        if(!red)for(int j=8;j<h-8;j++)for(int i=15;i<w-15;i++)pixels[j*w+i].a=1;
        result=new Texture2D(w,h,TextureFormat.RGBA32,false){name=id+"-face",wrapMode=TextureWrapMode.Clamp};
        result.SetPixels(pixels);result.Apply();cache[id]=result;return result;
    }
    public void Draw(Rect rect,Texture image)
    {
        float cap=Mathf.Min(rect.width*.25f,rect.height*.48f);
        GUI.DrawTextureWithTexCoords(new Rect(rect.x,rect.y,cap,rect.height),image,new Rect(0,0,.2f,1),true);
        GUI.DrawTextureWithTexCoords(new Rect(rect.x+cap,rect.y,rect.width-2*cap,rect.height),image,new Rect(.2f,0,.6f,1),true);
        GUI.DrawTextureWithTexCoords(new Rect(rect.xMax-cap,rect.y,cap,rect.height),image,new Rect(.8f,0,.2f,1),true);
    }
    public void Dispose(){foreach(var image in cache.Values)AssetRegistry.ReleaseTransient(image);cache.Clear();}
}
