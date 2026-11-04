# 架构

## 长期维护入口（2026-10-15）

本节是当前结构概览；下方保留既有专题与历史实现记录，不代表这些子系统都已完整接入。

```text
Unity 自主客户端 / Profile / UI 校对
    ├─ TCP JSON Lines → Python game.server → 权威规则 / Match
    └─ 客户端快照 / 表现队列 → NativeEffectPlayer
          ├─ 已审核区域效果 → NativeAreaPlayback / NativeSuperArea
          ├─ 原数据图 → NativeEffectInstance / 类型化动作 / 动画 / 粒子 / 音频
          └─ 等价 Shader / Mesh / Material → Unity 渲染
本机原版资源（只读）→ tools Importer / Scanner → local 缓存 / Registry → 客户端
```

权威状态不等待动画。当前 NativeMissileFlight 是独立已验证端点，NativeSuperImpact 是未验收草稿；完整根 SuperSpell 调度尚未贯通，不能在上图中画成已上线控制器。正式启用的完整原效果仍仅两项。

源码 `game/`、`tools/`、Unity Scripts 与审阅清单 `manifests/` 受 Git 管理；本机 `local/`、构建 `build/`、日志 `work/`、截图 current/reference 均忽略。现有 115 个 manifests 是研究元数据，不自动代表每个都适合公开，需后续发布审计；本轮不删除、不改写历史。

未来 Visual Runtime 在现有端点上逐步实现 Birth/Projectile/Path/Trail/Hit/Death/Audio/Sequence，不预建巨大抽象。可选 Original Runtime Bridge 尚未实现，只允许隔离进程协议，不可污染规则核心。详见 [视觉架构](visual/VISUAL_ARCHITECTURE.md)、[决策](DECISIONS.md)、[当前状态](PROJECT_STATE.md)。

NativeZoneLayout恢复原布局分发并连接区域完成器，明确加载/Card/Zone与缩放延迟依赖；非死亡快照枚举和死亡直接遍历语义分开。当前HandPresentation未替换，原布局算法尚未恢复，见[ORIGINAL_ZONE_LAYOUT](ORIGINAL_ZONE_LAYOUT.md)。

NativeZoneChangeList对接INativeZoneCompletion，Finish与FireCompleteCallback独立；明确ZoneBinding服务承担输入阻塞端点，布局/Busy/ZoneMgr实际服务尚未接入。Server不等待区域动画，见[ORIGINAL_ZONE_CHANGE_LIST](ORIGINAL_ZONE_CHANGE_LIST.md)。

NativePowerExecution以明确服务依赖恢复原任务标记、范围及Update等待；GameState/ZoneMgr缺失不直接伪造完成。服务器不运行这些等待，CreatePowerExecution必须有原历史输入，完整服务与正式联动未完成，见[ORIGINAL_POWER_EXECUTION](ORIGINAL_POWER_EXECUTION.md)。

NativePowerTaskTargets恢复控制器来源/目标收集，NativePowerTaskList仅接受明确原历史输入。NativeEffectPlayer提供OriginalPowerTasks回调和独立PrepareSource路径，缓存来源在当前writer重新登记；默认无原历史提供器，完整任务执行未恢复，见[ORIGINAL_POWER_TARGETS](ORIGINAL_POWER_TARGETS.md)。

NativeSpellTargets保留基础Spell真实可变GameObject列表，VisualTarget访问器共用它；NativeSpellObjectSelection将首项映射到已绑定元数据并实现BestTarget，不能在底层去重。完整任务/派生目标覆盖尚未恢复，见[ORIGINAL_SPELL_TARGETS](ORIGINAL_SPELL_TARGETS.md)。

NativeSpellObjectSelection持有原SourceAuto/TargetAuto优先级，NativeSpellContext.FromEvent从前后快照建立延迟对象关系并沿用借用占用接口；NativeEffectInstance自动接入定位选择。完整TaskList/VisualTarget集合与原Card组件身份未恢复，见[ORIGINAL_SPELL_OBJECTS](ORIGINAL_SPELL_OBJECTS.md)。

NativeSpellTransform恢复原两个dirty标记、世界定位/朝向与父级操作；NativeEffectInstance在Birth/Action调用并通过BindSourcePlacement绑定实际对象。重设到外部父级的自有节点仍由实例释放；正式原对象选择未完成，见[ORIGINAL_SPELL_TRANSFORM](ORIGINAL_SPELL_TRANSFORM.md)。

原攻击事件使用同节点AudioSource/SoundDef解析，声音由该节点下的本地子AudioSource承载；重复播放重启单声音。NativeActorAttackWait保持跨状态等待请求与下一表现帧恢复，Server不等待。原UpdateTransform仍明确计入缺口，见[ORIGINAL_ATTACK_EVENT_AUDIO](ORIGINAL_ATTACK_EVENT_AUDIO.md)。

NativeEffectInstance 按原事件时钟调用源状态结束/完成与粒子组件，状态回调可替换正在采样的片段；旧片段不继续通知。只驱动客户端，正式对战来源/目标和完整控制器未接通，见[ORIGINAL_ANIMATION_EVENTS](ORIGINAL_ANIMATION_EVENTS.md)。

