# F023：音效、换武器卡死与对局开场

## 本轮交付结论（2026-10-27）

最终Windows Player为r9，DLL SHA-256 `496937AFCF0D5897251656D2BC45AACA9DFBB02FAAB75B017E6116A041D673B8`，固定源码 `60fd1b8b6717a0a66d92474d8741e9b325eae03e`；综合Unity/GPU/Build自然exit0，47个定向Python测试含quiet实际raw重验通过。r8→r9仅菜单行对齐及相应检查，盗贼/音频/H10等实现哈希不变；不同版本实际证据不伪称全部在r9重跑。

- 盗贼根因是H03 source-only无Arrival，旧武器Death却无条件等技能命中。真实源旧Editor推进9秒永锁，新码只解除替换武器Death依赖并保留fired/applied。`work/f023-rogue-weapons-shaman-r5` 双端完成空槽装备v13、攻击v17、占槽替换v18、绿色唯一EndTurn、真实concede v19、同hash `5fc7601d…` 的双方ResultReady与主菜单，独立V023有界PASS；原用户748场客户端完整转储/逐帧死亡动画不冒认。
- 圣骑士、牧师原生Start音源从离听者约30000/10000单位修回22.8/24.46，原WAV在实际Listener中独立相关约0.93/0.89；不以回调或总RMS代替来源归因，Loop与设备听感范围保留。
- H10原热prepare约34ms降至约0.12ms；无PNG实际GUI quiet双端P50约4.2ms、P95约5–6ms。首次约263ms和51/83ms偶发长帧仍有，源码缓存/同龄推进与PNG探针开销已区分，未宣全程无卡顿。
- 24实际Player覆盖11英雄各双端Attack与Rogue武器对照，48观察对应24权威事件，原clip/Listener与独立音源分组核对；原15clip缓存可溯源，实际本轮选中11种_0，未扩大全部随机变体覆盖。七菜单用原atlas，左3右4、首三对齐、右第4SORRY；最终r9 `work/f023-opening-priest-dh-r5` 两端自然exit0/errors空，7发送、14接收、原文气泡及投降完成。
- 原VS字母按原Mesh/三段Transform/UV/材质/Repeat+Bilinear+深度投影生成透明图，正确去除atlas红底及杂箭头，65来源/0错误。r8/r9正式开场原before→VERSUS→after→Keep/换牌，START与换牌并行，实际稳定截图和Listener均有；24Attack报告另覆盖11英雄before/after原播报名。完整原shader/light/VS_Shadow/mip/camera/controller/prefab动态不在静态几何/自主衔接完成声明内。

两独立V报告：[V023-opening-audio](V023-opening-audio.md)、[V023-rogue-performance](V023-rogue-performance.md)。Task0007/0008已自动登记本地有界DONE，Task0006完整官方视觉保持IN_PROGRESS。可复用结论在[CommonFX](../ORIGINAL_COMMONFX.md)，开放项在[KNOWN_ISSUES](../KNOWN_ISSUES.md)。下方为真实失败和执行历史，保留原状态，不覆盖旧raw/summary。

## 2026-10-27 总控实测进展（尚未整体验收）

r7综合构建exit0，DLL `2AA86218260723D05F5C6B3E401802200DBA3FC822A1D5B8217C238C5A0880BB`；`work/f023-rogue-weapons-shaman-r4` 已实际接受v18 power，双方在v18队列0、Busy/InputLocked=false、weapon76/durability2、hash收敛，driver唯一常规动作EndTurn且绿色。v19是driver真实concede，observer写出 `peer-result-ready.txt` 的同一v19 hash，证明其本地ResultReady已达；driver被回放play-only总门早退，没有记录v19，故run超时。这个false结果标志不能当作生产ResultReady=false；`cf100a4`只修终态回放dispatch/双方marker门，待下一Player完整退场。

