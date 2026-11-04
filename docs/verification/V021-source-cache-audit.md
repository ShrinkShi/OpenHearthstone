# V021 来源快照与运行缓存失败分类

2026-10-24，TASK-0006，独立只读复核。此记录只分类当前失败证据，不刷新来源清单，不宣称整套任务验收通过。

## 定向验证

对总控给出的 12 个失败节点运行限定 pytest（`-q --tb=short`），结果为 **11 failed, 1 passed, 74.43s**。唯一通过项是 `test_paladin_formal_replay_wires_source_area_only_evidence_and_untargeted_input`；当前测试与 `NativeEffectPlayer.cs:47` 的新 `SourceBusy` 控制流一致。UnityPy 对原始 bundle 给出 `UnityVersionFallbackWarning`，fallback 为 `6000.3.11f1`。未运行全量 pytest、Unity、Build 或导出命令，未修改业务代码、测试或清单。

## 缓存范围扩大：应补覆盖，不是渲染回归

- H11 新增 `local/runtime/40ac8d77701fdbccc4033483.native-effect.json`，来源为 `ReuseFX_Necrotic_DeathKnight_OverrideSpawn_HeroPower_Super.prefab:d71d2c073842bf24a9a24f9cabada171`。
- 对比现有清单与只读缓存扫描，`native-commonfx-none` 引用由 55 增至 59，4 条增量都在该 H11 文件；`native-transparent` 由 314 增至 317，3 条增量也都在该文件。
- Additive `audit()` 的差异只在 references：147→148 行、404→405 个材质引用；唯一新增行为同一 H11 文件中的 1 个引用，其余字段、已有引用完全相同。
- `original-effect-transform-source.json` 记录 254 个文件，而当前缓存有 255 个；集合差仅为该 H11 文件。三个 super area/missile/start 配置清单各有 232 条，当前缓存中带对应 root 字段的文件各为 233 个；唯一多出的也都是 H11 文件。当前 actor-spell 缓存及 transform 清单都是 205 个，数量相符。
- `native-effect-index.json` 当前 active 集合为 `CS2_025`、`CS2_062` 及新增 `HERO_02bp`、`HERO_05bp`、`HERO_07bp`、`HERO_10bp`、`HERO_11bp`。后五项指向 `.hero-power-source.json`，没有旧 fingerprint 测试要求的 `program_sha256` 字段；测试应按 hero-power source manifest 的来源哈希验证，不能只扩充旧集合断言。

以上是清单覆盖范围变化。把期望数改大只能记录新缓存范围，不能替代来源和画面验证。

## 不能作为“旧测试可忽略”的来源差异

- `test_commonfx_none_actual_shader_dependencies` 在现有清单引用的同名 Shader 身份 `CAB-c394926ba464822396a05cbfae08a8b3/-402334618889270515` 处失败：当前原始 Shader 编译 stages/constants 与保存的 fingerprint 不同。该失败不是 H11 四条新增材质导致的计数差异，也不是测试只漏列一个文件。当前证据没有重新采纳新 fingerprint；应先确认本机原版资源安装/版本，再决定是否重采并更新来源记录。
- `test_original_spell_transform_catalog_matches_all_actual_sources` 的首个差异在 `DeathKnight_FX` 源记录：`m_ImpactInfo.m_Prefab.m_FileID` 当前为 87、缓存与 manifest 为 83；两边 `m_PathID` 精确相同（`-7347481063036194986`）。该路径中的 `m_GameObject.m_PathID=5995246279055483552` 也用 Python 任意精度整数读取，未发生浮点取整。当前 external table 的 87 指向 `CAB-8d240f331c9acacfd73bb27e66b63655`，缓存记录的 83 对应 `CAB-5f914e1fdd543d01e2b853d8407f04e5`。这不是可直接忽略的数字格式差异；它表明保存的 raw 快照与当前源 external table/目标不一致。
- area、missile、start 三个测试首个失败都落在同一个 `RLKFX_NoxiousCadaver_Missile_Super` root：来源为 `essential_base_global-prefab-0.unity3d`、`CAB-69c88b234dfa427e02d1fcee9337ae34`、pathID `2306146241369383063`。`m_FriendlyAreaEffectInfo.m_Prefab` 当前是 FileID 11、缓存值是 13，双方 pathID 都是 `-203738824554055140`。当前 FileID 11 指向 `CAB-93ce91e5953dced408f61269ec90fec3` 并能解析到该 pathID；缓存的 13 在当前 external table 指向 `CAB-c394926ba464822396a05cbfae08a8b3`，不能解析该对象。三项是一个根 raw 来源不一致，不是三个独立代码回归；也不能把它归一化成“同一 CAB 的 FileID 重排”。
- `test_two_root_branches_match_readonly_original_components` 中新 `recover()` 对原 `m_ChainInfo` 记录了四个事实：`chain_enabled=true`、`chain_prefab_null=true`、空引用、空 source。因此 `chain_available=false` 仍成立，但旧 catalog 未包含这四项。应补充真实来源字段，不应删除这些字段或把它们当作缓存覆盖扩展。

