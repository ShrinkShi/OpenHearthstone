using System;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class ClientOptions
{
    public int version = 1;
    public int resolutionWidth;
    public int resolutionHeight;
    public int qualityLevel;
    public int targetFrameRate = 60;
    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public bool fullscreen;
    public bool screenShake = true;
    public bool runInBackground = true;

    public static ClientOptions Load(string profilePath)
    {
        var defaults = new ClientOptions
        {
            resolutionWidth = Screen.width,
            resolutionHeight = Screen.height,
            qualityLevel = QualitySettings.GetQualityLevel(),
            fullscreen = Screen.fullScreen
        };
        var path = FilePath(profilePath);
        if (!File.Exists(path)) return defaults;

        try
        {
            var saved = JsonUtility.FromJson<ClientOptions>(File.ReadAllText(path));
            if (saved == null || saved.version != 1) return defaults;
            saved.masterVolume = ClampGain(saved.masterVolume, defaults.masterVolume);
            saved.musicVolume = ClampGain(saved.musicVolume, defaults.musicVolume);
            if (saved.resolutionWidth < 640 || saved.resolutionHeight < 480)
            {
                saved.resolutionWidth = defaults.resolutionWidth;
                saved.resolutionHeight = defaults.resolutionHeight;
            }
            int qualityCount = QualitySettings.names.Length;
            saved.qualityLevel = qualityCount == 0 ? defaults.qualityLevel :
                Mathf.Clamp(saved.qualityLevel, 0, qualityCount - 1);
            saved.targetFrameRate = ClosestFrameRate(saved.targetFrameRate);
            return saved;
        }
        catch (Exception error)
        {
            Debug.LogWarning("客户端选项无法读取，沿用运行时默认值：" + error.Message);
            return defaults;
        }
    }

    public bool Save(string profilePath, out string error)
    {
        error = "";
        string path = FilePath(profilePath);
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(profilePath);
            File.WriteAllText(temp, JsonUtility.ToJson(this, true));
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { }
            return false;
        }
    }

    static string FilePath(string profilePath)
    {
        return Path.Combine(profilePath, "client-options.json");
    }

    static float ClampGain(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
    }

    static int ClosestFrameRate(int value)
    {
        int[] rates = { 30, 60, 120, 144 };
        int best = rates[0];
        foreach (int rate in rates)
            if (Math.Abs(rate - value) < Math.Abs(best - value)) best = rate;
        return best;
    }
}
