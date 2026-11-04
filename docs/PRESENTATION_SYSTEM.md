# 战斗表现系统

非死亡区域先注册完成再布局；Hand跳过已处理draw/DoNotSort，等待其他Actor准备；死亡块使用独立的原缩放延迟路径。实际布局/Tween服务未恢复，不用现有手牌近似收敛充当原完成条件，见[ORIGINAL_ZONE_LAYOUT](ORIGINAL_ZONE_LAYOUT.md)。

区域列表先等待布局dirty清除，再等GameState不忙；完成标记与任务范围回调分开，多等待保留独立token。实际原布局/区域动画还未实现，见[ORIGINAL_ZONE_CHANGE_LIST](ORIGINAL_ZONE_CHANGE_LIST.md)。

原任务执行范围回调与task/zone完成标记分离，CanDoTask和MulliganBusy等待仅在客户端Update阶段恢复；未接完整原服务/区域动画。普通SemanticEvent不自动生成原历史，见[ORIGINAL_POWER_EXECUTION](ORIGINAL_POWER_EXECUTION.md)。

控制器历史收集按原来源、元数据门槛及TAG_CHANGE目标顺序执行；当前实例重新登记缓存来源借用。PrepareSource与GUI重绘适配分离，不让Server等待动画；完整原历史提供器与任务执行未接入，见[ORIGINAL_POWER_TARGETS](ORIGINAL_POWER_TARGETS.md)。

目标集合保留原顺序、重复/空项和首项语义；BestTarget优先对方Card，否则仅回退首项Card，Facing3取其英雄。正式多目标任务尚未导入，不能从AOE伤害自行推导目标，见[ORIGINAL_SPELL_TARGETS](ORIGINAL_SPELL_TARGETS.md)。

原来源定位支持事件上下文自动SourceAuto：法术取所属英雄、技能取对应power、附魔取Attached、武器保留自身。正式召唤工厂已提供来源上下文；正式攻击附属生命周期与场景合成尚未接入，见[ORIGINAL_SPELL_OBJECTS](ORIGINAL_SPELL_OBJECTS.md)。

原UpdateTransform不等于冲刺：只在dirty时定位/朝向，不持续跟随；四源攻击实例显式绑定来源后完成Birth/等待恢复Action的定位。正式对战目标绑定及攻击合成尚未接入，见[ORIGINAL_SPELL_TRANSFORM](ORIGINAL_SPELL_TRANSFORM.md)。

原攻击StopWaitingToAct事件解除等待，已yield的Action在下一表现帧恢复，保留前态；PlayDefaultSound按原节点默认SoundDef播放并随节点隐藏清理。原位移和正式攻击附属实例尚未接入，见[ORIGINAL_ATTACK_EVENT_AUDIO](ORIGINAL_ATTACK_EVENT_AUDIO.md)。

源攻击状态尾段现在由原 AnimationEvent 驱动，而非仅采样曲线。OnSpellFinished 与视觉状态退出保留不同时间点；声音/等待同步回调继续记录为未处理，见[ORIGINAL_ANIMATION_EVENTS](ORIGINAL_ANIMATION_EVENTS.md)。

原 SpellState 显式播放接入真实动画层、速度、CrossFade 与同对象/同层切换保留规则；Show/Hide 不等于重建实例。正常对战生命周期联动及原 AnimationEvent 仍未完成，见[ORIGINAL_SPELL_STATE](ORIGINAL_SPELL_STATE.md)。

原本地法术表显示排除类型0，隐藏包含类型0；两个原入场图仍各有显隐副作用缺口。新回调入口未接入正式角色，不计为完整入场画面，见[ORIGINAL_SPELL_VISIBILITY](ORIGINAL_SPELL_VISIBILITY.md)。

CommonFX无Ramp RGBR使用family19，保留原HDR顶点变换、贴图红通道alpha及动态混合值；实际22Prefab采样仍有11空白和黑alpha，不能计为完整专属动画。见[ORIGINAL_COMMONFX_NONE](ORIGINAL_COMMONFX_NONE.md)。

family18从原编译程序恢复亮度遮罩透明度与顶点颜色，Glow保留独立贴图RGBA采样；六原Prefab离屏采样记录空白及黑alpha，未作为完整对战效果放行，见[ORIGINAL_COLOR_ALPHA](ORIGINAL_COLOR_ALPHA.md)。

原透明粒子family17分别保留RGB Intensity与alpha Alpha，基础TintColor和Glow Color分开。属性混合状态随Seek更新；内部捕获及仿射合成读取当前值，重播恢复原材质值，见[ORIGINAL_TRANSPARENT](ORIGINAL_TRANSPARENT.md)。

NativeEffect新增原Additive family16：基础层使用原_TintColor与SrcAlpha/One，Glow独立使用_Color.a；原未截断颜色进入既有仿射捕获。覆盖引用不计为完整卡牌动画，见[ORIGINAL_ADDITIVE](ORIGINAL_ADDITIVE.md)。

原AudioPlayClipAction延迟协程现在由效果实例持有，不随状态退出取消；完成后移除，重播/销毁清理。两个原入场完整图已验证真实Actor与声音时序，不把隔离动作验证视为整效果完成，见[ORIGINAL_ENTRY_GRAPH](ORIGINAL_ENTRY_GRAPH.md)。

两个显式声音模板及原混音器已接入，347声音动作隔离运行无未解析动作，但不是347整卡效果完成。其他语音路由和真实侧链听感未验收，见[ORIGINAL_MIXER](ORIGINAL_MIXER.md)。

原声音实例限制已接入共享播放桥，拒绝重复声音不阻塞动作完成；限额等待独立于音源删除或PCM结束。183源程序345声音动作隔离检查通过，不等于全卡视觉完成，见[ORIGINAL_SOUND_LIMITS](ORIGINAL_SOUND_LIMITS.md)。

