# 标准法师火焰冲击专项

TASK-0005 / F005，更新2026-10-19。目标固定为 HERO_08bp。用户人工复验已拒绝740494b的实际输出；本轮d4fc538五项修复已获全新V005-r2独立本地有界PASS，报告见[独立验收](verification/V005-r2.md)。旧V005-r1（6d3452b）仅保留历史。当前实物主观视觉待复看；原运行/官方完整保真缺项仍使整Task BLOCKED，不声明完整一致。

## 人工复验修复 R2（当前交付）

- **提前受击 Confirmed**：隐藏Unity PID18212/exit1，`work/f005-r2-timing-red.log`，显示血量已等待真实Arrival，但Tint/Animate/Shake/Dying仍以旧预估start启动。统一Cue.Ready后PID14648/exit0/14项。继续补首次观察延迟：原Arrival时间可能已过完整反馈窗口；现以首次可见Arrival开启完整Hit，保留Death相对Hit的延后，死亡Draw也受Ready门控。PID29084/exit0/26时序项覆盖两视角、正常、晚观察与已从权威快照消失的致死目标。最终r8正式双端连续帧已完成：1920敌英雄与1280友随从各41+7、Hit各一次，完整窗口和实际速率见下表；不是仅探针结论。
- **主体漏绘 Confirmed**：Start与Missile实际RenderToTexture都是target=-1/m_CreateRenderPlane=1/renderMaterial5；旧实现只接target>=0却将Offscreen主体禁画，实际只剩火星/烟。新增有界自动plane与原AlphaClip/Bloom：Start170²、Missile111×380（medium长边截断），owner子节点原3×3与2.5×8.5几何、queue3200/3201，独立Bloom源捕获→threshold→横/纵4tap。`NativeGeneratedCapture`复用Registry/原捕获，不改粒子或parent尺度。原字段/IL/3个Shader完整ASM/hash见`work/f005-r2-source/REPORT.md`及相邻原始数据；源资产仍本机忽略。
- **真实GPU Observed**：`work/f005-r2-visual-r1.log` PID5608/exit1；Start六时刻捕获成功，后续固定.18秒取Missile不存在而失败，不能称全通过。改沿真实Flow克隆可见事件采样后`f005-r2-visual-r2.log` PID29084/exit0，26时序＋28捕获/环境光检查。Start .05/.15/.3/.6/1.5/3秒的源RT alpha像素214/550/1576/1744/3964/3961，root与owner lossyScale均1；disable→reenable重新建RT、硬取消与Missile Dispose物理释放。已读`work/f005-r2-gpu`主图，正交32world适配下仍较小，因此不以非零像素宣称尺寸已解决。
- **投影修复（已正式GUI）**：仅HERO_08bp采用原authored Main Camera的透视参数，当前GUI点射线到明确y0平面；与已认可箭头同基准，其它SuperSpell仍保留旧投影。Start原3×3 plane投影约169px，RGB>.01的实际可见高度在.6/1.5/3秒为43.125/48.75/52.5px，相对英雄rect高200为.216/.244/.263。隔离Missile实例（排除Start/Impact）在源state1→3的可见高度22.5→31.875→43.125→50.625→56.25px，尾段约60px；完整图bbox包含Start残留，不当弹体头尺寸。root/owner scale1，未改用户校准或任意倍率。work/f005-r2-gpu/screen-metrics.json及missile-body/flight/selected帧可复跑，已实读；原运行动态camera/hitbox/遮挡仍Unknown。
- **暗色 Confirmed/Observed**：原Board构造ambient白、EndMulliganWithTiming调用RaiseTheLights；保存场景暗SH不是结束换牌后白输入。Registry.BattleAmbient接当前BoardAmbientColor，战斗普通阶段刷新共用Actor与技能详情。真实OriginalActorPortrait+packedSH：保存probe输出(.247,.173,.102)，白ambient与纹理同为(.400,.200,.102)，SHAr.w=1。正式1920/1280已实读技能详情及CS2_119卡面/战场同屏，颜色改善；不改原sRGB1/DXT1纹理或Gamma工程。CS2_179是森金，不能移用为绿洲钳嘴龟。原换牌3秒灯光渐变和主灯/阴影服务仍未完整恢复。
- **共用箭头 Observed**：ClientApp唯一活动Arrow路径统一NativeTargetArrow；权威moves决定靶心，保留各入口origin/拖动/放置和取消。修held卡自身遮挡命中，与TryDropCard排除dragged一致。正式Mage/Warlock的技能、CS2_029法术、CS2_189战吼、CS2_119攻击预览已读图，取消零请求；Warlock非目标技能不产生箭头。卡片中心被邻卡遮挡的r1 probe失败保留，现用真实露出命中区域并验证dragged实体，不伪置selected。

## 上一交付来源与差距表（R2未覆盖项保留）

以上R2实现还包括DEC-009单一表现帧输入：`PresentationFrameClock`每次Update最早采realtime double两帧差，供现有Presentation/选择/FX/UI/ShaderTime和显式音源限额/CameraShake输入；首次/菜单/OS暂停重置，暂停仍采基线，Speed按原路径一次。**原引擎delta异常为何发生仍Unknown**；r5已证伪“同一次Update内变坏”的猜测。旧rawEngine在最早已.0041625而wall每帧约.095秒，随后集中跳13.366秒；不能把r5双端PASS当正常速率。修后r7/r8逐帧wall和batch一致，源Scene/typed clock/停止/倒带本身未替换，规则网络继续即时结算。

### R2固定运行结果与范围

