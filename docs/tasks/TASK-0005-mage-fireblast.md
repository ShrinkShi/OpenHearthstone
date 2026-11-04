# TASK-0005

## 任务名称
标准法师英雄技能火焰冲击与目标指示器完整恢复。

## 背景
2026-10-17用户要求完整还原法师技能“烈焰冲击”和指示器，覆盖音效、特效、文字描述、图标、图片、动画与渲染。本机卡库名称为“火焰冲击”；本Task按同一标准法师技能HERO_08bp执行，不因口语名称误差换成其他卡牌或外观版本。用户明确启动该专项目标，优先于旧双卡队列的横向暂停；TASK-0004仍保留其原有资源阻塞，不是本Task的前置依赖。

## 当前已知事实
- Confirmed：local/card-database.json（SHA256 678211E237FEF4B957BB784CCE85BA1FFC395B3D5E2D738FD6B8A5F1F37C9A95）HERO_08bp，Name=火焰冲击，Class=4，Type=10，Cost=2，Text=<b>英雄技能</b>\n造成$1点伤害。Targeting含11。规则/显示值及运行行为须由Feature复核，不能把原描述中的动态标记直接显示给玩家。
- Observed：docs/UI_FEEDBACK.md及manifests/feedback-assets.json记录HERO_08bp关联ReuseFX_Missile_Fire_Fireball_Small_Super.prefab:b8e7bb6e0944d204297efdc937967085，已有原起手/发射/命中音轨关联与重实现弹道。该文档含历史状态，不代表当前完整实现；需要沿卡牌→真实资产→控制器核实，不能仅凭Fireblast文件名选资源。
- Confirmed（启动时）：BattlePainter.TargetArrow为程序红段/环/生成箭头。现HERO_08bp已接NativeTargetArrow原mesh/纹理/Shader与源曲线；当前GUI投影不是原运行layer9 hitbox/遮挡服务已恢复，详见专题。
- Confirmed：TASK-0002/0003的局部命中、根编排及显式规范化历史宿主已独立验收；不可把既定双卡的通过迁移为HERO_08bp通过。
- Unknown：该技能完整官方时序/全屏交互/音频与当前本机源的版本可比性。先实际核查已有官方图片/资源/行为证据，不沿用Task4双卡缺参考推断本技能也无参考。
- 2026-10-19第二轮用户复验补充：技能按钮/准备、火球飞行、命中闪光及伤害数字五张用户称为真实炉石画面的逐阶段JPG，原样保存在 `work/user-review-2026-10-19-round2/`，原始SHA见新回合。它们是新增视觉参考；客户端版本、图间精确时间间隔、原音频未知，不能仅凭静帧推导完整动画或发射音轨。

## 目标
1. 建立HERO_08bp单技能来源与覆盖清单：中文名称/描述/动态数值、原画/按钮图标/框体/费用、可用/不足费用/已使用/敌方/悬停/按下/选中状态；原字体/排版与图片UV、材质及渲染状态逐项核实。
2. 还原从选择技能到合法/非法目标提示、指示器锚点/形状/动画/颜色/层级、移动跟随、点击/拖动确认、右键/Esc/回退取消的交互与生命周期。客户端以权威合法动作判定，取消/非法操作不得发送施放请求；友敌英雄/随从、不可选目标、自己视角/对手视角均有真实用例。
3. 还原真实起手→施放→飞行/到达→伤害/命中→结束/回收，完整恢复本技能实际触及的原控制器、粒子/动画/路径/材质/Shader/混合/渲染顺序及音轨播放、时序、音量/音调/空间与停止语义。不得以任意延时/合成近似代替已可证实原链，避免原音轨与反馈cue重复播放。
4. 正式接入当前ClientApp/Presentation/Importer/Registry与权威规则，保留原资源引用与来源哈希。仅为本技能必要分支增量实现，不重建系统、不把原DLL作为执行实现。
5. 交付可复现完整GUI/局部GPU/真实音轨与事件记录、关键帧/contact sheet、原版与实现差异分项、可直接运行的Windows客户端和独立V005。没有证据的分项保持Unknown，完整还原不以测试数量自证。
6. 2026-10-19用户复验追加授权：修复法师对术士敌方视角提前/重复受击、火球与选择阶段效果尺度/动画、技能详情及战场随从肖像偏暗，并将已认可的原资源法师目标指示器复用到所有带选目标指示器的卡牌、技能和现有目标交互入口；不扩展这些卡牌自身规则/特效。
7. 2026-10-19第二轮复验：调查并修复火球发射音效异常；依新增阶段参考，恢复飞行期间实际原拖尾/粒子、命中闪光与伤害数字的外观、大小、时序和渲染。先从原资源链确认系统/Prefab/控制器、音轨和伤害数字来源，不把用户的视觉举例误当作具体粒子实现指定；数字呈现沿权威伤害事件验证，不改伤害规则。
8. 2026-10-20用户人工复验认可火球/命中特效已基本还原，发现命中特效后约数秒才能继续操作；调查权威结算、Hit Cue、DamageSplat生命周期和`Presentation.Busy`锁的关系。不得改为服务端等待动画；如后续修复，应让纯装饰尾段不再锁全局输入并保留数字淡出。