原声音模板与空间快照已接入，音源按播放时的位置创建而不追随目标；所有183程序的声音动作另有隔离运行检查，不能用其证明整卡表现完整，见[ORIGINAL_AUDIO_TEMPLATE](ORIGINAL_AUDIO_TEMPLATE.md)。

AudioPlayClipAction独立等待原延迟协程，声音开始即Finish，不等PCM播放结束；重播不重复交付已经播放的声音。完整混音和其他声音分支仍未恢复，见 [ORIGINAL_CLIP_AUDIO](ORIGINAL_CLIP_AUDIO.md)。

RandomWaitAction采用原参数的Unity随机抽样，独立记录动作完成和事件，严格超过时长才结束；重播使用隔离随机状态，不影响服务器结算。Realtime输入边界见 [ORIGINAL_RANDOM_WAIT](ORIGINAL_RANDOM_WAIT.md)。

源SetMesh / SetMaterial在客户端效果FSM执行，保存和恢复借用组件，材质副本随实例释放；RGBR分支保留原红通道透明度。完整源子图仍未启用，见 [ORIGINAL_GEOMETRY](ORIGINAL_GEOMETRY.md)。

两个FromCorpse子图按真实Actor的ELITE标签路由原On/Off分支，缺Actor只完成；95Unity动作/生命周期检查通过。非传奇网格与材质替换仍未接入，详见 [ORIGINAL_ACTOR_ELITE](ORIGINAL_ACTOR_ELITE.md)。

CameraShaker2已按源ShakerHelper驱动实际战斗相机，支持原曲线、30位置循环及生命周期复位。源静音轨不打开；整套RLK仍缺其余Actor/材质/Shader，详见 [ORIGINAL_TIMELINE_SHAKER](ORIGINAL_TIMELINE_SHAKER.md)。

原 ColorBySpeed 已用于 12 个缓存粒子节点，按真实粒子速度驱动原渐变，而非人工提亮烟雾。7 个子程序仍未完整启用，详见 [ORIGINAL_COLOR_BY_SPEED](ORIGINAL_COLOR_BY_SPEED.md)。

ControlRandomizedTrack 已接入源激活/暂停及原自定义粒子时钟，父粒子带孩子模拟与既有独立采样互斥，倒放/重入/释放恢复生命周期。子Director、ITimeControl、镜头仍未恢复，不启用整个RLK；详见 [ORIGINAL_TIMELINE_CONTROL](ORIGINAL_TIMELINE_CONTROL.md)。

两个原自定义入场 Timeline 已恢复动画轨道及 TimelinePlay 时间语义。183曲线/26Renderer开关按源图采样，FSM动作退出不会中断动画；3未静音控制/镜头轨未恢复，整套效果仍禁用。1008实际源图专项检查通过；不是全卡/整套入场完成，见 [ORIGINAL_TIMELINE](ORIGINAL_TIMELINE.md)。

源InitializeCustomSpawn/FinalizeCustomSpawn父级与姿态操作已运行于真实Actor，输出供后续显隐/Finalize使用；完整Timeline及入场视觉仍未完成，见 [ORIGINAL_CUSTOM_SPAWN](ORIGINAL_CUSTOM_SPAWN.md)。

原GetCardPortraitTextureAction→FSM纹理变量→SetMaterialTexture链路已恢复，RLK_060/061实际Prefab传递真实Actor原画。仍缺Timeline/自定义生成/其余Shader，未启用整套专属动画，见 [ORIGINAL_PORTRAIT_TEXTURE](ORIGINAL_PORTRAIT_TEXTURE.md)。

普通随从原高光已接入正常战场，持续保留与召唤共用的Actor；只对该路径停用旧GUI光圈，保留英雄/详情/编辑预览。原完整状态脚本未恢复，见 [ORIGINAL_BATTLE_HIGHLIGHT](ORIGINAL_BATTLE_HIGHLIGHT.md)。

原全局ShaderTime已在ClientApp接入：使用Time.deltaTime/20，保持独立于表现队列时间。真实原高光Actor三帧验证flare随源uniform变化，见 [ORIGINAL_SHADER_TIME](ORIGINAL_SHADER_TIME.md)。

原动态轮廓从实际Actor生成并作白/绿/黄状态组合；普通模板没有原FSM，其直接状态分支已接入上述普通随从范围，见 [ORIGINAL_HIGHLIGHT_SILHOUETTE](ORIGINAL_HIGHLIGHT_SILHOUETTE.md)。

原 Highlight Shader 运算与源21状态槽已恢复；当前只映射普通随从的六个状态，完整输入路由未完成，见 [ORIGINAL_ACTOR_HIGHLIGHT](ORIGINAL_ACTOR_HIGHLIGHT.md)。

普通随从主 Mesh 边框恢复源 UnlitTexture 双 Pass 公式，主渲染接现有 Actor，源阴影图管线未恢复；见 [ORIGINAL_ACTOR_FRAME_SHADER](ORIGINAL_ACTOR_FRAME_SHADER.md)。

静态随从不再在原召唤尾段结束后切回旧GenFX材质；两条取景路径统一为同原Actor。内部静态/动态停驻图像比较已建立，见 [ORIGINAL_STATIC_ACTOR](ORIGINAL_STATIC_ACTOR.md)。

原随从原画现用源双UV/双纹理/颜色运算及原Gameplay环境探针；动画继续作用于同一实际Actor，用户UV校对保持额外适配。完整动态光照与所有Actor Shader未完成，见 [ORIGINAL_ACTOR_PORTRAIT_SHADER](ORIGINAL_ACTOR_PORTRAIT_SHADER.md)。

敌方Summon使用共用原Actor的专用42槽；每个实例记录独立完成/清理边界，敌方0.49/2.49秒，友方0.56/2.56秒，见 [ORIGINAL_OPPONENT_SUMMON](ORIGINAL_OPPONENT_SUMMON.md)。以下旧节点描述保留为历史记录。

