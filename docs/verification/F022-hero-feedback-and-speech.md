# F022 职业技能二次反馈与英雄语音

2026-10-25，TASK-0006继续IN_PROGRESS；新增语音/表情单独登记TASK-0007。

## 用户本轮验收要求
- 战士近似瞬发，取消长操作锁；恶魔猎手稍快、无锁或最多约0.2秒短锁。
- 死亡骑士结束与操作锁过长，修对方视角左下多余错位效果；保留按钮/真实召唤点双层。
- 猎人爆炸音仍听不到：排查实际voice增益/位置/时序/遮挡和混音，不以回调/相关性代替用户可听。
- 德鲁伊特效略缩小；圣骑士头像效果与原参考不符，核查来源与渲染；牧师满血也要目标Impact、无+0；术士补完整前中后段。
- 萨满和盗贼本轮不改技能表现。
- 开场/投降及右键七表情的英雄原声与气泡，感谢/称赞/问候/惊叹/失误/威胁/抱歉并存，见TASK-0007。

## 编排与验证
已实际检查原生subagent、fork/read与worktree工具元数据；原生subagent可用。App list_artifacts返回Invalid app tool request，重试后若仍失败使用本机Git独立worktree。不使用桌面自动化，不中断用户游戏；主工作区既有脏改动保留。后续实现/失败/验证追加本记录，未完成项不标通过。

## 初步已证实来源与接口（不是完成声明）

- 输入锁：H01/H10没有resolved Hit，仍被Presentation的NativeActive通用门持续锁到source结束；H11还把5秒cue纳入当前batch。需要将短输入门、权威显示推进与可见source清理分开，并实际提交后续合法动作。
- H09真实authority fixture：满血合法治疗扣费/用技但无Heal事件；SpendMana/PlayHeroPower也不含所选目标。因此服务端成功HERO_09bp后追加独立`HeroPowerTarget`语义信息，客户端以其权威目标播放基础Impact；真实Heal继续负责血量和+2，不伪造0Heal。
- 11个基础英雄各有9项原EmoteDef：type1–8和33，包含独立Sorry和Wow；原字幕来自`Strings/zhCN/GAMEPLAY_AUDIO.txt`，菜单标签来自`GAMEPLAY.txt`，各项引用可解析zhcn AudioClip。原Emote_Bubbles贴图512²已找到；原Actor绑定尚未证明，不冒称完整原Actor恢复。
- speech独立wire设计：server从已匹配peer/seat推导英雄，七项白名单、request_id有界去重和每席限频，专用ack/error，表情不改规则version/public hash/legal moves；显式concede与断开连接分别处理。临时端口协议/规则6项测试由执行者通过，尚无Unity或正式Player证据。

## 集成中的真实发现与失败记录

