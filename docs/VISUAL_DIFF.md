# 表现纵切片差异与验收

原布局分发：加载/手牌准备和区域回调顺序已验证；真实ZoneHand姿态、Tween和原布局图尚未接入。当前正常手牌保持原项目实现，本节点不计为新画面还原，见[ORIGINAL_ZONE_LAYOUT](ORIGINAL_ZONE_LAYOUT.md)。

区域完成信号：实际列表可接任务区域接口，等待与回调顺序通过检查；实际手牌/随从移动布局、ZoneMgr和动画未接通，不计为新增原卡画面，见[ORIGINAL_ZONE_CHANGE_LIST](ORIGINAL_ZONE_CHANGE_LIST.md)。

原任务等待：完成和范围/区域回调时序已验证，缺失原服务不伪造完成。正式游戏尚未接入完整GameState/ZoneMgr和原历史，因此不计为新原卡动画画面验收，见[ORIGINAL_POWER_EXECUTION](ORIGINAL_POWER_EXECUTION.md)。

原历史目标收集：明确输入能驱动真实播放器来源/目标，源缓存借用切换已验证；提供器默认关闭，正式对战仍走SemanticEvent，完整原任务、攻击与整场合成未完成。没有新增整卡画面验收，见[ORIGINAL_POWER_TARGETS](ORIGINAL_POWER_TARGETS.md)。

原目标集合：明确多目标ID与BestTarget选择已有实际模型验证；正式客户端仍输入单一SemanticEvent.target，完整元数据及攻击合成未接通。本节点未新增原卡整体画面验收，见[ORIGINAL_SPELL_TARGETS](ORIGINAL_SPELL_TARGETS.md)。

原SourceAuto：实际五类模型可供来源选择，攻击Prefab无需手写location解析器即可定位；事件桥接与召唤回归通过。正式源/目标空间、攻击附属生命周期与整场合成未完成，未新增全卡画面验收，见[ORIGINAL_SPELL_OBJECTS](ORIGINAL_SPELL_OBJECTS.md)。

原Spell定位：四攻击Prefab显式来源绑定的transform_gaps降为0；实际源父级切换/释放验证通过。正式SourceAuto/目标/区域选择、冲刺与合成仍未恢复，不能把定位检查当作新增完整攻击画面，见[ORIGINAL_SPELL_TRANSFORM](ORIGINAL_SPELL_TRANSFORM.md)。

原攻击事件声音/等待：四真实Prefab离线检查已接入攻击声与跨帧恢复；声音静音验证未人工听音，正式对战攻击资源、原来源/目标移动及完整合成未完成。本节点未新增全卡画面验收，见[ORIGINAL_ATTACK_EVENT_AUDIO](ORIGINAL_ATTACK_EVENT_AUDIO.md)。

原攻击Prefab：离线实际状态链与原粒子回调已验证，正式双客户端攻击附属实例仍未启用；声音/目标同步/合成未完成，不能把57事件检查当作全卡视觉验收，见[ORIGINAL_ANIMATION_EVENTS](ORIGINAL_ANIMATION_EVENTS.md)。

原 SpellState：23 源状态的显式播放/清理已验证，51 条绑定解析；正式攻击/取消/死亡状态选择及原动画事件未接通，因此不计为新画面还原或新增完整卡牌效果。见[ORIGINAL_SPELL_STATE](ORIGINAL_SPELL_STATE.md)。

原Actor附属显隐：本地法术表规则与215原组件绑定已恢复调查；正式SpellState播放/停止、ownedSpells/护甲/阴影联动未接入，四入场图仍各2副作用缺口。本节点没有新增画面还原验收，见[ORIGINAL_SPELL_VISIBILITY](ORIGINAL_SPELL_VISIBILITY.md)。

原CommonFX无Ramp RGBR：不再走通用替代公式，源Shader及实际关键词核对通过；66帧只有11程序可见，另外11程序空白，部分黑alpha仍明显。原目标/生命周期、完整合成及原后处理未完成，见[ORIGINAL_COMMONFX_NONE](ORIGINAL_COMMONFX_NONE.md)。

原亮度遮罩粒子：恢复独立顶点RGB/亮度alpha/Glow，18帧诊断显示五程序可见、一程序空白，部分黑alpha仍在；正式目标合成、后处理与完整动画未完成，见[ORIGINAL_COLOR_ALPHA](ORIGINAL_COLOR_ALPHA.md)。

原透明粒子：修复Zero/Zero占位导致的消失，恢复原TintColor、独立RGB/alpha增益和Glow。完整目标场景及后处理未完成；部分射击离屏仍有黑alpha区域，未增加整效果启用数，见[ORIGINAL_TRANSPARENT](ORIGINAL_TRANSPARENT.md)。

原Additive粒子：恢复基础TintColor与独立Glow，避免错误双重相乘导致的暗淡/消失。地狱烈焰离屏中心仍过亮，后处理、目标场景和完整画面合成未完成，没有新整效果启用，见[ORIGINAL_ADDITIVE](ORIGINAL_ADDITIVE.md)。

原入场动画末段音效：原协程在Anim退出后继续，旧适配器丢掉延迟第二声；现四组完整图均在Actor Visible播出第二声。原角色附属法术/护甲/阴影等联动及实际画面合成未验收，整效果启用数仍2，见[ORIGINAL_ENTRY_GRAPH](ORIGINAL_ENTRY_GRAPH.md)。

原声音显式模板：现在路由到实际Unity重建L_SFX，原四组七效果24快照值与编译数据相同；347动作无未解析声音路径。真实侧链信号、其他语音路由和整卡视听仍未验收，见[ORIGINAL_MIXER](ORIGINAL_MIXER.md)。

原声音重复抑制：现恢复按生成名称和独立协程限额，345动作请求中342启动、3抑制；2个L_SFX显式模板及可听/整卡视觉验收未完成，见[ORIGINAL_SOUND_LIMITS](ORIGINAL_SOUND_LIMITS.md)。

专属声音空间参数：默认模板、线性衰减和发声位置快照已恢复，327源动作隔离运行通过；20声音动作、完整原场景坐标/监听器和可听验收仍未完成，见[ORIGINAL_AUDIO_TEMPLATE](ORIGINAL_AUDIO_TEMPLATE.md)。

原专属入场声音：已恢复FromCorpse的Cast / Impact精确资源与0.8秒延迟参数，播放动作已接入；完整SoundManager混音/抑制/实例限制和整套入场仍未完成，验证范围见 [ORIGINAL_CLIP_AUDIO](ORIGINAL_CLIP_AUDIO.md)。

原入场随机延迟：当前37动作从固定平均数改为原Unity随机抽样，严格超过阈值再推进。已知差异是可重复播放采用客户端种子、不是原全局随机序列；36整卡效果仍有其他缺口，见 [ORIGINAL_RANDOM_WAIT](ORIGINAL_RANDOM_WAIT.md)。

