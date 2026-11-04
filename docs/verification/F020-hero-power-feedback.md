# F020 英雄技能反馈修复记录

2026-10-22，TASK-0006，执行者记录；整体保持 IN_PROGRESS。

## 已有修复及本地证据

- H06/H09 的 source root 在初始化 `Seek(0)` 后绑定技能按钮位置，并在后续采样保持该绑定。此前初始化会覆盖放置位置，造成光环出现在战场中央。H09 的 Impact 仍绑定实际 Heal 目标。
- `work/f020-position-fix/h06-player-r1/run.json`：1280×720，driver 28 checks、observer 正常退出；双方公共/显示 hash 一致。`driver/05-cast.png` 可见技能按钮上的爪形光效。
- `work/f020-position-fix/h09-player-r1/run.json`：1280×720，driver 37 checks，唯一 Heal 与 GUI callback，自身英雄 28→30，双方公共/显示 hash 一致。`driver/05-cast.png` 可见按钮光环及英雄上的治疗光效。Listener 有 22 个 float 样本绝对值大于 1，不能声称无削波。
- 上述两次 Player DLL SHA-256 为 `D09A7A5B67B35F059A5005925F010FA36811401087D29143C2406DC2674A4036`；不是本轮稍后构建的结果。原版同机位、全尺寸与听感一致性仍待核验。

## H05 猎人命中诊断

- 已保留 impact 粒子来源 simulation space；相应 Editor 检查在 build-r9 至 build-r12 为 50 checks passed。不能只凭这些结构检查判断爆炸恢复。
- r8 的离屏图在 256×256 纹理中约 49×49 区域有内容；将相机 ortho 从 2 缩至 .5，内容变大。这只证明缩放实验的响应，不证明原相机范围有误。
- 独立代码复核发现 `NativeParticle.Create` 末尾已设置粒子 renderMode、alignment、lengthScale、velocityScale、maxParticleSize 和 mesh；此前“缺失渲染参数”的判断已撤回，没有为此重复修改。
- 原 `OffScreenRenderFX.UpdateOffScreenCamera` 的静态 IL 与现有 localScale×mesh X bounds/2 计算一致。当前诊断截图在 Arrival 后 .95 秒才取得，需补查峰值时段；不得以 .25 倍相机范围作为无来源的生产修复。
- 诊断代码曾临时更改实际 capture camera，下一次 Render 才恢复；应移除这类采样对生产状态的污染。

## 本轮针对性回归

`pytest tests/test_fireblast.py tests/test_match.py tests/test_hero_power_import.py tests/test_shaman_hero_power_import.py tests/test_hunter_trail_diagnostic_contract.py tests/test_priest_player_replay_contract.py tests/test_druid_player_replay_contract.py tests/test_rogue_player_replay_contract.py tests/test_warlock_player_replay_contract.py -q`：127 passed，21.83 秒。

上述结果不等同全职业视听还原验收。正在分别核查 H05 命中、H07 生命分流及通用/萨满召唤可见性；共享 Unity 项目和 Player 构建仍串行使用。

## r13 构建与 r9 失败

- `build-r13.log` 为 Windows Player Build Success；主场景恢复后 SHA-256 仍为 `322FF7094630BAF2323D0628349CDB213580C8CD01D43DACB11FD8CC0444B8AD`。
- `h05-player-r9` 因新增截图工具误访问已销毁 flight 的 Unity Root/Camera 而失败，不能登记为技能验收通过。失败工件完整保留。工具现把 Impact 采样与 flight 采样分开，并对已销毁 Unity 对象早退；修复后 targeted contract 为7 passed，尚待新构建运行。
- r9 实采粒子年龄 `.054/.151/.376s`，RT 有色像素分别 `123/1105/5162`、包围框约 `17/45/92px`，相机 ortho 均为2。来源爆炸在箭体动画与随机等待之后才开始，先前截图处于强度增长段。仍需取得 `.5/.8/1.2s` 及同帧最终屏幕，不能据此提前判定视觉恢复。

## 召唤可见性与术士来源复查