这几项证明当前 source-vs-cache 原始数据存在差异，但仅凭本轮读取不能断定差异发生在哪次安装更新、导出或 external-table 变更中。不要盲目刷新快照：先固定安装清单/来源 bundle 哈希，再复核 PPtr 的解析目标及 Shader 编译 fingerprint；若目标 CAB 已变化，需重导相关程序与派生配置，并同步保留来源证据。

## Player 双端产物复核（2026-10-24）

只读检查 `work/f021-{shaman,druid,warrior,rogue-weapons,warlock,hunter}-r1-1920`。六份 `run.json` 都记录同一 DLL SHA-256：`cb32b0c4df912d99055cfafc8dc23170a2c0491c4400deaf8859804063ad6aac`；每份均为 1920×1080，driver/observer 退出码为 0，并记录有界 PASS。server 的退出码 1 标为 `owned authority cleanup`。所有六对 driver/observer `publicHash` 相同；Druid、Warrior、Rogue、Warlock 的报告也有相同的 display hash。Shaman 通用报告的 display hash 为空，但 `driver/shaman-evidence.json` 中 display 与 authority hash 相同。Hunter 两端 display hash 都为空，不能把它写成显示状态 hash 已核验。五份有 `evidence-index.json` 的工件（Druid、Warrior、Rogue、Warlock 各 9 张，Hunter 20 张）逐张重算 SHA-256 均匹配索引；Shaman 工件没有该索引文件。

输入解锁以各职业细分证据为准；通用 `reports[]` 在 Shaman、Druid、Warrior、Rogue 仍写 `inputUnlockSampleFrame=-1`、`inputUnlockedAfterSource=false`，这些字段不能覆盖专用回放记录：

| 职业 | 可复核的解锁证据 | 边界 |
| --- | --- | --- |
| Shaman | `driver/shaman-evidence.json` 记录 source 期间约 1.159s 解锁；display/public hash 相同。 | 这是 source 执行期间解锁，不证明 source 完成后才解锁。 |
| Druid | 专用报告记录 source root completed/released 后解锁，sample frame 2846；规则状态与 display hash 匹配。 | Observer 不作为 active seat 的输入解锁证据。 |
| Warrior | 专用报告记录 source root released 后解锁，frame 3169；护甲由 0 到 2、mana 由 10 到 8，状态 hash 匹配。 | 当前截图只有 ready/hover/cast/used 阶段，没有真实峰值帧。 |
| Rogue | 专用报告记录英雄技能 source 后 frame 3085 解锁；两次武器攻击各自到达结算，第二次 frame 4732 后可继续输入，display/public hash 匹配。 | 现有截图是 ready/hover/cast/used 阶段；没有武器特效真实峰值图。 |
| Warlock | `driver/warlock-evidence.json` 记录 source root completed/released 后 frame 3504 解锁；扣除 2 mana/2 health、抽 1 张牌，display/public hash 匹配。 | 仅限本次记录的规则和时序路径。 |
| Hunter | Timeline 中 frame 2663 出现 `inputLocked=false`，但 source 仍未标记 completed/released，且 `hashMatched=false`；最终通用记录没有 unlock sample，display hash 为空。 | 当前产物不足以证明 source 完成后的输入解锁与显示态一致。 |