非传奇入场网格/材质：已恢复原四组替换资源和CommonFX RGBR像素分支；完整MaterialService及其他入场脚本未恢复，尚无整套入场视觉一致证据。继续核对完整源FSM，见 [ORIGINAL_GEOMETRY](ORIGINAL_GEOMETRY.md)。

ActorElite节点：原版入场区分传奇/非传奇；当前两子图可按源标签和事件选择分支，并验证真实普通/传奇Actor。非传奇分支的SetMesh/SetMaterial和其他Shader仍未完成，不能称完整视觉一致，见 [ORIGINAL_ACTOR_ELITE](ORIGINAL_ACTOR_ELITE.md)。

CameraShaker2：原版以主相机呈现30位置/原强度曲线抖动；当前两个源入场子图已在实际相机上通过104项源公式/生命周期检查。完整入场仍缺Actor/Elite和其他材质，并未提供整卡视觉一致的证据；继续补这些缺口，见 [ORIGINAL_TIMELINE_SHAKER](ORIGINAL_TIMELINE_SHAKER.md)。

ColorBySpeed 节点：原版依粒子速度改变 RGB/Alpha；当前 12 个节点已接入相同 Unity 模块，并通过 156 项源引擎对照。受控离屏入场仍仅有很小效果，不能证明完整视觉一致；其余原 Shader/脚本尚未完成，见 [ORIGINAL_COLOR_BY_SPEED](ORIGINAL_COLOR_BY_SPEED.md)。

原Timeline粒子控制节点：两个真实烟雾轨道的激活/粒子时钟已执行，1034Timeline与16粒子引擎检查通过。4张64×64受控源图中1.20秒仍只有小青色效果；完整入场的材质、Actor/Elite和镜头未恢复，不计作原卡视觉完成。下一步沿CameraShaker/其余原分支继续，见 [ORIGINAL_TIMELINE_CONTROL](ORIGINAL_TIMELINE_CONTROL.md)。

2026-10-11 原自定义入场Timeline：源图同时包含动画、粒子控制、镜头和音频轨。当前恢复动画轨/183曲线及时间语义，1008源曲线/时钟专项检查通过；源控制和镜头轨、Elite/材质分支仍未完成，未启用整套RLK。没有新增完整原卡比赛截图，不把受控源轨测试冒充整套效果；下一步恢复ControlRandomized和CameraShaker源脚本，见 [ORIGINAL_TIMELINE](ORIGINAL_TIMELINE.md)。

原自定义入场现可把真实Actor挂到原token节点并恢复，但没有原Timeline运动，验证图是隔离的源动作/Actor画面。原完整粒子/移动/材质/音频仍未完成，RLK整套效果不启用，见 [ORIGINAL_CUSTOM_SPAWN](ORIGINAL_CUSTOM_SPAWN.md)。

RLK_060/061原生成子效果已恢复静态原画传递，但6张实际GPU图仍仅有细小静态部件。原版需要Timeline/完整生成脚本与Shader；这些未完成，下一步恢复源控制与时间轴。此节点不视为整卡专属动画完成，见 [ORIGINAL_PORTRAIT_TEXTURE](ORIGINAL_PORTRAIT_TEXTURE.md)。

普通战场随从的源高光状态已接入并停用该路径旧GUI圈；六张真实组件GPU图验证，完整GUI人工对比仍未完成，见 [ORIGINAL_BATTLE_HIGHLIGHT](ORIGINAL_BATTLE_HIGHLIGHT.md)。

原ShaderTime服务已驱动真实Actor绿高光的原flare变化，三帧离屏检查通过，见 [ORIGINAL_SHADER_TIME](ORIGINAL_SHADER_TIME.md)。

原Actor动态轮廓呈源绿色高光；缺失第二UV和UNorm8误读已修复，普通模板无FSM状态分支和原时间服务已接入普通战场随从。英雄/手牌仍用旧高光，特殊轮廓规格及完整状态路由未完成，见 [ORIGINAL_HIGHLIGHT_SILHOUETTE](ORIGINAL_HIGHLIGHT_SILHOUETTE.md)。

原高光Shader/状态材质已恢复并作离屏算术验证；普通随从源静态轮廓为空，使用动态轮廓。组件源公式有验证，完整原版画面逐帧对比未完成，见 [ORIGINAL_ACTOR_HIGHLIGHT](ORIGINAL_ACTOR_HIGHLIGHT.md)。

普通随从边框已替换旧 AssetSurface 兼容材质，恢复源双面/SH/顶点色/采样公式；原动态主光/棋盘阴影/金色反射仍未完成，见 [ORIGINAL_ACTOR_FRAME_SHADER](ORIGINAL_ACTOR_FRAME_SHADER.md)。

普通随从静态/动态原画切换：已统一原主Mesh、专用Shader、源环境探针与UV，三种实际原画共6张比较图通过；完整原版场景逐帧认证仍未完成，见 [ORIGINAL_STATIC_ACTOR](ORIGINAL_STATIC_ACTOR.md)。

随从原画：通用纹理贴图被原双UV/双纹理/去饱和/对比Shader替代，原Gameplay环境探针使其重新可见；原画颜色现在受源光照运算影响。动态棋盘光照和边框等其余材质仍有差异，完整原版逐帧认证未完成，见 [ORIGINAL_ACTOR_PORTRAIT_SHADER](ORIGINAL_ACTOR_PORTRAIT_SHADER.md)。

敌方召唤由原42槽执行独立粒子/入场Clip/0.43秒落地音效，源尾段2秒保留。6张新离屏图已建立；完整棋盘空间、兼容材质与原版听感比较未完成，见 [ORIGINAL_OPPONENT_SUMMON](ORIGINAL_OPPONENT_SUMMON.md)。

原召唤整段2.56秒阻塞已修正：原0.5秒完成后交还队列（含0.06秒帧余量），原清理粒子独立保留。完成监听器/同Actor共享动画及世界空间运动组合未完全恢复，见 [ORIGINAL_SUMMON_COMPLETION](ORIGINAL_SUMMON_COMPLETION.md)。

原随从召唤：已进入正式友方事件调用链，原AllyInPlaySpawn倾斜/下落、EdgeMotes、Dust及落地声共同执行。新增同生产适配器离屏6帧取景见 [ORIGINAL_BATTLE_SUMMON](ORIGINAL_BATTLE_SUMMON.md)。当前原Actor只显示主Mesh，Shader仍兼容、队列含2秒清理尾段偏长、敌方专用表未恢复；完整原版连续帧/听感认证未完成。