NativeSpellStatePlayback 持有原组件显隐/播放/延迟语义；NativeEffectInstance 显式选择源状态并通过既有动画/粒子时钟执行。未让 Server 等待动画，尚未接通正式 SpellController；反向 Seek 清除显式选择，见[ORIGINAL_SPELL_STATE](ORIGINAL_SPELL_STATE.md)。

NativeActorSpellTable提供按源类型规则的Show/Hide回调入口，尚未与正式NativeBattleSummons/Actor显隐连接。原活动SpellState的动画/声音回调需要单独恢复，不能替换为根物体开关，见[ORIGINAL_SPELL_VISIBILITY](ORIGINAL_SPELL_VISIBILITY.md)。

CommonFX family19严格匹配五关键词，独立处理无Ramp RGBR运算；_SrcBlendOutline/_DstBlendOutline经现有动态材质混合桥更新。源审计工具支持精确关键词阶段选择，见[ORIGINAL_COMMONFX_NONE](ORIGINAL_COMMONFX_NONE.md)。

NativeEffect的family18独立处理原ColorAsAlpha，禁止混入普通透明Shader的RGB贴图乘法及Alpha属性；源依赖审计与GPU浮点检查分开验证，见[ORIGINAL_COLOR_ALPHA](ORIGINAL_COLOR_ALPHA.md)。

NativeMaterial保存原src/dst/alpha混合属性名，NativeEffectInstance解析当前材质属性，NativeAffineCapture消费当前有效状态；避免动态渲染被固定导出枚举覆盖，见[ORIGINAL_TRANSPARENT](ORIGINAL_TRANSPARENT.md)。

原Additive基础/Glow采用不同颜色参数，不能共享通用Color*TintColor公式。family16基础程序与独立Glow分别恢复，12个源CAB的同名程序先按实际字节核对才复用实现，见[ORIGINAL_ADDITIVE](ORIGINAL_ADDITIVE.md)。

NativeEffectInstance区分FSM当前状态的动作完成与实例拥有的延迟声音协程。原状态退出只解除其完成条件，待播放协程仍继续；依据PlayMakerFSM原静态IL重建，不执行官方程序集，见[ORIGINAL_ENTRY_GRAPH](ORIGINAL_ENTRY_GRAPH.md)。

显式原AudioSource模板通过Copy分支消费，输出到重建LegendaryDucking/L_SFX；默认模板仍走实例化属性恢复。反射创建Mixer只在Editor中，运行时使用AssetBundle与Unity原生DSP，完整SoundManager尚未恢复，见[ORIGINAL_MIXER](ORIGINAL_MIXER.md)。

NativeSoundLimits在客户端共享播放桥中按原生成名称计数，独立推进缩放时间释放；动作SoundOptions覆盖SoundDef默认限制，FSM不等待额度释放。原混音器/生成池生命周期尚未完整恢复，见[ORIGINAL_SOUND_LIMITS](ORIGINAL_SOUND_LIMITS.md)。

NativeAudioTemplate恢复原默认AudioSource参数和曲线，NativeEffectInstance快照源parent位置并转换离屏隔离坐标；Playback消费空间混合值。完整混音服务、显式模板和实例限制仍未恢复，见[ORIGINAL_AUDIO_TEMPLATE](ORIGINAL_AUDIO_TEMPLATE.md)。

类型化SoundDef导出与音频动作时钟分离：NativeClipAudioDelay保留原协程采样顺序，NativeAudio.ResolveClip消费主/随机音频与覆盖参数，原PCM通过既有播放桥发送。完整SoundManager边界见 [ORIGINAL_CLIP_AUDIO](ORIGINAL_CLIP_AUDIO.md)。

NativeRandomWait与FSM其余动作独立完成，NativeRandomWaitSampler使用隔离Unity.Random状态保障Seek重播。当前源均为缩放时钟，独立实时输入未接入实例，见 [ORIGINAL_RANDOM_WAIT](ORIGINAL_RANDOM_WAIT.md)。

NativeGeometryBindings 局部查找源Owner组件，保留原材质槽位实例化语义，并由客户端表现实例负责倒放恢复和释放；原MaterialService未完整恢复，见 [ORIGINAL_GEOMETRY](ORIGINAL_GEOMETRY.md)。

原Actor身份的Elite判断使用定义/实体标签和源优先级，独立于稀有度及PremiumType。表现实例发送原FSM事件，历史卡池输出只增加显示用标签，规则与网络契约保持原行为，见 [ORIGINAL_ACTOR_ELITE](ORIGINAL_ACTOR_ELITE.md)。

CameraShaker2由客户端源Timeline时钟驱动，NativeTimelineShaker借用战斗相机并共享原位置快照，退出/倒放/销毁复位；不修改权威规则结算。参数、源算法与剩余边界见 [ORIGINAL_TIMELINE_SHAKER](ORIGINAL_TIMELINE_SHAKER.md)。

NativeParticle 的 color_speed 独立驱动 Unity ColorBySpeedModule，使用源速度范围和 MinMaxGradient，不改变 FSM/服务器时间。12 个节点通过独立原始源引擎对照，见 [ORIGINAL_COLOR_BY_SPEED](ORIGINAL_COLOR_BY_SPEED.md)。

源Timeline控制子树采用NativeRandomizedParticleClock；沿用原自定义周期/重启分支，激活、种子快照、恢复归属于表现实例。受控粒子不再被既有FSM粒子循环重复模拟；不修改服务器或网络结算，见 [ORIGINAL_TIMELINE_CONTROL](ORIGINAL_TIMELINE_CONTROL.md)。

