# 资产管线

原ZoneChangeList布局/手牌等待迭代器与谓词静态核对；原Hand等待Actor准备而非固定时长，死亡块消费原缩放延迟。Prefab时长不能替代这些运行时服务，见[ORIGINAL_ZONE_LAYOUT](ORIGINAL_ZONE_LAYOUT.md)。

原ZoneChangeList/ZoneChange/ZoneMgr及FinishWhenPossible迭代器静态IL核对，完成标记不自动通知回调；dirty/Busy是实际运行时依赖，不能用Prefab时长猜测，见[ORIGINAL_ZONE_CHANGE_LIST](ORIGINAL_ZONE_CHANGE_LIST.md)。

原PowerTask、PowerTaskList等待迭代器和ZoneMgr静态IL核对；UniTask原枚举8确认为Update。任务输入payloadType是本地规范化标记，不代表官方Network对象/protobuf已加载或恢复，见[ORIGINAL_POWER_EXECUTION](ORIGINAL_POWER_EXECUTION.md)。

原SpellController两个空Card谓词及PowerTaskList源缓存IL静态导出，明确HistTagChange的PowerType4筛选；原历史不是Prefab数据，也不能从SemanticEvent推测。输入提供器需要明确历史记录/块标志，正式传输未恢复，见[ORIGINAL_POWER_TARGETS](ORIGINAL_POWER_TARGETS.md)。

原Spell/SpellUtils静态IL确认VisualTargets共用m_targets、直接List追加与BestTarget顺序；它们是运行时集合规则，不从Prefab猜测任务目标。明确targetEntities可跨前后快照绑定真实模型，正式PowerTaskList导入未完成，见[ORIGINAL_SPELL_TARGETS](ORIGINAL_SPELL_TARGETS.md)。

SourceAuto按原SpellUtils类型分支选择对象：6附魔用Attached，7武器保留自身；法术/技能复用card/power模型模板，来源关系通过Entity上下文延迟绑定，不能用空对象或武器=附魔代替，见[ORIGINAL_SPELL_OBJECTS](ORIGINAL_SPELL_OBJECTS.md)。

`python -m tools.spell_transform_source`导出205根Spell/派生组件的原Location/Facing、父级策略、命名节点及完整记录，写spell_transforms和original-spell-transform-source.json；原SourceAuto与其他区域选择不等于UI位置，见[ORIGINAL_SPELL_TRANSFORM](ORIGINAL_SPELL_TRANSFORM.md)。

`python -m tools.animation_event_audio`只读恢复8源Prefab的5声音节点与5片段引用，写event_audio_file及original-animation-event-audio.json；按同节点SoundDef选声，事件对象参数不能作为默认声音来源，见[ORIGINAL_ATTACK_EVENT_AUDIO](ORIGINAL_ATTACK_EVENT_AUDIO.md)。

原状态导出同时保留 AnimationEvent 原参数/对象身份和确切接收组件、目标节点；event_horizon 与曲线 duration 分开。不能将回调丢失视为动画已恢复，见[ORIGINAL_ANIMATION_EVENTS](ORIGINAL_ANIMATION_EVENTS.md)。

`python -m tools.spell_visibility_source --export-states` 刷新8实际源程序的 spell_states，并写 original-spell-state-playback.json；51动画引用按原CAB/PathID匹配，不以名字猜测。见[ORIGINAL_SPELL_STATE](ORIGINAL_SPELL_STATE.md)。

`tools.spell_visibility_source`从205原角色附属程序导出215Spell/SpellState组件及容器、状态动画/音频指针。原主层级匹配后才绑定缓存节点；44依赖警告/7外部owner未消除，见[ORIGINAL_SPELL_VISIBILITY](ORIGINAL_SPELL_VISIBILITY.md)。

`tools.commonfx_none_audit`按完整原关键词集合选择VS114/PS1202并检查12个原材质实际关键词/Shader指针；四个混合属性名刷新22缓存，重复零更新。CommonFX汇总现记录四个已恢复基础变体，其余分支未恢复，见[ORIGINAL_COMMONFX_NONE](ORIGINAL_COMMONFX_NONE.md)。

`python -m tools.color_alpha_shader_audit`追踪原ColorAsAlpha编译阶段、常量和五个实际材质Shader依赖，不执行原程序集；不能用普通透明粒子公式替代，见[ORIGINAL_COLOR_ALPHA](ORIGINAL_COLOR_ALPHA.md)。

`python -m tools.transparent_shader_audit`解析65实际源材质的属性混合值，刷新106缓存程序并记录四个属性名，重复零更新。编译状态val=0不替代属性指向的实际值，见[ORIGINAL_TRANSPARENT](ORIGINAL_TRANSPARENT.md)。

原Additive Shader嵌在材质Bundle中；`tools.additive_shader_audit --verify-source`逐一解析100原材质的实际Shader依赖，保存12个CAB身份及相同编译阶段/参数块的证据，见[ORIGINAL_ADDITIVE](ORIGINAL_ADDITIVE.md)。

原入场完整图证据增加FsmStateAction、FsmState、Fsm、PlayMakerFSM及DoCoroutine迭代器静态IL；来源和程序集哈希见original-script-audit.json。原图不删改，NativeEntryGraphChecks.RunGpu连续执行验证，见[ORIGINAL_ENTRY_GRAPH](ORIGINAL_ENTRY_GRAPH.md)。

`python -m tools.native_mixer`规范化原混音器程序，NativeMixerBuilder构建派生Unity原生Mixer；`--verify-bundle`比较实际编译常量。`python -m tools.native_audio_templates`恢复两个显式模板并支持零更新重复执行，见[ORIGINAL_MIXER](ORIGINAL_MIXER.md)。

`python -m tools.native_sound_limits`核对19限制动作原FSM参数，并记录2显式模板和6原Mixer依赖对象。混音器仅提取元数据，尚未运行时重建，见[ORIGINAL_SOUND_LIMITS](ORIGINAL_SOUND_LIMITS.md)。

`python -m tools.native_audio_config`按精确SoundConfig组件及m_PlayClipTemplate指针导出默认AudioSource模板，重复一致，原来源和运行边界见[ORIGINAL_AUDIO_TEMPLATE](ORIGINAL_AUDIO_TEMPLATE.md)。

