# 实际进度

## 当前路线与优先级（2026-10-15）

以下路线优先于下方历史横向推进记录。保留全部旧成果，但暂停继续扩卡。

| 阶段 | 工作 | 依赖 / 出口 |
| --- | --- | --- |
| P0 | 长期文档、Task 协议、已有证据和视觉数据库种子 | TASK-0001；App 项目操作仍需人工 |
| P1a | SuperSpell 命中分派与生命周期契约 | TASK-0002；先审查暂停草稿，独立验收后进入编排 |
| P1b | 刀扇/刺骨根 SuperSpell 自主编排，完善飞行/命中/任务同步 | TASK-0003 依赖 0002；未知分支明确报错 |
| P1c | Shader 方法与官方关键帧对比闭环，发布视觉逆向标准工作流 | TASK-0004；可先整理证据，整卡评估依赖 0003 |
| P2 | 依赖图自动导出、数据库 schema 演进、资源导入隔离/发布审计、无原安装运行路径 | P1 真实需求驱动，届时分配 Task |
| P3 | Hearth Visual Browser：卡/效果/Prefab/材质/Shader/纹理/动画/粒子/音频及依赖、生命周期、映射、等级、Task | 数据模型稳定以后实施，本轮不开发 |
| P4 | 按标准批量迁移现有卡；自定义视觉/卡牌/冒险/Boss/Mod | 首套完整案例和独立验收通过以后 |
| 研究支线 | 少量可选 Original Runtime Bridge | 仅当真实成本/依赖证据支持；不设为核心必需 |

每个 Feature 完成先 READY_FOR_REVIEW，再由 V 聊天独立验收；通过后 VERIFIED，总控归档为 DONE。下一聊天为 F002-SuperSpell命中分派与生命周期契约。工作区当前串行 master，无并行功能分支；未来并行按任务开 worktree。

## 历史进度（以下内容保留，不作当前完整度声明）

2026-10-12 原区域布局分发：loading/Actor捕获等待、Hand准备谓词、普通回调顺序和死亡块独立路径恢复，16新增Unity检查通过。真实Zone姿态/Tween/原缩放延迟及整卡合成未完成，效果启用仍2，见[ORIGINAL_ZONE_LAYOUT](ORIGINAL_ZONE_LAYOUT.md)。

2026-10-12 原ZoneChangeList：输入阻塞转发、独立完成/回调、dirty/Busy两阶段等待及多请求/取消恢复，24新增Unity与1165Python通过。实际区域布局/ZoneMgr/历史与整卡合成未完成，效果启用仍2，见[ORIGINAL_ZONE_CHANGE_LIST](ORIGINAL_ZONE_CHANGE_LIST.md)。

2026-10-12 原PowerTask执行：原分派/重复完成、pending与独立完成判断、范围回调、Update等待/取消/换牌及区域路由恢复，61新增Unity检查通过，包含原Yield创建取消和同帧阶段边界。完整原GameState/ZoneMgr/历史传输及整卡合成未完成，效果启用仍2，见[ORIGINAL_POWER_EXECUTION](ORIGINAL_POWER_EXECUTION.md)。

2026-10-12 原控制器历史收集：Source缓存/dirty、来源检查、时序门槛、TAG_CHANGE目标筛选/去重、Attach/Set/Detach与播放器明确输入接口接入；正式原历史传输/任务执行及攻击合成未完成，效果启用仍2，见[ORIGINAL_POWER_TARGETS](ORIGINAL_POWER_TARGETS.md)。

2026-10-12 原Spell目标集合：同一真实列表的逻辑/视觉访问器、重复/空项/首目标语义及BestTarget优先级恢复，27新增Unity与1165Python通过。完整任务目标导入和正式攻击合成未完成，效果启用仍2，见[ORIGINAL_SPELL_TARGETS](ORIGINAL_SPELL_TARGETS.md)。

2026-10-12 原SourceAuto：恢复法术/技能/附魔/武器分支及任务类型覆盖优先级，事件快照真实模型绑定接入，召唤播放器提供来源上下文；35新增Unity与1165Python通过。正式任务/目标集合与攻击合成未完成，完整效果仍2，见[ORIGINAL_SPELL_OBJECTS](ORIGINAL_SPELL_OBJECTS.md)。

2026-10-12 原Spell定位：205实际根组件全量导出，dirty定位/朝向、强制更新、原父级及释放操作恢复；29定位与57攻击检查、1165Python通过。正式源/目标/区域选择及攻击联动未完成，整效果启用仍2，见[ORIGINAL_SPELL_TRANSFORM](ORIGINAL_SPELL_TRANSFORM.md)。

2026-10-12 原攻击事件声音/等待：8源恢复5音频节点，四真实攻击Prefab声音与下一表现帧恢复通过53检查，1163Python通过。正式攻击联动、原位移/控制器及完整SoundManager未完成；完整效果启用数仍2，见[ORIGINAL_ATTACK_EVENT_AUDIO](ORIGINAL_ATTACK_EVENT_AUDIO.md)。

2026-10-12 原动画事件：8源程序48事件/43接收组件导出，事件空曲线保留至原回调时间；状态结束/完成及原粒子回调接入。四真实攻击Prefab的57时序检查、1161Python通过；声音/攻击等待及正式联动未完成，见[ORIGINAL_ANIMATION_EVENTS](ORIGINAL_ANIMATION_EVENTS.md)。

2026-10-12 原 SpellState：8 源 Prefab 的 23 状态/51 动画对象精确导出；显式状态播放、延迟/显隐、层/速度/CrossFade 与切换停止接入。78 源播放检查通过，80 次动画启动；原控制器/动画事件及正式战斗联动未完成，见[ORIGINAL_SPELL_STATE](ORIGINAL_SPELL_STATE.md)。

2026-10-12 原法术显隐源节点：本地SpellTable的类型0显隐差异及回调入口恢复；205源程序192Spell/23SpellState精确绑定导出，主层级校验与7外部owner缺口保留。1158Python、42表检查/原入场图及Windows构建通过。正式角色回调、状态动画/音频/阴影未接入，全卡目标未完成。见[ORIGINAL_SPELL_VISIBILITY](ORIGINAL_SPELL_VISIBILITY.md)。

2026-10-12 原CommonFX无Ramp分支：family19按原VS114/PS1202恢复红通道alpha与独立RGB运算，22程序55引用12原材质关键词/Shader依赖核对，动态混合属性接入。1156Python、GPU和Windows构建通过；66源Prefab采样11可见/11空白、部分黑alpha，全部整效果仍未完成。见[ORIGINAL_COMMONFX_NONE](ORIGINAL_COMMONFX_NONE.md)。

2026-10-12 原亮度遮罩粒子：family18恢复独立亮度alpha、顶点HDR增益与原Glow，六程序九引用五原材质依赖核对。1153Python与GPU检查通过；18实际源Prefab采样中一程序空白、部分黑alpha保留，完整全卡效果仍未完成，见[ORIGINAL_COLOR_ALPHA](ORIGINAL_COLOR_ALPHA.md)。

2026-10-12 原透明粒子：恢复family17原TintColor、独立Intensity/Alpha及Glow；修复编译混合属性占位误存为Zero/Zero，106源程序314引用刷新，65原材质依赖核对通过。1151Python、浮点/动态混合/仿射GPU与Windows构建通过；全卡、全Shader/脚本仍未完成，见[ORIGINAL_TRANSPARENT](ORIGINAL_TRANSPARENT.md)。

2026-10-12 原Hero/Additive/Additive：恢复基础TintColor与独立Glow路径，100原材质/12CAB逐项核对四阶段编译字节和渲染状态；覆盖147源程序404引用。1148Python、24新增GPU、104Glow、146离屏帧及Windows构建通过。全卡、场景合成/后处理仍未完成，整效果启用数仍2，见[ORIGINAL_ADDITIVE](ORIGINAL_ADDITIVE.md)。

2026-10-12 原入场整图：两源FromCorpse图以普通/传说真实Actor连续运行，发现并修复原延迟声音跨状态退出被取消的问题；四组完整状态链到NoneState并归还Actor，1144Python与GPU回归通过。仍缺原可见性联动与完整Shader，未扩大整效果启用范围，见[ORIGINAL_ENTRY_GRAPH](ORIGINAL_ENTRY_GRAPH.md)。

