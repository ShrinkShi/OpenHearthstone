using System;
using System.IO;
using System.Linq;
using UnityEngine;

public static class NativeColorAlphaRenderChecks
{
    [Serializable] sealed class Sample
    {
        public string file;
        public float time;
        public int visible_pixels,black_alpha_pixels,particles;
    }
    [Serializable] sealed class Report
    {
        public Sample[] samples;
        public bool full_card_effects_verified=false;
    }
    public static void Run()
    {
        string[] files={"13e4ba2cda5d567ad3d3e0be","81adc1ee5e4a815a745824b5","8b9080000adab86469ff8b06","9ac81ae7be2944e54fd04367","c53d28f7c2d01da67780e3fd","ce9e2ba5c3e2aa3061331d02"};
        CaptureFiles(files,"native-color-alpha",18);
    }
    public static void CaptureFiles(string[] files,string scenario,int family)
    {
        string folder=Path.GetFullPath("../tests/visual/current/"+scenario);Directory.CreateDirectory(folder);
        var assets=new AssetRegistry(Path.GetFullPath("../local"));var samples=new System.Collections.Generic.List<Sample>();
        var previous=RenderTexture.active;
        try
        {
            foreach(string file in files)
            {
                var program=JsonUtility.FromJson<NativeProgram>(File.ReadAllText(Path.Combine(assets.Root,file+".native-effect.json")));
                using(var instance=new NativeEffectInstance(program,assets,0,512,512))
                {
                    if(!instance.Nodes.SelectMany(n=>n.GetComponents<Renderer>()).SelectMany(r=>r.sharedMaterials).Any(m=>m.GetFloat("_OriginalFamily")==family))
                        throw new Exception("Original material family "+family+" not bound: "+file);
                    foreach(float time in new[]{.3f,1f,2f})
                    {
                        var frame=instance.Render(time);RenderTexture.active=frame;
                        var image=new Texture2D(frame.width,frame.height,TextureFormat.RGBA32,false,true);
                        try
                        {
                            image.ReadPixels(new Rect(0,0,frame.width,frame.height),0,0);image.Apply();
                            var sample=new Sample{file=file,time=time,particles=instance.ParticleCount};
                            foreach(var pixel in image.GetPixels32())
                            {
                                if(pixel.a<=8)continue;
                                if(Math.Max(pixel.r,Math.Max(pixel.g,pixel.b))>8)sample.visible_pixels++;
                                else sample.black_alpha_pixels++;
                            }
                            samples.Add(sample);
                            File.WriteAllBytes(Path.Combine(folder,file+"-"+time.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png"),image.EncodeToPNG());
                        }
                        finally{AssetRegistry.ReleaseTransient(image);}
                    }
                }
            }
        }
        finally{RenderTexture.active=previous;}
        File.WriteAllText(Path.Combine(folder,"render-report.json"),JsonUtility.ToJson(new Report{samples=samples.ToArray()},true));
        Debug.Log("SOURCE "+scenario+" PREFAB SAMPLES: "+samples.Count+"; full effects unverified");
    }
}