原 Timeline 现由 NativeEffectInstance 独立时钟驱动：精确 Director/Animator 绑定、源 AnimationTrack 曲线、动作完成与动画停止分离、倒放重建、停止退役。控制/镜头轨仍显式诊断，不改变服务器结算；完整脚本尚未完成，见 [ORIGINAL_TIMELINE](ORIGINAL_TIMELINE.md)。

原自定义生成动作使用NativeCustomSpawn管理临时Owner/Actor父级；倒放/释放先反序恢复局部姿态与父级，再销毁效果，保护借用的真实Actor。这是源动作加客户端借用保护，完整原Actor/Timeline未恢复，见 [ORIGINAL_CUSTOM_SPAWN](ORIGINAL_CUSTOM_SPAWN.md)。

原Actor静态原画引用与完整Actor RT分开；NativeEffectInstance的纹理变量归属MachineClock，跨状态保留、倒放按源初值重建，Getter失败不覆盖声明值。Setter只修改实例拥有的材质，详见 [ORIGINAL_PORTRAIT_TEXTURE](ORIGINAL_PORTRAIT_TEXTURE.md)。

正常战场随从与原效果共用NativeBattleActors持久Actor；NativeHighlightState在PortraitContent负责源状态材质/轮廓。释放顺序和UI校准的借用保护见 [ORIGINAL_BATTLE_HIGHLIGHT](ORIGINAL_BATTLE_HIGHLIGHT.md)。

NativeShaderTime恢复原delta/20、严格单次65535回绕、gravity与关闭清零；ClientApp每帧绑定，独立于表现cue的暂停/Seek/Speed。原服务注册框架未执行，见 [ORIGINAL_SHADER_TIME](ORIGINAL_SHADER_TIME.md)。

NativeHighlightFilters提供原UV1四角采样/合成；NativeHighlightSilhouette执行普通原Actor的R8多级替换绘制，归一化根并在finally恢复。NativeHighlightState恢复经源组件证明的普通随从无FSM分支；正式接入范围见上文，生成细节见 [ORIGINAL_HIGHLIGHT_SILHOUETTE](ORIGINAL_HIGHLIGHT_SILHOUETTE.md)。

NativeActorMaterial可以按精确源名称创建原Highlight材质；主渲染入口开启原画/边框，普通随从控制器另绑定源高光平面。英雄/手牌原高光未接入。Shader来源见 [ORIGINAL_ACTOR_HIGHLIGHT](ORIGINAL_ACTOR_HIGHLIGHT.md)。

NativeActorMaterial 按精确源 Shader 分派原画与边框；AssetRegistry 同时要求完整且匹配的 native.shader。Shader 公式共用可验证函数，Editor 阴影探针不进入 Player；源 URP 调度暂作 Built-in 适配，见 [ORIGINAL_ACTOR_FRAME_SHADER](ORIGINAL_ACTOR_FRAME_SHADER.md)。

CardVisuals.CreateOriginalMinion供静态缓存及LiveActor共用；基础包围盒/材质/源SH/UV校准相同，动态2倍留白由ImageRect抵消。Server无变化，见 [ORIGINAL_STATIC_ACTOR](ORIGINAL_STATIC_ACTOR.md)。

NativeActorMaterial以源材质创建普通随从原画专用Shader；原Gameplay烘焙SH经每Renderer PropertyBlock绑定，避免全局补光及原画黑屏。该源通道与未恢复的其余材质分开，见 [ORIGINAL_ACTOR_PORTRAIT_SHADER](ORIGINAL_ACTOR_PORTRAIT_SHADER.md)。

NativeBattleSummons依据实际实体归属选择原表2/42，实例持有自己的源定义和完成/清理时间；借用同一Actor的并发动画仍明确拒绝。敌方不再使用旧通用召唤，见 [ORIGINAL_OPPONENT_SUMMON](ORIGINAL_OPPONENT_SUMMON.md)。

NativeBattleSummons区分BlockingDuration和SourceDuration；按原SpellFinishAction首通知交还队列，清理实例仍借用Actor，ClientApp.Update独立推进/取消。原同Actor共享写入者/Zone服务并未恢复，见 [ORIGINAL_SUMMON_COMPLETION](ORIGINAL_SUMMON_COMPLETION.md)。

NativeBattleSummons为真实友方Summon创建原本地Spell实例，Presentation按事件key驱动，NativeBattleActors供给实际原Actor；固定槽位容器与动画Pose分离，完成时先释放Spell再回收借用模型。Server无动画依赖。原共享SpellManager/敌方表仍未完成，见 [ORIGINAL_BATTLE_SUMMON](ORIGINAL_BATTLE_SUMMON.md)。

原AudioPlaythrough的表现时钟与物理播放分离：FSM按源delay发cue/完成事件，回退重建逻辑而不重复voice；NativeAudioPlayback管理PCM缓存和独立AudioSource，比赛退出停止voice。Server不等待声音。完整原SoundManager/空间音频尚未恢复，见 [ORIGINAL_AUDIO_CLOCK](ORIGINAL_AUDIO_CLOCK.md)。

