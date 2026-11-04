using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

[Serializable] public sealed class NativePropertyDescriptor
{
    public int target_node;public string target_variable,target_type,property_name;
    public bool target_null,target_unresolved,set_property;
    public NativeAssetSource target_source;public NativeParam[] values;
}

public static class NativePropertyAction
{
    public static Action Restore(UnityEngine.Object target,string name)
    {
        var particle=target as ParticleSystem;
        if(particle!=null)
        {
            var main=particle.main;var emission=particle.emission;
            switch(name)
            {
                case "startSize":var size=main.startSize;return ()=>{if(particle!=null){var m=particle.main;m.startSize=size;}};
                case "startSpeed":var speed=main.startSpeed;return ()=>{if(particle!=null){var m=particle.main;m.startSpeed=speed;}};
                case "startLifetime":var life=main.startLifetime;return ()=>{if(particle!=null){var m=particle.main;m.startLifetime=life;}};
                case "startRotation":var rotation=main.startRotation;return ()=>{if(particle!=null){var m=particle.main;m.startRotation=rotation;}};
                case "startColor":var color=main.startColor;return ()=>{if(particle!=null){var m=particle.main;m.startColor=color;}};
                case "gravityModifier":var gravity=main.gravityModifier;return ()=>{if(particle!=null){var m=particle.main;m.gravityModifier=gravity;}};
                case "emissionRate":var rate=emission.rateOverTime;return ()=>{if(particle!=null){var e=particle.emission;e.rateOverTime=rate;}};
            }
        }
        var property=target.GetType().GetProperty(name,BindingFlags.Public|BindingFlags.Instance);
        if(property==null||!property.CanRead||!property.CanWrite)throw new NotSupportedException("Original property restoration is unrestored");
        var initial=property.GetValue(target,null);
        return ()=>{if(target!=null)property.SetValue(target,initial,null);};
    }
    static readonly Type[] types={typeof(GameObject),typeof(Transform),typeof(ParticleSystem),typeof(Renderer),typeof(MeshRenderer),typeof(SkinnedMeshRenderer),typeof(ParticleSystemRenderer),typeof(TrailRenderer),typeof(Camera),typeof(Light),typeof(AudioSource),typeof(Rigidbody),typeof(Collider),typeof(BoxCollider),typeof(SphereCollider)};
    public static UnityEngine.Object Target(NativePropertyDescriptor data,GameObject owner)
    {
        var type=types.FirstOrDefault(t=>t.FullName==data.target_type.Split(',')[0].Trim());
        if(type==null)throw new NotSupportedException("Original property component type is unrestored: "+data.target_type);
        var target=type==typeof(GameObject)?(UnityEngine.Object)owner:owner.GetComponent(type);
        if(target==null)throw new NotSupportedException("Original property component has not been created");
        return target;
    }
    public static bool Set(NativePropertyDescriptor data,UnityEngine.Object target,Func<NativeParam,Type,object> value)
    {
        if(target==null)return false;
        // Nested/member-array reflection and original custom script types are
        // separate restoration work; never substitute a guessed field.
        if(String.IsNullOrEmpty(data.property_name)||data.property_name.Contains('.'))throw new NotSupportedException("Original nested property path is unrestored");
        var type=target.GetType();var property=type.GetProperty(data.property_name,BindingFlags.Public|BindingFlags.Instance);
        var field=property==null?type.GetField(data.property_name,BindingFlags.Public|BindingFlags.Instance):null;
        if(property==null&&field==null||property!=null&&!property.CanWrite)throw new NotSupportedException("Original writable member is unrestored");
        var memberType=property!=null?property.PropertyType:field.FieldType;
        string parameter=memberType==typeof(float)?"FloatParameter":memberType==typeof(int)?"IntParameter":memberType==typeof(bool)?"BoolParameter":
            memberType==typeof(string)?"StringParameter":memberType==typeof(Vector2)?"Vector2Parameter":memberType==typeof(Vector3)?"Vector3Parameter":
            memberType==typeof(Color)?"ColorParameter":memberType==typeof(Quaternion)?"QuaternionParameter":memberType==typeof(Rect)?"RectParamater":null;
        if(parameter==null)throw new NotSupportedException("Original property value type is unrestored: "+memberType.FullName);
        var selected=(data.values??new NativeParam[0]).SingleOrDefault(p=>p.name==parameter);
        if(selected==null||selected.unresolved||!selected.none_known)throw new NotSupportedException("Original property value/None metadata is unrestored");
        if(selected.is_none)return false;
        var result=value(selected,memberType);
        if(property!=null)property.SetValue(target,result,null);else field.SetValue(target,result);
        return true;
    }
}