| 检查 | 实际结果 | 本机输出 |
| --- | --- | --- |
| 新GPU/帧输入/构建 | PID14372 exit0，Clock12、时序26、捕获/环境光29、Shader28；BuildClient原常规检查与Windows构建成功 | work/f005-r2-build-r7.log；work/f005-r2-gpu |
| 当前最终构建 | PID7064 exit0，DLL 188BB0868DE43ECBB8AA9E15567419F739AB49B550340AB15F42E0776BF45D58；相对cb96仅Replay友方目标证据通用化 | work/f005-r2-build-r8.log |
| 当前1920敌方英雄 | driver10808/observer3468 exit0，41+7，两端同hash；27/39逐帧记录，Hit各1、28→27 | work/f005-r2-final-gui-r8 |
| 当前1280友方随从 | driver30236/observer28668 exit0，41+7，CS2_119 7→6；实际PNG1280×720 | work/f005-r2-scaled-r8 |
| 当前断连/硬清理 | driver33652/observer6588 exit0，20+3，实际TCP关闭、无Cast/晚音源重启，根/RT释放 | work/f005-r2-disconnect-r8 |
| 共用根合同 | PID20936 exit0，五原probe34/44/44/43/66 failure0，RunFinal92/30真实双视角、停止270与History1117等原正常链；Probe源码未改 | work/f005-r2-regression-build-r3.log |
| 自然双端 | cb962513 DLL，24420/14540 exit0，turn11/version35/36journal/error0/concede0，真实power3（Mage2/Warlock1），双方同最终hash | work/f005-r2-natural-r7；两终局PNG均1920×1080 |
| Python/来源 | 107 passed（fireblast/source/match/network/native_effects/presentation_metadata）；未跑四个大型旧原源重读/全1187。新增只读来源工具重跑exit0 | tools/source_review/fireblast_scene.py、fireblast_shaders.py、fireblast_il.ps1；work/f005-r2-source-recheck |

最终1920实际Hit窗口wall .570719/.562237秒（cue .55），最大wall/batch漂移.001175/.000219秒；1280为.559181/.553719秒、漂移≤.001179秒。每帧Tint/Animate/Shake按age复算最大误差≤1.4e-6。此为**Feature数学分析，不是独立V005**。选择暂停后无补偿，2x实测2.000893、.5x .5000651（r7）。全部实际timeline/完整连续PNG、关键帧、contact sheet、声音能量CSV及PCM hash由`tools.fireblast_evidence --analyze`复算，official_fidelity_verified=false。

音频仍保存真实Listener原float32混音，48k双声道、1024×4 DSP：当前1920为891904帧/871回调/peak1.365077734/abs>1样本240；1280为891904帧/peak1.083602905/超1样本20；断连398336帧/peak1.066918135/超1样本28。没有改成PCM16或末端clip，abs>1不证明设备实际硬裁，也不等于原版听感；observer未挂录音Tap，不能称双方都录音。

失败全部保留：timing-red18212；visual-r1 PID5608 fixture固定.18秒未找到Missile；formal-r1拾错邻卡/held自身命中；r2退场早于observer完成（现双方ack）；r3尾端采样断言；r4完整窗口暴露真实时间异常；r5虽然窗口模型PASS仍有wall漂移，未当有效速率；scaled-r7预览错误使用最终友方目标（现预览敌英雄与最终目标分开）。最终自然按cb96引用范围，没有假称188bb又跑自然/旧五probe。

来源重复命令输出仅work，原安装/SQLite只读，不执行原Assembly：三Shader exact-DXBC audit与先前hash一致；Board ctor/ResetAmbientColor/RaiseTheLights、EndMulligan调用和RenderToTexture创建/清理的Cecil IL保留。原DLL SHA256=3F67779510122DBCE79EC431E4E7494643083160EC9E7592F6ECB899B2B69728。UnityPy未给出有效源Unity版本时有fallback6000.3.11f1警告，不能当原游戏版本。原GraphicsQuality动态绑定未恢复，RT采用显式medium长边采样，未声称原运行分辨率模式。

旧矩阵以下为上一交付历史，当前R2修复优先；旧KI/验收引用不升级为当前独立通过。

| 分项 | 原来源 / 已确认事实 | 当前实际 | 修复与证据计划 |
| --- | --- | --- | --- |
| 身份 / 文字 | 本机卡库HERO_08bp，Class4/Type10/Cost2，原动态描述 | 权威damage/cost→原中文TTF/TextMesh/富文本/名称RTT；普通法伤不误增 | 9规则专项及正式hover图；名称当前Mono45、原运行舍入Unknown；通用UberText并未全集恢复 |
| 原画 / 按钮 / 费用 | Card_Play_HeroPower；BigCard→GetBigCardActor→GetHistoryActor(type10) asset23/24；原原画/框/静态轮廓 | 每实体/详情/敌方独立图与原曲线；按钮和详情原cost文字 | r14完整GUI白环已消失，hover/非法指向费用2可见；合法箭头首段遮挡费用，未强制置顶/挪字 |
| 状态与输入 | 原11 ActorState材质，权威moves，源选择/取消调用 | 正式输入重放点击/拖动/右键/Esc、stale拒绝、pending移交、快速重选 | 最终1920/1280各27+3、DC10+3、低费7+2、双Player均自然exit0；真实自然整局与完整记录见文末 |
| 指示器 | 原Bullseye/Link、mesh/UV/Shader；15池与原抛物线/滚动/alpha | 本技能已替换程序箭头；真实source技能位置与pointer端点，合法才靶心 | r9友随从、r10缩放、r14敌英雄实读；当前authored camera+y0投影不是原运行layer9 hitbox/遮挡恢复 |
| 根控制器 | Super b8e7bb6e0944d204297efdc937967085，SourceAuto type10；原Start/TargetJoint分支 | NativeSuperSpell完整本技能Start→Action→missile→arrival→impact→None；未知其它分支仍拒绝 | 46选择/所有权检查；正式同实例Cast/Arrival/Completed、场景物理释放 |
| 起手 / 取消 | 原选择Birth、确认Action；Cancel4→Finish中Stop/Fizzle并列，Wait1只控制FINISHED | BeginSelection/Cast/CancelSelection有界API；hardCancel/Dispose维持立即释放、不新播尾音 | .05/1.5/3秒等待不按猜测Seek；普通AudioPlayAction及延迟回调实际执行/停止诊断 |
| 飞行 / 命中 | .25秒/Ease21/OrientToPath；TargetJoint查投射物自身、缺失根fallback；Impact Location6/零delay | 原独立克隆/曲线与源目标服务；到达/结束驱动正式表现释放 | 不再保留本技能固定Lerp贴图；完整官方屏幕几何/时序仍待可比序列 |
| 音频 | 原Start/Small/Impact六cue与按钮used/restore、CancelAttack；原Delay/Loop/Stop/volume/pitch | 实际AudioSource与Listener DSP float32捕获；本技能通用重复feedback已跳过 | r8/r9真实48kHz双声道、clip0/overflow0；r14单样本超1保留；混音非独立音轨听感/官方比较 |
| Shader / 粒子 / 动画 | 15粒子系统/原Light/原动画、family25-40实际依赖 | 23公式GPU、原mesh/纹理实物、直接UI目标绘制；31/32两种无主阴影分支SH.w+Lambert+索引附加灯已实现 | 四种阴影分支未恢复；原运行全局选支/灯输入/阴影服务Unknown，当前显式零主/附加灯。原plane受控bounce已验证 |
| 官方一致性 | 153媒体专项清点；两张敌方法师静态按钮参考，无所搜范围施放/指示器/音频序列 | 内部实物/GUI/DSP可复现，完整官方八分项未建立比较 | 不能内部自比升级V5/V6；版本/运行相机/HDR/原环境仍Unknown |