原召唤的完成通知与FSM尾段分离：0.56秒阻塞cue结束后，原实例/Actor作为tail保留并由表现速度独立推进，尾段不保持Busy。暂停冻结，实体移除或源清理结束释放，见 [ORIGINAL_SUMMON_COMPLETION](ORIGINAL_SUMMON_COMPLETION.md)。

正式友方Summon现使用原Actor本地SUMMON_IN表，固定槽位取景同时捕获原入场曲线和粒子，落地声接实际PCM桥接；旧缩放/VFX/通用召唤声避免叠加。源Wait 0.5+2秒完整保留，原清理并行边界、敌方表仍未恢复，见 [ORIGINAL_BATTLE_SUMMON](ORIGINAL_BATTLE_SUMMON.md)。

原AudioPlaythrough已接表现时钟：完成事件在发起声时发出，回退不重复物理播放，释放取消待发cue；PCMvoice由独立播放桥接清理。正式原召唤表路由及完整原混音/语音仍未完成，见 [ORIGINAL_AUDIO_CLOCK](ORIGINAL_AUDIO_CLOCK.md)。

2026-10-11 原召唤音频来源节点：原AudioPlaythrough发起播放后立即Finish，SoundManager缺省SoundDef取源物体组件；音高和音量范围已保存，实际源WAV已恢复。动作时钟/回调及完整混音尚未接入，见 [ORIGINAL_SUMMON_AUDIO](ORIGINAL_SUMMON_AUDIO.md)。

2026-10-11 原Actor动画节点：原AnimationPlaythrough立即启动所属Actor的原Clip后继续FSM，源位置/旋转曲线与粒子同步；原SUMMON_IN离屏动画缺口从1降到0。正式对战仍旧展示模板，全部专属效果及原Shader未完成。见 [ORIGINAL_ACTOR_ANIMATION](ORIGINAL_ACTOR_ANIMATION.md)。

2026-10-11 原Actor效果表节点：原SUMMON_IN FSM在通用随从模板父级初始化并启动真实粒子，4帧GPU捕获；外部Actor的AllyInPlaySpawn动画尚未恢复，播放器明确记录动画缺口。正式表调度、全部原效果未完成，见 [ORIGINAL_ACTOR_SPELL_TABLES](ORIGINAL_ACTOR_SPELL_TABLES.md)。

2026-10-11 通用Actor节点：原初始化在真实模板父级对象上接通Actor/RootObject/RootObjectMesh，原ScaleFrom作用于这些对象；正式比赛仍用旧模型，完整原Actor/Shader及父级调度未完成。见 [ORIGINAL_ACTOR_TEMPLATES](ORIGINAL_ACTOR_TEMPLATES.md)。

2026-10-11 FSM Owner节点：MachineOwner暴露已证明的挂载节点，初始化区分缺Owner与缺父级Actor。现有网格不是完整Actor，专属效果不能猜测绑定，详见 [ORIGINAL_FSM_OWNER](ORIGINAL_FSM_OWNER.md)。

2026-10-11 ScaleFrom节点：原Paw Spawn动作立即缩小并按原Back曲线恢复，支持现有时间轴的delay/speed/重播/释放。原完整Spawn Owner调度未完成，详见 [ORIGINAL_SCALE_FROM](ORIGINAL_SCALE_FROM.md)。

2026-10-11显隐节点：普通Renderer递归显隐及客户端Actor根显隐接入，隐藏模型同步隐藏附属数值；resetOnExit、回退重播、释放恢复已验证。完整原Actor.Show/Hide副作用未完成，见 [ORIGINAL_ACTOR_VISIBILITY](ORIGINAL_ACTOR_VISIBILITY.md)。

2026-10-11生命周期节点：NativeEffectInstance可在精确时间向指定Machine进入原Spell状态，回退重放；NativeEffectPlayer可读取已审核initial_lifecycle/lifecycle计划。原Next规则不跨越缺失Idle/Action。全卡原生命周期自动路由及完整SpellController尚未恢复，未增加启用数，详见 [ORIGINAL_SPELL_LIFECYCLE](ORIGINAL_SPELL_LIFECYCLE.md)。

2026-10-11后续：英雄/随从已接通真实原模型显示供给，原脚本在实体绘制前采样，与合成共用实例身份和年龄。实际缩放驱动模型图像与数值/状态区域；过期借用先恢复再释放。其他Actor类型、原触发/出生/死亡生命周期和全卡脚本未完成，详见 [NATIVE_BATTLE_ACTORS](NATIVE_BATTLE_ACTORS.md)。

原时序增量：NativeImpactDelay 将源完成通知与 NativeDuration 视觉尾部区分；地狱烈焰审阅为 .35/2.1 秒，队列提前显示伤害但保留原捕获退场。Named Clip 时钟可暂停/倒放和回退；服务器不等待动画，见 [ORIGINAL_FINISH_TIMING](ORIGINAL_FINISH_TIMING.md)。

原溶解/无纹理遮罩增量：按原 DXBC 恢复红通道边缘与常量黑色深度遮罩，保持捕获和队列分离。地狱烈焰原约 6 秒 Clip 与当前 1.2 秒启用区间仍不等价；中央过曝未完成修复。见 [ORIGINAL_DISSOLVE_MASK](ORIGINAL_DISSOLVE_MASK.md)。

原 FX 坐标增量：修正地狱烈焰真实环形/内部地面材质的坐标、20 分之一时钟、透明度和饱和顺序，沿用生产双通道合成。深色环消失但中央仍过曝；全卡原版动画未完成。见 [ORIGINAL_FX_COORDINATES](ORIGINAL_FX_COORDINATES.md)。

原目标分支增量：表现批次前后值送入原效果实例，首帧派发与每帧更新分开；回退Seek重复目标分支和可见性状态。BT_035/175不再空白，但主体Animator和纹理绑定缺失，未启用，见 [ORIGINAL_TARGET_BINDING](ORIGINAL_TARGET_BINDING.md)。