2026-10-11 原Actor动画节点：AnimationProgram按源GUID/Clip引用绑定实际外部对象；Mixer/退出停止按GameObject身份，借用姿态回放及释放恢复。同一Actor并发写入显式拒绝；外部非Transform通道及完整源控制器尚未恢复。见 [ORIGINAL_ACTOR_ANIMATION](ORIGINAL_ACTOR_ANIMATION.md)。

2026-10-11 原Actor效果表节点：源GUID绑定定义，NativeActorSpellTable按原首项/缓存语义创建与挂接，未恢复inline实例明确拒绝；人工容器与真实原Prefab本地Transform分离。正式Actor.GetSpell共享表及Spawn调度未完成，见 [ORIGINAL_ACTOR_SPELL_TABLES](ORIGINAL_ACTOR_SPELL_TABLES.md)。

2026-10-11 通用Actor节点：源catalog模板独立加载NativeActorIdentity，原初始化沿精确FSM Owner父级写入对象变量。正式Spell挂接与全部Actor脚本未恢复，见 [ORIGINAL_ACTOR_TEMPLATES](ORIGINAL_ACTOR_TEMPLATES.md)。

2026-10-11 FSM Owner节点：组件m_GameObject精确导出Owner，原运行时拒绝把未知Owner默认到根。完整Actor与父级挂接仍未恢复，见 [ORIGINAL_FSM_OWNER](ORIGINAL_FSM_OWNER.md)。

2026-10-11 ScaleFrom节点：原立即起始缩放、延迟ScaleTo回原值分支恢复；未知FSM Owner初始化显式计数，不以事件source代替。见 [ORIGINAL_SCALE_FROM](ORIGINAL_SCALE_FROM.md)。

2026-10-11显隐节点：NativeActorPresentation明确绑定客户端模型VisualRoot；原递归动作区分Renderer与Actor边界，借用显隐在重播/退出/释放恢复。原Actor附属Spell、阴影和状态回调未完成，见 [ORIGINAL_ACTOR_VISIBILITY](ORIGINAL_ACTOR_VISIBILITY.md)。

2026-10-11生命周期节点：原枚举/Next选择分支和已就绪FSM状态映射接入，原运行时支持精确Machine生命周期时间轴及回退重放，NativeEffectPlayer结构化路径接受已审核计划。完整SpellController/PowerTaskList和全卡自动路由未完成，详见 [ORIGINAL_SPELL_LIFECYCLE](ORIGINAL_SPELL_LIFECYCLE.md)。

2026-10-11后续：正式ClientApp已接通英雄/随从原模型供给、原脚本预采样与动态显示。过期效果先恢复对象，再按存活引用释放模型；静态和动态共用校准。其他Actor类型、完整生命周期及全卡Shader/脚本未完成，详见 [NATIVE_BATTLE_ACTORS](NATIVE_BATTLE_ACTORS.md)。

原时序增量：实例保存确定性完成通知，客户端队列采用独立审阅 impact_delay；SetAnimationSpeed 按节点/Clip 保存并在变速点重定时钟。没有执行官方 Spell 代码或将延迟加入服务器；完整外部生命周期未恢复，见 [ORIGINAL_FINISH_TIMING](ORIGINAL_FINISH_TIMING.md)。

原遮罩增量：实例分别绑定原溶解/Black 家族 10/11，常量 Shader 无纹理也可绘制；Black 保留原背面剔除/深度。源规则与服务器不增加动画等待，完整生命周期仍待核验，见 [ORIGINAL_DISSOLVE_MASK](ORIGINAL_DISSOLVE_MASK.md)。

原 FX 坐标增量：NativeEffectInstance 按精确源名选择家族 8/9；基础 Shader 使用 clip.xy 或 raw UV 和 seconds/20。源状态仍立即由服务器结算，客户端捕获/队列独立运行。Scrolling Glow 与其它家族未恢复，见 [ORIGINAL_FX_COORDINATES](ORIGINAL_FX_COORDINATES.md)。

原目标类型增量：NativeSpellContext在实例创建时从公开表现批次和现有CardDef解析类型，缺失不猜测；客户端FSM派发原事件并保存递归可见性退出状态。服务器无动画等待，完整Spell所有权/生命周期仍未恢复，见 [ORIGINAL_TARGET_BINDING](ORIGINAL_TARGET_BINDING.md)。

Distortion 增量：NativeEffectInstance 按精确原 Shader 名选择第四个已核对家族，捕获保持原混合/棋盘预乘隔离。NativeEffectGlowCapture 接受第一 Distortion 家族已核对的 Glow Pass。原场景时间起点和外部实体绑定未恢复，服务器仍立即结算，见 [ORIGINAL_DISTORTION](ORIGINAL_DISTORTION.md)。

原 Glow/环境光增量：RenderGlow 独立输出两个已核对粒子家族的 HDR Glow，动态参数沿用原材质 FSM/Clip；普通 Render 不叠加未恢复的后处理。基础 Shader 使用原 SH 转换/混合数学；取色动作具有显式棋盘上下文与原 RenderSettings fallback。全局光照和完整后处理仍未恢复，见 [ORIGINAL_GLOW_LIGHTING](ORIGINAL_GLOW_LIGHTING.md)。