原时序增量：地狱烈焰显示伤害与尾部退场不再共享 1.2 秒区间，按已核对完成通知 .35 秒和 Wait 2 的捕获隐藏安排。6 秒 Clip 并不等待播放结束，纠正前次仅由长度推断的差异；中央过曝与原版连续帧仍未认证，见 [ORIGINAL_FINISH_TIMING](ORIGINAL_FINISH_TIMING.md)。

原遮罩增量：溶解及 Black 原数学已恢复，真实离屏捕获边缘受无纹理黑色遮罩影响；中央仍过曝，2 秒样本仍亮。原 6 秒 Clip 生命周期与当前播放区间不一致尚未认证，不能算地狱烈焰完成。见 [ORIGINAL_DISSOLVE_MASK](ORIGINAL_DISSOLVE_MASK.md)。

FX 坐标增量：新地狱烈焰离屏组件图中，错误的实心深色环消失，火环透明区透出棋盘；中央仍过曝。World/Scrolling 基础数学通过 GPU 检查，不等于原场景、全部材质和连续帧一致；启用槽仍为 2。见 [ORIGINAL_FX_COORDINATES](ORIGINAL_FX_COORDINATES.md)。

CommonFX 增量：恢复两组原关键词分支后，BT_035/175 绿色轮廓出现，亮像素 308→893、黑 alpha 808→95。仍缺原目标纹理和完整生命周期，未启用，不以阈值通过冒充原版外观一致。新 Shader/程序解析范围及截图见 [ORIGINAL_COMMONFX](ORIGINAL_COMMONFX.md)。

Mecanim 增量：BT_035/175 的原缩放曲线实际运行，三个 _AlphaMul 通道的身份已确认；兼容 Shader 仍缺该程序及外部主体绑定，未启用。不能以曲线/节点运动测试替代原版外观认证，见 [ORIGINAL_MECANIM](ORIGINAL_MECANIM.md)。

目标分支增量：BT_035/175从空白恢复小型绿色粒子，BT_801发射增多。三个原Prefab仍有黑遮罩、主体动画及外部纹理缺失，保持禁用；没有新增完成卡数。原版连续帧与完整Shader/脚本未验收，见 [ORIGINAL_TARGET_BINDING](ORIGINAL_TARGET_BINDING.md)。

Distortion 增量：第一家族的原RGBA/UV数学已通过GPU固定样本，2Layer未恢复。BT_035/175因目标类型分支缺失仍空白，BT_801三个样本有有限输出；三者保持禁用。原场景时间、光照服务与全局Glow仍有差异，没有新增完成卡数或原版整屏认证，见 [ORIGINAL_DISTORTION](ORIGINAL_DISTORTION.md)。

Glow/环境光程序增量：原 Glow 输出与 SH 转换、mip bias 数学通过 GPU 样本检查；正常画面尚未接入原全局 Glow 后处理，不能称最终发光效果一致。普通棋盘取色按原 Board 构造函数提供白色；特殊事件、SH/主光来源仍未恢复，冰霜新星差异仍保留。详细边界见 [ORIGINAL_GLOW_LIGHTING](ORIGINAL_GLOW_LIGHTING.md)。

原捕获排序增量：实际重叠图层 GPU 检查证明与原 IL 的分组/排序规则一致。三个候选重新导出并查看合成图，冰霜新星仍有白色饱和和半圆形态，未启用。全卡专属动画及完整 Shader/脚本仍未完成；本节点没有新增完成卡数。见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。

原 Shader 基础程序增量：修正了旧 _Intensity 污染、alpha 倍率和加法捕获压暗背景问题；三个家族的基础 RGBA/UV 与合成通过数值检查。冰霜仍有饱和白弧、半圆形态和布局问题，保持禁用。无原版同状态连续帧对照，不声称逐帧一致。验证边界见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。

内部捕获增量：冰霜新星已从灰影进展为真实原网格/粒子捕获，内部黑块消除；蓝白饱和、圆弧形态与覆盖范围仍错误，继续禁用。没有以 GPU 非空检查代替原版外观核验。离屏组件图 CS2_026-board-components.png 是诊断结果，不能当成整屏比赛截图。来源及测试见 [RENDER_TEXTURE_RESTORATION](RENDER_TEXTURE_RESTORATION.md)。

## 原 Prefab 增量（2026-10-10）

| 场景 | 原数据依据 | 当前表现 | 未完成 / 下一步 |
|---|---|---|---|
| 奥术爆炸 | 原环形 Prefab、粒子和动画曲线 | 原层级进入客户端队列，GPU 离屏透明合成通过 | 原 Shader 精确混合及整屏逐帧核对 |
| 冰霜新星 | 原冰霜冲击波/Offscreen 层级 | 原播放器仅灰色阴影，画面核验拒绝启用，保留旧效果 | 原 RenderTexture、冻结/实体绑定及完整 Shader |
| 地狱烈焰 | 原橙色材质、冲击波与 FSM | 原冲击波叠加现有表现；修复残留蓝色 tint 与黑色淡出 | 原地面捕获、溶解、脚本及逐帧核对 |
| 其余卡牌 | 352 个已导出效果槽的审计 | 350 个槽保持禁用，沿用已有表现 | 依赖、未知动作、外部绑定与可见阶段逐项恢复 |

`native-effects/*-board-components.png` 是真实 GPU 离屏组件组合，不作为正常 GUI 整屏或官方截图。数据导出与局部可见性测试不代表“全卡完全一致”。详见 [NATIVE_EFFECTS](NATIVE_EFFECTS.md)。

## 手牌、目标和转场（2026-10-10）

| 场景 | 依据 | 当前表现 | 未完成差异 / 下一步 |
|---|---|---|---|
| 手牌和攻击箭头 | 用户三张复刻反馈，正常扇形与连续箭头 | 统一局部旋转，窗口缩放后中心/端点不漂移 | 真实鼠标连续操作、官方曲线形状未逐帧核验 |
| 英雄攻击预选 | 用户要求抬升 | 区域随选择抬升 44、放大 1.11，取消回落 | 原抬升高度/曲线未实测 |
| 指向战吼/连击/武器 | 合法 moves 和 TCP 重放 | 放置后继续选择，原卡牌实体/装备预览，显示箭头 | 抉择各分支的实际手感待人工核验 |
| 窗口侧边 | 用户要求名字、菜单常可见 | 名字左锚、齿轮右锚，核心仍等比 | 极端窄窗可能遮挡侧边场景，须人工核验 |
| 入场/死亡/结算 | 原皇冠、横幅、裂纹/音轨，本项目时间线 | 入场后发牌、20 片破碎、胜负进入与退出 | 运动和 Shader 为重实现；完整原 Prefab/脚本未完成 |
| 专属粒子 | 原 Renderer/Material/ParticleSystem 关联 | 6 张牌、11 层颜色/尺寸/旋转曲线采样 | 全部专属动画、发射/碰撞、完整脚本/Shader 未完成 |

