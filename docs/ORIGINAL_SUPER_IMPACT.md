# 原 SuperSpell 命中分派与宿主契约

TASK-0002 的局部端点与独立验收记录。后续 TASK-0003 已实现两卡根控制器、区域/pending 聚合与显式原规范化历史宿主，等待独立 V003，见 [ORIGINAL_SUPER_SPELL](ORIGINAL_SUPER_SPELL.md)。正式本项目语义事件路径没有原历史传输，不能称官方 metadata 已接入。下文未实现描述保留 TASK-0002 当时范围。未执行原 DLL，未使用 Computer Use，原安装只读。

## 来源与行为对照

Confirmed：本机 Cecil 重读 `Assembly-CSharp.dll`，SHA256 `3F67779510122DBCE79EC431E4E7494643083160EC9E7592F6ECB899B2B69728`。源清单 `manifests/original-script-audit.json`；IL 缓存 `local/script-audit/Assembly-CSharp.dll.<type>.il.txt`。客户端产品版本 Unknown。`tools/export_script_il.ps1` 只读取元数据/IL，未加载执行官方程序集。

| 来源 | 原行为与实现边界 |
| --- | --- |
| `HasImpact`、`SpawnAllImpacts`、`SpawnImpact` | enabled 且默认 Prefab 非空或 ranges 非空表示可用；SpawnAll 只对 IsValidSpellTarget 为真的索引调度。Spawn 在启动协程之前 Pending++；等待函数为私有，不能绕过计数。直接 Spawn 只验证索引边界，不补做原本没有的有效性筛选。 |
| `<WaitAndSpawnImpact>d__89` IL_002d–005d | 每次先采样 spawn delay，再无条件 yield scaled WaitForSeconds，0 秒仍有帧边界。 |
| d__89 IL_0065–00a6 | 仅 spawn_offset>0 且 index>0 时，另 yield offset*index；与首段等待分开。 |
| d__89 IL_00ae–0130 | 等待后查 visualIndex→targetIndex→metadataIndex。metadata>=0 时先查询 ShouldCompleteUntil；为真则 StartCoroutine(Until) 并 yield 宿主 handle；随后才采样 game delay，StartCoroutine(From)，不 yield From。无映射时不采样 game delay。同步 Until 返回 null 时仍 yield 一次。 |
| d__89 IL_0131–016c | metadata 分派之后才检查目标列表缩短或 Unity 对象销毁；失效仅 Pending-- 并返回，不调用 FinishIfPossible，不创建替身。恢复时不重新调用 IsValidSpellTarget。 |
| `DetermineImpactPrefab`、`SpellUtils.GetAppropriateElementAccordingToRanges` | 无 ranges 直接默认 Prefab，不调用 DamageInfo；无 task list/Card/DamageInfo 回落默认。范围闭区间，重叠取首个；大于所有 max 取首个最大 max，小于所有 min 取首个最小 min；中间空隙或选中 null Prefab 回落默认。并不排序、合并或补洞。 |
| `CloneSpell`、d__89 IL_016d–0253 | GetSource→选 Prefab→独立克隆→订阅 state callback→挂 root 并登记 active→订阅 finished→SetSource→AddTarget。UseSuperLocation 时设世界位置并跳过 location/rotation；否则总是覆盖 Location/Parent（包括 Location=0），UpdatePosition，再在 AdjustRotation 且非零时按 Transform.Rotate 本地欧拉增量。最后 UpdateOrientation→Activate。原 IsMakingClones 恒 true。 |
| `SpellManager.GetSpell(Spell)`、`Spell.Awake` | 前者直接 Instantiate；后者建立状态图，不发送 None。两张样板的根 FSM start 均为空动作状态，首次可用生命周期为 Death。命中工厂启用 `startAtFsmEntry`，保留初始 ActiveState=None 和原对象 active，避免初始化时主动运行 None 的根关闭动作。既有 area/missile 默认入口未切换。 |
| `OnCloneSpellFinished`、`OnCloneSpellStateStarted`、`SpellManager.ReleaseSpell` | finished 负责 Pending-- 和 FinishIfPossible；进入 None 独立移出 active 并 ReleaseSpell(false)，其返回值被调用者忽略。原 Release(false) 不调用 Spell.ReleaseSpell，仍销毁实例。NativeImpactFactory 排队释放，宿主在采样/回调栈退出后 FlushReleased，销毁独立图、相机、RT、临时材质/动画。 |

