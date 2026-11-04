using System;
using UnityEngine;

[Serializable] public sealed class NativeCameraTransform
{public Vector3 position,scale;public Quaternion rotation;}
[Serializable] public sealed class NativeSourceCamera
{
    public string name;public int projection_mode;public bool orthographic;
    public float size,fov,near,far;public NativeCameraTransform[] chain;
    public Vector3 Position
    {
        get
        {
            Matrix4x4 world=Matrix4x4.identity;
            for(int i=chain.Length-1;i>=0;i--)world=world*Matrix4x4.TRS(chain[i].position,chain[i].rotation,chain[i].scale);
            return world.MultiplyPoint3x4(Vector3.zero);
        }
    }
    public Quaternion Rotation
    {
        get{Quaternion world=Quaternion.identity;for(int i=chain.Length-1;i>=0;i--)world=world*chain[i].rotation;return world;}
    }
    // Source and destination origins are supplied by scene ownership, not inferred
    // from a card rectangle or bounds. Capture clear/layers remain pass-owned.
    public void Apply(Camera camera,Vector3 sourceOrigin,Vector3 destinationOrigin,float aspect)
    {
        if(projection_mode!=1)throw new NotSupportedException("Original physical/custom camera projection is unrestored");
        if(chain==null||chain.Length==0||aspect<=0||float.IsNaN(aspect)||float.IsInfinity(aspect))
            throw new ArgumentException("Invalid original camera input");
        camera.usePhysicalProperties=false;camera.ResetProjectionMatrix();camera.ResetWorldToCameraMatrix();
        camera.orthographic=orthographic;camera.orthographicSize=size;camera.fieldOfView=fov;
        camera.nearClipPlane=near;camera.farClipPlane=far;camera.aspect=aspect;
        camera.transform.position=Position-sourceOrigin+destinationOrigin;camera.transform.rotation=Rotation;
    }
}
[Serializable] public sealed class NativeSourceCameras{public NativeSourceCamera[] cameras;}