2026-10-12 原显式混音模板：原LegendaryDucking四组七效果和24快照值重建，实际bundle编译常量对照通过；1141Python、78Unity、347声音动作隔离运行通过。完整全局声音管线和全卡目标未完成，见[ORIGINAL_MIXER](ORIGINAL_MIXER.md)。

2026-10-12 原声音实例限制：原名称额度、独立释放等待及动作覆盖选项恢复，1137Python、66Unity通过；183程序345声音动作可完成，2显式混音模板与全卡目标仍未完成，见[ORIGINAL_SOUND_LIMITS](ORIGINAL_SOUND_LIMITS.md)。

2026-10-12 原声音模板：默认SoundConfig及空间参数恢复，1135Python、50Unity、183程序327声音动作隔离运行通过；20声音动作与完整全卡目标仍未完成，见[ORIGINAL_AUDIO_TEMPLATE](ORIGINAL_AUDIO_TEMPLATE.md)。

2026-10-11 原专属声音引用：183源程序347动作恢复SoundDef引用，导出130定义及132原音频，加入AudioPlayClipAction延迟播放路径。完整SoundManager与全卡目标未完成；最终验证记录见 [ORIGINAL_CLIP_AUDIO](ORIGINAL_CLIP_AUDIO.md)。

2026-10-11 原RandomWait：36源程序37动作恢复Unity浮点随机时长及严格结束边界，816Python、261Unity和全GPU回归通过。完整效果启用范围未扩大，实时输入与完整全卡目标未完成，见 [ORIGINAL_RANDOM_WAIT](ORIGINAL_RANDOM_WAIT.md)。

2026-10-11 原网格/材质节点：四源程序18替换动作、外部资源引用及CommonFX RGBR分支恢复；779 Python、73 Unity和236 Shader通道检查通过，完整GPU及Windows构建成功。完整MaterialService、动态对象参数和整套入场未完成，见 [ORIGINAL_GEOMETRY](ORIGINAL_GEOMETRY.md)。

2026-10-11 原ActorElite：两原入场子图的Actor父级搜索、ELITE标签/排除/优先级与On/Off事件恢复；759Python、95实际Unity及全GPU检查通过，Windows构建与15回合双客户端回归成功。非传奇SetMesh/SetMaterial仍未恢复，整套RLK不启用，见 [ORIGINAL_ACTOR_ELITE](ORIGINAL_ACTOR_ELITE.md)。

2026-10-11 原CameraShaker2：两个入场子图恢复源30位置随机采样、原曲线/双余弦插值、主相机绑定及退出复位；757Python、104实际Unity检查和完整GPU通过。其余材质/Actor分支仍未完成，整套RLK未启用，见 [ORIGINAL_TIMELINE_SHAKER](ORIGINAL_TIMELINE_SHAKER.md)。

2026-10-11 原 ColorBySpeed：7 个 Prefab 的 12 个节点恢复原单双渐变与速度范围；746 Python、156 原粒子引擎对照及全 GPU 回归通过。整卡启用数量未扩大，完整 Shader/脚本未完成，见 [ORIGINAL_COLOR_BY_SPEED](ORIGINAL_COLOR_BY_SPEED.md)。

2026-10-11 原Timeline粒子控制节点：恢复源ControlRandomized目标/根搜索、激活区间、原自定义粒子时钟/seed生命周期及单一采样所有权；730Python、16粒子引擎检查、1034Timeline检查通过。微小离屏图不证明完整效果，镜头/其余脚本和Shader仍未完成，见 [ORIGINAL_TIMELINE_CONTROL](ORIGINAL_TIMELINE_CONTROL.md)。

2026-10-11 原Timeline节点：两个嵌套入场图的Director/Animator/PPtr绑定、183源曲线及Renderer开关、TimelinePlay独立播放/停止接入；1008专项GPU检查、728Python、全GPU与Windows构建通过。3条未静音控制/镜头轨及Elite/材质分支仍未完成，整套RLK仍禁用，全卡目标继续，见 [ORIGINAL_TIMELINE](ORIGINAL_TIMELINE.md)。

2026-10-11 原自定义入场脚本节点：Initialize/Finalize父级、姿态reset、输出变量及借用恢复接入，3源缓存精确刷新，101实际Actor/层级检查通过。Timeline/Elite/非传奇材质分支仍未完成，未启用整套效果，全卡目标继续，见 [ORIGINAL_CUSTOM_SPAWN](ORIGINAL_CUSTOM_SPAWN.md)。

2026-10-11 原画纹理变量节点：恢复源Getter/Setter、FSM声明/null/独立性/倒放，4缓存源Owner/参数刷新，32实际Prefab/变量检查、712Python与完整GPU通过。RLK_060/061子效果仍缺Timeline/生成脚本/其余Shader，未启用；全卡未完成，见 [ORIGINAL_PORTRAIT_TEXTURE](ORIGINAL_PORTRAIT_TEXTURE.md)。

2026-10-11 原普通随从高光接入：正常战场使用源状态/Shader/R8，持久Actor与召唤共用，UI校准/原画更新借用保护；恢复两原flare纹理字节与Repeat采样，51高光/111召唤GPU与全回归通过。原完整状态脚本/全卡专属效果仍未完成，见 [ORIGINAL_BATTLE_HIGHLIGHT](ORIGINAL_BATTLE_HIGHLIGHT.md)。

2026-10-11 原ShaderTime节点：恢复原全局时间/gyro和生命周期，15运算+3实际高光渲染检查及全GPU回归通过；正式状态路由、全卡专属Shader/脚本仍未完成，见 [ORIGINAL_SHADER_TIME](ORIGINAL_SHADER_TIME.md)。

2026-10-11 原动态轮廓节点：恢复三个采样/合成Shader及五个源灰度Shader，原Actor多级轮廓生成/归一化/恢复；补齐原高光UV1/UNorm8并消除过曝。源组件证明普通随从没有FSM，其直接状态分支已恢复；正式交互与全卡脚本仍未完成，见 [ORIGINAL_HIGHLIGHT_SILHOUETTE](ORIGINAL_HIGHLIGHT_SILHOUETTE.md)。

2026-10-11 原高光节点：恢复源 Highlight 双 UV/Seed/ShaderTime/flare/非线性 RGBA Shader，导出原21状态槽/20材质和真实纹理；动态轮廓与原状态转场尚未恢复，未在正式战场启用，见 [ORIGINAL_ACTOR_HIGHLIGHT](ORIGINAL_ACTOR_HIGHLIGHT.md)。

2026-10-11 原普通随从边框节点：恢复 UnlitTexture 主渲染及阴影公式/原状态，静态与动态共用；新增 GPU 源公式检查。原 URP 阴影调度、动态主光、反射/高光及全卡脚本仍未完成，见 [ORIGINAL_ACTOR_FRAME_SHADER](ORIGINAL_ACTOR_FRAME_SHADER.md)。

2026-10-11 静态随从原Actor统一节点：静态缓存与原召唤共用模型/Shader/原环境探针/UV入口，19检查6图、704Python、28Shader/105召唤、完整GPU/Windows构建及14回合双端同步通过。完整动态光照和全Shader/脚本未完成，见 [ORIGINAL_STATIC_ACTOR](ORIGINAL_STATIC_ACTOR.md)。

2026-10-11 原随从原画Shader节点：原双纹理/双UV、RGBA去饱和/对比/颜色上限运算已接入实际Actor；从官方Gameplay恢复27项环境探针，修复原画黑屏并强化中心区域检查。704Python、28Shader GPU/105召唤GPU/14帧、完整GPU/Windows构建及13回合双端哈希同步通过。完整动态光照/静态随从渲染统一/其余Shader/全卡脚本未完成，见 [ORIGINAL_ACTOR_PORTRAIT_SHADER](ORIGINAL_ACTOR_PORTRAIT_SHADER.md)。

2026-10-11 原敌方召唤节点：核对共用Actor与原SUMMON_IN_OPPONENT=42专用槽，接入独立0.43秒落地/2秒清理/原音频；704Python、86召唤C#、100召唤GPU/14帧、完整GPU/Windows构建、最终10回合双端比赛通过，队列移除对已恢复源效果的0.5秒旧下限。完整原Shader/脚本与特殊召唤规格未完成，见 [ORIGINAL_OPPONENT_SUMMON](ORIGINAL_OPPONENT_SUMMON.md)。

