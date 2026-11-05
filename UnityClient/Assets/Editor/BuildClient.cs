using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildClient
{
    public static void Build()
    {
        BuildLocalFonts();
        if(File.Exists("../local/runtime/native-mixer-program.json"))NativeMixerBuilder.Build();
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Resources");
        if (!File.Exists("Assets/Resources/AssetSurface.mat"))
            AssetDatabase.CreateAsset(new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/AssetSurface.shader")),"Assets/Resources/AssetSurface.mat");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Client").AddComponent<ClientApp>();
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Main.unity");
        PlayerSettings.companyName = "LocalLab";
        PlayerSettings.productName = "HearthstoneClone";
        PlayerSettings.runInBackground = true;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.defaultIsNativeResolution = false;
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        var report = BuildPipeline.BuildPlayer(new[] {"Assets/Scenes/Main.unity"},
            "../build/client/HearthstoneClone.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new Exception(report.summary.result.ToString());
    }
    public static void BuildValidated()
    {
        PresentationChecks.Run();
        NativeShaderTimeChecks.Run();
        UICalibrationChecks.Run();
        F024HubMeshChecks.Run();
        F024AccountStateChecks.Run();
        F024BattleChecks.Run();
        NativeWeaponAudioChecks.Run();
        BattleInteractionChecks.Run();
        FeedbackChecks.Run();
        CardInputChecks.Run();
        CombatFeedbackChecks.Run();
        WindowFeedbackChecks.Run();
        AssociatedPresentationChecks.Run();
        InteractionLifecycleChecks.Run();
        if(File.Exists("../local/runtime/native-effect-index.json")){NativeEffectChecks.Run();HunterHeroPowerChecks.Run();}
        Build();
    }
    public static void BuildLocalFonts()
    {
        string runtime=Path.GetFullPath("../local/runtime");
        Directory.CreateDirectory("Assets/LocalFonts");
        var names=new[]{"BelweNumbers","UGCSH00M","gkai00m","GLEI00M_t_Outline","BlizzardGlobal"};
        foreach(string name in names)
        {
            string path="Assets/LocalFonts/"+name+".ttf";
            File.Copy(Path.Combine(runtime,name+".ttf"),path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer=(TrueTypeFontImporter)AssetImporter.GetAtPath(path);importer.includeFontData=true;importer.SaveAndReimport();
            if(name=="GLEI00M_t_Outline"||name=="BlizzardGlobal")
            {
                importer.characterPadding=name=="GLEI00M_t_Outline"?4:1;importer.characterSpacing=1;importer.fontRenderingMode=FontRenderingMode.Smooth;importer.SaveAndReimport();
                var settings=new SerializedObject(importer);var legacy=settings.FindProperty("useLegacyBoundsCalculation")??settings.FindProperty("m_UseLegacyBoundsCalculation");
                if(legacy!=null){legacy.boolValue=true;settings.ApplyModifiedPropertiesWithoutUndo();importer.SaveAndReimport();}
                else Debug.LogWarning("Original font legacy bounds importer setting unavailable: "+name);
            }
        }
        BuildPipeline.BuildAssetBundles(runtime,new[]{new AssetBundleBuild{assetBundleName="local-fonts.assetbundle",
            assetNames=Array.ConvertAll(names,n=>"Assets/LocalFonts/"+n+".ttf")}},BuildAssetBundleOptions.None,BuildTarget.StandaloneWindows64);
    }
}