605 Python、4101 C#、新增 49 GPU 检查与两场后台原生比赛已执行。组件图在 current/lifecycle，未作为完整 GUI 或人工比赛证据。详细边界见 [INTERACTION_LIFECYCLE](INTERACTION_LIFECYCLE.md)。

## 窗口、机制与关联素材（2026-10-09）

| 场景 | 原版/用户依据 | 当前表现 | 未完成差异 |
|---|---|---|---|
| 木盒菜单 | 附件中的亮菜单 | 原材质兼容，移除黑 Glow 遮罩 | 神光、灯光、全 Shader/开盒未恢复 |
| 宽/窄窗口 | 核心等比、有限侧背景 | 统一投影；宽屏有限背景，极窄保底 | 极限与回退为项目配置，非官方实测 |
| 英雄攻击 | 伤害/反击一起 | 共同命中，耐久同帧，扳机另排 | 整屏连续帧未核验 |
| 亡语/剧毒 | 原安装资源、药瓶反馈 | 原骷髅/绿色药瓶，独立校对 | 原动态 Animator 未执行 |
| 扳机 | 高光明显 | 扩大原闪电、金光晕 | 官方强度/时序未逐帧比对 |
| 更多专属效果 | CardDef 分支 | 233 组可用主施放关联、原音轨/贴图和分类时序 | 全分支、专用 Shader、原粒子轨迹未完成 |

599 项规则/管线、3527 条构建断言、421 项分组 GPU 检查和两局后台真实双客户端通过。图片是离屏组件，不声称全卡牌效果或整屏人工验收完成；见 [WINDOW_FEEDBACK](WINDOW_FEEDBACK.md)。

## 手牌与交战反馈（2026-10-09）

| 场景 | 期望表现 | 本轮实现 | 剩余差异 |
|---|---|---|---|
| 多张手牌 | 集中，法力数字可见 | 十张中心跨度上限 590；整体避让校对计数框，含 Hover | 拖动的独立卡牌仍可经过计数区域；任意极端卡面部件改动不在布局约束中 |
| 嘲讽 | 原盾牌框 | GenFX_Taunt_Shield 原网格/材质贴图替代自画圆环 | 原 Gleam/盾破动画和光照逐帧核对未完成 |
| 连击 | 条件满足且可打出的牌为黄光 | 权威 combo_ready + 合法 moves；保留悬停白光 | 强度与官方连续帧比较未完成 |
| 攻击数字 | 高于基础攻击为绿 | 手牌、场上随从、武器、英雄统一动态色 | 编辑器自定义填充仍可覆盖动态色 |
| 普通交战 | 双向扣血一起反馈 | 同命中时刻，纯交战双方死亡并行；实际扳机仍顺序 | 保留短冲撞/命中动画，不让服务器等待 |
| 战吼/连击目标 | 放置后显示指向箭头 | 读取合法目标，放置点起箭头，卡面不遮挡 | 含可选指向与非指向混合抉择的交互核对未完成 |
| 背刺/刺骨/冷血 | 原专属效果 | 原关联音效；刺骨匕首/刀痕，冷血光点/脸部贴图，客户端重实现 | 原状态机/粒子全参数/专用 Shader 未完成；背刺视觉复用刺骨贴图 |

实际测试、自动双客户端结果及组件截图性质见 [COMBAT_FEEDBACK](COMBAT_FEEDBACK.md)。没有新增官方窗口录像，没有整屏人工验收或像素相似度结论。

## 装备状态与刀扇时序（2026-10-09）

| 场景 | 原版依据 | 本轮表现 | 已知差异 / 下一步 |
|---|---|---|---|
| 游戏菜单 | 用户图一暴露复刻正文缺失 | 恢复原按钮正文 alpha，三段绘制边框 | GPU 组件通过；整屏文字/选项面板人工核对未完成 |
| 场上武器 | 原版图三圆形武器，复刻图二误用卡面 | 原 Card_InPlay_HeroWeapon_v2 与第二 submesh 原画，独立攻击/耐久 | 不复制手牌校对；整屏位置仍需人工核对 |
| 技能用后 / 悬停 | 原版图四/五符文背面及正常详情 | 原 Hero_Power_D 背面，used 由服务器提供，详情仍正面 | GPU 背面通过；动态翻面/连续帧及悬停整图未验收 |
| 牌库 | 图六插槽内厚卡叠 | 原 GenFX_Deck 五档网格、真实厚度侧面 | 槽口前景遮挡和全屏位置未核验，仍可用原编辑器区域校对 |
| 刀扇 / 铸甲师 | 用户指定离手→刀扇→伤害→逐次扳机→历史 | 原匕首/拖尾与音轨；独立时轴、中间数值、延迟历史 | 时序/运动自行重实现；原 PlayMaker、Animator、Shader 全状态机未执行 |

51 项 GPU 检查是组件渲染，不能冒充实机整屏。参考图前两张是复刻缺陷基线，后四张是原版局部图，分开归档；主题/裁切不同，不计算伪造的像素还原分数。详细测试、同步证据及未完成项见 [BATTLE_PACING](BATTLE_PACING.md)。

## 字体、拾牌与原音效反馈（2026-10-09）

| 场景 | 当前变化 | 剩余差异 |
|---|---|---|
| 数字/卡牌描述 | 满值白、受损红、增益满值绿；保留粗体 | 编辑后的整屏人工核验未完成 |
| 留牌点选/战斗悬停 | 分离菜单音轨，使用客户端翻牌/悬停 Foley | 留牌按钮准确原轨未核定 |
| 法师技能/法术 | 四个效果映射原声音与粒子纹理，加法合成 | 原脚本/完整粒子动画未执行，播放时序重实现 |
| 铸甲师 | 原闪电网格，实际 Trigger 短暂提亮，沉默移除 | 闪光强度与原版逐帧比对未完成 |
| 点击/拖牌 | 持牌后战场释放；指向战吼先落位再选目标 | 原鼠标跟随深度仍是现有 2.5D 实现 |
| UI 校对 | 背景缩放、填充/描边/阴影、调色板 | 内容逐字编辑、动画时间线编辑未完成 |

583 项规则、336 条客户端构建断言、23 项 GPU 组件检查通过。详细实测和后台比赛结果见 [UI_FEEDBACK](UI_FEEDBACK.md)，后台结果不能替代人工整局或整屏视觉验收。

2026-10-08，现有 master 项目上的 Presentation Sprint；未扩卡池、未重写服务器。**本轮整体视觉验收未完成**。已运行的自动测试、Windows 构建、截图及权威完整对局与未完成的官方比对/人工完整局分别记录。