- 现有业务代码已以 `NativeActorPresentation.IsActorShown → LiveActor.PresentationVisible → ClientApp.PortraitContent` 统一隐藏原画、框、阴影、嘲讽、攻血与Z。H02 Editor新增可见FX兄弟节点夹具，确保FX可见不会误开隐藏Actor UI；待串行Unity入口验证，目标24 checks。Python Shaman import 14 passed。
- H07原CardDef同时引用primary与一个SubSpell；当前主引用优先选择器没有分派第二项。原客户端SubSpell由 `HistSubSpellStart.SpellPrefabGUID/SourceEntityID/TargetEntityIDS` 驱动，不能仅凭CardDef数组顺序把第二项无条件绑到自身Hit。
- H07当前展开图的SourceAuto(type10)与父Impact.location4均指向技能按钮，不能把location4解释为英雄头像。另发现真实R2T为 `target=-1/CreateRenderPlane=1/RenderMaterial=0` 的显式材质自动平面，当前仅支持mode5的生成路径漏掉它；source-only `RenderTogether(empty)` 也没有更新自身capture。正在先修这两个可证明的生产缺口，再决定是否需要额外SubSpell适配。

### H07真实资源修复与Editor结果

已恢复mode0显式材质自动平面、隐藏源对象时的离屏相机位置，以及原 `Custom/FX/FX_AreaEffect_Render` base pass 的Ramp解码（兼容shader family43）；无Flow分支现在更新自身捕获。没有改生命分流SoundDef，也没有无条件启用SubSpell。

`work/f020-position-fix/h07-editor-r1.log` 自然退出0，`NATIVE WARLOCK LIFE TAP: 534 checks passed`。新增断言用真实H07资源在`.1/.25/.5/.9`采样，覆盖生成capture、实际纹理内容、family43和最终合成内容，音源仍只一次。Python针对HeroPower import/native effects/Warlock replay contract为122 passed。Main.unity SHA-256未变。此结果是Editor GPU执行证据，仍需新Player头像截图及Listener验收；第二GlowAdditive pass与SubSpell完整调度未因此宣称恢复。

### 后续已定位、正在实施

- H04：父配置location3=SourceHero，但NativeAreaPlayback使用固定离屏host原点，最终效果位于棋盘中心；真实scene位置/相机映射已修复。SpellFinish后的两秒淡出由原生FSM继续驱动；本轮拆分其视觉尾段与authority batch，细节见文末H04记录。
- H03匕首：原武器Actor SpellTable的41/42槽装备声音、7槽耐久伤害声音、5槽死亡声音均已从原Prefab/FSM PPtr及SoundDef确认。现有武器模型路径没有原Actor SpellTable，不能把“Resolve支持type7”当作生命周期接入。正在建立限定无自定义override的CS2_082来源音频适配，不扩展未知自定义武器或改变规则。

### 武器音频与综合回归第一轮

`NativeWeaponAudioChecks.Run` 在 `weapon-editor-r1.log` 自然退出0，90 checks passed。生产适配现限定CS2_082：原41/42槽 `hero_weapon_draw` 音量.8、7槽 `Weapon_Durability_Hit_01` 音量.6、5槽 `Weapon_Destruction_01` 音量1，均delay0。原AnimationPlaythrough启动后立即Finish，不能拿动画.683333秒当声音延迟。八份真实规则双视角批次验证Presentation回调、解码/voice启动和误绑定/重复/reset边界；尚不等同正式Player Listener验收或完整武器动画还原。

`NativeSuperSpellChecks.RunRegression` 的 `regression-r1.log` 自然退出1：H07 534、H09 82及前序检查通过，在H04 authoredCamera=true / age0 / seat0的SourceHero投影断言失败。原失败日志保留，已退回执行者修复；不放宽投影断言、不登记综合回归通过。

### H07 头像吸取视觉复核

