# TASK-0003

## 任务名称
刀扇（EX1_129）/军情七处特工（EX1_134）根 SuperSpell 自主编排。

卡名勘误（2026-10-16）：已直接读取 `local/card-database.json`，EX1_129=刀扇/Type5，EX1_134=军情七处特工/Type4；刺骨是EX1_124/Type5。此前Task/专题/交接中的“EX1_134 刺骨”是名称误标；本队列一直实现既定EX1_129/EX1_134及其原Prefab，不切换为EX1_124。下方历史测试/报告/提交名称保留原文，旧名称不把证据迁移给真正的刺骨。

## 背景
独立路径、区域和命中端点并不等于完整根控制器。需以这两个真实案例贯通，再考虑横向迁移。

## 当前已知事实
Confirmed：232 根配置核对、两个真实投射物图和独立 Catmull-Rom 端点为启动基础。F003 已恢复这两根实际分支并正式接入本项目棋盘投影；显式原规范化历史宿主单独验证，不从公开语义事件伪造原历史。详细来源、原失效分支和后续视觉边界见 ORIGINAL_SUPER_SPELL。

## 目标
恢复两卡实际触及的 Birth、投射物生成/飞行/到达、命中/Death/回收与任务同步，数据驱动接入现有播放器。未触及分支明确声明不支持，不要求一轮恢复所有 SuperSpell 类型。

## 非目标
不扩卡、不改权威规则/网络、不制作 Browser/Bridge、不以延长任意时间模拟原链。

## 相关文件
NativeEffectPlayer / NativeAreaPlayback / NativeSuperArea / NativeSuperAction / NativeMissilePath / NativeMissileFlight / NativeSuperImpact / NativePowerExecution；visual 入口、ORIGINAL_MISSILE_PATH、hearth_visual_db。

## 实施约束
按 DEC-005，TASK-0002 经独立验证并由总控登记 DONE 后自动派发独立 F003；不等待用户手动创建聊天。先核对 Source/Target/原历史契约、控制器调用与真实两卡分支；未知 Source服务不猜。大 Shader 缺口另立 Task，不无限吞入。沿用当前架构和 UI 校准。

## 验收标准
真实两卡多目标/顺序差异/目标中途消失/取消及重复回调验证；生命周期事件轨迹与静态源匹配；GPU 关键相位不黑帧；正式双客户端整局/规则回归无新增错误；文档和等级如实更新；独立 V003。视觉逐帧准确由 TASK-0004 判断，不由本 Task 自封。

## 当前状态
DONE（2026-10-16，总控核对独立V003-r6 PASS提交59ae9098396eecc40a0cdc212cd1249c7c64b1d9后登记VERIFIED→DONE）。固定业务3868df335ba2850ce3abf201a018741d42110f21；原样五轮探针、独立新增对照、RunFinal/真实规则/GPU、67 Python、9类型静态IL及新构建通过；正式自然终局turn12/version41，同hash、42 journal/0错误/0concede。前五轮FAIL与范围限制保留，整卡视觉等级不升级；完整独立证据见docs/verification/V003-r6.md，TASK-0004依赖已满足。

## 实施记录
F003 启动于 master @ 5fecfb5，工作区干净。已核对真实两卡根、WaitAndSpawnMissile、OnMissileTargetReached、metadata Until/From 和现有宿主；详细来源和实施边界将落 ORIGINAL_SUPER_SPELL。

两根无 root FSM/SpellState，Start 禁用，Chain 的 Prefab=null，reverse=null，target behavior=0。刀扇 SourceAuto/并发/有友敌 area；军情七处特工 Source/顺序/无有效 area。现有 Actor 是隔离拍卡坐标，使用当前 Presentation 的明确 GUI 棋盘投影作为自主宿主，不猜原场景坐标。语义 Hit 只提供视觉目标，不能转成原 PowerTaskList；显式原历史另接严格宿主。

新增 NativeSuperSpell、NativePresentationCoroutines、NativeSuperSpellPlayback、NativeSuperSpellTaskHost；复用现有 Flight/Impact/Area/Action/PowerExecution，finished 与 None 回收分开，表现批次等待真实活动克隆。实际 LookUpdate(time/up/无axis) 已恢复；没有给原无完成通知的目标消失分支添加任意超时。显式取消/重复与晚到回调/物理回收均有边界检查。

真实 Match.apply 双视角暴露并修复：CardPlay 缺 owner 时使用同源 SpendMana.owner；原三条 TrailRenderer 和EX1_134六次 time 写入必须恢复；新增可选节点字段必须以 restored 标记区分原数据；Trail 的三张原纹理必须通过 raw/source/SHA 核对。独立 Task3 投射物缓存保留 Trail，不覆盖 Task2 的图。原 Trail 参数/材质/time 绑定已接入，表现时钟点管理为行为重建，原引擎内部顶点年龄/细分/UV 精度仍 Unknown。

未触及而拒绝：有效 Start/Chain/reverse、其他 target behavior、同目标多次发射、joint、非 super-location 投射物、其他 Trail alignment/textureMode/textureScale/autodestruct。需要手牌槽必须提供明确服务；两卡当前真实公开场景未触及。没有扩卡、改权威规则/网络、执行原 DLL 或改用户 UI 校准。

## 测试结果
环境：Unity 2022.3.60f1c1 / 469b28c2de88，Windows D3D11 / RTX 4080 Laptop；Python 3.11/.venv。所有 Unity 为隐藏图形 batch，无 -nographics，Start-Process 后先读 Process.Handle，再 WaitForExit 并记录 PID/退出码。运行日志/原缓存为本机忽略文件，不把它们当作独立验收。

下表是849f5fe首轮业务的历史检查；本轮修复新证据见下方“F003修复回合”，不把旧1187/来源/构建/整局计为本轮通过。

| 检查 | 实际结果与日志 |
| --- | --- |
| 首个窄链 | PID30172，work/f003-narrow.log，退出0；正式 PrepareSource 的双卡 Birth→Arrival→Impact/Death，最大可见像素4221/225。仅3.5秒、刺骨仍有活动克隆，因此不是完整回收证据。 |
| 首个边界与修复 | PID3888，f003-boundaries.log，退出1：重复计算执行器请求容器会在真正 callback 后错过完成通知。根已有任务票据，移除重复容器计数；PID33004，f003-boundaries-r2.log，71项退出0。 |
| 最终合并图形/边界/实际规则 | PID31296，work/f003-final-regression.log，NativeSuperSpellChecks.RunFinal，退出0；92根编排、30真实规则双视角、205Impact、27路径、标准GPU41项/146帧、51Action/32Area/61Power及既有表现链；Presentation276项。两卡正常路径0未绑定动作。 |
| 原源重读 | work/f003-source-final.log，退出0；6 passed / 6 UnityPy version-fallback warnings / 49.45秒：Task3两根/六图/三Trail/三纹理raw，前序Missile/Impact来源。 |
| 公共 Python 回归 | 原会话71400确认退出0；work/f003-python-regression.log，1187 passed / 4 deselected / 33 warnings，658.07秒。排除四项全库大型原源重读，另三份源专项如上一行已实际运行。 |
| 最终 Windows 构建 | PID38748，work/f003-build.log，BuildClient.Build，退出0，Build Finished Result: Success；包含既有构建检查链。HearthstoneClone.exe SHA256 47C6D9B4B0B26E0DD7CA8BD02A49859A8C338CE8FE8F1FCAC00743AECD5D1CE7。 |
| 正式双客户端整局 | tools.launch_duel 端口47503、profile f003_final_1/2、职业7/10、autoplay/background，启动退出0。客户端PID34408/4100，服务wrapper37860/实际Python30284。双方finished，第13回合/版本49，同hash b99333a79047fa325d48df43ed218711073652894b3f40422a87f34eb6528e51，LOST/WON。match fdc57bcd7ce6461a99506afa34d5a31f；50条权威journal、0错误。work/f003_final_1.log、f003_final_2.log、server-1792132110.log无异常/fallback；两端各5条既有serialization depth警告。 |