2026-10-11 原召唤完成节点：按OnSpellFinished的BlockZones(false)/回调证据，把阻塞改为0.56秒，原2秒清理独立播放，暂停/移除/退出回收；704Python、Windows构建、完整GPU回归及13回合双客户端通过。完整原Shader/脚本和同Actor共享动画仍未完成，见 [ORIGINAL_SUMMON_COMPLETION](ORIGINAL_SUMMON_COMPLETION.md)。

2026-10-11 正式友方原召唤节点：真实Actor/效果表/入场Clip/粒子/落地声接到Summon事件，关闭重复旧表现，45新调用链检查、51GPU检查6帧、704Python和Windows构建通过。源清理尾段使当前队列2.56秒，原并行边界未恢复；敌方表/完整Actor Shader与全卡专属效果仍未完成，见 [ORIGINAL_BATTLE_SUMMON](ORIGINAL_BATTLE_SUMMON.md)。

2026-10-11 原音频时钟节点：源召唤AudioPlaythrough的0.5秒落地声、延迟/完成事件、回放去重和PCM播放桥接接入，29项新Unity检查、704Python及完整GPU/Windows构建通过；后台双端16回合/v51完成、哈希一致。正式Actor表路由、完整SoundManager/Shader/脚本仍未完成，启用数仍2，见 [ORIGINAL_AUDIO_CLOCK](ORIGINAL_AUDIO_CLOCK.md)。

2026-10-11 原召唤音频来源节点：原空AudioSource通过同物体SoundDef关联精确FX_MinionSummon_Drop GUID，实际导出1.198667秒原WAV及音高范围；154类型静态审核、701Python通过。原音频动作调度和SoundManager混音未实现，不扩大启用数。见 [ORIGINAL_SUMMON_AUDIO](ORIGINAL_SUMMON_AUDIO.md)。

2026-10-11 原Actor动画节点：两模板32/16 Clip独立导出；原AllyInPlaySpawn 0.48333秒/7通道实际接到Actor，原SUMMON_IN粒子与入场姿态共同验证，外部动画缺口1->0。26新C#、699Python、完整GPU回归与Windows构建通过。正式Actor/效果表对战调度、音频、全Shader仍未完成，见 [ORIGINAL_ACTOR_ANIMATION](ORIGINAL_ACTOR_ANIMATION.md)。

2026-10-11 原Actor效果表节点：原随从163/英雄62槽位独立导出，206引用/205文件；按原IL恢复本地复用、独立实例和播放前父级挂接子集，真实SUMMON_IN粒子离屏验证。完整Actor动画、表调度、原音频/Shader未完成，启用数仍2，见 [ORIGINAL_ACTOR_SPELL_TABLES](ORIGINAL_ACTOR_SPELL_TABLES.md)。

该节点最终698Python、4106既有C#/78172原运行时/34效果表检查、13新GPU检查4帧及标准回归通过；Windows双端最终12回合/v35结束，状态哈希一致，队列峰值1。原AllyInPlaySpawn仍缺外部Actor动画绑定，明确诊断1，不视为已完成召唤。

2026-10-11 原通用Actor节点：按catalog GUID恢复两套模板与对象引用，父级Actor初始化和显隐边界执行。78172原运行时/4106客户端/693Python、GPU12检查6帧及Windows构建通过。原完整Shader/脚本、正式父级挂接未完成，未增加启用数，见 [ORIGINAL_ACTOR_TEMPLATES](ORIGINAL_ACTOR_TEMPLATES.md)。

2026-10-11 原FSM Owner节点：17Prefab/25FSM精确归属补齐，78141原运行时/4106客户端/692Python检查通过。原完整Actor、初始化与父级挂接未完成，全卡Shader/脚本未完成，启用数仍2，见 [ORIGINAL_FSM_OWNER](ORIGINAL_FSM_OWNER.md)。

2026-10-11 原ScaleFrom节点：原Paw Spawn缩放动作执行、精确参数刷新、初始化缺口诊断接入。78132原运行时/4106现有客户端/688Python、GPU8检查4帧与Windows构建通过。全卡原专属动画、Shader/脚本仍未完成，启用数仍2，见 [ORIGINAL_SCALE_FROM](ORIGINAL_SCALE_FROM.md)。

2026-10-11 原Actor显隐节点：11Prefab/26动作/156参数精确刷新，普通Renderer及客户端模型根显隐接入；78118原运行时、4106现有客户端、688Python、新GPU4检查2帧、标准GPU及Windows构建通过。全卡原专属动画、完整Shader/脚本仍未完成，正式启用数仍2，见 [ORIGINAL_ACTOR_VISIBILITY](ORIGINAL_ACTOR_VISIBILITY.md)。

2026-10-11 原生命周期节点：静态审计144类型，恢复原Next选择和已就绪FSM状态映射，新增可重放精确Machine时间轴及已审核索引传递。78110原运行时/4106客户端/688Python/新GPU7检查5帧/原ActorGPU18/标准GPU41及Windows构建通过；正式启用数仍2，完整全卡原Shader/脚本/自动路由未完成，见 [ORIGINAL_SPELL_LIFECYCLE](ORIGINAL_SPELL_LIFECYCLE.md)。

2026-10-11 原Actor显示节点：ClientApp英雄/随从真实模型供给与动态显示接通，原脚本预采样、数值区域跟随与引用释放通过检查。688Python、4106客户端、78078原资源/运行时、18适配器GPU/5帧、41标准GPU/146帧及Windows构建通过。启用数仍2；全卡原动画/Shader/脚本未完成，详见 [NATIVE_BATTLE_ACTORS](NATIVE_BATTLE_ACTORS.md)。下方为历史节点记录。

## 原完成与退场时序（2026-10-10）

- [x] 原协程/立即 Finish 核对，地狱烈焰伤害 .35 秒与约 2 秒视觉退场独立。
- [x] 命名 Clip 速度、暂停/倒放/每帧变量与回退，672 Python、77182 原运行时、30 Prefab/84 帧、Windows 构建通过。
- [ ] 完整 Spell/Actor、Clip 混合/停止/自动停用、中央过曝与全卡原动画。

6 秒 Clip 不是 6 秒 FSM 等待；启用槽仍 2。见 [ORIGINAL_FINISH_TIMING](ORIGINAL_FINISH_TIMING.md)。

## 原溶解/遮罩（2026-10-10）

- [x] 恢复 AddMulDissolveSingleTex 红通道/顶点 alpha/Alpha RGB 运算及 Utility/Black 常量输出、Cull/深度。
- [x] 修复无纹理常量材质被当作缺失依赖；667 Python、180 Shader GPU、30 Prefab/84 帧、Windows 构建通过。
- [ ] 地狱烈焰完整约 6 秒 Clip 生命周期、中央过曝、全部 Shader/脚本与全卡原版动画。

21 效果槽引用不代表完成卡数，启用槽仍 2。见 [ORIGINAL_DISSOLVE_MASK](ORIGINAL_DISSOLVE_MASK.md)。

## 原 FX 坐标程序（2026-10-10）

- [x] 按原 DXBC/命名偏移恢复 FXWorld 与 FXScrolling 基础采样、投影坐标、时钟及 RGBA。
- [x] 152 Shader GPU、487 合成 GPU、30 Prefab/84 帧、666 Python、Windows 构建及后台双客户端整局通过。
- [ ] Scrolling Glow、溶解材质、全卡原版连续帧及完整 Shader/脚本。

地狱烈焰深色环消失，中央仍过曝，没有新增启用卡槽。见 [ORIGINAL_FX_COORDINATES](ORIGINAL_FX_COORDINATES.md)。

## 原 CommonFX 程序（2026-10-10）

- [x] 严格区分原 GPU 条目/参数块，2110 程序 + 133 参数记录，不把静态数量称完成数
- [x] 两个原关键词分支及共同 Glow、AlphaMul/UV/ramp/mask/named Blend
- [x] 51 缓存/151 唯一材质按源刷新；108 Shader、68 Glow、26 Prefab GPU 检查
- [ ] 其他 78 组 CommonFX 变体、目标纹理/脚本、139 次零轨道 Clip、全卡原版连续帧认证