## 本轮直接读取

- `tools.fireblast_source --export` 只读CardDef及原类型树，专用`.fireblast-root.json`不覆盖Task2/3缓存。
- `local/runtime/fireblast-source.json`含CardDef、root配置、Arrow候选身份；root文件`21d7088c72887568b7d0f559.fireblast-root.json`。
- 原root组件 CAB `CAB-69c88b234dfa427e02d1fcee9337ae34` / PathID `-316560505458650560` / `essential_base_global-prefab-0.unity3d`。
- 原SuperSpell静态IL的OnBirth/OnAction/SpawnStart/DoStartSpellAction已读；原DLL仅Cecil静态证据，不执行。
- `local/ui-calibration.json`实测SHA256保持`E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6`。

## 状态

完整官方保真BLOCKED；当前R2本地五项修复经全新V005-r2独立有界PASS，报告docs/verification/V005-r2.md。当前188BB086新构建1920/1280/断连/低费与15回合自然双端终局通过，唯一受击、实际窗口/暂停倍率、56新增异常与输入边界及原共用合同均有独立证据；来源197字段/3个捕获入口、原像素及10段R2T DXBC新读。两次夹具错误、仅batch缺Event.current的精确Draw豁免、未跑范围见报告；正式GUI绘制证据另列。V重跑硬编码生成目录覆盖work/f005-r2-gpu，本轮以新log和work/v005-r2-gpu副本为准，不回填旧hash。原安装/用户图/校准未动。旧V005-r1只属历史；任何内部自比不能算官方Ground Truth，剩余原输入/分支与解除条件见Task。

## 已确认的选择与界面原链

- `InputManager`点选技能→`ActivatePlaySpell`→`Card.GetPlaySpell(0,true)`→根None时进入Birth；SuperSpell.OnBirth克隆Start并进入Birth。原起手在选择期，不能全部延迟到权威施放后重播。
- 确认后根Action经NativeSuperStart等待已有Birth完成，Idle时直接继续；Start与根必须是同一实例。选择不产生missile、Hit或power请求。新BeginSelection/Cast入口仅限此已审核技能，旧Begin与hard Cancel合同保留。
- 取消原枚举是Cancel=4：CancelTargetMode→DestroyFriendlyTargetArrow(true)→Card.NotifyTargetingCanceled→ActivateHandStateSpells(false)→SpellUtils.ActivateCancelIfNecessary→SuperSpell.OnCancel→Start.SafeActivateState(4)。Start Cancel→Kill Fireball→Finish；Finish的AudioStop/AudioPlay与Wait1秒并列OnEnter，不能把停止/尾音延后1秒。强Dispose仍立即释放且不新播取消音。
- 真正详情链为BigCard.LoadAndDisplayBigCard→ActorNames.GetBigCardActor→GetHistoryActor（CardType10）→asset23/24→History_HeroPower/History_HeroPower_Opponent。不是由资源名猜用途。标准按钮来自Card_Play_HeroPower完整Actor，原HeroPower_Used/Restore为.533333/.433333秒。
- zhCN FontDefinition解析：Belwe_Outline→GLEI00M_t_Outline（名称/费用，字号modifier1.15）；FranklinGothic→BlizzardGlobal（正文，行距modifier1.25）。两者原TrueType glyf数据/本卡字形本机完整，Outline差异还包括原材质/UberText配置，不仅是TTF。
- 原箭头PreloadTargetArrows明确加载Target_Arrow_Bullseye与Target_Arrow_Link；头/靶心/段的原mesh与UV可取。15段池、1.2间距、1.5抛物高度、.5滚动；本地目标来自layer9射线，合法目标仅控制bullseye Renderer，技能锚点是技能实体（useHandAsOrigin=false）。完整源公式/字段已保存在work/r005-indicator/REPORT.md，F005仍须正式接入与验证。

## 中间失败与修复记录

| 隐藏Unity进程 | 结果 | 实际证据与修复 |
| --- | --- | --- |
| PID2148 / f005-initial.log | 自然exit1 | 新代码缺System.Linq编译失败；已补，不作运行通过 |
| PID17756 / f005-lifecycle-r2.log | 自然exit1 | 原Start Death2停留，pending0/completedTrue但active1；沿原AnimateFloat.OnUpdate IL确认缺finishEvent，不加猜测超时 |
| PID12512 / f005-lifecycle-r3.log | 自然exit0 | 恢复曲线结束事件后Start/Small/Impact三实物克隆→None、pending0/clones0、controller unbound0；实际物理对象释放。仅确认后启动原链的中间检查，不覆盖选择/取消、完整GUI/音频/保真 |

原安装Resources/unity default resources实际存在（5923140B）；Importer原先直接拒绝内建PPtr，本轮补精确资源读取。专用图已恢复原10207网格、依赖errors0，不以程序Quad代替。

## 官方参考专项清点

R005实际清点E:/HS/references、local/visual、tests/visual/reference含忽略文件共153媒体，全PNG、无视频/音频捕获。确有两张官方静态参考：oracle/interaction/02-board-exhausted.png、03-power-ready.png（1924×1127）上方敌方法师的2费按钮；下方绿色ready是战士，不能迁移到本技能。04详情为战士、pacing技能为潜行者。18张oracle实读/contact sheet，无本技能施放/指示器序列；PNG metadata空、版本/时标Unknown。可用静态参考会用于按钮比较，不能由此声称动态一致。