Distortion 增量：基础效果按原texel尺寸扭曲MainTex，FX独立scale/scroll；独立Glow沿用原未扭曲UV。原时钟起点、目标卡牌类型和全局后处理仍缺失，新增真实Prefab诊断不触发自动启用，见 [ORIGINAL_DISTORTION](ORIGINAL_DISTORTION.md)。

原 Glow/环境光增量：独立 HDR Glow 输出读取当前 FSM/Clip 参数，保持基础捕获不受未恢复 Bloom 污染；GetBoard/GetAmbient 的进入、每帧取色与重放行为单独恢复。没有服务器动画等待。原后处理和原棋盘光照未完成，见 [ORIGINAL_GLOW_LIGHTING](ORIGINAL_GLOW_LIGHTING.md)。

原捕获指令增量：原 IL 指定的 LightMode 选择、3000 队列分组与 Renderer/Material 排序在客户端执行；服务器不等待捕获。只绘制已恢复基础 Pass，未恢复多 Pass 跳过。细节见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。

原粒子基础运算增量：三个家族使用原 RGBA/UV 数学；棋盘捕获保存 source + destination×factor，内部捕获临时恢复原 RGB/alpha 混合状态。初始参数、FSM、Clip 均以原 uniform 声明过滤旧属性。完整 Shader/脚本仍未恢复，见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。

内部捕获增量：NativeEffectCapture 在 Seek 后按原数据渲染隐藏子树并更新目标材质，再由棋盘特效相机采样。可见状态与相机隔离分开，动画和回退重放均保持一致。最终 alpha 修正为兼容实现，冰霜新星外观仍不合格，未启用；边界见 [RENDER_TEXTURE_RESTORATION](RENDER_TEXTURE_RESTORATION.md)。

## 原 Prefab 播放增量（2026-10-10）

NativeEffectPlayer 可选接入现有施放 cue，使用核验后的区间安排离手、施放、伤害、扳机和历史。奥术爆炸使用原层级替代旧图层；地狱烈焰叠加原冲击波；冰霜新星的原播放器画面核验未通过，继续保留旧效果。服务器立即结算，客户端展示状态按队列追赶；普通交战的同刻双向伤害不改变。原粒子停止发射后继续模拟存活粒子，退场统一释放。原材质曲线、Clip ID 绑定和部分 FSM 动作已执行，完整原 Shader/脚本仍未完成。详细边界、校验与截图性质见 [NATIVE_EFFECTS](NATIVE_EFFECTS.md)。

最新窗口/关联增量（2026-10-09）：公开 Play 补 Summon 以保留战吼前入场；普通英雄反击与耐久共用命中点，真实扳机仍顺序。关联效果在法术离手 0.72 秒后、随从/武器显牌 0.84 秒后再延迟 0.2 秒启动，默认 0.8 秒。下文随从 EffectLaunch 0.35 秒是旧值，现为 0.84 秒。当前构建 3527 条断言、599 项 Python、421 项分组 GPU 检查；视觉过程帧按实际 cue 采样，不停在施法前。截图与人工验收边界见 [WINDOW_FEEDBACK](WINDOW_FEEDBACK.md)。

2026-10-09 交战反馈：双方直接攻击伤害共用 0.28 秒命中点，不因夹在中间的真实 Trigger 让反击延后。没有扳机/召唤/增益等额外结算的普通交战，死亡共同在命中后 0.14 秒开始、各 0.40 秒，并行清除；不是每个随从单独串行等待。真实 Trigger/死亡连锁仍逐个表现。背刺/刺骨/冷血在 0.72 秒离手后分别播放 0.38/0.55/0.70 秒效果，伤害/增益随后显示；音效使用对应 CardDef 关联。以上为本项目时长，未声称官方实测。

当前构建实跑 271 表现、21 校对、33 交互、33 反馈、18 输入/TCP、1794 交战反馈断言，共 2170；新增断言主要覆盖 1～10 张手牌与所有 Hover 槽的排列和计数避让组合，不代表新增上千种规则。Python 独立实跑 594 项；GPU 离屏组件/组合检查 63 项。完整整屏 GUI 与人工比赛核验仍未完成。

时序补充（2026-10-09）：法术先用 0.72 秒离手/显牌/淡出，再启动独立 SpellEffect；施放声音跟随此阶段。随从出牌的 EffectLaunch 保持 0.35 秒、技能保持 0.08 秒。`CardEffectRegistry` 区分起手/施放/命中原音轨与纹理；`SpellParticles` 绘制原粒子纹理并加法混合。`TriggerPulse` 只消费实际监听回调，在随从闪电槽短暂提亮。完整原粒子/AnimationClip 状态机重放未完成，刀扇来源与验证见 [BATTLE_PACING](BATTLE_PACING.md)。

本轮延续现有 Unity 客户端与 TCP 协议，规则和服务器 RNG 不变。Match 适配器增加公开事件中间值及技能已使用字段，基准仍为 Windows Player 客户区 1920×1080。实际视觉差异和未完成验收见 [VISUAL_DIFF](VISUAL_DIFF.md)。

## 逻辑快照与显示快照

`ClientApp.state` 始终接收服务器最新快照，用于合法动作、版本号、同步日志。`Presentation.Display` 是当前显示快照，允许落后。输入在 pending、表现队列 Busy、换牌转场期间锁定；服务器即时结算，不等待动画。

`Presentation.Accept` 保留每条消息的 before/after 与语义事件，逐批播放，后续包不能替换正在播放的攻击。重复版本忽略；换 MatchId、返回菜单会清空。排队上限 64 批，溢出记录警告并同步到最新状态。`Skip` 到最新快照，普通速度 1；F3 打开开发模式后 F4 切换 2 倍速、F5 跳过。

战斗批次从 before 的独立副本开始，按 cue.apply 推进显示。公开 Hit/Heal/GainArmor/Buff/Summon 等事件带 visual 实体中间值；伤害在命中显示，护甲随对应扳机逐次增加。Death 保留实体到塌缩结束再移除；装备武器进入 weapon 槽，不能进入 field。批次完成恢复完整 after 并调用 BatchCompleted，操作历史此时入栏。服务器立即结算，动画不改变逻辑或 RNG。

