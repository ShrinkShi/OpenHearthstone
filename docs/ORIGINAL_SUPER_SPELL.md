# 刀扇 / 军情七处特工根 SuperSpell 自主编排

TASK-0003，2026-10-16，F003 r6。V003-r5确认前四反例解除，但真实服务停止后正常返回仍分派后续任务FAIL；本轮在Task3宿主集中补齐外部调用前后停止门，原样r1-r5与正常链通过，只交READY_FOR_REVIEW待全新独立验收。程序恢复与官方视觉精度分开，不声明整卡保真，五轮FAIL历史保留。

卡名勘误（Confirmed）：本机 `local/card-database.json` 的EX1_129=刀扇/Type5，EX1_134=军情七处特工/Type4，刺骨为EX1_124/Type5。旧文档/报告将EX1_134误称“刺骨”，但所有来源与运行始终使用既定EX1_129/EX1_134；本轮只纠正当前标签，原ID、源Prefab、配置和历史报告原文保留，不扩卡或迁移证据到EX1_124。

本轮读取该数据库SHA256为678211E237FEF4B957BB784CCE85BA1FFC395B3D5E2D738FD6B8A5F1F37C9A95；这是本机卡名/Type标签证据，不据此宣称原产品版本。

## 来源与实际分支

Confirmed：原 `Assembly-CSharp.dll` SHA256 `3F67779510122DBCE79EC431E4E7494643083160EC9E7592F6ECB899B2B69728`，产品版本 Unknown。静态来源为 `local/script-audit/Assembly-CSharp.dll.SuperSpell.il.txt`、`Spell.il.txt`、`PowerTaskList.il.txt`、`SuperSpell._WaitAndSpawnMissile_d__78.il.txt`、`_CompleteTasksUntilMetaData_d__126`、`_CompleteTasksFromMetaData_d__129`、`_WaitForTasks_d__128`，以及 `Blizzard.T5.iTween.dll.iTween.il.txt` 的 LookUpdate。只读 Cecil/原资产类型树，没有执行原 DLL。

`python -m tools.super_spell_source` 从原根完整类型树核对旧缓存，导出 `manifests/original-super-spell.json` / `local/runtime/native-super-spell.json`。两卡六个独立 Prefab，其中两个 area 是本 Task 新导出；Task3 投射物另存 `.root-missile-spell.json`，没有覆盖前序独立投射物图或命中图。只为 Task3 导出启用 Trail 数据，其他现有导出默认行为保持。

| 根 | 原 Location | 目标与发射 | 其他实际分支 |
| --- | --- | --- | --- |
| EX1_129 刀扇 | 2 / SourceAuto，法术通常解析来源英雄 | Behavior=0，全部目标同时 Spawn；路径 .08–.10 秒，OrientToPath=true | 选友/敌一个 area；Start 禁用；Chain enabled 但 Prefab=null，因此 HasChain=false；reverse=null；joint 空；times-to-hit=0 |
| EX1_134 军情七处特工 | 1 / Source | Behavior=0，从索引0顺序；上一发到达时发下一发；路径 .4–.45 秒，OrientToPath=false | 没有可用 area；其他禁用/空值同上；Birth 内有原 RandomWait .25–.45 秒 |

根本身无 root FSM/SpellState 内容，因此 Birth 仅更新位置/朝向与初始目标索引，Action 更新视觉目标并走无 start-delay、无 Start、无 game-event-show gate 的实际分支。目标 Behavior=0 按原目标次序拷贝到独立 visual list；Source/Target list 与 CleanUp 清掉的 visual list 分开。

不支持其他 Start、有效 Chain、reverse、随机/区域目标 Behavior、重复同目标发射、joint、非 super-location 的投射物分支；需要手牌槽时必须显式提供服务。不得借这两个案例自动开启其他卡。

## 实际控制流与回收

`NativeSuperSpell` 复用 `NativeSuperAction`、`NativeSuperArea`、`NativeMissileFlight`、`NativeSuperImpact`。每个投射物请求先计 pending，再调度 d__78 对应等待。并发每个目标都 yield spawn delay；顺序只有索引0 yield 首段（仍采样每发 delay）；offset>0 且 index>0 时另跨帧等待。等待后才查 metadata 和 Unity target，随后独立克隆、挂根/订阅、SetSource/AddTarget、设置 super location、激活真实 Birth。真实 Birth 完成后生成 Catmull-Rom 路径并采样原 duration。

