using System.Linq;
using System.Collections.Generic;
using UnityEngine;

public sealed class BoardPresentation
{
    readonly GameObject board;
    readonly Camera camera;
    readonly Transform surface;
    readonly Bounds baseBounds;
    readonly Dictionary<Transform,Vector3> positions=new Dictionary<Transform,Vector3>(),scales=new Dictionary<Transform,Vector3>();
    readonly Dictionary<Transform,Quaternion> rotations=new Dictionary<Transform,Quaternion>();
    int appliedRevision=-1;
    public BoardPresentation(GameObject board,Camera camera)
    {
        this.board=board;this.camera=camera;
        surface=board.GetComponentsInChildren<Transform>(true).First(t=>t.name=="PlaySurface_Combined");
        baseBounds=surface.GetComponent<Renderer>().bounds;
        foreach(var t in board.GetComponentsInChildren<Transform>(true)){positions[t]=t.localPosition;scales[t]=t.localScale;rotations[t]=t.localRotation;}
        // Combined 是未激活的合并备选；当前桌面由八块 Projected 网格拼成，保留它们。
    }
    public void Show(Vector2 shake)
    {
        Show(shake,DesignViewport.Battle);
    }
    public void Show(Vector2 shake,DesignViewport viewport)
    {
        Bounds b=baseBounds;
        float aspect=BattleLayout.Width/BattleLayout.Height;
        float field=43;
        // 用桌面几何推导相机高度，桌面的水平跨度占设计画面 69%。
        float distance=b.size.x/(2*Mathf.Tan(field*Mathf.Deg2Rad/2)*aspect*.69f);
        var focus=b.center+Vector3.forward*.1f;
        viewport.Apply(camera);camera.fieldOfView=field;
        camera.transform.position=focus+new Vector3(shake.x*.012f,distance,shake.y*.012f);
        // 原网格已含边缘厚度和道具透视；从正上方取景，避免再次倾斜压缩桌面。
        camera.transform.rotation=Quaternion.Euler(90,0,0);
        var calibration=UICalibration.Current;
        var edit=calibration==null?new UIAdjustment():calibration.Get("scene.camera");
        var background=calibration==null?new UIAdjustment():calibration.Get("scene.background");
        camera.transform.position+=new Vector3(edit.x*.015f,edit.y*.015f,0);
        camera.transform.rotation*=Quaternion.Euler(0,0,-edit.angle-background.angle);
        camera.ResetProjectionMatrix();var projection=camera.projectionMatrix;
        // 桌面水平跨度略收紧，纵向取景独立，贴近桌面版的高纵横比例。
        projection[0,0]*=.92f*edit.scaleX*background.scaleX*(edit.flipX?-1:1)*(background.flipX?-1:1);
        projection[1,1]*=1.035f*edit.scaleY*background.scaleY*(edit.flipY?-1:1)*(background.flipY?-1:1);
        projection[0,2]-=2*background.x*viewport.Scale/viewport.Pixels.width;projection[1,2]+=2*background.y*viewport.Scale/viewport.Pixels.height;camera.projectionMatrix=projection;
        if(calibration!=null)ApplyNodes(calibration);
    }
    void ApplyNodes(UICalibration calibration)
    {
        if(appliedRevision==calibration.Revision)return;appliedRevision=calibration.Revision;
        foreach(var t in positions.Keys)
        {
            string id=t==board.transform?"scene.board":"scene.node/"+UICalibration.NodePath(t,board.transform);var edit=calibration.Get(id);
            t.localPosition=positions[t]+new Vector3(edit.x*.015f,0,-edit.y*.015f);
            t.localRotation=rotations[t]*Quaternion.Euler(0,edit.angle,0);
            t.localScale=Vector3.Scale(scales[t],new Vector3(edit.scaleX*(edit.flipX?-1:1),1,edit.scaleY*(edit.flipY?-1:1)));
        }
    }
    public void RegisterEditorTargets()
    {
        var calibration=UICalibration.Current;if(calibration==null||!calibration.Collecting)return;
        calibration.Register("scene.camera","相机 · 平移 / 旋转 / 取景",new Rect(0,0,1920,1080),Vector2.one,false);
        calibration.Register("scene.board","整个棋盘模型",new Rect(280,85,1390,925),Vector2.one,false);
        calibration.Register("scene.background","整个战斗背景 · 含桌面 / 缩放取景",new Rect(0,0,1920,1080),Vector2.one,false);
        foreach(var node in positions.Keys.Where(t=>t!=board.transform&&t.GetComponent<MeshRenderer>()!=null&&t.gameObject.activeInHierarchy))
        {
            var b=node.GetComponent<Renderer>().bounds;
            var center=DesignViewport.Battle.Project(camera,b.center);
            // 部件列表可精确选择；框仅表示当前投影的近似占位。
            var r=new Rect(center.x-45,center.y-45,90,90);
            calibration.Register("scene.node/"+UICalibration.NodePath(node,board.transform),"棋盘 · "+node.name,r,Vector2.one,false);
        }
    }
}