`python -m tools.native_clip_audio` 按类型化SoundDef原引用导出AudioClip，`--audit-only`核对PCM哈希/采样与源Bundle / CAB / PathID；183程序347动作、130定义及132原音频，见 [ORIGINAL_CLIP_AUDIO](ORIGINAL_CLIP_AUDIO.md)。

`python -m tools.native_random_wait` 精确刷新36个源效果中的37随机等待动作，148参数、重复零更新零失败；来源和时钟边界见 [ORIGINAL_RANDOM_WAIT](ORIGINAL_RANDOM_WAIT.md)。

`python -m tools.native_geometry` 导出4精确网格及2原材质定义，重复刷新零更新零失败。`python -m tools.commonfx_variant_audit` 记录新增RGBR分支的原GPU程序/参数哈希，见 [ORIGINAL_GEOMETRY](ORIGINAL_GEOMETRY.md)。

`python -m tools.native_elite` 精确刷新两个原ActorEliteEventAction的Owner/Actor变量和On/Off事件，重复零更新零失败。历史卡池显示JSON另保留EliteTag及MercenaryTag，未用稀有度推导；来源边界见 [ORIGINAL_ACTOR_ELITE](ORIGINAL_ACTOR_ELITE.md)。

Timeline提取保留CameraShaker2的原模板、幅度/interval和7键曲线/斜率/权重/wrap，精确来源在两个original-timeline manifests。Cecil只读源Helper IL后重实现，未加载官方程序集，见 [ORIGINAL_TIMELINE_SHAKER](ORIGINAL_TIMELINE_SHAKER.md)。

`python -m tools.native_color_speed` 精确回填 7 个 Prefab 的 12 个原 ColorBySpeedModule，原始键/范围/PPtr 写入 manifest，重复执行零更新、零失败；只移除此模块缺口，见 [ORIGINAL_COLOR_BY_SPEED](ORIGINAL_COLOR_BY_SPEED.md)。

Timeline提取新增ControlRandomized的精确ExposedReference目标、ParticleSystem根搜索和子脚本/Director检查；两个同名烟雾按PPtr区分，不按路径混用。原字段/源身份与兼容缺口保留manifest，见 [ORIGINAL_TIMELINE_CONTROL](ORIGINAL_TIMELINE_CONTROL.md)。

`python -m tools.timeline_source --refresh-actions` 刷新21个缓存的源TimelinePlay参数，并提取指定原PlayableDirector图、ExposedReference、精确嵌套绑定和AnimationClip曲线。原始轨道/Clip/Asset参数与Source身份保存在 `manifests/original-timeline-*.json`；两个源图183曲线均已解码，但完整播放仍未完成，见 [ORIGINAL_TIMELINE](ORIGINAL_TIMELINE.md)。

新增 `--refresh-custom-spawn-cache` 按精确源刷新三个自定义入场程序Owner、GameObject/None/变量及reset标志；源追踪见 `manifests/native-custom-spawn-refresh.json` 和 [ORIGINAL_CUSTOM_SPAWN](ORIGINAL_CUSTOM_SPAWN.md)。

原效果FSM纹理变量导出/精确Owner与参数刷新新增 `--refresh-portrait-texture-cache`；声明null、源Texture2D身份与未恢复依赖分别记录。4缓存刷新无失败，RLK_060/061两个子效果未启用，见 [ORIGINAL_PORTRAIT_TEXTURE](ORIGINAL_PORTRAIT_TEXTURE.md)。

ActorStateMgr新状态分支已静态核对，正常战场普通随从绑定源2/3/5/6/7材质与动态R8轮廓。完整原ActorStateMgr未恢复，见 [ORIGINAL_BATTLE_HIGHLIGHT](ORIGINAL_BATTLE_HIGHLIGHT.md)。

原flare的RGB24/DXT1原字节和Repeat采样由`tools.actor_highlight`重建来源/缓存；AssetRegistry独立加载，避免改变共享PNG消费者，见 `manifests/original-highlight-flare.json`。

ShaderTime/嵌套Initialize按精确FullName静态导出，保留程序集SHA和原常量；客户端绑定恢复的运算，不加载官方DLL。见 [ORIGINAL_SHADER_TIME](ORIGINAL_SHADER_TIME.md)。

原灰度Shader包含在官方resources.assets，三个轮廓合成Shader分别来自shader/prefab Bundle；`tools.highlight_filter_audit`记录八类源程序。`tools.actor_highlight`补齐实际高光平面的UV1和UNorm8颜色，并记录源平面无FSM/父级类型推断及历史位移，见 [ORIGINAL_HIGHLIGHT_SILHOUETTE](ORIGINAL_HIGHLIGHT_SILHOUETTE.md)。

`python -X utf8 -m tools.actor_highlight` 恢复原21高光状态槽/20材质/flare与Shader来源记录；现代参数kind=5按有界参数解析。IL审计增量保留此前来源，见 [ORIGINAL_ACTOR_HIGHLIGHT](ORIGINAL_ACTOR_HIGHLIGHT.md)。

`python -X utf8 -m tools.actor_frame_shader_audit` 重建原 UnlitTexture 双 Pass 的程序/参数 SHA256、常量布局和原渲染状态；实际普通随从边框启用恢复材质。阴影调度仍未完成，见 [ORIGINAL_ACTOR_FRAME_SHADER](ORIGINAL_ACTOR_FRAME_SHADER.md)。

MODEL_ACTOR_MINION现供静态与动态普通随从共用，保留原portrait完整材质/第二UV、专用Shader及Gameplay原环境探针。运行 `python -m tools.actor_templates` 与 `python -m tools.actor_shader_audit` 可重建；完整Actor其余材质与动态光照恢复仍未完成，见 [ORIGINAL_STATIC_ACTOR](ORIGINAL_STATIC_ACTOR.md)。

原AnimationProgram、Card_Play_Ally_SpellTable和绑定音频的SUMMON_IN程序已进入正式双方召唤链。敌方使用同Actor的原42槽及独立音频来源记录，可运行 `python -m tools.native_audio --opponent --bind-actor-spell` 重建，见 [ORIGINAL_OPPONENT_SUMMON](ORIGINAL_OPPONENT_SUMMON.md)。