- 用户指出 `work/f020-position-fix/h07-player-r1/driver/05-cast.png` 与 `00-warlock-ready.png` 看不出头像“嘬一口”效果。该正式1280×720 Player批次的DLL SHA-256为`bc33595edcb618864eb99c6b1d7fc0dbe05f74b55266f3c2847129b8f92a63eb`；driver/observer均自然退出0，分别26/4项检查通过，最终双方hash一致。报告记录唯一`PlayHeroPower`、自伤`Hit`与`Draw`各一次，source root音频动作/播放各一次；报告没有SubSpell历史事件字段，故这不是SubSpell视觉验收。
- 已读本地原资源manifest `manifests/hero-power-native-sources.json`。H07 primary `/m_PlayEffectDef/m_SpellPath` 指向 `ReuseFX_BuffImpact_Void_SwirlIntake_Small_HeroPower_Super.prefab:5c3f93a27c9cb434ea6e0c37005a2c0d`，SubSpell `/m_SubSpellEffectDefs/0/m_SpellPath` 指向 `ReuseFX_HealImpact_Void_Small_Super.prefab:05489d0a27c884946819a6ffee9e40e3`；在11个基础技能的已导出CardDef引用中，后者只出现在H07。SubSpell程序导出SHA-256=`016b59391db48638f4425d31b2d76b7c82769cd3dc947692614b3a25c82cb4aa`，与primary同属资源bundle SHA-256=`cf777f395fd6edd4a17d4571eaafad5d416bbb8e8c4294f1f615c4dd76f91a4e`。
- SubSpell prefab展开为21节点；序列化的 `SuperSpell` 与 `Spell` 均为 `location=2`、source指针为空，Start启用但prefab为空，Missile关闭；FSM包含激活/粒子/音频/Wait动作，没有Flow或目标实体ID字段。其显式R2T capture的 `target=-1` 是生成平面配置，不能据此认作英雄实体。粒子节点名包含 `fx_wisps_suckin`、`fx_motes_suckin`、`FX_streaks_suckin`，说明资源含吸入风格粒子，但没有序列化的头像锚点或连接到头像的轨迹。`m_TargetInfo.m_Behavior=0`、随机目标数8–10及Impact原始location枚举的语义未在本次证据中解析，不把它们解释成自我命中。
- primary与SubSpell各自的FSM都引用同一个SoundDef及同一音频流：原片段SHA-256=`1017ea78fd44523a96d262392da474d874ddd4556179f922ead94237097b0319`，解码WAV SHA-256=`1be895c69e13cf1843744ffc868e36011715f9b5d7e4aef265bf750efb8eedaf`。因此额外调度SubSpell还会引入重复播放风险；当前没有来源证据支持它应在头像处播放或静音。
- 结论：**当前不安全接入头像**。资源证明有独立SubSpell和吸入风格粒子，但没有证明它的历史目标就是施法者英雄，也没有足够信息恢复其原版播放时序；权威规则`Hit(target=自身英雄)`不能单独代替 `HistSubSpellStart.TargetEntityIDS` 证据。生产路径继续只播放已审核的primary source root，不新增SubSpell、Hit冲击或音频回放。没有修改业务代码、Editor、资产或场景；本轮未运行Unity/Build，文档研究无需追加pytest。
- 后续若要打开限H07的行为重建，需从原版播放采集该次 `HistSubSpellStart` 的 `SpellPrefabGUID`、`SourceEntityID`、完整 `TargetEntityIDS`，并将实体ID关联到CardDef/Actor/头像位置，确认它与唯一自伤Hit的对象和时间一致；再用Editor检查保证目标仅为真实Warlock英雄、source audio仍恰好一次，并检查头像实际合成帧。未取得这些字段前保持Blocked。

### H04 source-action 尾段与 authority batch 分离（2026-10-23）

- 旧正式Player时间线在`batchTime=2.696s`仍记录`Busy=true / InputLocked=false`，到`2.712s`才变成`Busy=false / InputLocked=false`；驱动器在`frame=3231`后才发送End Turn。它没有覆盖尾段期间到达的后续authority batch。源码状态机显示：当后续快照在此期间进入`Accept`，会排队；当前批次又因`NativeActive`等到原生area进入`None`，队列非空令`InputLocked=true`。所以尾段不会单独锁住当前输入，但会间接推迟下一批之后的输入。
- `NativeEffectPlayer.SourceBusy`现只报告root/area尚未完成的来源工作；新的`SourceVisualTailActive`单独报告H04淡出。`Presentation`在H04普通批次cue结束后保存原cue与before/after上下文，将仍活跃的尾段分离；旧key仍进入`NativeKeys`、`PrepareNative`和Repaint绘制，并继续用原cue age采样。`Busy`保留视觉生命周期，`InputLocked`只判断authority队列/当前批次；选择界面也改按`InputLocked`开放。
- `NativePaladinHeroPowerChecks.RunSourceRoute`新增真实H04资源状态机夹具：source age约`.85s`时确认area已完成但fade仍活跃；在约`1.05s`分离尾段后接入`TURN_START` authority batch；约`2.25s`确认新版本已显示、输入已解锁而旧source仍可采样；随后验证旧FSM到达`None`并释放。此Editor检查已编写，本轮未运行Unity/Build；不能登记Editor或Player通过。
- `pytest tests/test_paladin_player_replay_contract.py -q`：最终复跑4 passed。中间两次单项失败分别是Editor夹具描述字符串未同步，以及原Python合同对fixture class选择器整行匹配、遇到新Shaman分支后过期；将其调整为只锁定Paladin分支后通过。它们都不是运行时行为失败。TASK-0006保持`IN_PROGRESS`，原版同机位与音频对照、完整H04 Chain含义仍未知。