9 槽匹配不是 9 卡完成，BT_035/175/801 继续禁用。见 [ORIGINAL_COMMONFX](ORIGINAL_COMMONFX.md)。

## 原 Mecanim 曲线（2026-10-10）

- [x] 三个原 Clip 的 36 轨道、真实时长、CRC28 材质绑定与直接 Controller 状态恢复
- [x] 原 FSM 驱动 scale、回退重播；641 Python、Windows 构建通过
- [ ] 原 _AlphaMul Shader、完整 Animator 语义、139 次零轨道旧缓存及全卡连续帧认证

BT_035/175 仍未启用；数据恢复不等于完整原特效。见 [ORIGINAL_MECANIM](ORIGINAL_MECANIM.md)。

## 原目标类型和可见性（2026-10-10）

- [x] 新旧PlayMaker事件参数恢复；254缓存/2877事件按原地址刷新，重复0更新/0失败
- [x] 原目标类型分支、首帧/回退派发与递归可见性退出恢复
- [x] 630 Python、649 GPU；新增36行为断言，静态脚本审计129类型
- [ ] 原Animator、实体纹理/姿态、完整Spell生命周期、完整原Shader/脚本及全卡外观认证

BT_035/175恢复发射但仍缺主体动画，三项新样本黑遮罩未合格，未启用。22/47槽是动作覆盖，不是完成卡数，见 [ORIGINAL_TARGET_BINDING](ORIGINAL_TARGET_BINDING.md)。

## 原 Distortion 程序（2026-10-10）

- [x] 第一 Distortion 家族按原 DXBC 恢复 UV、RGBA、混合和独立 Glow
- [x] 两个家族编译程序只读审计；累计10 Shader/57程序/45反汇编
- [x] 646 GPU、623 Python、4101既有C#检查；Windows构建成功
- [ ] 第二层 Distortion、原场景时间、目标类型绑定与完整后处理
- [ ] 全卡专属动画、完整原 Shader/脚本与原版逐帧认证

17效果槽是材质覆盖范围，没有新增完成卡数。新增真实Prefab的两项空白和一项有限输出均未启用，见 [ORIGINAL_DISTORTION](ORIGINAL_DISTORTION.md)。

## 原 Glow/环境光与全缓存刷新（2026-10-10）

- [x] 两个原粒子 Glow 程序独立 HDR 输出与动态材质参数，真实 mip bias 采样
- [x] 三个基础家族原环境光数学，GetBoard/GetAmbient 进入、每帧更新与重放行为
- [x] 原 Board 构造函数白色上下文；静态脚本审计124类型
- [x] 352槽/254 Prefab 全刷新，1879粒子/96捕获组件；313依赖错误保留
- [x] 623 Python、619 GPU、76934原数据/行为断言，Windows构建成功
- [ ] 原全局Glow/Bloom、特殊事件与SH/主光来源、所有家族完整Shader/脚本、全卡逐帧验收

独立Glow输出并未合成为最终画面，没有新增完成卡数。细节见 [ORIGINAL_GLOW_LIGHTING](ORIGINAL_GLOW_LIGHTING.md)。

## 原捕获绘制指令（2026-10-10）

- [x] 按原 IL 恢复捕获分组、sortingOrder/renderQueue 顺序和基础 Pass 选择
- [x] 导出原 Queue 标签/偏移、自定义队列、LightMode 与 Renderer 排序值
- [x] 三个主 Prefab 缓存刷新，25 条新增 GPU 检查；623 Python、563 GPU 通过
- [ ] 全缓存刷新、非基础多 Pass、完整渲染状态、全部原 Shader/脚本

细节与验证边界见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。

## 原粒子基础程序与混合状态（2026-10-10）

- [x] 三个原 Shader 家族基础 RGBA/UV 数学与原混合关系恢复，48 条独立 GPU 数值检查通过
- [x] 旧材质属性过滤贯穿初始化、FSM、Clip、重放；保留编译声明的隐藏 uniform
- [x] 621 Python、4101 既有 C#、76928 原数据/行为、538 GPU 检查，Windows 构建成功
- [x] 原脚本静态审计119类型，恢复嵌套类型读取
- [ ] 环境光、Glow pass 和完整 Shader/脚本（基础捕获排序见后续节点）

来源、数据覆盖与未完成范围见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。

## 内部 RenderTexture 与编译 Shader 增量（2026-10-10）

- [x] 恢复两套冰霜新星隐藏模型捕获、原目标材质绑定、有符号裁剪与重放隔离
- [x] 8 个原 Shader 的来源及程序 SHA256 审计，35 个 DXBC 程序反汇编
- [x] 620 Python、4101 既有 C#、76925 原数据/行为断言、490 GPU 检查通过，Windows 构建成功
- [x] 独立端口后台双客户端完成新一局，16 回合 / v51，双方最终公共哈希一致
- [ ] 冰霜新星外观达到启用标准，原 AlphaMesh/Stencil/Bloom 和完整 Shader/脚本恢复
- [ ] 全部现有牌的原版专属动画与条件分支

详细来源、截图性质及未完成范围见 [RENDER_TEXTURE_RESTORATION](RENDER_TEXTURE_RESTORATION.md)。

## 原 Prefab 特效与脚本恢复（2026-10-10）

- [x] 352 个效果槽、254 个不同 Prefab 的原层级/粒子/动画/FSM/材质参数导出与来源审计
- [x] 113 个相关原脚本类型的静态 IL 与程序集哈希审计
- [x] 有界原粒子/Clip/FSM 客户端播放器、兼容 Shader、退场回收和失败回退
- [x] 奥术爆炸、地狱烈焰原特效数据接入；其他效果槽保持禁用
- [ ] 冰霜新星原渲染链：GPU 可见性通过，但实际仅灰色阴影，画面核验拒绝启用
- [x] 淡出黑遮罩、内部 Offscreen 子树、旧参数格式和同名 Clip 绑定修复
- [x] 618 Python 测试、4101 原有 C# 断言、76916 原数据断言、490 GPU 检查通过
- [x] Windows 构建成功；后台双客户端完成三局独立端口自动完整比赛，最后一局使用最终启用配置，双方最终哈希一致
- [ ] 全卡原版专属动画、完整原 Shader/脚本、条件分支与音频 FSM
- [ ] 整屏人工视觉验收与长期性能验收

本轮复现、验证边界与未完成清单见 [NATIVE_EFFECTS](NATIVE_EFFECTS.md)。

## 手牌、嘲讽与交战反馈（2026-10-09）

- [x] master/e8611c5 恢复，基线 588 实跑通过
- [x] 手牌中心跨度收拢并整体避让校对后的计数框，1～10 张及所有 Hover 槽验证
- [x] 原 GenFX_Taunt_Shield 模型/材质取代自画圆环，添加校对槽
- [x] 权威连击条件黄光、攻击增益绿色，保留用户填充覆盖
- [x] 指向性战吼/已激活连击放置后选择目标，箭头不被卡面遮挡
- [x] 直接交战扣血同时、纯交战双方死亡并行；实际扳机仍分阶段
- [x] 背刺/刺骨/冷血关联音效及原材质纹理，轨迹重实现
- [x] 增量效果注册合并，早期导出器重跑不删除其他效果
- [x] 594 Python 测试、2170 构建断言、63 GPU 离屏检查
- [x] 本轮双原生客户端自动完整局两局；14/16 回合结束，双方公共哈希一致
- [ ] 本轮整屏人工操作、动画连续帧及官方音画同步核验
- [ ] 原嘲讽盾 Gleam/破盾、全部卡牌专属动画恢复

详细实现与边界见 [COMBAT_FEEDBACK](COMBAT_FEEDBACK.md)。

## 文字、拾牌与原版效果反馈（2026-10-09）

