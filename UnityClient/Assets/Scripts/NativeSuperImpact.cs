using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public sealed class NativeImpactPrefab
{public bool prefab_null;public string prefab_reference;public int prefab_node;}
[Serializable] public sealed class NativeImpactRange
{public int min,max;public NativeImpactPrefab prefab;}
[Serializable] public sealed class NativeImpactInfo
{
    public bool enabled,use_super_location,parent_to_location,adjust_rotation;
    public int location;public float delay_min,delay_max,spawn_offset,game_delay_min,game_delay_max;
    public Vector3 rotation;public NativeImpactPrefab prefab;public NativeImpactRange[] ranges;
}
public sealed class NativeImpactClone
{public NativeAreaClone Clone;public Action<GameObject> AddTarget;public Action<Vector3> Rotate;}
public sealed class NativeSuperImpactServices
{
    public GameObject Root;public Func<GameObject> Source;public IList<GameObject> Targets;
    public Func<GameObject,bool> IsValidTarget;
    public Func<int,int> MetaDataIndex;
    public Func<int,bool> ShouldCompleteUntil;
    public Func<int,IEnumerator> CompleteUntil;
    public Func<int,float,IEnumerator> CompleteFrom;
    public Func<IEnumerator,object> StartCoroutine;
    public Func<GameObject,int?> Damage;
    public Func<NativeImpactPrefab,NativeImpactClone> GetSpell;
    public Func<NativeAreaClone,bool,bool> Release;
    public Action FinishIfPossible;
    public Action<int,string,Exception> Error;
    public Func<float,float,float> RandomRange=UnityEngine.Random.Range;
}