未覆盖的光环、控制权变化等中间状态仍在批次末尾校正；512 条事件上限以外的连锁仍可能截断。换牌保留自己的转场/快照策略。F5 可跳过队列；菜单认输先跳过视觉再提交当前权威版本，不被长动画阻塞。

## 设计坐标与布局

`BattleLayout.Matrix` 只做一次等比缩放，窗口长宽比变化不会让各控件分别漂移。3D 相机按棋盘 PlaySurface 几何设定视角；TableTop 作为独立原版桌面网格置于棋盘全部可见网格下方。不是铺整张游戏截图，也不是一张 full_board.png。

| 区域 | 设计坐标/策略 |
|---|---|
| 己方英雄 | x=865，y=700，190×200 |
| 对方英雄 | x=865，y=88，190×200 |
| 英雄技能 | x=1090，己方 y=750，对方 y=146 |
| 随从 | 中心 x=960，己方 y=512，对方 y=310；1～7 个自动居中 |
| 结束回合 | x=1496，y=477，205×82 |
| 己方手牌 | 中心 y=1048，数量控制间距、角度和弧高 |
| 对方手牌 | 中心 y=-10，缩小、旋转、重叠卡背 |
| Hover | 手牌中心抬至 y=863，1.37 倍，邻卡避让 |
| Drag | 平滑回正，1.16 倍跟随指针；取消平滑返回 |

手牌按 EntityId 缓存姿态，旋转后命中测试。新卡从牌库进入；在 Update 与 OnGUI 之间切换测试快照/跳过时，未建立缓存的新实体有安全初始姿态。`MinionBoardLayout` 保留存活 EntityId 的排布姿态，增减随从时平滑重排。

## 起手换牌

`MulliganPresentation` 是战斗表现状态，拥有开场牌、旧牌、替换、退出计时，不是普通 GUI Modal。棋盘压暗最高 52%；原版横幅和蓝色按钮按原始网格 UV 渲染。点选显示原版红 X。确认后旧牌退出、新牌从牌库进入；等待另一方时显示等待，双方确认后牌列回到手牌扇形，再恢复战斗输入。

只取原始起手数量；不把幸运币和第一回合额外抽牌并入换牌大卡列。Hover 具有抬升和放大，当前换牌 Hover 仍为直接状态变化，平滑曲线和原版时序核验未完成。

## 时序原语

| 事件 | 当前时长/采样 |
|---|---|
| 抽牌 | 0.62 秒；连续抽牌错开 0.28 秒，等待前序效果 |
| 随从出牌 | 0.84 秒；召唤从 0.58 秒开始 |
| 法术出牌 / 效果 | 离手 0.72 秒；效果默认 0.70 秒，刀扇 0.85 秒 |
| 攻击 | 0.12 秒蓄力、0.16 秒冲撞、0.07 秒命中停顿、0.43 秒返回 |
| 命中/治疗 | 0.55 秒；闪色、震动、数字、光晕 |
| 扳机 / 增甲 | 闪电 0.50 秒；0.24 秒时开始 0.36 秒增甲反馈；逐个排队 |
| 死亡 | 前序伤害/扳机后间隔 0.12 秒，0.64 秒塌缩，结束才移除 |
| 英雄技能 | 0.65 秒；0.32 秒显示结算，法师具有火焰纹理弹道 |
| 回合开始 | 1.1 秒文字淡入/淡出 |

这些是本项目调校值，**不是已测得的官方时间**。`Tween` 提供 Position/Rect、Scale、Alpha/Color 可用的曲线；`AnimationTimeline.At/Then` 提供并行/顺序轨道。实际战斗主要使用 Presentation 的 cue 时轴，不应声称全部 UI 动画均迁移到了 AnimationTimeline。

## 音频与素材

`AudioEventRegistry` 将 Draw、CardPlay、Summon、Attack、Hit、Death、TURN_START 等事件映射到逻辑 ID。出牌离手、召唤落地、攻击启动/命中、换牌确认、回合按钮、胜负使用各自本地 AudioClip。语音继续使用 CardDef GUID 导出的首个默认候选；其余候选不同时叠放。

火球、寒冰箭、奥术智慧及法师技能已由 CardEffectRegistry 关联本机 CardDef 效果资源；刀扇新增原施放/命中声与匕首/拖尾。轨迹、混合和时序为本项目重实现，不能描述为直接执行原 Prefab。其他技能的专属完整音轨、混音和英雄入场语音未完成。

原版数字 BelweNumbers、中文 UGCSH00M、正文 gkai00m 按需从 Font 对象恢复，构建成本地 AssetBundle。原字库、音频、PNG、导出网格都忽略，不提交源码。

## 回归与性能

回合按钮使用原 3D 网格和四元数采样的客户端动画（旋转段 0.5/0.583 秒），不阻塞服务器。召唤环/光点仍在既有 Summon cue 内，睡觉 Z 读取权威 `asleep`，与攻击耗尽分离。恢复边界见 [BOARD_UI](BOARD_UI.md)。

构建执行 `PresentationChecks`：1～10 手牌、1～7 随从、排队/音效/跳过/换牌、刀扇阶段与逐次增甲、延迟历史、武器召唤归属/技能背面。当前为 **271 条表现断言**；加校对 21、交互 33、反馈 33、输入/TCP 14，共 **372 条**，与 Python 的 **588 项测试**分开报告。GPU 离屏检查为 51 项，不代替整屏 GUI 或人工比赛验收。

`--visual-test <scene>` 仅从本地测试快照生成真实实体，动作快照由现有 Match.apply 得到；`Presentation.Seek` 固定过程帧。真实 LAN 比赛不使用测试入口。结算自动截图等待队列清空，避免在胜负动画完成前截图。

每个 profile 的 `performance.json` 记录战斗页面预热 5 秒后的前 3600 个帧样本、均值、P95、缓存规模、队列峰值、托管内存和进程 GC 计数。该统计含结算画面，不是 GPU Profiler，也不能代替长期泄漏测试。

