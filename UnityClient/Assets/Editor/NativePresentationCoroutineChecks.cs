using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class NativePresentationCoroutineChecks
{
    static int passed,cases;
    static void Check(bool value,string label){if(!value)throw new Exception("Presentation coroutine cleanup: "+label);passed++;}
    sealed class Frame : IEnumerator,IDisposable
    {
        public Func<bool> Move;public Func<object> Read;public Action Close;
        public int Moves,Currents,Disposals;
        public bool MoveNext(){Moves++;return Move==null?Moves==1:Move();}
        public object Current{get{Currents++;return Read?.Invoke();}}
        public void Reset(){throw new NotSupportedException();}
        public void Dispose(){Disposals++;Close?.Invoke();}
    }
    sealed class Tree
    {
        public readonly Frame Leaf=new Frame(),Middle=new Frame(),Outer=new Frame();
        public readonly Frame[] Frames;
        public Tree(){Frames=new[]{Leaf,Middle,Outer};Middle.Read=()=>Leaf;Outer.Read=()=>Middle;Leaf.Read=()=>new WaitForSeconds(1);}
    }
    static Exception Capture(Action action){try{action();return null;}catch(Exception error){return error;}}
    static bool Contains(Exception error,Exception original)
    {return ReferenceEquals(error,original)||error is AggregateException aggregate&&aggregate.InnerExceptions.Any(e=>Contains(e,original));}
    static void Drained(NativePresentationCoroutines clock,IEnumerable<Frame> frames,params NativePresentationCoroutines.Handle[] handles)
    {
        Check(frames.All(f=>f.Disposals==1),"each owned frame is disposed once");
        Check(clock.Active==0,"actual scheduler queue drains");
        foreach(var handle in handles)
        {
            Check(handle.Done,"handle completes after cleanup");
            Check(typeof(NativePresentationCoroutines.Handle).GetField("wait",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(handle)==null,
                "handle releases wait reference");
            var stack=(ICollection)typeof(NativePresentationCoroutines.Handle).GetField("stack",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(handle);
            Check(stack.Count==0,"actual owned stack drains");
        }
    }
    static void Paths()
    {
        foreach(string path in new[]{"cancel","normal","move","current"})foreach(int faultCount in new[]{0,1,3})
        {
            var tree=new Tree();var reports=new List<Exception>();var genuine=new IOException("real "+path+" failure");
            var disposal=Enumerable.Range(0,faultCount).Select(i=>new IOException("real layer "+i+" Dispose failure")).ToArray();
            for(int i=0;i<faultCount;i++){var fault=disposal[i];tree.Frames[i].Close=()=>{throw fault;};}
            using(var clock=new NativePresentationCoroutines(reports.Add))
            {
                var handle=(NativePresentationCoroutines.Handle)clock.Start(tree.Outer);
                Check(tree.Frames.All(f=>f.Moves==1)&&clock.Active==1,"all nested frames are genuinely owned "+path);
                if(path=="cancel")clock.Dispose();
                else
                {
                    if(path=="move")tree.Leaf.Move=()=>throw genuine;
                    if(path=="current"){tree.Leaf.Move=()=>true;tree.Leaf.Read=()=>throw genuine;}
                    clock.Tick(1);
                }
                Drained(clock,tree.Frames,handle);
                bool bodyFault=path=="move"||path=="current";
                Check(reports.Count==(bodyFault||faultCount>0?1:0),"cleanup reports after draining "+path);
                if(bodyFault)Check(Contains(reports[0],genuine),"original body failure survives cleanup errors "+path);
                foreach(var fault in disposal)Check(Contains(reports[0],fault),"every layer fault is diagnosable "+path);
                if(!bodyFault&&faultCount==1)Check(ReferenceEquals(reports[0],disposal[0]),"single disposal error preserves original instance");
                clock.Dispose();clock.Dispose();Check(tree.Frames.All(f=>f.Disposals==1),"repeated stop does not repeat frame disposal");cases++;
            }
        }
    }
    static void MultipleJobs()
    {
        foreach(string observer in new[]{"returns","throws","absent"})
        {
            var a=new Tree();var b=new Tree();var waiter=new Frame();var reports=new List<Exception>();
            var first=new IOException("first job disposal");var second=new IOException("second job disposal");var diagnostic=new InvalidOperationException("diagnostic observer");
            a.Leaf.Close=()=>throw first;b.Middle.Close=()=>throw second;
            NativePresentationCoroutines clock=null;Action<Exception> report=null;
            if(observer!="absent")report=e=>{reports.Add(e);clock.Dispose();if(observer=="throws")throw diagnostic;};
            using(clock=new NativePresentationCoroutines(report))
            {
                var ah=(NativePresentationCoroutines.Handle)clock.Start(a.Outer);var bh=(NativePresentationCoroutines.Handle)clock.Start(b.Outer);
                waiter.Read=()=>ah;var wh=(NativePresentationCoroutines.Handle)clock.Start(waiter);
                var escaped=Capture(clock.Dispose);Drained(clock,a.Frames.Concat(b.Frames).Concat(new[]{waiter}),ah,bh,wh);
                var observed=observer=="absent"?escaped:reports.Single();
                Check(Contains(observed,first)&&Contains(observed,second),"one job failure cannot abandon another job or hide its cause "+observer);
                Check(observer=="returns"?escaped==null:escaped!=null,"observer/no-observer propagation remains explicit "+observer);
                if(observer=="throws")Check(Contains(escaped,first)&&Contains(escaped,second)&&Contains(escaped,diagnostic),"observer failure keeps cleanup causes too");
                clock.Dispose();Check(a.Frames.Concat(b.Frames).All(f=>f.Disposals==1),"observer reentry does not repeat disposal");cases++;
            }
        }
        var failed=new Tree();var other=new Tree();var diagnostics=new List<Exception>();var fault=new IOException("natural leaf close");
        failed.Leaf.Close=()=>throw fault;
        using(var clock=new NativePresentationCoroutines(diagnostics.Add))
        {
            var fh=(NativePresentationCoroutines.Handle)clock.Start(failed.Outer);var oh=(NativePresentationCoroutines.Handle)clock.Start(other.Outer);
            clock.Tick(1);Drained(clock,failed.Frames.Concat(other.Frames),fh,oh);
            Check(diagnostics.Count==1&&ReferenceEquals(diagnostics[0],fault),"faulted natural completion does not skip independent normal job");cases++;
        }
    }
    static void Reentry()
    {
        foreach(string stage in new[]{"move","current","dispose"})foreach(bool fault in new[]{false,true})
        {
            var tree=new Tree();var other=new Tree();var reports=new List<Exception>();var genuine=new IOException("reentrant dispose "+stage);
            using(var clock=new NativePresentationCoroutines(reports.Add))
            {
                var handle=(NativePresentationCoroutines.Handle)clock.Start(tree.Outer);
                var oh=(NativePresentationCoroutines.Handle)clock.Start(other.Outer);int currents=tree.Leaf.Currents;
                Action stop=()=>{clock.Dispose();clock.Dispose();if(fault)throw genuine;};
                if(stage=="move")tree.Leaf.Move=()=>{stop();return true;};
                if(stage=="current"){tree.Leaf.Move=()=>true;tree.Leaf.Read=()=>{stop();return new WaitForSeconds(20);};}
                if(stage=="dispose")tree.Leaf.Close=stop;
                clock.Tick(1);Drained(clock,tree.Frames.Concat(other.Frames),handle,oh);
                Check(stage!="move"||tree.Leaf.Currents==currents,"stopped MoveNext does not read Current");
                Check(fault?reports.Count==1&&Contains(reports[0],genuine):reports.Count==0,"reentrant stop preserves genuine error only "+stage);
                clock.Dispose();Check(tree.Frames.All(f=>f.Disposals==1),"running frame is cleaned after unwind exactly once");cases++;
            }
        }
        var parent=new Frame();var child=new Frame();var seen=new List<Exception>();
        using(var clock=new NativePresentationCoroutines(seen.Add))
        {
            parent.Read=()=>{clock.Dispose();return child;};
            Check(clock.Start(parent)==null,"Current stop completes synchronous Start after deferred cleanup");
            Check(child.Moves==0&&child.Disposals==1&&parent.Disposals==1&&clock.Active==0,"late yielded nested frame is owned then disposed without execution");
            Check(seen.Count==0,"late nested return is a clean stop");cases++;
        }
        foreach(bool observerThrows in new[]{false,true})
        {
            var running=new Tree();var sibling=new Tree();var reports=new List<Exception>();
            var cleanup=new IOException("sibling cleanup while other frame is running");var body=new IOException("running frame failure");var observer=new IOException("throwing observer during deferred stop");
            sibling.Leaf.Close=()=>throw cleanup;
            using(var clock=new NativePresentationCoroutines(e=>{reports.Add(e);if(observerThrows)throw observer;}))
            {
                var rh=(NativePresentationCoroutines.Handle)clock.Start(running.Outer);var sh=(NativePresentationCoroutines.Handle)clock.Start(sibling.Outer);
                running.Leaf.Move=()=>{clock.Dispose();throw body;};
                var escaped=Capture(()=>clock.Tick(1));Drained(clock,running.Frames.Concat(sibling.Frames),rh,sh);
                Check(reports.Any(e=>Contains(e,cleanup)),"deferred running cleanup does not hide sibling fault");
                Check(observerThrows?Contains(escaped,cleanup)&&Contains(escaped,observer):reports.Any(e=>Contains(e,body))&&escaped==null,
                    "running unwind retains actual body/observer cause after sibling cleanup");
                clock.Dispose();Check(running.Frames.Concat(sibling.Frames).All(f=>f.Disposals==1),"deferred unwind and throwing observer cannot repeat disposal");cases++;
            }
        }
    }
    public static void Run()
    {
        passed=cases=0;Paths();MultipleJobs();Reentry();
        Debug.Log("NATIVE PRESENTATION COROUTINES: "+passed+" checks / "+cases+" cleanup cases passed");
    }
}
