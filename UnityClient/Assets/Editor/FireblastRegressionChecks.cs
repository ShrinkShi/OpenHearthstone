using System;
using System.Reflection;
using UnityEngine;

// 归档原探针原样临时复制到Editor；此入口不修改其断言或验收归档。
public static class FireblastRegressionChecks
{
    public static void Run()
    {
        FireblastVisualChecks.Run();
        for(int round=1;round<=5;round++)
        {
            var name="V003R"+round+"Checks";var type=typeof(FireblastRegressionChecks).Assembly.GetType(name,true);
            try{type.GetMethod("Run",BindingFlags.Static|BindingFlags.Public).Invoke(null,null);}
            catch(TargetInvocationException error){throw error.InnerException??error;}
        }
        NativeSuperSpellChecks.RunFinal();
        Debug.Log("FIREBLAST SHARED REGRESSION completed; archived assertions unchanged");
    }
}