实际规则先运行 `python -m tools.super_spell_scenarios`，生成种子42的真实 Match.apply 前后快照（刀扇对两个敌方随从，刺骨先 Coin 连击后指定敌英雄），双方公开视角各跑 PrepareSource；没有将这些事件转换原 PowerTaskList。Root/Until/From 的真实 NativePowerExecution、Zone/loading/回调用单独显式历史输入验证。

GPU 关键相位：tests/visual/current/native-super-spell/phases.json、同目录事件轨迹和PNG；固定1024×576/size16/白ambient/240Hz。Birth/Early/Mid/Arrival/Hit/Death/Cleanup按真实事件采样；刀扇/刺骨 Mid可见像素1043/225，Arrival1795/144、Hit1936/144。刺骨Birth的0符合源RandomWait，Death/Cleanup的0符合该阶段。两卡最终active/pending归零与实际GameObject/RT销毁单独断言。该GPU入口不提供官方Ground Truth。

失败证据保留：f003-real-rules.log 来源owner、r2 六次Trail未绑定、r3/r4可选Trail默认实例、r5/r6缺少原Trail纹理；r7 PID34912退出0，之后补足两视角area/Source位置断言，最终以RunFinal为准。预期负例fallback和原观察器故障告警与正常路径分开，不将失败统计为通过。

## 最终结果
当前为独立V003-r6 PASS、总控登记DONE，五轮旧反例与新增FullEntity revelation/无Gameplay同步分支均通过，已补本轮独立新构建与正式自然终局，详见最新记录。前五轮固定版本的FAIL历史保留。新终态在 `%LOCALAPPDATA%/HearthstoneClone/profiles/v003_r6_final_1/2/state.json`，权威 `local/matches/98ee1d23c79849cab756adfc89bc1608.jsonl`，双方turn12/version41/finished、同hash、42条journal/0错误/0concede。后台card_render_count=0只作为规则/队列回归，不能替代本轮真实PrepareSource/GPU或官方视觉。自然终局并归档后才强制清理自建进程，客户端/实际服务退出-1，不冒称客户端自然退出0；wrapper退出码未捕获写Unknown。

用户UI校准SHA256仍 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。构建仅产生的Main场景fileID和mixer bundle audit hash噪声已核对并清理，未改变既有内容；没有临时测试入口遗留，NativeSuperSpellChecks是正式可复用验收入口。PROJECT_STATE/INDEX/AUTOMATION_RUN、Task2与独立报告由总控持有，F003未修改。

## V003-r1 独立验收（2026-10-16）

FAIL，报告 docs/verification/V003-r1.md。标准 RunFinal PID21092 退出0（92根/30规则双视角/205Impact/27路径/标准GPU41项146帧/相关链/Presentation276），9类型原IL只读重读匹配，6来源测试退出0，67项Python针对回归退出0；另重新生成真实Match.apply双卡双视角，独立验证正式Presentation短区间保活、同age重复采样、显式历史缺host拒绝和实际资源销毁。

独立探针 tools/verification/v003_r1_checks.cs：三个新Unity进程38168/34796/9040均退出1；最终31 pass/3 fail均指向同一刀扇area工厂取消缺口。工厂创建真实NativeAreaInstance后、返回前调用根Cancel；Begin返回时active=1/pending=0、source_writes=1/activations=1/releases=0，Tick(8)后1个RT仍存活。刺骨对照立即回收。显式重复Cancel可清理，不称永久泄漏。最终日志work/v003-r1-probe-r3.log，所有初始探针前提校正与失败日志均保留。

最小解除条件：晚返回区域克隆遵守已取消根的停止/所有权契约，停止Source/Activate副作用并销毁图/RT；保持正常分派和原目标消失语义；原探针与相关正常链通过，再派全新V003。验证者未修生产代码。已知失败后未重复1187全量、Windows构建或正式整局，这些旧日志不算本轮通过。GPU非官方比较，raw历史/官方几何/Shader/Trail内部精度边界不变。临时Editor及meta已清理、无自建进程遗留，UI校准哈希未变。

## F003 修复回合（2026-10-16，V003-r1 之后）

恢复 master@200264b，唯一工作树、工作区干净、无Unity/客户端；先读独立报告与归档探针，不读取验证推理历史。原样临时Editor副本SHA与归档均为1ECAC3890F035EACA86BE6A7BC98F3C08F813ADB129F3D37D4125D7F153F116E。先重现再修改：PID34712，work/f003-r2-reproduce.log，退出1、31pass/3fail，刀扇source1/activate1/active1/RT1，刺骨对照立即回收。

最小修复位于NativeSuperSpell：晚返回值先登记根所有权，再检查取消/故障/Dispose；根拥有的订阅、Source、AttachTasks、位置/朝向、Activate端点在外部调用前后停止检查，晚通知不再推进分派；Trace回调也可同步停止。Begin/Tick的当前分派/采样栈退出才Flush，避免销毁仍在执行激活的克隆。回收中与回收快照均有幂等门，回收失败保留实例和LastError，取消后的Tick不静默重试，显式Cancel可恢复。未改通用NativeSuperArea、Task2端点、规则/网络/原配置。

新增NativeSuperSpellStopChecks并加入RunFinal：真实area/missile/impact工厂Cancel/Dispose、订阅、Source查询/设置、任务附着、两卡实际位置分支、Activate/Birth内部回调、Clone/Arrival观察回调、晚通知、多克隆回收重入、失败诊断/显式重试。正常原分派、metadata和目标消失由原有链一起回归。原探针/独立报告未修改。