Arrival 先 SpawnImpact，再按顺序发下一发，最后让到达的投射物进入 Death。finished 仅归还视觉 pending；None 独立排队回收。宿主退出当前采样/回调栈后 Flush，销毁克隆图、相机、RT、临时材质和动画。根完成后仍可有原 Death 中的活动克隆；正式表现批次等待这些真实 None，不增加猜测的固定播放时间。取消阻止后续调度/任务回调成功，并清理所有根拥有的克隆；回收失败保留实例可显式再次取消重试。

V003-r1 的实质反例是 area 工厂返回前同步 Cancel：旧实现尚未登记该克隆，随后仍 Source/Activate；Tick 因已取消不再推进，需再次 Cancel 才能恢复。F003 修复在根接收工厂返回值后先登记所有权，再判断停止；根拥有的订阅/Source/任务附着/位置/朝向/激活端点在宿主回调前后检查停止，晚状态/完成通知不推进分派。Begin/Tick 使用调用深度延后物理销毁至当前栈退出，避免激活内部回调取消时销毁正在执行的图；回收中的实例有重入门，快照已被内层回收的实例不重复释放。失败实例仍保留、LastError 可诊断，取消后的 Tick 不自动重试，显式 Cancel 才恢复资源；重试不清掉既有故障、不伪造Completed或重新启动根。这是自主宿主的取消契约，不改变原正常分派/metadata/目标消失时序，也没有修改通用 NativeSuperArea 或 Task2 独立端点。

`NativePresentationCoroutines` 是 scaled 表现帧调度器，Start 同步到首次 yield，0秒 WaitForSeconds 也跨帧；重复相同 age 的绘制不重复推进调度。它不是完整 Unity PlayerLoop 的替代声明。LookUpdate 恢复本分支传入 time 时的 `.15 * time * .05` smoothTime、每次重置速度向量、三个 SmoothDampAngle、原 up/无 axis；源 joint/其他参数分支继续拒绝。

V003-r3的实际清理反例已在本轮原样先红后绿。调度器对每个已接管帧逐层尝试Dispose，单层失败仍继续其余栈和其他任务；栈和wait引用清空后才Done并移出实际队列，运行中/清理中的重入交当前栈安全退出处理，不重复处置。清理后统一报告，单一异常保留原对象，多个异常以AggregateException保留各原因，诊断回调再抛也不截断已经执行的清理。根LastError保留首个诊断，Errors保留后续不同异常。每层一次处置尝试不等于声明外部故障Dispose内部资源成功；根仍Faulted/Cancelled，不伪造Completed。原克隆回收失败保留实例供显式Cancel重试的合同不变。

Confirmed 的特殊边界：d__78 等待后目标缩短/销毁只 pending--，没有 FinishIfPossible，也不发下一发。无 area 的EX1_134可能因此保持待完成标记；检查断言不能自行成功，之后显式 Cancel 回收。目标在 Birth 内消失而无法生成原路径时记录故障并清理；路径已捕获后目标消失，沿原固定路径到达，命中按自身等待后的有效性分支跳过。不能用占位目标或超时成功覆盖这些区别。

## 正式本项目宿主

`ClientApp → NativeEffectPlayer.PrepareSource/Draw → NativeSuperSpellPlayback` 为两卡正式分派。普通 Actor 是 `(40000+slot*100,-1000,0)` 的拍卡缓存，不能拿它们之间的距离发射。`NativeSuperSpellBoard` 使用既有 1920×1080 GUI 棋盘、CardPlay 收尾中心 `(960,480)` 和实际实体矩形中心，明确投影到统一离屏场景；Source/Target 保留实体身份，SourceAuto 的法术来源解析该控制方英雄。源卡不在对手公开手牌时，CardPlay 提供 id/card，原有同源 SpendMana 公开事件提供 owner；没有证据就拒绝，不猜 Source 服务。权威规则/网络没有改写。