音频记录中能看到 Unity 内部 `source play` / `playing=true`、`pending=false`、`sourcePending=0`，且 scene-mix DSP 回调有采样；这证明引擎侧音源启动状态和混音回调路径被触发，不证明扬声器或系统设备实际可听，因为没有 WASAPI/设备 loopback。scene-mix 的采样中 Shaman 有 30 个 clipped samples，Rogue 有 82 个，Hunter 有 189 个，峰值分别约 1.09、1.35、1.42；Druid、Warrior、Warlock 没有 clipping。Warrior 的音效时序记录同一 native-effect 在约 13.665s/13.686s 启动两次，其后 power-ui 在约 13.688s 启动；报告中的音效设计字段分别记有 parent 与 impact clone 各一个 action，因此本次证据能确认两次启动，不能单凭它判定是否偏离原版设计。Rogue 槽位 7/5 的 `actualStarted=True` 记录在约 18.276s、22.218s、22.626s。以上仍是回放进程内部时间和状态，不是设备可听证明。

Hunter 的 trail capture summary 标为 complete；impact-particle summary 缺 450ms 样本，记录原因是首个 source Repaint 落在 target tolerance window 之后。Warrior/Rogue 当前工件也没有特效峰值截图。因此这些职业的阶段截图不能替代 Warrior/武器峰值帧复核；按当前回放计划补齐图像后再更新该项。以上不改变六次 run 的有限 PASS 记录，也不构成整项 TASK-0006 的完成结论。

### 2026-10-25 补充事实更正

- H05 Hunter 命中后的爆炸声来源现已由原生 sound 定义和 Listener PCM 相关性共同核实：`local/runtime/866ce32403a1e84d9b7cced8.native-sound.json` 的 clip 是 `Arrow_Targeted_Explode_03.wav`，导出音频为 `84debef801d83ef877fed63e.wav`；`d47fe21568b31585dca77e.wav` 对应 `Arrow_Targeted_Build_02.wav`，不是爆炸声。`work/f021-hunter-r1-1920/driver/hunter-audio-correlation.json` 报告 Hit 后 32.326ms、correlation 0.75346、projection gain 0.70861，且 Listener PCM 中检测到信号。它证明爆炸采样进入 Unity Listener PCM；采集锚点是相邻 hover-enter 时间，且没有设备 loopback，不能外推系统输出或可听性。
- H10 r4 曾有双端 PASS；更新产物见下方 r5。根侧另报告 build-r6 集成检查通过 113 项、build-r7 成功，本次未重跑 Unity/Build 检查。可见的旧 `work/f021-h10-instance-r6.log`（2026-10-24 23:58）在 `NativeDemonHunterHeroPowerChecks.RunSourceRoute` 的 GPU framebuffer 粒子断言失败并退出 1；它早于后续 Player 产物，不能据此否定后续回放，也不能冒充 build-r6/r7 的最终检查日志。

### 2026-10-25 双端与回放补充