| 本轮检查 | 实际结果 |
| --- | --- |
| 原失败探针首次转绿 | PID23456，work/f003-r2-probe-fixed.log，退出0，34pass/0fail；两卡source0/activate0/release1/active0，Tick(8)后RT0。 |
| 新矩阵首轮 | PID14704，work/f003-r2-stop-checks.log，退出1：测试假设Impact调用SetPosition，但两卡真实配置use_super_location=false、Location=6。仅校正测试为实际SetLocation/UpdatePosition/Orientation，未改原配置或生产分支。 |
| 新矩阵第二轮 | PID35836，work/f003-r2-stop-checks-r2.log，退出0，235项/46案例；之后补充Clone/Arrival回调停止检查，最终结果如下。 |
| 最终合并正常/边界/GPU | PID11300，work/f003-r2-final-regression.log，退出0；新增270项/53真实克隆案例，原92根、30真实规则双视角、205Impact、27路径、标准GPU41项/146帧、51Action/32Area/61Power/Presentation276及既有链通过。双卡pending/active=0，最大可见像素4196/230。 |
| 最终原样探针 | PID19376，work/f003-r2-probe-final.log，退出0，34pass/0fail，源探针SHA未变化，晚返回对象/RT均在公开调用返回前销毁。 |
| 针对Python | work/f003-r2-python-targeted.log，tests/test_presentation_metadata.py与tests/test_native_effects.py，67passed/12.11秒，退出0。 |
| 新Windows构建 | PID36808，work/f003-r2-build.log，BuildClient.Build，退出0，Build Finished Result: Success。Player的Assembly-CSharp.dll SHA256 E460B708B00764D4F110BFA05089F7A0DBC9D717672F7FA9C3A8ABEB9CD08793；Unity exe stub哈希不变，不以它区分代码。 |
| 新正式整局 | 端口47505、profile f003_r2_1/2、职业7/10、autoplay/background；工具启动退出0，客户端9932/31552、服务wrapper21156/实际Python15256。match cabed03c5e354af989d5c3ab23e5e3be，双方finished/turn17/version64/WON,LOST，同hash 3615a3472e084328dd332e3f332c71baa7d50212e964e9a7e11d1d65fdf92b51；65条权威journal/0错误。work/f003_r2_1.log、f003_r2_2.log、server-1792134612.log无Exception/fallback，既有serialization depth警告保留。 |

选择依据：修改限于C#根取消边界和Editor回归，未改原资产导出、Python权威规则或网络。重跑正常根/完整相关表现链/GPU/实际Match.apply和Player构建；67项公开表现/导出契约为轻量回归。不重复无变化的1187公共长链与6来源重读，也不把旧通过数算本轮通过。新关键相位JSON SHA仍8EE736E661431E90D0E5B162B1457AA05EAE3709449D20F65EE53F5372F20A8A；官方视觉边界不变。

终局后核对CommandLine再清理自建客户端/服务4个PID，强制退出均-1，无本轮遗留；所有Unity均自然退出并记录退出码。临时V003R1Checks.cs及meta已删除，原tools探针未改；NativeSuperSpellStopChecks为正式回归。仅清理本轮Main场景fileID生成噪声，mixer audit本轮无差异。UI校准SHA仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。未改PROJECT_STATE/INDEX/AUTOMATION_RUN/独立历史报告；无amend/push。

固定修复实现：eaed37f9a587b31395b714f3389a8670d2bbda7e（修复：处理根SuperSpell取消后的晚返回克隆与重入回收）。修复交接/复现入口见docs/handoffs/F003-task-0003-r2.md；只交READY_FOR_REVIEW。

## V003-r2 独立验收（2026-10-16）

FAIL，固定业务eaed37f9a587b31395b714f3389a8670d2bbda7e，报告docs/verification/V003-r2.md；验证者/root/v003_r2为空历史独立Agent，起始干净master@788214f，目标之后仅文档/状态差异。未修业务、未改旧报告或前轮探针。

原样V003R1Checks.Run新PID4088退出0，34pass/0fail，归档SHA不变；双卡晚返回source0/activate0/release1/active0/RT0，前轮area缺口已解除。重新生成Match.apply场景后RunFinal新PID22592退出0：92根、30真实规则双视角、270停止/53真实克隆、205Impact、27路径、标准GPU41项/146帧、51Action/32Area/61Power/Presentation276及相关链。正常双卡pending/active0、最大像素4196/230。关键相位与真实对象/RT销毁通过；新69项Task3来源/公开表现/导出回归退出0，9类型原IL只读重读MATCH。

新增独立探针tools/verification/v003_r2_checks.cs使用真实NativeSuperSpellTaskHost/PowerExecution和有效规范化Tasks（未完成type4/tag44→type8 metadata、visual0→target4→metadata1）。仅在实际映射/判定得到结果后同步Cancel或Dispose并返回原结果。PID440与最终PID36300均退出1、38pass/6fail；停止现场Cancelled=true/Faulted=false/LastError=null，WaitAndSpawn:165–166返回后仍调用clock.Start(Until)，新产生Faulted/LastError=ObjectDisposedException。映射停止后另调用predicate1次；4错误故障状态+2后续调用均为同一缺口，无克隆/RT泄漏、无伪造Completed或未执行task完成。初始受控delegate探针PID31960为26pass/6fail，真实宿主复现后才判定失败。

对照：相同真实宿主无停止可完整执行/回收；真实PowerExecution handler内Cancel可干净停止；双卡Dispose后晚返回/首回收失败的所有权与显式Cancel重试、工厂内History.Failed变真均通过。失败限公开显式History适配器同步返回边界，不声称实际纯Dictionary映射或正式无历史UI会自发触发；不冒充raw历史传输运行。

解除条件：历史映射/判定返回后遵守停止门，不向已停止clock派发，不靠吞所有ObjectDisposedException掩盖真实故障；保持正常Until/From、目标失效、Source/等待及回收错误重试语义。原样重跑本轮与前轮探针/相关链，再派全新独立验收。验证者不自行修复。

证据work/v003-r2-original-probe.log、v003-r2-final.log、v003-r2-independent-r3.log、v003-r2-python.log、v003-r2-il.log；相位/轨迹/PNG与新规则快照另存work/v003-r2-gpu/和v003-r2-rule-scenes/。最终探针SHA88EE40554A6F992CF5BC6F303C6764A0D64625EFCE16F53B959C782AEC20E21E。已知失败后未重复1187/全部来源/新Windows构建/正式整局，不把执行者旧日志算本轮结果。全部Unity自然退出、临时Editor/meta清理，无自建进程遗留；UI校准SHA未变。官方几何/Shader/Trail内部精度和raw历史边界不变。

## F003 History修复回合（2026-10-16，V003-r2之后）

恢复master@eb243e3，工作区干净、唯一工作树、无Unity/客户端；先读仓库r2报告/归档探针，不读验证者推理历史。新生成真实Match.apply场景退出0，work/f003-r3-rule-scenes.log。原样r2探针与临时Editor SHA均88EE40554A6F992CF5BC6F303C6764A0D64625EFCE16F53B959C782AEC20E21E。先重现：PID33556，work/f003-r3-reproduce.log，退出1、38pass/6fail，四组合新产生ObjectDisposedException、mapping后predicate仍1。

修复仅本Task的NativeSuperSpell/NativeSuperSpellTaskHost：映射和Until判定返回后再检查停止；统一调度入口在clock.Start之前检查；Until/From工厂晚返回的IEnumerator若已停止则Dispose而不执行；正常返回迭代器在MoveNext/Current返回后检查，递归包裹嵌套IEnumerator并只Dispose一次。Failed查询返回时重新读取Cancel/Dispose，TaskHost在DoTasks/Entity/目的Zone/ActorLoading返回后检查。真正异常仍交Fail保存，不增加ObjectDisposedException过滤，不改变原Until/From等待/完成callback、目标失效或显式回收重试。未改通用Task2端点、原配置、权威规则或网络。

新增NativeSuperSpellHistoryChecks并加入RunFinal：真实TaskHost/PowerExecution、有效两级映射和前置/metadata/后续任务；适配器在有效结果/迭代器边界Cancel或Dispose，覆盖无后续调用、真正异常原对象/handler原原因/Dispose故障不丢、正常真实host/handler正对照。该适配器合同不代表纯Dictionary或正式无历史UI自发触发，也不是原raw历史传输证据。

