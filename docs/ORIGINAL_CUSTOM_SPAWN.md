# 原自定义入场父级与姿态脚本

本文保留历史节点验证。后续已恢复原Timeline、ControlRandomized、CameraShaker2及ActorElite事件，当前剩余边界见 [ORIGINAL_ACTOR_ELITE](ORIGINAL_ACTOR_ELITE.md)。

2026-10-11。恢复InitializeCustomSpawn/FinalizeCustomSpawn的真实父级与姿态操作；全卡原版动画、完整原Shader/脚本仍未完成。专属法术启用数仍2个，RLK入场效果仍禁用。

## 源证据

只读IL：`local/script-audit/Assembly-CSharp.dll.InitializeCustomSpawn.il.txt`、`Assembly-CSharp.dll.FinalizeCustomSpawn.il.txt`。不执行官方程序集。

Initialize要求tokenRoot非None且非null。Owner的Transform就是控制节点；从Owner及父级寻找Actor，保存Actor父级；先Owner.SetParent(ActorParent,true)，再Actor.SetParent(tokenRoot,true)，顺序避免Actor/效果父级形成环。按独立resetActorPosition/Rotation/Scale清零局部位置、局部欧拉角、归一化局部缩放。actorOutput/actorParentOutput仅在非None且UseVariable时写入。OnEnter完成后Finish。

Finalize读取actor；源Actor组件缺失时告警但继续。目标父级取当前Owner.transform.parent，Actor.SetParent(parent,true)，再执行三个独立reset标志，然后Finish。源Reset的三个标志均false；实际RLK缓存Initialize=位置true/旋转true/缩放false，Finalize三个均true。未用自定Tween代替这些操作。

## 实现及借用保护

NativeEffectInstance按准确FSM Owner、源参数及变量执行两个动作；输入/None/输出未解析时保留UnboundCustomSpawnActions。输出Actor可供后续Finalize和现有ActorSetVisibilityRecursiveAction消费。原时序与Shader保持独立，未加服务器等待。

NativeCustomSpawn执行原SetParent(true)及reset操作。实例保存被移走的Owner/Actor原父级、局部位置/旋转/缩放与兄弟索引。倒放先按反序恢复，再重放源状态；退出/取消/Dispose在销毁效果前恢复Actor与Owner，防止临时token下的真实Actor被效果销毁。这份快照/并发借用保护属于客户端适配，不宣称原Actor/Spell完整生命周期或取消系统已恢复。重叠实例不能夺取同一Actor；不安全环形目标拒绝且不先修改场景。

源效果的Owner移到Actor原父级后，部分Nodes暂时处在效果Root之外，不能只销毁Root假定所有对象已回收。当前Restore先把Owner归回原Root，再释放Root及拥有的材质/RT。借用保护不改用户UI校准，也不重写网络、规则或服务器。

## 可重复源刷新

```powershell
.\.venv\Scripts\python.exe -X utf8 -m tools.native_effects --refresh-custom-spawn-cache
```

精确CAB/PathID/完整节点层级刷新3个源程序：FromGround_Super_Random（42069975192ed4a4fb2d84d4925509b7）、FromCorpse_Super_Random（e51c00c98fe156748a1e45b17b0fb182）、FromCorpse_Super（5f189b59ddd96234ba95040813a40274）。实际RLK_060/061两个子效果原Owner=节点1、tokenRoot=节点3。追踪 `manifests/native-custom-spawn-refresh.json`，动作GameObject值/IsNone/变量、reset标志、Owner及源身份保留，0失败；再次刷新0更新。既有两个专属法术程序和启用标记不改。

## 验证与剩余差异

101项Unity/GPU检查通过：八组reset组合、SetParent(true)世界姿态保持、原Finalize独立标志、完整层级/局部姿态恢复、非法token不变更、并发借用拒绝、两个原入场动作/Finalize精确参数在真实Actor上执行、输出变量、倒放、提前释放、缺Actor、IsNone不消费过时引用。两张实际Actor验证图在 `tests/visual/current/native-custom-spawn/`，使用完整原模型与真实动作；测试时间轴显式隔离这些原动作，不称原完整Timeline已运行。

完整GPU回归 `work/native-custom-spawn-final-gpu.log` 退出0：101本节点、32原画变量及既有Actor/高光/轮廓/召唤/法术检查保持通过，iTween798帧零异常。715项Python通过（20.55秒），新增3项custom-spawn Owner精确CAB/PathID映射检查。最终Windows构建 `work/build-native-custom-spawn-final.log` Result: Success、退出0；后附该构建双Player回归结果。

customspawn_1/2独立Profile、47410后台真实Player自动完成一局：befb50b0b9be461f8a883c6bed1212de，15回合/version50，Shrink1法师2血胜、Shrink2术士0血负。双方公共hash d3a65a78763bba86ac8e6d810a830c73d3e1b3c17f243ada651e411312399e9f一致，队列峰1，日志无异常/原效果fallback。batchmode无完整GUI，不代表人工比赛、完整动画视觉验收或正式60FPS。用户校准SHA256仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。仅清理本轮准确PID/Profile/端口进程。

下一步证据：原TimelinePlay源IL调用目标PlayableDirector.Play；finishOnTimelineStopped=false则立即Finish，不等待整段Timeline。当前RLK参数确为false，但gameObject旧缓存node=-1，需精确OwnerDefault目标刷新及原PlayableDirector轨道恢复，不能简单把Timeline总长变为FSM等待。

仍未完成：TimelinePlay/PlayableDirector轨道、ActorEliteEventAction、非传奇SetMesh/SetMaterial分支、原专属音频与其余Shader，以及正式自定义入场事件路由。没有启用整个RLK专属效果；没有完成全卡专属动画。下一步沿原Timeline源轨道及这些具体脚本推进，不把本节点测试通过当成全卡完成。