## 历史阶段记录（当时未完成项以后续矩阵为准）

### 2026-10-18恢复后的增量实测

- PID5512 / `work/f005-lifecycle-r6.log`自然exit0：原Start/Small/Impact控制器、普通AudioPlayAction、SetLightIntensity/EnableBehaviour/SetAlphaRecursive及AnimateFloat.finishEvent接入后，确认施放六cue、三克隆→None、pending0、实物释放；runtime未绑定audio/property/component/ignored均0。音频回调日志不等于DSP输出。
- PID19796 / `work/f005-selection-r1.log`自然exit0，早期计数2561含逐帧Prepare检查，已改为明确失败即抛出，不能拿大数字作覆盖率。后续实际PID17480/31824均重新跑Selection，真实38项，包括选择时无飞行/命中、等待.05/1.5/3秒后同实例确认、取消.01/.6/1.8秒、强Dispose不新播延迟声音。音源状态由显式服务模型提供，实际声源/输出捕获仍待做。
- PID17480 / `work/f005-visual-r1.log`自然exit0，首次15项局部GPU/清理检查。实际读图发现仅靶心，原头/段Renderer初始disabled未由Show开启；原先Any非黑像素断言不足，未将此轮算完整箭头通过。修复Show启用真实Renderer并加非法目标仍有头/段像素断言。
- PID31824 / `work/f005-visual-r2.log`自然exit0：16项局部GPU/物理清理+Selection38项，实际读`work/f005-ui-gpu/arrow-legal.png`确认头/靶心/滚动段都可见，非法目标仅隐藏靶心；15池、短距离/零距离、屏幕边缘、关闭相位与全部原克隆/投影Camera释放。标准完整按钮正/背面、真实详情/敌方详情及原文本节点投影出图；同CardID双方独立实例/RT/姿态不串状态。这不是正式GUI验收。

`NativeTargetArrow.sourceCamera`读取既有`original-battle-cameras.json`的Main Camera authored Transform/FOV/near/far；来源应回溯`tools.battle_camera_source`与其清单，而非把当前项目Camera当原资源。该原相机姿态用于显式棋盘平面投影（GUI设计点→ray与y=0平面交点），不是原GameLayer9场景hitbox/遮挡已恢复。原15段几何公式已接，原实际相机的运行时变更、原hitbox/完整GUI深度仍Unknown。

`NativePowerVisual`新增完整标准button/detail/enemy薄图，原mesh私有`.fireblast.mesh.json`包含UV1及作者法线，不覆盖Task2/3缓存。每实体/视角/详情独立材质和翻面状态。原Used/Restore quaternion/position/scale曲线直接采样，忽略编辑器EulerHint；固定正面取景避免每帧包围盒抵消动画。原静态Hero_Power_Silhouette、11个ActorState材质、取消CancelAttack和翻面音轨已接，正式状态GUI/点击仍待验。

Shader源：`work/r005-indicator/SHADERS-UI-IMPACT.md`、`POWER-SHADOW-HIGHLIGHT.md`及`shader-audit-extra.json`。family25/26/27对应箭头TransparentColor/Texture/Cutout；28/29对应双UV Portrait/TextureColor，30为Mana滚动，33复用已解析Highlight数学，34为原DstColor/Zero阴影（实际_Color.rgb=0时输出tex.rgb，不是tex*Color）。均需独立浮点GPU公式样本，现仅实物GPU出图。31/32两个Diffuse当前只有SH环境输入的局部接入，不等于原全局光/附加光/阴影分支恢复；原运行keywords/动态灯尚Unknown，不能标完整。原TTF已导出并加入构建列表，当前GUI文本经验字号将继续替换为原TextMesh参数，字体渲染尚未验收。

本段为当时排程；后续已接入正式GUI/DSP并提交0af5556，当前剩余见顶部矩阵及最新增量，不以旧文字重复造系统。

### 字体与正式运行入口增量（2026-10-18）

- PID6580/r3、30528/r4均自然exit1：旧断言预期名称46，实际原字段与同顺序IL在当前Mono得到45；费用74、正文40、正文characterSize .0525、行距 .9375。不以猜测Round修成46；原运行数值仍Unknown。
- PID31608/r5自然exit1：局部变量definition重名编译失败，已改materialDefinition。PID26712/r6自然exit0：20项局部GPU与38项选择检查，实际读取原TextMesh详情/动态伤害图。名称RTT、普通/粗体描边材质、原TTF与节点布局已接入；不代表整套UberText通用换行或原全屏保真。随后修正GPU fixture基础Cost=2，尚待新图确认。
- 真实Match.apply专项9项通过，含友敌英雄/随从、非法/不可选、普通法术伤害不增英雄技能、已有英雄技能伤害与费用修正。显示damage来自权威快照，费用来自既有cost，不借UI修改规则。
- Windows构建r1 PID15268自然exit0，业务DLL SHA256 `0AAE0B48DAD8C1F01D2131192AD6AD19DC9EEFA09914B24B05C93ADBE08B967C`。其后新增正式输入重放、pending后晚取消保护/拒绝尾音与PCM缓冲修正，不能沿用r1作为这些修改的运行证据；r2 PID18548已启动，结果待记录。
- `tools.fireblast_evidence --run-players`只替换有界初始局面，随后使用正式Server/Match.apply/Connection/ClientApp.HandleBattleInput。两标准法师同屏一方已用一方可用，分别hover；实际右键取消/非法点击零请求、实际stale版本拒绝、快速取消重选、确认后同实例移交和场景强释放均在诊断入口中。入口已编写，正式结果尚未产生。
- `FireblastPcmTap`仅显式重放参数启用，确认本进程唯一Listener和实际Stereo配置。OnAudioFilterRead只复制到预分配128块并入队，主线程写float32 WAV及统计rate/channels/buffer/callback/overflow/peak/RMS/clip。它是实际引擎混音采样入口，尚未执行，不能当成DSP通过或听感证据。

选择API/箭头/标准按钮与字体已接入、局部检查如上；正式GUI、实际DSP、局部Diffuse/碰撞证据、共用Task2/3与五轮旧反例、新构建后双端整局及最终来源/DB/交接仍待完成。整Task保持IN_PROGRESS，不自标独立通过。

