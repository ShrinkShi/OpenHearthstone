# V023 开场与原生音频独立核验

**状态：本地有界 PASS。** Task 0007 的 11 英雄 Attack 原声和七项菜单、Task 0008 的 VS/播报/换牌本地流程均有固定源、构建及双端 Player 证据。r6 的 atlas 误绘、r8 的菜单行错位均保留为历史 FAIL，r9 已由实际截图复核修复。此结论不覆盖官方 prefab 动画/shader/灯光，也不把 Listener PCM 当作扬声器或人耳听音。

## 基线与输入

- 固定源码快照：`refs/verification/f023-r4`，`122c0a4eddd01e3f87b3bf0145c1af02c4257ac0`。
- 固定构建：`work/f023-build-r4/unity.log`；关联 DLL SHA256：`8209E1BE77964CBA0A0B5DD090C91219E12BB6063DBD63F753E0CBA952D864A6`。
- r5 后续音频回归使用 DLL SHA256 `9BB3BDF2B7E54C1B4713C052FE0CB06EB5B2CC7273502AB22D32C22AE4AAA2E6`；不能把它与 r4 基线混作同一个二进制。
- r6 固定源码快照：`refs/verification/f023-r6`，`b92eab03ba453d597c9eb046edd5282a47392921`；`work/f023-build-r6/unity.log` 成功退出，DLL SHA256 `59C4BA73163968D1AC2493031BFD0C331C0CE5B5E4FDA02EAECC8F12CCE50162`。
- r8 固定源码快照：`refs/verification/f023-r8`，`95fe4fdd63dd4224f8b33c904c6fed9d23b5018e`；`work/f023-build-r8/unity.log` 以 return code 0 成功退出，DLL SHA256 `11ACF337669C61729269CC66C8FF2D1BEC69D9CDD49759D6E656F4078711832D`。
- r9 固定源码快照：`refs/verification/f023-r9`，`60fd1b8b6717a0a66d92474d8741e9b325eae03e`；`work/f023-build-r9/unity.log` 以 return code 0 成功退出，DLL SHA256 `496937AFCF0D5897251656D2BC45AACA9DFBB02FAAB75B017E6116A041D673B8`。相对 r8 的功能源改动只有 `HeroEmote.cs` 和 `HeroSpeechChecks.cs`；VS 派生资源及 `BattleOpening`/播报实现未改。日志中的 `HERO SPEECH CHECKS passed` 覆盖 11 英雄来源和菜单几何断言。
- 只读检查现有源、manifest、报告、截图和 Listener PCM；没有运行 Unity、Player、Importer 或改共享缓存。输出设备回环录音不可用，Listener PCM 相关性只证明声音进入引擎 Listener 混音，不证明物理扬声器或人耳听到。

## VS 开场与原播报绑定

固定源 `BattleOpening.PlaySequence` 的顺序为己方 `ANNOUNCER_BEFORE_VERSUS`、`GLOBAL:VERSUS`、对方 `ANNOUNCER_AFTER_VERSUS`，再启动两名英雄的 START 台词；START 可与 VS 淡出/换牌交互并行。最终 `work/f023-opening-priest-dh-r5` 两份报告均记录完整顺序：A 为 H09 → VERSUS `_0` → H10，B 为 H10 → VERSUS `_2` → H09，随后都记录 H10/H09 START。两端 `mulligan_interactable_before_start_complete=true`，Replay Keep 在 Repaint 后提交并由下一 Update 接受，阶段变为 `play`；不要求 START 全播完才开放输入。双端 summary 均 `passed=true`、自然 exit 0、无 errors。