- [x] 战斗悬停/拾牌与菜单音轨分离
- [x] 烈焰冲击按原 Prefab 的起手/施放/命中音轨播放
- [x] 满生命/耐久白、受损红、增益后满值绿
- [x] 背景取景缩放、填充/描边/阴影/字号/粗斜体与 RGBA 调色板
- [x] 描述保留原始粗体，兼容用户旧布局
- [x] 原扳机闪电、实际监听回调提亮、沉默移除
- [x] 点击持牌/拖动持牌、战场释放、指向战吼先落位选目标
- [x] 四个稳定法术/技能原声音及粒子纹理按需接入
- [x] 583 项规则、336 条客户端断言、23 项 GPU 组件检查通过
- [x] 两局后台双 Windows Player 完整自动对战；最终构建 15 回合/v54，双方客户端结束哈希一致
- [ ] 留牌点击准确原轨和新字体面板整屏人工核对
- [ ] 完整原 ParticleSystem/AnimationClip/Shader 重放
- [ ] 所有已实现卡牌专属 VFX

- [x] 核实官方安装、AssetStudio、Unity 编辑器、Python、Git、VS/MSVC
- [x] 初始化正式 Git 与资产排除规则
- [x] 独立 clone Resource Lab 并阅读核心解析代码
- [x] 验证 AssetStudio 和 UnityPy 对真实样本的兼容性
- [x] 完成对象索引、GUID 依赖查询和按需导出
- [x] 本地卡牌数据库与 489 张目标池 ID（431 历史牌 + 58 死亡骑士上市牌）
- [x] Unity Windows 可运行构建
- [x] 菜单、30 张卡组构筑/保存、独立 profile 基础功能
- [x] 权威服务器、匹配、换牌与正常动作完成比赛
- [ ] 十一职业与目标卡池完整效果
- [x] 十一职业预设、死亡骑士尸体/符文/技能扩展与基础执行测试
- [ ] 完整历史发现/随机生成池与逐牌行为矩阵
- [x] 489 张目标牌基础执行与 20 个抉择分支，恶魔猎手历史脚本专项修正
- [ ] 原版资源、音频和表现动画
- [ ] 规则与网络测试
- [x] 双 Unity 客户端通过正常协议自动操作完成真实比赛
- [x] 死亡骑士/恶魔猎手实机完整比赛，18 回合、版本 97、相同公共哈希
- [x] 卡牌方向、英雄/法术/武器原画错配、宝石与卡背修复
- [x] 衍生原画后台按需提取
- [x] 实机窗口点击选牌、替换、确认并进入首回合
- [ ] 双客户端人工交互完整验收与异常恢复
- [ ] 官方视觉参考和自动差异测试
- [x] 固定换牌/棋盘/gallery 截图与像素/dHash 比较工具
- [x] 原版木盒菜单相机、网格按钮、Multiply 阴影、悬停与 UI 音效
- [x] CardDef GUID 事件音轨按需导出与客户端播放接口
- [x] 带原版事件音频的双客户端完整比赛与双方结算后返回菜单
- [x] 本机服务器/双客户端启动入口、各职业独立套牌保存
- [x] 正常/异常断线清理与 Windows 连接重置回归
- [x] 双客户端窗口实际拖牌、法术、技能、随从/英雄攻击与断线返回
- [x] 资产跨进程写锁、原子增量合并与失败历史保留
- [x] 随机生成牌的显示元数据覆盖，组牌池继续受限
- [x] 最终 Windows 构建的法师/术士完整比赛，18 回合、版本 64、同步无异常
- [ ] 489 张完整语音/候选选择、叠放与原版时序核验

## Presentation / Visual Fidelity Sprint（2026-10-08）

- [x] 真实 Git baseline 恢复与 577 项测试重新通过
- [x] 1080p 统一设计坐标、相机与原版 TableTop 消除大黑边
- [x] 默认关闭 HUD/内部版本/事件日志，F3 开关
- [x] 英雄/技能/武器/牌库布局，原版法力水晶和数字
- [ ] 原版战斗构图/光照逐项 Oracle 核验
- [ ] Hero Power Used/Disabled 独立原版状态与完整技能声音
- [x] 1～10 张动态扇形、Hover 避让、拖动回正/取消回位
- [x] 敌方扇形卡背与双方抽牌运动
- [x] 换牌战斗状态、原版横幅/按钮/红 X、替换与转场
- [ ] 换牌 Hover 平滑、完整英雄入场与硬币仪式
- [x] CardPlay 离手/揭示/随从或法术阶段
- [x] 1～7 随从平滑自动排布、通用状态覆盖
- [x] 攻击蓄力/冲撞/Hit Stop/返回，统一命中和延迟死亡
- [x] 原版火球纹理的法术/法师技能弹道范例
- [ ] 原版 Particle/Shader/死亡破碎/棋盘机关
- [x] 原版字库、核心战斗声音与 BGM
- [x] 有界表现队列、显示/权威快照分离，服务器无动画等待
- [ ] 复杂连锁逐事件视觉状态重放
- [x] 五个 Gold Standard 实际 Windows 场景/过程帧
- [ ] 五场达到官方视觉标准并通过 Oracle 比对
- [x] reference/current/diff 目录与实机截图回归工具
- [x] 官方主菜单 1080p 原生 Reference
- [ ] 官方战斗 17 场景 Reference/录像时序
- [x] 245 条 C# 断言、32 个固定场景截图检查
- [x] 改造后双原生客户端自动完整比赛、哈希一致
- [ ] 本轮双客户端全程人工完整比赛（桌面工具返回错窗口）
- [x] 双开帧耗时/缓存/队列/托管堆采样
- [ ] 长时间 GC/显存/资源泄漏压力验收
- [x] 来源清单和架构/管线/表现/差异文档同步

## 人工 UI 校对（2026-10-09）

- [x] 复用现有客户端，独立启动入口与 F6 校对页
- [x] 随从/法术/武器独立模板，原画 UV、文字、数值与网格部件校对
- [x] 场上随从、英雄、技能、卡背、法力/攻击/生命宝石校对
- [x] 相机/棋盘及双方英雄、技能、武器、随从、牌库、法力区域校对
- [x] 手牌数量/随从数量预览，换牌区域/牌位/按钮校对
- [x] 移动/旋转/缩放手柄、数值、翻转、撤销/重做与预览平移
- [x] 本地原子保存/备份，JSON 导入导出，运行时重新加载
- [x] 参考图半透明叠加，仅限编辑器
- [x] 校对后的视觉输入逆矩阵命中与模板隔离验证
- [x] 30 个原生编辑器截图场景，577 项规则与 245+21 条构建断言通过
- [x] 编辑后普通对战截图回归、双客户端自动完整比赛与状态哈希验证
- [ ] 主菜单木盒 3D 按钮校对
- [ ] 动画时序可视化编辑

## 战斗交互与专用图标（2026-10-09）

- [x] 保留用户已保存的 29 个 UI 校对元素，文件哈希不变
- [x] 起手保留绿色轮廓、悬停白色叠加、替换取消光晕
- [x] 棋盘投影与英雄/随从/手牌默认纵向布局校准
- [x] 权威合法动作控制绿色结束回合提示及技能可用绿光
- [x] 技能蓝色详情卡与随从左右侧详情，详情副本不覆盖实体命中
- [x] 左侧有界图标历史、滚轮回看与悬停，过滤隐藏信息
- [x] 按需恢复原版技能详情框、历史框、护甲银盾和耐久破盾
- [x] 六项来源清单与现有原画的 537 条历史裁切参数索引
- [x] 技能详情、护甲、耐久三页加入现有校对器
- [x] 577 项回归及 245+21+33 条构建断言通过
- [x] 十项 GPU 离屏模型图检查，修复银盾方向及历史图标朝向
- [x] 用户提供的七张正版参考归档，新增九个交互固定场景
- [x] 最终构建后台双原生客户端自动完整局，13 回合/v44/公共哈希一致
- [ ] 最终整屏交互截图与人工悬停/点击核验（后台采样黑帧，未使用桌面工具）
- [ ] 与官方 Shader 的历史原画裁切/光晕强度精确核验

## 棋盘与战斗 UI 修正（2026-10-09）

