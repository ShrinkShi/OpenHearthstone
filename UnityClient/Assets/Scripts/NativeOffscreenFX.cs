using System;
using UnityEngine;

[Serializable] public sealed class NativeOffscreenFXData
{
    public int owner,source,width,height;public bool enabled,use_bounds;
    public float above,below,force_size;public NativeFXViewport rect;
}
[Serializable] public sealed class NativeFXViewport
{public float x,y,width,height;public Rect Rect{get{return new Rect(x,y,width,height);}}}

// Original OffScreenRenderFX, distinct from the older RenderToTexture component.
// Original source geometry moves into its separately allocated capture region.
public sealed class NativeOffscreenFX : IDisposable
{
    static float nextY=250;
    readonly NativeOffscreenFXData data;
    readonly Transform owner,source;
    readonly Renderer target;
    readonly Action<Renderer,bool> setVisible;
    readonly Camera camera;
    readonly RenderTexture texture;
    readonly Bounds bounds;
    readonly Vector3 offset;
    Vector3 localCameraPosition;
    Quaternion localCameraRotation;
    float size;
    public RenderTexture Texture{get{return texture;}}
    public float Size{get{return size;}}
    public Camera CaptureCamera{get{return camera;}}
    public NativeOffscreenFX(NativeOffscreenFXData data,GameObject[] nodes,Action<Renderer,bool> setVisible)
    {
        this.data=data;this.setVisible=setVisible;
        if(data.owner<0||data.owner>=nodes.Length||data.source<0||data.source>=nodes.Length)throw new NotSupportedException("Original OffScreenRenderFX node binding is unrestored");
        owner=nodes[data.owner].transform;source=nodes[data.source].transform;
        target=owner.GetComponent<Renderer>();if(target==null)throw new NotSupportedException("Original offscreen target renderer is missing");
        var filter=owner.GetComponent<MeshFilter>();
        if(data.use_bounds&&(filter==null||filter.sharedMesh==null))throw new NotSupportedException("Original offscreen mesh bounds are missing");
        bounds=data.use_bounds?filter.sharedMesh.bounds:default(Bounds);
        if(data.width<=0||data.height<=0)throw new ArgumentOutOfRangeException("Original offscreen resolution");
        offset=new Vector3(250,nextY,0);nextY+=10;
        texture=RenderTexture.GetTemporary(data.width,data.height,24);
        var cameraObject=new GameObject("Original OffScreenFXCamera");camera=cameraObject.AddComponent<Camera>();
        camera.enabled=false;camera.orthographic=true;camera.allowHDR=false;camera.cullingMask=1<<30;
        camera.nearClipPlane=-data.above;camera.farClipPlane=data.below;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
        camera.targetTexture=texture;camera.rect=data.rect.Rect;
        target.sharedMaterial.mainTexture=texture;target.sharedMaterial.SetFloat("_HasSourceTexture",1);
    }
    public void Reset()
    {
        if(!data.enabled)return;
        source.position+=offset;setVisible(target,true);
        size=data.force_size!=0?data.force_size:owner.localScale.x*(data.use_bounds?bounds.size.x:1)/2;
        localCameraPosition=owner.InverseTransformPoint(source.position);
        localCameraRotation=Quaternion.Inverse(owner.rotation)*(source.rotation*Quaternion.Euler(90,180,0));
    }
    public void Render()
    {
        if(!data.enabled||!owner.gameObject.activeInHierarchy)return;
        if(size<=0)throw new NotSupportedException("Original offscreen orthographic size is invalid");
        var rotation=owner.rotation*localCameraRotation;
        var view=Matrix4x4.Scale(new Vector3(1,1,-1))*Matrix4x4.TRS(owner.TransformPoint(localCameraPosition),rotation,Vector3.one).inverse;
        float aspect=(float)data.width/data.height;
        camera.transform.SetPositionAndRotation(owner.TransformPoint(localCameraPosition),rotation);
        camera.worldToCameraMatrix=view;camera.orthographicSize=size;
        camera.projectionMatrix=Matrix4x4.Ortho(-size*aspect,size*aspect,-size,size,-data.above,data.below);
        camera.cullingMatrix=camera.projectionMatrix*view;
        var previous=RenderTexture.active;
        try{camera.Render();}finally{RenderTexture.active=previous;}
    }
    public void Dispose(){camera.targetTexture=null;AssetRegistry.ReleaseTransient(camera.gameObject);if(RenderTexture.active==texture)RenderTexture.active=null;RenderTexture.ReleaseTemporary(texture);}
}