- H10 `work/f021-demon-hunter-r5-1920` 的 `run.json` 与 `analysis.json` 均为 PASS，1920×1080。driver/observer 共用 runtime index SHA-256 `86AA1B3774C0B10A72D73868D5382944C1774E7E4B9FC2DF845D7443F99BB33D`；source 分别运行 2.037s、2.009s 后释放，activeClones=0、unhandledActions=0，结束回合 hash 同步。两端输入样本均为 sourceReleased、busy=false、inputLocked=false、pending=false、displayMatched=true；driver 还有 legalEndTurn=true。该记录只支持本次 source 生命周期、输入恢复及权威状态同步。
- Priest `work/f021-priest-r3-1280` 的双端 run PASS，DLL SHA-256 `8b855ce6babbaf1c600612eec089ddadfe73f1c8064183ebb2e926900e6eae5e`；两端 public/display hash 同为 `883320daa41d961d4d88ff8687042250d1afdf00932f3ea9325497f9386a283b`。Driver 在 frame 4888 解锁，目标英雄生命由 28 增至 30，记录一个治疗事件。陈旧输入证据来自真实 `ServerActionRejected` 路径：`matches/fcc3555775774c4d88d22fd75d394133.jsonl` 中 version 5 请求在当前 version 6 被拒绝为 `stale state version`，随后 version 6 请求被接受并推进到 version 7/hash `883320...`。这比读取瞬时状态提示更强，但只说明本次重放路径。
- Rogue weapons、Warrior、Warlock、Hunter 的 r2 双端 `run.json` 均为 PASS，driver/observer 退出码 0，server 以 `owned authority cleanup` 退出码 1；四份使用同一 DLL SHA-256 `8b855ce6babbaf1c600612eec089ddadfe73f1c8064183ebb2e926900e6eae5e`。Rogue 两端 public/display hash 同为 `28f24f5cf175c9c4d123f5247f222221f15111428b7da2e54e4f75b0073251b2`；第二次攻击在 frame 4805/version 11 命中实体 37，生命 29→28、武器实体归零，攻击记录 hashMatched/inputUnlocked/legalEndTurn 均为 true。该帧 timeline 同时含普通 weapon-target 71/hashMatched=false 样本和攻击解锁 target 37/hashMatched=true 样本。`analyze_rogue_weapon_report` 现在按解锁帧、第二次攻击 version 和 targetEntity 精确选唯一记录，且保留 inputLocked/busy/hash/health/legalEndTurn 断言；回归夹具覆盖错误目标与错误版本的 decoy，并验证这些状态条件不可放松。定向测试 `tests/test_rogue_player_replay_contract.py` 为 9 passed，r2 `--analyze --rogue-weapons` 成功生成 evidence index。
- Warrior r2 driver hash 匹配，source release 后 frame 3146 解锁；护甲 0→2、mana 10→8，legalEndTurn=true，DSP 211 callbacks、0 overflow、0 clipped samples。Warlock r2 driver hash 匹配，source 完成并释放后 frame 3494 解锁；mana 10→8、生命 29→27、手牌 7→8、牌库 24→23，source audio action/play 各 1、unhandled/unbound actions 均 0；DSP 215 callbacks、0 overflow、0 clipped samples。两者证据支持报告的规则状态与引擎回调路径，不证明扬声器可听。
- Hunter r2 双端 public hash 同为 `35d6285a2fd2bbeecc77d8d76493ae751984b735c151cbb849971bfd24baedee`，driver 的 displayHash 仍为空，故没有把它记为 live display hash 已核验。`hunter-impact-sample-summary.json` 对 250/450/650ms 三个目标粒子年龄均标记捕获完成；run PASS 只证明这次有界输入/渲染采样路径。检查时尚无该目录的 `analysis.json`，也没有据此声称完整 Hunter analyzer 验收。其 DSP 有 211 callbacks、0 overflow、115 clipped samples；回调仍不能证明系统设备实际可听。
- 各 Player 的 source/DSP 报告是 Unity 内部生命周期和 scene-mix 证据，不证明扬声器或系统输出。H10 r5 截图与源静态 mesh 比例支持这轮绿拱几何放置重建；没有官方同帧对照，不能称逐帧等价。

### 2026-10-25 r11 冻结构建与 Warrior r3