## 非目标
其他职业技能自身特效与规则、升级/替换技能、法师全套皮肤/全卡池、网络重设计、Bridge、执行原游戏DLL、修改原安装、Computer Use、发布/推送资源。2026-10-19用户明确追加的共用指示器与肖像渲染修复属于本Task；须在不破坏既有行为下补最小共用实现。

## 相关文件
AGENTS.md、PROJECT_STATE.md、DECISIONS.md、WORKFLOW.md；UI_FEEDBACK.md、ASSET_PIPELINE.md、BUILD.md、tests/visual/README.md、visual/VISUAL_COMPATIBILITY.md及VISUAL_REVERSE_ENGINEERING_WORKFLOW.md；ORIGINAL_SUPER_SPELL/ORIGINAL_SUPER_IMPACT/ORIGINAL_LEGACY_MIXER等按实际触及分支读取。代码入口：ClientApp.PowerVisual/PowerArea/Arrow、BattlePainter/BattleGeometry/BattleInteraction、Presentation/NativeEffectPlayer、Importer/Registry、game/match.py；tools/feedback_assets.py、presentation_assets.py、interaction_assets.py、native_effects.py、visual_scenarios/visual_capture；相关manifests和local本机来源。

## 实施约束
总控编排，独立F005最小上下文执行，达到READY_FOR_REVIEW自动创建全新独立V005；失败回Feature修复后另开验证回合。串行master/唯一工作树，Unity/local/build由当前Agent独占。已验收旧Task不改历史结论；新改动对共用路径的风险需回归。用户UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。隐藏图形batch、不-nographics，准确记录PID/退出码，原安装只读，不抢焦点。手动apply_patch，中文提交，不amend/reset/push。

## 验收标准
- 来源：确认技能身份、所有实际使用图/音/模型/Prefab/脚本/Shader与依赖；记录版本/hash、字段/原调用与接入位置，直接恢复/行为重建/推测分别列出。
- 规则及输入：真实Match.apply+双方公开视角，消耗/伤害/合法目标/使用次数/回合重置/取消/非法请求无回归；输入重放实际走正式点击/拖动/鼠标移动/取消与目标判定代码，不操作桌面，不仅测试视觉伪状态。
- 完整GUI：正式技能按钮和选目标界面，以本项目客户端真实布局/渲染输出核查图标/描述/动态数值/费用/状态/指示器，1920×1080及缩放/边缘/视角边界；后台card_render_count=0的对局不可替代GUI证据。
- 生命周期/GPU：实际原资源克隆、真实路径/事件相位与正常及取消/重复/目标消失/场景切换/暂停清理；无残留对象/相机/RT/音源、无正常路径未绑定动作；实际Shader/颜色Alpha/遮挡与C/K/全屏合成按所需路径验证。
- 声音：原文件/PCM及采样率/声道/时长/hash、正确cue与控制器时刻、播放次数/音量/音调/停播，必要实际录制/波形比较；不能仅以文件存在或AudioSource调用声明听感完全一致。
- 程序：针对回归、适当共用链、Windows新构建与正式双端真实施放/整局双方状态一致、无新增错误；实际跑项和引用旧基线分清。
- 完整一致性：独立V005程序与来源验收，原版可比画面/音频及分项差异真实记录。若自动部分完成且仅主观判断待人确认，AWAITING_HUMAN_REVIEW并输出原参考/实现/关键帧/差异；若真正缺参考或关键源，只能BLOCKED并交已完成部分/缺项/解除条件。任何一项未恢复不得将整Task标DONE或声称一个都不差。