原捕获排序增量：导出原材质 Queue/LightMode 与 Renderer sortingOrder；NativeEffectCapture 依原 IL 以 3000 分组并排序，显式绘制基础 Pass。旧缓存与未恢复多 Pass 的边界见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。

原基础 Shader 增量：三个家族按原DXBC恢复颜色/UV与混合数学，NativeEffectCapture临时切换原alpha混合后恢复棋盘捕获。FSM/Clip以原uniform声明过滤残留属性，服务器不依赖渲染时间。完整恢复边界与最新验证见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。

内部捕获增量：NativeEffectCapture 根据原组件参数和静态 IL 执行二级 CommandBuffer；动画可见状态独立于棋盘相机隔离。目标材质绑定和释放由客户端管理，服务器不等待动画。来源与未恢复行为见 [RENDER_TEXTURE_RESTORATION](RENDER_TEXTURE_RESTORATION.md)。

原 Prefab 增量（2026-10-10）：`NativeEffectPlayer` 通过 Presentation 的可选 Duration/Draw 回调接入原层级、粒子与 FSM 数据，保持既有队列、输入、规则和 TCP 协议。奥术爆炸和地狱烈焰通过 GPU 与画面核验后启用，其余槽禁用；冰霜新星画面核验未通过。原数据与最新权威状态分离，动画等待只发生在客户端；Offscreen 子树和 layer 与棋盘隔离。兼容 Shader 及静态 IL 审计的恢复边界见 [NATIVE_EFFECTS](NATIVE_EFFECTS.md)。

窗口/原关联增量（2026-10-09）：DesignViewport 统一投影和 GUI 等比矩阵，背景有限宽度由清屏相机留黑边，编辑器用独立 Fit。MinionStatusVisuals 输出有效亡语/扳机/剧毒原模型槽；普通 Play 补公开 Summon/visual/index，保留战吼前入场与插入位置。CardEffectRegistry 加载关联图层元数据，SpellParticles 做 GPU 去底，Presentation 按显牌→入场/施放→命中→历史播放。服务器不等待动画，完整原脚本/Shader 未完成，实测见 [WINDOW_FEEDBACK](WINDOW_FEEDBACK.md)。

手牌与交战反馈增量（2026-10-09）：HandLayout.OwnPose 共用收拢布局，并按校对后的法力计数框整体避让；缓存以数量、Hover、配置对象和 Revision 失效。编辑器和比赛使用同一姿态，不覆盖本机校对。BattleInteraction.PlayRequiresTarget 只读取服务器合法 moves；指向性战吼/已激活连击放置后等待目标，箭头置于卡面和效果上方。普通交战的双向伤害共享命中时刻，纯交战双方死亡并行；实际 Trigger 仍逐个播放。详情见 [COMBAT_FEEDBACK](COMBAT_FEEDBACK.md)。

装备与结算节奏补充：MenuButtonVisuals 缓存原按钮图集的三段绘制，正确解释正文 alpha；CardVisuals 使用独立场上武器模型、技能原背面与原牌库厚度模型。card.weapon 与 weapon 是不同校对模板，已有配置不迁移或覆盖。

Match 的 card_view 共用于最终快照和公开事件中间值，新增 owner/card_type/visual/used，不改卡牌规则或服务器 RNG。Presentation 在独立 before 副本上按时间线更新生命/护甲/召唤/装备，批次结束后恢复完整 after；BattleHistory 由 BatchCompleted 更新。未覆盖的复杂中间状态仍通过最终快照校正。详见 [BATTLE_PACING](BATTLE_PACING.md)。

文字/拾牌反馈延续原模块：`UIText` 统一字体填充、描边、阴影、富文本和状态色，`UICalibration` 可选 text 字段保存样式，`scene.background` 只影响棋盘/桌面取景。`CardPickup` 区分短点击持牌与拖动，`ClientApp.HandleBattleInput` 仅从服务器 moves 提交动作；指向战吼保留落位后的插入位置。

`ObservedGame` 只在实际 minion 监听器回调追加 Trigger，不改变 Fireplace 动作结算；EntityView 增加当前生命/耐久上限和增益标志。`CardEffectRegistry` 读取本地 CardDef 效果映射，发射音效、弹道、命中和闪电提亮进入现有 Presentation 队列，服务器仍不等待动画。原动画/粒子参数已缓存研究，完整执行尚未恢复，见 [UI_FEEDBACK](UI_FEEDBACK.md)。

战斗交互提示通过 `BattleInteraction` 读取服务器合法动作，决定卡牌/技能就绪及无动作时的绿色结束回合提示。`InteractionVisuals` 在客户端缓存模型 alpha 轮廓，不进入规则或网络；`BattleHistory` 保留有界动作快照，按已显示版本开放历史悬停，并过滤未揭示的敌方手牌/奥秘。技能详情、护甲和耐久仍复用 `CardVisuals` 与 `UICalibration`，不改变用户已保存的配置。来源及当前验证限制见 [UI_INTERACTION](UI_INTERACTION.md)。

Unity/C# 负责 Windows 显示、输入和表现；Python 负责可重复资产管线和服务器规则。TCP JSON 行协议，客户端不执行 RNG 或规则。

资产解析不直接向 Unity 2022 加载 Unity 6000 bundle，而按需转换纹理、音频与可读参数。资源通过逻辑 ID 关联本地缓存。