manifest 将 11 个基础英雄的 before/after 都映射到原 `CardDef.m_AnnouncerLinePath`，两项均标 `fallback_used=true`；这是复用原始卡牌语音字段，不宣称有 11 组独立的专用 before/after clip。`work/f023-hero-attacks-all-r1` 的 24 个真实 Player 报告中，48 条 before/after 记录全与 manifest 逐项匹配，实际都选 `_0`；11 个导出 WAV 的 SHA256 均与来源 manifest 相符。`VERSUS` 按原 `SoundDef.m_RandomClips` 三个等权候选选择；不能把任何一条候选称为固定的官方主音轨。`GameStart_VS_Letters` prefab 在 provenance 中仍为 `source_only`，当前 VS 展示是依据原素材重建行为，不宣称官方 prefab 动画或布局 1:1。

下表按 `CardVisuals.classes[class_id]` 对上 11 个职业；before/after 两个 event-specific Voice ID 均回退到同一行的原始 clip 和 WAV。原始 reference、CAB、PathID、asset ID、完整哈希均在 `manifests/original-hero-announcer-opening.json`。

| 职业 | class_id | hero card | before/after 使用的原始 clip | 导出 WAV |
|---|---:|---|---|---|
| 死亡骑士 | 1 | `HERO_11` | `VO_Innkeeper_Male_Dwarf_HERONAME_LICHKING_01` | `bf511632694c69a426115dc7.wav` |
| 德鲁伊 | 2 | `HERO_06` | `VO_ANNOUNCER_MALFURION_14` | `21c5b66b943746a9e7cb6978.wav` |
| 猎人 | 3 | `HERO_05` | `VO_ANNOUNCER_REXXAR_08` | `dc9849014383acb18aaea179.wav` |
| 法师 | 4 | `HERO_08` | `VO_ANNOUNCER_JAINA_06` | `cbc7014ac702af433f6af4d3.wav` |
| 圣骑士 | 5 | `HERO_04` | `VO_ANNOUNCER_UTHER_10` | `2e5915f9e278ed03ffa1a3ed.wav` |
| 牧师 | 6 | `HERO_09` | `VO_ANNOUNCER_ANDUIN_12` | `2c978f0bca838ecd4e712f8e.wav` |
| 盗贼 | 7 | `HERO_03` | `VO_ANNOUNCER_VALEERA_07` | `1d46d9c9824a9c3f66e4605b.wav` |
| 萨满 | 8 | `HERO_02` | `VO_ANNOUNCER_THRALL_11` | `48e43eb3a28e4cbd4167767e.wav` |
| 术士 | 9 | `HERO_07` | `VO_ANNOUNCER_GUL'DAN_13` | `e2640694bdf0077f455a764c.wav` |
| 战士 | 10 | `HERO_01` | `VO_ANNOUNCER_GARROSH_09` | `ccd6413dc1857ec08c7ad3d9.wav` |
| 恶魔猎手 | 14 | `HERO_10` | `VO_Innkeeper_Male_Dwarf_HERONAME_ILLIDAN_01` | `c1c9e201a9c93c2af6d948f8.wav` |

同一 24 Player Attack 工件的 72 条 announcer 记录（每份报告 before / VERSUS / after 各一条）全部按此顺序出现；每份 `opening_phases` 都包含 `complete`。HERO_11 两份报告在最终 `cleared` 后 `opening_complete` 布尔字段为 false，但其事件列表仍有 `versus_complete`、`hero_start`、`complete`，不据末态布尔值否认播报序列。24 次 VERSUS 选择覆盖全部三个 runtime ID：`_0=4`、`_1=12`、`_2=8`，源权重均为 `1.0`；这只是候选覆盖，不是随机分布统计验证。manifest 中 `_0` 与 `_2` 指向同一导出文件/哈希，`_1` 是另一 WAV；因此三种 source reference/runtime ID 不等于三种独立 PCM 内容。原 fallback ID 未在这 24 次中观察到。

r1 Listener PCM 与注册的原始 WAV 逐段相关，能将播报关联到各自来源。A 端 HERO_09 / VERSUS / HERO_10 的最佳归一化相关系数分别为 `.9996 / .9371 / .9552`；B 端 HERO_10 / VERSUS / HERO_09 为 `.9993 / .9538 / .9665`。START H09/H10 在 A 端为 `.9464 / .9178`，B 端为 `.9464 / .9122`。这是 Listener 混音中的源匹配证据，不是设备回环听音测试。