### H07 原版 SubSpell 历史记录只读追查（2026-10-23）

- 限定搜索范围为仓库 `work/`、`local/runtime`、`local/visual`、`local/script-audit`、`manifests/` 和本记录；未扫描仓库外用户目录。事件字段搜索只命中文档与静态 IL；`work/` 的既有日志及对局工件没有 `HistSubSpellStart`、`SUB_SPELL_START`、`SpellPrefabGUID` 或 `TargetEntityIDS`。
- H07 自建 Player JSONL 只记录本地命令：时间 `1792679276.5073948`、技能按钮实体 5、目标 0；SHA-256=`61136C7E091A544561F665C76A253DFD3F9D38A2D2B77428259A3A59AB03D34A`。本地规则将实体4识别为 `HERO_07`，实体5为 `HERO_07bp`，但这不是原版 SubSpell source/target 记录。
- `work/f005-ui-research/GameState.il.txt`（SHA-256=`F4A3EDE5E20D39424935911CF87B666A353E70224EA380E2976D1D113BCB9F5A`）显示静态 `DebugPrintPower` 能输出 SubSpell GUID、source 数字和目标数量，并可逐项解析目标名称；它没有直接格式化原始目标 ID 数组。当前没有头像锚点的运行证据。
- H07 头像吸入仍不接线、不补播 SubSpell 音频。最小补证是在原版客户端施放一次 Life Tap，同步记录实体 roster 和 `HistSubSpellStart` 的时间戳、GUID、source、完整 target ID 数组，再把实体映射到英雄头像；可先检查 `Power.log` 是否有对应事件，但单靠静态日志格式无法恢复 raw target IDs。

### 2026-10-23 H04 Editor/Player构建复验与 H10/H05 续查

- H04尾段隔离实现随后由root串行执行Unity `NativeSuperSpellChecks.RunRegression`，日志`work/f020-position-fix-h04-editor-r1.log`自然退出0；H04真实`FriendlyArea`路由56 checks、Presentation 280 checks通过。日志中的Impact diagnostic observer异常是负向夹具预期，不影响最终成功标记。`BuildClient.Build`日志`work/f020-position-fix-h04-build-r1.log`明确Build Successful并自然退出0；新Player `Assembly-CSharp.dll` SHA-256=`B31DBDF66DC54DD1DCE80092095BAFE8E85AA79C62ADC3FD596120C137595444`。构建只重新编号`Main.unity`中同一Client对象的本地fileID；已按构建前快照恢复，当前文件SHA-256=`322FF7094630BAF2323D0628349CDB213580C8CD01D43DACB11FD8CC0444B8AD`。相关Python合同复跑74 passed。
- H10来源点坐标合同修正后，`work/f020-position-fix/h10-player-r9-position-diagnostics-1280/run.json`为`PASS bounded H10 dual Player 1280x720`；隔离测试运行器退出0、双方权威/显示hash同步，mana 10→9、英雄攻击0→1，source释放后`Busy=false / InputLocked=false / pending=false`，Listener 48kHz stereo、overflow 0。运行器结束后production index仍`H10 enabled=false`。截图对比r7/r8/r9确认绿色主图案/光晕从棋盘中央移到技能按钮附近，但source-active仍有屏幕低边绿色残片；左上小图案在改坐标前的r7也已存在，不能归因于本次改动。残片来源/对应原生层尚未查明，因此仅定位改进，不证明构图已修好或达到Mage基线，不登记H10视觉通过。
- H05 `work/f020-position-fix/h05-player-r12-1920`是本地有界双端回放；`evidence-index.json`明确`official_fidelity_verified=false`。实际音源顺序为`Arrow_Targeted_Fire_03` 14.0617s播放、`Arrow_Targeted_Impact_03` 14.1939s、`Arrow_Targeted_Build_02` 14.1944s、`Arrow_Targeted_Build_01` 14.9494s、`Arrow_Targeted_Explode_02` 14.9525s，Fire片段15.177s停止；当前证据没有显示发射音被重复触发，也没有原版同段音频听感对照。屏幕帧50ms只有小火花、500ms目标上出现较亮星状爆发；`hunter-missile-mid.png`不能证明飞行箭体清晰可见。impact年龄诊断缺少800/1200ms样本且观察时点有误差，不能据此评定完整命中生命周期。H05维持视觉FAIL。
- H10边缘片段及H05箭体可辨性仍需按实际Player帧进一步定位；不以Editor合同、音频动作顺序或本地截图代替原版逐帧/原声比较。TASK-0006继续`IN_PROGRESS`。

