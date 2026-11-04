# V023：盗贼占槽换武器与 H10 性能独立复核

**状态：分项复核完成。** r5 固定 r8 Player 对真实双 Player 盗贼占槽换装、v19 结果同步的有界场景通过；H10 有 PNG 隔离后的双 Player 热帧证据。原场次的客户端 Presentation 状态与旧武器死亡动画逐帧可见性仍未知；H10 冷首帧与少数长帧仍存在。本报告不代表 F023 其他部分整体验收。

## 范围与来源

- 盗贼固定源码/构建：`refs/verification/f023-r8`，commit `95fe4fdd63dd4224f8b33c904c6fed9d23b5018e`；`work/f023-build-r8` Player DLL SHA-256：`11ACF337669C61729269CC66C8FF2D1BEC69D9CDD49759D6E656F4078711832D`，Unity build exit 0。r8 对 `FireblastReplay.Tick` 在非 play phase 时先处理 Rogue driver step 36，并要求 observer 与 driver 的 v19 ready marker 内容同 hash 后再收尾；这修正了 r4 提前 phase return 导致终态未采样的问题。r8 聚合 Editor 回归含 `Death.fired` 和 `Death.applied` 断言并自然通过。
- H10 静默计时使用 r6 Player DLL SHA-256：`59C4BA73163968D1AC2493031BFD0C331C0CE5B5E4FDA02EAECC8F12CCE50162`。r6/r8 输入 hash 对照中 `H10PlayerReplay.cs`、`NativeAudio.cs`、`BoardPresentation.cs` 与 `NativeAudioPlayback.cs` 相同；r8 未重新跑 H10 timing-only。
- 旧回归基线：r5 DLL SHA-256 `9BB3BDF2B7E54C1B4713C052FE0CB06EB5B2CC7273502AB22D32C22AE4AAA2E6`；其输入记录在 `work/f023-build-r5/input-hashes.json`。固定 Feature Editor 测试提交 `00fe7dd9d6547289a8d1436bf59ce2cefc740b69`。
- V023 本轮只读核对源码、JSON、日志和现有构建结果；未启动 Unity、Player 或 Importer。原始证据在 `work/`，不据分析报告覆盖原始日志。

## 盗贼：根因、修复语义与真实 Player 已复核

### 原场次与代表夹具

- **Confirmed（authority记录）：** `local/matches/74823703908b438985b949517ebee3f3.jsonl` 记录 Rogue seat 0 在命令版本17以 power entity 5 行动；服务器接受为版本18、hash `df35e5f6e01ac53ada7527f49c263f266b33c334077310532b671e1c9619d8a8`。其后版本18的 concede 被接受为版本19、hash `f1d538a7990fc4ce26216c378fbd786ebe43c96d6bd19df6ee685d2a27a0f125`。最终双方 `-state.json` 同为版本19 finished。
- 原 JSONL 记录命令和版本/hash，不含语义事件、Presentation 队列、输入锁、按钮颜色或 `ResultReady`。原局 `Destroy(74) → Death(74) → Summon(76)` 次序来自 `docs/tasks/TASK-0006-all-hero-powers.md` 所记的规则回放，不是原 JSONL 中的事件转储。
- **Observed（Editor夹具）：** `NativeRogueHeroPowerChecks.OccupiedReplacement` 造了 v17/v18 的代表快照：旧武器74 durability 1、新武器76 durability 2，Shaman场上保留73/75；并人工放入 `SpendMana → PlayHeroPower → Destroy(74) → Death(74) → Summon(76)`。它的 match id 与 hash 是合成值，不能当作原场次 v18 原样快照或 hash 重建。

### 卡死根因与最小修复

- **Confirmed（源码与旧回归）：** r5 的 `Presentation.Accept` 将 H03 同批 `Death` cue 绑定到 native power impact。`NativeEffectPlayer.SourceImpactTime` 只有 source 有 `Flow` 且存在 `FirstArrivalTime` 才返回时间；H03 使用的 source-root-only route 没有 parent `Flow`，因此该死亡 cue 永远等不到 impact。固定旧 Presentation 的回归日志 `work/f023-rogue-old-code-regression/unity.log` 在9秒后仍是 `busy=True / inputLocked=True / queue=1 / sourceImpact=null / deathImpact=True`，仍显示 v17 hash，执行入口异常退出1。
- **Confirmed（生产源码）：** `afff98c387f1c63c0f02d09789030b55b612697b` 新增窄条件：仅当 H03 同批 `Death` 对应实体在 before 是武器、after 已不再是武器时，不把该死亡 cue 绑定到 native impact。保留原 `Death` 事件/cue、常规死亡时长和播放/绘制流程；不改 `Hit`、`Destroy`、`Summon`、规则状态或 hash，也没有伪造 Arrival。
- **Confirmed（r8 aggregate Editor）：** `NativeRogueHeroPowerChecks.RunSourceRoute` 包含代表占槽 fixture；r8 Unity log 记录 `NATIVE ROGUE HERO POWER: 62 checks` 后自然 exit 0。当前断言验证 source 的 `Flow == null`、`SourceImpactTime == null`、死亡 cue 的 `nativeImpact == null` 且 `fired && applied`，并检查 Presentation 结束时 queue/input lock 清空、显示新武器/hash。它不证明真实 Player 里旧武器 `Dying(74)` 动画逐帧可见，亦未与官方画面比对。