## 当前状态
READY_FOR_REVIEW（2026-10-20，Feature第二轮用户复验已按本轮新增范围完成局部实现和双端正式GUI复验；待总控开启全新独立V005-r3。旧V005-r2提交5e34c98只覆盖此前五项，不能继承为本轮独立验收。整Task完整官方一致性仍未达成，保留未知项，不标VERIFIED/DONE。）

### Feature第二轮复验交付（2026-10-20；等待独立V005-r3）

- 源实现边界：只为真实Small missile的五个来源粒子节点传入World simulation-space policy；通用粒子、Start、Impact和Frost Nova旧路径不变。基于原Prefab实际引用沿生命周期继续使用原Arrival/Impact和回收逻辑。`NativeParticle.Create` 默认保留旧enum映射，专用policy仅在本卡实际missile节点调用。
- 伤害数字沿权威 `Hit` 和来源 `DamageSplat` Actor表现接入：英雄和友方随从双端GUI均只显示一次，随从实际生命值7→6；同一个Target/Actor路径，不额外叠加Presentation文字。无效选择/取消没有施放请求；正式场景与Hit时序验证以r10报告为准。
- 最终正式候选：`work/f005c-formal-minion-r10/`，1280×720；driver与observer均natural exit 0，42/10项，Hit各1次，完整Hit窗口分别4.100756s和4.103216s，状态哈希相同`643e3941ba082bf423065179d0274568b6d8954ec9d0a71a0bca1834d88c2359`。实际随从命中Contact sheet：`observer-cast-contact-sheet.png`；完整索引`evidence-index.json`；双方timeline/report与PNG均在同目录。发射/到达/命中连续关键帧可从contact sheet回查。
- r10全局截图间隔未覆盖约0.252秒的真实飞行窗口；build-r16添加两张review-only定点帧后，最小1280×720随从补充run `work/f005c-formal-flight-r1/` 同样双端exit0、42项、publicHash与r10一致。实际GUI帧 `driver/review-flight-mid.png` 与 `driver/review-flight-late.png`：flow `MissileBirth 1.253640`（frame5955）至`Arrival 1.505708`（frame5997）；两截图分别frame5965和5982，均在Arrival前，后帧可见火球/来源尾迹。r16 build日志 `work/f005c-build-flight-r16.log`，DLL SHA256 `da11e1a29c736715d15c1e0aa394d616dc6cfbf31b4758e16a45b2149f08a05b`，变化仅在review采样逻辑。
- BuildClient.Build原正式候选build-r15成功，产物SHA256 `68BAD1BC92679F3A646229FEE0B5ADE10653369630C5D16ADDA804B26232FBDF`；r10双方自然退出，scene exit资源计数powerViews=0/nativeVoices=0。r16补画面run DSP有168个float样本绝对值>1，声音结论仍只引用r10混音文件。
- 声音来源与实际混音见专题和新交接：两个原Prefab source actions各播放一次同一44.1kHz mono clip `local/runtime/0e9c3a038b94535def357f71.wav`，播放时刻27.521982s/27.540022s，间隔18.04ms，原Prefab source volume分别0.4/0.5，pitch均1。48kHz双声道float32 Listener混音`driver/scene-mix.wav`为916480 frames，peak .9420093、clippedSamples=0；同目录timeline.json记录事件，scene-mix-energy.csv按10ms列混音能量。无原游戏录音或版本信息，无法客观确认用户所说的听感异常已消除，保留用户听审；不删除原动作或凭主观改音量。
- 局部来源粒子字段及连续轨迹、成功/失败的运行夹具、音频PCM/播放事件记录、用户图与客户端画面的限制见[Feature R2交接报告](../handoffs/F005-task-0005-round2-review.md)。黑底隔离Missile图不能单独作为完整强度结论；目前接入的是源证实粒子配置，无硬编码增亮或放大。
- 保留正式失败与环境限制，包括早期报告共享/marker竞态、等待窗口/observer关闭问题、1680×1050环境前提失败、ResetMenu后服务端排队状态重建静态合法技能按钮的r8失败，以及此前SIGSEGV归因Unknown和`-nographics`无效build实验；最终r10是完成有序连接关闭后的双端一致绿跑。旧失败输出不删除，不将夹具失败归到生产路径。