### 2026-10-23 H04 尾段期间真实提交回合并追踪下一批

- Paladin Player夹具不再等到`Presentation.Busy=false`才继续；它在`HERO_04bp` FriendlyArea视觉尾段仍活跃时，通过生产`ClientApp.SendMove`提交authority提供的真实`end_turn`。随后检查对手`TURN_START`到达、该新批次自己的输入锁解除、显示hash追平，以及旧source尾段最后释放。
- 最终正式双端回放`work/f020-position-fix/h04-player-r6-tail-1280/`由新Windows Player DLL（SHA-256=`A2D657285A30620C2E27BD413E4D41FE251F3844456263FA03BCC8CD774270E1`）在1280×720运行；driver 25 checks、observer和双端运行报告通过。unlock/提交发生在frame 2822：`Busy=true / InputLocked=false / sourceTailActive=true / hashMatched=true`。Mage authority版本8的`Draw,TURN_START`于frame 2825（realtime 14.52s）抵达时，旧尾段仍活跃；该新回合自身的批次在同步期间短暂锁输入。frame 3170（15.95s）时其显示hash追平且`InputLocked=false`，Paladin旧尾段仍活跃；frame 3247（16.27s）旧尾段才释放。由此验证旧视觉尾段不再卡住新authority批次后的输入；新回合自身的短暂反馈锁仍正常存在。
- 实际`--analyze --paladin`报告解析通过。driver仍恰好扣2费、播放一个HERO_04bp与召唤一个1/1`CS2_101t`；FriendlyArea clone/Finished各1，来源音频动作/播放各2。observer侧不产生driver的规则事件计数，分析器现在明确区分两端证据。`official_fidelity_verified=false`仍保留：这验证的是本地权威/GUI/来源生命周期，不是原版画面或原声等价。
- 保留r3/r4诊断工件：r3在新测试发送前被过早的“所有AudioSource已停止”断言拦截；r4完成回合/尾效时序，但Python analyzer暴露了帧哈希仍使用cast旧值及observer事件计数不应套用driver的合同缺陷。修正帧级比较与角色条件后r6完整通过。构建未改动`Main.unity`，当前SHA-256=`322FF7094630BAF2323D0628349CDB213580C8CD01D43DACB11FD8CC0444B8AD`。
- 最终复验：`pytest tests/test_paladin_player_replay_contract.py tests/test_paladin_hero_power_import.py tests/test_native_effects.py -q`为74 passed；Python `py_compile`与限定文件`git diff --check`通过。TASK-0006仍`IN_PROGRESS`，H04完整原版Chain含义、逐帧同机位与原声对照仍未证明。

### 2026-10-23 H05真实Bang年龄采样复验 r13

- r12只在GUI Repaint里推进一个样本。粒子年龄首次被观察到时已约231.55ms，但仍被写成50ms；紧接着的241.44ms被写成150ms，之后415.04/692.96ms被写为300/500ms。原生`Bang`系统时长为0.7秒，因此800/1200ms请求超出其发射周期。这个摘要不能作为目标年龄截图序列。
- `FireblastReplay`现以250/450/650ms作为有效寿命内的早/中/晚三个目标，并限定±50ms；错过窗口会记录目标、帧和实际观察年龄，摘要不会把超差样本算成完成。只读/诊断逻辑未触碰生产效果。
- Unity `BuildClient.Build`成功（`work/f020-h05-impact-build-r1.log`），新Player DLL SHA-256=`9ABAE9305BA2327ACC37360218DDBA937E12D8B34FC9D0884D696170FA9328AD`。构建只重写`Main.unity`的8个本地fileID，已恢复构建前快照；当前SHA-256=`322FF7094630BAF2323D0628349CDB213580C8CD01D43DACB11FD8CC0444B8AD`。
- `work/f020-position-fix/h05-impact-r13-player-1920/`真实双端Player在1920×1080通过（driver 28 checks、observer 3 checks，public hash一致），`--analyze --hunter`退出0，诊断年龄摘要complete=true：目标250/450/650ms实采221.56/462.90/627.68ms，误差−28.44/+12.90/−22.32ms；GPU readback三帧成功。Impact离屏帧从小亮点扩展为星状爆发；实际棋盘命中帧在英雄头像上出现闪光。`hunter-missile-mid.png`仍没有清晰飞行箭体；同帧TrailRenderer生成11点/24顶点/22三角形，Arrow与Glow renderer禁用，粒子trail数量0。飞行轨迹最终不可辨的渲染/色彩原因仍Unknown，H05仍视觉FAIL，且`official_fidelity_verified=false`。
- focused `tests/test_hunter_trail_diagnostic_contract.py -q`为8 passed。只修正了诊断样本年龄标记和漏采报告；未改生产技能或材质/特效参数。TASK-0006保持`IN_PROGRESS`。