r7 `work/f023-opening-priest-dh-r3` 已双端exit0、summary PASS，replay_harness_version2记录实际换牌可交互Repaint后下一Update提交合法Keep，authority进入play，START未播完时输入已开放；正常对局再捕七气泡。旧r2超时保留。VS atlas视觉问题仍排除在该功能PASS外；`6b6a119`已改原三维Mesh/Transform/UV/材质正面深度渲染，尚待shared exporter/新Player。root另实际查出原纹理WrapU/V=0（Repeat），V网格有UV>1，不能静默Clamp，继续小修。

r6 `work/f023-hero-attacks-all-r1` 实跑24 Player均自然exit0；11英雄各两端攻击 + Rogue合法武器对照，两端共48条观察对应24个真实权威Attack。旧summary保留seat误判/武器cue分类失败；83af7c0/81c457b/7e84849的纯分析器按真实席位与原武器源分组修后 `revalidation-analysis.json` 全24 PASS。独立V023逐原clip/PCM复核已记录：11基础英雄原声进入Listener，Rogue武器撞击为单独cue；无武器的+1攻击来自明示测试fixture，非经职业卡牌。15个映射clip缓存hash均核对，本轮每英雄实际选中_0，未声称全部随机候选动态覆盖。主记录见V023-opening-audio。

最新执行：r6综合构建自然exit0，DLL `59C4BA73163968D1AC2493031BFD0C331C0CE5B5E4FDA02EAECC8F12CCE50162`，固定源快照 `b92eab03ba453d597c9eb046edd5282a47392921`；Main自动ID再次仅恢复生成项并核baseline SHA。旧Presentation在 `work/f023-rogue-old-code-regression` 的真实源Editor测试中实证9秒永锁，因H03替换武器Death误依赖不存在的Arrival；`afff98c`只解除该Death依赖，r6新源回归PASS。该Editor的v17/v18快照是按关键事件形状构造，部分血量/hash/match_id自定义，不是原用户对局的完整捕获。Rogue/Shaman-r3仍未到v18：v17实际攻击已settled/hash一致，回放却错误要求第三次攻击播放武器摧毁音；`d6cf950`仅修该夹具，并强化Editor Death.fired/applied，待r7双端完整v19。

`work/f023-dh-quiet-r1` 实际双Player使用r6、无PNG而保留GUI/合法EndTurn/原声/Listener/hash，分析器 `1263c8f` 修正漏分流的PNG断言后完整PASS，原raw保留：driver首帧262.97ms、其余P50=4.238/P95=5.680/max=51.441ms；observer首帧262.94ms、其余P50=4.175/P95=4.930/max=82.786ms。cold prepare226/230ms仍未消除，不把构造器重复Supports去重说成首次卡顿彻底解决。间歇未归因慢帧和冷加载单独保留。

`work/f023-opening-priest-dh-r2` 在新布局/稳定VS捕图后180秒超时：新版回放等phase=play才开表情，但旧回放没有自动Keep，双方一直mulligan；正常用户Keep路径未改，C补回放专用合法Keep。稳定VS图另外暴露整张opaque atlas误绘（红方底和箭头杂图），必须按原mesh UV修，而非以首帧透明截图宣称完成。11英雄Attack取证进行中；Priest smoke两实际Player均exit0、Attack完成，但初版分析器误把Popen顺序当席位，实际两席反转，需按实际seat唯一性/同match/authority核对，不能改写raw或放宽语义。

新增音源回归实际执行：`work/f023-source-audio-r1` 到达17项SourceAudio断言并全通过，但下游链仍在NativePropertyChecks的粒子速度曲线检查FAIL、Unity exit1，完整链不宣PASS。保留原Run全链；新增RunSourceOnly只执行这17项并纳入r6聚合入口，r6该body和全聚合exit0可分别核对。不隐藏独立发现的历史失败。