### 2026-10-20用户复验：命中后操作延迟调查

- 用户实际游戏确认本轮飞行及命中特效表现良好；新增问题是命中效果播放后继续操作延迟过长。
- 代码核实根因在客户端：`Presentation.Accept` 给本技能Hero Hit设`duration=4.1f`，DamageSplat/数字沿此时长绘制；`Presentation.Busy`在整批Cue结束前保持true，`ClientApp`输入、出牌、技能及回合结束入口均以Busy阻塞。故长数字展示尾段被当成全局输入锁；不是服务器或权威伤害结算等待。
- 真实双端时间线中权威血量已更新为6，而显示仍为7；Missile Birth到Arrival约0.252秒，Arrival后一次Hit才切换显示7→6。相关记录`work/f005c-formal-flight-r1/driver/timeline.json`、`work/f005c-formal-minion-r10/observer/timeline.json`。4.1秒代码窗口与约0.252秒飞行时间量级分离，解释了“视觉结束后仍在等”的现象。
- 本轮仅完成根因调查与方法文档记录，未改Busy/伤害数字生命周期，未声称延迟已修复。若单纯缩短同一Cue会截断数字/飞溅；合理修复是拆分Gameplay输入锁与装饰飘字生命周期，保留权威即时结算和按原曲线淡出。用户本次请求仅要求调查及复用经验记录。

### 第二轮用户复验与本轮要求（2026-10-19）

- 用户观察：起手/手中火球特效及音效基本同步，发射音效听感异常；飞行阶段看起来是单个圆球直飞，未看到其参考中明显的拖尾/粒子；要求恢复命中呈现及伤害数字。用户指出“像什么”仅为视觉描述，不指定原内部实现。需分别检查原动作/音轨调用和实际音频混音、Source/Target路径与粒子/Trail、Arrival/Hit/伤害飘字完整链。
- 五帧按参考阶段顺序编号，原始字节保存在work；文件SHA256：
  - `work/user-review-2026-10-19-round2/01-power-ready.jpg` `F39478041F4C9A34DD952573094204ED1D63D770179FB8D98F1778078798E4F2`
  - `work/user-review-2026-10-19-round2/02-fireball-flight.jpg` `CC2C385877374EA4DC354F00728828287765CB8B3573BDE25E41E7BBD77BE8D1`
  - `work/user-review-2026-10-19-round2/03-impact-flash.jpg` `7AAC7A2C405EA1ACA25BE0E056F33F2F2E301786B3529D7B3312D80479E5633C`
  - `work/user-review-2026-10-19-round2/04-impact-light.jpg` `C483EBA42A79B6C58541D639B39232A97A51E33A793A3467DB21D7A559AF839A`
  - `work/user-review-2026-10-19-round2/05-damage-number.jpg` `769D9A44536B4B962E0319A198CDCAEBE69C2C15602D47D725985C7311E09A44`