规则采用固定 SHA 的 HearthSim Fireplace 事件/卡牌 DSL，通过自己的 Match 适配器执行。上游依赖安装在虚拟环境，参考仓库保持不变；自己的死亡骑士扩展不能从上游覆盖推断为完成。

`game/database.py` 定义卡池与卡组验证，`game/match.py` 负责单一 RNG、动作校验、事务回滚、可见状态与语义事件，`game/server.py` 配对 TCP 连接并广播各座位视图。Unity 的 `Connection` 不执行规则，`CardVisuals` 复用本地导出的原始卡框几何，`Presentation` 只播放事件。

Unity 2022 编辑器已经合法激活并实际完成 Windows x64 构建，因此采用 Unity；无需以缺失 .NET SDK 为由换成网页或占位客户端。

BattleGeometry 在核心窗口矩阵后组合局部旋转，并计算窗口边缘锚点；HeroSelection 和 BattleLifecycle 只管理交互抬升、入场、死亡和结果时钟，服务器无动画等待。HeroFragments 使用原画像和自有 Shader，OriginalParticleTracks 有界采样原曲线。实现边界与验证见 [INTERACTION_LIFECYCLE](INTERACTION_LIFECYCLE.md)。

`TurnButtonVisuals` 独立渲染原按钮网格，读取本地原旋转采样，按已显示回合播放，静止复用纹理；服务器不等动画。EntityView 的 `asleep` 仅增加公开表现字段，读取 Fireplace 属性，不改变规则。菜单输入隔离、原图集气泡和提示框见 [BOARD_UI](BOARD_UI.md)。

本轮表现层保留原有架构：最新权威 `state` 与 `Presentation.Display` 分离，按批次顺序播放语义 cue。`BattleLayout` 统一 1920×1080 设计坐标，`HandPresentation`/`MinionBoardLayout` 维护 EntityId 姿态，`MulliganPresentation` 管理起手替换和退出，`AudioEventRegistry` 统一声音事件。服务器不依赖动画时间。队列细节、快照切换限制和测试入口见 [PRESENTATION_SYSTEM](PRESENTATION_SYSTEM.md)。

`UICalibration` 在现有绘制函数外应用可持久化变换，`UIEditor` 收集实际绘制时的元素 ID/矩阵并提供校对交互。Card/Portrait/Power/Badge 的预览与比赛共用绘制函数。输入通过视觉矩阵的逆变换判断，箭头/动画位置使用校对后的占位；CardVisuals 的网格在固定原始取景下校对，原画旋转/翻转只修改本项目 Shader 的采样参数。BoardPresentation 按配置版本应用本项目棋盘节点，不改动导出文件或官方客户端。校对数据不进入服务器协议或 GameState。
`native_animation` 解析原 Mecanim 通道、路径 CRC32 与材质 CRC28；`NativeEffectInstance` 在客户端播放直接状态并支持时间回退。它不执行原程序集，也不替代完整 Unity Animator。来源、测试与未完成项见 [ORIGINAL_MECANIM](ORIGINAL_MECANIM.md)。

原 CommonFX 只在完全匹配已核对关键词集合时选两个基础程序分支，其他分支保持兼容路径。原 AlphaMul 曲线进入这些程序，原 named Blend 解析为材质值；独立 Glow 使用原共同程序，服务器不等待 Shader/动画。见 [ORIGINAL_COMMONFX](ORIGINAL_COMMONFX.md)。

SetMaterialTexture 固定 Renderer 纹理绑定仅在客户端执行；初始化后的纹理快照包含 secondary-camera RT，Seek 回退先恢复再重放。未绑定动作显式计数，详见 [ORIGINAL_TEXTURE_BINDING](ORIGINAL_TEXTURE_BINDING.md)。

原 SetScale 进入客户端 FSM：None 轴保留现值，原曲线变量驱动 localScale，持续动作保留 FINISHED 等待，晚阶段写入放在 Clip 之后。原 AnimateFloat 在进入状态时写曲线初值，缺失变量不归零。服务器不参与等待，详见 [ORIGINAL_SCALE_ACTION](ORIGINAL_SCALE_ACTION.md)。

NativeColorAnimation 为原 AnimateColor 的客户端时钟/四通道采样与当前计算模式提供独立实例，进入状态捕获初值，结束后下一步 Finish，转场避免重复采样，回退重建。原循环枚举从序列化 infinity 映射，禁止误把值 2 当 Loop；动态控制值/其他计算分支未完成，见 [ORIGINAL_COLOR_ACTION](ORIGINAL_COLOR_ACTION.md)。

NativeEaseAnimation 单独恢复 EaseFsmAction 时钟：延迟时不推进 runningTime，与 NativeColorAnimation 的原基类行为分开。客户端当前七种原缓动进入 SetMaterialFloat/Color，Finish/event 时间参与原 FSM 转场，回退重建。未恢复分支计入 UnboundEaseActions，不把线性近似冒充原行为，见 [ORIGINAL_EASING](ORIGINAL_EASING.md)。

NativeEffect family 7 是原 Particle Multiply：原四通道乘数及 Zero/SrcColor 用于内部 RT，避免误入 CommonFX family 5/6；Glow 按独立原 pass 输出。最终透明 board 合成尚缺彩色 transmission 通道，不能宣称整场 Multiply 完成，见 [ORIGINAL_MULTIPLY_SHADER](ORIGINAL_MULTIPLY_SHADER.md)。