- [x] 核实 Board_STR 原网格/贴图，去掉额外相机倾斜
- [x] 原齿轮/菜单框/按钮接入，菜单阻断底层操作
- [x] 原结束回合模型和原旋转采样翻转；服务器无等待
- [x] 仅关键词加粗，正文保持正常字重/大小写
- [x] 对方法力仅计数，己方保留水晶，原图集蓝色气泡
- [x] 修正技能/扳机额外压暗
- [x] 原提示框显示己方牌库/手牌、敌方公开数量
- [x] 原召唤贴图和权威召唤疲劳睡觉 Z
- [x] 编辑器分类/部件独立滚动；保留用户校对
- [x] 585 项规则、344 条构建、37 项离屏检查
- [x] 两局双原生客户端自动完整局；最终构建 14 回合/v51，双方公开哈希一致
- [ ] 本轮整屏交互与粒子人工验收
- [ ] 原完整菜单脚本、Shader、Animator/ParticleSystem 执行

## 装备状态与动画结算节奏（2026-10-09）

- [x] 恢复 master/f7df7e5，基线 585 通过；保留最新用户校对
- [x] 菜单正文 alpha 缺块修复，三段绘制边框
- [x] 圆形装备武器原模型/原画，独立于手牌模板；新增校对页
- [x] 技能已用显示原背面、隐藏费用，悬停正常详情
- [x] 原五档厚度牌库替代平面卡背堆叠
- [x] 法术离手/效果/伤害/逐个扳机与增甲/死亡/历史分阶段
- [x] 公开事件中间值，512 条覆盖刀扇 49 次铸甲师触发
- [x] 原刀扇匕首/拖尾/施放声/命中声；动画/粒子参数研究缓存
- [x] 588 规则测试、372 构建断言、51 GPU 检查
- [x] 双原生自动完整局两局，最终构建 13 回合/v53，公开哈希一致
- [ ] 整屏人工比赛、技能详情/菜单/牌库槽口核验
- [ ] 原 Animator/Shader/粒子逐帧恢复、全部复杂中间状态重放

证据和恢复边界见 [BATTLE_PACING](BATTLE_PACING.md)。

## 手牌、目标交互和终局（2026-10-10）

- [x] 修复手牌/箭头局部旋转坐标，保留用户校对
- [x] 英雄攻击选择抬升/回落；名字/齿轮按窗口左右边缘定位
- [x] 指向随从/武器拾取、放置、目标选择及 TCP 输入重放
- [x] 英雄入场、发起手牌延后、败方破碎、胜负转场与返回菜单
- [x] 原皇冠/结算横幅/裂纹/破碎垫音；单材质破碎 Shader
- [x] 6 张牌、11 层原颜色/尺寸/旋转模块；旧配置显式禁用空模块
- [x] 608 Python、4101 C#、470 GPU 检查（49 项新增）
- [x] 盗贼/战士及法师/战士累计 5 局双后台原生完整比赛
- [x] 审计 447 个音频导出，修复 1 个全零 WAV 缓存并保留坏文件；导出校验防止再次复用
- [ ] 整屏人工操作及正式渲染性能验收
- [ ] 全部现有卡牌原版专属动画、完整 Shader/脚本

验收范围见 [INTERACTION_LIFECYCLE](INTERACTION_LIFECYCLE.md)。

## 窗口、机制标志与主施放关联（2026-10-09）

- [x] 延续 master/0b9f97d，用户校对哈希保持不变
- [x] 相机/GUI/命中统一等比矩阵，背景有限宽度，编辑器独立 Fit
- [x] 原菜单材质亮度兼容与大写 Glow 黑遮罩修复
- [x] 原亡语轮廓网格、剧毒绿色药瓶，独立校对，沉默移除
- [x] 原闪电触发扩大/金光晕；英雄直接伤害/反击/耐久同命中
- [x] 普通 Play 补公开入场视觉事件与 index，战吼前显示随从
- [x] 现有 489 张牌效果分支审计，有界接入 233 组可用主施放原关联素材
- [x] 离手/施放/命中/历史有时间间隔，GPU 去底不改原图
- [x] 599 Python、3527 C#、69+9+343 GPU 检查通过
- [x] 两局后台双原生完整比赛，15 回合/v55、16 回合/v62，最终哈希一致
- [x] 1920×1080、1220×1110、2335×515 离屏场景/组件图归档
- [ ] 全部现有牌的原专属动画、条件分支和原脚本/Shader 执行
- [ ] 正常 GUI 整屏人工验收、鼠标交互与连续帧核验
- [ ] 正式 1080p 渲染 60 FPS 与长期泄漏验收

详情、素材来源与测试边界见 [WINDOW_FEEDBACK](WINDOW_FEEDBACK.md)。

2026-10-10 原纹理脚本节点：97 个 Prefab / 693 个原类型 32/33 参数恢复，Renderer 固定 Texture2D/null 绑定及回放验证通过。656 Python、4101 原有 C#、76985 原资源检查、28 GPU/72 帧；新 Windows 后台双客户端 11 回合/v35 完成、哈希一致。动态 Actor 原画、完整 Shader/脚本、全卡原动画仍未完成，见 [ORIGINAL_TEXTURE_BINDING](ORIGINAL_TEXTURE_BINDING.md)。

2026-10-10 原缩放脚本节点：55 个 Prefab / 119 个动作的 None/旧编码恢复，109 个本地目标已解析、10 个外部目标未绑定。662 Python、4101 原有 C#、76996 原资源检查、29 GPU/78 帧；两局后台真实双客户端 15 回合/v45、14 回合/v44 完成且哈希分别一致。逐卡整场原效果验收仍未完成，见 [ORIGINAL_SCALE_ACTION](ORIGINAL_SCALE_ACTION.md)。

2026-10-10 原颜色脚本节点：25 个 Prefab / 59 个 AnimateColor 的 885 参数和 236 曲线恢复，原 RGBA/None/时间/当前计算分支进入材质并可回退。664 Python、4101 原有 C#、77004 原资源检查、30 GPU/84 帧；后台双客户端 14 回合/v48 完成且哈希一致。完整 Shader、其他计算/动态时钟及逐卡原动画验收未完成，见 [ORIGINAL_COLOR_ACTION](ORIGINAL_COLOR_ACTION.md)。

2026-10-10 原缓动脚本节点：34 个 Prefab / 122 个 EaseFloat/EaseColor 的 1220 参数恢复，当前七种原 easing、延迟、反向及完成时刻替换线性兼容路径。冰霜新星和暗影真实材质联动已验证；后台双客户端 14 回合/v47 完成且哈希一致。仍只有两个原特效正式启用，完整原 Shader/脚本、全卡逐场景原动画验收未完成，见 [ORIGINAL_EASING](ORIGINAL_EASING.md)。

2026-10-10 原 Multiply Shader 节点：原 RGBA 乘数、Zero/SrcColor 混合及独立 Glow 已按 DXBC 恢复，44 个 Prefab / 61 个材质引用进入路径。666 Python、4101 既有 C#、77150 原资源、128 Shader GPU、76 Glow GPU、30 Prefab GPU/84 帧通过；Windows 构建成功。最终 board 彩色透射与完整全卡/Shader/脚本未完成，见 [ORIGINAL_MULTIPLY_SHADER](ORIGINAL_MULTIPLY_SHADER.md)。

2026-10-10 彩色透射节点：正式棋盘接入原 blend 的浮点 C/K 重放及双 pass；487 GPU 覆盖 48 个混合组合和 HDR/淡出/状态恢复，30 Prefab GPU/84 帧、Windows 构建通过。深色环/过亮和完整原 SRP/后处理/全卡动画仍未完成，见 [ORIGINAL_AFFINE_COMPOSITE](ORIGINAL_AFFINE_COMPOSITE.md)。

2026-10-10 扩展 FX 节点：原 Manual/Rotation/TwoLayer 基础程序和 Scrolling/TwoLayer Glow 按 DXBC 恢复；逐 Pass 原常量偏移已追踪。672 Python、4101 既有 C#、77191 原资源/运行时、224 Shader GPU、92 Glow GPU、487 affine、32 Prefab GPU/96 帧通过；Windows 构建成功，新双客户端 14 回合/v43 完成且哈希一致。目标场景、全局 Bloom、完整原脚本和全卡原动画未完成，没有新增启用槽，见 [ORIGINAL_EXTENDED_FX](ORIGINAL_EXTENDED_FX.md)。