// 按 d__89 保留等待和分派顺序；故障/取消保护是自主宿主契约。
public sealed class NativeSuperImpact : IDisposable
{
    sealed class Request
    {
        public int index;public bool counted=true,stopped,released,releasing,finished;
        public NativeAreaClone clone;public readonly List<Routine> routines=new List<Routine>();
    }
    sealed class Routine : IEnumerator,IDisposable
    {
        readonly NativeSuperImpact owner;readonly Request request;readonly string stage;
        readonly Stack<IEnumerator> stack=new Stack<IEnumerator>();bool running;
        public bool Done{get;private set;}
        public object Current{get;private set;}
        public Routine(NativeSuperImpact owner,Request request,IEnumerator body,string stage)
        {this.owner=owner;this.request=request;this.stage=stage;stack.Push(body);}
        public bool MoveNext()
        {
            if(Done||request.stopped){Close();return false;}
            running=true;
            try
            {
                while(stack.Count>0&&!request.stopped)
                {
                    var body=stack.Peek();
                    if(!body.MoveNext()){stack.Pop();(body as IDisposable)?.Dispose();continue;}
                    var current=body.Current;
                    if(current is IEnumerator nested){stack.Push(nested);continue;}
                    Current=current;return !request.stopped;
                }
                Done=true;return false;
            }
            catch(Exception error)
            {if(request.stopped)owner.Report(request,stage,error);else owner.Fail(request,stage,error);return false;}
            finally{running=false;if(Done||request.stopped)Close();owner.Prune();}
        }
        public void Reset(){throw new NotSupportedException();}
        public void Dispose(){if(!Done&&!request.stopped)owner.Fail(request,stage,new OperationCanceledException());Close();}
        public void Close()
        {
            if(running)return;Done=true;Current=null;
            while(stack.Count>0)
            {
                try{(stack.Pop() as IDisposable)?.Dispose();}
                catch(Exception error){owner.Report(request,stage+" dispose",error);}
            }
        }
    }
    readonly NativeImpactInfo info;readonly NativeSuperImpactServices services;
    readonly List<NativeAreaClone> clones=new List<NativeAreaClone>();
    readonly List<Request> requests=new List<Request>();bool disposed;
    public int Pending{get;private set;}
    public int Faulted{get;private set;}
    public int Cancelled{get;private set;}
    public int DiagnosticFailures{get;private set;}
    public Exception LastError{get;private set;}
    public Exception LastDiagnosticError{get;private set;}
    public IReadOnlyList<NativeAreaClone> ActiveClones{get{return clones;}}
    public NativeSuperImpact(NativeImpactInfo info,NativeSuperImpactServices services)
    {
        this.info=info??throw new ArgumentNullException(nameof(info));
        this.services=services??throw new ArgumentNullException(nameof(services));
        Finite(info.delay_min);Finite(info.delay_max);Finite(info.spawn_offset);
        Finite(info.game_delay_min);Finite(info.game_delay_max);
        Finite(info.rotation.x);Finite(info.rotation.y);Finite(info.rotation.z);
        if(info.ranges!=null)foreach(var range in info.ranges)
            if(range==null)throw new ArgumentException("Impact ranges cannot contain null entries");
    }
    static void Finite(float value){if(float.IsNaN(value)||float.IsInfinity(value))throw new ArgumentOutOfRangeException(nameof(value));}
    public static bool HasImpact(NativeImpactInfo info)
    {return info!=null&&info.enabled&&(info.prefab!=null&&!info.prefab.prefab_null||info.ranges!=null&&info.ranges.Length>0);}
    public static NativeImpactPrefab Select(NativeImpactInfo info,int? damage)
    {
        if(info==null)throw new ArgumentNullException(nameof(info));
        if(info.ranges==null||info.ranges.Length==0||!damage.HasValue)return info.prefab;
        int high=-1,low=-1,hit=-1,maximum=int.MinValue,minimum=int.MaxValue;
        for(int i=0;i<info.ranges.Length;i++)
        {
            var range=info.ranges[i]??throw new ArgumentException("Impact range is null");
            if(range.max>maximum){maximum=range.max;high=i;}
            if(range.min<minimum){minimum=range.min;low=i;}
            if(hit<0&&damage.Value>=range.min&&damage.Value<=range.max)hit=i;
        }
        int selected=damage.Value>maximum&&high>=0?high:damage.Value<minimum&&low>=0?low:hit;
        var prefab=selected<0?null:info.ranges[selected].prefab;
        return prefab!=null&&!prefab.prefab_null?prefab:info.prefab;
    }
    public void SpawnAll()
    {Validate();for(int i=0;i<services.Targets.Count;i++)if(services.IsValidTarget(services.Targets[i]))Spawn(i);}
    void Validate()
    {
        if(disposed)throw new ObjectDisposedException(nameof(NativeSuperImpact));
        if(!HasImpact(info))throw new InvalidOperationException("Impact configuration is unavailable");
        if(services.Root==null||services.Source==null||services.Targets==null||services.IsValidTarget==null||
            services.MetaDataIndex==null||services.StartCoroutine==null||services.GetSpell==null||
            services.Release==null||services.FinishIfPossible==null||services.RandomRange==null)
            throw new NotSupportedException("Original impact host services are unbound");
        if(info.ranges!=null&&info.ranges.Length>0&&services.Damage==null)
            throw new NotSupportedException("Original impact DamageInfo service is unbound");
    }
    public void Spawn(int targetIndex)
    {
        Validate();if(targetIndex<0||targetIndex>=services.Targets.Count)throw new ArgumentOutOfRangeException(nameof(targetIndex));
        var request=new Request{index=targetIndex};requests.Add(request);Pending++;
        try{Start(request,WaitAndSpawn(request),"spawn");}
        catch(Exception error){Fail(request,"schedule",error);throw;}
    }
    object Start(Request request,IEnumerator body,string stage)
    {
        if(request.stopped)
        {
            try{(body as IDisposable)?.Dispose();}catch(Exception error){Report(request,stage+" cancelled dispose",error);}
            return null;
        }
        if(body==null)throw new NotSupportedException("Original impact "+stage+" coroutine is unbound");
        var routine=new Routine(this,request,body,stage);request.routines.Add(routine);
        var handle=services.StartCoroutine(routine);
        if(handle==null&&!routine.Done)throw new InvalidOperationException("Impact scheduler returned no handle for a suspended coroutine");
        return handle;
    }
    void Prune(){requests.RemoveAll(r=>!r.counted&&(r.clone==null||r.released)&&r.routines.TrueForAll(c=>c.Done));}
    float Sample(float min,float max){float value=services.RandomRange(min,max);Finite(value);return value;}
    void Settle(Request request)
    {if(!request.counted)return;request.counted=false;Pending--;}
    void Release(Request request)
    {
        if(request.clone==null||request.released||request.releasing)return;
        // 回收服务可同步触发取消，禁止同一克隆重入回收。
        request.releasing=true;
        try{services.Release(request.clone,false);request.released=true;clones.Remove(request.clone);}
        catch{if(!clones.Contains(request.clone))clones.Add(request.clone);throw;}
        finally{request.releasing=false;}
    }
    void Report(Request request,string stage,Exception error)
    {
        LastError=error;
        if(services.Error!=null)
        {
            try{services.Error(request.index,stage,error);}
            catch(Exception observerError)
            {
                DiagnosticFailures++;LastDiagnosticError=observerError;
                Debug.LogWarning("Native impact diagnostic observer failed: "+observerError);
            }
        }
        else if(error is OperationCanceledException)Debug.Log("Native impact cancelled: target="+request.index+", stage="+stage);
        else Debug.LogException(error);
    }
    void Fail(Request request,string stage,Exception error)
    {
        if(request.stopped){Report(request,stage,error);return;}request.stopped=true;Settle(request);
        if(error is OperationCanceledException)Cancelled++;else Faulted++;
        foreach(var routine in request.routines)routine.Close();
        try{Release(request);}catch(Exception releaseError){Report(request,"release",releaseError);}
        Report(request,stage,error);
    }
    public void CancelAll()
    {
        foreach(var request in requests.ToArray())
        {
            bool active=request.counted||request.clone!=null&&!request.released||request.routines.Exists(r=>!r.Done);
            if(active&&!request.stopped)Fail(request,"cancel",new OperationCanceledException());
            else if(request.stopped)try{Release(request);}catch(Exception error){Report(request,"release retry",error);}
        }Prune();
    }
    public void Dispose(){if(disposed)return;disposed=true;CancelAll();}
    IEnumerator WaitAndSpawn(Request request)
    {
        int targetIndex=request.index;
        yield return new WaitForSeconds(Sample(info.delay_min,info.delay_max));
        if(info.spawn_offset>0&&targetIndex>0)
        {float offset=info.spawn_offset*targetIndex;Finite(offset);yield return new WaitForSeconds(offset);}
        int metadata=services.MetaDataIndex(targetIndex);
        if(request.stopped)yield break;
        if(metadata>=0)
        {
            if(services.ShouldCompleteUntil==null||services.CompleteUntil==null||services.CompleteFrom==null)
                throw new NotSupportedException("Original impact metadata services are unbound");
            bool until=services.ShouldCompleteUntil(metadata);
            if(request.stopped)yield break;
            if(until)yield return Start(request,services.CompleteUntil(metadata),"metadata until");
            if(request.stopped)yield break;
            float delay=Sample(info.game_delay_min,info.game_delay_max);
            if(request.stopped)yield break;
            Start(request,services.CompleteFrom(metadata,delay),"metadata from");
            if(request.stopped)yield break;
        }
        if(services.Targets.Count<=targetIndex||services.Targets[targetIndex]==null){Settle(request);yield break;}
        var source=services.Source();
        if(request.stopped)yield break;
        var target=services.Targets[targetIndex];
        int? damage=info.ranges!=null&&info.ranges.Length>0?services.Damage(target):(int?)null;
        if(request.stopped)yield break;
        var selected=Select(info,damage);
        if(selected==null||selected.prefab_null)throw new NotSupportedException("Selected original impact prefab is null");
        var impact=services.GetSpell(selected);var clone=impact==null?null:impact.Clone;request.clone=clone;
        if(request.stopped){Release(request);yield break;}
        if(clone==null||clone.Object==null||clone.AddFinished==null||clone.AddStateStarted==null||clone.ActiveState==null||
            clone.SetSource==null||impact.AddTarget==null||clone.UpdateOrientation==null||clone.Activate==null||
            (info.use_super_location?clone.SetPosition==null:clone.SetLocation==null||clone.UpdatePosition==null)||
            (!info.use_super_location&&info.adjust_rotation&&info.rotation!=Vector3.zero&&impact.Rotate==null))
            throw new NotSupportedException("Original impact clone endpoint is incomplete");
        NativeSuperArea.RegisterClone(clone,services.Root,clones,(c,reset)=>
        {
            try{Release(request);}catch(Exception error){Fail(request,"release",error);}return false;
        },()=>
        {
            if(request.stopped||request.finished)return;request.finished=true;Settle(request);
            try{services.FinishIfPossible();}catch(Exception error){Fail(request,"finish",error);}Prune();
        });
        if(request.stopped){clones.Remove(clone);Release(request);yield break;}
        clone.SetSource(source);
        if(request.stopped)yield break;
        impact.AddTarget(target);
        if(request.stopped)yield break;
        if(info.use_super_location)clone.SetPosition(services.Root.transform.position);
        else
        {
            clone.SetLocation(info.location,info.parent_to_location);
            if(request.stopped)yield break;
            clone.UpdatePosition();
            if(request.stopped)yield break;
            if(info.adjust_rotation&&info.rotation!=Vector3.zero)impact.Rotate(info.rotation);
        }
        if(request.stopped)yield break;
        clone.UpdateOrientation();
        if(request.stopped)yield break;
        clone.Activate();
    }
}