| 本轮检查 | 实际结果 |
| --- | --- |
| 原样r2首次转绿 | PID25016，work/f003-r3-probe-fixed.log，退出0，44pass/0fail；mapping后predicate0，四组合Faulted=false/LastError=null。 |
| 新History首轮 | PID28684，work/f003-r3-history-checks.log，退出1；fixture漏配tag49所需区域执行/实体类型服务，未到第二次映射；仅补测试服务，不改原配置。 |
| 新History第二轮 | PID28380，work/f003-r3-history-checks-r2.log，退出1；from-body停止后Current/Dispose断言失败。沿调用栈定位上层仍展开History迭代器，故补根内递归HistoryEnumerator，未改Task2端点或断言。 |
| 新History第三轮 | PID35796，work/f003-r3-history-checks-r3.log，退出0，179项/48案例；后补嵌套/Current/真正故障/Dispose失败覆盖，最终如下。 |
| 最终RunFinal | PID38112，work/f003-r3-final-regression.log，退出0：新History230项/67真实宿主边界案例、旧停止270项/53真实克隆、原92根/30真实规则双视角/205Impact/27路径/标准GPU41项146帧/51Action/32Area/61Power/Presentation276及既有链通过。 |
| 最终原样r1/r2 | PID36988 / work/f003-r3-probe-r1-final.log：34pass/0fail，退出0；PID4000 / work/f003-r3-probe-r2-final.log：44pass/0fail，退出0。源/临时SHA各自匹配，原独立文件未改。 |
| 针对Python | work/f003-r3-python-targeted.log，67passed/10.52秒，退出0（test_presentation_metadata.py、test_native_effects.py）。 |
| 新Player构建 | PID16220，work/f003-r3-build.log，退出0，Build Finished Result: Success。Assembly-CSharp.dll SHA256 CC576E4B46BC0B7BC1D34D6A3792BD389F87D0D40FD991B091EEF29A42A3BFC9。 |

GPU相位JSON SHA仍8EE736E661431E90D0E5B162B1457AA05EAE3709449D20F65EE53F5372F20A8A，正常双卡pending/active0，原有内容相位可见；刺骨Birth原RandomWait/Death/Cleanup允许0。预期负例fallback与既有观察器诊断故障不冒充正常卡牌错误。

选择依据：本轮只改显式History适配/停止边界，Python/导出/原资产/网络/权威规则未变，正式公开路径无原History。新真实双视角/完整表现/GPU与Player构建覆盖正常路由；未重跑最近未变的1187、完整来源与后台整局，不把r2整局旧日志算新通过。全新独立V003仍需判断风险覆盖。官方几何/Shader/Trail精度/raw历史边界和V2 provisional不提升。

所有本轮Unity隐藏图形、无-nographics，先Handle后WaitForExit，均自然退出；无本轮服务/客户端，无强杀退出码。临时r1/r2 Editor与meta已删；仅清本轮Main fileID噪声，mixer audit无差异。UI校准SHA仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。未改PROJECT_STATE/INDEX/AUTOMATION_RUN/独立材料；只交READY_FOR_REVIEW。

本轮固定实现 `850620a836e47c91670beb8784619be381cb4f2d`，复现命令、完整业务程序集哈希与清理证据见 `docs/handoffs/F003-task-0003-r3.md`；后续交接提交只改文档。

## V003-r3 独立验收（2026-10-16）

FAIL，固定业务850620a836e47c91670beb8784619be381cb4f2d，报告docs/verification/V003-r3.md。空历史独立Agent `/root/v003_r3`，起始干净master@f63febc、唯一工作树；固定目标之后仅docs差异。未修业务/改旧报告或旧探针。

原样r1 PID31812退出0、34/0，原样r2 PID22376退出0、44/0，源与临时副本SHA均匹配；旧两处反例解除。新生成Match.apply双卡双视角后RunFinal PID10420退出0：92根/30真实双视角、270停止/53真实克隆、230History/67边界、205Impact/27路径、标准GPU41项146帧、51Action/32Area/61Power/Presentation276及相关链。正常双卡active/pending0、最大像素4196/230，物理对象/RT销毁与0未绑定控制动作通过。9类型原IL只读重读MATCH；6来源+67公开表现/导出回归共73passed/6warnings/121.16秒，退出0。

新归档tools/verification/v003_r3_checks.cs以真实TaskHost/PowerExecution和有效规范化tag49→metadata→tag44、visual0→target7→metadata1，在CanDoTask=false真实WaitingRequests=1时暂停三层已交付IEnumerator。普通双卡逐项task/目的Zone/loading放行、深层Cancel/Dispose、晚Host.Tick和真实Current异常对照通过。叶Dispose抛受控IOException后，原异常实例被LastError保留，但两外层Dispose计数为0；重复Cancel/Dispose均不恢复，根Until finally未运行，metadataPending=1而取消后的公开Pending=0。最终相同源PID38144与9804均退出1、38pass/6fail；六失败为同一缺口两入口的首次/重复观察，不是六独立故障。无已观察原图/RT泄漏、无伪完成，不声称纯Dictionary/正式无历史UI会自发触发。

定位NativePresentationCoroutines.Close提前置Done且Dispose异常中断剩余栈；根捕获诊断后重复停止不重入已disposed时钟。最小解除条件：真实Dispose失败不能丢弃其余已接管迭代器的清理，完成剩余清理或保留可实际完成的显式重试；保留真实错误、正常原等待/任务callback及回收合同。验证者不修业务，待Feature修复后新独立回合。

本轮证据work/v003-r3-original-r1.log、original-r2.log、final.log、python.log、il.log、independent-final.log、independent-repeat.log（均v003-r3-前缀），GPU/真实规则另存work/v003-r3-gpu/与work/v003-r3-rule-scenes/。最终探针SHA86547DF127769C75C0531B5CB6558C0F8A414FFB8E4B92917DC6D53286812EA7。已知失败后未重复1187/四项大型来源/新Windows构建/正式整局，不把执行者旧日志算本轮通过；修复后的新Verifier在无实质失败时仍需补新构建/整局。全部7个Unity自行退出，临时Editor/meta清理，无本轮服务/客户端或进程遗留、无已跟踪生成噪声，UI校准SHA未变。raw历史/官方几何Shader/Trail精度边界不变。

## F003 清理修复回合（2026-10-16，V003-r3之后）

恢复干净master@df1fc49、唯一工作树，无Unity/客户端，先读报告和原探针，不读验证推理历史。原样tools/verification/v003_r3_checks.cs复制临时Editor，源/副本SHA均86547DF127769C75C0531B5CB6558C0F8A414FFB8E4B92917DC6D53286812EA7。PID11716/work/f003-r4-reproduce.log先退出1、38pass/6fail，Cancel/Dispose均为disposed=1,0,0、metadataPending=1；相同错误对象未丢，没有把进程消失当通过。