- 已整合三条独立 worktree 的变更；首组协议/目标/回放合同 76 项、第二组 importer/native/回放合同 103 项通过。原 H02/H03 root 文件 SHA-256 仍为 `94e20dbaaa9c4b2ed02f19e7e25ffb4b8dd01ea29b745fd3179a2a6235b82b04` / `4e2370690fbc2a0c8961b933f05ae9340b97bc33cf4911991a1ab9d058760fe9`。
- 语音正常 exporter 已导出 11 英雄、99 条、99 候选、errors=[]；H04 正常重导、H07 16 个旧版 shape.radius 常量包装无损迁移后重导。未改原版安装或 H02/H03 的技能资源。
- DK Node17 原 FSM 会 `SetParent(None)` 后使用原世界坐标。原主 `PresentationCamera` 能看见离屏专用 layer30，导致原节点直接入主画面，同时又分别合成到技能和召唤点。现主相机排除30，离屏相机保留30；待实际双方截图确认。
- H04 原 `OffScreenRenderFX` 排除 layer21；导出器此前丢失节点层，使原白色 `Card_Hero` producer 平面也进入最终效果。现保留 `source_layer=21`，producer 仍存在，最终捕获仅看 layer30；待 GPU/Player 确认硬色块消失。
- H05 原 `Card_Play_Hero_DamageSplat` 的 Health 音频动作使用原 root AudioSource 与 `Audio_Container/SFX_BloodSplatHealth` 的 `Blood_Splat_Damage.wav`，原固定随机音量 .75。它与通用 `FX_Minion_AttackImpact`、箭头 `Explode` 是独立层；此前少了这一层。正在补接真实原声音源，不用全局增益掩盖漏层。
- Unity r1 编译失败：两个回放入口分别误用 `Event(EventType)` 构造器和被局部变量遮蔽的 `source.projections`。修为 Event initializer 与 `this.source.projections`。
- Unity r2/r3 的 H09 新检查先后发现两个 fixture 问题：只 Seek 一次无法跨原 Start→Impact 的下一帧调度边界；旧断言仍要求 cue.target=0，与本轮权威目标20冲突。已加入1.01/1.02两次采样，并保持 Heal 唯一且由服务端负责的条件。原业务来源门未放松。r1-r3 失败日志保留在 `work/f022-build-r*/unity.log`；不将它们计作构建通过。
- Unity r4 的 H10 检查在故意倒放后仍持有已释放的旧 playback 引用；重新从生产 player 取得当前实例后，r5 的对应来源检查通过。生产1.15倍时钟未改。
- r5 菜单检查失败；独立 Editor r1 记录实际 `Event.type=Ignore`，而 button=1、Contains=true、match 均正常。Unity 在非 OnGUI 上下文会规范化新建鼠标事件。现真实 Event.current adapter 和 Replay 都调用同一套明确字段的处理逻辑；独立 Editor r2 exit0，七项选择、外点、Escape、99条数据检查通过。
- 独立协议审查找到并复现：seat0 投降离开后 room 长度变1，而剩下 peer.seat 仍1，旧 room[seat] 抛 IndexError。现只验证当前成员和服务端座位；独立临时TCP重放收到专用 hero_emote_error，无额外state/error、规则版本不变。回归已加入真实TCP测试（6项通过）。
- H11 空尾裁剪以原 WAV 192634 frames / 48000 Hz、Timeline 1/6秒开始及实测 DSP 排空为依据；此前保持原时钟，之后跳至原5秒终点。0.5秒显现和1秒Finalize不变；输入门独立，待真实双端核验。
- Unity r6 聚合来源/GPU/表现/语音检查及 Build exit0；业务 DLL SHA-256 `54DBED7809886E3B24815B17F6BC2FD6C4D79E25C0B006024DB9DA0712F1631D`。构建改写的 Main 场景仅恢复自动fileID，待后续构建仍须核对。此候选真实语音双Player r1 已自然exit0，开场各2、七表情双方广播/菜单、显式投降原声和实际Repaint气泡报告均通过；实物图片与后续候选复核另记。
- Warrior Player r1 真实短门已有实证：接受技能后 .182232 秒在source仍活跃时提交 End Turn，authority接收、双方hash/旧尾释放通过。但其后旧规则检查读取已推进的回合事件而FAIL；保留失败工件，正在修复fixture的cast快照范围，不能把整轮记作通过。

## 2026-10-26 持续回放与原始数据修复

