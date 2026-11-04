using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

// 原画缺失时调用只读导出器；规则线程与画面不会等待资源解析。
public sealed class RuntimeAssetLoader : IDisposable
{
    readonly string dataRoot, project;
    readonly HashSet<string> requested=new HashSet<string>();
    readonly List<string> queue=new List<string>();
    Process process;
    float nextAttempt;
    public RuntimeAssetLoader(string data)
    {
        dataRoot=data;project=Directory.GetParent(dataRoot).FullName;
    }
    public void Request(string id)
    {
        if(Regex.IsMatch(id,"^[A-Za-z0-9_]{1,100}$")&&requested.Add(id))queue.Add(id);
    }
    public bool Pump()
    {
        bool changed=false;
        if(process!=null&&process.HasExited)
        {
            if(process.ExitCode!=0)UnityEngine.Debug.LogWarning("按需原画导出失败，请检查 local/portrait-export.jsonl");
            changed=process.ExitCode==0;process.Dispose();process=null;
        }
        if(process==null&&queue.Count>0&&Time.time>=nextAttempt)
        {
            string python=Path.Combine(project,".venv/Scripts/python.exe");
            nextAttempt=Time.time+2;
            if(!File.Exists(python)){UnityEngine.Debug.LogWarning("缺少按需资源导出环境："+python);queue.Clear();return changed;}
            var info=new ProcessStartInfo(python,"-X utf8 -m tools.export_portraits --local \""+dataRoot+"\" --ids "+String.Join(",",queue));
            info.UseShellExecute=false;info.CreateNoWindow=true;info.WindowStyle=ProcessWindowStyle.Hidden;info.WorkingDirectory=project;
            try{process=Process.Start(info);queue.Clear();}catch(Exception e){UnityEngine.Debug.LogWarning(e.Message);queue.Clear();}
        }
        return changed;
    }
    public void Dispose(){if(process!=null)process.Dispose();}
}
