using System;
using UnityEngine;

[Serializable] public sealed class NativeSourceSpellTransform
{
    public string kind,location_transform;public int node,location,facing;
    public bool enabled,parent_to_location,rotate_x,rotate_y,source_null;
    public NativeSuperRawConfig raw;
    public NativeSourceSpellTransform Copy(){return (NativeSourceSpellTransform)MemberwiseClone();}
}

// Spell.UpdatePosition/UpdateOrientation clear dirty even when resolution fails.
// Resolvers bind original location/facing enums to actual client scene objects.
public sealed class NativeSpellTransform
{
    readonly NativeSourceSpellTransform source;readonly Transform owner;
    readonly Func<int,GameObject> locations,facings;
    public bool PositionDirty{get;private set;}=true;
    public bool OrientationDirty{get;private set;}=true;
    public int Unresolved{get;private set;}
    public int PositionUpdates{get;private set;}
    public int OrientationUpdates{get;private set;}
    public string Kind{get{return source.kind;}}
    public int Location{get{return source.location;}}
    public int Facing{get{return source.facing;}}
    public NativeSpellTransform(NativeSourceSpellTransform source,Transform owner,Func<int,GameObject> locations,Func<int,GameObject> facings)
    {this.source=source;this.owner=owner;this.locations=locations;this.facings=facings;}
    public void SetPosition(Vector3 value,bool local=false)
    {if(local)owner.localPosition=value;else owner.position=value;PositionDirty=false;}
    public void SetOrientation(Quaternion value,bool local=false)
    {if(local)owner.localRotation=value;else owner.rotation=value;OrientationDirty=false;}
    public void ClearPositionDirty(){PositionDirty=false;}
    // Original SpellBase property setters do not re-arm dirty flags.
    public void SetLocationFields(int location,bool parent){source.location=location;source.parent_to_location=parent;}
    public void SetFacingFields(int facing,bool rotateX,bool rotateY){source.facing=facing;source.rotate_x=rotateX;source.rotate_y=rotateY;}
    public void InvalidatePosition(){PositionDirty=true;}
    public void InvalidateOrientation(){OrientationDirty=true;}
    public void ForceUpdate(){PositionDirty=true;Update();}
    public void Update()
    {
        UpdatePosition();UpdateOrientation();
    }
    public void UpdatePosition()
    {
        if(PositionDirty)
        {
            if(source.location!=0)
            {
                var location=locations==null?null:locations(source.location);
                if(location==null)Unresolved++;
                else
                {
                    string name=(source.location_transform??"").Trim();
                    var child=String.IsNullOrEmpty(name)?null:NativeActorIdentity.FindChildBySubstring(location,name);
                    var target=child!=null?child.transform:location.transform;
                    if(source.parent_to_location)owner.SetParent(target,true);
                    owner.position=target.position;PositionUpdates++;
                }
            }
            PositionDirty=false;
        }
    }
    public void UpdateOrientation()
    {
        if(OrientationDirty)
        {
            if(source.facing!=0)
            {
                var target=facings==null?null:facings(source.facing);
                if(target==null||source.facing<1||source.facing>13)Unresolved++;
                else
                {
                    bool same=source.facing==1||source.facing==7||source.facing==8;
                    bool opposite=source.facing==10||source.facing==11||source.facing==12;
                    var origin=same||opposite?target.transform.position:owner.position;
                    var end=same?origin+target.transform.forward:opposite?origin-target.transform.forward:target.transform.position;
                    Orient(owner,origin,end,source.rotate_x,source.rotate_y);OrientationUpdates++;
                }
            }
            OrientationDirty=false;
        }
    }
    public static void Orient(Transform owner,Vector3 origin,Vector3 target,bool rotateX,bool rotateY)
    {
        if(!rotateX||!rotateY)
        {if(rotateX)target.x=origin.x;else if(rotateY)target.y=origin.y;else return;}
        var direction=target-origin;
        if(direction.sqrMagnitude>Mathf.Epsilon)owner.rotation=Quaternion.LookRotation(direction);
    }
}