- 根侧提供的 r11 Player DLL SHA-256 为 `9237bca2cdf55a4ab2b22619d717234d1ccd5d1b879d570c938773a701922aca`；`work/f021-all-build-r11.log` 记录 `Build Finished, Result: Success`。实际 Warrior r3 `run.json` 也记录同一 DLL hash，driver 与 observer 进程均退出码 0；server 退出码 1 的终止原因为 `owned authority cleanup`。
- `work/f021-warrior-r3-1920/run.json` 中 driver 结果为 `PASS bounded formal Warrior armor GUI/input/source/DSP; official fidelity unverified`（25 checks），observer 结果为 `PASS bounded formal HERO_01bp observer GUI/authority state; official fidelity unverified`（4 checks）。两端 1920×1080，public/display hash 均为 `9682c33ef08eb5d92c45cf688e8a88f9b55a8c1949ad5e891f8ceb499f9f7856`。driver 记录 HERO_01bp source completed/released 后 frame 3131 解锁；护甲 0→2、mana 10→8，hero health 29 不变、powerUsed=true、legalEndTurn=true。source cue 1、unexpected hit cue 0、unhandled/unbound actions 0。observer 是 seat 1 的观察端，不以其 `inputUnlockSampleFrame=-1` 推翻 driver 的 active-seat 解锁记录。
- driver scene-mix 报告 211 DSP callbacks、0 overflow、0 clipped samples，source audio play count 为 2；这仅证明该回放的 Unity 内部 source 与 scene-mix 路径，不证明系统设备 loopback 或实际可听。`evidence-index.json` 明确 `official_fidelity_verified=false`；其 11 张图片重算 SHA-256 全部匹配索引。截图包含 ready/hover/cast/used 及标为 `reference` 的捕获，不能单独证明原版动态事件逐帧等价。
- `work/f021-rogue-weapons-r3-1920/run.json` 随后实际落盘并记录与 Warrior 相同的 DLL SHA-256。driver 为 53 checks 的 bounded PASS，observer 为 5 checks 的 bounded PASS；driver/observer 退出码 0，server 退出码 1 且标记为 `owned authority cleanup`。driver/observer 的 public 与 display hash 都为 `28f24f5cf175c9c4d123f5247f222221f15111428b7da2e54e4f75b0073251b2`。`evidence-index.json` 标记 `official_fidelity_verified=false`；10 张截图 hash 均与索引相符。
- Rogue driver 记录 HERO_03bp source root released 后 frame 3172 解锁；之后两次武器攻击都以 target entity 37、hashMatched/inputUnlocked/legalEndTurn=true 结算。第一次 frame 3806/version 8，英雄 30→29，weapon entity 71 留下 1 attack/1 durability；第二次 frame 4808/version 11，英雄 29→28，weapon entity 清零并产生 1 个 weapon-death event。最终 `rogueWeaponLifecycleComplete=true`，第二次攻击后输入恢复，driver public/display hash 与 observer 对齐。observer 的攻击数为 0，因此只用于权威状态/显示态对照，不作为 active-seat 攻击回放证据。
- Rogue 音频记录包含直接施放 slot 41，以及攻击阶段 slot 7、slot 5、slot 7；scene-mix 有 483 DSP callbacks、0 overflow、115 clipped samples，报告 peak 为 1.289。索引中的四个原始 source PCM clips 各自记录 0 clipped samples，但混合回放的 clipping 仍需保留为本地限制；这些都是 Unity 内部状态/混音数据，不证明设备 loopback 或扬声器可听。
- `work/f021-priest-r4-1280/run.json` 随后记录 bounded PASS：driver 39 checks、observer 3 checks、退出码均为 0，DLL hash 与 r11 一致；双方 public/display hash 为 `883320daa41d961d4d88ff8687042250d1afdf00932f3ea9325497f9386a283b`。driver frame 4993 解锁，目标英雄 28→30，heal event/callback 各 1。`matches/583740d236c241289a5e7f599ace48c3.jsonl` 先记录 version 5 的 power 请求在 version 6 被 server 以 `stale state version` 拒绝；随后 version 6 请求成功推进到 version 7 与该 hash。Driver scene-mix 596 callbacks、0 overflow、32 clipped samples；evidence index 的 13 张图 hash 均匹配，且明示 `official_fidelity_verified=false`。此项只证明本地该次 action/rejection、回放状态与 Unity 混音路径，不证明官方逐帧或硬件输出。
- `work/f021-mage-r1-1920/run.json` 记录 r11 DLL，但 driver 与 observer 均 FAIL、进程退出码均为 1，display hash 为空且没有 evidence index。driver 在 18.5006s 记下 stale rejection 已到；后续 selected source 保持 busy 到约 20.512s 才完成取消，driver 仍等到 28.4976s 并以“server did not reject the stale power probe within 10s”失败。该目录的 `.jsonl` 同时明确记有 version 5/entity 5/target 37 请求被拒绝为 `stale state version`、当前 version 6。代码复核与根侧说明将原因定位到旧 observer 的 Priest-only 门控：Mage 的状态提示会被后续普通回合状态覆盖。当前 `Assets/Scripts/FireblastReplay.cs` transport observer 已改为 step 5 直接记录 `ServerActionRejected`，不再要求 Priest；相关 Priest replay contract 断言也覆盖了这一条件。r1 历史 FAIL 保留，不拿新 harness 修复覆盖旧 run。
- `work/f021-mage-r2-1920/run.json` 使用 r12 DLL SHA-256 `de0aea31741c5de9f956142d3f7c313a54722d7d9149c518c95d34bd00ca73fb`，root 说明 r12 只改变上述 harness 门控。driver 32 checks、observer 3 checks 均 bounded PASS，driver/observer 退出码 0，server 以 `owned authority cleanup` 退出码 1；双方 public hash 为 `65b1286cccefc17936ed18877b4346956d0e8d6ddfb2cfdfdcadbc7ad778c3df`，两端 display hash 仍为空，且无 evidence index。`.jsonl` 记录 version 5 stale 请求被拒、version 6 正式 HERO_08bp action 被接受并推进到 version 7/hash。driver records 显示 transport rejection 被观察、source cancel 后 selection 清空且 clone/pending 均为 0，随后正式 drag 发出一次 power 请求；source graph cast/arrival 完成（arrival 1.5065s、一次 Birth/Action），source audio 全部停止，scene-mix 576 callbacks、0 overflow、0 clipped samples。通用报告的 `finalTargetHealth=-1`，因此不据此声称目标生命数值变化已复核；本地 PASS 也不证明 live display hash、官方逐帧或硬件输出。root 报告 26 个 Priest+Fireblast 定向测试通过，本次未重跑。