### 正式GUI/DSP首次失败与定位（2026-10-18）

- r2构建PID18548自然exit0；正式r1 driver5048/observer9268实际TCP配对、非法点击/右键取消/真实stale拒绝均执行。driver在接受后原选择图没有Cast而失败；两个Player退出native crash，完整保留`work/f005-formal-r1`。DSP已实际录得48000Hz、2ch、504回调、516096帧、peak .762995、RMS .082175、overflow0，不是合成或cue模型。此轮没有完整接受施放六音轨，不能称DSP完整通过。
- 首轮ScreenCapture请求后同帧立即移动指针，文件名legal但图实际已是非法屏幕边缘；修为冻结一帧再操作。r5合法图实际读到从技能按钮至敌方英雄的原箭头，r5 hover详情实际包含原名称/费用/正文。首轮错误图保留，不把非黑图当端点通过。
- r3构建PID8196、r4构建PID2844、r5构建PID27508自然exit0。formal-r2/r3/r4在入队前失败；r4 boot明确initialized=True/page=queue/status=connection refused。启动器固定sleep1早于权威Server导入/初始化与listen完成，已改为实际start_server完成后写本轮ready文件再启动Player，非延长超时。
- native index已有HERO但旧effect-index没有cast_duration，原Duration返回0导致正式Presentation不建源cue；新非零调度窗口仅启用原链，原到达/结束仍由真实Flow/Busy决定。进一步发现正式JsonUtility将缺card_id读为空字符串，原仅`??`未回退实体身份；改IsNullOrEmpty并加空串PlayHeroPower用例。
- formal-r5 driver22152/observer34580自然exit1（非native crash），DSP48000Hz/2ch/500回调/512000帧/overflow0/clip0；原根仍未成功移交。formal-r6进一步真实trace为Action/StartAction在1.219614、MissileClone/Birth在1.223776，随后同一时刻Cancel；cast=True/arrival空/completed=False/cancelled=True，不能归因等待不足。正在追正式owner释放/rewind，保留反例。
- 退出缺陷分别为NativeEffectInstance对已销毁Root.SetActive、LiveActor对已销毁Camera.targetTexture，以及Connection后台catch在Unity关闭后调用JsonUtility引发native crash。已做fake-null/幂等清理及纯托管JSON转义，OnApplicationQuit/OnDestroy共享一次资源释放；新正式双端尚待自然exit0确认。observer的flush完成等待移到phase guard前，避免driver退出导致finished快照阻断等待。
- r6构建PID21508自然exit0；formal-r7正在验证首次取消来源日志。白色技能高亮环仍需按原静态RGB轮廓、材质和C/K实值核验；不能以另绘遮盖或当前实物图宣称官方一致。25项`tests/test_fireblast.py tests/test_match.py`实际通过。

### 首次有界正式闭环通过（r8）

- r7定位为NativeEffectPlayer.Instance的rewind回收：requested/current以R格式均为1.2624979却进入`<`分支（Observed）；跨时钟age+CastOffset的Mono扩展精度是解释（Inferred）。改为保存/比较同一Presentation输入age，保留真实输入倒带的回收语义，不延长超时或加猜测容差。新同帧双Prepare回归已编写，待专项执行。
- r7构建PID35612自然exit0；`work/f005-formal-r8/run.json`固定DLL SHA256 `04F712AA68E30934DC079018EAFAD812B529BE364AD9EA02144C911AA1F63F51`。driver8916/observer35536均自然exit0，25+3项有界正式GUI/input/DSP检查通过，双方publicHash `65b1286cccefc17936ed18877b4346956d0e8d6ddfb2cfdfdcadbc7ad778c3df`；server34656仅由launcher退出清理，exit1明确不是自然终局。
- 同一root在权威接受后一次Birth/Action、真实Arrival/Completed，真实源动作/音轨绑定0缺项；持有选择无飞行、非法点击和取消零正式请求、真实stale拒绝无状态变化、快速取消重选、pending晚UI清理保护及场景实际释放均在正式路径检查。observer同CardID不同used与hover另有完整GUI。
- 实际Listener混音48000Hz/2ch、DSP1024×4、500回调、512000帧（10.6667秒）、peak .94217、RMS .089377、overflow0、clip0；本轮实际接受后的原音轨已播放，cue与stop记录在report.json。原PCM资源本机不提交。没有官方可比音频，不声明听感一致。
- 实读r8合法敌英雄箭头与详情图（基础文字不含$）、05-cast伤害命中图。`06-power-used.png`仍在同帧Capture后ResetMenu而拍到菜单，已发现并将冻结一帧修复；不得把其命名/25项检查算已用状态截图通过。退出日志无此前Native crash/Root/Camera异常。窗口1920×1080，缩放边界/友方随从/拖动Esc/断线与真正倒带及共享根回归尚待完成。
- Task仍IN_PROGRESS。此稳定点仅实现checkpoint，未独立V005，未完成原光照/白环数学/全部GUI边界/正式自然终局及官方比较。

### 2026-10-19原色域、目标格式与正式屏幕修复