`python -X utf8 -m tools.native_audio --bind-actor-spell` 将精确音频元数据关联至同源Actor效果程序，并仅刷新音频Object参数，保留视觉/目标节点。源WAV已接时钟和PCM桥接，完整混音/正式召唤路由仍未完成，见 [ORIGINAL_AUDIO_CLOCK](ORIGINAL_AUDIO_CLOCK.md)。

2026-10-11 原召唤音频来源节点：`python -X utf8 -m tools.native_audio`按源GUID/CAB/PathID导出AudioSource/SoundDef字段和实际原Clip，空直接Clip指针不等于无声。默认源组件1组、原WAV1、0错误，原音频运行时未接通，见 [ORIGINAL_SUMMON_AUDIO](ORIGINAL_SUMMON_AUDIO.md)。

2026-10-11 原Actor动画节点：通用模板导出新增独立`.actor-animation.json`及元数据，收集随从32/英雄16 Clip、原Animation组件覆盖和引用。源AllyInPlaySpawn 7通道/0.48333秒验证，英雄内建网格缺口保留，不改启用缓存；详见 [ORIGINAL_ACTOR_ANIMATION](ORIGINAL_ACTOR_ANIMATION.md)。

2026-10-11 原Actor效果表节点：`python -X utf8 -m tools.actor_spell_tables`导出两表225槽和独立actor-spell缓存，来源/45程序依赖错误/92程序参数缺口完整记录；`--refresh-positions`回填原根本地位置，不改两个启用程序。见 [ORIGINAL_ACTOR_SPELL_TABLES](ORIGINAL_ACTOR_SPELL_TABLES.md)。

2026-10-11 通用Actor节点：按唯一catalog GUID导出原96节点随从/61节点英雄模板，保留组件/肖像/材质索引及SpellTable来源，1处英雄内建Quad缺失明确记录。详见 [ORIGINAL_ACTOR_TEMPLATES](ORIGINAL_ACTOR_TEMPLATES.md)。

2026-10-11 FSM Owner节点：17Prefab/25FSM归属按CAB/PathID精确补齐，复跑0更新。带真实Actor的特定召唤实例只记录来源，未用作通用模型，见 [ORIGINAL_FSM_OWNER](ORIGINAL_FSM_OWNER.md)。

2026-10-11 ScaleFrom节点：iTween精确刷新增加ScaleFrom，40缓存中1份补齐None语义，复跑0更新。原启用缓存指纹不变，见 [ORIGINAL_SCALE_FROM](ORIGINAL_SCALE_FROM.md)。

2026-10-11显隐节点：静态Actor审核加入后共145类型；11Prefab/26动作/156参数精确刷新，复跑0更新0失败，详见 [ORIGINAL_ACTOR_VISIBILITY](ORIGINAL_ACTOR_VISIBILITY.md)。没有执行官方DLL，也未新增启用效果。

2026-10-11生命周期节点：静态IL审计加入原SpellBase和SpellStateType，共144类型，各restored=false；没有执行官方程序集或改动资源缓存指纹。原状态映射/Next分支与显式时间轴验证见 [ORIGINAL_SPELL_LIFECYCLE](ORIGINAL_SPELL_LIFECYCLE.md)，全卡完整脚本/Shader仍未完成。

2026-10-11后续：NativeBattleActors复用已导出的MODEL_HERO/MODEL_MINION和原画，CardVisuals动态模型使用与静态图一致的UV/Mesh校准。没有新增资源提取或改动已审核原效果缓存；正式启用数仍2。动态显示桥接是本项目代码，不是官方Actor脚本完整恢复，详见 [NATIVE_BATTLE_ACTORS](NATIVE_BATTLE_ACTORS.md)。

原时序增量：静态 IL 扩展到 132 类型，包含完成延迟协程；审阅 impact_delay 经过有限/区间及源 SHA 验证。地狱烈焰两源机的 Wait 2 隐藏时点独立于长 Clip；没有全池启用，见 [ORIGINAL_FINISH_TIMING](ORIGINAL_FINISH_TIMING.md)。

原遮罩增量：识别现代类型 1 空参数记录，严格有边界解析 Utility/Black；dissolve_shader_audit 保存 Black/单纹理溶解原阶段、参数 SHA 和状态。21 槽/15 Prefab 引用不自动启用，见 [ORIGINAL_DISSOLVE_MASK](ORIGINAL_DISSOLVE_MASK.md)。

原 FX 程序增量：只读审计五个 AddMultiplyFX 家族；仅 World/Scrolling 基础程序按命名常量偏移恢复。tools.fx_coordinate_shader_audit 输出两者 GPU/参数 SHA 与绑定，不自动启用卡槽。见 [ORIGINAL_FX_COORDINATES](ORIGINAL_FX_COORDINATES.md)。

目标事件增量：`tools.native_effects --refresh-event-cache`按原机器Bundle/CAB/PathID更新新旧事件参数，保留其他缓存；254 Prefab/2877事件重复0更新/0失败。重名状态按序列核对，原依赖错误保留，静态IL审计129类型，见 [ORIGINAL_TARGET_BINDING](ORIGINAL_TARGET_BINDING.md)。

Distortion 增量：只读导出两个原家族，累计10 Shader/57程序/45反汇编；第一家族的UV扭曲、RGBA和Glow按原偏移恢复，第二家族仅审计。17槽覆盖不代表17张卡完成；源依赖和未绑定事件继续记录，见 [ORIGINAL_DISTORTION](ORIGINAL_DISTORTION.md)。

原 Glow 增量：按两个粒子 Shader 的原 DXBC/参数偏移恢复 GlowTransparent 数学和 mip bias；静态脚本审计扩展到124类型，包含 Board 初始化。原编译程序与完整哈希仍在 original-shader-audit.json，不加载官方程序集执行。恢复边界见 [ORIGINAL_GLOW_LIGHTING](ORIGINAL_GLOW_LIGHTING.md)。

原绘制指令增量：native_effects 导出原 Queue 标签/偏移、自定义材质队列、各 Pass LightMode 和 Renderer sortingOrder；未知队列不猜值。三个主 Prefab 已刷新，其他缓存仍待刷新。详见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。