## metadata 的严格边界

`NativeSuperImpactServices` 不默认创建 metadata，不把 SemanticEvent 转成原 PowerTaskList。`MetaDataIndex` 必须显式绑定；`-1` 仅表示宿主确认该 visual target 没有映射，不能作为“没有实现服务”的默认值。有映射却缺少 Until/From/ShouldCompleteUntil 时记录故障并停止该请求。

- `ShouldCompleteUntil(index)` 必须依据真实 task list、batched metadata 和 earlier incomplete task 状态。原 `IsBatchedTargetInfo` 的 IL 有异常的 count/index 分支；本任务未擅自修正或实现该原历史宿主。
- `CompleteUntil(index)` 对应 d__126：宿主独立 pending++，执行 DoTasks(0,index)，收集需等待的区域任务，必要时等待 d__128 的任务完成/Actor loading/Zone 完成，之后 pending--。不能把“调用过 DoTasks”当成这些依赖已经完成。
- `CompleteFrom(index,delay)` 对应 d__129：宿主独立 pending++，scaled WaitForSeconds(delay)（包括 0），再 CompleteMetaDataTasks；真正完成回调 `OnMetaDataTasksComplete` 才 pending-- 并 FinishIfPossible。协程返回和任务回调完成是不同事件。
- `NativeSuperImpact.Pending` **仅计命中视觉请求**。根宿主须聚合自己的 metadata/其他子效果 pending，且检查 Faulted/Cancelled；不得看到本端点 Pending=0 就宣布根法术成功。Server 不等待这些表现服务。
- 有 ranges 时必须绑定 `Damage(target)`；返回 null 表示真实查询没有 DamageInfo，缺失委托则拒绝，二者不混用。两张样板原 ranges 均为空，导出工具只承诺这两项，不猜测其他卡的范围结构。

## 调度、失败与释放责任

`StartCoroutine` 必须立即推进所给 IEnumerator 至首次 yield，并返回可被 yield 等待的宿主 handle；仅同步完成时允许 null。Unity 可直接绑定 MonoBehaviour.StartCoroutine；检查器使用明确的 scaled 时间/帧调度器，未声称验证 Unity 的完整 PlayerLoop。根宿主应在销毁/取消时调用控制器 Dispose 或 CancelAll；不能依赖 Unity StopAllCoroutines 自动提供完整清理。

Until/From 的枚举器由端点保护，嵌套 IEnumerator 一并推进、关闭并捕获异常；不允许在委托内部启动未交给端点管理的隐式 fire-and-forget 工作。From 的任务派发之后，异步 GameState/ZoneMgr 回调、共享 pending 和取消责任仍在 metadata 宿主；本端点不能撤销已派发的权威任务。失效/取消标志会阻止随后恢复的受管枚举器继续副作用；已经挂起的 Unity wait handle 可等到下次调度才自然退出。

自主保护（不是原版额外事实）：构造/调用参数和必要服务检查；有限采样值检查；每请求计数票据；finished/None/取消幂等；异常/取消不触发虚假成功；Error(targetIndex,stage,exception)、Faulted/Cancelled/LastError 可诊断。释放异常保留 ActiveClones，以 CancelAll 显式重试。正常 finished 后仍保留活动克隆至 None，不能为 pending 归零提前销毁。服务按客户端主线程调用，配置在端点生命周期内不得改写。

同聊天补查确认并补齐了同步重入保护：Dispose 先关闭调度入口再取消；诊断委托抛异常只增加 DiagnosticFailures/LastDiagnosticError 并输出警告，不覆盖该次原始 LastError，也不中断其他请求清理。回收期间的再次 CancelAll 不重复进入同一 Release。每次外部 Source/metadata/定位/克隆服务返回后检查停止标志；若 GetSpell 在取消期间返回已分配克隆，立即转交 Release(false)，不再设置目标或 Activate。取消之后仍出现的回收/枚举器异常继续经 Error/LastError 上报，保留未释放克隆供显式重试。它们是自主宿主约束，不升级为原 IL 的额外行为。

工厂从 AssetRegistry.Root 读取 `native-super-impact.json` 和独立图，校验 reference，按原 Target location/facing 绑定实际对象。缺少定位/朝向服务明确失败。宿主必须在所有 Seek/Render/Activate 回调返回后调用 FlushReleased；Dispose 是最后的资源兜底。工厂与流程同生共死，先停流程再 Dispose 工厂。