公开 Hit 只给本项目视觉目标的有序去重列表，不伪造成 PowerTaskList。两卡进入正式控制器取决于该显式场景宿主；旧 `native-effect-index.json` 的整卡人工/GPU审核 enable 字段不因此被提升。`Presentation.NativeActive` 保留仍有真实活动克隆的当前批次，服务器继续立即结算。GUI 投影、缩放、原场景几何、原相机与官方逐帧精度为 Unknown/后续 TASK-0004。

共享视口一次捕获独立克隆并合并它们的真实材质来源，保留深度/排序与 C+D*K 彩色透射；没有通过简单亮度相加冒充源混合。

## 显式原历史与任务同步

无历史是原 m_taskList=null 的真实分支，metadata map 空、Until=false，命中不采样 game delay。若提供 `OriginalPowerTasks`，必须同时提供指向同一个 Tasks 的 `NativeSuperSpellHistory`；缺失服务不能静默回落无历史。

`NativeSuperSpellTaskHost` 接受显式原规范化 `NativePowerTaskList`、visualIndex→targetIndex→metadataIndex 两级映射、现有 NativePowerServices。直接用 NativePowerExecution 执行任务，不从 SemanticEvent 构建历史。Until 按原 DoTasks(0,index)，收集 visual entities 的区域任务（同实体保留最后一项），逐项等待 task complete、目标 Zone、Actor loading；缺任一真实查询明确失败。From 由根先计 pending 并跨 scaled game-delay，再按原 Spell.CompleteMetaDataTasks 扫描到下一个 type8/metaType0 之前；真实任务 callback 才归还根票据。From 协程结束、执行器请求容器出队和任务 callback 是三件不同的事，不能重复计请求容器而错过根完成通知。重复/晚到 callback 有票据门，取消不标已派发任务为完成。

Until票据由真实finally归还，不能用取消后的公开Pending=0证明内部清理。From可在迭代器自然结束后仍等待真实执行器callback；根另持有该票据的停止操作，Cancel停止真实Host后归还未完成票据，不调用Finish或标task完成。晚/重复callback不再归还第二次。真实Host的tag44受阻正对照验证该区别，未将事件或计时当作任务成功。

V003-r2发现有效映射/判定适配器得到真实结果后同步Cancel/Dispose，旧根仍求值clock.Start(Until)，新增假ObjectDisposedException。F003本轮在映射/判定返回及调度前检查停止；Until/From工厂晚返回的迭代器若停止则Dispose而不MoveNext，正常迭代器及其嵌套IEnumerator在MoveNext/Current返回后再检查，Dispose只转交一次。Failed查询返回后重新读停止标记；TaskHost在DoTasks/Entity/Zone/loading返回后也检查。原等待、null跨帧、真正任务callback和目标消失语义不变；不吞ObjectDisposedException，真实服务/handler/迭代器Dispose异常仍可诊断。这是显式历史适配器的公开合同，未声称纯Dictionary或无原历史UI自发重现。

V003-r4补充：Current正常返回IEnumerator即交付子所有权。History工厂返回体与Current子体统一经AdoptHistory接管，再作停止检查；门拒绝时Dispose已接管体，不执行其MoveNext。Reject同时保存真实门故障与处置故障，干净自主停止不变。父只处置自身，正常已交给上层栈的子体仍由上层处置，避免双重所有权。真实GO/RT租约检查覆盖Until/From、1/3层返回、Cancel/Dispose、未停止、直接scheduler、晚工厂与真正Dispose故障/重入；没有把fixture资源称为原Prefab泄漏。

OCE合同：根与TaskHost内部停止使用专用NativeSuperSpellStoppedException，只过滤该自主信号。Cancel先停止Impact请求，再关时钟/History，避免Routine.Dispose另生成通用OCE；Impact明确stage=cancel的既有取消通知单独识别，其他外部factory/服务/MoveNext/Current/Dispose抛出的OCE保留原对象。共享PowerExecution把OCE当取消是既有执行器合同；Task3 TaskHost只把所绑定外部服务/handler抛出的OCE包装为带原InnerException的真实故障，内部执行器自身的取消不变。原始原因可由GetBaseException取得；延迟Tick及时带回故障，诊断观察器再抛会同时保留原原因。没有修改共享执行器/Task2或权威规则。正常返回后自主停止与真实故障在对照中分别验证。