- `work/f005-formal-r9`：driver34852/observer7672均自然exit0，27+3项、1920×1080；友方雪人真实target/拖动释放/Esc/原已用背面、扣费10→8及伤害5→4均实读。DSP593回调/607232帧/peak .904386/RMS .085969、clip0/overflow0。r10同构建1280×720、敌英雄目标、27+3、双自然exit0。这两轮还含旧高亮输入错误，不作为最终高亮保真证据。
- Shader独立数学样本PID34312自然exit0，18项（`work/f005-shader-r1.log`）：25 alpha平方、26忽略_Color、27 ceil且无clip、28双UV/min、30时间/遮罩、31 B0有限输入、32 alpha、33非线性vertex red、34 DstColor/Zero。来源及有区分度输入见R005 `SHADER-SAMPLES.md`，不是同候选图反推预期。31/32完整光照分支未被这些样本覆盖。
- visual-r7 PID16528虽然22局部/46选择检查通过，原Highlight与候选Float C/K数学相等，但peakC=6.541378E9/minK=-81152不属原单片元合法域，不能据此认定正确。原pPlane2 COLOR channel3/stream0/offset28/format2/UNorm8，32顶点仅red0/255，UnityPy MeshHandler保留原字节；旧专项导出错误将255直接作为Color(float)。新`mesh_colors`按源format2除255，format0 Float32保持原值（包括HDR255），未知format/非法字节明确拒绝，禁止按max猜测。
- UI缓存schema变为`source-channel-v2`，重导命令`.venv\Scripts\python.exe -X utf8 -m tools.fireblast_ui_assets --export`；仅更新专用`.fireblast.mesh.json`及本技能清单，不覆盖旧Task2/3。原始原色/格式依据和参数包络见R005 `HIGHLIGHT-INPUT-DOMAIN.md`。正确色域下实际GPU peakC29.60489/minK-2.626575；原PS alpha仍可>1，未用clamp伪装。
- 组合回归PID3548（不是独立visual-r8 PID27464）自然exit0：23局部GPU/46选择，含严格真正倒带与同帧Prepare/Draw；未改归档V003五探针分别34/44/44/43/66、fail0，另正式NativeSuperSpellChecks.RunFinal正常共用链/源命中/停止/历史宿主/30真实规则双视角通过。入口`FireblastRegressionChecks.Run`仅反射顺序调用；归档源临时复制后已移除。没有声称全量Python重跑。
- visual-r8 PID27464自然exit0：同源mesh/state/Shader在ARGB32直接绘制与Float C/K重构背景D的maxRGB差.490196049。原authored相机m_HDR=false，仅支持非HDR实验，原运行是否改HDR仍Unknown。UNorm混合每层夹取造成非线性，不能在最终C/K上一次clamp还原。DEC-007限定本技能按钮/详情的直接framebuffer分支，共用FX/其它卡/Task4路径不改。
- direct-r1 PID17548自然exit0：25局部/46选择。新`NativePowerDirectDraw`复用原Renderer/material/mesh/source投影，独立深度、仅清自己depth、按widget裁剪，原混合状态临时恢复并在finally还原；原Shader直接ARGB32与生产CommandBuffer实物误差<.015。离屏通过不代表screen通过。
- formal-r11失败：screen color与RT depth直接混绑非法，旧日志/无report保留，只清理自建Player28636。r12 CameraTarget、r13 CurrentActive均实际落到拍卡320×384目标，而depth为screen1680×1050，两个Player自然exit1。没有以替换builtin名字算修复。
- build-r12 PID31632自然exit0，formal-r14实际修复为`ClientApp.OnGUI`最早Repaint入口、任何拍卡前保存GUI color/depth/RT；准备完成后显式恢复这组buffers，再绑定CurrentActive+自有depth。首8次native buffer追踪entry/afterText均screen；无混绑/尺寸异常。driver3396自然exit0/27项、真实白环消失且箭头正确；observer25940因完成文件读写Sharing violation自然exit1，整轮FAIL保留。双方公开hash相同；写方改临时文件关闭后原子Move发布。
- r14实读01 hover、03非法水平指向：按钮费用2正常可见；02合法敌英雄箭头首段覆盖费用徽章位置，未为截图强制置顶或移字。原箭头层次映射当前GUI，完整原场景遮挡仍Unknown。DSP float32格式3直接保存609280帧/595回调/48kHz双声道，peak1.00089657、abs>1单样本1、overflow0；该统计未写PCM16/未clamp，不宣称无超限或官方听感通过。
- build-r13 PID28020自然exit0；formal-r15取消固定两秒断言失败，实际取消音/Stop Loop/Fizzle已执行，未到None；observer原等待未收到失败退出而超时。新的证据入口按源实际None推进、仍保留整轮90秒上界与失败时源相位trace；不修改源等待或强制完成。正式新结果待记录。
- 针对Python106项通过（fireblast/source/match/network/native_effects/presentation_metadata）；五色域codec专项覆盖Float32不重复归一化。四大型旧原源重读不在本轮；不以历史78838内部断言计数冒充本轮全量案例。

上述为该轮排程；后续结果如下，不用旧待办掩盖当前交付。

### 最终代码与运行证据（2026-10-19）