r1 VS 捕获不能作视觉 PASS：`VersusDrawn` 在第一次 Repaint 即置位，报告首帧约在开场年龄 `.166s`，而淡入时长是 `.25s`，截图因此接近透明。r1 的开场矩形是己方 `380×370`、对方 `380×350`；这一帧本身无法判断实际比例。后续 r6 已使用更大的矩形并有稳定帧，尺寸比较见下文；不作官方 1:1 判断。

后续 r6 源把英雄矩形扩大到己方/对方均为 `450×500`。在 `work/f023-opening-priest-dh-r2/captures/*-battle-opening-vs.png` 中，稳定帧相对开场时刻分别约 `.289s`、`.288s`，已超过 `.25s` 淡入，适合做视觉判定。两端都显示整块深红方形底、VS 字样以及左下角箭头/杂块，故这是确定的视觉 FAIL，不是首帧透明导致的误判。r6 `BattleOpening.Draw` 对 `UI_VS_LETTERS` 完整纹理调用 `GUI.DrawTexture`，没有 UV 裁切或字形遮罩；对应原纹理 `local/runtime/bfc3fe8a6ef5ea5aafdbaf74.png` 为 256×256 sRGBA，alpha 最小/最大/平均值均为 1，整张图不透明。

r6 放大框在截图中已接近参考比例：按截图边缘粗略量得己方约 `380×435px`、对方约 `390×444px`；用户参考约 `400×450px`、`410×460px`。高度约小 3–4%，宽度约小 5–7%，不支持先前估计的 20–25% 尺寸差；当前不应再以“大幅放大”作为主要修复方向。该比较是截图像素估量，不构成官方 1:1 声明。

r8 修正的是 logo 纹理来源，不是原 prefab 运行时：`manifests/presentation-assets.json` 有 65 个 asset、`errors=[]`。`UI_VS_LETTERS_geometry.png` 为 `435×480`、SHA256 `0160da00d509eb15086897b03fd2e06eedd8ec3226044fb626fa1d1a7a4235e3`；逐像素检查有 103,733 个全透明像素和 9,653 个半透明像素，图像呈 V/S 字形而不是不透明 atlas 方块。manifest 的 `alpha_mask_bounds_px` 覆盖全画布，只表示 alpha 包围框触边，不代表整块不透明。

r8 来源链回到原 `GameStart_VS_Letters` prefab：V/S 的原 Mesh、UV0、MeshFilter、MeshRenderer、共用材质主纹理、两条字形 Transform 和共享 group/root Transform 均有 source-only 身份及引用闭环。源网格共 1,372 个三角形，其中 962 个朝 +Y 的正面三角形参与 `depth_tested_orthographic_world_xz_from_positive_y` 投影。素材采样记录为 Bilinear (`m_FilterMode=1`)、aniso 1、mip bias 0、U/V/W Repeat (`m_Wrap*=0`)；V 网格 UV0 的最大 V 为 `1.00384`，r8 以 Repeat 采样处理越界。Unity 日志中的 `HERO SPEECH CHECKS passed` 与 manifest 断言覆盖该来源图和几何计数。旧 `work/f023-vs-uv-union-r1` 是失败路线：直接并集网格 UV 仍导出整张 atlas，不将其作为修复证据。

这只复现静态可见字形几何及 base-level 双线性 Repeat 采样。原 prefab 仍标为 `source_only`；未复现 `VS_Shadow`、原 shader/灯光、mip 选择、相机、controller 或 prefab 动画。r8 的 `work/f023-opening-priest-dh-r4` 与 r9 的 `work/f023-opening-priest-dh-r5` 两端稳定 VS 捕获均显示完整 V/S 字形、透明周围且没有 atlas 红底/箭头杂块；r9 报告年龄约 `.3001s`，已过 `.25s` 淡入。截图证明 r9 Player 实际使用的重建结果，没有证明复原官方 VS prefab。