原 Shader 基础程序增量：导出声明合并 Inspector 属性和编译 m_NameIndices，保留隐藏 uniform，拒绝旧材质残留值。静态 IL 遍历嵌套类型，当前119类型。数学、混合与采样验证见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。

2026-10-10 内部捕获增量：原 RenderToTexture 组件保持 CAB/PathID 与完整参数；新增只读 Shader 编译数据导出，记录 8 个 Shader、45 个程序，其中 35 个 DXBC 程序成功反汇编。源字节码/IL 留在忽略的本地缓存，追踪清单为 manifests/original-shader-audit.json。解析错误如实保存。详见 [RENDER_TEXTURE_RESTORATION](RENDER_TEXTURE_RESTORATION.md)，不能把反汇编成功视作原源码恢复。

2026-10-10 音频修复：`python -m tools.repair_audio_cache` 审计 447 个本地音轨，修复 1 个没有 RIFF 头的全零运行时 WAV，坏文件保留在 `local/runtime/invalid-audio`。`audio_cache` 共用校验用于 Importer、RuntimeExport 及卡牌关联导出；缓存损坏时重新读取合法安装，发布前检查容器和 PCM 帧完整性。来源及前后哈希见 [audio-cache-repairs](../manifests/audio-cache-repairs.json)。重复审计 0 修复，两个后台原生客户端不再报对应 FMOD 错误。

2026-10-10：新增 tools.lifecycle_assets 和 tools.original_particle_tracks，恢复原结算皇冠/横幅、英雄裂纹、两个原音轨，并按 Renderer/Material 关联原 ParticleSystem 的颜色、尺寸、旋转模块。原文件留本机，身份记录在 manifests/lifecycle-assets.json 与 original-particle-tracks.json。完整原 Shader、脚本 FSM 和发射形状未完成，详见 [INTERACTION_LIFECYCLE](INTERACTION_LIFECYCLE.md)。

新增 `python -m tools.window_feedback_assets` 恢复原亡语/剧毒网格与纹理；`python -m tools.card_presentation_assets --limit 300 --all-types` 审计现有 489 张牌并有界恢复主施放关联。`--audit-only` 不导出，`--ids CS2_028,CS2_022` 指定牌，`--refresh-associated` 刷新自动关联，`--render-metadata-only` 仅更新 GPU 去底元数据。不会全客户端解包或扩卡池；其他条件分支只记录。来源/失败见 manifests/window-feedback-assets.json、card-presentation-audit.json 和 [CARD_PRESENTATION_STATUS](CARD_PRESENTATION_STATUS.md)。原 PNG/音频/粒子/动画参数留 local。恢复边界见 [WINDOW_FEEDBACK](WINDOW_FEEDBACK.md)。

手牌/嘲讽/盗贼效果增量：`python -m tools.combat_feedback_assets --research` 检查 CardDef 效果关联；去掉 research 参数按需恢复 GenFX_Taunt_Shield 及背刺/刺骨/冷血的小集合。来源见 [combat-feedback-assets.json](../manifests/combat-feedback-assets.json)，当前 11 条使用来源、58 个研究对象，本机 Registry 1026 项。嘲讽使用原网格及材质贴图，客户端不再画替代圆环。原参数只留 local，轨迹与 Shader 混合由本项目解释。`merge_effects` 加锁合并各导出器所负责的卡牌；重跑早期反馈工具不会删除刀扇或新增盗贼效果。背刺音效由自己的 CardDef 核对，匕首/刀痕贴图复用刺骨，不能说是恢复了背刺全部原效果。

装备/节奏增量入口为 `python -m tools.battle_pacing_assets`，研究入口为 `python -m tools.research_battle_pacing --card EX1_129`。本机恢复场上武器、五档牌库模型，复用技能模型第三 submesh 原背面，追踪刀扇 CardDef 的 SpellPath。来源见 [battle-pacing-assets.json](../manifests/battle-pacing-assets.json)：12 条使用来源及 17 条研究对象，包含 bundle/CAB/PathID/GUID/缓存/usage；本机 registry 1013 项。原 AnimationClip 和 ParticleSystem 类型树只保存在 local/runtime，不提交原曲线或素材。

刀扇使用 GenFX_ValeeraDagger、GenFX_FanOfKnivesTrail 的原纹理，以及 Rogue_FanOfKnives_Cast_1 / Impact_1。客户端重实现轨迹和时序，未运行官方 PlayMaker/Animator/Shader。菜单恢复 alpha 只修改衍生内存纹理，官方文件和导出图集不改写。Scene 为每个实例创建独立材质，离屏渲染使用 sharedMaterials，避免再克隆并泄漏原材质。恢复边界见 [BATTLE_PACING](BATTLE_PACING.md)。

新导出入口为 `python -m tools.feedback_assets`：本机提取 11 条音轨、6 张粒子纹理及 Trigger 网格，18 个 ID，无批量解包。映射与 bundle/CAB/PathID/GUID 见 [feedback-assets.json](../manifests/feedback-assets.json)，registry 增量合并为 984 项。原效果参数、AnimationClip 和 ParticleSystem/Renderer 类型树只保存在忽略目录 `local/runtime`；使用原音轨/纹理/网格，由自写时间线与加法混合 Shader 表现，未直接执行原 Prefab 脚本。关联与限制见 [UI_FEEDBACK](UI_FEEDBACK.md)。

只读官方安装 -> UnityFS 元数据扫描 -> SQLite -> GUID/CAB/PathID 查询 -> 按需导出 -> AssetRegistry -> Unity 表现。

对象身份必须包含 bundle、CAB 和 PathID；导出状态与解析状态分别记录。禁止全客户端批量解包。

参考项目的按块 UnityFS 读取避免资源正文全量常驻。采用时保留 MIT 许可、上游 SHA 和出处。

```powershell
.\.venv\Scripts\python.exe -X utf8 -m tools.importer scan
.\.venv\Scripts\python.exe -X utf8 -m tools.importer query Fireball --type AudioClip
.\.venv\Scripts\python.exe -X utf8 -m tools.importer export <asset-id>
.\.venv\Scripts\python.exe -X utf8 -m tools.export_runtime
.\.venv\Scripts\python.exe -X utf8 -m tools.export_ui
.\.venv\Scripts\python.exe -X utf8 -m tools.export_portraits --ids BT_407t
```