更新：r5业务DLL为 `9BB3BDF2B7E54C1B4713C052FE0CB06EB5B2CC7273502AB22D32C22AE4AAA2E6`。r5包含 `b3e825f` 同龄H10快路径、`bbefa87` 共享音源staging frame/H10 Supports缓存及 `030ff70`。H09 r1经独立V023退修：克隆根从34000重锚24000但旧固定StagingOrigin仍34000，Start/Loop voice距Listener9995.7；原Start片段相关仅0.064。r5-r2实测两声源距Listener24.46，功能PASS，待V023波形复核；H04-r2也实际双端PASS。H10-r2热prepare中位0.118ms、Enabled检查近0ms、Draw中位0.704ms，后半Update中位4.27ms；首帧冷prepare189ms，随后7个约96–106ms帧与逐帧PNG捕获一一对应的强相关证据尚待quiet timing模式隔离，不能将这些采集开销直接算作生产热帧。Rogue/Shaman-r2走到v16显示/权威hash一致后，被夹具误读driver本地observer计数拦住（实际observer计数2、序列正确）；`18450d1` 加版本/hash绑定的对端完成marker，未放宽收敛条件。v17清理投降不是用例v19，用户原卡死根因仍Unknown。`c3e0dd8` 的450×500开场框/228×114菜单/稳定截图及 `e24bfd5` 的11英雄Attack夹具已合源，尚未在r5内，待r6构建。

- r4 综合 Unity/GPU/输入检查与 Windows Player 构建成功，日志 `work/f023-build-r4/unity.log`；业务 DLL SHA-256 `8209E1BE77964CBA0A0B5DD090C91219E12BB6063DBD63F753E0CBA952D864A6`。固定源快照 `122c0a4eddd01e3f87b3bf0145c1af02c4257ac0`。构建生成的 Main 场景 ID 已仅恢复自动变化，文件 SHA-256 回到 `D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51`。
- r1/r2/r3 构建失败保留在对应 `work/f023-build-r*/unity.log`：分别为检查器缺少 LINQ import、总控整合时误带工具输出截断文本、未初始化 ClientApp fixture 的 opening 空引用。已修复并逐文件对比 Feature blob；失败构建没有替换既有可执行文件。后续读取补丁先检查截断标记，长补丁分块核对，不能把截断输出作为源码。
- `work/f023-dh-profile-r1` 实际双 Player 功能、原声回调、合法后续 EndTurn 与最终 hash PASS；同时实测首次 Update 间隔 544ms、后续多帧 100–200ms。GUI/composite 通常小于 4ms，同帧两次 prepare-hot-seek 合计约 64–111ms，冷 prepare 343ms；性能问题仍 IN_PROGRESS，不能以功能通过或 0.2s 逻辑门宣称流畅。
- `work/f023-paladin-audio-r1`、`work/f023-priest-audio-r1` 双端实际回放 PASS。已修 staged 原生音源相对固定 StagingOrigin 的坐标映射；H04 声源从约 30000 单位移回距 Listener 约 22.8 单位（原 minDistance=100）。H09 覆盖选目标 Start/Loop 与满血 no-Heal。Listener 波形与听感尚由独立 V023 复核，不能只凭 playing=true 宣称可听。
- Rogue/Shaman r1 `work/f023-rogue-weapons-shaman-r1` 在首个阵容断言就 FAIL：Shaman 被夹具误断为 Mage，尚未执行武器序列。`030ff70` 修复断言，16 个定向 Python 测试通过；等待新 Player 二次实测，不把这次夹具失败当作用户卡死根因。
- 独立 V023 已派发，只读复核固定源、原声绑定、菜单布局与实际 Player/Listener 工件；开场与11英雄攻击仍待实际运行覆盖。任务未提前登记 DONE。

2026-10-26，延续TASK-0006/0007；开场对阵流程新增TASK-0008。保持F022实际构建与失败历史，不回退原实现。

## 用户本轮要求