修复NativePresentationCoroutines关闭合同：每个已接管帧逐层尝试Dispose，一个异常不能截断其余栈和其他job；running/closing重入门把当前帧交安全退出后处理，实际栈和wait清空后才Done/出队。所有清理完成后统一报告，单异常保持原对象，多异常AggregateException保留各原因，观察器再抛仍可诊断。根History包装在finally清Current；LastError保持首个诊断，新增Errors保存后续不同异常。From协程已结束而真实callback未回时，根持有独立停止票据，Cancel停止真实Host后归还，不调用Finish/完成task。每帧一次释放尝试不保证故障外部Dispose自身内部成功；故障仍Faulted/Cancelled，不伪成功。原克隆回收失败显式Cancel重试合同不变。

新增NativePresentationCoroutineChecks，并扩展真实History检查：自然完成/MoveNext/Current/Dispose故障、多层多job、句柄依赖/wait引用/实际队列、运行中和清理重入、无观察器/观察器抛错、多个异常；真实双卡深层等待/两并发Until、原物理area回收、正常Until/From回调及迭代器结束后仍等待tag44的取消。只观察实际拥有状态，不构造私有状态；内部metadata反射读数用于确认真实finally/票据归还，不靠公开取消Pending=0掩盖。RunCleanup提供窄入口，RunFinal包含全部生产回归。

| 本轮检查 | 实际结果 |
| --- | --- |
| 原样r3首次转绿 | PID8720/work/f003-r4-probe-fixed.log，退出0，44pass/0fail；两入口disposed=1,1,1、metadataPending=0，LastError仍原IOException。 |
| 新窄检查首轮 | PID28400/work/f003-r4-cleanup-checks.log，退出0：scheduler241项/23案例、History308项/85案例。随后仅扩运行中同伴故障/观察器组合与调用顺序，最终以下为准。 |
| 新真实规则场景 | tools.super_spell_scenarios/work/f003-r4-rule-scenes.log退出0，RunFinal使用本轮Match.apply双卡双视角快照。 |
| 最终RunFinal | PID35580/work/f003-r4-final-regression.log，退出0：scheduler263项/25案例、History308/85、旧Stop270/53、原92根/30真实双视角/205Impact/27路径/标准GPU41项146帧/51Action/32Area/61Power/Presentation276及相关链。正常双卡active/pending0、最大像素4196/230。 |
| 最终原样r1/r2/r3 | PID31728/8312/36616，work/f003-r4-probe-r1-final.log、r2-final.log、r3-final.log，均退出0：34/0、44/0、44/0。三份源/临时SHA均匹配，原件未改；r3内部metadataPending=0。 |
| Python针对回归 | work/f003-r4-python-targeted.log，退出0，67passed/11.69秒（test_presentation_metadata.py、test_native_effects.py）。 |
| 新Windows Player | PID4780/work/f003-r4-build.log，退出0，Build Finished Result: Success。业务Assembly-CSharp.dll SHA256 594E1A7799A4C16D7D1AA8A845586559B5FE9D1CF121142BCB89A0365E83ABE1。 |

关键相位JSON SHA仍8EE736E661431E90D0E5B162B1457AA05EAE3709449D20F65EE53F5372F20A8A；内容相位可见、真实图/RT销毁通过，刺骨Birth原RandomWait及Death/Cleanup的0像素不改为占位。预期负例fallback/既有观察器诊断与正常卡牌错误分开。只证明本项目GPU路径，不提供官方比较。

所有本轮Unity隐藏图形、不加-nographics，先Handle后WaitForExit并记录真实退出码，均自然退出。构建前删除三份临时探针及meta；仅按已核实Diff清理本轮Main的1675602966/67/68生成fileID噪声，mixer audit无差异。没有启动服务/客户端、没有强制清理退出码，无自建进程遗留。UI校准SHA仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

本轮只改Task3调度/根清理与检查，未改Python/网络/规则/导出/原资源或Task2。新实际规则双视角、正常Until/From/GPU/表现与Player覆盖清理风险；最近未变1187/完整来源/后台整局未机械重复，历史整局不是本轮新通过。新独立V003在无实质失败时仍需补独立构建/整局。未改PROJECT_STATE/INDEX/AUTOMATION_RUN/独立报告或归档探针；只交READY_FOR_REVIEW。

本轮固定实现 `8028c6780e670eac6abe7f312129d2e9788b2d68`，复现/业务程序集/清理与覆盖选择见 `docs/handoffs/F003-task-0003-r4.md`；后续交接提交只改文档。

## V003-r4 独立验收（2026-10-16）

FAIL，固定业务8028c6780e670eac6abe7f312129d2e9788b2d68，报告docs/verification/V003-r4.md。空历史独立Agent `/root/v003_r4`，起始干净master@9a5f040、唯一工作树；恢复中7933d32仅AUTOMATION_RUN一行，生产不变。验证者未改业务/旧报告/旧探针/交接/数据库。

新进程原样r1/r2/r3 PID36152/33960/11336均退出0、34/0、44/0、44/0，源/临时SHA匹配；r3三层Dispose=1且内部metadataPending=0，真实原IOException保留。9类型原IL只读重读MATCH；6来源+67公开表现/导出回归73passed/6warnings/117.90秒退出0。新合法Match.apply变体：刀扇空敌场、两致死目标、EX1_134无连击、连击指定友方致死目标，8公开视角正式PrepareSource均完成/回收、0未绑定动作。

新RunFinal PID33536/work/v003-r4-final.log退出0：92根/30真实双视角、Stop270/53、History308/85、scheduler263/25、205Impact/27路径、51Action/32Area/61Power/Presentation276及相关链；标准GPU41项/146帧，双卡pending/active0、最大像素4196/230，物理图/RT释放通过。新相位/PNG/轨迹33文件另存work/v003-r4-gpu/，直接查看本轮两Mid；相位JSON SHA仍8EE736E661431E90D0E5B162B1457AA05EAE3709449D20F65EE53F5372F20A8A，非官方比较。

独立新探针tools/verification/v003_r4_checks.cs使用真实TaskHost/PowerExecution和有效tag49→metadata→tag44、visual0→target7→metadata1。适配器Current先确认实际WaitingRequests=1，再同步Cancel/Dispose并正常返回拥有GameObject/已Create RT的嵌套IEnumerator。父Dispose1、Host0/metadata0、无任务执行/投射物/虚构完成，但返回子Move0/Dispose0、对象RT仍活，重复Cancel/Dispose不恢复。相同最终归档源PID38048和7608均39pass/4fail退出1（前一功能稿PID25288/8520同样39/4，最终仅注释中文化）；四失败是同一缺口两停止入口的首次/重复观察。direct scheduler同条件晚交付和无停止History对照均回收，故失败定位NativeSuperSpell.HistoryEnumerator.Current返回后的RequireActive门，未把已修调度器/旧票据清理说成仍失败。

这是显式History支持合同内的晚返回所有权缺口，不声称正式无历史UI/纯Dictionary自发触发，不是raw历史运行；实际泄漏为独立fixture子体资源，尚未创建原Prefab图。探针finally在观察后手动清理，不冒充产品成功。解除条件是接管并处置停止后已正常返回的嵌套子体、不执行停止后的MoveNext、保留真正处置故障；保持正常Until/From/真实callback/多job清理/原克隆显式重试，再派全新验收。已知实质失败后未跑不能解除该反例的新Player/整局/1187/四项大型来源，旧日志不算本轮通过。