- r16 direct正式双端driver18972/observer4092自然exit0，27+3、同hash65b1286c…，实际1680×1050；已读已用背面/扣费/敌方详情与后续GUI，没有白环。DSP594回调/608256帧、peak1.1306226/RMS .0859169、abs>1为6、overflow0，保留原float，不声明这一轮无超范围。
- 断连driver31132/observer32600自然exit0，10+3：真正TCP传输关闭→Connection断线→未Cast选择硬取消，晚回调不新播、Loop停止、请求数不增、场景物理对象/音源全部释放。低费driver3604/observer32696自然exit0，7+2：真实快照1费/原2费技能无power合法moves，正式点击不选择不发送。缩放driver5192/observer31600自然exit0，27+3，实际1280×720，友方随从target、拖动/Esc与原箭头端点实读。
- 碰撞r1 PID2340 exit1是受控测试未激活原始inactive Rocks01；r2 PID19676自然exit0。原impact的Rocks01引用node11 Collision plane，Planes/3D、dampen .4/bounce1/loss0；受控激活一个原粒子，对照只变collision.enabled，观察法向速度-9.939138→+5.571338，原图/plane物理释放。没有把受控激活当控制器原始启用时刻；FireImpactRing原六plane全null，不造碰撞平面。
- 自然整局r1 driver17368/observer4788自然exit0，正式原Match、无fixture篡改，turn15/version52、同hash3144f451…，双方真实finished。journal含1次power/18攻击/17出牌/14回合结束/2换牌，error0、Concede0。实际两端1920×1080终局PNG已读，媒体索引含各SHA；此轮不是DSP录制。server22588在两Player自然终局退出后由启动器清理，exit1与自然终局分开。
- 31/32可做代码缺口已收敛：NativeDiffuseInputs显式传主灯方向/色/visibility、全局count与renderer索引、灯位置/色/atten/spot；family31/32实现原3/15在PS求附加灯、5/17在VS求附加灯后插值，主Lambert仍在PS。逐条读原ASM确认gamma只用SHAr/g/b.w，纠正旧方向ShadeSH9；原不读meshCOLOR/_Color。原全局数组/indices/range→atten来源尚未取得，当前宿主显式零主/附加灯、PS无主阴影策略，report明写，未猜白灯或将原local Light保留等同原数组映射。
- Shader-r2 PID29200自然exit0，23项（18旧+5新的完整条件数学）：索引取第2灯、count min、directional与point的原atten/spot、主光visibility、Custom B混合、Hero alpha/tint不参与，以及PS点灯与VS插值产生不同结果。未实现四种主光阴影分支/原阴影图、级联与原实际选支；这仍是完整保真缺口，不能说Shader全集恢复。
- build-r15 PID28056自然exit0；新DLL SHA256 `F05E9C2BE77AD0E5E9D0C94A1849A54259E7B4AE06A76263868E99811D56A405`。final-gui-r1 driver17252/observer17116自然exit0，27+3、同hash65b1286c…，真实1920×1080：显式证据入口只对本Player调用SetResolution并等待帧末实际匹配。1920已可运行，旧1680不是外部阻塞。DSP598回调/612352帧/48kHz双声道、peak .9065204/RMS .0861651、abs>1为0/overflow0；源WAV/各cue/stop/元数据与10ms能量、contactsheet在该目录。
- 最终局部PID2508自然exit0（`work/f005-final-visual-r1.log`）：25GPU/46选择、原mesh/state/sourceLDR对照、材质恢复、字体/详情/同CardID独立实例、箭头及物理释放；包含新Shader后的实物。实际动作覆盖日志列ActivateGameObject9、AnimateFloat6、AudioPlayAction3、AudioPlayClipAction1、AudioPlaythroughAction2、AudioStopAction1、EnableBehaviour2、iTweenScaleTo5、MainCameraShakerAction1、ParticlePlay11/Stop13、PlayAnimation1、SetAlphaRecursive1、SetLightIntensity5、SetProperty2、SetScale5、SetVisibility27、SpellFinish4、SpellState11、Wait5。本技能正常调用unhandled/audio/component/missing playback0；这不是全部未走分支覆盖。

当前真实差异：静态官方03上方敌方法师按钮的原画更亮、按钮相对英雄更大；当前原资源按钮的投影/灯输入与该图没有同条件校准，未算像素一致。官方03下方绿色战士和04战士详情不参与本卡验证。官方版本/原运行相机/layer9 hitbox/遮挡/HDR目标、全局光与阴影选支仍Unknown；可比本技能选择→取消/确认→飞行/命中/结束视频及音频未取得。文字本卡排版已实现，原通用UberText wrapping/corner算法与原运行名称舍入仍未全闭合。以上与已完成本地程序交付分开，不自封完整还原。

- 同最终DLL `F05E9C2B…` 的 final-scaled-r1 driver15368/observer5852自然exit0，27+3、实际1280×720、同hash5bf800cf…，友方随从端点/费用与后续UI已读。DSP594回调/608256帧、peak1.1311495/RMS .0852789、abs>1为6/overflow0；原float保存，单独记录该轮超范围，不拿另一轮clip0覆盖。
- final-natural-r1 driver12952/observer5256自然exit0，原Match无fixture、14回合/version41、同hash `4cf2a6e9fcb6f9e6fe3807d35459ba2fc418069d3b700140ff63b57e29a1f718`；journal为GAME_START1/换牌2/出牌11/power5/攻击10/结束回合13，error0/Concede0。两端实际1920×1080终局PNG和源日志已生成媒体SHA索引；原server30376仅在终局之后清理exit1。不是用Concede/transport close当自然结束。
- UI原色缓存`165a212ee0cb87359589bb7c.fireblast.mesh.json` SHA256 `50669C5525232680D634E72ABEA8BE891A9F45BD42EDA53A20A4433945B3DFAA`，schema/source-channel-v2；旧命中缓存不能继续上传0..255。校准SHA仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

整Task现BLOCKED（完整官方保真），本地交付已由独立V005-r1复核通过，详见顶部状态及报告，不是100%恢复。最小解除条件见Task遗留问题；无可比视频/原声或原运行服务输入前，不以候选自比/数学GPU通过推断官方一致。已实现的两种无阴影源数学与仍未恢复的四阴影/服务分支明确分开。

## 第二轮用户复验 Feature 交付（2026-10-20）

状态：Feature局部复验`READY_FOR_REVIEW`，等待独立V005-r3；下方证据仅代表本地实现，不覆盖旧V005-r2的固定范围，也不解除整Task完整官方保真阻塞。

- Small missile的五个来源粒子节点用本技能专用World simulation-space policy；Start/Impact/Frost Nova/普通Particle保持旧映射。来源没有TrailRenderer；实测飞行拖尾来自其来源粒子。实现限定在 `NativeSuperSpellPlayback` 对实际missile节点调用时，不全局翻转 enum 映射。
- 命中数字沿权威Hit调用DamageSplat来源ActorSplats表现，hero和随从路径都只出单个权威数字，不叠加自制Presentation文字。r10双端1280×720真实GUI中友方随从7→6、Hit一次；observer命中接触表见`work/f005c-formal-minion-r10/observer-cast-contact-sheet.png`。
- 该接触表的350ms截图节拍没有捕获飞行约0.252秒窗口，因此只加了两张review-driver定点采样图，正式1280棋盘GUI实际可见。新补充`work/f005c-formal-flight-r1/driver/review-flight-mid.png`与`review-flight-late.png`对应flow `MissileBirth 1.253640`到`Arrival 1.505708`，均在Arrival前；后图可见沿棋盘前进的火球与来源尾迹。对应BuildClient成功的build-r16只变更review采样，未改表现逻辑；报告与图SHA见[Feature第二轮交接](handoffs/F005-task-0005-round2-review.md)。
- 音频已确认两个原Prefab source action都各播放一次同一44.1kHz clip，相隔18.04ms、时长2.069s，运行音量0.4/0.5；两个动作近乎全长重叠。r10 Listener实际48kHz双声道float32混音peak .9420093、零clipped sample；事件时间及10ms波形能量归档在`work/f005c-formal-minion-r10/driver/timeline.json`和`scene-mix-energy.csv`。没有原游戏录音和客户端版本，故听感是否修复不能客观对比，保留用户听审，不主观删cue/改音量。
- 飞行frame补充run的DSP有168个float样本绝对值超过1，明确不作为无clipping声音证据；声音结论限定为r10音轨采样。完整官方轨迹、音效听感、原版本/相机遮挡与全局光照仍未知，不声明完整一致。