扫描通过 size + mtime 跳过成功且未改变的来源，Manifest 改变时重建 GUID 缓存；导出按来源时间戳缓存，并用 SHA256 去重。运行时 `registry.json` 将逻辑 ID 映射到本地文件名；场景导出保留原始 transform、UV、submesh、顶点颜色、纹理槽、材质参数和粒子原始参数。

渲染素材的 alpha 经常是专用 Shader 通道，而非普通透明度。必须参考原材质与原几何解释，不能直接把整张图集当成单个按钮或卡牌。官方 Shader 尚未直接执行，当前有自写兼容 Shader，特效完整重建仍在推进。

宝石与卡背改由原始网格/UV 渲染；支持 MeshFilter、SkinnedMeshRenderer 的静态几何，以及 CardBack 定义组件中的网格/材质指针。法术与武器的原画位于第二 submesh，随从位于第一，不能统一按序号零处理。

原生 RuntimeAssetLoader 会批量请求缺少的衍生原画。Python 导出器以文件锁串行合并 registry，并原子替换注册表；Unity 在后台进程结束后刷新缓存。来源与失败写入 `local/portrait-export.jsonl`。每台电脑仍需自己的 Python 导出环境与官方安装，不分发原版资源。
## 事件音频补充

`python -m tools.export_voices --classes 4,10,14,1 --ids CS2_182,EX1_116` 仅导出指定职业预设与指定牌的 Play/Attack/Death 默认事件音轨。按 CardDef GUID 追踪 SoundDef 与 AudioSource，不按相似名称猜关联；跳过条件语音分支，保留 CAB、PathID、语言及静态延迟证据。`local/runtime/voice-index.json` 提供客户端索引，`voice-report.json` 保存全部候选和来源。每个事件当前播放首个默认候选，完整互斥候选/底层音效混音仍待实现。

全职业预设：`python -m tools.export_voices --classes 2,3,4,5,6,7,8,9,10,14,1`。本机此次导出 297 条，错误为零；并入此前额外测试牌后索引共 304 条。

所有 RuntimeExport、原画与音轨索引共用 `asset-write.lock`，在短事务内合并增量映射并原子替换 JSON。旧导出器未更改的映射不能覆盖另一个进程新增/更新的映射。`export-history.jsonl` 追加保留每次来源与错误，成功的后续导出不会抹去历史失败。双真实进程锁与过期注册表合并已有回归测试。

## 表现素材与来源清单

运行 `python -m tools.presentation_assets` 按名称和对象类型提取本轮小集合，复用现有 UnityPy/Resource Lab 读取、GUID 关联、去重和增量缓存；不修改参考仓库或官方客户端。生成本地 `runtime/presentation-provenance.json`，同时生成可提交的 `manifests/presentation-assets.json` 元数据。清单有逻辑 ID、名称、来源 bundle、CAB、PathID、GUID、类型、缓存文件名、usage；不含原始素材。

| 逻辑 ID | 原版来源 | 用途 |
|---|---|---|
| SCENE_BOARD | Board_STR，qualityhigh content | 分层棋盘网格/原始材质 |
| MODEL_BOARD_TABLE | TableTop，qualityhigh content | 棋盘外侧酒馆桌面 |
| MODEL_MULLIGAN_BANNER | BannerMulligan，essential mesh | 起手横幅轮廓与 UV |
| MODEL_MULLIGAN_BUTTON | MulliganButton，essential prefab | 蓝色确认按钮 |
| UI_MULLIGAN_MARK | Mulligan_X | 点选替换标记 |
| MODEL_HERO / MODEL_HERO_POWER | Card_Hero / HeroPowerV2 | 头像遮罩、框和技能 |
| MODEL_CARD_* / MODEL_MINION | InHand / InPlay 模型 | 真实卡框、随从、武器 |
| MODEL_GEM_* / MODEL_CARD_BACK | Gem / Card_Back_Default | 数值宝石、牌库与敌方手牌 |
| UI_END_TURN | Board_EndTurnButton | 回合按钮原图集裁切 |
| VFX_FIREBALL_BOLT | GenFX_Fireball_1Hot | 重实现火球/法师技能弹道 |
| FONT_*_SOURCE | BelweNumbers / UGCSH00M / gkai00m | 原版数字与中文字库 |

原始棋盘由 33 个可渲染网格组成，保留前景道具与层级；加 TableTop 后不再在纯黑背景里摆一张棋盘图。PlaySurface_Combined 是原 Prefab 中 inactive 的替代网格，只用于边界参照；实际可见地面是 PlaySurfaceProjected 下的八块网格，不能一起隐藏。桌面必须按世界包围盒放到棋盘全部网格下面，避免遮住桌面上方的棋盘。

`usage=inspected_only` 代表已经解析导出，但没有直接用于当前运行画面。原横幅和 StandardButton 图集的 alpha 不是普通透明轮廓；横幅/换牌按钮实际用原始 mesh/UV，未把灰色图集整块铺到界面上。箭头/光晕图集目前只作研究；实际箭头、选中环、死亡淡出和冲击光晕是本项目代码重实现，不能算作恢复原始 ParticleSystem。

字体先输出到忽略的 `Assets/LocalFonts`，构建脚本生成 `local/runtime/local-fonts.assetbundle`，需要 `com.unity.modules.assetbundle`。PNG、WAV、TTF、AssetBundle、reference/current/diff 图片均不进入 Git。

## 原 Prefab、ParticleSystem、FSM 与脚本审计（2026-10-10）

`tools.native_effects` 复用 RuntimeExport/UnityPy 的 GUID、依赖读取、网格/纹理缓存和资产写锁，导出本机原层级、粒子模块、AnimationClip、材质参数及 PlayMaker 动作数据。当前为 352 个效果槽 / 254 个不同 Prefab，来源与缺失依赖记录在 [native-effect-audit.json](../manifests/native-effect-audit.json)。导出不自动启用；[native-effect-review.json](../manifests/native-effect-review.json) 与 GPU 报告共同约束启用，并校验缓存 SHA256。