全部9个本轮Unity自然退出，无自建服务/客户端或进程遗留；4临时Editor入口及meta已清，无已跟踪生成噪声，UI校准SHA未变。最终独立探针SHA 9743F12D07F66447D95BC38A7202067C062291F743C164ECEED940ADAEB6BF93；完整命令/退出码/证据SHA与限制见报告。本轮只改Task/报告/两新工具，旧FAIL与归档不变；验收提交用git log -- docs/verification/V003-r4.md定位。

## F003 晚子所有权与停止诊断修复（2026-10-16，V003-r4之后）

恢复干净master@86a9a7b、唯一工作树，无Unity/客户端，按新AGENTS/报告/原探针与生成器核对，不读验证推理历史。原样r4归档/临时Editor SHA均9743F12D07F66447D95BC38A7202067C062291F743C164ECEED940ADAEB6BF93。先生成4种合法Match.apply变体退出0，work/f003-r5-variants.log。原样PID23060/work/f003-r5-reproduce.log退出1、39/4；真实Until等待1时Current停止，子Move0/Dispose0且GO/RT仍活，父/Host/metadata已清理，与旧r3不同。

History.Current与工厂正常返回IEnumerator后统一AdoptHistory接管，再检查停止。拒绝时Reject处置该体，不MoveNext，同时保存真实门错误和Dispose错误；父不递归处置已交出的子，正常子仍由上层栈拥有。扫描确认原克隆Create仍先登记后检查、Schedule拒绝体也处置，没有改ID/源/等待/目标失效分派。新资源租约检查覆盖Until/From、1/3层Current、未停止/Cancel/Dispose、实际GO/RT、子Dispose故障/重入、晚factory、gate+cleanup不同原因与direct scheduler对照，反射只读内部票据/已持有栈，不构造状态。

补充诊断取舍：旧代码笼统过滤OCE。根/TaskHost现在仅用专用NativeSuperSpellStoppedException标记自主停止；正常返回后停止保持无故障，外部factory/MoveNext/Current/tick/Failed/Pending和Dispose的真实OCE原对象保留。Cancel先停Impact请求，再关clock和History；只识别现有Impact明确stage=cancel的内部通知，不改Task2端点。真实Power服务OCE被共享执行器算取消的反例另先红后绿；Task3宿主把其所绑定外部服务/handler抛出的OCE桥接成带原InnerException的故障，执行器自身取消语义不变，原原因可由GetBaseException取得，延迟Tick也报告。未改共享PowerExecution、权威规则/网络或原配置。

卡名已在本页标题/当前说明、专题与数据库标签纠正：EX1_134军情七处特工/Type4，真正刺骨是EX1_124/Type5；原ID和Prefab不变。local/card-database.json SHA678211E237FEF4B957BB784CCE85BA1FFC395B3D5E2D738FD6B8A5F1F37C9A95。历史报告/归档/旧交接与历史提交名称原文保留，误称不把证据迁移到EX1_124；没有扩卡。

| 本轮检查 | 实际结果 |
| --- | --- |
| 原样r4首绿 | PID3980/work/f003-r5-probe-fixed.log，退出0、43/0；两入口子Move0/Dispose1、GO/RT销毁，父/Host/metadata归还。4种合法变化/8视角也通过。 |
| 新所有权窄检查 | PID28820/work/f003-r5-owned-checks.log，退出0：History461/121、scheduler263/25。 |
| 中间正常链 | PID33444/work/f003-r5-final-regression.log退出0；后补拒绝体真实OCE后PID34576/work/f003-r5-final-regression-r2.log退出0（History465/123）。这些是诊断补充前的中间结果，最终以下为准。 |
| 内外OCE区分窄检查 | PID3564/work/f003-r5-oce-checks.log，退出0：History509/145，正常内部停止与真实外部OCE分别验证。 |
| Power服务OCE先红后绿 | PID20896/work/f003-r5-power-oce-reproduce.log退出1，真实CanDoTask原OCE被取消分支掩盖；Task3桥接后PID34276/work/f003-r5-power-oce-fixed.log退出0：History527/153，含初始/延迟CanDoTask、Mulligan、真实handler及先Cancel再抛对照。 |
| 新标准规则场景 | tools.super_spell_scenarios/work/f003-r5-rule-scenes.log，退出0；最终30双卡双视角使用本轮新快照。 |
| 最终RunFinal | PID15300/work/f003-r5-final.log，退出0：History527/153、scheduler263/25、Stop270/53、原92根/30真实双视角/205Impact/27路径/标准GPU41项146帧/51Action/32Area/61Power/Presentation276及相关链。双卡active/pending0，最大像素4196/230。 |
| 最终原样r1-r4 | PID18008/36604/4664/34124，各work/f003-r5-probe-rN-final.log，均退出0：34/0、44/0、44/0、43/0。各源/临时SHA匹配；r4晚子Move0/Dispose1且GO/RT销毁，4种合法输入/8视角通过。 |
| Python针对回归 | work/f003-r5-python-targeted.log，退出0，67passed/6.73秒（test_presentation_metadata.py、test_native_effects.py）；后续C#诊断改动未改变Python/导出。 |
| 新Windows Player | PID31472/work/f003-r5-build.log，退出0，Build Finished Result: Success。业务Assembly-CSharp.dll SHA84F472F0FA170C250DC943888395AE25A7DD9E922EBF9763F56C5468C5F90482。 |

全部14个本轮Unity隐藏图形、不加-nographics，先Handle后WaitForExit取真实退出码，均自然退出。构建前删除四临时探针及meta；仅精确清本轮Main的44869269/70/71生成fileID为基线，mixer audit无差异。没有启动服务/客户端、没有强杀及对应退出码，无自建进程遗留；UI校准SHA仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。相位JSON SHA仍8EE736E661431E90D0E5B162B1457AA05EAE3709449D20F65EE53F5372F20A8A；内容相位可见，EX1_134 Birth原RandomWait/Death/Cleanup允许0。预期负例fallback/既有观察器诊断不算正常卡牌错误，非官方比较。

覆盖选择：本轮Task3所有权/停止诊断小修，ID/源配置/原资产/Python/规则/网络/导出未变。四轮原样反例、新真实资源/正常Until/From、4合法规则变化8视角、新标准双视角/完整GPU表现和新Player覆盖本修复；未机械重复最近未变1187/完整来源/后台整局，旧整局不充新通过。新独立V003在无实质失败时仍需补独立构建/整局。未改PROJECT_STATE/INDEX/AUTOMATION_RUN/独立材料/Task2/Task4；只交READY_FOR_REVIEW。

本轮固定实现 `a4a31f7f8911ffc023d2d46144ab430cad2fcc58`，复现、业务程序集、诊断取舍和清理详见 `docs/handoffs/F003-task-0003-r5.md`；后续交接提交只改文档。

## V003-r5 独立验收（2026-10-16）

FAIL，固定业务a4a31f7f8911ffc023d2d46144ab430cad2fcc58；新空历史验证者/root/v003_r5，起始干净master@90cfddc、唯一工作树。只改本轮报告/归档工具与Task记录，不修业务、不改前四报告/原探针/数据库/总控文档。完整报告docs/verification/V003-r5.md。