## 人工校对层

2026-10-10 新增 BattleGeometry 设计坐标局部旋转、HeroSelection、BattleLifecycle 和 HeroFragment Shader；起手发牌等待英雄入场，终局等待英雄破碎再显示结果。6 张牌的原粒子模块通过显式 restored/ enabled 开关兼容旧配置，Color/alpha 和加权标量曲线独立采样。当前整套为 605 Python、4101 C# 断言，详细实测和未完成范围见 [INTERACTION_LIFECYCLE](INTERACTION_LIFECYCLE.md)；本文件前面的旧计数属于前轮历史。

`UICalibration` 叠加模板、部件及战场区域的偏移/旋转/缩放/翻转。未配置时采用原有布局。`UIEditor` 通过真实绘制时注册的矩阵选择/操作元素，预览没有复制一套卡面实现；配置独立于玩家 Profile，共用原版缓存。原画 UV/模型网格修改仅使相应模板的 RenderTexture 失效，普通文字/位置校对不重新解码纹理。棋盘节点只在配置版本变化时重设。

编辑器使用本地展示实体，禁用游戏动作输入，不创建比赛；实时比赛中打开编辑器时网络仍接收状态。关闭编辑器恢复正常页面及相机。详细作用域和已知限制见 [UI_EDITOR](UI_EDITOR.md)。

## 战斗交互补充

`BattleInteraction` 根据服务器 moves 提供就绪与无动作提示。`InteractionVisuals` 缓存真实卡框/头像 alpha 的双色轮廓；`BattleHistory` 按显示版本播放公开动作记录，保留死亡实体快照并过滤隐藏信息。技能悬停、随从两侧详情和专用护甲/耐久图标见 [UI_INTERACTION](UI_INTERACTION.md)。历史不是调试事件日志，正常界面显示动作图标，内部 ID/HUD 继续默认关闭。
原非 legacy 动画增量：原 Streamed/Dense/Constant 曲线与直接 Controller 状态接入客户端时间线，AnimatorPlayAction 按原 IL 立即 Finish。服务器不等待 Clip。BlendTree、完整状态机与原 Shader 属性执行仍未完成，见 [ORIGINAL_MECANIM](ORIGINAL_MECANIM.md)。

CommonFX 增量：两个原基础变体执行动画 AlphaMul、原 UV/ramp/mask；其共同 Glow 沿用原未扭曲纹理独立输出。9 槽仅为分支匹配，完整目标纹理、其他变体与生命周期未完成。见 [ORIGINAL_COMMONFX](ORIGINAL_COMMONFX.md)。

原纹理绑定增量：类型 32/33 按原对象表保留精确引用，固定 Texture2D/null 的 Renderer 索引动作进入客户端 FSM，回放恢复初始 RT/纹理。动态 Actor 原画、显式材质与纹理变量未完成，见 [ORIGINAL_TEXTURE_BINDING](ORIGINAL_TEXTURE_BINDING.md)。

原 SetScale 增量：按 NamedVariable 原 IsNone 语义保留向量/各轴，原曲线与 float 默认值驱动缩放，并支持回退重放。119 个动作参数已解析，10 个外部目标绑定未完成，原自然伤害冲击仍未启用，见 [ORIGINAL_SCALE_ACTION](ORIGINAL_SCALE_ACTION.md)。

原 AnimateColor 增量：当前 59 个动作使用的 RGBA 曲线、None、独立通道长度与计算模式进入原材质参数，循环/结束分开，原智慧祝福颜色及回退通过检查；未启用该特效，仍有目标与 Shader 差异。EaseColor 的线性兼容未在本节点恢复，见 [ORIGINAL_COLOR_ACTION](ORIGINAL_COLOR_ACTION.md)。

后续原 EaseFloat/EaseColor 增量已替换上述线性兼容：NativeEaseAnimation 保存独立延迟/运行时钟、from/to、反向及完成事件；当前源资源七种原函数已恢复，未知类型与动态控制明确未绑定。服务器不等待客户端缓动，详见 [ORIGINAL_EASING](ORIGINAL_EASING.md)。

原 Particle Multiply 新增真实 RGBA kernel / blend / Glow，不按亮度 alpha 冒充原乘色；冰霜新星擦除与地狱烈焰粒子使用原路径。当前最终 board 彩色目标透射仍缺失，不新增原动画认证，见 [ORIGINAL_MULTIPLY_SHADER](ORIGINAL_MULTIPLY_SHADER.md)。

后续正式 NativeEffectPlayer 改用浮点 C/K 捕获与乘色/发光双 pass，将原彩色透射接入棋盘。回退重建和 GUI 矩阵保留，未知非对角混合不近似；未新增认证卡，见 [ORIGINAL_AFFINE_COMPOSITE](ORIGINAL_AFFINE_COMPOSITE.md)。

原 Manual/Rotation/TwoLayer 与两个 FX Glow 分支已接入原材质播放器；不是新增完整原动画。紫色符文/升级飘带目前仍缺目标场景，保持禁用；全局后处理与 Actor 所有权仍未完成，服务器立即结算不变。见 [ORIGINAL_EXTENDED_FX](ORIGINAL_EXTENDED_FX.md)。

原 StopAnimation 现在停止指定自身节点的旧版动画时钟，精确采样停止时刻，不误停 Animator。自身目标覆盖错误已修复，月火术 Birth/Death 原分支重拍仍缺目标尺度，继续禁用。服务器不参与动画等待；完整 PlayMode/CrossFade 未完成，见 [ORIGINAL_ANIMATION_OWNER](ORIGINAL_ANIMATION_OWNER.md)。

原 CrossFade 的真实播放模式对照已证实：单 Clip 正权重归一化、0=StopSameLayer/4=StopAll、重复同名不重置时间。正式 ClipClock 尚未实现混合；不能将探针通过说成原动画完成，见 [ORIGINAL_LEGACY_BLEND_ORACLE](ORIGINAL_LEGACY_BLEND_ORACLE.md)。