- 圣骑士技能缺音，牧师点击后选目标阶段缺音。
- 盗贼使用首把技能武器攻击后再使用技能换第二把武器，双方不能操作；结束按钮颜色疑似未按可用操作更新，一端投降回菜单而另一端停在棋盘。先保存实际日志，再复现完整顺序，不先假设是规则冲突。
- 恶魔猎手技能播放卡顿，按实际帧开销追查，不仅缩短逻辑锁。
- 11基础英雄增加攻击原声。
- 右键七表情菜单改为头像两侧气泡，左3（感谢、称赞、问候）、右4（惊叹、失误、威胁、抱歉）。
- 加入对局开始VS动画与原播报，完成后抽牌/进入换牌，保留原开场英雄对白/气泡并正确排时。

## 编排约束

已实际检查原生collaboration可用、fork/read/worktree工具元数据存在；工作区插件两次内部错误，App附件查询两次Invalid app tool request，自动重试后以原生工具/Git工作树继续。GPT-6 Luna/max三独立Agent分别处理换武器卡死、技能音效/帧开销、英雄语音/菜单/开场。每人独立worktree；原安装只读，不执行原DLL，不使用Computer Use。共享local导入、Unity、Build、正式Player由root串行；不终止用户对局。

## 验收计划

先用真实Match动作复现武器攻击→同回合换武器→合法后续动作/结束回合→投降同步，核两端最终hash及队列/锁/源对象释放。音效必须对应原clip并在Listener中存在；DH必须测实际Update/渲染帧耗时。表情菜单按实际图片布局和同一输入路径验收，原99条功能保留；攻击声只在已接受英雄Attack播放一次。开局两个座位/先后手均按VS/播报→起手牌→换牌展示顺序，不能为动画改规则结算。执行结果和失败继续追加，不预先标通过。

## Rogue freeze 原始场次校正与复现基线

- 原场次 `74823703908b438985b949517ebee3f3` 是 seat0 Rogue (`HERO_03` / `HERO_03bp`) 对 seat1 Shaman (`HERO_02` / `HERO_02bp`)，不是 Mage。`-state.json` 两个视角均为 version 19、turn 7、phase `finished`、hash `f1d538a7990fc4ce26216c378fbd786ebe43c96d6bd19df6ee685d2a27a0f125`；结果为 `LOST/WON`。
- 原始 JSONL 每行保存 time/seat/command/error/version/hash；动作载荷含 type/kind/choice/action_id/match_id/entity/target/version/index/cards。v1–19 为：Shaman mulligan；Rogue mulligan；Rogue end turn；Shaman play entity 70；Shaman power 38；Shaman end turn；Rogue power 5；Rogue attack 4→71；Rogue end turn；Shaman power 38；Shaman end turn；Rogue attack 4→73；Rogue power 5；Rogue end turn；Shaman power 38；Shaman end turn；Rogue attack 4→75；Rogue power 5；Rogue concede。所有原始命令的 error 为空。
- 用 seed `634333382356097596`、classes `[7,8]` 和 preset decks 的规则回放命中相同实体编号，并依次召唤 Shaman 图腾 `CS2_050`/71、`NEW1_009`/73、`CS2_051`/75。该回放只用于规则与输入顺序夹具；不同 match identity/deck metadata 下 hash 不作为与原件相等的断言。
- 不应把两次 Rogue 后续技能都描述成“同回合再次使用”。原动作先在 v7 装备 entity 72，v8 攻击后 durability 2→1，v9–11 经过 Shaman 回合；v12 攻击摧毁空槽前的 entity 72，v13 在同一刷新后的 Rogue 回合合法装备 entity 74；v14–16 再经过 Shaman 回合；v17 entity 74 durability 2→1，v18 在仍持有 1 durability 武器时合法使用技能，authority 事件为 `SpendMana → PlayHeroPower → Destroy(74) → Death(74) → Summon(76)`；v19 在 Rogue 当前回合投降。两种换装情况必须分开验证：v13 空槽 equip 和 v18 非空槽替换。
- `player1.log` / `player2.log` 中的 `Native source owner expired while busy 210ebc0912fb4c68be46d9e00a417bcd:7:38:0` 属于另一场 `210e...`（Warlock 对 Druid），不是 `748...` 的 Rogue 行为。两份日志中的原 Rogue 武器声音记录带有 `748...` key：v8 slot 7，v12 slot 5 与 slot 7，v17 slot 7。不要用同文件内 `210e` 的 owner-expired 告警证明 748 根因。
- 原 JSONL/state 证明服务器接受至 v19 并形成相同 finished hash；它们不包含两客户端 Presentation 队列、`InputLocked`、按钮 tint 或 `BattleLifecycle.ResultReady`。截图的一端主菜单、一端留在棋盘，仍需用双 Player harness 记录 v18 操作开放/绿色结束按钮、v19 后两端 ResultReady、显示 hash 与资源/队列状态，才能定位客户端最早分叉。结果生命周期原因目前 **Unknown**，没有据原始 authority 日志宣称已复现或修复。