### r5 双 Player 复核

- r1/r2 harness 均未到目标 v18。r3 在 v17 第三次 Rogue 攻击后已 settled/hash matched，但被错误的音效计数条件挡住；后续 v18 是 cleanup concede，不能算 occupied-power 测试。r4 的 v18 已实测解锁且绿色唯一 End Turn；其 v19 已被 authority 接受，但 `state.phase != "play"` 总门跳过 driver 的 step-36 终态采样，driver lifecycle 停在 v18。r4 的 observer marker `19:5fc7601d…` 证明 observer 本地达到 `RogueConcessionReady`；r4 不构成生产结果锁失败。
- **Observed（r5，`work/f023-rogue-weapons-shaman-r5`）：** r8 DLL 双 Player runner PASS；Driver 与 Observer 自然 exit 0（authority 为 runner-owned cleanup）。authority 接受 Rogue power v18，hash `99c95a929878135ba461a450c1538df7bb41da8677419e43107bae1bb054e027`；之后真实 driver concede v19，hash `5fc7601df0465edd859c956de88297deea671e97be990d3c889dd9e9335862e4`，无命令错误。新 match 与原局具有相同关键序列/实体结构，但新 match hash 不等于原场次 hash。
- **Observed（v18 Presentation）：** driver 在 `t=37.320s` 接受占槽替换，weapon 76 / durability 2；当时 queue=1、busy/inputLocked=true、display 仍是 v17 hash。`t=39.926s` 源处理后 queue=0、busy=false、inputLocked=false、sourcePresent=false，public/display hash 同步到 v18，合法 End Turn 绿色且为唯一可用动作；从接受到解锁约 2.605 秒。报告同时记录 source-only=true、parent Flow=false、source root released=true、input unlocked after source=true。该实测直接说明原来 9 秒仍锁的 v18 路径已在此固定场景中结算。
- **Observed（v19 lifecycle/UI）：** driver 于 `t=40.045s` 记录 authority accepted；最终两端均 v19、`ResultReady=true`、queue=0、busy/inputLocked=false、public/display hash 同为 `5fc7601d…`，driver/observer ready marker 内容一致。两端 `rogue-concede-final.png` 均显示主菜单，不再有一端仍停在棋盘的画面；截图是在本有界流程的结束采样，不延伸为任意客户端/原场次的菜单行为保证。scene-exit 记录 powerViews=0、nativeVoices=0。
- **截图与死亡反馈边界：** v18 settled 截图可见新 1/2 dagger、绿色 End Turn；v18 accepted 的单帧截图显示了当时 board 状态，但不能证明旧武器 Death cue 每帧的视觉呈现。r8 Editor regression 明确通过 `Death` cue 存在、`nativeImpact == null`、`fired && applied`，并检查 queue/input lock 清空及 weapon 76/hash 更新。生产最小修复仅对同批被替换武器 Death 取消 native impact 绑定；保留 cue、常规 duration/audio/apply 路径，不泛改 Mage/Hunter。Player 层的真实解锁证明不依赖 `.2s` 逻辑门；该数字也不作为帧流畅度指标。
- **Unknown（原场次）：** `74823703908b438985b949517ebee3f3` 的 JSONL/state 只记命令、版本、hash 和终态，不记录 Presentation 事件队列、死亡 cue fired/applied、输入锁或 `ResultReady`。相同规则事件次序 `SpendMana → PlayHeroPower → Destroy(74) → Death(74) → Summon(76)` 有 TASK-0006 的规则回放依据；不能把它写成原 JSONL 中的事件转储，也不能声称 r5 复建了原 match hash 或原客户端的逐帧死亡表现。

## H10：静默计时证据与旧采集模式分开

### r6 PNG-free timing-only 双 Player