后续默认层正式混合现保留新旧时钟，Unity 原生变换采样与材质归一化同步，服务器不等待动画。原非零层、完整生命周期和逐卡认证未完成，见 [ORIGINAL_LEGACY_MIXER](ORIGINAL_LEGACY_MIXER.md)。

PlayAnimation 的 StopOnExit 已在客户端状态退出时执行，防止旧时钟覆盖新状态。三组真实箭矢 Death 分支已验证停止；通用 Finish/LoopEvent 和完整原生命周期仍未完成，见 [ORIGINAL_ANIMATION_EXIT](ORIGINAL_ANIMATION_EXIT.md)。
原 WrapMode 采样已接入旧版播放器：已知模式按状态/Clip/组件继承顺序解析，PingPong 与 Loop 使用统一变换/材质采样时刻；新字段尚未回填旧缓存，原自动停用和动态完成未恢复。见 [ORIGINAL_LEGACY_WRAP_RUNTIME](ORIGINAL_LEGACY_WRAP_RUNTIME.md)。

后续PlayAnimation不再按初始固定时长完成：跟随命名ClipClock的当前时间、停止/替换状态和原finishEvent/loopEvent条件；Once正向末端退役已接入。负速边界和完整同步事件分发仍未完成，见 [ORIGINAL_DYNAMIC_ANIMATION_COMPLETION](ORIGINAL_DYNAMIC_ANIMATION_COMPLETION.md)。

旧卡池源模式已回填，Playthrough单次动画也自动退役；原箭矢以Once自然完成退出。新增原准星长时采样仍无画面，不启用，见 [ORIGINAL_ANIMATION_WRAP_CACHE](ORIGINAL_ANIMATION_WRAP_CACHE.md)。

2026-10-11后续节点：原 iTween 线性21/弹性30已接入独立时钟，保留 delay、退出回调注销、缩放 owner/type 停止和淡入退出后继续；Wait的显式finishEvent不再被循环Animation阻塞。准星离屏已可见，但场上目标绑定未恢复、槽位仍禁用。其余曲线及子级停止、完整重入/脚本未完成，见 [ORIGINAL_ITWEEN_RUNTIME](ORIGINAL_ITWEEN_RUNTIME.md)。

进一步恢复现有动作使用的全部10种原曲线，speed优先于time、目标transform快照、None默认时间，以及后代淡入独立时钟。133生命周期调查保留46空白和1个外部对象未绑定，正式启用名单未扩大。完整源脚本及重入/角色绑定仍未完成，见 [ORIGINAL_ITWEEN_BRANCHES](ORIGINAL_ITWEEN_BRANCHES.md)。

角色对象执行已增加显式GameObject供给和Machine局部变量，原RLK目标Actor隐藏/弹性恢复可以驱动供给的原MODEL_MINION模型；8项实际模型GPU检查通过。正常棋盘尚未给NativeEffectPlayer供给Actor对象，缺少时明确记录而不伪造；场上生命周期和显示桥接仍未完成，见 [ORIGINAL_ACTOR_BINDINGS](ORIGINAL_ACTOR_BINDINGS.md)。

# 原手牌姿态恢复节点

原OffScreenRenderFX在普通/Glow/Affine捕获入口更新临时纹理，自有相机/RT显式释放；源姿态与视口转换见 [ORIGINAL_OFFSCREEN_FX.md](ORIGINAL_OFFSCREEN_FX.md)。原完整全局相机服务仍未恢复。

原SetProperty保持None与everyFrame语义，支持的曲线/颜色/属性/材质更新按源动作顺序；粒子逐步推进与自有属性重建恢复见 [ORIGINAL_PROPERTY.md](ORIGINAL_PROPERTY.md)。完整事件帧序仍未恢复。

原SetColorRGBA写具名/本地颜色，不截断HDR，不把None误作0；真实曲线透明度进入材质，见 [ORIGINAL_RGBA.md](ORIGINAL_RGBA.md)。原完整逐动作帧序仍未恢复。

NativeSuperSpawnStart/NativeStartInstance 实际连接源 Birth→Action→Death→None；状态完成在旧状态停止后、新状态播放前，回调不重入采样。详见 [ORIGINAL_SPAWN_START.md](ORIGINAL_SPAWN_START.md)。正式播放器仍未绑定。

NativeSuperStart 使用原游戏事件/延时/启动 Prefab 门禁顺序及真实 WaitForSeconds 类型；状态回调顺序恢复见 [ORIGINAL_SUPER_START.md](ORIGINAL_SUPER_START.md)。实际启动模型与正式播放器尚未连接。

NativeSuperAction 按原子效果计数及设置状态挂起父完成，先完成效果再结束状态；原分派与真实子 Prefab 完成链见 [ORIGINAL_SUPER_ACTION.md](ORIGINAL_SUPER_ACTION.md)。正式播放器尚未绑定。

NativeSuperArea / NativeAreaInstance 按原来源控制方只选择一个区域配置，独立克隆、设置与激活，分离完成及 None 释放；详见 [ORIGINAL_SUPER_AREA.md](ORIGINAL_SUPER_AREA.md)。正式播放器尚未切换，不能以诊断克隆代替正常整卡播放。

原四场景相机的源数据与播放前 BindCaptureView 入口见 [ORIGINAL_SOURCE_CAMERA.md](ORIGINAL_SOURCE_CAMERA.md)。原世界原点和运行时 viewport 未恢复，正式播放器尚未启用此新入口。

原神圣捕获合成恢复 FX_HolyRender 的 R/G 两色解码，避免直接显示红色内部通道。见 [ORIGINAL_HOLY_SHADER.md](ORIGINAL_HOLY_SHADER.md)；完整目标遮罩、原画与正式全卡播放未完成。