- r7/r8/r9聚合Unity构建均exit0。r8 DLL为`9F920DE8C562E4A728BBDF99B53CFA900BD2D4E35C9A44580D36B3E95C98A908`，r9为`AC4B6310BD1D8CA43ACDF36E9663855B060B4CA12A84E7C4C4222E40ECC5E9E9`。每次构建仅恢复自动Main fileID，场景SHA持续回到`D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51`；一次恢复时误写的单行已立即修正并用该完整SHA重核，未用于后续构建。
- 语音22个实际Player自然exit0、99个不同hero/emote键、22个Listener WAV逐样本复核通过，独立结论见[V022](V022-hero-speech.md)。六个旧r2摘要保留初版分析器末态/截图时态混淆的FAIL；统一重验`work/f022-speech-all-22-revalidation.json`通过，不改写历史摘要。
- 牧师满血r2双端及阶段图通过：目标权威事件1、Heal=0、回调0、额外数字0，目标屏幕误差0；修复的是选择阶段scene在接纳权威目标时未同步screen anchor/Actor目标引用。不是伪造Heal。r2正式画面和source callback在`work/f022-priest-full-r2`。
- Warrior r3双端及analyzer通过；真正提前EndTurn被authority接受、旧tail结束后无显示回退。H10 r3双端运行通过，analyzer原先误要求observer生成仅driver SendMove分支才有的截图，已拆清角色（`b68687e`）；后续构建保留driver实际PNG硬检查。H11先后发现观察端套用driver响应字段、首次加载长帧与Time.unscaledDeltaTime污染、采样误等display hash；对应harness修正后仍需最终重跑，不把历史FAIL记绿。
- H05原HealthSplat已导入并接入真正Hero血量受损：原源clip×.75、2D，纯护甲保原Armor音、自伤LifeTap走原专用声。r9将成功播放的Hero原音用于替代旧通用Minion AttackImpact fallback；其它原spell impact不移除。r2实际Hunter双端和analyzer通过。
- 独立隔离Player真实Listener中，原Health PCM 70224帧完整、前后静音均保留；48k逐样本等于原WAV×.75，最大误差与RMS误差0，无削波。完整Hunter r1/r2混音的48k两奇偶格和条件拟合也确认该分量：r2 Health gain=.754555、扣其它源后corr=.956584、残差改善43.780%，旧generic Hit系数由约.35降至.007。先前4k/8k相关性因重采样相位与叠音误判，原始48k结果优先。可重放脚本`work/v022-health-pcm-audit.py`、结果`work/v022-health-pcm-audit-r2.json`；这仍是软件Listener，不冒充设备loopback。
- H04真正的过曝来源：原Mesh Color通道是UNorm8 format2，缓存却把0/255直接喂float Mesh.colors。H04 node6/7单独clone归一、共享cache不变后，GPU RT绿均值90.6降到10.1，实心强光恢复为薄金拱形halo。原材质/blend不改；来源细节见[F022-B1](F022-B1-H04-vertex-color-normalization.md)。node11/maincamera的泄漏假设被GPU实际排除：源在world约20000，main/preview隔离捕获0像素。不得继续用隐藏节点/改共享Shader掩盖。
- H04/H07偏小另找到投影差异：通用本地场景span32、ortho16；原MainCamera为Y32、FOV34.870445，主视野垂直跨度约20.1。Mage已使用原透视，两职业还走通用正交。当前主源码仅追加H04/H07沿已有原相机适配（位置投射和Effect相机共用），待下一构建双端核验；未启用待审的H07经验缩放草稿`27dc395`，避免重复补偿。H02/H03源哈希仍冻结。
- H11动态音尾风险另在处理：r3 token回调15.0374、原WAV4.0132秒且无尾静音，而source在18.8081释放，理想source age门可能因首次加载延迟裁掉约.24秒实际音尾。将以真实原token cue的isPlaying延缓最终回收，并保持输入门独立；Prepare/Draw使用同一映射，待实际重跑，不能以静态5秒/固定grace冒称声音完整。

## 最终集成复跑（2026-10-26）

- r10 聚合检查被 H04 旧归一化投影误差断言拦住，未生成新 Player。与已有 SourceProjection 一致改为2逻辑像素并记录实际误差后，r11 为 `(-0.472, 1.114)` 像素、原相机和实例 Mesh 回读/释放检查通过。r11 完整 Unity/Build exit0，DLL `ABEF8D06D821CDF3A2B7DD543968AB3BC8A395F10F7E4A8D97586C97E9C0D816`；H04/H07 r3 双端规则/source/DSP 通过，三阶段真实图均已生成在 driver，但开场气泡与部分效果重叠，需更干净的双方阶段图。
- H11 r4 两个 Player 自然 exit0、真实 EndTurn 在 source 活跃时获 authority 接受，最终三方 hash 相同。新实际 cue 持有修复不再在理论时钟到点就截音。独立原48k Listener 拟合确认两端 token 主体、完整4.013208秒源区间位于捕获内、gain约.78；WAV最后半秒本就近静音，不能宣称尾段逐样本分离。停播观察至释放约.028/.018秒，映射无回退。但文件分析器发现两端 `source-active.png` / `source-released.png` 被旧异步截图请求覆盖，虽然 report.images 已登记；本轮整组保留不完整/失败状态。
- H11 截图现单队列跨帧保存，只有独占读取到有效PNG尺寸才登记，PASS前复验所有原必需图；不删除分析器要求。实际 token cue 仍播时持有 source，结束后下一帧清理；源 gate 拒绝已知不合法的循环 token 音配置。分析器另加停播至释放不得超过实测两项帧宽限之和的上界，避免仅检查没有提前释放。
- Priest damaged r1 已完成真实28→30、唯一 Heal/显示回调和44项规则检查，但观察端读取对端report时 Windows sharing violation 导致整组失败。共享回放只对Win32 32/33短时读写竞争等待下一帧，保留原超时和其它IO/JSON错误；失败产物未改写。
- r12 聚合来源/GPU/表现/语音和 Windows Build exit0，DLL `E1E9329E34BDBEC05D4F949A2AAFB2047983AC0797E7CCCC123208CEC2329DBD`。输入源码哈希在 `work/f022-build-r12/input-hashes.json`；Main场景仅恢复自动fileID，完整SHA仍`D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51`。定向本轮71、随后57、上界反例13项分别通过，范围有重叠，不累加。后续新Player结果接续记录，不以构建通过代替实际画面验收。
- 视觉回放夹具等待原开场语音完成及实际 `HasBubble(0/1)` 自然消失；不清泡、不停声音、不向普通对局加输入锁。H04/H07等待双方同match ready标记后施放，双方均采early/mid/tail并记录实际source age。

