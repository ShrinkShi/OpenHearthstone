# 原Spell生命周期入口和时间轴

2026-10-11。全卡原动画、完整Shader/脚本仍未完成。

## 原依据

静态Cecil审计新增SpellBase/SpellStateType，总计144类型，各类型仍restored=false。没有加载执行官方程序集。

- SpellStateType原枚举：None=0、Birth=1、Idle=2、Action=3、Cancel=4、Death=5、Reset=6。IL SHA256 `cfae8aa14dd1f3218a4487c5cb2f5d88a30b07ea9eee204f31a261c4e750def1`。
- Spell.GuessNextStateType：None依次选Birth、Idle、Action、Death、Cancel；Birth只尝试Idle，缺失则None；Idle只尝试Action；Action只尝试Death；其他返回None。不会自动跨越缺失阶段。Spell IL SHA256 `521c912883e863201ba836dbb088d2dbec2bbf61481f0642ac3ba9144b42f491`。
- GenerateSpellFsmStateList/BuildFsmStateMap：仅含SpellStateAction的状态进入FSM生命周期映射，映射按全局转移目标匹配，首个匹配保留。普通粒子FSM不因存在Birth事件就成为Spell。
- SpellStateAction.DiscoverSpellStateType按当前状态的全局转移查事件；完整Spell owner解析、OnFsmStateStarted/状态回调/SpellState对象播放停止仍未恢复。动作IL SHA256 `7d8a3e51ef542f0066cb0eddc285ee1909c7551e9de9f2164dcef1ae25db1d17`。

## 接入

NativeSpellLifecycle.Next恢复上述原分支，usability由调用者供给。运行时默认入口使用已就绪FSM中实际含SpellStateAction的映射；没有映射的普通FSM保留start。原HasUsableState的自定义重写、禁用Zone、未就绪FSM回退尚未完整接入，不能把此选择器称为完整Spell类。

NativeEffectInstance.SetLifecycleTimeline按精确时间对指定Machine进入映射状态，沿用原状态退出、Tween/Animation停止和借用Actor恢复逻辑。采样遇到事件时间时插入精确边界，跨帧不会把Tween起点改成当前帧；同时间保留请求顺序，回退时重放游标。缺少映射明确计UnboundLifecycleEvents，不替换为其他阶段。非法范围、NaN和超长计划拒绝。

NativeEffectPlayer结构化事件路径接受已审核索引的initial_lifecycle与lifecycle[]（machine/state/time），转给原运行时。现有2个已启用效果没有新增计划，没有修改缓存或启用名单。**全卡生命周期自动路由、原PowerTaskList/SpellController、触发槽位语义映射仍未完成**，不能凭索引数组顺序猜某个扳机作用。

## 验证

新增32原运行时检查，合计78110；4106客户端、688Python（21.23秒）、Windows构建通过。检查涵盖原分支、原RLK映射、时间边界、真实目标模型、重放、同时间次序、缺少Reset、非法作用域及索引计划传递。

新的离屏GPU7检查/5帧：原RLK_086@m_TriggerEffectDefs:1的Machine1从None在0.4秒进入Death，真实MODEL_MINION/CS2_179模型在0.39秒存在、0.4秒隐藏、0.5秒原弹性超调、1.41秒复位。截图 `tests/visual/current/native-lifecycle/RLK_086-Death-at-0.40-*.png` 已查看。此时间用于确定性验证，不声称0.4秒就是完整原卡触发时序；该槽位仍禁用。

Actor GPU18检查/5帧、标准GPU41检查/146帧通过；40Prefab/133生命周期调查798帧0异常，46空白场景保留。日志 `work/build-native-lifecycle-final.log`、`work/native-lifecycle-final-gpu.log`。未进行Computer Use或人工GUI性能验收，用户校准SHA不变。

最终构建双客户端 `nativelifecyclefinal_1/2` 自动完成20回合/v64：Shrink2术士7HP胜，Shrink1法师-4HP负；match `d4fb0cbcacc74574af2734e1d90ceea1`，两端public_hash `d985bfa3f01976d1e9cbf68d826e12ea23ad02622720d92b183fa95e024b6a48` 相同，队列峰值1，日志无异常。测试进程清理保留用户服务器；后台回归不证明人工GUI性能。