V003-r5补充：整个DoTasks返回后的Active不足以阻止内部同步分派。Task3的Guard在每次服务/handler调用前与正常返回后检查Active；两级IDictionary查询及Zone Entity/VisualEntities/目标Zone/loading也集中走此门。正常返回时若服务已Cancel/Dispose，不开始后续handler/任务/服务；真实OCE/其他错误先抛时仍保留真实原因。ExecutionError是停止后的真实故障诊断渠道，不与业务分派一起屏蔽。已执行handler内部副作用不回滚；若Clock/Timeline停止时task已经完成也不反向抹状态。PendingTasks保持实际未完成数量，清理另查真实Host等待、内部metadata与图/RT。共享NativePowerExecution、Task2及正常Until/From/callback合同不变。

源 `IsBatchedTargetInfo` 的 `count < index` 异常分支按 IL 保留，未擅自反转条件。raw transport/protobuf、真实官方 GameState/ZoneMgr/Actor 网络对象和全部历史类型没有因此完成；正式本项目公开语义路径不声称运行官方历史。该接口支持已明确附着的原输入与宿主，不接受缺字段替代。

## 原 Trail 实际触及分支

真实规则入口的绑定计数揭示EX1_134六次 SetProperty(TrailRenderer.time) 未绑定，不能忽略。只读类型树同时确认：刀扇 GreenRibbon 一条、军情七处特工 Head/Head1 两条原 TrailRenderer。Task3 缓存保留完整 raw/source/material、time、min distance、width curve、gradient、corner/cap、alignment=View、textureMode=Stretch、lighting/shadow 参数。实际 SDK TrailRenderer 接受原 time 写入（Start=0、Action=.3、Death=0），表现时钟记录/淘汰路径点并上传，禁止原生自动发射再重复取点。

直接恢复的是来源参数与属性绑定；行为重建的是表现时钟路径点管理。原引擎内部顶点年龄、细分、纹理铺展的逐像素等价和完整 Trail Shader 未验证，不能把 SDK 组件存在或控制绑定归零升级为官方视觉通过。没有扩大 Shader 家族。未知/非实际分支（autodestruct、其他 alignment/textureScale/textureMode）明确拒绝。UnityPy 的 version-fallback warning 只描述读取器采用的回退配置，不是原产品/引擎版本证明。

## 检查入口与当前证据

- `NativeSuperSpellChecks.Run`：根分支/多目标顺序/目标失效/取消/metadata/共享GPU关键相位。
- `NativeSuperSpellChecks.RunRegression`：上述加既有命中/路径/区域/历史/标准 GPU 与 Presentation。
- `python -m tools.super_spell_scenarios`，再 `NativeSuperSpellChecks.RunRules`：真实 Match.apply 双卡、双方公开视角；用现有 BattleLayout 坐标和真实 card database 检查 Source/目标/分支/绑定。
- `NativeSuperSpellChecks.RunFinal`：最终同一实现依次跑 RunRegression 与 RunRules；需要先生成场景及来源缓存。
- `NativeSuperSpellStopChecks.Run`：真实原克隆的工厂/设置/激活/回调同步取消、Dispose、回收重入与显式失败重试；已进入RunFinal。
- `NativeSuperSpellHistoryChecks.Run`：真实TaskHost/有效映射、Until/From及嵌套迭代器停止、Zone/loading与真正异常保留；已进入RunFinal。
- `NativePresentationCoroutineChecks.Run`：已接管栈的正常/故障/停止、多job清理、wait引用/实际队列、重入与多异常诊断；已进入RunFinal。
- `NativeSuperSpellChecks.RunCleanup`：上述调度器与History窄回归，不重复完整GPU链。
- `pytest tests/test_super_spell_source.py -q`：两根完整类型树、六独立图、原 Trail raw 与身份重读。