### Rogue/Shaman 双端 Player 复现入口

- `--rogue-weapons-shaman` 使用 seed `634333382356097596`、classes `[7,8]`，仅让规则服务器预装到 v6：Rogue 当前回合、Shaman entity 71 `CS2_050`、双方 mulligan 与 v1–6 动作已核对。v7–19 必须由两个真实 Player 继续输入；规则单测也另外走完 v19。
- Rogue Player 从真实位置点击 v7 power、v8 攻击 entity 71、v9 结束；Shaman Player 在 v10 power 后真实结束，再在 v15 power 后真实结束；Rogue 在 v12 攻击 entity 73、v13 空槽装备，再于 v17 攻击 entity 75，v18 对 1 durability entity 74 做非空槽替换并投降。Harness 验证替换事件链各自独立：v13 `SpendMana → PlayHeroPower → Summon`，v18 `SpendMana → PlayHeroPower → Destroy → Death → Summon`。
- v18 先等待 Shaman observer 看见相同 authority hash，再记录 `ClientApp.EndTurnContent` 使用的 ready/`BattleInteraction.NoActions` 绿态条件和唯一合法常规 move `end_turn`，然后才从原 Rogue 回合提交 concede。双方各写自己的 `ResultReady` marker 并交换同一个 v19 hash 后，才允许离开棋盘；observer 也实际执行 v10/v15 Shaman power 和 end-turn。
- 在已合入该分支并完成 Player 构建的仓库根目录运行：`& 'E:\HS\HearthstoneClone\.venv\Scripts\python.exe' -X utf8 -m tools.fireblast_evidence --run-players --rogue-weapons-shaman --size 1920x1080 --output work/f023-rogue-weapons-shaman-r1`，之后运行相同命令并将 `--run-players` 换成 `--analyze`。旧 `--rogue-weapons` Rogue-vs-Mage 控制模式仍保留，classes 为 `[7,4]`。
- 当前仅做 Python 规则及 analyzer/runner 合同验证：`pytest tests/test_rogue_player_replay_contract.py tests/test_rogue_weapon_replacement_flow.py -q` 为 15 passed，`py_compile` 与 `git diff --check` 通过。此分支尚未启动 Unity、构建 Player 或运行双端 Player；UI 最早分叉、实际按钮颜色、双方 `ResultReady` 及修复结论仍待该入口的真实运行结果。

## F023-C：英雄攻击、七气泡菜单、对阵开场实现

实现提交前静态结果：