## 真实 baseline

开始时 Git clean，master，HEAD `5a5777a`。首次 577 passed / 17.62 秒；修改后最终 Python 回归 577 passed / 23.67 秒。构建执行 **245 条 C# 表现断言**，Windows x64 构建退出码 0。这些断言不与 pytest 数量相加冒充独立测试数。

改动前截图：`tests/visual/baseline/sprint-baseline-visual.png`，1920×1080。官方木盒菜单原生截图：`tests/visual/reference/oracle/main_menu.png`，1920×1080。通过 Beta Launcher/Battle.net 正常启动，游戏内设置固定分辨率后用 Print 截图，未处理凭证或绕过登录。

官方战斗参考**未完成**：连接中断和教程弹窗曾阻挡进入对战；后续桌面工具报告 minimized/user input，重绑与重置后仍失败。用户回复已经还原后工具仍报告 minimized。另一次复刻窗口观察返回了其他应用画面，因此停止鼠标操作，没有依据错误截图点击。内部截图不能称作官方 Reference。

## 五个 Gold Standard

“原版预期”来自用户本轮表现要求，尚未测得官方逐帧坐标/曲线。五场已实际渲染，**达到官方 Gold Standard 的视觉核验未完成**。

| 场景 | 原版预期 | 当前实际表现 | 未完成 / 下一步 |
|---|---|---|---|
| A 起手换牌 | 棋盘卡列、点选、替换、回到手牌 | 原版横幅/蓝色按钮/红 X；真实起手 Entity；独立换牌状态与转场 | Hover 尚为直接放大；英雄入场/硬币仪式未完成，待 Oracle 校准 |
| B 四张手牌 | 扇形、重叠、抬升、邻卡避让、拖动回正 | 动态数量，Hover 1.37 倍，平滑姿态，拖动 1.16 倍，取消回位 | 原网格先渲染成 GUI 卡面；真实倾斜透视/鼠标速度惯性未完成 |
| C 打出随从 | 离手、揭示、召唤、收拢 | 0.84 秒 CardPlay cue，0.58 秒显示落地，随从缩放与原版声音 | 原 Prefab 粒子/灯光未恢复，语音仍是首候选 |
| D 互攻并死亡 | 蓄力、冲撞、hit stop、反应、尸体消失、重排 | 0.12/0.16/0.07/0.43 秒攻击；保留双亡实体、数字、淡出、平滑重排 | 原破碎粒子和复杂亡语链的逐事件视觉状态未完成 |
| E 技能和回合 | 技能状态/命中、按钮、提示、抽牌 | 技能可用/悬停/按下/目标/禁用，法师弹道、原版回合音效与飞牌 | Used/Disabled 独立盖板、技能专属音轨未完成；箭头重实现 |

这些时长是本项目调校值，不是官方实测时间。

胜负目前复用换牌横幅与英雄框，官方 Victory/Defeat 专用庆祝/破碎序列未完成。十张手牌两端下沉较多，需要官方参考校准弧度；护甲专用框、技能盖板、绳索计时、部分普通菜单按钮风格也仍有差异。

## 固定截图

全部是实际 Unity Windows Player 输出，过程帧带 `-action`。32 个场景先后执行日志/新鲜度/分辨率检查通过。固定入口不是人工操作验收。

| 序号 | 场景参数 / 文件名前缀 | 当前 | 官方战斗 Reference |
|---|---|---|---|
| 01 | match_start | 起手静态采样；英雄飞入未完成 | 未完成 |
| 02 | mulligan | visual-mulligan.png | 未完成 |
| 03 | turn_start | 过程/结束帧 | 未完成 |
| 04 | hand4 | visual-hand4.png | 未完成 |
| 05 | hand7 | visual-hand7.png | 未完成 |
| 06 | minion_on_board | 单随从 | 未完成 |
| 07 | multiple_minions | 四随从 | 未完成 |
| 08 | card_hover | 放大/邻卡避让 | 未完成 |
| 09 | card_drag | 固定拖动姿态 | 未完成 |
| 10 | minion_attack | 过程/结束帧 | 未完成 |
| 11 | hero_power | 过程/结束帧 | 未完成 |
| 12 | spell_cast | 火球过程/结束帧 | 未完成 |
| 13 | damage | 过程/结束帧 | 未完成 |
| 14 | death | 过程/结束帧 | 未完成 |
| 15 | end_turn | 过程/结束帧 | 未完成 |
| 16 | victory | visual-victory.png | 未完成 |
| 17 | defeat | visual-defeat.png | 未完成 |
| C | minion_summon | 过程/结束帧 | 未完成 |

额外 hand1/2/3/5/6/8/9/10、minions2/3/5/6/7、card_draw；单随从和四随从分别由 minion_on_board、multiple_minions 覆盖。截图位于 `tests/visual/current`，内部基线与官方基线分目录保存。

最终 32 场景整批运行通过；随后 mulligan、hand4、minion_summon、minion_attack、hero_power、end_turn 共 10 张图重复采样。对内部基线的 dHash 距离全部 0，RGB 像素平均差最大 0.000114134（0～255），少量差异位于手牌收拢采样和栅格化边缘。报告保存在 current/capture-report.json，差图位于 diff/。这是重复运行稳定性，不是官方还原度评分。

## 2026-10-09 人工 UI 校对器回归

当前增加的是可保存的人工校对工具，默认配置保持既有画面几何；没有将卡牌编辑器截图当成游戏背景。卡牌原画 UV 和恢复的模型部件可独立修改；正常文字、数值以及实际输入命中跟随各自模板。

`current/ui-editor-final` 包含 30 个真实编辑器预览截图。`current/ui-runtime` 的换牌、四张手牌、四随从、攻击、英雄技能、胜负共 9 张图与内部基线 dHash 相同；技能过程帧 RGB 平均差 0.007873585，其他帧最大 0.000005792（0~255）。这是回归检查，不是官方视觉评分。具体位置仍需用户校对，主菜单木盒 3D 布局和动画时间曲线编辑未完成。详见 [UI_EDITOR](UI_EDITOR.md)。

## 来源与重实现

[presentation-assets.json](../manifests/presentation-assets.json) 有 **50 项**来源元数据。原版网格：Board_STR、TableTop、英雄/技能、手牌/随从/武器框、卡背、宝石、换牌横幅/按钮。原纹理：原画、框图集、换牌红 X、结束回合、火球纹理。原字库：BelweNumbers、UGCSH00M、gkai00m。声音：DrawFromHand、Summon_Drop、AttackLaunch/Impact、draw_card_1、YourTurn、EndTurn、Mulligan shuffle、victory_jingle、defeat_jingle、Duel/Main_Title。