证据目录：`work/f023-dh-quiet-r1`。`run.json` 为 `PASS bounded H10 dual Player 1920x1080 timing-only`；DLL hash 与 r6 构建一致，driver/observer exit 0，authority cast 与自然结束回合 hash 两端一致。两端 report 均有 `timingOnly=true`，角色目录没有 PNG，`images=[]`，截图请求记录为 `suppressedCaptures`。源/输入/DSP报告完整，独立 runtime 为全量副本，测试索引启用 H10，production index SHA 在运行前后不变，清理已确认。最终 `analysis.json` 为 `PASS H10 dual Player timing-only 1920x1080`。总控记录首次分析器调用走错 PNG 模式而失败；后续 timing 专用分析通过，Player 原始报告未重跑或改写。

下列帧值来自每个 Player 的 `source.frameTimings`，是 Stopwatch 测得的 Update-to-Update wall intervals。`steady` 排除单独列出的第一个采样；这是双 Player 静默计时运行的主线程帧间隔，不是 GPU-only 渲染耗时。

| 角色 | 样本数 | 首个间隔 | steady p50 | steady p95 | steady max | steady ≥16.67ms / ≥33.33ms |
|---|---:|---:|---:|---:|---:|---:|
| Driver | 440 | 262.97ms | 4.238ms | 5.680ms | 51.441ms | 3 / 2 |
| Observer | 447 | 262.94ms | 4.175ms | 4.930ms | 82.786ms | 3 / 2 |

单独记录的 source stage：

| 角色 | cold create+seek p50 | hot seek p50 / p95 | draw total p50 / p95 |
|---|---:|---:|---:|
| Driver | 226.027ms | 0.114 / 0.268ms | 0.446 / 1.262ms |
| Observer | 230.244ms | 0.128 / 0.225ms | 0.456 / 1.011ms |

**Observed：** 两端大多数 steady Update 间隔约4.2ms，H10 hot-seek 和 draw 的中位耗时很低；和 r1 的热 seek 约34ms 中位及百毫秒级常见 Update 间隔相比，有明确的实际热路径改善证据。首个采样约263ms、cold create+seek 约226–230ms 仍是明显冷开销。steady 最大值仍有51ms和83ms，且源码 analyzer 不设性能阈值；因此证据支持“热帧显著改善”，不支持“没有卡顿/整体流畅”。

### 旧版逐帧 PNG 工件

| 构建/模式 | Driver PNG | Observer PNG | 首个间隔 | 后续间隔 p50 / p95 / max | 后续 ≥80ms |
|---|---:|---:|---:|---:|---:|
| r1，DLL `8209E1BE…` | 14 | 10 | 544 / 610ms | 112 / 218 / 218ms（D）；108 / 230 / 230ms（O） | 14/15；14/16 |
| r2，DLL `9BB3BDF2…` | 14 | 10 | 239 / 288ms | 4.31 / 6.03 / 106ms（D）；4.16 / 5.30 / 97ms（O） | 7；6 |

r1 同时观测到 cold prepare 343/385ms、hot prepare 中位34.381/33.947ms；r2 cold prepare 189/245ms、hot prepare 中位0.118/0.122ms。r2 热路径计时已明显改善，但 PNG 模式仍有与逐帧截图捕获强相关的高间隔；这些 PNG-bound outlier 不能作为正常生产帧耗时。r1/r2 与 quiet run 为不同构建和模式，表中用来区分量级和采集背压，不是同 DLL 的严格 A/B。

**Inference：** quiet run 的热帧数据与 r2 的常态区间一致，支持 H10 业务热路径已改善；仍有少数未解释长间隔，且 cold create 明显较慢。性能应继续按冷首帧、PNG 采集背压与 production-like 热帧分开解释。0.2秒输入门仅验证动作提交边界，不用于 FPS 或流畅度结论。

## 当前结论

- Rogue：**r5 固定 r8 的有界真实双 Player 流程通过**，覆盖 v18 occupied replacement settled/unlocked/green-End-Turn、v19 accepted、双端 ResultReady/hash sync 与 scene-exit cleanup。旧回归基线 9 秒仍锁；新流程 v18 约 2.605 秒后解锁。原场次客户端队列和逐帧 Death 画面仍未知，不把重演 hash 当作原场次 hash。
- H10：**r6 quiet 双 Player 热路径改善已测得**，r8 的 H10 相关代码输入 hash 相同；冷首帧和少量 outlier 保留，不宣称整体无卡顿。
- F023 整体：**本报告不标整体验收 DONE**；其结论只覆盖 Rogue 与 H10 性能子项。
