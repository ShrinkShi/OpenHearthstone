using System;
using System.IO;
using UnityEngine;

public static class NativeSpellTransformChecks
{
    static int passed;
    static void Check(bool value,string message){if(!value)throw new Exception("Original Spell placement: "+message);passed++;}
    static bool Same(Vector3 a,Vector3 b){return (a-b).sqrMagnitude<.000001f;}
    public static void Run()
    {
        passed=0;var parent=new GameObject("Placement parent");var owner=new GameObject("Placement spell");
        var location=new GameObject("Original location");var child=new GameObject("CenterPointBone");child.transform.SetParent(location.transform,false);
        try
        {
            parent.transform.position=new Vector3(4,3,2);parent.transform.rotation=Quaternion.Euler(12,30,8);parent.transform.localScale=new Vector3(2,3,4);
            owner.transform.SetParent(parent.transform,false);owner.transform.rotation=Quaternion.Euler(5,12,17);
            location.transform.position=new Vector3(20,5,-3);child.transform.localPosition=new Vector3(1,2,3);
            var source=new NativeSourceSpellTransform{location=2,location_transform=" CenterPoint ",rotate_x=true,rotate_y=true};
            var playback=new NativeSpellTransform(source,owner.transform,n=>location,null);var rotation=owner.transform.rotation;var scale=owner.transform.lossyScale;
            playback.Update();Check(Same(owner.transform.position,child.transform.position),"trimmed substring original location");
            Check(owner.transform.parent==parent.transform&&Quaternion.Angle(rotation,owner.transform.rotation)<.001f,"unparented location preserves orientation");
            Check(Same(scale,owner.transform.lossyScale)&&!playback.PositionDirty&&!playback.OrientationDirty,"scale preserved and dirty flags consumed");
            location.transform.position+=Vector3.right*5;playback.Update();Check(playback.PositionUpdates==1,"clean placement does not continuously follow target");
            playback.ForceUpdate();Check(Same(owner.transform.position,child.transform.position)&&playback.PositionUpdates==2,"force updates only position");
            playback.SetPosition(Vector3.one,true);location.transform.position+=Vector3.up;playback.Update();Check(Same(owner.transform.localPosition,Vector3.one),"explicit local setter clears position dirty");
            source.parent_to_location=true;playback.ForceUpdate();Check(owner.transform.parent==child.transform&&Same(owner.transform.position,child.transform.position)&&Same(scale,owner.transform.lossyScale),"source parent assignment preserves world scale");
            source.location_transform="missing bone";playback.ForceUpdate();Check(owner.transform.parent==location.transform&&Same(owner.transform.position,location.transform.position),"missing named child falls back to resolved object");
            var absent=new NativeSpellTransform(source,owner.transform,n=>null,null);absent.Update();absent.Update();Check(absent.Unresolved==1&&!absent.PositionDirty,"failed resolution clears dirty without retry");
            var none=new NativeSpellTransform(new NativeSourceSpellTransform(),owner.transform,null,null);none.Update();Check(none.Unresolved==0,"None location and facing retain pose");
            owner.transform.SetParent(null,true);owner.transform.position=Vector3.zero;location.transform.position=new Vector3(3,4,5);location.transform.rotation=Quaternion.Euler(15,33,7);
            foreach(int facing in new[]{1,7,8,10,11,12,2,3,4,5,6,9,13})
            {
                var spec=new NativeSourceSpellTransform{facing=facing,rotate_x=true,rotate_y=true};
                var orientation=new NativeSpellTransform(spec,owner.transform,null,n=>location);orientation.Update();
                var expected=facing==1||facing==7||facing==8?location.transform.forward:facing==10||facing==11||facing==12?-location.transform.forward:location.transform.position.normalized;
                Check(Same(owner.transform.forward,expected)&&orientation.OrientationUpdates==1,"authored facing group "+facing);
            }
            NativeSpellTransform.Orient(owner.transform,Vector3.zero,new Vector3(3,4,5),true,false);Check(Mathf.Abs(owner.transform.forward.x)<.0001f&&owner.transform.forward.y>0,"RotateX-only projects x");
            NativeSpellTransform.Orient(owner.transform,Vector3.zero,new Vector3(3,4,5),false,true);Check(Mathf.Abs(owner.transform.forward.y)<.0001f&&owner.transform.forward.x>0,"RotateY-only projects y");
            rotation=owner.transform.rotation;NativeSpellTransform.Orient(owner.transform,Vector3.zero,Vector3.zero,true,true);NativeSpellTransform.Orient(owner.transform,Vector3.zero,Vector3.one,false,false);Check(Quaternion.Angle(rotation,owner.transform.rotation)<.001f,"zero direction and disabled rotation preserve pose");
        }
        finally{AssetRegistry.ReleaseTransient(owner);AssetRegistry.ReleaseTransient(location);AssetRegistry.ReleaseTransient(parent);}
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var external=new GameObject("Explicit source hero binding");
        external.transform.position=new Vector3(7,4,-2);
        try
        {
            var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,"ba5220bc610280125378e669.actor-spell.json")));
            var effect=new NativeEffectInstance(program,assets,0,32,32);var originalRoot=effect.Nodes[0];
            try
            {
                effect.BindSourcePlacement(n=>n==3?external:null);effect.SelectSourceSpellState(1,0);
                Check(originalRoot.transform.parent==external.transform&&Same(originalRoot.transform.position,external.transform.position),"actual parent-to-location source prefab");
                Check(effect.UnboundSourcePlacements==0,"actual original hero location resolved explicitly");
            }
            finally{effect.Dispose();}
            Check(originalRoot==null&&external!=null,"disposing source instance destroys reparented owned root and preserves external actor");
        }
        finally{AssetRegistry.ReleaseTransient(external);}
        Debug.Log("NATIVE SPELL TRANSFORM: "+passed+" checks passed; original object selection, zones and complete controller remain incomplete");
        NativeActorAttackSoundChecks.Run();
    }
}