## 两个真实样板与证据

`python -m tools.super_impact_source` 重读两个根组件全类型树，并通过 CAB/PathID/GUID 解析原引用；独立导出 Prefab、FSM、音源依赖，不覆盖原根图或 UI 校准。来源为 `manifests/original-super-impact.json`。

| 卡 | 原命中 Prefab | spawn delay / game delay | 配置 |
| --- | --- | --- | --- |
| EX1_129 刀扇 | ReuseFX_Sneaky_Attack_Impact_FanOfKnives / 7bdf273a76b122d41b3c9fb262a6f4a9 | [0,.5] / [.4,.4] | Location=6，非 parent，非 super location，无旋转，无 damage ranges |
| EX1_134 刺骨 | ReuseFX_Sneaky_Attack_Impact_01 / ad6a1891058a81d499ce5b6f7c330477 | [0,0] / [.5,.5] | 同上 |

局部 GPU 入口 `NativeSuperImpactChecks.Run`，图像及生命周期 JSON 位于 `tests/visual/current/native-super-impact/`。使用现有 512×512 隔离正交相机、白 ambient、固定最小 delay、固定采样时刻 .01/.1/.3/.6/1/2/3/5，显式 Target/facing 服务；没有 metadata 映射。不是官方背景或正式比赛画面，原 Shader/音频/根时序没有因此获得整卡认证。自动化详细结果在 Task 中登记。

最终 `work/task-0002-final-impact-resources.log` 154 项 / 退出 0。两卡分别最大可见像素 168/140，完成回调各一次，pending=0；None 后 FlushReleased 直接验证克隆 GameObject、隔离根/相机、输出 RenderTexture、原图临时材质为已销毁对象。多目标实际克隆的取消也核对每个实例被销毁。资产 Registry 共享缓存仍由 Registry 持有，不归单个命中克隆销毁。

补查最终 `work/task-0002-supplemental-final-gpu.log` 172 项及既有表现链、标准 GPU 41 项/146 帧通过，退出 0。新增七类宿主边界先在修复前日志复现失败，再转绿；实际双卡工厂额外验证 Dispose 中晚返回的克隆没有接受 Source/Target/Activate，且隔离根/相机/RT 被释放。故意抛错的诊断观察者会产生两条预期警告，并有独立计数。这是同一实现聊天补查，仍待 V002 独立复验。

V002-r1 固定 `6838ddf` 的独立验收仍发现例外：首次取消回收失败后，晚到 None 的第二次回收错误被 Fail 的 stopped 早退吞掉；详见未改写的 [失败报告](verification/V002-r1.md)。F002B 在新进程原样复现，再以实现 `752f909` 将 stopped 分支改为只报告新错误后返回，不重复结算、计数或回收。Error/LastError 是每次新异常的诊断通道，Faulted/Cancelled 是请求首次停止的计数，两者责任分开。

F002B 原独立探针修复前退出 1、修复后退出 0（`work/f002b-late-none-before.log`、`work/f002b-late-none-after.log`）：errors 2→3、LastError 为第二次回收错误，保留克隆后第三次显式重试成功。新增 cancel/dispose/fault 三类停止序列均先红后绿，并覆盖回收期间重入 CancelAll/None、晚到诊断观察者抛错以及成功重试后的重复回调。`NativeSuperImpactChecks.Run` 为 205 项 / 退出 0（`work/f002b-impact-lifecycle.log`），双卡可见性、finished/None、真实 GPU 资源销毁通过；Windows 构建及 24 项规则/表现针对性测试退出 0。原来源、标准 GPU 全套、正式根及 metadata 宿主范围未扩展；当前仅 READY_FOR_REVIEW，等待全新独立 V002，不提升数据库视觉/独立验收等级。

失败证据保留：首轮检查脚本编译声明错误；第二轮缺 Facing=6 服务；第三/四轮黑帧，最终确认初始 None FSM 关闭原根对象。调整相机隔离位置不是解决生命周期问题的依据；依据为上述 Spell.Awake/Instantiate/初始空 FSM 与重新运行的可见性/完成/释放证据。未拉长原时间、删减 None 动作或添加替代画面。
