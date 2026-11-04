using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// 表现时钟的帧调度器；零秒等待也跨帧，Start 同步推进到首个 yield。
public sealed class NativePresentationCoroutines : IDisposable
{
    public sealed class Handle
    {
        internal readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();
        internal object wait;internal int frame;internal float due;internal bool running,stopped,closing;
        public bool Done{get;internal set;}
    }
    static readonly FieldInfo seconds=typeof(WaitForSeconds).GetField("m_Seconds",BindingFlags.NonPublic|BindingFlags.Instance);
    readonly List<Handle> jobs=new List<Handle>();
    readonly Action<Exception> error;bool disposed;
    public float Time{get;private set;}
    public int Frame{get;private set;}
    public int Active{get{return jobs.Count;}}
    public NativePresentationCoroutines(Action<Exception> error){this.error=error;}
    public object Start(IEnumerator body)
    {
        if(disposed)throw new ObjectDisposedException(nameof(NativePresentationCoroutines));
        if(body==null)throw new ArgumentNullException(nameof(body));
        var job=new Handle();job.stack.Push(body);jobs.Add(job);Step(job);
        return job.Done?null:job;
    }
    void Step(Handle job)
    {
        if(disposed||job.Done||job.running||job.closing)return;
        var failures=new List<Exception>();job.running=true;
        try
        {
            while(!disposed&&job.stack.Count>0)
            {
                var body=job.stack.Peek();
                bool moved=body.MoveNext();if(disposed)return;
                if(!moved){job.stack.Pop();(body as IDisposable)?.Dispose();continue;}
                var value=body.Current;
                if(value is IEnumerator nested){job.stack.Push(nested);continue;}
                if(disposed)return;
                if(value!=null&&!(value is WaitForSeconds)&&!(value is Handle))
                    throw new NotSupportedException("Unbound presentation coroutine yield: "+value.GetType());
                job.wait=value;job.frame=Frame+1;job.due=Time;
                if(value is WaitForSeconds wait)
                {
                    if(seconds==null)throw new NotSupportedException("Unity scaled wait duration is unavailable");
                    float delay=(float)seconds.GetValue(wait);
                    if(float.IsNaN(delay)||float.IsInfinity(delay))throw new ArgumentOutOfRangeException(nameof(delay));
                    job.due+=delay;
                }
                return;
            }
        }
        catch(Exception exception){job.stopped=true;failures.Add(exception);}
        finally{job.running=false;if(disposed||job.stopped||job.stack.Count==0)Close(job,failures);Report(failures);}
    }
    void Close(Handle job,List<Exception> failures)
    {
        if(job.Done||job.running||job.closing)return;job.closing=true;job.wait=null;
        try
        {
            // 每个已接管帧只尝试一次；单层故障不能截断外层 finally 或其他任务。
            while(job.stack.Count>0)
            {
                var body=job.stack.Pop();
                try{(body as IDisposable)?.Dispose();}catch(Exception exception){failures.Add(exception);}
            }
        }
        finally{job.wait=null;job.closing=false;job.Done=true;jobs.Remove(job);}
    }
    void Report(List<Exception> failures)
    {
        if(failures.Count==0)return;
        Exception failure=failures.Count==1?failures[0]:new AggregateException("Presentation coroutine failures",failures);
        if(error==null)throw failure;
        try{error(failure);}
        catch(Exception observer)
        {
            if(ReferenceEquals(observer,failure))throw;
            throw new AggregateException("Presentation coroutine diagnostic failed",failure,observer);
        }
    }
    public void Tick(float time)
    {
        if(disposed)throw new ObjectDisposedException(nameof(NativePresentationCoroutines));
        if(float.IsNaN(time)||float.IsInfinity(time)||time<Time)throw new ArgumentOutOfRangeException(nameof(time));
        Time=time;Frame++;
        foreach(var job in jobs.ToArray())
        {
            if(disposed)break;
            if(!job.Done&&Frame>=job.frame&&Time>=job.due&&(!(job.wait is Handle dependency)||dependency.Done))Step(job);
        }
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;var failures=new List<Exception>();
        foreach(var job in jobs.ToArray())Close(job,failures);
        Report(failures);
    }
}