原HeroMask/顶点Glow两个无纹理Shader恢复及实际材质绑定见 [ORIGINAL_VERTEX_SHADERS.md](ORIGINAL_VERTEX_SHADERS.md)。不强行启用作者禁用的Renderer；目标Card与激活路由未完成。

SetVisibility按原脚本只操作本对象Renderer，恢复toggle/退出重置，修复误递归。源Card_Hero是原白色纹理，撤回缺失头像的推断；见 [ORIGINAL_LOCAL_VISIBILITY.md](ORIGINAL_LOCAL_VISIBILITY.md)。

SetVisibilityRecursiveDelayAction按原OnUpdate顺序延迟执行并参与FINISHED聚合，提前退出取消，原类没有退出恢复。书本实际源时序见 [ORIGINAL_DELAYED_VISIBILITY.md](ORIGINAL_DELAYED_VISIBILITY.md)。

原ColorInterpolate按颜色数组等区间、实时读取变量和严格大于时长完成；与材质更新按源顺序衔接。17程序/44动作恢复见 [ORIGINAL_COLOR_INTERPOLATE.md](ORIGINAL_COLOR_INTERPOLATE.md)，不代表完整整卡通过。

原AudioPlaythrough延迟协程跨状态继续，到Play时读音源/音量并完成，不等待WAV结束；重入共享原动作计时字段。详见 [ORIGINAL_PLAYTHROUGH_COROUTINE.md](ORIGINAL_PLAYTHROUGH_COROUTINE.md)，完整SoundManager/Actor策略仍未完成。

普通原发声节点携带完整音源模板，不再覆盖原空间曲线为0；原距离/优先级/声像/Doppler、移动跟随与销毁停止见 [ORIGINAL_SOURCE_AUDIO_TEMPLATE.md](ORIGINAL_SOURCE_AUDIO_TEMPLATE.md)。原场景监听器与完整音场未完成。

原既有音源按节点身份复用，空闲由源持有，None代码音量保留前次值。SDK/源FSM实测与未恢复声部策略见 [ORIGINAL_SOURCE_REUSE.md](ORIGINAL_SOURCE_REUSE.md)。

AudioSetVolumeAction按原立即/EveryFrame/None/空目标语义更新正在播放的物理音量，57程序126动作来源及SDK检查见 [ORIGINAL_AUDIO_VOLUME_ACTION.md](ORIGINAL_AUDIO_VOLUME_ACTION.md)。完整外部源/混音未完成。

2026-10-12 普通效果恢复 254 程序的 715 Spell 定位组件及显式 SuperSpell 根定位入口。子实例/原相机尚未连接，详见 [ORIGINAL_EFFECT_PLACEMENT.md](ORIGINAL_EFFECT_PLACEMENT.md)。

原效果运行时现覆盖 35 个 iTween 声明枚举，Scale/Fade 完成帧按原执行器精确写入目标，详见 [ORIGINAL_ITWEEN_COMPLETE_CURVES.md](ORIGINAL_ITWEEN_COMPLETE_CURVES.md)。完整布局运动与 iTween 组件仍未完成。

见 [ORIGINAL_HAND_GEOMETRY.md](ORIGINAL_HAND_GEOMETRY.md)。原世界坐标算法已恢复；正式 HandPresentation 尚未切换，原场景参数、投影与布局 Tween 未完成。

原取消/死亡/结束状态的 AudioStopAction 已停止对应自有音源，保留重播与延迟播放行为。完整外部 SoundManager 尚未恢复，见 [ORIGINAL_AUDIO_STOP_ACTION.md](ORIGINAL_AUDIO_STOP_ACTION.md)。

原 AudioVolumeFadeAction 每帧更新同一发声源，完成前阻止该状态 FINISHED，提前退出保留当前音量；完整 SoundManager 与全卡场景仍未完成，见 [ORIGINAL_AUDIO_FADE_ACTION.md](ORIGINAL_AUDIO_FADE_ACTION.md)。

原循环启动先播放再淡入，结束等待原淡出时间后按stop清理源；默认生成源引用失效并支持回退。12个真实原程序启动检查通过，完整原销毁回调未完成，见 [ORIGINAL_AUDIO_LOOP_ACTIONS.md](ORIGINAL_AUDIO_LOOP_ACTIONS.md)。

奥术爆炸/地狱烈焰正式播放已改为原区域选择、克隆与完成回调。原完成通知和视觉动画区间分别保留，回退重建克隆，见 [ORIGINAL_AREA_PLAYER_INTEGRATION.md](ORIGINAL_AREA_PLAYER_INTEGRATION.md)。

区域原主相机震屏动作现在参与FINISHED计时，已触发震屏独立于特效状态继续；地狱烈焰1秒震屏后2秒Wait，视觉区间3.1秒，.35秒通知保持，见 [ORIGINAL_MAIN_CAMERA_SHAKE.md](ORIGINAL_MAIN_CAMERA_SHAKE.md)。

原 AudioPlaythrough 指定声音复制接收者模板，显式音量覆盖模板值，生成声音快照位置并使用动作限流设置，发起播放即发完成事件；见 [ORIGINAL_PLAYTHROUGH_SOUND.md](ORIGINAL_PLAYTHROUGH_SOUND.md)。

物体音源限流拒绝重播时保留播放位置，替换策略只回收生成音源；动作关闭限流覆盖SoundDef限流，见 [ORIGINAL_OWNED_SOUND_LIMITS.md](ORIGINAL_OWNED_SOUND_LIMITS.md)。

双层烟雾使用原顶点R/A驱动噪声，SneakySmoke使用红通道遮罩插值两种颜色；两类均按原编译程序忽略未使用字段，不再套用通用时间滚动/贴图乘色，见 [ORIGINAL_SMOKE_SHADERS.md](ORIGINAL_SMOKE_SHADERS.md)。

独立原刀扇投射物已在固定相机验证Birth→原Catmull-Rom路径→到达回调→Death，未接入正式根控制器或猜测命中时间，见 [ORIGINAL_MISSILE_PATH.md](ORIGINAL_MISSILE_PATH.md)。
