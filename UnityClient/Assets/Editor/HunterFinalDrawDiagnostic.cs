using System;
using System.IO;
using System.Linq;
using UnityEngine;

// Replays a captured Hunter TrailRenderer through the production affine draw;
// never starts a Player.
public static class HunterFinalDrawDiagnostic
{
    [Serializable] sealed class VisualSnapshot
    {
        public string owner;
        public int trailPositionCount,trailMeshVertices,trailMeshTriangles,layer;
        public bool enabled;
        public Vector3[] trailWorldPoints;
        public HunterRuntimeMaterialSnapshot[] materialState;
    }
    [Serializable] sealed class FlightSnapshot
    {
        public VisualSnapshot[] visuals;
        public int cameraCullingMask;
        public int sourceState,sourceStateChanges,propertyWrites,unhandledActions;
        public HunterActionCount[] actionExecutions;
        public string[] unhandledActionDetails;
    }
    [Serializable] sealed class DrawSnapshot
    {
        public int screenWidth,screenHeight;
        public Vector4 logicalRect;
        public float[] guiMatrix;
        public bool passesBound;
    }
    [Serializable] sealed class PrefabRow{public string reference,file;}
    [Serializable] sealed class PrefabCatalog{public PrefabRow[] prefabs;}
    [Serializable] sealed class DrawProgramNodes{public string reference;public NativeNode[] nodes;}
    [Serializable] sealed class PixelStats
    {
        public int width,height,colorPixels,alphaPixels,minX,minY,maxX,maxY,changedPixels,changedMinX,changedMinY,changedMaxX,changedMaxY;
        public float maxRgb,maxAlpha,maxDelta,meanDelta;
    }
    [Serializable] sealed class Report
    {
        public string result,acceptance,sourcePrefab,material,texture,drawRect,outputImage,error,materialStateError;
        public int sourceLayer,cameraCullingMask,bakedVertices,bakedTriangles,sourceVertices,sourceTriangles,screenWidth,screenHeight;
        public bool arrowEnabled,glowEnabled,passesBound,renderPassesBound,runtimeMaterialStateApplied;
        public int runtimeMaterialStateCount,sourceState,sourceStateChanges,propertyWrites,unhandledActions;
        public HunterActionCount[] actionExecutions;
        public float vertexAlphaMin,vertexAlphaMax,materialAlpha,materialRgbMax;
        public Vector2 uvMin,uvMax;
        public PixelStats emission,transmission,screenDifference;
    }
    static void Require(bool value,string message){if(!value)throw new InvalidOperationException("Hunter final draw diagnostic: "+message);}
    static string Argument(string name)
    {var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,name);return index>=0&&index+1<args.Length?args[index+1]:"";}
    static PixelStats ReadStats(RenderTexture target,Texture2D readback,float threshold)
    {
        RenderTexture.active=target;readback.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);readback.Apply(false,false);
        var pixels=readback.GetPixels();var result=new PixelStats{width=target.width,height=target.height,minX=target.width,minY=target.height,maxX=-1,maxY=-1};
        for(int i=0;i<pixels.Length;i++)
        {
            var color=pixels[i];float rgb=Mathf.Max(color.r,Mathf.Max(color.g,color.b));
            result.maxRgb=Mathf.Max(result.maxRgb,rgb);result.maxAlpha=Mathf.Max(result.maxAlpha,color.a);
            if(rgb>threshold){result.colorPixels++;int x=i%target.width,y=i/target.width;result.minX=Mathf.Min(result.minX,x);result.minY=Mathf.Min(result.minY,y);result.maxX=Mathf.Max(result.maxX,x);result.maxY=Mathf.Max(result.maxY,y);}
            if(color.a>threshold)result.alphaPixels++;
        }
        if(result.colorPixels==0){result.minX=-1;result.minY=-1;result.maxX=-1;result.maxY=-1;}
        return result;
    }
    static PixelStats Compare(Texture2D before,Texture2D after,float threshold)
    {
        var a=before.GetPixels();var b=after.GetPixels();var result=new PixelStats{width=before.width,height=before.height,
            changedMinX=before.width,changedMinY=before.height,changedMaxX=-1,changedMaxY=-1};double sum=0;
        for(int i=0;i<a.Length;i++)
        {
            float delta=Mathf.Max(Mathf.Abs(a[i].r-b[i].r),Mathf.Max(Mathf.Abs(a[i].g-b[i].g),Mathf.Abs(a[i].b-b[i].b)));
            result.maxDelta=Mathf.Max(result.maxDelta,delta);sum+=delta;
            if(delta>threshold){result.changedPixels++;int x=i%before.width,y=i/before.width;
                result.changedMinX=Mathf.Min(result.changedMinX,x);result.changedMinY=Mathf.Min(result.changedMinY,y);
                result.changedMaxX=Mathf.Max(result.changedMaxX,x);result.changedMaxY=Mathf.Max(result.changedMaxY,y);}
        }
        result.meanDelta=(float)(sum/Math.Max(1,a.Length));
        if(result.changedPixels==0){result.changedMinX=-1;result.changedMinY=-1;result.changedMaxX=-1;result.changedMaxY=-1;}
        return result;
    }
    static void SaveReadback(RenderTexture target,Texture2D readback,string path)
    {
        var png=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);
        try
        {
            RenderTexture.active=target;png.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);png.Apply(false,false);
            File.WriteAllBytes(path,png.EncodeToPNG());
        }
        finally{AssetRegistry.ReleaseTransient(png);}
    }
    public static void Run()
    {
        var previous=RenderTexture.active;AssetRegistry assets=null;NativeEffectInstance effect=null;NativeEffectPlayer compositor=null;NativeAffineCapture affine=null;
        RenderTexture screen=null;Texture2D board=null,beforePixels=null,afterPixels=null;Mesh baked=null;
        string project=Directory.GetParent(Application.dataPath).FullName;string repo=Directory.GetParent(project).FullName;
        string evidenceName=Argument("--hunter-final-draw-source");if(String.IsNullOrEmpty(evidenceName))evidenceName="f006b-hunter-target0j";
        if(Path.GetFileName(evidenceName)!=evidenceName||evidenceName.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
            throw new ArgumentException("Hunter evidence folder must be one work-directory name");
        string sourceFolder=Path.Combine(repo,"work",evidenceName);string outputFolder=Path.Combine(sourceFolder,"editor-final-draw");
        Directory.CreateDirectory(outputFolder);string reportPath=Path.Combine(outputFolder,"report.json");string materialStateError="";
        try
        {
            var flight=JsonUtility.FromJson<FlightSnapshot>(File.ReadAllText(Path.Combine(sourceFolder,"driver","hunter-flight-mid.json")));
            var draw=JsonUtility.FromJson<DrawSnapshot>(File.ReadAllText(Path.Combine(sourceFolder,"driver","hunter-gui-draw-mid.json")));
            var recordedTrail=flight.visuals.Single(item=>item.owner=="Arrow Trail");
            var recordedArrow=flight.visuals.Single(item=>item.owner=="Arrow");
            var recordedGlow=flight.visuals.Single(item=>item.owner=="ArrowGlowPlane");
            var root=Path.Combine(repo,"local");assets=new AssetRegistry(root);
            string programName=Argument("--hunter-final-draw-program");
            if(String.IsNullOrEmpty(programName)||Path.GetFileName(programName)!=programName||programName.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
                throw new ArgumentException("Hunter draw program must be a generated JSON filename in work/");
            string programPath=Path.Combine(repo,"work",programName);
            if(!File.Exists(programPath))throw new FileNotFoundException("Sanitized Hunter draw program is missing",programPath);
            var nodeData=JsonUtility.FromJson<DrawProgramNodes>(File.ReadAllText(programPath));
            var program=new NativeProgram{reference=nodeData.reference,nodes=nodeData.nodes,animations=new NativeAnimation[0],machines=new NativeMachine[0],
                captures=new NativeCapture[0],offscreen_fx=new NativeOffscreenFXData[0],timelines=new NativeTimeline[0],spell_states=new NativeSourceSpellState[0],
                animation_event_receivers=new NativeAnimationEventReceiver[0],spell_transforms=new NativeSourceSpellTransform[0],unknown_actions=new string[0]};
            Require(program.nodes.Length==12,"captured missile prefab node count changed");
            effect=new NativeEffectInstance(program,assets,0,1024,576,()=>Color.white);
            int trailIndex=Array.FindIndex(program.nodes,node=>node.name=="Arrow Trail");
            Require(trailIndex>=0,"exported Trail node was not found");
            var trail=effect.Nodes[trailIndex].GetComponent<TrailRenderer>();
            Require(trail!=null&&trail.enabled&&trail.sharedMaterial!=null,"source TrailRenderer did not initialize enabled with a material");
            Require(!recordedArrow.enabled&&!recordedGlow.enabled,"formal Player capture shows source-disabled Arrow/Glow renderers");
            Require(recordedTrail.materialState!=null&&recordedTrail.materialState.Length==trail.sharedMaterials.Length,
                "runtime material state is missing; recapture the formal Player flight before final-draw replay");
            var stateApplied=0;
            for(int i=0;i<recordedTrail.materialState.Length;i++)
            {
                string stateError;
                if(!recordedTrail.materialState[i].Apply(trail.sharedMaterials[i],assets,out stateError))
                {materialStateError=stateError;throw new InvalidOperationException("captured runtime material state cannot be replayed: "+stateError);}
                stateApplied++;
            }
            Require(recordedTrail.trailWorldPoints!=null&&recordedTrail.trailWorldPoints.Length==recordedTrail.trailPositionCount,
                "target0j snapshot is missing the captured TrailRenderer points");
            trail.Clear();trail.AddPositions(recordedTrail.trailWorldPoints);trail.emitting=false;
            baked=new Mesh();trail.BakeMesh(baked,effect.PreviewCamera,true);
            var colors=baked.colors;var uvs=baked.uv;
            var texture=trail.sharedMaterial.GetTexture("_MainTex") as Texture2D;
            Require(texture!=null&&trail.sharedMaterial.shader!=null,"actual TrailRenderer material lost its source MainTex or shader");
            var report=new Report{result="DIAGNOSTIC_ONLY",acceptance="FAIL pending new formal Player review: this replays captured Trail points and source-declared runtime material values through the production affine capture and final two-pass draw; it does not enable source-disabled Arrow/Glow renderers.",
                sourcePrefab=programPath,material=trail.sharedMaterial.name+" shader="+trail.sharedMaterial.shader.name+" supported="+trail.sharedMaterial.shader.isSupported+" passes="+trail.sharedMaterial.passCount+
                    " family="+trail.sharedMaterial.GetFloat("_OriginalFamily").ToString("R",System.Globalization.CultureInfo.InvariantCulture),
                texture=texture.name+" "+texture.width+"x"+texture.height,sourceLayer=trail.gameObject.layer,cameraCullingMask=effect.PreviewCamera.cullingMask,
                bakedVertices=baked.vertexCount,bakedTriangles=baked.triangles.Length/3,sourceVertices=recordedTrail.trailMeshVertices,sourceTriangles=recordedTrail.trailMeshTriangles,
                arrowEnabled=recordedArrow.enabled,glowEnabled=recordedGlow.enabled,vertexAlphaMin=colors.Length==0?0:colors.Min(c=>c.a),vertexAlphaMax=colors.Length==0?0:colors.Max(c=>c.a),
                uvMin=uvs.Length==0?Vector2.zero:new Vector2(uvs.Min(v=>v.x),uvs.Min(v=>v.y)),uvMax=uvs.Length==0?Vector2.zero:new Vector2(uvs.Max(v=>v.x),uvs.Max(v=>v.y)),
                materialAlpha=trail.sharedMaterial.GetColor("_Color").a,materialRgbMax=trail.sharedMaterial.GetColor("_Color").maxColorComponent,
                drawRect=draw.logicalRect.ToString(),screenWidth=draw.screenWidth,screenHeight=draw.screenHeight,passesBound=draw.passesBound,
                runtimeMaterialStateApplied=stateApplied==recordedTrail.materialState.Length,runtimeMaterialStateCount=stateApplied,
                sourceState=flight.sourceState,sourceStateChanges=flight.sourceStateChanges,propertyWrites=flight.propertyWrites,
                unhandledActions=flight.unhandledActions,actionExecutions=flight.actionExecutions,materialStateError=materialStateError};
            report.renderPassesBound=draw.passesBound;
            Require(baked.vertexCount>0&&baked.triangles.Length>0,"recorded trail points did not generate a TrailRenderer mesh");
            var sourceMaterial=program.nodes[trailIndex].materials.Single();
            affine=new NativeAffineCapture(1024,576);
            affine.Render(effect.PreviewCamera,new System.Collections.Generic.Dictionary<Material,NativeMaterial>{{trail.sharedMaterial,sourceMaterial}});
            var emission=affine.Color;var transmission=affine.Transmission;
            Require(transmission!=null,"production affine capture did not expose transmission RT");
            var floatReadback=new Texture2D(1024,576,TextureFormat.RGBAFloat,false,true);
            try{report.emission=ReadStats(emission,floatReadback,.0001f);report.transmission=ReadStats(transmission,floatReadback,.0001f);}
            finally{AssetRegistry.ReleaseTransient(floatReadback);}
            Require(report.emission.colorPixels>0,"captured trail produced no RT RGB");
            string boardPath=Path.Combine(sourceFolder,"driver","hunter-missile-mid.png");
            board=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            Require(board.LoadImage(File.ReadAllBytes(boardPath)),"target0j screenshot PNG did not load");
            Require(board.width==draw.screenWidth&&board.height==draw.screenHeight,"screenshot size differs from captured final Draw dimensions");
            screen=new RenderTexture(draw.screenWidth,draw.screenHeight,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);screen.Create();
            beforePixels=new Texture2D(draw.screenWidth,draw.screenHeight,TextureFormat.RGBAFloat,false,true);
            afterPixels=new Texture2D(draw.screenWidth,draw.screenHeight,TextureFormat.RGBAFloat,false,true);
            Graphics.Blit(board,screen);RenderTexture.active=screen;
            beforePixels.ReadPixels(new Rect(0,0,draw.screenWidth,draw.screenHeight),0,0,false);beforePixels.Apply(false,false);
            var matrix=new Matrix4x4();for(int i=0;i<16;i++)matrix[i]=draw.guiMatrix[i];
            compositor=new NativeEffectPlayer(assets);
            bool passes=compositor.RenderAffine(new Rect(draw.logicalRect.x,draw.logicalRect.y,draw.logicalRect.z,draw.logicalRect.w),
                emission,transmission,matrix,draw.screenWidth,draw.screenHeight,Color.white);
            Require(passes,"production affine material failed to bind one or more final Draw passes");
            RenderTexture.active=screen;afterPixels.ReadPixels(new Rect(0,0,draw.screenWidth,draw.screenHeight),0,0,false);afterPixels.Apply(false,false);
            report.screenDifference=Compare(beforePixels,afterPixels,.005f);
            Require(report.screenDifference.changedPixels>0,"production final Draw changed no framebuffer pixels");
            string outputPath=Path.Combine(outputFolder,"target0j-final-draw-overlay.png");SaveReadback(screen,afterPixels,outputPath);report.outputImage=outputPath;
            report.renderPassesBound=passes;
            File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
            Debug.Log("HUNTER FINAL DRAW DIAGNOSTIC: RT RGB="+report.emission.colorPixels+" alpha="+report.emission.alphaPixels+
                " final changed pixels="+report.screenDifference.changedPixels+" bounds="+report.screenDifference.changedMinX+","+
                report.screenDifference.changedMinY+".."+report.screenDifference.changedMaxX+","+report.screenDifference.changedMaxY+
                " image="+outputPath);
        }
        catch(Exception error)
        {
            File.WriteAllText(reportPath,"{\"result\":\"DIAGNOSTIC_ERROR\",\"error\":"+JsonUtility.ToJson(new ErrorReport{message=error.ToString()})+"}");
            Debug.LogException(error);throw;
        }
        finally
        {
            RenderTexture.active=previous;compositor?.Dispose();affine?.Dispose();effect?.Dispose();
            AssetRegistry.ReleaseTransient(baked);AssetRegistry.ReleaseTransient(screen);AssetRegistry.ReleaseTransient(board);
            AssetRegistry.ReleaseTransient(beforePixels);AssetRegistry.ReleaseTransient(afterPixels);
        }
    }
    [Serializable] sealed class ErrorReport{public string message;}
}