`tools/export_script_il.ps1` 使用 Unity 的 Mono.Cecil 静态读取官方程序集，输出本机忽略的 IL 与跟踪清单 [original-script-audit.json](../manifests/original-script-audit.json)，不执行或注入官方脚本。所有原二进制/参数仍仅存本机。当前 Shader 是兼容重实现，未恢复原平台完整程序。复现命令、数据数量及明确未完成项见 [NATIVE_EFFECTS](NATIVE_EFFECTS.md)。

## UI 人工校对配置

`tools.board_ui_assets` 按需提取原菜单齿轮、回合按钮/旋转采样、提示框、睡觉/召唤粒子材质引用，来源见 `manifests/board-ui-assets.json`。FrameManaEnemy 裸网格材质没有贴图，蓝色气泡从原 STR_FRAME 图集恢复。原二进制均忽略，恢复边界见 [BOARD_UI](BOARD_UI.md)。

`Start-UIEditor.cmd` 使用同一套 AssetRegistry、原始模型和卡牌绘制函数。校对结果保存在忽略的 `local/ui-calibration.json`：只存稳定元素 ID、设计坐标偏移、角度、缩放和翻转，不改写 PNG、模型导出 JSON、来源 Manifest 或官方安装。

原画校对由本项目 Shader 调整 UV；模型网格校对在固定原始相机取景后应用。仅原画/网格调整使对应卡牌模板的 RenderTexture 缓存失效，文字和整体位移不重复解码资源；棋盘节点仅在配置版本改变时重新应用变换。参考图叠加只用于编辑器比对，不进入正常对战。配置可单独导出并复制到另一台局域网客户端，详见 [UI_EDITOR](UI_EDITOR.md)。

## 战斗交互素材（2026-10-09）

`python -m tools.interaction_assets` 增量提取六项小集合，复用 RuntimeExport/UnityPy 的现有读取、GUID、缓存和写锁；官方目录及 Resource Lab 不变。来源清单为 [interaction-assets.json](../manifests/interaction-assets.json)，含 bundle、CAB、PathID/GUID、类型、缓存路径和用途。

| 逻辑 ID | 原对象 / 纹理 | 使用方式 |
|---|---|---|
| UI_POWER_DETAIL_FRAME | Lettuce_Ability_SpellCaster_Frame | 蓝色卷轴纹理 |
| MODEL_POWER_DETAIL | Lettuce_Ability_Frame | 原始卡框/说明网格，技能圆盘及文字由本项目组合 |
| MODEL_HISTORY_CARD | HistoryTile_Card | 左侧历史图标框与原画网格 |
| MODEL_HISTORY_ATTACK | HistoryTile_Attack | 攻击历史的原框、武器装饰与原画网格 |
| MODEL_GEM_ARMOR | Hero_Armor / Armor_D | 英雄护甲的银色完整盾 |
| MODEL_GEM_DURABILITY | Wep_SheildBroken / Card_Weapon_Icons_D | 武器耐久的破裂盾 |

只为本机已有 `CARD_` 缓存读取历史头像的 CardDef GUID 材质参数 `_Scale/_OffsetX/_OffsetY`，写入带 CAB/PathID/GUID 的忽略文件 `local/runtime/history-portraits.json`。当前 537 条参数，不是新增 537 张卡或重新批量导出美术。该 JSON 与其他资产索引共用写锁/原子替换；参数由自写 Shader 解释，原 Shader 的精确采样还未核验。

本轮六项提取错误为零，合并后本机 registry 为 966 项。保留用户校对，不根据材质颜色猜测宝石类别；生命、护甲、耐久分别指定资源 ID。绿色/白色轮廓、回合按钮光晕、历史布局与详情文字是自行重实现；恢复的是原版网格、UV、纹理和参数。
Mecanim 导出新增原 Clip source 身份、真实 StopTime、曲线与 Controller 状态。覆盖审计可通过 `python -m tools.native_animation_audit` 重建；36 个解码轨道不等于 36 个已完成动画，材质属性仍依赖对应原 Shader 恢复。见 [ORIGINAL_MECANIM](ORIGINAL_MECANIM.md)。

CommonFX 增量：按原条目边界拆分 GPU 程序/参数块，修正旧参数读取跨界造成的错误程序审计；按 Shader 阶段 keyword mask 关联具体变体。51 缓存只刷新源关键词/named Blend，两组基础分支及共同 Glow 有实现，不代表整个 Shader 恢复，见 [ORIGINAL_COMMONFX](ORIGINAL_COMMONFX.md)。

FSM 原材质/纹理参数可用 `python -m tools.native_effects --refresh-asset-cache` 精确刷新，97 个 Prefab 的 693 个参数已核对来源，重复执行零更新。来源与运行时限制见 [ORIGINAL_TEXTURE_BINDING](ORIGINAL_TEXTURE_BINDING.md)。

SetScale 可用 `python -m tools.native_effects --refresh-scale-cache` 重建原主/独立引用层级索引并刷新原 None/packed 参数。55 个 Prefab / 833 参数重复零更新；已区分保留轴与真实 0，10 个外部目标仍未绑定，见 [ORIGINAL_SCALE_ACTION](ORIGINAL_SCALE_ACTION.md)。

`python -m tools.native_effects --refresh-color-cache` 按源 FSM 精确刷新原 AnimateColor 及曲线 infinity 元数据；25 个 Prefab / 885 参数重复零更新。曲线内部序列化值不可直接 cast 为 WrapMode，已用本机 Unity 实际曲线验证映射，见 [ORIGINAL_COLOR_ACTION](ORIGINAL_COLOR_ACTION.md)。

`python -m tools.native_effects --refresh-ease-cache` 复用源 FSM 参数刷新流程，恢复 34 个 Prefab / 122 个 EaseFloat/EaseColor / 1220 参数的 None 语义，重复零更新。原 easing Enum 与静态 IL 一一核对，源身份见 native-ease-refresh.json；不会启用未评审特效，见 [ORIGINAL_EASING](ORIGINAL_EASING.md)。

Multiply Shader 原 bytes 用 inspect_compiled_shaders 的 --bundle essential_base_global-shader-0.unity3d --pattern '%Particle Multiply%' 只读导出。Windows modern kind 2/3/4 参数记录按独立边界解析；`python -m tools.multiply_shader_audit` 关联原 VS/PS、参数 SHA 及 44 个缓存的材质源。不是导出即启用，见 [ORIGINAL_MULTIPLY_SHADER](ORIGINAL_MULTIPLY_SHADER.md)。