新进程原样r1/r2/r3/r4的PID18840/5160/37388/26240均退出0，34/0、44/0、44/0、43/0，源与临时副本SHA匹配；晚Current返回子Move0/Dispose1、实际fixture GO/RT销毁，前四缺口解除。新真实Match.apply标准双卡双视角、4合法变体/8视角生成并通过正式PrepareSource；RunFinal PID32212/work/v003-r5-final.log退出0：92根/30规则、Stop270/53、History527/153、scheduler263/25、205Impact/27路径、标准GPU41项146帧、51Action/32Area/61Power/Presentation276与既有链。正常active/pending0、max_pixels4196/230、0未绑定动作与真实图/RT释放通过。9类型原IL经Cecil静态重读MATCH，67项公开表现/导出Python退出0；原资源/导出未变，6来源专项引用V003-r4独立73项基线，未冒称本轮重跑。

新增tools/verification/v003_r5_checks.cs以实际EX1_134根、真实TaskHost/PowerExecution和有效tag49→metadata→tag44、visual0→target7→metadata1运行11个对照。CanDoTask先同步Cancel/Dispose根、再正常return true，实际仍执行尚未开始的tag49 handler并标task完成，状态1,0,0；From的metadata handler停止根后正常return true，实际仍执行后续tag44 handler，task全完成1,1,1。延迟CanDoTask先建立真实WaitingRequests1/metadataPending1后再停止也失败。相同最终源SHA5BB493A08F70231A839F94C77ABB32965F93702DAFCB4A08C73EC300C53028EB，PID34128/2644两新Unity均56pass/10fail、自然退出1；十断言为五停止场景的后续调用/任务副作用两观察，指同一缺口。早期两个进程在permission失败后因探针未捕获故障Host晚Tick重抛提前结束；最终只补此真实原因对照并完整同源复跑，未当成额外产品失败。

根始终Cancelled=true、Completed=false、Finished0；真实NativePowerTask.IsCompleted与根完成必须区分。正常相同Host完整执行、外部OCE及先停再OCE四对照保留原原因、真实资源/RT/Host/private metadata清理和重复停止均通过，无观察到泄漏。副作用是绑定fixture handler计数及实际task完成状态，不冒称官方GameState或权威服务器被改变，也不声称正式无历史UI自发触发。定位TaskHost.Guard只桥OCE、无服务返回停止检查，Active在整个DoTasks后太晚；执行器正常结果继续DoTask/下一项。

解除条件：宿主服务正常返回后如已停止，阻止尚未分派的handler/任务，正常内部停止干净、外部OCE真实原因保留；保持Until/From真实callback、原失效分支、晚返回所有权与原克隆显式回收重试。原样五轮探针及正常链通过后再派全新独立验收。已知失败后未跑新Player/整局/1187/大型来源；旧执行者构建/整局不算本轮通过。后续无失败独立回合仍需新构建与正式自然终局/同hash/journal无新错。

本轮9个Unity均自然退出，临时五份Editor及meta已删除，没有Main/audit噪声，没有客户端/服务启动、强杀或遗留进程，已释放Unity/local/build。UI校准SHA仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。GPU本轮新33文件另存work/v003-r5-gpu/，相位SHA仍8EE736E661431E90D0E5B162B1457AA05EAE3709449D20F65EE53F5372F20A8A，已查看Mid图，非官方比较；raw历史/官方几何/机位/Shader/Trail内部精度等Unknown，整体V2 provisional、whole_effect_verified=false不变。

## F003 修复回合 r6：真实服务返回停止门（2026-10-16）

按AGENTS→PROJECT_STATE→本Task→专题→V003-r5报告与原归档检查恢复；起始master@9bcd320工作区干净，无遗留Unity/客户端，随后总控91b0ae7只提交三个编排文档。本轮不读验收聊天、不改独立报告/归档/旧交接。固定两卡ID/原配置不变。

V003-r5的实质失败先原样复现：CanDoTask停止后正常true仍调用未开始tag49；metadata handler停止后正常true仍调用tag44。旧整个DoTasks返回后的Active过晚。Task3的Guard现在调用前Active，正常返回后再次Active；所有已包装的Power服务/handler共用该门，两个IDictionary读取与Zone Entity/VisualEntities/目标Zone/loading也经过它。服务先停止再抛真正OCE仍桥接保留原异常对象，其他真实异常按原诊断传播；干净内部停止用既有专用信号。ExecutionError作为故障诊断渠道允许停止后报告，不能把它当作后续业务分派而屏蔽。未改共享NativePowerExecution/Task2。

已经运行中的外部handler内部副作用不回滚；门阻止返回后才开始的SetCompleted/后续服务或任务。Clock/Timeline停止时若任务在调用前已经完成，也不反向抹状态。真实PendingTasks仍等于未完成task数量，取消不强制归零；Host WaitingRequests、根内部metadata票据及图/RT释放另外断言。正常Until/From、原task callback和原目标失效行为不变。

新增矩阵通过实际服务包装挂点，检查初始/真实WaitingRequests延迟Until与From、CanDoTask false/true、Mulligan、Frame/Clock/Timeline、实体/Zone/集合查询、handler、取消查询、两级映射；含Cancel/Dispose、正常放行、原OCE/IOException及先停再抛。逐次保存停止当刻真实task完成状态和已发生mutation，后续不得推进或回滚；资源、实际等待、内部票据、真实原因与晚Tick/重复停止分别核对。不是反射构造私有状态或把公开Hit当原历史。

| 本轮入口 | 真实结果 |
| --- | --- |
| 原样r5复现 | PID38000 / work/f003-r6-reproduce.log，退出1，56/10；归档/临时SHA均5BB493A08F70231A839F94C77ABB32965F93702DAFCB4A08C73EC300C53028EB。 |
| 最小门首绿 | PID31052 / work/f003-r6-probe-fixed.log，退出0，66/0。 |
| 新矩阵前提校正 | PID32604/37532/5720，各work/f003-r6-services.log、services-r2.log、services-r3.log，均退出1。调用轨迹显示fixture的AllowFrom=false只阻塞tag44，metadata在Armed前已执行，未命中选定permission-from；改为明确阻塞被测查询。正常Mulligan对照按源只要求执行器请求等待，不能额外要求Until视觉暂停；保留三份失败日志，未把它们算新增产品失败或通过。 |
| 新矩阵最终 | PID7580 / work/f003-r6-services-r4.log，退出0，History1117项/239真实宿主边界案例、scheduler263/25。相比此前History527/153新增590项/86案例。 |
| 新规则输入 | tools.super_spell_scenarios与tools.verification.v003_r4_scenarios，work/f003-r6-rule-scenes.log、f003-r6-variants.log，均退出0；标准双卡双视角及4种合法变化8视角均为真实Match.apply。 |
| 最终RunFinal | PID24952 / work/f003-r6-final.log，退出0：History1117/239、scheduler263/25、Stop270/53、92根/30真实双视角、205Impact/27路径、GPU41项146帧、51Action/32Area/61Power/Presentation276与既有链。正常两卡active/pending0、max_pixels4196/230、0未绑定动作。 |
| 原样五轮最终 | PID12704/37472/15684/15944/20168，work/f003-r6-probe-rN-final.log，均退出0：34/0、44/0、44/0、43/0、66/0；每轮新Unity，源/临时SHA各自相同。r4含4种规则变化8视角；前四反例和原回收显式重试保持通过。 |
| 针对Python | tests/test_presentation_metadata.py、tests/test_native_effects.py，work/f003-r6-python-targeted.log，67passed/12.52秒，退出0。 |
| 新Windows构建 | BuildClient.Build，PID33756 / work/f003-r6-build.log，退出0，Build Finished Result: Success。业务DLL SHA256为2018422482D905D553B3D73B3B648DBA4D2BF58503AB7C46D2741760559592FA。 |