光晕、阴影、状态环、箭头、攻击/受击/死亡、手牌姿态、技能/火球运动、描边和相机震动是本项目重实现。未执行官方游戏脚本、专用 Shader、完整 ParticleSystem、骨骼动画或棋盘机关。`inspected_only` 素材没有用于运行画面，不能计入原 VFX 恢复。

## 已修复的实际问题

- Combined 是 inactive 的替代网格；不能隐藏 Projected 下八块真实地面。
- 非平面 TableTop 固定下移不足会遮住棋盘；按世界 maxY/minY 放到底层。
- 横幅 alpha 是材质通道；用原始 mesh/UV 还原轮廓，去掉图集矩形。
- 手牌遮血量；下移扇形，增加 55 条可见性断言。
- Update/OnGUI 间抽牌快照切换导致缓存 KeyNotFound；安全初始姿态并通过 turn_start/card_draw 回归。
- phase 是 `play`，误写 `playing` 会令回合按钮失效。
- 结算自动截图现在等队列完成，避免一秒定时器拍到动画结束前。
- 截图检查曾将 hand1 前缀误匹配为 hand10，导致虚假的旧截图失败；现在只检查精确文件名和对应 action 帧。固定场景关闭实际指针 Hover，只对 card_hover/drag 使用固定输入姿态。

## 实际对局与性能

升级构建对局 `a9f5b957cec64f3195045e1f0466caf2`：12 回合 / v46，双方公共哈希一致 `83cda66ede639a8e744191c14f81c7e330aa99bef0e11ebc96a266ec8421e04d`。

后续构建 `12089212a68441379580519537548d79`：15 回合 / v49，Shrink1 法师 LOST（-1 HP），Shrink2 战士 WON（29 HP）。双方哈希 `0432d825d0cfe21e699a1c0c8197afa5c29e14832381071d2afaec3cbaeb872f`。日志无 Exception/NullReference/KeyNotFound/error。均为双实际 Windows 客户端正常匹配和动作，不是“18 局”，不是只有 Python 模拟。

该次双开战斗页面预热后的前 3600 个 Update 样本：平均 220.47 / 223.44 FPS，P95 8.34 / 8.34 ms；队列峰值 1 / 1；纹理缓存 106 / 105、卡面 RenderTexture 45 / 44；托管堆约 7.04 / 7.10 MB。高刷/VSync 可覆盖 Unity targetFrameRate；这是短窗口测量，不声称锁定 60 FPS或完成长期 GC/泄漏验收。

本轮**全程人工操作完成一局未完成**。无 autoplay 的独立实例已启动，但工具返回错误应用画面后停止输入；未把自动局或 fixture 冒充人工完整局。

结算截图修复后的实际局 `2f2d3cab9e9341299b59e56366f684e1`：13 回合 / v48，Shrink1 LOST（-3 HP），Shrink2 WON（8 HP），双方公共哈希 `c0e1a181cca4b2c626191f47621ff5b0bc1bbce857c8b5f455aae9a007f15af5`，两个日志无运行异常，胜负截图包含结算界面。三局均为自动输入通过普通客户端协议完成，不是人工局。

## 战斗交互修正（2026-10-09，用户校对后）

后续棋盘相机倾斜、原 3D 回合按钮翻转、菜单/提示框、敌方法力、图标压暗、召唤/睡觉粒子及编辑器重叠已修改，详见 [BOARD_UI](BOARD_UI.md)。原按钮正/背/过程帧和棋盘离屏图通过验证；完整 GUI 人工比较**未完成**，不将离屏图当作整屏还原度。原完整菜单脚本/Shader/Animator/ParticleSystem 执行仍未恢复。

| 场景 | 官方截图依据 | 当前代码表现 | 已知差异 / 下一步 |
|---|---|---|---|
| 起手留牌 | 绿光、悬停白光、替换红 X | 轮廓缓存分别叠绿/白，替换取消高亮 | 原光晕 Shader 重实现；强度及 Hover 整屏人工核对未完成 |
| 正常手牌/棋盘 | 更充足的纵向场地与手牌文字空间 | 棋盘投影、英雄/随从/技能、手牌中心同步校准 | 用户偏移保留；不同棋盘主题不能按同状态像素比较 |
| 无动作提示 | 无卡/技能/攻击可用时回合按钮绿色 | 使用权威合法动作；有动作黄色，无动作绿光 | 提示策略断言通过，最终 GUI 图未完成 |
| 英雄技能 | 就绪绿光、蓝色详情卷轴 | 真实技能模型、原版卷轴网格及纹理、费用和文字 | 组合离屏渲染通过；字体/光照仍为本项目 Shader |
| 战場随从 | 悬停白光、详情向另一侧展开 | 同一卡牌模板显示当前快照；左右侧及边缘限制 | 完整鼠标路径人工核对未完成 |
| 操作历史 | 原版小图框与悬停大卡 | 原框、裁切参数；保留死亡目标、限制可见事件 | 原 Shader 精确裁切未核验；不记录对方隐藏抽牌 |
| 护甲/耐久 | 完整银盾 / 破裂盾 | 专用 Hero_Armor / Wep_SheildBroken，已修正方向 | 原图标离屏图已检查；最终战场位置仍可人工校对 |

本轮规则回归 577 通过，构建 245+21+33 条断言通过，十项 GPU 离屏资源图通过。新后台原生自动对局 `71cd7f4c1b7e46b5bae71425a86055fe` 为 13 回合 / v44，双方公共哈希一致；Shrink1 LOST（0 HP）、Shrink2 WON（17 HP）。未使用 Computer Use。

后台 Player 整图采样是黑帧/截图失败，已拒绝；十张 `interaction-models` 图不能冒充完整实机截图。六项交互已接入并编译，最终整屏人工悬停/点击核验**未完成**。原始七张用户参考位于 `reference/oracle/interaction`，有窗口边框及局部裁切，没有制作虚假像素还原度分数。来源见 [interaction-assets.json](../manifests/interaction-assets.json)，详细验证见 [UI_INTERACTION](UI_INTERACTION.md)。

2026-10-10 BT_035 英雄分支：原固定纹理三处绑定已接入，离屏 0.30 秒图有绿色英雄轮廓。当前 4/6 可见样本、最大 1420 像素，轮廓过小、目标场景/生命周期及后处理未完整恢复，仍禁用。没有用该图宣称整场原版动画还原。来源、72 帧 GPU 结果及后续缺口见 [ORIGINAL_TEXTURE_BINDING](ORIGINAL_TEXTURE_BINDING.md)。

2026-10-10 原 Nature-Thorns：SetScale 与原 ThornsScale 曲线联动恢复，6/6 离屏样本可见、最大 2794 像素、无黑色 alpha。0.60 秒图仍为小型绿金冲击，尺寸、场景目标、其他 Shader 与完整原后处理未恢复，仍禁用；不能视作全卡原版效果验收。详见 [ORIGINAL_SCALE_ACTION](ORIGINAL_SCALE_ACTION.md)。

