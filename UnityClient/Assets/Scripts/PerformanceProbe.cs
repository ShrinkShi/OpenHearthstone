using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class PerformanceProbe
{
    [Serializable] public class Report
    {
        public int samples,queue_peak,collections,texture_count,card_render_count;
        public float mean_fps,p95_frame_ms,managed_mb;
    }
    readonly List<float> frames=new List<float>(3600);
    float age,nextWrite=10;
    int queuePeak;
    public void Tick(float delta,int queue,string path,int textures,int renders)
    {
        age+=delta;if(age<5)return;
        queuePeak=Mathf.Max(queuePeak,queue);if(frames.Count<3600)frames.Add(delta);
        if(age<nextWrite)return;nextWrite+=10;
        var sorted=frames.ToArray();Array.Sort(sorted);float total=0;foreach(float t in sorted)total+=t;
        var report=new Report{samples=sorted.Length,mean_fps=sorted.Length/Mathf.Max(.001f,total),
            p95_frame_ms=sorted[Mathf.Min(sorted.Length-1,Mathf.FloorToInt(sorted.Length*.95f))]*1000,
            managed_mb=GC.GetTotalMemory(false)/(1024f*1024),collections=GC.CollectionCount(0),queue_peak=queuePeak,
            texture_count=textures,card_render_count=renders};
        File.WriteAllText(path,JsonUtility.ToJson(report,true));
    }
}