首个窄链 PID30172 / `work/f003-narrow.log` 退出0；后续边界失败 `f003-boundaries.log` 保留 metadata 重复计数问题。修复后 PID33004 / `f003-boundaries-r2.log` 71 项退出0。PID35584 / `f003-final-gpu.log` 92 根检查、205 命中、27路径、标准 GPU 41项/146帧及51/32/61相关链退出0；这是 Trail 补齐前的中间结果，最终结果以 Task 表格为准。

实际 Match.apply 的失败记录保留 `f003-real-rules.log`（敌方来源 owner 缺失）、`f003-real-rules-r2.log`（六次 Trail 属性未绑定）、r3/r4（新增可选字段需要明确 restored 标记，不能给没有原 Trail 的节点创建组件）、r5/r6（Trail 材质所需原纹理缺少已核对 raw 数据）。r7 的 22 项通过后，再补足两视角的 area 选择与 SourceAuto/Source 原始位置断言，最终如下。不删除失败证据，不把这些失败报成规则通过。

Observed / F003 执行者检查，非独立验收：Unity 2022.3.60f1c1 `469b28c2de88`，D3D11 / RTX 4080 Laptop；隐藏图形 batch，无 `-nographics`。`NativeSuperSpellChecks.RunFinal` PID31296 / `work/f003-final-regression.log` 退出0：92 根编排、30 真实规则双视角、205 命中、27 路径、标准 GPU 41项/146帧、51 Action、32 Area、61 Power 及既有表现链；Presentation 276 项。正常双卡路径未绑定控制动作计数0。负例的失效 Birth/缺历史服务产生预期 fallback，既有观察器故障测试的告警也不作为正常路径错误。

GPU 相位在 `tests/visual/current/native-super-spell/phases.json`、同目录 PNG 与事件轨迹。固定1024×576、正交 size16、白 ambient、240Hz、宿主 range=min / randomBool=true、克隆 FSM seed117。下表 age 是采样时钟；Arrival/Hit 等帧由真实事件触发记录，不按任意延时伪造。

| 卡 | Birth | Early | Mid | Arrival | Hit | Death | Cleanup |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 刀扇 age / 可见像素 | .004167 / 133 | .0125 / 132 | .045833 / 1043 | .0875 / 1795 | .091667 / 1936 | .091667 / 1936 | 3.091667 / 0 |
| 军情七处特工 age / 可见像素 | .004167 / 0 | .466667 / 75 | .629167 / 225 | .829167 / 144 | .833333 / 144 | 2.829167 / 0 | 3.833333 / 0 |

EX1_134 Birth 的0像素是源 RandomWait 分支，Death/Cleanup 的0也符合该阶段内容；有内容的 Early/Mid/Arrival/Hit 均非黑帧。已直接查看双卡 Mid 图，颜色/布局仅证明本项目渲染路径。没有官方同条件帧，不能量化官方误差或宣称整卡视觉通过。

来源重读：`work/f003-source-final.log`，6 passed / 6 UnityPy version-fallback warnings / 49.45秒，退出0；同时覆盖 Task3 两根/六图/Trail/3纹理 raw SHA，以及前序 Missile/Impact 来源。四个全库大型原源测试仍由公共回归命令明确排除，不能声称全部来源重读。最终公共规则、构建与正式整局证据以 TASK-0003 为准。

V003-r1后的F003修复新证据：先原样重现31pass/3fail（PID34712，work/f003-r2-reproduce.log，退出1），再最终原样34pass/0fail（PID19376，work/f003-r2-probe-final.log，退出0）。新RunFinal PID11300退出0，work/f003-r2-final-regression.log：270项/53真实克隆停止案例、原92根/30真实规则双视角/205Impact/27路径/标准GPU41项146帧/表现链通过，正常双卡active/pending=0；关键相位JSON SHA仍8EE736E661431E90D0E5B162B1457AA05EAE3709449D20F65EE53F5372F20A8A。新针对Python67passed/12.11秒、新Player构建PID36808退出0；新双客户端自然终局turn17/version64、同hash、65条权威journal/0错误。详细日志/PID/清理见Task修复回合。未重跑未变化的1187/6来源，不将首轮旧日志算本轮通过；等待全新独立V003，不撤销r1的FAIL历史。

