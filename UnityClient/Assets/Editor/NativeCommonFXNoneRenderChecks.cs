using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeCommonFXNoneRenderChecks
{
    [Serializable] sealed class Reference {public string file;}
    [Serializable] sealed class Manifest {public Reference[] references;}
    public static void Run()
    {
        var source=JsonUtility.FromJson<Manifest>(File.ReadAllText("../manifests/native-commonfx-none-variants.json"));
        NativeColorAlphaRenderChecks.CaptureFiles(source.references.Select(r=>r.file.Replace(".native-effect.json","")).ToArray(),"native-commonfx-none",19);
    }
}
