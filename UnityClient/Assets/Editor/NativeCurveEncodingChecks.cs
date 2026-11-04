using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class NativeCurveEncodingProbe : ScriptableObject { public AnimationCurve curve; }
public static class NativeCurveEncodingChecks
{
    public static void Run()
    {
        var probe=ScriptableObject.CreateInstance<NativeCurveEncodingProbe>();
        try
        {
            using(var writer=new StreamWriter("../work/native-curve-encoding.txt"))
            foreach(var mode in new[]{WrapMode.Default,WrapMode.Once,WrapMode.Loop,WrapMode.PingPong,WrapMode.ClampForever})
            {
                probe.curve=AnimationCurve.Linear(0,0,1,1);probe.curve.preWrapMode=mode;probe.curve.postWrapMode=mode;
                string json=EditorJsonUtility.ToJson(probe);writer.WriteLine(mode+" "+json);
                int serialized=mode==WrapMode.Loop?1:mode==WrapMode.PingPong?0:mode==WrapMode.Default?3:2;
                if(!json.Contains("\"m_PostInfinity\":"+serialized))throw new Exception("Unity infinity mapping changed");
                var parameter=new NativeParam{curve=new[]{new NativeKey{time=0,value=0,outSlope=1},new NativeKey{time=1,value=1,inSlope=1}},wrap_known=true,pre_wrap=serialized,post_wrap=serialized};
                float expected=probe.curve.Evaluate(1.25f);float actual=parameter.SampleCurve(1.25f);writer.WriteLine("sample "+expected+" / "+actual);
                if(Mathf.Abs(actual-expected)>.0001f)throw new Exception("Native infinity sampling differs: "+mode+" "+expected+" / "+actual);
            }
        }
        finally{UnityEngine.Object.DestroyImmediate(probe);}
    }
}