### 2026-10-23 H05真实技能按钮源点复验 r1

- 根因：`ResolvePresentationPosition`将未列入例外集合的source entity固定放到`(960,480)`，H05由此从棋盘中部起飞。现在H05与Druid/Priest/Demon Hunter一样使用实时`Presentation.NativePosition`；Editor和pytest合同覆盖正向定位以及缺失位置不伪造回退坐标。
- `NativeSuperSpellChecks.RunRegression`自然退出0，`PresentationChecks`通过280 checks；`BuildClient.Build`成功。新Player DLL SHA-256=`1DB5BAD5186C3411C14DF94CC2FAD9BF2FB50D1C0F81256B910A13E75760A71A`。场景文件按构建前快照恢复，`Main.unity` SHA-256=`322FF7094630BAF2323D0628349CDB213580C8CD01D43DACB11FD8CC0444B8AD`。
- `work/f020-position-fix/h05-source-position-r1-1920/`双端正式Player与analyzer在1920×1080报告有界本地回放PASS（driver/observer 28/3 checks，双方public hash一致）。运行时source位置从旧固定`(20000,10000,20001.78)`改为实时`(20004.49,10000,19991.67)`，target仍为`(19999.53,10000,20009.03)`。在`driver/hunter-screen-mid-frame-2779.png`中，先前空白的source-to-target区间现在出现细金色轨迹；轨迹仍偏弱，尚未证明等同原版火箭箭体。0/100/200ms arrival Trail样本完整且同帧读回成功。
- 本地回放JSON通用的`finalTargetHealth=-1`及`inputUnlockedAfterSource=false`是未采集的哨兵值，不能据本轮报告证明伤害或源完成后解锁；原版同机位/原声比较也仍缺。`official_fidelity_verified=false`，H05视觉继续FAIL，TASK-0006保持`IN_PROGRESS`。focused pytest三组18 passed，`git diff --check`无空白错误；未提交。

### 2026-10-24 H11双层渲染与H05 Impact 音频复核

- H11原生source节点17（Source_Root及其FX_Smoke/FX_Motes）现独立绘制于实时技能按钮，原始破土/裂地/尘土/碎石节点绘制于权威Ghoul生成处；使用同一FSM age采样。双端Player与analyzer在1280×720及1920×1080均通过，双方authority hash一致，CustomSpawn/side event/Timeline/token音各一次，generic summon fallback和未处理action均为0。按钮层早期截图显示青绿色脉冲；`.55s`破土层有明显白色Bloom及矩形边缘，不能据此登记对原版视觉保真通过。证据目录：`work/f020-h11-button-r2-1280/`、`work/f020-h11-button-r2-1920/`。
- Hunter 1920×1080双端正式Player回放/独立analyzer通过，driver实际Hit callback在13.927s，原生声音回调在13.954s提交`Arrow_Targeted_Explode_03`（本次三选一随机action选中该变体），`playing=true`；距Hit约27ms。Listener PCM采样48kHz stereo、overflow 0。证据见`work/f020-hunter-impact-audio-r1-1920/driver/report.json`和`run.json`。该记录验证本地Player音频cue触发与Listener链路，不代表对照原版音量/听感。
- Unity全回归与`BuildClient.Build`均自然退出成功，H11 disabled-route 36/positive route 19、Hunter 55、Presentation 281、NativeEffect 80,526 checks通过；对应日志`work/f020-feedback-regression-r3.log`、`work/f020-feedback-build-r3.log`。Focused pytest三组29 passed。H05飞行箭体可辨性和所有官方同机位/音频A/B仍未解决；TASK-0006保持`IN_PROGRESS`。