- 这些是用户提供的本技能画面参考；原客户端版本、逐帧时间差、原声轨和运行相机/灯光配置仍Unknown。先核实本机对应原资源/源码，再比较，不伪造时间点或推导音频。
- 本回合验收：发射cue与按下/Spawn/飞行首帧的调用数、时间和实际float32 Listener捕获；火球从出生到终点的连续双视角帧须呈现来源证实的拖尾/粒子且正常回收；Arrival闪光、目标受击与权威伤害数字只触发一次、数值正确、显示层级/动画/尺寸实际渲染；取消、免疫/无效目标或断连不生成伪伤害数字/残留；新构建与标准回归。记录本项目画面与用户关键帧的差异；这组静帧不代表原声音轨或完整可比时序。

### V005-r2独立验收（本轮当前）

- 全新无Feature聊天历史Agent先只读初审，明确移交后独占Unity/local/build，固定业务d4fc538/交接4bb08ba。新构建PID32316自然exit0；Clock12/时序26/捕获环境光29/Shader28通过，107针对Python通过。独立source197字段/身份/像素检查，3个不同原capture（Start170²、Missile111×380、Impact180²）；两原肖像fresh解码与runtime PNG RGBA像素字节一致；fresh R2T10段DXBC与原DLL仅静读。
- 56独立边界最终PID34484自然exit0，涵盖帧时钟重置/长期精度、音源限额provider帧去重/零delta冻结、两相机provider隔离、capture post-RT异常清理和四类正式输入（补牧师定向技能）。五旧probe原样34/44/44/43/66、RunFinal92、真实规则30及停止/History通过。首16956/19384 exit1分别为必需sourceMaterial返回null及batch缺Event.current夹具前提错误；纠正及精确Draw栈豁免保留于报告，不算业务FAIL。GUI真实绘制另有证据。
- 同一新188BB DLL：1920 Mage/Warlock 14400/21288、1280友CS2_119 1876/26828均41+7；DC33672/27848为20+3，低费19688/6020为14+2，全部Player自然exit0。双方真实Arrival前HP/Tint/Animate/Shake/Hit为等待态，Arrival后仅Hit1；28→27/7→6完整wall窗口.583424/.584377及.551559/.575876秒，对应cue.55，漂移≤.001310秒，实际选择倍率1.999080/.499560与1.999195/.499871。完整图/连续帧实读与独立审计位于work/v005-r2-gui、scaled。
- 当前DLL无fixture自然Mage/Warlock整局28432/20624自然exit0，15回合/version54、journal55/error0/Concede0、power2，双方finished/public hash与authority同为ef7832a47480e7e10b0279702395cfff4abe314c58b46a32d89e825351019b70，双1920末态PNG已读；不是复用Feature旧DLL整局。server证据完成后launcher清理exit1，未与Player错误混同。
- 实际driver Listener float32 48k双声道：1920 peak1.356694/abs>1样本340、1280 .928199/0、DC .700266/0、低费1.238289/82；overflow均0，原float保留，不声称设备硬裁或官方听感一致。独立复算全部RIFF/PNG/journal。自然无DSP、observer无Tap，范围明确。
- 用户五项本地客观结果PASS；火球头尺寸/选择动画及整体视觉仍须用户认可，官方精确动态视听/相机/hitbox/灯阴影等缺口仍真实，整Task保持BLOCKED。只读重新清点153媒体均PNG，03确有上方敌方法师静态参考，非完整动态gold；未新跑四大型旧来源/全1187/所有其它职业双GUI。
- 证据工具限制：现有RunBuild硬编码输出work/f005-r2-gpu，本轮重跑覆盖该本机Feature生成目录，已即时复制work/v005-r2-gpu；旧目录不再当不可变旧证据，新log/副本只属本轮，不回填旧hash。用户图/原安装/校准未变。自建Unity/Player、临时Editor/meta、8处Main.unity fileID噪声已清，业务只读。完整复现、两次Editor夹具失败及首audit查询失败见V005-r2报告；验收提交用git log -- docs/verification/V005-r2.md定位。

