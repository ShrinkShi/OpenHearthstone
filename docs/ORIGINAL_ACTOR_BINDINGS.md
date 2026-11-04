# 原脚本 Actor 对象链路

## 后续节点：客户端显示供给

2026-10-11。ClientApp现在设置ActorObject，NativeBattleActors按实体身份供给真实原英雄/随从模型；Presentation在绘制实体前采样原脚本，LiveActor渲染实际模型并让生命/攻击/状态区域跟随缩放和平移。模型UV/Mesh校准与静态画面共用，用户校准文件未变。退出的效果先恢复借用对象，再按存活引用释放模型/相机/纹理。同Actor第二写入者尚不能仲裁，明确拒绝绑定。

正式原效果仍只启用2个。原出生/死亡/弃牌/触发槽位路由、武器/手牌/技能Actor、完整Actor显隐/初始化/附属Spell、世界目标位置和全卡Shader/脚本仍未完成。适配器18项GPU检查/5帧通过，是离屏模型验证，不是人工完整画面或全效果验收。详情见 [NATIVE_BATTLE_ACTORS](NATIVE_BATTLE_ACTORS.md)。

以下为此前记录，“普通棋盘尚未供给Actor”已由本节点替代，其余缺口保留。

2026-10-11。本节点恢复有实际目标供给时的对象读写和缩放执行，正式棋盘显示绑定、全卡原脚本/Shader仍未完成。

## 原链路和实现

原 `SpellGetActorAction.OnEnter` 调用 `SpellAction.GetActor(Which)`，经GetCard取得Actor，再写入非None的FsmGameObject变量并Finish。原 Which：Source=0、Target=1、SourceHero=2；SourcePlayer=4在GetCard中降为SourceHero；PowerTarget=3/SpellActor=5没有对应对象时按原分支回退Source。没有Target时返回null，不替换为特效Root。

静态IL SHA256：GetActor动作 `05cbf841f026e94a76e91230ace39a8cef8b6b44b701088b90d3ccba76f62108`，SpellAction `29a8ca76b22669d3323ff991e86caebb72fa311243940133fa45f32a2b697326`。只读Cecil导出的IL，未加载执行官方程序集。

NativeSpellContext接受角色到实际GameObject的供给；NativeEffectInstance有按Machine分隔的GameObject变量表。SpellGetActorAction写入后，SetScale与ScaleTo优先读变量的当前对象，而非导出时node=-1的空快照。ScaleTo的停止作用域现在按实际owner对象/type匹配，避免两个外部对象都为node=-1时相互停止。

借用Actor的初始缩放被记录，反向Seek重放前恢复，Dispose恢复且不销毁调用者Actor。缺少Actor供给明确计入UnboundActorActions；不伪造根节点。外部Actor的淡入材质绑定未完成，仍拒绝，不把普通UI材质当完整原Shader。

NativeEffectPlayer有ActorObject(EntityView)供给接口，FromEvent将before/after快照中的Source/Target/SourceHero身份传入对象选择，保证出手牌、离开战场的实体身份仍可查询。**当前普通棋盘没有设置该供给接口**，实际EntityView到场上Actor生命周期/隐藏原GUI/动态模型显示仍需接通；因此不能称正式卡牌Actor动画已可玩。

## 原数据与模型

`python -m tools.native_effects --refresh-actor-cache` 精确刷新25个Prefab中的31动作、93参数、0未解析、0失败；首次25更新，再运行0更新。来源在 `manifests/native-actor-refresh.json`。刷新也保留SpellInitActorVariables参数，但该动作的完整根对象/根网格初始化尚未实现，不计作动作恢复。

CardVisuals新增CreateActor，复用既有原模型、卡牌原画和材质设置逻辑；Render继续走相同Capture及校准流程，没有重写UI布局。CreateActor返回的模型和独立材质由调用者管理。用于验证的是MODEL_MINION与CS2_179原画，普通卡面/随从渲染仍按既有方法运行。

原 `RLK_086@m_TriggerEffectDefs:1` Death先GetActor(Target)写入Actor、SetScale为0、随后ScaleTo向1弹性恢复。原变量链现可驱动供给的真实模型，未绑定时仍计1，保持禁用。

## 实际验证与差异

18项新增原对象检查：Which分派及回退、快照身份、原Prefab隐藏/弹性/终点、重放、借用所有权、未供给明确失败。78078原资源/运行时、4101既有C#、Windows构建通过。688Python通过（20.89秒）。

独立Actor GPU检查8项/5帧通过：实际原模型0秒完全隐藏，.1秒弹性超调，.3/.5/1.01秒可见，结束归还原始缩放。截图已查看，位于 `tests/visual/current/native-actor/RLK_086-target-CS2_179-*.png`。固定相机保留超调空间；这不是正式棋盘或人工对局截图。

标准原效果GPU41检查/146帧通过；40Prefab/133生命周期调查798帧0异常仍保留46空白场景。新增Actor未绑定统计，3个执行场景缺Actor供给；以往只有Tween计数，不能用其他动作的零计数证明Actor绑定完整。两个已启用效果的cache SHA保持审核值，不增加启用数量。

日志：`work/build-native-actor-model-final.log`、`work/native-actor-final-gpu.log`、`work/native-actor-render-checks.json`、`work/native-itween-survey.json`。未使用Computer Use，官方安装只读。下一步需要接通正式棋盘实体/模型身份与原生命周期，再逐个检查完整效果，不能仅靠接口或离屏片段启用全卡。

最终Windows构建双Profile `nativeactorfinal_1/2` 实际自动比赛：match `337a842ecc5741cebea3e5c36b3ad56b`，12回合/v41，Shrink2术士8HP胜、Shrink1法师-5HP负，两端public_hash `439de020fee7b541fa50d518d9ff815dc84572c8383136880f869a9ca5caadd6` 相同、队列峰值1。仅后台回归，不是人工GUI/FPS验收；测试进程清理保留用户服务器。