- 11基础英雄 Attack 资源由 CardDef 原 `m_AttackEffectDef` 导出；主线程接线在 `ClientApp.PlayPresentationAudio`，只在已接受语义 `Attack` 且 card id 为 `database.classes[].hero` 时排队播放。该调用位于 `NativeBattleWeapons.TryHandle` 的提前返回之前，保留已接受英雄 Attack 的一次原声与武器撞击各自播放；随从 Attack 不进入 Hero 语音分支。已提交实现为 `20f7116`。
- 七气泡菜单使用原 atlas 下半部气泡的正确朝向绘制右列；两侧各自位于己方英雄周围，命中范围和原图 alpha 主体分别断言不交叠，并检查 1920×1080、1440×1080、1280×720。helper 单独提交为 `cbac120`。
- 原播报调用顺序已由静态 Cecil 闭环：`MulliganManager.HandleGameStart` 明确加载 `GameStart_VS_Letters` 与 `VO_ANNOUNCER_VERSUS_21`；`<WaitForHeroesAndStartAnimations>d__158::MoveNext` 的顺序为友方 `BEFORE_VERSUS` → `versusVo` → 对方 `AFTER_VERSUS`，每条通过 `SoundManager` 播放并等待完成。`GetAnnouncerLine(Card, AnnouncerLineType)` 根据类型取 CardDef 的 before/after 路径。GAMEPLAY_AUDIO 无对应字幕键，所以不靠字幕键或资源名猜测。通用 versus 导出器按确认的 prefab GUID 遍历 `SoundDef`，直接解析 primary `m_AudioClip` fallback 与全部 `m_RandomClips` 候选。只读静态 IL 表明 `LoadClipForPlayback` 先调用 `GetRandomClipFromDef`，按 `RandomAudioClip.m_Weight` 与 `UnityEngine.Random.Range(0,total)` 加权择一，只有随机选择返回空时才回退到 `m_AudioClip`；自主客户端沿同一候选权重策略单次播放一个 clip，并在Player时间线记录实际选择的clip ID。缺失引用、权重无效或来源不完整仍 fail closed，不查询同名 AudioClip。
- 新 `BattleOpening` 使用 Registry 中的三个原播报来源和 `UI_VS_LETTERS`，淡入重建的左右英雄开场图、姓名、职业标签和 VS overlay。职业标签只显示职业名，昵称保留在既有画面位置。三段播报完成后启动 F022 `START` 原声；VS 短暂淡出后释放换牌输入，不等待 START 音频结束。START 气泡在正常棋盘继续绘制，并按原语音时长自然过期。时间由现有 `visualFrameDelta` 驱动；服务端不等待动画。
- 原 `GameStart_VS_Letters` prefab 仅标 source-only，不宣称自主Client复用了原动画/Actor；完整官方视觉timing、听感与Speaker/Listener证据未知。`HeroSpeechChecks.Run` 已增加三段原声和VS素材manifest、source-only prefab、布局矩形/分辨率断言，等待root在串行构建前生成素材并运行。

总控验证入口：

```powershell
E:\HS\HearthstoneClone\.venv\Scripts\python.exe -X utf8 -m tools.export_voices --base-hero-attacks --base-hero-announcer
E:\HS\HearthstoneClone\.venv\Scripts\python.exe -X utf8 -m tools.presentation_assets
```

本Feature没有运行上述Importer、Unity、Player或测试。新增实际 Player 证据入口复用 `HeroSpeechReplay`，逐帧记录 BattleOpening 阶段、原播报/START clip ID 与时间戳，并自动保存 VS、换牌显示、换牌输入三个截图；Listener PCM 继续输出为 `.listener.wav`。root在构建完成后可运行：

```powershell
E:\HS\HearthstoneClone\.venv\Scripts\python.exe -X utf8 -m tools.hero_speech_player --emotes THANKS --class-a 4 --class-b 10 --timeout 180 --output-dir E:\HS\HearthstoneClone\work\f023-opening-player
```

实际音频/帧、两座位行为和严格来源映射仍待root串行验证；任务状态继续为 `IN_PROGRESS`。