2026-10-10 原动画目标/停止节点：修复自身目标被空显式指针覆盖，按源核对刷新 111 Prefab/220 动作/1214 参数，重复零更新；原 StopAnimation 名称/空名语义经本机 Unity 对照。674 Python、4101 既有 C#、77200 原资源/运行时、34 Prefab GPU/108 帧、Windows 构建通过；双客户端 12 回合/v38 完成，哈希一致。月火术完整目标/尺度、原混合及全卡原动画未完成，见 [ORIGINAL_ANIMATION_OWNER](ORIGINAL_ANIMATION_OWNER.md)。

2026-10-10 原混合对照节点：真实 Unity Play Mode 74 帧/299 检查证实单 Clip 权重归一化、两 Clip 混合、0/4 模式与重复同名时间保持；现有播放器仍删除旧 Clip，正式混合未恢复。未新增启用槽，见 [ORIGINAL_LEGACY_BLEND_ORACLE](ORIGINAL_LEGACY_BLEND_ORACLE.md)。

2026-10-10 默认层正式混合节点：保留旧/新时钟并接入 Unity 原生变换混合、材质通道归一化、同名时间保持和回退。674 Python、4101 既有 C#、77208 原资源/运行时、34 Prefab GPU/108 帧、Windows 构建通过；双客户端 11 回合/v36 完成且哈希一致。非零层、完整生命周期、后处理和全卡逐场景验收未完成，见 [ORIGINAL_LEGACY_MIXER](ORIGINAL_LEGACY_MIXER.md)。

2026-10-10 原退出停止节点：PlayAnimation.StopOnExit 按源节点/名称停止旧版时钟，三组实际猎人箭矢退场验证通过。674 Python、4101 既有 C#、77215 原资源/运行时、37 Prefab GPU/126 帧、Windows 构建通过；双客户端 13 回合/v39 完成且哈希一致。完整 Finish/LoopEvent/生命周期和全卡原动画未完成，无新增启用槽，见 [ORIGINAL_ANIMATION_EXIT](ORIGINAL_ANIMATION_EXIT.md)。

2026-10-11 动画完成边界对照：新增隐藏 Unity Play Mode 真实逐帧 Oracle，验证 Once 停用、Loop/ClampForever 无界计时、零速暂停和恢复速度。正式播放器未修改；源 WrapMode 覆盖和动态完成仍未恢复，无新增启用槽，见 [ORIGINAL_LEGACY_COMPLETION_ORACLE](ORIGINAL_LEGACY_COMPLETION_ORACLE.md)。

2026-10-11 原模式导出节点：新导出保留 Clip 完整 WrapMode、Animation 组件覆盖和默认 Clip 指针及来源；新增精确源只读调查工具。正式播放器尚未读取，缓存未刷新，无新增启用槽，见 [ORIGINAL_ANIMATION_WRAP](ORIGINAL_ANIMATION_WRAP.md)。

该节点实测 683 Python 通过；346 引用/205 唯一 Clip 均按精确 ID 定位，379 Animation 组件引用均为默认模式。调查因 59 组缺 Unity 内建依赖而以非零退出，错误保留；不能声明全部 Prefab 依赖已完整恢复。

2026-10-11 原模式采样节点：真实 Unity 17 组合/39 检查确认状态>Clip>组件>Once 继承，正式旧版混合接入 Loop/PingPong/Clamp 采样；77241 原资源/运行时、37 Prefab GPU/126 帧和 Windows 构建通过。新模式未回填原缓存，自动停用/动态事件和全卡原效果仍未完成，见 [ORIGINAL_LEGACY_WRAP_RUNTIME](ORIGINAL_LEGACY_WRAP_RUNTIME.md)。

新构建后台双客户端15回合/v48结束且状态哈希一致；4101既有C#检查通过，无普通GUI/FPS人工验收。

2026-10-11 动态完成节点：PlayAnimation跟随实际ClipClock，暂停/变速、停止/替换、自定义事件、原循环条件和聚合FINISHED已接入；停止后旧循环事件不会覆盖完成通知。683 Python、4101既有C#、77253原资源/运行时、37 Prefab GPU/126帧和Windows构建通过；完整事件分发、负速边界、旧缓存模式回填及全卡原效果仍未完成。见 [ORIGINAL_DYNAMIC_ANIMATION_COMPLETION](ORIGINAL_DYNAMIC_ANIMATION_COMPLETION.md)。

最终构建双客户端16回合/v57结束、状态哈希一致；表现队列峰值1。后台不作为正式GUI/FPS人工验收。

2026-10-11 原模式回填节点：254程序精确验证、189缓存更新、0映射失败，346 Clip引用/379 Animation组件引用接入模式，Once播放末端退役补齐；59内建依赖警告保留。688 Python、4101既有C#、78002原资源/运行时、39 Prefab GPU/146帧、Windows构建通过。原准星两场景为黑，保持禁用；两个既有启用效果新指纹已重新审核。全卡原脚本/Shader/效果未完成，见 [ORIGINAL_ANIMATION_WRAP_CACHE](ORIGINAL_ANIMATION_WRAP_CACHE.md)。

该节点新构建双客户端10回合/v37结束，状态哈希一致、队列峰值1，未作普通GUI/FPS人工验收。

2026-10-11 原iTween源节点：40 Prefab/187动作/2573参数恢复，重复0更新/0失败；688Python通过。原准星透明度为0需FadeTo，ScaleTo原弹性30与Fade线性21及父子回调/退出类型停止已静态追踪，iTween库、基类、回调和枚举纳入142类型静态审计，未执行官方程序集。Fade/Scale运行时及准星画面尚未恢复，见 [ORIGINAL_ITWEEN_SOURCE](ORIGINAL_ITWEEN_SOURCE.md)。

2026-10-11 原iTween执行节点：原线性淡入/弹性缩放的时间驱动分支接入；Wait独立finishEvent已恢复。两个原准星各10/10采样从空白变为可见，仍需目标绑定，不启用。688Python、4101既有C#、78020原资源/运行时、41GPU/146帧、Windows构建通过；全卡原脚本/Shader未完成，见 [ORIGINAL_ITWEEN_RUNTIME](ORIGINAL_ITWEEN_RUNTIME.md)。

最终构建双客户端16回合/v58结束，状态哈希一致、队列峰值1；测试进程已清理，用户服务器保留。后台回归不作为正式GUI/FPS或人工比赛验收。

2026-10-11 原缓动分支节点：现有187动作实际使用的10种原easing全部数值实现，speed/transformScale/default-time及父子淡入独立停止接入。40Prefab/133生命周期/798帧调查0异常，132场景Tween未绑定0，1个外部对象缺口；46空白场景明确保留，正式启用数仍2。78060原运行时、4101既有C#、41GPU/146帧、688Python与Windows构建通过，详见 [ORIGINAL_ITWEEN_BRANCHES](ORIGINAL_ITWEEN_BRANCHES.md)。全卡原效果仍未完成。

2026-10-11 原Actor节点：25Prefab/31动作93参数精确刷新；对象变量、角色选择、SetScale/ScaleTo动态owner与借用模型所有权恢复接入。原MODEL_MINION/CS2_179目标GPU8检查5帧验证，18新对象检查；78078原运行时/4101既有C#/688Python/标准GPU41检查146帧/Windows构建通过。正式棋盘Actor供给仍未接入，原效果启用数仍2；46空白/3场景缺Actor明确保留，完整全卡脚本/Shader未完成，见 [ORIGINAL_ACTOR_BINDINGS](ORIGINAL_ACTOR_BINDINGS.md)。最终双客户端12回合/v41完成，状态哈希一致，队列峰值1。

# 原手牌姿态恢复节点

原神圣图实际组件OffScreenRenderFX的隔离/正交相机/临时纹理链接入，10新增检查通过；原源图从黑屏变为放射粒子，颜色与完整Shader仍未恢复。见 [ORIGINAL_OFFSCREEN_FX.md](ORIGINAL_OFFSCREEN_FX.md)。旧RenderToTexture剩余属性仍未完成。

原kind36 FsmProperty与自有SDK组件赋值接入，99程序554动作原数据导出、10新增SDK/源图检查通过。支持的每帧更新改为原动作顺序，详见 [ORIGINAL_PROPERTY.md](ORIGINAL_PROPERTY.md)。原完整属性类型/自定义组件/Shader与正式全卡播放未完成。