2026-10-10 Holy-Wisdom：原四通道颜色曲线及钳制枚举恢复，Birth 分支 5/6 可见、最多 1847 像素，无黑色 alpha；0.60 秒图仍为微小红点。原目标、尺度、Shader/后处理未完成，仍禁用；颜色变量进入材质不等于原专属动画认证。详见 [ORIGINAL_COLOR_ACTION](ORIGINAL_COLOR_ACTION.md)。

2026-10-10 CS2_026 冰霜新星：原 EaseColor 的两秒等待、三次淡出及完成帧已恢复，离屏 5/6 可见、最大 49779 像素；2.00 秒图仍为局部蓝白碎片，原目标场景、其它 Shader 与后处理未完成，仍禁用。下一步需要完整目标捕获和 Shader 变体验证；不以参数绑定数量代替原效果验收。详见 [ORIGINAL_EASING](ORIGINAL_EASING.md)。

后续 Multiply Shader 节点：CS2_026-2.00.png 从局部碎片变为蓝白大面，仍过亮且原目标几何/场景不完整；CS2_062-0.30.png 为亮黄面和橙环。原内部乘色运算已验证，最终彩色透射/后处理未恢复，保持既有两槽启用而不新增认证，见 [ORIGINAL_MULTIPLY_SHADER](ORIGINAL_MULTIPLY_SHADER.md)。

后续彩色透射节点：真实 CS2_062-board-components.png 已通过生产 C/K 链路组合，地狱烈焰深色环和过亮黄面仍明显。源材质/捕获/目标场景仍要核对；数学等价不等于原版外观认证，详见 [ORIGINAL_AFFINE_COMPOSITE](ORIGINAL_AFFINE_COMPOSITE.md)。

2026-10-10 扩展 FX：EX1_180 的原 Rotation 材质和 EX1_044 扳机的 Manual 材质进入已核对程序。0.30 秒离屏图分别为小紫色符文、小绿黄飘带；原目标场景与尺度未完成，仍禁用。下一步核对外部目标和全局后处理，不把 Shader GPU 数量当作完成卡数。TwoLayer 尚无对应缓存卡场景，未做逐卡验收。见 [ORIGINAL_EXTENDED_FX](ORIGINAL_EXTENDED_FX.md)。

2026-10-10 原动画自身目标：月火术播放/停止节点已从 -1 恢复为原 1/11/20。Birth 0.30 秒几乎不可见，Death 为小蓝白球；完整原目标场景/尺度未完成，仍禁用。现有两个启用效果重拍仍可用，地狱烈焰中央过曝仍存在。下一步完整 CrossFade/混合和目标绑定，见 [ORIGINAL_ANIMATION_OWNER](ORIGINAL_ANIMATION_OWNER.md)。

2026-10-10 CrossFade 对照：真实引擎已证明当前删旧 Clip 行为不等价于原混合；本节点没有新的原卡完整画面或启用槽。单 Clip 归一化与同名时间保持需要进入正式播放器后再做逐卡连续帧核验，见 [ORIGINAL_LEGACY_BLEND_ORACLE](ORIGINAL_LEGACY_BLEND_ORACLE.md)。

2026-10-10 默认层混合接入：半程旧/新姿态和材质通道通过检查，重拍紫色圆环仍可见、地狱烈焰中央仍过曝。全卡逐场景连续帧认证未完成，没有新增启用槽，见 [ORIGINAL_LEGACY_MIXER](ORIGINAL_LEGACY_MIXER.md)。

2026-10-10 退出停止：原三组箭矢 Take 001 状态退出后不再保持动画时钟。0.30 秒真实离屏图仍为小紫环/小橙亮点，完整原目标与尺度未恢复，继续禁用。见 [ORIGINAL_ANIMATION_EXIT](ORIGINAL_ANIMATION_EXIT.md)。

# 原手牌世界姿态

native-offscreen-fx/source.png从黑屏恢复放射状粒子；composed.png为实际外层采样，但仍红色而非原神圣光芒，缺原画/完整Shader。未新增整卡一致性通过，见 [ORIGINAL_OFFSCREEN_FX.md](ORIGINAL_OFFSCREEN_FX.md)。

native-property/holy-start.png的原SDK粒子属性已实际更新，但图像仍仅卡形轮廓/红色粒子，原神圣光芒和目标Card未恢复；未新增整卡一致性通过。见 [ORIGINAL_PROPERTY.md](ORIGINAL_PROPERTY.md)。

RGBA诊断 holy-start.png 现在有卡形轮廓与红色小亮点，原目标原画/神圣光芒/完整Shader仍缺失；未新增整卡一致性通过，见 [ORIGINAL_RGBA.md](ORIGINAL_RGBA.md)。

源启动截图库 native-start/wisdom.png、kings.png 在0.8秒仅有小红色亮点，缺原神圣光芒/目标Card表现；0.1/0.3秒早期可见性检查失败已保留记录。真实状态完成不等于原Shader视觉通过，见 [ORIGINAL_SPAWN_START.md](ORIGINAL_SPAWN_START.md)。

原启动时序与两卡实际延时参数已恢复入口；本节点无新增正式视觉通过图，实际启动 Prefab 未绑定。见 [ORIGINAL_SUPER_START.md](ORIGINAL_SUPER_START.md)。

原父/子效果完成等待已用真实区域 FSM 验证，见 [ORIGINAL_SUPER_ACTION.md](ORIGINAL_SUPER_ACTION.md)。正式播放器尚未切换，本节点未新增正式视觉一致性通过场景。

区域克隆诊断：奥术爆炸和地狱烈焰各两侧共四图位于 tests/visual/current/native-super-area/。原逻辑只选择一个区域 Prefab，已避免把地狱烈焰双方版本一起生成；仍有饱和亮部、捕获投影与正式场景坐标差异。正式播放器未切换，未新增整卡一致性通过场景。见 [ORIGINAL_SUPER_AREA.md](ORIGINAL_SUPER_AREA.md)。

原相机透视配置可渲染真实奥术爆炸图：tests/visual/current/native-camera/arcane-perspective.png。诊断锚点取光轴32单位处，未恢复实际战场坐标或官方同状态视图，不作为新整卡通过场景；见 [ORIGINAL_SOURCE_CAMERA.md](ORIGINAL_SOURCE_CAMERA.md)。

2026-10-12 定位源数据导出与根入口恢复后，原 41 GPU 检查通过，仍有 35 生命周期场景空白、地狱烈焰中央过曝。两个已审核效果重拍及更新缓存指纹，未新增原版一致性通过场景，见 [ORIGINAL_EFFECT_PLACEMENT.md](ORIGINAL_EFFECT_PLACEMENT.md)。