## 七选项菜单

固定选项顺序为左侧三项 `THANKS / WELL_PLAYED / GREETINGS`、右侧四项 `WOW / OOPS / THREATEN / SORRY`。左侧使用 `upper_right_tail`，右侧使用 `lower_left_tail`，尾尖都朝向英雄。r9 使用 228×114 图像槽；前三行以相同垂直锚点对齐，右侧第四项接在第三项下方，再对整组做视口内平移。文字命中区与透明图像槽分开计算，几何断言比较 alpha artwork body 和 hit rect。

用户参考图中英雄约 `188px` 宽、气泡 alpha 主体约 `180–188px` 宽，比例约 `1.0`。原 atlas 为 512×512；每个半区的主体 alpha 框约 `403×188px`。r1 的 `184×92` 图像矩形对应主体约 `145×68px`、相对约 `170px` 英雄宽偏小；228×114 槽的主体约 `179×84px`、比例约 `1.05`，接近参考。r9 两端实际菜单截图确认气泡主体尺寸与该推算一致；截图与用户参考分辨率/宽高比不同，不作 1:1 声明。

r8 的 `work/f023-opening-priest-dh-r4` 双端流程完成，但菜单截图仍有真实布局缺陷：右侧首个 WOW 比左侧首个 THANKS 高约 58px。r9 将前三行改成同 y 锚点，保留原气泡 atlas、尾向、228×114 图像槽和文字区；Editor 断言改为检查不透明 Artwork 与 hit rect 不互叠、前三行对齐、右侧第四项不压第三项，并覆盖 1920×1080、1440×1080、1280×720。实际 r9 `work/f023-opening-priest-dh-r5` 两张菜单截图显示首三行水平对齐、左三右四、右第四项在第三项下方且未压住它；左右尾向正确。两端报告均为 `menu_opened=true`，Keep 在 `play` 接受后才捕获菜单，`menu_capture_after_start_bubble_expired=true`，requested/sent 顺序均为七项，双方各收到 14 个表情广播；A 发送 concede、B 接收，双方 `result_seen=true`。菜单是 replay harness 的真实绘制/交互路径证据，不是人工物理鼠标测试。r6 的 `work/f023-opening-priest-dh-r2` 超时和 r8 的行错位分别保留为历史结果；不能因最终 r9 PASS 改写旧工件。

## H04 / H09 原生音频与 Listener 证据

- **H04 r1：Start 有直接 Listener 证据。** `work/f023-paladin-audio-r1/driver/report.json` 记录 spatial=1、volume=1，声源到 Listener 距离约 `22.8235`，source min/max distance 为 `100/500`。原始 Start WAV `0b172af97e533a48568c40c7.wav` 的 `.20–.44s` 片段在 Listener PCM 中最佳相关系数约 `.7912/.7906`（L/R），偏移约 `+0.67ms`。原 loop 是另一 WAV `b248ece18bdc57738255ba2f.wav`；预期窗口相关性约 `.081`，没有足够直接证据单独确认 loop 可闻。故只判 Start 进入 Listener 混音，不能把整个 Start+Loop 宣称为听音 PASS。
- **H04 r2：再次确认 Start 进入 Listener 混音。** `work/f023-paladin-audio-r2/driver/report.json` 中事件时间为 `18.777136s`，距离仍约 `22.8235`、spatial=1、min/max=`100/500`。对 `scene-mix.wav` 与原始 Start WAV `.20–.44s` 片段做独立相关，L/R 峰值约 `.93069/.92987`，相对预期偏 `+8.30ms`。r2 同时记录另一原 loop 来源，但此处不据此扩展 Loop 可闻结论。
- **H09 r1：真实音频/空间 FAIL，必须保留。** `work/f023-priest-audio-r1/driver/report.json` 中 Start/Loop 虽 `playing=true`、voice 存在，但声源距 Listener 约 `9995.701`，超过 max distance `500`；原始 Start/Loop 在预期窗口相关性约 `.064/.049`。原因与 staging root 重定位后仍用旧 `StagingOrigin` 一致。`playing=true` 不能代替可闻证据。
- **H09 r2：Start 的物理位置及 Listener 源匹配通过；Loop 可闻性仍未证实。** `work/f023-priest-audio-r2` 使用 r5 DLL。Start 距 Listener 约 `24.457`，spatial=1，min/max=`100/500`；原 Start WAV `.20–.44s` 在 Listener PCM 的相关系数约 `.894`，比预期段晚约 `4.3ms`。Loop 是另一原始 WAV `b248ece18bdc57738255ba2f.wav`，min/max=`1/500`，与 Start 重叠；原 loop 片段相关性约 `.061`，扣除主 Start 后尾段约 `.026`。所以 r2 证明 Start 进入 Listener 混音、修正后距离合理；不能由 `playing=true`、总 RMS 或 Start 的相关性推断 Loop 已听到。r2 的 full-health-no-heal 双端功能 PASS 不改变这个音频结论。