后续 NativeAffineCapture 已建立 C/K 两通道和生产棋盘双 pass，保留当前对角仿射原混合结果、HDR 及淡出；未知耦合混合显式拒绝。完整原后处理/SRP 与整场动画仍未完成，见 [ORIGINAL_AFFINE_COMPOSITE](ORIGINAL_AFFINE_COMPOSITE.md)。

NativeEffect 精确 Shader 名增加家族 12/13/14：原 Manual 无时间偏移、Rotation 弧度矩阵、TwoLayer 的特殊打包采样；前两者保留未钳制 HDR。Scrolling/TwoLayer 的独立原 Glow 进入捕获，不混用基础 FX 参数，仍未恢复全局 Bloom。见 [ORIGINAL_EXTENDED_FX](ORIGINAL_EXTENDED_FX.md)。

原动画自身目标解析不再被未使用的显式指针覆盖。StopAnimation 按本节点/名称停止旧版 Clip，精确末帧采样与回退重建，独立于 Animator；完整 CrossFade/混合与生命周期仍未完成。见 [ORIGINAL_ANIMATION_OWNER](ORIGINAL_ANIMATION_OWNER.md)。

NativeLegacyBlendChecks 是独立 Editor Play Mode 引擎探针，检验原 CrossFade 需要的归一化、层模式和同名时间语义；尚未改变正式 ClipClock 混合架构。见 [ORIGINAL_LEGACY_BLEND_ORACLE](ORIGINAL_LEGACY_BLEND_ORACLE.md)。

后续正式默认层混合已接入：保留动态 ClipClock，变换用本地 Unity 原生 Animation.Sample，材质通道归一化；非零层和完整生命周期仍未恢复。见 [ORIGINAL_LEGACY_MIXER](ORIGINAL_LEGACY_MIXER.md)。

原 PlayAnimation 按状态保存 StopOnExit 节点/名称；退出前采样混合并停止指定旧版时钟，回退重建，Animator 不被误停。完整完成/循环事件尚未恢复，见 [ORIGINAL_ANIMATION_EXIT](ORIGINAL_ANIMATION_EXIT.md)。

原Actor链路新增Machine局部GameObject变量；SpellGetActorAction通过显式角色对象供给写变量，SetScale/ScaleTo读取当前对象，借用模型重放/释放恢复缩放。NativeEffectPlayer的供给接口基于before/after实体身份，普通棋盘尚未接入，不能以Root伪造Actor。CardVisuals.CreateActor复用现有原模型配置供后续显示绑定；独立实际模型GPU片段已验证，见 [ORIGINAL_ACTOR_BINDINGS](ORIGINAL_ACTOR_BINDINGS.md)。

# 原效果定位连接

OffScreenRenderFX作为独立源组件，避免混用旧RenderToTexture数据结构。原临时RT/世界隔离与本地捕获宿主边界见 [ORIGINAL_OFFSCREEN_FX.md](ORIGINAL_OFFSCREEN_FX.md)。

原组件属性只写自有节点，借用实体、原自定义类型及未恢复值类型明确计数；SDK组件赋值/完整曲线恢复边界见 [ORIGINAL_PROPERTY.md](ORIGINAL_PROPERTY.md)。

源RGBA赋值使用机器变量与实例局部颜色，缺失None/变量信息明确计入未绑定；见 [ORIGINAL_RGBA.md](ORIGINAL_RGBA.md)。共享原程序保持不变。

源启动和区域实例共享 CloneSpell 回调/父子关系登记；实际生命周期与显式可用性服务边界见 [ORIGINAL_SPAWN_START.md](ORIGINAL_SPAWN_START.md)。目标身份存储尚未代替完整原 Card/任务服务。

NativeSuperStart 的原协程与启动状态端点保持客户端所有权，服务器不等待动画。范围与未连接服务见 [ORIGINAL_SUPER_START.md](ORIGINAL_SUPER_START.md)。

原 SuperSpell 分派和完成等待分离于服务器规则；真实源子 Prefab 回调解锁父状态的验证见 [ORIGINAL_SUPER_ACTION.md](ORIGINAL_SUPER_ACTION.md)。导弹/历史/原启动流程仍需原服务恢复。

独立原区域克隆入口与回调顺序见 [ORIGINAL_SUPER_AREA.md](ORIGINAL_SUPER_AREA.md)。服务显式提供原来源/任务/等待/对象池；缺失服务不伪造完成。完整 SuperSpell 控制器与正式场景集成未完成。

原普通特效定位数据与 SuperSpell 根 Birth/Action 的显式定位入口见 [ORIGINAL_EFFECT_PLACEMENT.md](ORIGINAL_EFFECT_PLACEMENT.md)。完整子实例解析、路径与原战场/相机连接仍未完成，正式启用名单保持不变。

NativeEffect family 20 专用原 HolyRender R/G 捕获通道解码，分离原内部颜色数据与最终金色输出；不把通用 RGBA tint 当作原合成。来源和限制见 [ORIGINAL_HOLY_SHADER.md](ORIGINAL_HOLY_SHADER.md)。

family21/22按原顶点程序处理HeroMask/Glow，不套用纹理和粒子光照，无纹理可见性判断与原深度/剔除状态见 [ORIGINAL_VERTEX_SHADERS.md](ORIGINAL_VERTEX_SHADERS.md)。完整Card对象与激活服务未完成。