彩色透射节点没有重新导出或改写源资源：NativeAffineCapture 用同一原源材质 blend 重放，数学/生产合成 GPU 487 项对照；源身份和启用 SHA 保持原值。合成属于本项目重实现，不冒充恢复了完整原后处理源码，见 [ORIGINAL_AFFINE_COMPOSITE](ORIGINAL_AFFINE_COMPOSITE.md)。

fx_coordinate_shader_audit 现记录五个原 FX 家族的基础/Glow 程序、参数 SHA、阶段绑定和逐 Pass 常量偏移。Manual/Rotation/TwoLayer 按原 DXBC 恢复，新增真实 Prefab 图片过滤 Windows 文件名非法字符；保持源效果 ID 和 SHA，不批量启用。见 [ORIGINAL_EXTENDED_FX](ORIGINAL_EXTENDED_FX.md)。

新增 `tools.native_effects --refresh-animation-cache`：原层级/动作身份核对后，恢复自身目标及四类动画动作参数；111 Prefab/220 动作/1214 参数，重复零更新。两已启用槽重拍核对 SHA 后重新登记，不能沿用旧缓存哈希。见 [ORIGINAL_ANIMATION_OWNER](ORIGINAL_ANIMATION_OWNER.md)。

native-legacy-blend-oracle.json 记录两个原动画类型的静态 IL SHA、阈值分支与真实 Unity 模式对照；restored=false。运行日志和逐帧权重留在 work，未重新导出或启用原卡效果。见 [ORIGINAL_LEGACY_BLEND_ORACLE](ORIGINAL_LEGACY_BLEND_ORACLE.md)。

正式混合从已恢复曲线构建实例内 legacy Clip，不修改原缓存字节或官方安装；新增 Unity 内置 Animation 模块依赖。仍无新增启用槽，见 [ORIGINAL_LEGACY_MIXER](ORIGINAL_LEGACY_MIXER.md)。

退出停止增量复用原动画刷新清单的 stopOnExit 参数，没有改写源缓存。三组原箭矢拥有节点/Take 001 的 .633333 秒、22 通道已验证，保持禁用，见 [ORIGINAL_ANIMATION_EXIT](ORIGINAL_ANIMATION_EXIT.md)。
# 原旧版动画循环信息补充（2026-10-11）

新导出保留 Clip 原 WrapMode 和是否已知，并保存 Animation 组件的覆盖、默认 Clip、自动播放和来源。现有缓存调查命令为 `python -m tools.native_animation_wrap_audit`；只读原资源和已审核缓存，输出来源审计。正式模式解释和缓存刷新尚未完成，见 [ORIGINAL_ANIMATION_WRAP](ORIGINAL_ANIMATION_WRAP.md)。

后续已精确回填254程序中的189缓存，保留曲线/材质/粒子并重新审核两个启用指纹；0映射失败、59内建依赖警告保留。命令 `python -m tools.native_animation_wrap_refresh`，见 [ORIGINAL_ANIMATION_WRAP_CACHE](ORIGINAL_ANIMATION_WRAP_CACHE.md)。

`python -m tools.native_effects --refresh-itween-cache` 精确刷新原FadeTo/ScaleTo参数和owner节点；参数恢复不代表动作执行。静态IL工具支持ExtraTypes/ExtraAssemblies和枚举常量，原库不加载执行，见 [ORIGINAL_ITWEEN_SOURCE](ORIGINAL_ITWEEN_SOURCE.md)。

后续正式播放器已恢复原linear21/easeOutElastic30的时间驱动淡入/缩放子集。源材质alpha=0保持原值，运行时按源FadeTo渐变，两个准星离屏可见；源cache和启用名单未改变。其他分支拒绝并审计，原目标对象绑定未完成，详见 [ORIGINAL_ITWEEN_RUNTIME](ORIGINAL_ITWEEN_RUNTIME.md) 和 `manifests/native-itween-runtime-checks.json`。

`python -m tools.native_effects --refresh-actor-cache` 精确回填25Prefab的31个Actor动作/93参数和None信息，重复0更新0失败。SpellInitActorVariables仅参数保存，未完成执行。启用的两个cache指纹未变。原角色对象供给与实际原模型验证见 [ORIGINAL_ACTOR_BINDINGS](ORIGINAL_ACTOR_BINDINGS.md)，普通棋盘显示绑定尚未完成。

# 原普通特效定位来源

export_program保存OffScreenRenderFX原类名、组件身份、指针与raw，新增offscreen_fx列表；已有prefabs-only入口重导原启动图。见 [ORIGINAL_OFFSCREEN_FX.md](ORIGINAL_OFFSCREEN_FX.md)。

kind36从原fsmPropertyParams读取；`tools.refresh_properties`刷新99程序554动作，保留raw、目标CAB/PathID和完整参数。schema归一化与原身份核对见 [ORIGINAL_PROPERTY.md](ORIGINAL_PROPERTY.md)。

`python -m tools.refresh_rgba` 复用原FSM字段解析，刷新22程序56个RGBA动作的None语义与参数；独立报告 native-rgba-refresh.json，不覆盖AnimateColor报告。见 [ORIGINAL_RGBA.md](ORIGINAL_RGBA.md)。

`python -m tools.super_start_source` 导出232原启动配置及当前两个祝福共用的独立启动Prefab/音频，复用区域的精确身份解析。源与视觉缺口见 [ORIGINAL_SPAWN_START.md](ORIGINAL_SPAWN_START.md)。

原根 raw.m_ActionInfo 的延时经类型字段读取，另保留 d__65/SpellActionInfo 静态来源，见 [ORIGINAL_SUPER_START.md](ORIGINAL_SUPER_START.md)。未改变原缓存文件或审核指纹。

已导出的原 SuperSpell 静态 IL 用于恢复分派与完成等待，不执行原程序集。范围与缺口见 [ORIGINAL_SUPER_ACTION.md](ORIGINAL_SUPER_ACTION.md)。本节点没有新增原资源或扩大审核启用名单。

