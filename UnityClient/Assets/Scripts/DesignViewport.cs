using UnityEngine;

// 核心按高度等比缩放；宽窗口只展开有限背景，极窄窗口保证核心操作区可见。
public struct DesignViewport
{
    public readonly float Width,Height,Scale,WindowWidth,WindowHeight;
    public readonly Rect Pixels;
    public readonly Matrix4x4 Matrix;
    public DesignViewport(float windowWidth,float windowHeight,float width,float height,float coreWidth,float maxAspect=2.5f)
    {
        WindowWidth=Mathf.Max(1,windowWidth);WindowHeight=Mathf.Max(1,windowHeight);Width=width;Height=height;
        Scale=Mathf.Min(WindowHeight/height,WindowWidth/coreWidth);
        float h=height*Scale,w=Mathf.Min(WindowWidth,h*maxAspect);
        Pixels=new Rect((WindowWidth-w)/2,(WindowHeight-h)/2,w,h);
        Matrix=Matrix4x4.TRS(new Vector3((WindowWidth-width*Scale)/2,(WindowHeight-height*Scale)/2,0),Quaternion.identity,new Vector3(Scale,Scale,1));
    }
    public static DesignViewport Battle{get{return new DesignViewport(Screen.width,Screen.height,1920,1080,1520);}}
    public static DesignViewport Menu{get{return new DesignViewport(Screen.width,Screen.height,1600,1000,1120);}}
    public void Apply(Camera camera)
    {
        camera.rect=new Rect(Pixels.x/WindowWidth,Pixels.y/WindowHeight,Pixels.width/WindowWidth,Pixels.height/WindowHeight);
        camera.aspect=Pixels.width/Pixels.height;
        camera.ResetProjectionMatrix();
    }
    public Vector2 Project(Camera camera,Vector3 point)
    {
        var clip=camera.projectionMatrix*camera.worldToCameraMatrix*new Vector4(point.x,point.y,point.z,1);
        var p=new Vector2((clip.x/clip.w+1)*.5f,(clip.y/clip.w+1)*.5f);
        var screen=new Vector2(Pixels.x+p.x*Pixels.width,Pixels.y+(1-p.y)*Pixels.height);
        return Matrix.inverse.MultiplyPoint3x4(screen);
    }
    public static Matrix4x4 Fit(float width,float height)
    {
        float scale=Mathf.Min(Screen.width/width,Screen.height/height);
        return Matrix4x4.TRS(new Vector3((Screen.width-width*scale)/2,(Screen.height-height*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
    }
}