### 本轮修复交付与证据
- 唯一受击：Tint/Animate/Shake/Death与显示生命值统一等待真实Arrival，首次可见Arrival开启完整Hit窗口并保留Death偏移。26时序项含晚观察和消失的致死目标；正式r8两视角各Hit一次，敌英雄28→27、友随从7→6。1920完整窗口wall .570719/.562237秒，cue .55；逐帧数学复算误差不超过1.4e-6，属于Feature分析而非独立验收。
- 主体/尺度：修复Start/Missile的target=-1自动plane漏建，恢复原3×3/2.5×8.5几何、170²/111×380捕获与AlphaClip/Bloom。仅本技能用原authored透视参数＋当前GUI/y0映射，root/owner scale1。Start可见高43.125→52.5px（英雄rect高200）；隔离Missile可见高22.5→56.25px、尾段约60px。完整GUI及独立bbox已读，未任意放大；原运行动态camera/遮挡仍Unknown。
- 暗色：保存场景暗SH与原结束换牌后白ambient不一致；共用Actor/技能详情读取显式BoardAmbientColor。真实原肖像Shader白SH输出与纹理RGB一致，正式1920/1280的CS2_119卡面/战场及详情同屏已读。不改sRGB/Gamma或加亮倍率；原换牌3秒渐变/全局灯阴影仍未完整恢复。
- 共用指示器：ClientApp全部实际目标入口统一NativeTargetArrow，保留各自origin、合法moves及取消；排除held卡自身遮挡。Mage/Warlock真实技能、Fireball、ElvenArcher战吼、CS2_119攻击入口取消零请求。非目标术士技能不画箭头。
- 选择/整体速率：r4/r5实测最早engineDelta已偏离wall，原因Unknown。按DEC-009新增ClientApp单一单调表现帧输入，暂停仍刷新基线，Speed保持一次；Clock12项及真实暂停/2x/.5x通过。r8最大wall/batch漂移≤.001179秒，选择倍率2.000893/.5000651（r7）。不修改权威即时结算。
- 当前DLL正式1920（10808/3468）与1280（30236/28668）各41+7、DC（33652/6588）20+3，双Player自然exit0。自然整局明确复用生产逻辑相同的cb962513 DLL：24420/14540自然exit0，turn11/version35/error0/Concede0、真实power3、双终局PNG1920×1080；最终188bb仅Replay证据目标通用化，未再跑自然。
- 回归：PID14372 exit0，Clock12/时序26/捕获环境光29/Shader28；PID20936 exit0，选择46、五原probe34/44/44/43/66失败0及正常共用根/30真实双视角规则；针对Python107通过。四大型旧原源重读/全1187未重跑。最终构建PID7064 exit0。
- 实际Listener float32 48k双声道混音保留：1920 peak1.365077734/abs>1样本240，1280 peak1.083602905/20，DC peak1.066918135/28；overflow均0。不是PCM16派生夹取，不证明设备硬裁或原版听感；仅driver录制。
- 红绿与所有失败保留：timing-red、visual-r1固定取样未找到Missile、正式r1拾错邻卡、r2双方退场竞态、r3尾端采样断言、r4/r5真实速率异常、scaled-r7预览目标错误。复跑命令、源字段、输出索引与限制见docs/handoffs/F005-task-0005-r2.md及ORIGINAL_FIREBLAST R2段。

### 用户复验失败与本轮必验项

输入为用户实际运行反馈与两张客户端截图，不是官方原版gold。原图已原样留本机忽略目录：

- `work/user-review-2026-10-19/fireblast-detail-dark.png`，SHA256 `55D2911FEA9F435D940E4643B030FC66CF5C297C3105D520C1E23CCE4DF0ED7F`：技能详情原画偏暗。
- `work/user-review-2026-10-19/minion-portrait-dark.png`，SHA256 `9892596AC9DF3D0C53D5D7A64D23B55FCAA033F595ED1BB1692BD001410FD5E6`：绿洲钳嘴龟卡面与战场肖像同屏，后者明显偏暗。