### 2026-10-25 weapon glow v2 来源与范围

- `local/runtime/weapon-audio.json` 中友方 slot 41 的 source material 是 `FX_Weapon_SummonGlowblue`（`CAB-ce5994190d1aec0cb58381aba153b724`, pathID `-4304543045473157492`），纹理 `WeaponSummonBlue`（`CAB-a8e7d77726c597e39e981817c71a6447`, pathID `-646629447142992627`）；shader 为同一 material bundle 中 `CAB-ce5994190d1aec0cb58381aba153b724`, pathID `-666151408197827071`。slot 42 对应 opponent/red 版本。profile 标为 `source-additive-tint-v2`，`shader_known=true`、`compiled_source_verified=true`、`texture_alpha_opaque=true`。
- 旧的 shader 未知结果来自读取空的 Shader `m_Name`。当前 `tools/weapon_audio.py:80-85` 改为读取 `m_ParsedForm.m_Name`，导出 `Hero/Additive/Additive`；`tests/test_weapon_audio.py:18` 覆盖 `m_Name` 为空而 parsed name 有值的情形。审计对象与基准 shader 在不同 CAB（基准 `CAB-eafd1cdad45fcefbd0ab155f627ea3d7`），因此这里依据 `weapon-audio.json.shader_verification` 中 family 16 / platform 4 的编译程序、参数布局与 render-state exact-audit-match，不把相同 pathID 说成同一个 Unity 对象。
- 导出的 material definition 记录 keywords 空、基础 pass 与 alpha blend 均为 SrcAlpha/One（5/1），`_LightingBlend`、`_GlowIntensity`、`_GlowAddMul` 均为 0，`_MainTex` ST 为单位 scale/零 offset，`_TintColor=(0,0,0,0.5019608)`，`_Color`/`_GlowColor` 为白色，`_InvFade=1`；两侧 glow 纹理 alpha 都是 255。此证据支持严格限定条件下的平面 additive base/material 重建；它没有恢复完整 Actor 的父子 transform、朝向、局部生命周期或 FSM/动画时序。Rogue r3 的 `reference-rogue-equip-glow.png` 有可见白蓝高亮且该帧无黑色矩形；本地截图仍不是官方同帧动态对照。