## 英雄 Attack 原声映射与 Listener 核验

`manifests/original-hero-attack-voices.json` 包含 11 个基础英雄、15 条原始 `ATTACK` 映射；每条都有来源 reference/CAB/PathID，15 个导出文件均存在且 SHA256 与 manifest 相符（其中 HERO_10 的 attack_1/attack_2 共用同一 WAV 文件）。本轮 Player 报告实际选中的是每个英雄的 `_0`；其余候选有来源映射，但没有声称本轮都播放过。

`work/f023-hero-attacks-all-r1/summary.json` 记录 12 个镜像对局、24 个 Player 均以 exit code 0 退出：11 个无武器英雄对局和一个 Rogue 合法 `CS2_082` 武器对照。旧 `summary.json` 的初始 `analysis.passed=false` 保留为历史结果：它把启动顺序当成实际 seat，并把 Rogue 的武器 cue 错分到无武器攻击。独立 `revalidation-analysis.json` 按各报告真实 `seat` 复核，24 条结果均无 errors；12 个 match 各有 seat 0/1 两份报告。24 份报告各有两个攻击事件，48 条报告记录中的每条 `accepted_count=1`、`voice_start_count=1`，并且 `duplicate_attack_callbacks=0`。每个真实事件被两个客户端分别记录，故 48 条记录对应 24 个真实事件，不是 48 次攻击。

我将每份 Listener PCM 的起点用该报告首段原始开场播报对齐；24 份对齐相关系数为 `.99885–.99972`。对 11 个无武器对局，每个 Voice ID 统一使用一个 250ms 原声片段：从原声自身 RMS 较高的 12 个候选窗口中，选出在该英雄的两个攻击事件、两端 Listener 和左右声道上最低相关仍最高的窗口。表中是这些匹配的最小相关系数幅值；峰值最迟偏移不超过约 `26ms`。这是 Listener 混音中的原声匹配，不是扬声器回环录音；由于候选窗口用混音相关择优，系数只用于来源匹配边界，不作为独立音质评分。

| 英雄 | 本轮实际选中的 Voice ID | 最小 `|r|` |
|---|---|---:|
| HERO_01 | `VOICE_HERO_01_ATTACK_0` | .720 |
| HERO_02 | `VOICE_HERO_02_ATTACK_0` | .831 |
| HERO_03 | `VOICE_HERO_03_ATTACK_0` | .594 |
| HERO_04 | `VOICE_HERO_04_ATTACK_0` | .555 |
| HERO_05 | `VOICE_HERO_05_ATTACK_0` | .739 |
| HERO_06 | `VOICE_HERO_06_ATTACK_0` | .679 |
| HERO_07 | `VOICE_HERO_07_ATTACK_0` | .613 |
| HERO_08 | `VOICE_HERO_08_ATTACK_0` | .598 |
| HERO_09 | `VOICE_HERO_09_ATTACK_0` | .563 |
| HERO_10 | `VOICE_HERO_10_ATTACK_0` | .802 |
| HERO_11 | `VOICE_HERO_11_ATTACK_0` | .777 |