2026-10-12 原 iTween 曲线扩展后重跑 40 程序/133 生命周期/798 帧，0 异常、35 场景全程无可见像素。源准星仍为小亮点，未新增整卡通过场景，详见 [ORIGINAL_ITWEEN_COMPLETE_CURVES.md](ORIGINAL_ITWEEN_COMPLETE_CURVES.md)。

原版收拢阈值、抽牌数量扣除、敌方揭示、目标选择英雄留空和 Hover 高度算法已恢复。当前画面仍走已校准 HandPresentation；未新增视觉一致性截图。下一步是恢复原场景参数、相机投影与 ZoneHand 布局 Tween。详见 [ORIGINAL_HAND_GEOMETRY.md](ORIGINAL_HAND_GEOMETRY.md)。

神圣启动光芒：原 Shader 使用捕获 R/G 形成双金色；此前最终图为红色，现实际 Prefab 合成为金色。完整英雄原画/遮罩、原场景亮度、尺度和整卡连贯播放仍未完成；没有新增整卡原版一致性通过场景。截图与来源见 [ORIGINAL_HOLY_SHADER.md](ORIGINAL_HOLY_SHADER.md)。

原HeroMask/顶点Glow公式、blend与无纹理过滤已修复。真实启动Prefab中两Renderer原默认禁用状态保持；未强行开启或宣称新增整卡视觉通过。还需恢复目标Card绑定和激活路由，详见 [ORIGINAL_VERTEX_SHADERS.md](ORIGINAL_VERTEX_SHADERS.md)。

后续按原SetVisibility局部启用mask/glow，修复递归副作用、toggle和退出清理。源Card_Hero默认确为原白色纹理，不能把它直接当作缺失头像；未新增整卡视觉通过，见 [ORIGINAL_LOCAL_VISIBILITY.md](ORIGINAL_LOCAL_VISIBILITY.md)。

原书本Death Poof现在等自身.25秒才隐藏，之前此延迟动作未执行。实际源总序列.3+.5+.25已核对，但ColorInterpolate与完整材质/整卡视觉仍未完成。见 [ORIGINAL_DELAYED_VISIBILITY.md](ORIGINAL_DELAYED_VISIBILITY.md)。

后续书本_TintColor已按原两色渐变，.9秒40%插值、1.1秒精确末色实测通过。其余43动作数组也已回读，完整源场景和整卡视觉未完成；未新增整卡一致性通过。见 [ORIGINAL_COLOR_INTERPOLATE.md](ORIGINAL_COLOR_INTERPOLATE.md)。

延迟音效不再因视觉状态退出消失，完成事件按原播放发起时刻、共享协程timer执行；时序测试通过，尚无原版听感/完整音画对比验收，见 [ORIGINAL_PLAYTHROUGH_COROUTINE.md](ORIGINAL_PLAYTHROUGH_COROUTINE.md)。

音源空间曲线不再被默认0覆盖，原参数已接到实际SDK voice，跟随源节点并随销毁停止；完整世界/监听器及听感未验收，见 [ORIGINAL_SOURCE_AUDIO_TEMPLATE.md](ORIGINAL_SOURCE_AUDIO_TEMPLATE.md)。

同一既有源重复触发不再叠加SDK声部，None音量不会错误回到默认代码增益。实现与实际检查见 [ORIGINAL_SOURCE_REUSE.md](ORIGINAL_SOURCE_REUSE.md)，原场景听感和完整整卡仍未验收。

音量曲线现在实际更新已播放声音，不再仅改变变量或后续cue。SDK渐弱测试通过，原听感/完整场景音画未验收，见 [ORIGINAL_AUDIO_VOLUME_ACTION.md](ORIGINAL_AUDIO_VOLUME_ACTION.md)。

原停止动作此前被跳过，现在停止指定自有源且不会取消延迟协程；8 SDK 检查通过，完整听感和整卡场景未验收，见 [ORIGINAL_AUDIO_STOP_ACTION.md](ORIGINAL_AUDIO_STOP_ACTION.md)。

原 AudioVolumeFadeAction 此前未执行，现在等待原音量渐变后完成状态，按完成标志停声；提前状态退出不会回到旧音量。实际 SDK 检查通过，完整听感与全卡场景未验收，见 [ORIGINAL_AUDIO_FADE_ACTION.md](ORIGINAL_AUDIO_FADE_ACTION.md)。

循环原音频此前因缺动作及主音频拒绝而不播放；现12原程序循环启动检查通过，淡入淡出后按原状态完成。声部生命周期及原引用失效已测试，完整销毁回调和整卡听感未验收，见 [ORIGINAL_AUDIO_LOOP_ACTIONS.md](ORIGINAL_AUDIO_LOOP_ACTIONS.md)。

正式区域卡此前展开主层级直接播放，现按原两侧配置独立克隆并通知父控制器。四个离屏原特效图已检查；原场景相机、整体战场构图与全部整卡一致性未验收，见 [ORIGINAL_AREA_PLAYER_INTEGRATION.md](ORIGINAL_AREA_PLAYER_INTEGRATION.md)。

主相机震屏此前跳过，现真实相机按原世界轴移动并等待动作计时，地狱烈焰尾段延后到约3秒。GUI实体尚未全部置于原3D场景，整体一致性未验收；见 [ORIGINAL_MAIN_CAMERA_SHAKE.md](ORIGINAL_MAIN_CAMERA_SHAKE.md)。

原 AudioPlaythrough 指定 SoundDef 此前被拒绝，现按原模板复制与延迟时序生成声音；隔离声音动作测试不代替整卡动画或听感验收，见 [ORIGINAL_PLAYTHROUGH_SOUND.md](ORIGINAL_PLAYTHROUGH_SOUND.md)。

物体音源限流此前被拒绝；现按原名称配额执行，拒绝不会切断旧声音，同名替换不会误删物体音源。完整场景混音与全卡听感未验收，见 [ORIGINAL_OWNED_SOUND_LIMITS.md](ORIGINAL_OWNED_SOUND_LIMITS.md)。

双层alpha-noise此前按时间滚动贴图，现恢复原顶点R/A坐标；SneakySmoke恢复红通道颜色插值。原Repeat与完整mip也恢复，完整场景和整卡连贯表现未验收；见 [ORIGINAL_SMOKE_SHADERS.md](ORIGINAL_SMOKE_SHADERS.md)。

原先根图静态播放无法表达独立投射物飞行；现已拍到真实刀扇克隆在原路径上起飞/中段/到达消失。转向仅记录服务请求，完整根控制器和命中链仍未完成，见 [ORIGINAL_MISSILE_PATH.md](ORIGINAL_MISSILE_PATH.md)。