缓存迁移收尾：新增NativeFireblastCache共用只读header校验，NativePowerVisual与NativeTargetArrow两个入口在分配CommandBuffer/Camera/克隆前都拒绝非source-channel-v2，诊断只提示重导本技能。按钮音源描述读取前移到GPU资源之前；箭头projection进入try/Dispose范围，独立Create的Seek失败也释放实例。build-r16 PID27588是单按钮入口中间构建，不作最终覆盖；build-r17 PID5268自然exit0，同进程12项受控检查：缺schema/v1两入口明确拒绝、无新增GameObject/RT；合法header但缺音源/箭头图时部分构造也释放。最终DLL SHA256 `1F7958626A93B45E590D5C78DAEDFAE38FAC29BA36846B2C3822F29A7B2A625B`。本次只改缓存校验/异常清理，前述F05E新Shader整局与1280按其固定DLL有效路径基线引用，不称重复运行；最终有效缓存正式GUI另记。

最终有效缓存正式GUI：`work/f005-final-cache-gui-r1`，driver23564/observer10860均自然exit0，27+3、两端1920×1080、同公开hash65b1286c…；原按钮/描述/合法敌英雄箭头实读，无白环。DSP实际48kHz/双声道float32、608256帧/594回调、peak .94210094/RMS .08513788、abs>1为0/overflow0；analyze生成evidence-index.json、contactsheet与10ms能量CSV。退出资源检查通过；本轮所有自建Unity/Player均已退出，未终止用户进程。BuildClient只重建Main.unity生成fileID，已按已核差异手动恢复原编号；无场景业务/用户校准改动。

## 用户复验补充：结算延迟根因与复用流程（2026-10-20）

- **根因 Confirmed**：`Presentation.Accept` 把 HERO_08bp 的 Hit Cue 时长设为 `4.1s`，并用同一 Cue 播放原伤害飞溅/数字。`Presentation.Busy` 在批次结束前始终为真，`ClientApp` 的目标/出牌/技能/回合结束入口均检查它，因此数字留屏时长被误用为整局输入锁时长。权威状态并未等待动画：正式随从局权威血量先从7变6，显示血量仍为7，直到原生 Missile Arrival 后才只触发一次Hit并更新显示。真实GUI时间线中 Birth→Arrival 约`0.252s`，主要多余等待来自约`4.1s`的Cue尾段，而非服务器或飞行控制器。
- **修复边界**：若直接缩短Hit Cue，会同时截断DamageSplat。后续应将短时交互/批次锁与独立装饰飘字生命周期分离，并用真实GUI验证下一操作能及时开始、数字仍按自身曲线淡出；不得改服务器即时结算或猜测原版输入解锁的精确帧。当前只完成调查和记录，未声称代码已修复。

### 可复用的动画与特效恢复方法

此案例的调查方式可加速其他卡牌，但源字段、时长、音轨、投影及目标策略必须逐CardID/Prefab核实，不能把本卡参数横向复制。

1. 固定CardID与入口，沿真实调用链追到Root Prefab、FSM/动作、来源音频和Authority事件；资源名只作为线索，静态IL解释行为，实际运行证据确认当前执行路径。
2. 先建事件时间线：输入、Birth/Start、MissileBirth、Arrival、Hit、Death、Native Busy、批次完成及服务器版本/血量。把服务端结算、网络、表现队列和资源生命周期分开，定位等待后再调时长。
3. 追完整生命周期与坐标：创建/克隆、父子层级、Source/Target解析、局部/世界空间、运动/旋转、命中生成、音频、结束通知、取消和物理释放。只恢复一个Prefab画面不代表整条技能链完成。
4. 清点实际参与的Renderer、Mesh、Material/Shader、RenderTexture、ParticleSystem、Trail、Animation/FSM与AudioSource；逐项核原字段到Unity运行值的映射，重点验证模拟空间、时间单位、相机投影、遮罩和合成。禁止按名字猜、用Quad/放大/加亮掩盖缺失。
5. 用正式输入和真实客户端分别采Source/Target、敌我双方的Birth/Early/Mid/Arrival/Hit/Death关键帧；局部GPU图只能回答组件问题，完整GUI才回答合成问题。音源文件/Prefab调用和Listener混音分开记；静态截图不能推出时间或听感。
6. 规则状态与纯装饰分开：权威事件控制何时应用显示伤害，装饰自身控制何时淡出；任何Busy/等待都要注明被等待的具体信号、取消/失败出口，以及它锁住的操作。
7. 先复现旧失败再最小修复，保留红绿证据；覆盖敌我视角、目标类型、分辨率、取消/非法输入、死亡和断连。明确区分执行者证据、独立验收、本项目内部对照和官方可比素材。

可复用资产包括来源清点工具、SuperSpell时序/释放合同、已认可目标指示器和正式GUI帧采样入口；上述流程见[NATIVE_EFFECTS](NATIVE_EFFECTS.md)的原Prefabs边界与[视觉兼容验收标准](visual/VISUAL_COMPATIBILITY.md)。

## TASK-0006 输入锁增量（2026-10-20）

用户认可的命中表现尾段问题已在TASK-0006中修复：短Hit反馈和玩家可操作窗口独立于纯DamageSplat寿命。法师正式r3 GUI确认操作恢复时飘字仍可见，后续End Turn经生产输入/发送链成功，飘字后续独立过期。代码检查另覆盖`Busy=true && InputLocked=false`时NativeEffect清理仍在继续；实际双端GUI采样时`Busy=false`。r3有界结果、1280截图与边界见[`F006交接`](handoffs/F006-task-0006.md)。

此修复不扩展法师原生特效完整度结论；官方实时声音/完整运行对照仍Unknown。全职业来源差异与余下10项Blocked/Unknown见[`基础英雄技能来源矩阵`](HERO_POWERS_SOURCE_MATRIX.md)，独立验证由V006完成。
