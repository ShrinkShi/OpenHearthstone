using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NativeSourceCameraChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original source camera: "+message);passed++;}
    public static void Run()
    {
        passed=0;var definitions=JsonUtility.FromJson<NativeSourceCameras>(File.ReadAllText("../local/runtime/original-battle-cameras.json"));
        var main=definitions.cameras.Single(c=>c.name=="Main Camera");
        var camera=new GameObject("Source authored camera check").AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;
        try
        {
            Check(main.chain.Length==1&&main.Position==new Vector3(1092.5f,32,6.1f),"real scene Main Camera world translation");
            foreach(var view in definitions.cameras)
            {
                Check(Vector3.Distance(view.Position,main.Position)<.00001f&&Quaternion.Angle(view.Rotation,main.Rotation)<.0001f,"camera parent chain reproduces main pose "+view.name);
                view.Apply(camera,new Vector3(1092.5f,0,0),Vector3.zero,16f/9);
                Check(!camera.orthographic&&camera.fieldOfView==view.fov&&camera.nearClipPlane==view.near&&camera.farClipPlane==view.far,"authored projection and clipping "+view.name);
            }
            main.Apply(camera,new Vector3(1092.5f,0,0),Vector3.zero,16f/9);
            Check(EditorJsonUtility.ToJson(camera).Contains("\"m_projectionMatrixMode\":1"),"local Unity ordinary FOV mode matches original serialized enum");
            camera.projectionMatrix=Matrix4x4.identity;main.Apply(camera,new Vector3(1092.5f,0,0),Vector3.zero,16f/9);
            Check(camera.projectionMatrix!=Matrix4x4.identity,"source FOV binding clears a previous custom projection");
            camera.worldToCameraMatrix=Matrix4x4.identity;main.Apply(camera,new Vector3(1092.5f,0,0),Vector3.zero,16f/9);
            Check(camera.worldToCameraMatrix!=Matrix4x4.identity,"source pose binding clears a previous custom view matrix");
            Check(Vector3.Distance(camera.transform.position,new Vector3(0,32,6.1f))<.00001f&&camera.cullingMask==1<<30,"explicit world rebasing preserves capture layers");
            var point=camera.transform.position+camera.transform.forward*32;
            var projected=camera.WorldToViewportPoint(point);
            Check(Vector2.Distance(new Vector2(projected.x,projected.y),Vector2.one*.5f)<.00001f,"authored perspective optical axis projects to center");
            var edge=point+camera.transform.right*(32*Mathf.Tan(main.fov*Mathf.Deg2Rad*.5f)*16/9);
            Check(Mathf.Abs(camera.WorldToViewportPoint(edge).x-1)<.00001f,"source FOV and output aspect define horizontal coverage");
            bool rejected=false;main.projection_mode=2;
            try{main.Apply(camera,Vector3.zero,Vector3.zero,16f/9);}catch(NotSupportedException){rejected=true;}
            Check(rejected,"physical/custom projection does not silently become ordinary perspective");main.projection_mode=1;
        }
        finally{AssetRegistry.ReleaseTransient(camera.gameObject);}
        var assets=new AssetRegistry(Path.GetFullPath("../local"));
        // Find the approved original program by registry rather than a guessed ID.
        var index=JsonUtility.FromJson<NativeEffectIndex>(File.ReadAllText(Path.Combine(assets.Root,"native-effect-index.json")));
        var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,index.entries.Single(e=>e.card_id=="CS2_025").file)));
        Camera owned=null;
        using(var effect=new NativeEffectInstance(program,assets,0,512,288,()=>Color.white))
        {
            // Diagnostic anchor along the original optical axis; this is not the
            // restored gameplay board origin or a matching official game state.
            var origin=main.Position+main.Rotation*Vector3.forward*32;
            owned=effect.Root.GetComponentInChildren<Camera>();
            effect.BindCaptureView(main,origin,effect.Root.transform.position);
            var position=owned.transform.position;var initialRoot=effect.Root.transform.position;
            effect.Root.transform.position+=Vector3.right;
            Check(owned.transform.parent==null&&owned.transform.position==position,"source scene camera remains fixed when Spell parent moves");
            effect.Root.transform.position=initialRoot;
            var capture=effect.Render(.3f);var previous=RenderTexture.active;
            var image=new Texture2D(512,288,TextureFormat.RGBA32,false);
            try
            {
                RenderTexture.active=capture;image.ReadPixels(new Rect(0,0,512,288),0,0);image.Apply();
                Check(image.GetPixels32().Count(p=>Math.Max(p.r,Math.Max(p.g,p.b))>3)>128,"actual original arcane graph visible through authored perspective capture");
                Directory.CreateDirectory("../tests/visual/current/native-camera");
                File.WriteAllBytes("../tests/visual/current/native-camera/arcane-perspective.png",image.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;AssetRegistry.ReleaseTransient(image);}
            bool rejected=false;
            try{effect.BindCaptureView(main,origin,effect.Root.transform.position);}catch(InvalidOperationException){rejected=true;}
            Check(rejected,"capture binding after playback is rejected");
        }
        Check(owned==null,"detached owned capture camera is destroyed with the effect");
        Debug.Log("NATIVE SOURCE CAMERA: "+passed+" checks passed; authored capture projection only, runtime board origin and viewport scripts remain unrestored");
        NativeEffectPlacementChecks.Run();
    }
}
