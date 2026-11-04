using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

// Reflection targets this installed Unity Editor's asset-authoring API only.
// No official Hearthstone assemblies are loaded or executed.
public static class NativeMixerBuilder
{
    [Serializable] sealed class Group{public string name;public int index,parent;public float volume,pitch;public bool mute,solo,bypass;}
    [Serializable] sealed class Effect{public string name;public int index,group,target;public bool bypass;public float mix;public float[] parameters;}
    [Serializable] sealed class Program{public string name,snapshot;public bool suspend;public float threshold;public int update_mode;public Group[] groups;public Effect[] effects;}
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    static Type Type(string name){return typeof(Editor).Assembly.GetType("UnityEditor.Audio."+name,true);}
    static object Call(object owner,string name,params object[] args)
    {
        var type=owner as Type??owner.GetType();var method=type.GetMethods(Flags).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length);
        return method.Invoke(owner is Type?null:owner,args);
    }
    static object Get(object owner,string name){return owner.GetType().GetProperty(name,Flags).GetValue(owner,null);}
    static void Set(object owner,string name,object value){owner.GetType().GetProperty(name,Flags).SetValue(owner,value,null);}
    static Array Typed(Type type,object[] values){var array=Array.CreateInstance(type,values.Length);for(int i=0;i<values.Length;i++)array.SetValue(values[i],i);return array;}
    public static void Build()
    {
        var source=JsonUtility.FromJson<Program>(File.ReadAllText("../local/runtime/native-mixer-program.json"));
        const string path="Assets/LocalAudio/LegendaryDucking.mixer";
        Directory.CreateDirectory("Assets/LocalAudio");AssetDatabase.Refresh();
        if(AssetDatabase.LoadMainAssetAtPath(path)!=null)AssetDatabase.DeleteAsset(path);
        var controller=Call(Type("AudioMixerController"),"CreateMixerControllerAtPath",path);((UnityEngine.Object)controller).name=source.name;
        var snapshot=Get(controller,"TargetSnapshot");((UnityEngine.Object)snapshot).name=source.snapshot;
        var groups=new object[source.groups.Length];groups[0]=Get(controller,"masterGroup");
        for(int i=1;i<groups.Length;i++)groups[i]=Call(controller,"CreateNewGroup",source.groups[i].name,false);
        int checkedValues=0;
        for(int i=0;i<groups.Length;i++)
        {
            var data=source.groups[i];((UnityEngine.Object)groups[i]).name=data.name;
            Set(groups[i],"children",Typed(Type("AudioMixerGroupController"),source.groups.Where(g=>g.parent==i).Select(g=>groups[g.index]).ToArray()));
            Set(groups[i],"mute",data.mute);Set(groups[i],"solo",data.solo);Set(groups[i],"bypassEffects",data.bypass);
            Call(groups[i],"SetValueForVolume",controller,snapshot,data.volume);Call(groups[i],"SetValueForPitch",controller,snapshot,data.pitch);
            if((float)Call(groups[i],"GetValueForVolume",controller,snapshot)!=data.volume||(float)Call(groups[i],"GetValueForPitch",controller,snapshot)!=data.pitch)throw new Exception("Original group snapshot mismatch");checkedValues+=2;
        }
        var effects=new object[source.effects.Length];
        foreach(var data in source.effects)
        {
            if(data.name=="Attenuation")effects[data.index]=((Array)Get(groups[data.group],"effects")).GetValue(0);
            else
            {
                var effect=Activator.CreateInstance(Type("AudioMixerEffectController"),Flags,null,new object[]{data.name},null);
                Call(controller,"AddNewSubAsset",effect,false);Call(effect,"PreallocateGUIDs");effects[data.index]=effect;
            }
            Set(effects[data.index],"bypass",data.bypass);
        }
        for(int i=1;i<groups.Length;i++)Set(groups[i],"effects",Typed(Type("AudioMixerEffectController"),source.effects.Where(e=>e.group==i).Select(e=>effects[e.index]).ToArray()));
        foreach(var data in source.effects)
        {
            var definitions=(Array)Call(Type("MixerEffectDefinitions"),"GetEffectParameters",data.name);
            if(definitions.Length!=data.parameters.Length)throw new Exception("Source effect parameter layout differs: "+data.name);
            for(int i=0;i<definitions.Length;i++)
            {
                var definition=definitions.GetValue(i);string parameter=(string)definition.GetType().GetField("name",Flags).GetValue(definition);
                Call(effects[data.index],"SetValueForParameter",controller,snapshot,parameter,data.parameters[i]);
                float actual=(float)Call(effects[data.index],"GetValueForParameter",controller,snapshot,parameter);
                Debug.Log("Original mixer parameter "+data.index+" "+parameter+" expected="+data.parameters[i]+" actual="+actual);
                if(Mathf.Abs(actual-data.parameters[i])>0.000001f)throw new Exception("Original effect parameter mismatch: "+parameter+" expected="+data.parameters[i]+" actual="+actual);checkedValues++;
            }
        }
        foreach(var data in source.effects.Where(e=>e.target>=0))
        {Set(effects[data.index],"sendTarget",effects[data.target]);Call(effects[data.index],"SetValueForMixLevel",controller,snapshot,data.mix);checkedValues++;}
        var serialized=new SerializedObject((UnityEngine.Object)controller);
        serialized.FindProperty("m_SuspendThreshold").floatValue=source.threshold;serialized.FindProperty("m_EnableSuspend").boolValue=source.suspend;serialized.ApplyModifiedPropertiesWithoutUndo();
        ((AudioMixer)controller).updateMode=(AudioMixerUpdateMode)source.update_mode;
        foreach(var obj in AssetDatabase.LoadAllAssetsAtPath(path))EditorUtility.SetDirty(obj);
        AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var loaded=AssetDatabase.LoadAssetAtPath<AudioMixer>(path);if(loaded.FindMatchingGroups("").Length!=groups.Length)throw new Exception("Original mixer group count mismatch");
        string runtime=Path.GetFullPath("../local/runtime");
        var bundle=BuildPipeline.BuildAssetBundles(runtime,new[]{new AssetBundleBuild{assetBundleName="native-audio-mixer.assetbundle",assetNames=new[]{path}}},BuildAssetBundleOptions.None,BuildTarget.StandaloneWindows64);
        if(bundle==null)throw new Exception("Native mixer bundle build failed");
        File.WriteAllText("../work/native-mixer-build.json","{\"groups\":"+groups.Length+",\"effects\":"+effects.Length+",\"values_checked\":"+checkedValues+",\"audible_verified\":false}");
        Debug.Log("Original mixer rebuilt: "+groups.Length+" groups, "+source.effects.Length+" source effects, "+checkedValues+" values checked");
    }
}