原局部SetVisibility与ActivateGameObject/递归可见性执行分开；退出队列恢复实际Renderer.enabled，见 [ORIGINAL_LOCAL_VISIBILITY.md](ORIGINAL_LOCAL_VISIBILITY.md)。

延迟显隐使用状态拥有的动作记录，每次客户端更新重新读取延迟，完成后参与原FINISHED聚合，状态退出取消未执行项。服务端结算不等待动画。见 [ORIGINAL_DELAYED_VISIBILITY.md](ORIGINAL_DELAYED_VISIBILITY.md)。

NativeColorInterpolation在原动作更新顺序读动态数组/时长并写颜色变量，区分精确时长和超过时长，退出清理、客户端FINISHED聚合见 [ORIGINAL_COLOR_INTERPOLATE.md](ORIGINAL_COLOR_INTERPOLATE.md)。实时钟仍未绑定。

音频Playthrough协程是组件拥有，不因状态退出取消；各迭代器共享原动作剩余时间，真正播放后参与状态FINISHED聚合。原证据、调度边界与未恢复策略见 [ORIGINAL_PLAYTHROUGH_COROUTINE.md](ORIGINAL_PLAYTHROUGH_COROUTINE.md)。

既有发声节点的完整Apply模板与继承空间曲线分离于PlayClip的Copy/显式空间覆盖。物理voice追踪自有节点、剥离staging平移、源销毁停止，见 [ORIGINAL_SOURCE_AUDIO_TEMPLATE.md](ORIGINAL_SOURCE_AUDIO_TEMPLATE.md)。

keepSource/restartKey仅用于既有原发声节点，普通生成声部仍按播放结束释放。代码音量按源保存、None继承、重建清理，见 [ORIGINAL_SOURCE_REUSE.md](ORIGINAL_SOURCE_REUSE.md)。

活跃物理音源保留随机选择时的音量基数，通过自有源代码音量读取更新volume；不重启/重新选择声音。AudioSetVolumeAction原顺序、EveryFrame与未绑定边界见 [ORIGINAL_AUDIO_VOLUME_ACTION.md](ORIGINAL_AUDIO_VOLUME_ACTION.md)。

AudioStopAction 按自有音源停止版本终止现有播放，后续及延迟 Play 绑定新版本；保留源身份，不取消原组件协程，见 [ORIGINAL_AUDIO_STOP_ACTION.md](ORIGINAL_AUDIO_STOP_ACTION.md)。

AudioVolumeFadeAction 的状态内时间、代码音量、完成停声与 FINISHED 等待见 [ORIGINAL_AUDIO_FADE_ACTION.md](ORIGINAL_AUDIO_FADE_ACTION.md)。显式 AudioSource 指针保留真实组件身份，不能用另一对象的音源替代。

完整程序音源树包括独立引用 Prefab。NativeAudio.Node 对新缓存使用 node/node_known，旧单树缓存使用路径；新索引缺失不回退同名节点，避免独立根之间误绑定。

原循环启动/结束复用音量动作时钟；生成音源结束后子树隐藏、原引用失效、声部释放，回退恢复池化节点。完整销毁回调与SoundManager注销未完成，见 [ORIGINAL_AUDIO_LOOP_ACTIONS.md](ORIGINAL_AUDIO_LOOP_ACTIONS.md)。

正式 NativeEffectPlayer 对已审核的两张区域卡默认分派原 SuperSpell 区域克隆，保留源/任务上下文、父完成回调和回退重建。其余根未切换；见 [ORIGINAL_AREA_PLAYER_INTEGRATION.md](ORIGINAL_AREA_PLAYER_INTEGRATION.md)。

原 MainCameraShakerAction 状态计时与独立 CameraShakeMgr 世界轴更新已接入；共享布局相机在LateUpdate应用，离开战斗停止，完整原相机/GUI关系未恢复，见 [ORIGINAL_MAIN_CAMERA_SHAKE.md](ORIGINAL_MAIN_CAMERA_SHAKE.md)。

NativeAudio 区分原物体播放与指定 SoundDef 生成音源路径；后者不共享原物体的停止、跟随及实时音量回调，原声音服务仍未完整恢复，见 [ORIGINAL_PLAYTHROUGH_SOUND.md](ORIGINAL_PLAYTHROUGH_SOUND.md)。

原场景物体音源与生成音源共用名称配额，但仅生成音源参与FinishFirstGenerated回收；限流检查先于实际重播，见 [ORIGINAL_OWNED_SOUND_LIMITS.md](ORIGINAL_OWNED_SOUND_LIMITS.md)。

NativeEffect新增双层alpha-noise与SneakySmoke原编译公式，两类材质使用独立原始纹理/mip/采样缓存，其他UI/PNG路径保持原行为；见 [ORIGINAL_SMOKE_SHADERS.md](ORIGINAL_SMOKE_SHADERS.md)。

NativeMissilePath/Flight以原三点路径和Catmull-Rom驱动独立原Spell克隆，Birth完成开始飞行，到达回调先于Death；完整根分派、转向与impact宿主未接入正式比赛，见 [ORIGINAL_MISSILE_PATH.md](ORIGINAL_MISSILE_PATH.md)。