1. 法师对术士，分别从施放方与敌方真实GUI观察：术士应只在实际命中阶段受击一次，不得在火球前先受击。记录authority事件、Presentation排队/启动/Arrival与目标受击触发/显示生命值时刻，区分权威即时结算与客户端显示，不改Server等待动画。
2. 火球错误或过小：重核真实Prefab/parent缩放/源目标与相机投影/材质及粒子，记录选择、发射、Early/Mid/Arrival/Hit同屏连续关键帧和可追溯尺度；不以任意放大倍率或局部非黑断言代替研究。
3. 技能详情与战场随从偏暗：沿原纹理、颜色空间、SH/灯输入、肖像Shader及RT/GUI合成核实根因，实跑同卡卡面/战场/双方视角；不得只对截图后处理或给单张图加亮掩盖共用问题。
4. 所有实际选目标入口复用已认可的NativeTargetArrow外观与动画，保留各自正确来源锚点、pointer、权威合法判定、取消/拖动/释放与清理；扫描并覆盖技能、手牌指向法术、战吼选目标及现有攻击等目标交互，不残留旧程序箭头作为常规路径。无目标入口不凭空画箭头。
5. 点选技能、尚未确认期间的动画/效果单独研究原Birth/Start、层级尺度、可见性与循环，固定多个选择等待时刻及取消/确认连续帧；旧“同实例Cast通过”不能代替选择视觉验收。

新独立V005须复现本轮相关旧行为后验证修复，至少加入法师对术士敌方视角连续时序、绿洲钳嘴龟共用肖像与跨入口指示器GUI；旧同Mage场景与数学探针不够。当前无法从用户静态图判定火球/选择动画的唯一根因，须调查，不预填修复结论。

## 上一轮实施记录（历史）
启动基点0460b9b；总控只完成任务定义/初查，不自行实现或独立验收。后续由F005记录真实调查、选择、失败与修复。

F005建立docs/ORIGINAL_FIREBLAST.md来源/当前实现/差距矩阵。原root/Start/Small/Impact、原TargetJoint自身fallback、选择Birth→确认Action/取消Cancel4同实例、普通AudioPlayAction/延迟循环停止、标准Actor/详情与中文TTF、15池原指示器及Shader25-40本卡分支已增量接入。修复真实源finishEvent、空串事件漏派发、同帧误倒带、Connection退出崩溃、UNorm8颜色误传与非线性UI合成；失败日志全部保留。按钮直接framebuffer分支仅限DEC-007；原authored HDR=false不升级原运行目标事实。31/32两种无主阴影源分支已补完整条件数学，原全局选支/灯输入与四阴影分支仍未恢复，当前显式0灯不是原运行事实。

## 上一轮测试结果（历史）
- 最终局部PID2508自然exit0：25GPU/46选择，真实严格倒带、同帧Prepare、根所有权、源动作覆盖与对象/RT释放；模型音源与正式DSP分开。
- Shader PID29200自然exit0：23有区分度源数学；粒子PID19676自然exit0：原plane受控bounce -9.939138→5.571338及物理释放。
- 共用根组合PID3548自然exit0：原V003五归档探针34/44/44/43/66，原断言未改；正常源链/命中/停止/历史宿主/30双视角规则通过。其后UI/31/32有界Shader变更用新GPU/新构建覆盖，不机械重跑未变旧根。
- 针对Python106通过；四大型旧原源重读未在本轮运行，不称全量1187。实际9项规则专项含普通法伤不误增本技能、已有费用/伤害修正。
- 最终Windows build-r15 PID28056自然exit0，DLL F05E9C2BE77AD0E5E9D0C94A1849A54259E7B4AE06A76263868E99811D56A405。final-gui-r1 driver17252/observer17116自然exit0，27+3、1920×1080、双方公开hash一致；实际Listener float32混音48k双声道612352帧/598回调、peak .9065204/RMS .0861651、超1样本0/overflow0。混音含源UI音轨，不等于官方听感比较。
- 缓存保护最终build-r17 PID5268自然exit0，同入口12项缺/v1 schema双入口拒绝及部分构造物理清理通过；新DLL `1F7958626A93B45E590D5C78DAEDFAE38FAC29BA36846B2C3822F29A7B2A625B`。final-cache-gui-r1 driver23564/observer10860自然exit0，27+3/双1920；实际DSP608256帧/594回调/peak .94210094/RMS .08513788、超1为0/overflow0。后述F05E整局/缩放明确引用未改有效路径基线，不称最终DLL重复全跑。
- DC10+3、低费7+2双端自然exit0（4593C260固定DLL）；新Shader版F05E固定DLL的final-scaled-r1 driver15368/observer5852自然exit0，27+3、实际1280×720友随从目标。同F05E版final-natural-r1 driver12952/observer5256自然exit0，14回合/version41、5次真实power、无错误/Concede，双方同最终公开hash4cf2a6e9…，实际1920×1080；不是最终1F7958版又跑了整局。真实完整GUI截图/contactsheet/PCM/10ms能量/源音轨元数据位于work对应目录，不提交原资产。