- H11 后续视觉复核定位并修正了 `.55s` 方块：`Mesh_Initial_Crack` 的 CommonFX RGBR ADD 关键词此前落入 generic fallback，One / OneMinusSrcAlpha 混合把透明角落隐藏的土色 RGB 绘成方形。按原 DXBC VS 130 / PS 1218 添加精确 family46；`work/h11-commonfx-add-r3-gpu.log` 中 shader GPU 308、NativeEffect 80,546、Glow 120、Affine 491、Native GPU 41 均通过，Focused pytest 93 passed，`BuildClient.Build` 成功。1920×1080 双 Player/analyzer 通过，修正后 `.55s` 与 `1.0s` 截图无方块且按钮青绿效果、Ghoul、裂地碎石/烟尘均可见，截图见`work/f020-h11-commonfx-add-r3-1920/driver/`。尚未做原版连续帧 A/B，视觉不登记为逐帧等价。

### 2026-10-24 H10 source/target 分层定位与启用门

- Confirmed：用户提供的两张原版截图显示绿色主纹章/光晕包围恶魔猎手头像，技能按钮仍以暗色为主、周边只有少量绿色碎片。导出 H10 根图 `ReuseFX_BuffImpact_DemonHunter_Small_Super` 中 node 0/1（source 与 Idle）和 node 12（Impact/英雄目标树）是独立顶层；owner 1 / 12 各自有FSM。旧 `ScreenRect` 把整张合成图按 source 按钮偏移，故会把头像目标层也画到按钮。
- H10 坐标统一使用完整棋盘空间：默认相机垂直覆盖 32 世界单位，`FromPresentation.Project` 的 `32/1080` 映射与 1920x1080 纹理一一对应；因此 source/Idle 节点绑定实际 `SourceAuto` 按钮，node 12 与 Impact clone 使用同批 Buff 英雄世界锚点，所有层都以全屏矩形合成。之前把同一按钮/英雄偏移再次写入 `DrawRect` 会重复平移，已移除。每次源程序采样后重新应用 source/hero 锚点；Editor合同新增实际 GPU 全屏合成和按钮/英雄像素邻域断言。
- 本次 Importer 改动只增加来源 profile 的 node 0/1/12 独立根层级校验；`native_effect_enabled` 的既有启用逻辑未改。总控已在主仓库串行重导，runtime index 现为 `enabled=true, duration=.1`、registry 1357 项且 0 errors。Editor锚点/层级与真实规则配对检查已补，本worktree 未运行 Unity/Build/Player；新画面与声音仍待本地Player验收。
- Unknown：截图不是连续同机位逐帧/原声A/B，原版精确动作时点与混音仍未确证；这些保留为保真限制，不再阻断该 bounded route。另为 H03 修复 SourceAuto 对两侧实际按钮的绑定，并在 Flow-null `Seek` / `RenderAffine` 采样后保持 node 0/1 锚点；Editor 检查覆盖双方及 `.95s/1.2s` 采样，待总控串行运行。

### 2026-10-24 Rogue装备前召唤光效来源

- Confirmed：CS2_082 的 Actor SpellTable 41/42 分别绑定 Friendly/Opponent SummonIn；两者都含初始关闭的 `Summon_quad` MeshRenderer，使用原始 512x512 `WeaponSummonBlue/Red` 贴图和 `FX_Weapon_SummonGlowblue/Red` 材质。`AllyInPlay_SummonMinionGlowFadeIn` 为 0.6833333s，包含四键 RGB tint 曲线与持续开启的 renderer 曲线；FSM 的 `AnimationPlaythroughAction` 先启动动画后立即Finish，所以该时长不是音频延迟。
- 已把贴图来源与原RGB曲线导入 weapon-audio profile；运行时仅按权威新武器 Summon 事件启动，定位在现有武器槽，动画区间先显示来源光效，结束后显示新武器。Editor 检查增加两侧资源、曲线采样和时长断言。
- Unknown：源材质 Shader 指针存在但序列化名称为空，原 blend mode 与3D quad到此项目UI比例的映射无法确认；当前使用真实贴图/tint的有界GUI覆盖，不声称恢复原shader合成或原版逐帧等价。待总控重导本地资源后串行运行Editor/Player验收。