Rogue 对照的两个 Attack 事件都报告 `weapon_card_id=CS2_082`；该 Listener 相关性较低但仍在预期事件窗口出现（两端共用来源片段的最小 `|r|=.424`，最大偏移约 `22ms`）。两端各记录两次 slot 7；原 manifest 将 slot 7 映射到 `Card_Play_Weapon_Damage.prefab` 的 `Weapon_Durability_Hit_01.wav`，CAB `CAB-c491983cea83897756f11fa097177509`、PathID `4227194628362892406`、导出文件 `508d69647c76de93961e372c.wav`。每端各有一次 slot 41 和 slot 42；它们是武器抽出 cue，不是攻击/碰撞 cue。11 个无武器对局的 `weapon_audio` 均为空。攻击测试使用 mulligan 后临时 `+1 hero attack` fixture，不代表 11 个英雄能在生产规则下无武器自然攻击。

## SourceAudio 合同检查

`work/f023-source-audio-r1/unity.log` 明确打印 `NATIVE SOURCE AUDIO: 17 checks passed`，覆盖 staging frame 重锚、隐藏父物体不因 `activeInHierarchy` 停声、源节点自身 `activeSelf=false` 停声，以及外部 Actor 不被错误 remap/跟踪/停止。之后同一 r1 完整入口继续跑 `NativePlaythroughChecks`，在下游 `NativePropertyChecks` 因 `authored float curve reaches actual particle playback speed` 异常退出，故该 Unity 调用整体 FAIL，不能记作整链通过。

r6 构建日志明确打印 `NATIVE SOURCE AUDIO: 17 checks passed`，即 `RunSourceOnly` 已在综合构建中实际执行并通过。先前完整 `NativeSourceAudioChecks.Run` 的 r1 结果仍是后续 Property 检查失败；r6 这 17 项通过不抹掉或重标旧的整链 FAIL。

## 结论与边界

Task 0007 的本地范围有界 PASS：11 个英雄 Attack 的原始 Voice ID/WAV 映射有 manifest 与哈希证据，24 个真实 Player 报告在双席分别记录一次 accepted Attack 与一次原声启动，Listener PCM 与候选原声可相关；七项表情在 r9 两端真实绘制、广播并完成菜单布局复核。Task 0008 的本地范围有界 PASS：固定 r9 构建的两端 Player 自然退出 0，按原顺序执行开场播报、VS、START 与换牌交互，Keep 在 Repaint 后提交并由下一 Update 接受，之后 `phase=play`，菜单、concede 和 result 均完成；稳定 VS 和菜单截图与报告一致。

PASS 仅指上述固定源与构建下的本地行为及 Listener 混音证据。r9 的完整来源身份是 `refs/verification/f023-r9`（`60fd1b8b6717a0a66d92474d8741e9b325eae03e`）、`work/f023-build-r9`（DLL SHA256 `496937AFCF0D5897251656D2BC45AACA9DFBB02FAAB75B017E6116A041D673B8`）和 `work/f023-opening-priest-dh-r5`。没有物理扬声器/人耳回环证据；H04/H09 Loop 的来源独立可闻性仍未确认，H09 r1 的空间音频 FAIL 继续保留。没有宣称恢复官方 prefab 动画、shader、灯光、controller、相机或 1:1 视觉；VS 为依原始几何与纹理采样重建的静态展示，菜单/换牌截图来自 replay harness 路径。HERO_11 Attack 对局的终态 `opening_complete=false` 与下游 `NativeSourceAudioChecks.Run` 的 Property FAIL 均保留为报告中的明确边界，不被最终本地 PASS 覆盖。