- r12正式补跑：H04 r4、H07 r4双方及分析器通过且双方三阶段图都真实落盘；H09伤血r2双方通过，真实28→30且唯一Heal。H10 r4双方及分析器通过；最终语音smoke两Player自然exit0、summary.passed=true（`work/f022-speech-r4-final`）。语音仍以原11英雄/99键和独立V022为完整功能证据，TASK-0007登记DONE。
- H11 r5所有截图齐全、规则/时钟/原音源/分析器通过，但干净图揭露同实体两条渲染路径：source中借用Actor在.5秒Reveal，立即EndTurn的Death cue同时画health=0 ghost。不是第二个权威随从。已按8d76abf仅在当前Display移除或Dying时临时从source capture排除该Actor，finally恢复；原Death ghost、颗粒、按钮、原声、输入门不动。
- r13聚合Unity/Build exit0，DLL `F3F6A4F616F8D18C335858031A427DF24D8F3943C380E8045D02B26AB689FE04`；输入哈希在`work/f022-build-r13/input-hashes.json`，Main恢复完整基线SHA。H11 r6两Player自然exit0、分析器PASS，observer .55/1.0秒实际图只剩一个死亡表示，不再有绿色出生Actor叠层；待独立V022最终复核，不把r5旧缺陷记录抹去。
- H04 r4的sourceAge约.154/.444/1.152秒，双方锚点/状态正确，Ready→Mid像素差分确认金色haze确已合成；相较参考仍窄淡。追加Editor-only r3诊断在.44秒/原MainCamera读regular affine、transmission、OffscreenRT和GlowTransparent，exit0，glow pass实测0像素，`_GlowIntensity=0/_GlowAddMul=0`，所以该“漏pass”候选被排除。诊断更新不改变业务DLL；旧诊断已保存在`work/f022-paladin-editor-r3/before`。外部Actor材质注入和原后处理未证实，不加臆测Bloom或经验增益，H04保留未完成状态。

- 独立最终[V022-fx-timing](V022-fx-timing.md)复核r6通过：两端原48k token可识别、source映射无回退、结束至释放约19.8/16.7ms、十张阶段图齐全且已观察时刻无双影/远处幽灵，真实EndTurn和最终hash一致。driver本次冷帧提交总耗时.424681秒（逻辑门.2秒加实测.418392帧宽限），不能声称端到端始终≤.2秒。近静音尾与设备loopback边界保留。
- 最终定向H11导入/回放及协议/语音40项通过。固定集成源码快照`5b60276d35571f2e67472df98dd967514bdff31b`使用独立临时index创建；主分支与原暂存内容保留，实际新增文件存在，关键源码blob与快照逐个核对一致。快照包含最终Editor-only诊断与Python上界检查；r13业务DLL未再改变。没有运行全量pytest，不把早期已知Shader/PPtr快照失败抹去。H02/H03最终源哈希仍为本记录冻结值。H04完整调查见[B4](F022-B4-H04-final-render-boundary.md)。

- H04最后一轮metadata-only来源[B5](F022-B5-H04-runtime-static-contracts.md)确认原`OffScreenRenderFX.SetupCamera`也关闭HDR，本地一致，故此前HDR Unknown候选排除。通用Actor portrait binder只给材质名含portrait者写纹理，与GenFX_HolyHeroRender不匹配；其它专属注入仍未证明。原Alpha曲线在动作本地.5秒达峰并Loop，不支持“早峰在实例.154秒前已经错过”的推断；未记录原参考图与AnimateFloat局部时钟，剩余相位/光晕差异不直接归因于Shader欠亮。没有追加经验亮度/Bloom或修改业务DLL。验收源码由本地`refs/verification/f022-5b60276`保存，避免孤立快照被清理。
- 默认本机服务器已在确认无任何Established连接且匹配之前自建PID后安全更新（旧14888，现wrapper33272/child17492）；37240真实hello公布hero_emotes_v1，无用户对局被中断。初次1秒启动探测早于模块加载而拒绝，随后正常监听并验证通过。