V003-r2后本轮：PID33556原样38/6退出1；最终r1 PID36988=34/0、r2 PID4000=44/0均退出0。RunFinal PID38112退出0（work/f003-r3-final-regression.log）：新History230项/67真实宿主边界案例、旧270/53及完整正常/规则/GPU链通过，关键相位SHA不变。Python67项/10.52秒与新Player构建PID16220均退出0，程序集SHA256 CC576E4B46BC0B7BC1D34D6A3792BD389F87D0D40FD991B091EEF29A42A3BFC9。本轮未机械重复最近1187/完整来源/整局，旧数不是新通过；原样归档探针/独立报告未改，详细选择依据和失败演进见Task最新History修复回合。只交READY_FOR_REVIEW，待新独立验证。

V003-r3后最新执行者检查：原样PID11716退出1、38/6，修复后PID8720退出0、44/0；最终原样r1/r2/r3新进程31728/8312/36616均退出0、34/0、44/0、44/0，r3每层Dispose=1且metadataPending=0。RunFinal PID35580/work/f003-r4-final-regression.log退出0：scheduler263项/25案例、History308/85、旧Stop270/53及正常根/新真实双视角/GPU/完整表现链通过。Python67项/11.69秒退出0，新构建PID4780退出0；业务DLL SHA256 594E1A7799A4C16D7D1AA8A845586559B5FE9D1CF121142BCB89A0365E83ABE1。本轮未重复未变1187/完整来源/整局，旧日志不充作新通过；新独立V003在无实质失败时仍需补独立构建/整局。固定实现与完整复现见Task及r4交接，当前仅READY_FOR_REVIEW。

V003-r4后本轮：原样PID23060退出1、39/4，首修PID3980退出0、43/0；最终四轮原样PID18008/36604/4664/34124均退出0，34/0、44/0、44/0、43/0。最新RunFinal PID15300/work/f003-r5-final.log退出0：History527/153、scheduler263/25、Stop270/53、92根/30新真实双视角/205Impact/27路径/标准GPU41项146帧/完整表现链。两卡active/pending0，4种新合法规则变化8视角通过，真实GO/RT与原图清理分别验证；相位SHA不变。针对Python67项/6.73秒与新Player PID31472均退出0；业务程序集SHA84F472F0FA170C250DC943888395AE25A7DD9E922EBF9763F56C5468C5F90482。外部Power服务OCE另有先红后绿记录，详见Task；四轮FAIL历史不撤销，未重复未变1187/完整来源/整局，待新独立V003。

V003-r5后r6最新执行者检查：原样PID38000/work/f003-r6-reproduce.log退出1、56/10，首修PID31052退出0、66/0。新矩阵前三次失败为fixture把tag44等待误作metadata查询等待，调用轨迹确认后显式阻塞被测查询；正常Mulligan只核对执行器等待而不额外延长Until。日志保留。PID7580/work/f003-r6-services-r4.log与最终RunFinal PID24952/work/f003-r6-final.log均退出0：History1117/239、scheduler263/25、Stop270/53、92根/30新真实双视角/205Impact/27路径/GPU41项146帧/完整表现链。新增590项/86服务案例核对初始/延迟Until/From、取消/Dispose、true/false、原异常/先停再抛、任务状态不推进或回滚、物理资源及重复停止。

最终五轮原样新进程12704/37472/15684/15944/20168均退出0，34/0、44/0、44/0、43/0、66/0，源SHA一致；r4包含4种本轮新合法Match.apply变化/8视角。针对Python67项/12.52秒、新Windows构建PID33756均退出0；业务DLL SHA256为2018422482D905D553B3D73B3B648DBA4D2BF58503AB7C46D2741760559592FA。相位SHA不变，33新GPU文件另存work/f003-r6-gpu/；真实图/RT与内部metadata等待单独通过，未把正数未完成task计数伪装清零。未重复未变1187/完整来源/最近整局，旧证据不当新通过；固定实现3868df335ba2850ce3abf201a018741d42110f21，完整命令/清理/边界见Task与docs/handoffs/F003-task-0003-r6.md。仍待新独立V003及其新构建/整局，不声明官方视觉一致。