原SetColorRGBA的None通道保留/动态透明度已恢复，22程序56动作原参数刷新，88新增检查通过。详见 [ORIGINAL_RGBA.md](ORIGINAL_RGBA.md)。完整Shader/逐帧调度及正式全卡播放未完成。

真实启动子 Prefab 的 SpawnStart/状态完成/释放链见 [ORIGINAL_SPAWN_START.md](ORIGINAL_SPAWN_START.md)。232 原配置、真实双根共用 Prefab 的16新检查通过；图像仍仅小红点，原 Shader/目标 Card 与正式播放未完成。

原启动门禁与 scaled 协程、Birth/Action/Death 顺序见 [ORIGINAL_SUPER_START.md](ORIGINAL_SUPER_START.md)。33 新检查通过，复仇之怒/虚空恐魔原延时已读取；真实 SpawnStart 与正式播放器绑定未完成。

原 SuperSpell.DoActionNow 分派与子效果完成等待见 [ORIGINAL_SUPER_ACTION.md](ORIGINAL_SUPER_ACTION.md)：51 新增 Unity 检查、真实区域子效果回调链通过。完整延时/启动/导弹/历史与正式播放器集成未完成。

原区域子效果的选择与独立克隆执行见 [ORIGINAL_SUPER_AREA.md](ORIGINAL_SUPER_AREA.md)。232 个配置与三个真实区域 Prefab 已恢复来源；正式全卡效果、完整 SuperSpell 与 Shader 未完成。

原四场景相机配置与独立捕获视图恢复，20 新增 Unity 检查及新来源核对通过，正式客户端尚未切换，详见 [ORIGINAL_SOURCE_CAMERA.md](ORIGINAL_SOURCE_CAMERA.md)。

2026-10-12 原普通效果定位节点：254 程序/715 组件/461 子组件来源已导出，11 新增 Unity 与 41 GPU 检查通过；完整子实例路由、原相机及全卡表现仍未完成，详见 [ORIGINAL_EFFECT_PLACEMENT.md](ORIGINAL_EFFECT_PLACEMENT.md)。

2026-10-12 后续：[原 iTween 完整曲线分发](ORIGINAL_ITWEEN_COMPLETE_CURVES.md) 恢复 35 枚举与完成姿态，460 新增检查通过；正式全卡效果启用仍 2，完整脚本/Shader/合成未完成。

见 [ORIGINAL_HAND_GEOMETRY.md](ORIGINAL_HAND_GEOMETRY.md)：恢复 ZoneHand 世界位置/旋转/缩放/Hover 计算，未完成实际场景投影和完整布局 Tween。全卡原专属动画与完整 Shader/脚本仍未完成。

原神圣 RenderPlane Shader 读取真实 DXBC 后恢复，27 缓存程序/8 材质来源核对、268 GPU 通道检查与1172常规测试通过；完整整卡效果仍未完成。见 [ORIGINAL_HOLY_SHADER.md](ORIGINAL_HOLY_SHADER.md)。

原英雄遮罩与顶点光芒Shader恢复，来源覆盖28/24缓存程序、1/2材质；284 Shader通道、16实际绑定和41效果GPU检查通过。全卡原专属动画、完整Shader/脚本仍未完成。见 [ORIGINAL_VERTEX_SHADERS.md](ORIGINAL_VERTEX_SHADERS.md)。

恢复原SetVisibility局部/toggle/退出重置，20执行与41GPU检查通过；真实原纹理调查纠正Card_Hero缺失头像推断。完整外部Card/场景与全卡动画未完成。见 [ORIGINAL_LOCAL_VISIBILITY.md](ORIGINAL_LOCAL_VISIBILITY.md)。

恢复原延迟显隐与书本Death状态相对时序，39新增检查、41GPU及原节点/None重放通过；ColorInterpolate与完整整卡仍未完成。见 [ORIGINAL_DELAYED_VISIBILITY.md](ORIGINAL_DELAYED_VISIBILITY.md)。

原ColorInterpolate游戏时间分支恢复，17程序/44动作/311参数回读、55检查与实际书本材质通过。全卡完整Shader/脚本和正式整卡仍未完成。见 [ORIGINAL_COLOR_INTERPOLATE.md](ORIGINAL_COLOR_INTERPOLATE.md)。

原AudioPlaythrough协程继续/重入/延迟读取已恢复，13新增、29既有音频、41GPU检查通过；完整SoundManager与原全卡仍未完成，见 [ORIGINAL_PLAYTHROUGH_COROUTINE.md](ORIGINAL_PLAYTHROUGH_COROUTINE.md)。

普通原音源模板/空间曲线与节点跟随、销毁停止恢复，10 SDK及41GPU检查通过；完整原声场/Shader/脚本/全卡未完成，见 [ORIGINAL_SOURCE_AUDIO_TEMPLATE.md](ORIGINAL_SOURCE_AUDIO_TEMPLATE.md)。

既有原音源复用与None代码音量保留恢复，6 SDK/源FSM、41GPU检查通过，完整SoundManager/原全卡仍未完成，见 [ORIGINAL_SOURCE_REUSE.md](ORIGINAL_SOURCE_REUSE.md)。

恢复57程序126个原AudioSetVolume动作与实际渐弱，6 SDK、41GPU及1177常规测试通过；完整原全卡仍未完成，见 [ORIGINAL_AUDIO_VOLUME_ACTION.md](ORIGINAL_AUDIO_VOLUME_ACTION.md)。

恢复原取消/死亡/结束状态的指定音源停止，10程序16动作回读、8 SDK及1178常规测试通过；完整 SoundManager 和全卡效果未完成，见 [ORIGINAL_AUDIO_STOP_ACTION.md](ORIGINAL_AUDIO_STOP_ACTION.md)。

原淡入淡出计时、完成等待与显式音源绑定恢复，61 程序168动作来源已回读、175音源/声音定义节点接入、24 SDK检查通过。全卡专属动画、完整原 Shader/脚本未完成，见 [ORIGINAL_AUDIO_FADE_ACTION.md](ORIGINAL_AUDIO_FADE_ACTION.md)。

原循环音源启动/结束与固定音频选取恢复，12程序36动作回读，33 SDK检查含全部12真实原启动程序通过。完整SoundManager、销毁回调和全卡效果未完成，见 [ORIGINAL_AUDIO_LOOP_ACTIONS.md](ORIGINAL_AUDIO_LOOP_ACTIONS.md)。

两张已审核区域卡的原 SuperSpell 区域分派已接入正式播放器；48项上下文/生命周期/图像检查通过。全卡与完整Shader/脚本未完成，见 [ORIGINAL_AREA_PLAYER_INTEGRATION.md](ORIGINAL_AREA_PLAYER_INTEGRATION.md)。

原主相机震屏世界轴/时序与正式相机驱动接入，66程序87动作回读，13新增SDK/41GPU/1182常规测试通过。完整原相机场景、Options与全卡目标未完成，见 [ORIGINAL_MAIN_CAMERA_SHAKE.md](ORIGINAL_MAIN_CAMERA_SHAKE.md)。

原 AudioPlaythrough 指定 SoundDef 的生成音源路径已接入，173程序280动作来源回读。完整声音服务及全卡目标未完成，验证见 [ORIGINAL_PLAYTHROUGH_SOUND.md](ORIGINAL_PLAYTHROUGH_SOUND.md)。

原物体音源限流、拒绝保留播放及只回收生成音源的替换策略恢复；279声音动作解析，1项蛇陷阱源诊断保留。完整声音服务及全卡目标未完成，见 [ORIGINAL_OWNED_SOUND_LIMITS.md](ORIGINAL_OWNED_SOUND_LIMITS.md)。

双层alpha-noise与SneakySmoke原公式、8原始纹理/mip/采样恢复，79新增GPU/纹理检查通过。全卡原Shader/脚本仍未完成，见 [ORIGINAL_SMOKE_SHADERS.md](ORIGINAL_SMOKE_SHADERS.md)。

232根投射物参数及2个独立源Prefab恢复，原刀扇克隆位置/生命周期27项检查通过；完整根missile/impact调度与全卡目标未完成，见 [ORIGINAL_MISSILE_PATH.md](ORIGINAL_MISSILE_PATH.md)。
