using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using UnityEngine;

// 直接重放客户端输入并检查 TCP 动作，既不操作桌面，也不替代服务器规则测试。
public static class CardInputChecks
{
    static int passed;
    static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Get(object app,string name){return app.GetType().GetField(name,flags).GetValue(app);}
    static void Set(object app,string name,object value){app.GetType().GetField(name,flags).SetValue(app,value);}
    static void Check(bool condition,string message){if(!condition)throw new Exception("Card input check: "+message);passed++;}
    static void Input(ClientApp app,EventType type,Vector2 point,int button=0)
    {
        typeof(ClientApp).GetMethod("HandleBattleInput",flags).Invoke(app,new object[]{type,button,point});
    }
    public static void Run()
    {
        passed=0;var oldCalibration=UICalibration.Current;
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        var connection=new Connection();var go=new GameObject("CardInputReplay");var app=go.AddComponent<ClientApp>();TcpClient peer=null;
        try
        {
            connection.Connect("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port);peer=listener.AcceptTcpClient();peer.ReceiveTimeout=2000;
            var reader=new StreamReader(peer.GetStream());bool ready=false;
            for(int i=0;i<200&&!ready;i++){try{connection.Send("{}");ready=true;}catch(IOException){Thread.Sleep(5);}}
            Check(ready&&reader.ReadLine()=="{}","test connection ready");
            Set(app,"connection",connection);
            var state=new StateView{match_id="input-replay",version=1,seat=0,phase="play",players=new[]{new PlayerView{
                hero=new EntityView{entity=40},power=new EntityView{entity=41},field=new EntityView[0],hand=new[]{
                    new EntityView{entity=1,card_id="MINION"},new EntityView{entity=2,card_id="DRAW"},
                    new EntityView{entity=3,card_id="BOLT"},new EntityView{entity=4,card_id="BATTLECRY"}}}},
                moves=new[]{new Move{kind="play",entity=1},new Move{kind="play",entity=2},
                    new Move{kind="play",entity=3,target=42},new Move{kind="play",entity=4,target=42,index=0}}};
            Set(app,"state",state);var cards=(Dictionary<string,CardDef>)Get(app,"cards");
            foreach(var item in new[]{new CardDef{CardId="MINION",Type=4},new CardDef{CardId="DRAW",Type=5},new CardDef{CardId="BOLT",Type=5},new CardDef{CardId="BATTLECRY",Type=4}})cards[item.CardId]=item;
            var calibration=new UICalibration(Path.GetFullPath("../work/input-replay.json"));Set(app,"calibration",calibration);UICalibration.Current=calibration;
            var regions=(Dictionary<int,UIEditTarget>)typeof(UICalibration).GetField("hitRegions",flags).GetValue(calibration);
            var hero=new Rect(865,88,190,200);regions[42]=new UIEditTarget{rect=hero,matrix=BattleLayout.Matrix};
            var locations=(Dictionary<int,Rect>)Get(app,"locations");locations[42]=hero;
            var hand=(HandPresentation)Get(app,"handPresentation");var presentation=(Presentation)Get(app,"presentation");
            Vector2 held=new Vector2(900,1000),board=new Vector2(900,600);
            Action<int> pick=index=>{
                Set(app,"pending",false);hand.Tick(state.players[0].hand,held,0,presentation,true,.016f,index);
                Input(app,EventType.MouseDown,held);Input(app,EventType.MouseUp,held);
            };
            pick(0);Check((int)Get(app,"dragged")==1&&!peer.GetStream().DataAvailable,"tap minion holds without network play");
            Input(app,EventType.MouseDown,held);Check((int)Get(app,"dragged")==0&&!peer.GetStream().DataAvailable,"click in hand cancels without play");
            pick(0);Input(app,EventType.MouseDown,board);
            var move=JsonUtility.FromJson<ActionMessage>(reader.ReadLine());Check(move.kind=="play"&&move.entity==1&&move.index==0,"second board click sends minion play");
            pick(1);Check((int)Get(app,"dragged")==2&&!peer.GetStream().DataAvailable,"untargeted spell tap does not cast");
            Input(app,EventType.MouseDrag,board);Input(app,EventType.MouseUp,held);
            Check((int)Get(app,"dragged")==0&&!peer.GetStream().DataAvailable,"dragging back to hand cancels");
            pick(1);Input(app,EventType.MouseDrag,board);Input(app,EventType.MouseUp,board);
            move=JsonUtility.FromJson<ActionMessage>(reader.ReadLine());Check(move.entity==2&&move.target==0,"untargeted spell drag into board casts");
            pick(2);Input(app,EventType.MouseDown,hero.center);
            move=JsonUtility.FromJson<ActionMessage>(reader.ReadLine());Check(move.entity==3&&move.target==42,"targeted spell arrow targets enemy hero outside play zone");
            pick(3);Input(app,EventType.MouseDown,board);
            Check((int)Get(app,"placementIndex")==0&&(int)Get(app,"dragged")==4&&!peer.GetStream().DataAvailable,"targeted battlecry waits after placement");
            Check((bool)typeof(ClientApp).GetMethod("TargetedHeld",flags).Invoke(app,null),"placed battlecry requests spell target indicator");
            Input(app,EventType.MouseDown,hero.center);move=JsonUtility.FromJson<ActionMessage>(reader.ReadLine());
            Check(move.entity==4&&move.index==0&&move.target==42,"battlecry target retains insertion index");
            state.players[0].hand[3].combo_ready=true;
            pick(3);Input(app,EventType.MouseDrag,board);Input(app,EventType.MouseUp,board);
            Check((int)Get(app,"placementIndex")==0&&(int)Get(app,"dragged")==4&&!peer.GetStream().DataAvailable,"active targeted combo drag waits for target");
            Input(app,EventType.MouseDown,hero.center);move=JsonUtility.FromJson<ActionMessage>(reader.ReadLine());
            Check(move.entity==4&&move.target==42,"active combo uses same target selection as spell");
            state.players[0].hand[3].combo_ready=false;state.moves[3].target=0;
            pick(3);Input(app,EventType.MouseDown,board);move=JsonUtility.FromJson<ActionMessage>(reader.ReadLine());
            Check(move.entity==4&&move.target==0,"inactive combo places without unnecessary target arrow");
            var weapon=new EntityView{entity=5,card_id="WEAPON"};cards["WEAPON"]=new CardDef{CardId="WEAPON",Type=7};
            state.players[0].hand=new[]{weapon};state.moves=new[]{new Move{kind="play",entity=5,target=42}};
            pick(0);Check((bool)typeof(ClientApp).GetMethod("TargetedHeld",flags).Invoke(app,null),"targeted weapon uses indicator on pickup");
            Input(app,EventType.MouseDrag,board);Input(app,EventType.MouseUp,board);
            Check((int)Get(app,"placementIndex")==0&&!peer.GetStream().DataAvailable,"weapon placement waits for battlecry target");
            Input(app,EventType.MouseDown,hero.center);move=JsonUtility.FromJson<ActionMessage>(reader.ReadLine());
            Check(move.entity==5&&move.target==42,"weapon target submits legal server move");
            state.players[0].hand=new[]{new EntityView{entity=1,card_id="MINION"}};state.moves=new[]{new Move{kind="play",entity=1}};
            pick(0);Input(app,EventType.MouseDown,board,1);Check((int)Get(app,"dragged")==0&&!peer.GetStream().DataAvailable,"right click cancels pickup");
            Set(app,"battleMenu",true);pick(0);
            Check((int)Get(app,"dragged")==0&&!peer.GetStream().DataAvailable,"menu blocks pickup without network action");Set(app,"battleMenu",false);
            var next=JsonUtility.FromJson<StateView>(JsonUtility.ToJson(state));next.version++;
            next.events=new[]{new SemanticEvent{type="PlayHeroPower",target=41}};
            presentation.Accept(state,next,locations);Set(app,"pending",false);
            var send=typeof(ClientApp).GetMethod("SendMove",flags);send.Invoke(app,new object[]{state.moves[0]});
            Check(presentation.Busy&&!peer.GetStream().DataAvailable,"ordinary action waits for presentation");
            send.Invoke(app,new object[]{new Move{kind="concede"}});move=JsonUtility.FromJson<ActionMessage>(reader.ReadLine());
            Check(move.kind=="concede"&&!presentation.Busy,"concede skips long animation and reaches server");
            File.WriteAllText("../work/card-input-checks.json","{\"passed\":"+passed+"}");Debug.Log("CARD INPUT CHECKS PASSED: "+passed);
        }
        finally{UICalibration.Current=oldCalibration;connection.Dispose();if(peer!=null)peer.Close();listener.Stop();UnityEngine.Object.DestroyImmediate(go);}
    }
}