## 上一轮最终结果（历史）
以下V005-r1为前一固定实现的历史局部结果，不覆盖当前R2候选；当前结论以本Task“当前状态”和R2交接为准。完整“一个不能差”未达成，原运行服务/官方比较缺口保留，不升VERIFIED/DONE。

### V005-r1独立验收（2026-10-19）

固定业务740494b/交接f4a156b，无Feature历史的新Agent实际验收。首Unity26840自然exit1为探针错误期待构造时立即音频callback；改到真实Tick注入并要求Fault/物理清理后，新Unity7772自然exit0，独立17边界及Shader23/粒子6/cache12/GPU25/选择46、原五归档34/44/44/43/66与RunFinal共用链通过。fresh原来源455项/6bundle、46段原DXBC与两TTF字节重读，原DLL3577行仅Cecil静态读取；106 Python通过。

新Windows构建27848自然exit0，DLL仍1F7958626A93B45E590D5C78DAEDFAE38FAC29BA36846B2C3822F29A7B2A625B。该新构建本轮五双端全自然exit0：1920 GUI23176/7248（27+3）；1280友随从33648/26336（27+3）；自然9560/7492（16回合/version64，power2/attack27/play18/endturn15/mulligan2，error0/Concede0，双方最终hash1c119bd1…）；真实DC292/4116（10+3）；低费33076/2236（7+2）。server均在完成后launcher清理exit1，不混称Player错误。除自然模式外均有受控初始局面及末尾Concede清理，不当自然终局。实际float32 DSP独立复算：1920为607232帧/593回调/peak.9839280844、超1=0；1280同帧数/回调、peak1.0834941864、超1=28/overflow0，保留原float，不声称硬裁音或官方一致。

独立实读GUI/原03参考及153媒体清点，确认静态亮度/相对尺寸差异、动态视听与原运行服务输入缺口真实。未发现需退IN_PROGRESS的实质程序FAIL，完整Task保持BLOCKED。校准SHA不变、临时Editor及Scene编号噪声已清、自建进程退出。报告与复跑探针见docs/verification/V005-r1.md；验收提交可用git log -- docs/verification/V005-r1.md定位。

## Commit
当前R2固定实现`d4fc53802bfd718611b1a2287dee45cb7844bd95`，交接docs/handoffs/F005-task-0005-r2.md。上一轮0af5556/740494b及旧交接仅保留历史。文档提交记录固定业务哈希，不伪造自引用哈希。

## 遗留问题
最小解除资料：同版本本技能选择/合法与非法指向/取消/确认/飞行到达命中结束的带帧时标视频与原声、原运行相机/目标hitbox及光照keyword/主附加灯数组/atten映射/阴影与目标格式输入。取得后恢复实际所需阴影分支和场景绑定，固定同条件逐项比较。通用UberText非本卡算法与名称原运行舍入也未全闭合。1920已实跑成功，不是外部阻塞。详细范围、搜索153媒体/原DLL静态来源及最小重跑命令见专题/交接。
