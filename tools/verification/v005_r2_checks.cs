using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// 独立边界夹具；网络发送接内存，只证明正式输入路由，不冒充权威双端对局。
public static class V005R2Checks
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception("V005_R2 "+message);checks++;Debug.Log("V005_R2_CHECK "+message);}
    static T Get<T>(object owner,string name){return (T)owner.GetType().GetField(name,Fields).GetValue(owner);}
    static void Set(object owner,string name,object value){owner.GetType().GetField(name,Fields).SetValue(owner,value);}
    static object Invoke(object owner,string name,params object[] values)
    {try{return owner.GetType().GetMethod(name,Fields).Invoke(owner,values);}catch(TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException??e).Throw();throw;}}
    static int Objects<T>() where T:UnityEngine.Object{return Resources.FindObjectsOfTypeAll<T>().Length;}
    static string root{get{return Path.GetFullPath("../local");}}
    static void ClockAndProviders()
    {
        var frame=new PresentationFrameClock();Check(frame.Sample(1e9)==0,"first double clock sample is zero");
        Check(Math.Abs(frame.Sample(1e9+.016)-.016)<.000001,"long-session precision retains actual frame");
        frame.Reset();Check(frame.Sample(42)==0&&frame.Sample(42.25)==.25f,"reset does not inherit old baseline");
        bool rejected=false;try{frame.Sample(double.PositiveInfinity);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected&&frame.Sample(42.5)==.25f,"nonfinite sample rejection preserves previous valid baseline");
        var parent=new GameObject("V005_R2 audio owner");var probeCamera=new GameObject("V005_R2 camera").AddComponent<Camera>();probeCamera.enabled=false;
        var other=new GameObject("V005_R2 other camera").AddComponent<Camera>();other.enabled=false;
        try
        {
            float delta=.04f;int reads=0;
            using(var audio=new NativeAudioPlayback(Path.Combine(root,"runtime"),parent.transform,true))
            {
                audio.DeltaTime=()=>{reads++;return delta;};
                var limits=Get<NativeSoundLimits>(audio,"limits");var options=new NativeSoundOptions{limited=true,duration=.1f,maximum=1};
                limits.Acquire("quota",options,.1f,0,_=>{});Set(audio,"limitFrame",-1);audio.Update();audio.Update();
                Check(reads==1&&audio.LimitedCount("quota")==1,"audio Update shares one provider read per actual frame");
                Set(audio,"limitFrame",-1);audio.Update();Check(audio.LimitedCount("quota")==1,"quota retains source coroutine pre-subtract gate");
                delta=0;Set(audio,"limitFrame",-1);audio.Update();Check(audio.LimitedCount("quota")==1,"zero scaled provider freezes quota countdown");
                delta=.04f;Set(audio,"limitFrame",-1);audio.Update();Set(audio,"limitFrame",-1);audio.Update();
                Check(audio.LimitedCount("quota")==0&&limits.Acquire("quota",options,.1f,0,_=>{}),"quota releases then allows another voice");
            }
            NativeCameraShakeDriver.UseFrameDelta(probeCamera,()=>delta);NativeCameraShakeDriver.UseFrameDelta(other,()=>0);
            NativeCameraShakeDriver.Play(probeCamera,new Vector3(1,0,0),.1f);NativeCameraShakeDriver.Play(other,new Vector3(1,0,0),.1f);
            var driver=probeCamera.GetComponent<NativeCameraShakeDriver>();var frozen=other.GetComponent<NativeCameraShakeDriver>();
            delta=.06f;Invoke(driver,"LateUpdate");Invoke(frozen,"LateUpdate");
            Check(Math.Abs(Get<float>(Get<NativeCameraShake>(driver,"playback"),"progress")-.06f)<.000001,"camera driver consumes explicit provider");
            Check(Get<float>(Get<NativeCameraShake>(frozen,"playback"),"progress")==0,"camera providers are isolated and zero freezes progress");
            Invoke(driver,"LateUpdate");Invoke(driver,"LateUpdate");
            Check(!Get<NativeCameraShake>(driver,"playback").Started&&probeCamera.transform.position==Vector3.zero,"provider completes shake and restores base pose");
            NativeCameraShakeDriver.Stop(other);NativeCameraShakeDriver.Stop(other);
            Check(!Get<NativeCameraShake>(frozen,"playback").Started&&other.transform.position==Vector3.zero,"repeated explicit stop restores held camera");
        }
        finally{AssetRegistry.ReleaseTransient(parent);AssetRegistry.ReleaseTransient(probeCamera.gameObject);AssetRegistry.ReleaseTransient(other.gameObject);}
    }
    static void PartialCapture()
    {
        var shader=Resources.Load<Shader>("NativeEffect");var assets=new AssetRegistry(root);
        using(var fixture=new FireblastSelectionChecks.Fixture(assets,true))
        {
            fixture.Player.SelectPower(fixture.State,2);fixture.Selection(.2f);
            var effect=fixture.Player.PowerSelection.Flow.Instances.Single().Effect;var data=effect.Program.captures.Single();
            int objects=Objects<GameObject>(),targets=Objects<RenderTexture>(),materials=Objects<Material>(),meshes=Objects<Mesh>();int released=0;
            var sources=Get<Dictionary<Material,NativeMaterial>>(effect,"materialSources");
            var capture=new NativeGeneratedCapture(data,effect.Nodes,r=>true,m=>sources[m],
                m=>new Material(shader),m=>{throw new IOException("V005_R2 partial capture injection");},m=>{released++;AssetRegistry.ReleaseTransient(m);});
            bool caught=false;try{capture.Render();}catch(IOException){caught=true;}
            Check(caught&&!capture.Active&&capture.Texture==null&&released==1,"post-RT initialization failure clears plane/capture/material");
            capture.Dispose();capture.Dispose();bool disposed=false;try{capture.Render();}catch(ObjectDisposedException){disposed=true;}
            Check(disposed&&Objects<GameObject>()==objects&&Objects<RenderTexture>()==targets&&Objects<Material>()==materials&&Objects<Mesh>()==meshes,"partial capture repeated disposal leaves no physical objects");
        }
    }
    static void InputRoutes()
    {
        var go=new GameObject("V005_R2 input-only client");go.SetActive(false);var app=go.AddComponent<ClientApp>();
        var memory=new MemoryStream();var writer=new StreamWriter(memory){AutoFlush=true};var connection=new Connection();Set(connection,"writer",writer);
        var assets=new AssetRegistry(root);var calibration=new UICalibration(Path.Combine(root,"ui-calibration.json"));
        var previous=UICalibration.Current;UICalibration.Current=calibration;
        Set(app,"assets",assets);Set(app,"calibration",calibration);Set(app,"connection",connection);Set(app,"uiEditor",new UIEditor(calibration,new CardDef[0],null,null));
        var state=new StateView{match_id="v005-r2-input",version=1,phase="play",active=0,seat=0,players=new[]{
            new PlayerView{hero=new EntityView{entity=1,card_id="HERO_09"},power=new EntityView{entity=2,card_id="CS1h_001"},
                field=new[]{new EntityView{entity=5,card_id="CS2_119"}},hand=new[]{new EntityView{entity=6,card_id="CS2_029"},new EntityView{entity=7,card_id="CS2_189"}}},
            new PlayerView{hero=new EntityView{entity=3,card_id="HERO_07"},power=new EntityView{entity=4,card_id="HERO_07bp"},field=new[]{new EntityView{entity=8,card_id="CS2_119"}},hand=new EntityView[0]}},
            moves=new[]{new Move{kind="power",entity=2,target=3},new Move{kind="power",entity=2,target=5},new Move{kind="attack",entity=5,target=3},new Move{kind="play",entity=6,target=3},new Move{kind="play",entity=7,target=3,index=1}}};
        Set(app,"state",state);var cards=Get<Dictionary<string,CardDef>>(app,"cards");cards["CS2_029"]=new CardDef{CardId="CS2_029",Type=5};cards["CS2_189"]=new CardDef{CardId="CS2_189",Type=4};
        var locations=Get<Dictionary<int,Rect>>(app,"locations");var hits=Get<Dictionary<int,UIEditTarget>>(calibration,"hitRegions");
        foreach(var item in new[]{new {id=2,r=new Rect(1180,800,80,80)},new {id=3,r=new Rect(860,180,190,200)},new {id=5,r=new Rect(800,560,100,100)},new {id=6,r=new Rect(700,920,100,100)},new {id=7,r=new Rect(900,920,100,100)}})
        {locations[item.id]=item.r;hits[item.id]=new UIEditTarget{rect=item.r,matrix=BattleLayout.Matrix};}
        var legal=locations[3].center;var illegal=new Vector2(1500,500);
        Action<EventType,int,Vector2> input=(t,b,p)=>Invoke(app,"HandleBattleInput",t,b,p);
        Action<Vector2> arrow=p=>{
            Set(app,"EvidencePointer",(Vector2?)p);
            try{Invoke(app,"Arrow",Get<int>(app,"selected"));}
            catch(NullReferenceException e)
            {
                // batch执行入口无GUI事件；只接受Show完成后Draw读取Event.current这一已知边界。
                if(Event.current!=null||e.StackTrace==null||!e.StackTrace.Contains("NativeTargetArrow.Draw"))throw;
            }
        };
        Action clear=()=>{Set(app,"pending",false);Set(app,"selected",0);Set(app,"dragged",0);Set(app,"placementIndex",-1);Get<CardPickup>(app,"cardPickup").Cancel();memory.SetLength(0);};
        try
        {
            foreach(var kind in new[]{"priest-power","attack","spell","battlecry"})
            {
                Action begin=()=>{
                    clear();int id=kind=="priest-power"?2:kind=="attack"?5:kind=="spell"?6:7;
                    input(EventType.MouseDown,0,locations[id].center);
                    if(kind=="spell")input(EventType.MouseDrag,0,legal);
                    if(kind=="battlecry"){input(EventType.MouseDrag,0,new Vector2(960,570));input(EventType.MouseUp,0,new Vector2(960,570));}
                    Check(Get<int>(app,"selected")==id,"actual input selects "+kind);
                };
                begin();arrow(illegal);var native=Get<NativeTargetArrow>(app,"targetArrow");
                Check(!native.BullseyeVisible&&native.VisibleLinks>0,"original arrow keeps body without illegal bullseye "+kind);
                input(EventType.MouseUp,0,illegal);Check(memory.Length==0&&!Get<bool>(app,"pending"),"illegal release emits no request "+kind);
                if(Get<int>(app,"selected")==0)begin();arrow(legal);Check(native.BullseyeVisible,"original arrow shows legal bullseye "+kind);
                input(EventType.MouseDown,1,legal);Check(memory.Length==0&&Get<int>(app,"selected")==0&&Get<int>(app,"dragged")==0,"right cancel emits no request "+kind);
                begin();Invoke(app,"HandleBattleEscape");Check(memory.Length==0&&Get<int>(app,"selected")==0,"Escape cancels without request "+kind);
                begin();if(kind=="battlecry")input(EventType.MouseDown,0,legal);else input(EventType.MouseUp,0,legal);
                Check(Get<bool>(app,"pending")&&memory.Length>0,"legal release/confirmation sends one request "+kind);
                writer.Flush();memory.Position=0;string json=new StreamReader(memory,System.Text.Encoding.UTF8,true,1024,true).ReadToEnd();
                Check(json.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Length==1&&json.Contains("\"target\":3"),"one exact target command "+kind);
            }
            clear();state.moves=new[]{new Move{kind="power",entity=2,target=0}};Invoke(app,"Select",2,"power");
            Check(Get<NativeTargetArrow>(app,"targetArrow")!=null&&Get<int>(app,"selected")==0&&Get<bool>(app,"pending"),"untargeted power sends directly without selecting arrow");
        }
        finally
        {
            Get<NativeTargetArrow>(app,"targetArrow")?.Dispose();Set(app,"resourcesReleased",true);connection.Dispose();writer.Dispose();memory.Dispose();
            AssetRegistry.ReleaseTransient(go);UICalibration.Current=previous;
        }
    }
    public static void Run()
    {
        checks=0;ClockAndProviders();PartialCapture();InputRoutes();
        Debug.Log("V005_R2_INDEPENDENT "+checks+" checks passed");
        FireblastReviewChecks.RunClock();FireblastSelectionChecks.Run();FireblastShaderChecks.Run();FireblastParticleChecks.Run();FireblastCacheChecks.Run();
        FireblastRegressionChecks.Run();
    }
}