所有Unity均为2022.3.60f1c1/469b28c2de88、D3D11/RTX4080 Laptop，隐藏图形batch、无-nographics，先Handle后WaitForExit。上述13个自建Unity全自然退出，退出码按表记录；无本轮服务/客户端或强杀、无遗留相关进程。构建前删除本轮10个临时Editor/meta；仅apply_patch还原构建生成Main的1381226404/05/06为原fileID，mixer audit无差异。UI校准SHA保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6，原安装只读、未执行原DLL、未抢焦点。

新GPU相位/轨迹/PNG按RunFinal起止时间筛出33文件保存在work/f003-r6-gpu/，标准报告work/f003-r6-native-render-report.json。相位JSON SHA仍8EE736E661431E90D0E5B162B1457AA05EAE3709449D20F65EE53F5372F20A8A；直接查看两张Mid图，EX1_134内容小而暗。固定相机下各有内容相位非黑帧，原RandomWait Birth及Death/Cleanup的0像素按源分支判断，非官方视觉对比。

本修复只动Task3宿主与回归，未变原资源/Importer/配置/Python规则/网络/共享执行器。新原样反例、完整正常表现/实际规则/GPU/针对Python/新Player覆盖此次风险；未机械重复最近1187、完整来源/IL重读和后台整局，前轮日志不计本轮通过。新的独立V003在无实质失败时仍须独立新构建与正式自然终局同hash/journal核对。整体V2 provisional、whole_effect_verified=false、官方历史网络宿主/机位/几何/Shader/Trail精度Unknown不变。固定实现与可重放命令见新r6交接；只交READY_FOR_REVIEW。

## V003-r6 独立验收（2026-10-16）

PASS，固定业务3868df335ba2850ce3abf201a018741d42110f21，Task为VERIFIED，报告docs/verification/V003-r6.md；DONE由总控登记。全新空历史Agent/root/v003_r6，起始干净master@3a5ef09、唯一工作树，未读Feature/前Verifier/总控聊天推理。只写本报告/Task3当前验收与tools/verification/v003_r6_checks.cs，不修业务、不改数据库/总控三文档/旧独立材料。

原样r1-r5临时副本SHA各与归档匹配，PID32104/17100/36224/35240/31108各新Unity自然退出0，34/0、44/0、44/0、43/0、66/0；五旧反例解除。独立新r6探针PID28200/work/v003-r6-independent-1.log自然退出0：14案例116/0。使用真实TaskHost/PowerExecution和EX1_134根，补查FullEntity→EntityRevealed、GameplayPresent=false同步路径、正常handler false、Cancel/Dispose、真实延迟Until/From与外部OCE/IO（含先停再抛）。停止时已发生mutation保留，实际task flags不推进或回滚，后续业务调用0；根Completed/Finished、图RT、Host等待与private metadata分开断言，不把fixture计数冒称官方GameState。归档/副本SHA5935222E05BB9480DF772843E8F1840E92AF86049474D518F1CA96991D707B5D。

新tools.super_spell_scenarios和tools.verification.v003_r4_scenarios退出0，标准双卡双视角及fan-empty/fan-lethal/agent-no-combo/agent-friendly-lethal四合法变体8视角经正式PrepareSource通过。RunFinal PID31408/work/v003-r6-final.log自然退出0：History1117/239、scheduler263/25、Stop270/53、92根/30标准规则、205Impact/27路径、51Action/32Area/61Power/Presentation276与既有链；正常两卡active/pending0、max_pixels4196/230、0未绑定控制动作，实际图与相机RT释放通过。

标准GPU新41项146帧，报告work/v003-r6-native-render-report.json；本轮33份相位/PNG/轨迹归档work/v003-r6-gpu/并直接查看双Mid，事件与实际内容相位非黑帧一致，EX1_134的RandomWait Birth/Death/Cleanup允许0；相位SHA8EE736E661431E90D0E5B162B1457AA05EAE3709449D20F65EE53F5372F20A8A。9类型原IL经Cecil只读新重读MATCH，67公开表现/导出Python新通过/10.87秒；原来源/导出未变，6来源专项引用V003-r4独立73项基线，未机械重跑1187/4大型来源、不冒称本轮全量。

删除12份自建临时Editor/meta后新BuildClient.Build，PID21144/work/v003-r6-build.log自然退出0、Build Finished Result Success；业务DLL SHA A00CD320DA3B5A45D96E8326227952DAFF144A4E3D946189FD4E92DA23CB5D4B。正式新双客户端PID2244/33536、profile v003_r6_final_1/2（seat1/0）、端口47636、服务wrapper16524/实际35160；自然终局match98ee1d23c79849cab756adfc89bc1608，turn12/version41，seat0 LOST/seat1 WON，双方hash8e3b44371f4cad41b06683b4b026906d47cfcaf30034828760df02ec5312e53b。journal42条/0error/0concede、最后play，最终hash/version与双方一致；双方与服务无新增Exception/Error/fallback，两端各5条既有serialization深度警告。终态/性能/journal/dump归档work/v003-r6-duel/；card_render_count0只作规则队列证据，真实GPU另测，不声称本局抽到固定两卡。

全部8个本轮Editor自然退出0；自然终局验证/归档后才强清双端及实际服务，退出-1，wrapper随子退出而未拿到退出码记Unknown；无Unity/客户端/server/自建conhost或47636监听遗留。Main仅本轮生成1498556724/25/26精确还原88614366/67/68、mixer audit无差异，UI校准SHA保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。原安装只读、未执行原DLL/ComputerUse或抢焦点。提交后工作区与Unity/local/build释放；验收提交由git log -- docs/verification/V003-r6.md定位。

原正常等待/真正callback/目标失效、晚子所有权、多层清理与原图显式回收重试保持；没有以取消强归零未完成task或任意超时伪造完成。整体仍V2 provisional、whole_effect_verified=false，raw/protobuf/官方GameState/ZoneMgr网络宿主、官方机位/几何/Shader/Trail内部精度Unknown，官方逐帧视觉归Task4。独立PASS只覆盖本Task两卡自主根编排与明确宿主合同、正式规则/队列接入，前五轮FAIL保留。

## 历史 Commit
实现/专题：849f5fe3b94855f8d852b83b78fd090beb17d20f（实现：贯通刀扇刺骨根SuperSpell与正式表现宿主）。交接与完整复现命令见 docs/handoffs/F003-task-0003.md；该后续文档提交不修改实现。未amend、未push。

## 遗留问题
V003-r5服务返回停止门由F003 r6修复并经全新独立V003-r6验收通过，当前Task范围没有剩余阻止交付的实质失败；五轮FAIL历史不撤销。关键帧/Shader/官方机位与原场景几何、Trail内部网格精度待TASK-0004，整体仍V2 provisional，非整卡保真。原raw transport/protobuf/GameState/ZoneMgr网络宿主未接入；正式分支明确无原历史，显式规范化输入严格宿主另测。其他卡牌/未触及分支不属本Task，尤其未将既定EX1_134改成真正刺骨EX1_124。