`python -m tools.super_area_source` 保存 232 根区域配置；`python -m tools.export_super_area_prefabs` 导出当前两个审核卡的三个独立区域 Prefab/音频，不改既有审核图。来源、执行边界见 [ORIGINAL_SUPER_AREA.md](ORIGINAL_SUPER_AREA.md)。

`python -m tools.battle_camera_source` 保存原 @player/level30 四相机及 Transform 父链，详见 [ORIGINAL_SOURCE_CAMERA.md](ORIGINAL_SOURCE_CAMERA.md)。

`python -m tools.spell_transform_source --kind effects` 导出普通效果根与嵌套/独立引用 Spell 的定位配置，校验原层级并记录 CAB/PathID/Bundle。export_program 同步保留这些字段；不意味着原子 Spell 实例路由已经完成。见 [ORIGINAL_EFFECT_PLACEMENT.md](ORIGINAL_EFFECT_PLACEMENT.md)。

`python -m tools.holy_shader_audit` 重放原 FX_HolyRender 两阶段、常量布局和8材质源指针，保存 native-holy-shader/source-bindings manifest；普通与启动缓存合计27程序。见 [ORIGINAL_HOLY_SHADER.md](ORIGINAL_HOLY_SHADER.md)。

`python -m tools.vertex_shader_audit` 回读两个原无纹理Shader及所有材质绑定，保存native-vertex-shaders/source-bindings；来源与真实覆盖见 [ORIGINAL_VERTEX_SHADERS.md](ORIGINAL_VERTEX_SHADERS.md)。

原Card_Hero白色纹理已按原材质指针回读验证，撤回缺失英雄头像的推断；来源CAB/PathID和原脚本证据见 [ORIGINAL_LOCAL_VISIBILITY.md](ORIGINAL_LOCAL_VISIBILITY.md)。

`python -m tools.refresh_visibility_delay` 按真实层级重读原延迟动作4参数与None，并同步该引用未知动作审计。来源重放与限制见 [ORIGINAL_DELAYED_VISIBILITY.md](ORIGINAL_DELAYED_VISIBILITY.md)。

`python -m tools.refresh_color_interpolate` 重读原FsmColor数组头/元素、311参数并同步17引用的未知动作和未解析计数，见 [ORIGINAL_COLOR_INTERPOLATE.md](ORIGINAL_COLOR_INTERPOLATE.md)。其他数组类型仍未恢复。

Cecil静态审计新增AudioPlaythroughAction/<Delay>d__17，保留原程序集哈希与IL，没有执行原DLL，见 [ORIGINAL_PLAYTHROUGH_COROUTINE.md](ORIGINAL_PLAYTHROUGH_COROUTINE.md)。

已导出的原AudioSource字段接入普通音源的完整模板应用，未改资源缓存；SDK验证与原SoundManager证据见 [ORIGINAL_SOURCE_AUDIO_TEMPLATE.md](ORIGINAL_SOURCE_AUDIO_TEMPLATE.md)。

原PlayImpl/RegisterExtension/SetVolume静态IL核对既有音源身份和代码音量保留，没有执行原程序集，见 [ORIGINAL_SOURCE_REUSE.md](ORIGINAL_SOURCE_REUSE.md)。

`python -m tools.refresh_audio_volume`及`--extra-only`分别重读普通与启动/Actor的音量动作，共57程序126动作，真实层级与None重放见 [ORIGINAL_AUDIO_VOLUME_ACTION.md](ORIGINAL_AUDIO_VOLUME_ACTION.md)。

`python -m tools.refresh_audio_stop` 回读 10 程序、16 原停止动作的真实 OwnerDefault 目标；普通、Actor、启动程序统一扫描，来源见 [ORIGINAL_AUDIO_STOP_ACTION.md](ORIGINAL_AUDIO_STOP_ACTION.md)。

`python -m tools.refresh_audio_fade` 回读 61 程序、168 动作、870 参数，并恢复 12 处显式 AudioSource 组件指针和所属节点；来源比较保留完整缓存，见 [ORIGINAL_AUDIO_FADE_ACTION.md](ORIGINAL_AUDIO_FADE_ACTION.md)。

随后 `python -m tools.recover_fade_audio` 接入完整程序层级中的主/引用 Prefab 音源与音频文件。按 node/node_known 保留独立根的身份，避免同名路径误绑定；61 程序175音源/声音定义节点的来源记录于 native-audio-fade-bindings.json。

原循环动作来源：`python -m tools.refresh_audio_loop`，随后 `python -m tools.recover_fade_audio --loop`，12程序36动作/28节点/24音频引用。固定主音频与默认参数验证见 [ORIGINAL_AUDIO_LOOP_ACTIONS.md](ORIGINAL_AUDIO_LOOP_ACTIONS.md)。

正式区域播放复用 native-super-area-config.json、native-super-area-prefabs.json 的已导出三个独立源 Prefab，不生成背景截图替代实际特效。范围与截图记录见 [ORIGINAL_AREA_PLAYER_INTEGRATION.md](ORIGINAL_AREA_PLAYER_INTEGRATION.md)。

`python -m tools.refresh_main_shake` 回读66程序87原主相机动作348参数；更新来源字段后重新GPU采样及审核指纹，不将缓存刷新等同整卡完成，见 [ORIGINAL_MAIN_CAMERA_SHAKE.md](ORIGINAL_MAIN_CAMERA_SHAKE.md)。

`python -m tools.refresh_playthrough_audio` 恢复280原声音动作及173音频根，保留已有音频文件绑定，记录指定 SoundDef 的源身份和PCM依赖；见 [ORIGINAL_PLAYTHROUGH_SOUND.md](ORIGINAL_PLAYTHROUGH_SOUND.md)。

`python -m tools.particle_alpha_shader_audit` 核对双层alpha-noise/SneakySmoke原编译程序与材质；`python -m tools.smoke_source_textures` 导出8原始纹理及17属性绑定、mip/采样设置，见 [ORIGINAL_SMOKE_SHADERS.md](ORIGINAL_SMOKE_SHADERS.md)。

`python -m tools.super_missile_source` 回读232原根组件，单独导出刀扇/刺骨2个.missile-spell.json与原音频依赖，不改已审核根图；见 [ORIGINAL_MISSILE_PATH.md](ORIGINAL_MISSILE_PATH.md)。