### 2026-10-25 H01 粒子取景修正记录

- r8 的 `work/f021-all-build-r8.log` 在 HERO_01bp route 遇到 `NotSupportedException: ... requires the reviewed stationary sphere-particle profile`，该版本不通过。r9 的 `work/f021-all-build-r9.log` 报 t=0.68 bright bounds 仅 14×14（145 bright pixels），小于 122×126 按钮的半尺寸要求。r10 `work/f021-all-build-r10.log` 两帧仍只有 14×14 与 43×48，未通过；它用探针 camera center `(20005.66,10000,19991.91)`、orthographic size `5.23126936`、aspect `0.96825397`，是失败的替代取景尝试，不是最终实现。
- 最后的 `work/f021-warrior-route-r11.log` 记录 source camera `(20000,10100,20000)`、rotation `(0.70711,0,0,0.70711)`、orthographic size 16、aspect 16:9。两帧 GPU bounds 为 t=0.68 的 42×42 和 t=1.02 的 133×146，按钮为 122×126，route 共 11 checks passed。当前 `Assets/Scripts/NativeEffectPlayer.cs` 的 `DrawHeroPowerButton` 分支保留源 placement，把 root 1/full-board affine subtree 合成到 1920×1080 画面；注释明确完整爆炸 fit 到按钮会缩小 authored world scale。因此 r11 是保留源 camera/world scale 的 bounded reconstruction，r10 camera 切换代码没有沿用。
- r11 的局部 GPU checks 只证明当前输入资源和这条合成路径达到设定像素阈值。它们没有官方动态同帧或原版运行时 camera/Actor 生命周期对照，仍不能称逐帧等价。

### 2026-10-25 Mage review r1 回放补充

- `work/f021-mage-review-r1-1920/run.json` 与两个 Player `report.json` 记录同一 r12 DLL SHA-256 `de0aea31741c5de9f956142d3f7c313a54722d7d9149c518c95d34bd00ca73fb`；driver 50 checks、observer 10 checks 均为 `PASS bounded formal GUI/input/DSP; official fidelity unverified`，两个 Player 退出码为 0，server 以 `owned authority cleanup` 退出码 1。根侧报告 analyzer PASS；本次只读复核 run、报告、timeline 和索引，没有重跑 analyzer。
- `evidence-index.json` 明示 `official_fidelity_verified=false`。两端 public hash 同为 `d4caa4242950a9879e5048dd8da4195a2c9b91f2869435c6971766e67b764db6`，display hash 为空。driver/observer 的实际 Hit timeline 各记录一个 Hit callback、source duration 0.55s；完整窗口分别为 0.547407s / 0.552280s，最大 wall-batch drift 为 0.004308s / 0.000938s。timeline 末帧两端 authority/display health 均为 27，damage decoration 为 1；通用 `finalTargetHealth=-1` 字段未填，数值以 timeline 样本为准。
- driver 末帧 `inputLocked=false`，但 `sourceCompleted=false`、`sourceReleased=false`、`hashMatched=false`；observer 末帧仍 `inputLocked=true`，且同样没有 source completion/release 或 matched hash。因此这次只证明双端 Hit 反馈窗口与数值样本，以及 driver 末端输入已打开；不证明双方 source 后解锁或完整 display hash 一致。observer 采样窗口结束时仍在反应中。
- r1 结果不改变 Mage r2 的历史记录。根侧报告 r2 默认 analyzer 的无 timeline 路径曾因 `frames` 未初始化而失败，补 `frames=[]` 后分析通过；该修正是 harness/analyzer 路径，不需要重建产品 DLL。r1 使用 r12 DLL，本地 bounded PASS 仍不能证明原版动态逐帧等价、系统音频设备 loopback 或硬件实际可听。

## 验证边界

本轮结论是来源/清单审计，不是 Player、GPU、原版逐帧或音频等价验收。没有修改失败期望，也没有把全量源快照差异归为可忽略的旧测试。
