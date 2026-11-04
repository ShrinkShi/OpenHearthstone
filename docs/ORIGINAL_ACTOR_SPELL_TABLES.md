# 原通用 Actor 效果表

后续原Actor动画节点已接通AllyInPlaySpawn变换Clip，本页的1个外部动画缺口是当时状态；最新验证为0，正式表调度及完整效果仍未完成，见 [ORIGINAL_ACTOR_ANIMATION](ORIGINAL_ACTOR_ANIMATION.md)。

2026-10-11。现有全卡专属动画、完整原 Shader/脚本：**未完成**。

## 来源与导出

只读 catalog GUID 与原资源，未执行官方程序集：

| 表 | GUID | 槽位 | 同表不同 SpellType |
| --- | --- | --- | --- |
| Card_Play_Ally_SpellTable | 81be4c58f5a9d454e815f1b0703d8179 | 163 | 163 |
| Card_Play_Hero_SpellTable | c7908ae61ab34412aa7f5a9bf3c89cf2 | 62 | 62 |

两表共225槽、206个不同资源引用、205个不同导出文件。不同引用可能指向同一个源对象；同一个Prefab也可用于不同SpellType。当前两张源表内没有重复SpellType，不把重复Prefab误认为重复类型。来源、组件PathID、原始顺序、inline指针和各程序缺口见 `manifests/original-actor-spell-tables.json`。

命令：`python -X utf8 -m tools.actor_spell_tables`。独立输出 `local/runtime/actor-spell-tables.json` 和 `*.actor-spell.json`，不覆盖已审核的 `*.native-effect.json`，不加入正式启用名单。`--refresh-positions` 精确回填原Prefab根Transform位置：205文件检查、57文件更新，保留其他曲线/参数。子级、旋转和缩放仍取原数据。

225槽均取得Prefab，顶层导出失败0；**不等于完整资源恢复**：45个引用程序仍有依赖错误，92个引用程序有未解析动作参数。源表中包含后续版本/酒馆效果，导出它们没有扩展规则卡池。完整性仍为false。

## 原脚本语义

静态IL审计新增SpellTable、SpellTableEntry、SpellType，共149类型。原SpellTable初始化按列表顺序建立索引，重复SpellType取首项并警告。GetLocalSpell复用尚存实例；创建时先SetSpellType，再保存缓存，最后AttachAndPreserveLocalTransform。GetSpellInstance创建独立实例，不挂载、不替换本地缓存。ReleaseAllSpells只销毁本地持有实例。

`NativeActorSpellTable<T>`恢复上述查找/缓存子集，创建、类型赋值、挂载和销毁由显式回调提供。没有恢复inline活Spell时拒绝该槽，不用Prefab伪造。JsonUtility会把JSON null嵌套对象创建成空实例，因此通过源PathID判定有无inline绑定。AssetRegistry按Actor原SpellTable GUID绑定定义；NativeEffectInstance记录原SpellType，首次播放前可挂到父级。人工捕获容器归零，真正原Prefab节点保留源本地Transform，材质Origin随挂载更新。播放后拒绝重新挂载。

**未完成**：原SpellManager池、正式Actor.GetSpell共享表分支、完整Show/Hide/Release副作用、实际对战SpellTable实例挂载及调度、全Actor Animation/Animator、所有动作参数与音频、全部原Shader。新表仍用于恢复验证，正式对战原效果启用数保持2，用户校准保持原文件。

## 实际验证

新增原效果表C#检查覆盖首项选择、本地复用、独立实例、空引用、创建失败、未恢复inline绑定、被销毁缓存重建、所有权清理，以及真实英雄/随从DAMAGE程序的原父级坐标和FSM Owner（随从DAMAGE本来没有FSM，不制造一个）。具体最终数量记录于 `work/native-spell-table-checks.json` 与构建日志。

新GPU13检查/4帧：原SUMMON_IN槽位2 -> `Card_Play_Ally_SummonIn.prefab:786da6b44c25c46d39459e873d237292`，使用真实通用随从模板、未改写源FSM、显式测试表父级。在0/0.15/0.30/0.60秒捕获，原SpellInitActorVariables得到真实Actor，源粒子实际启动。截图在 `tests/visual/current/native-spell-table/`，已检查方向；主要Mesh以肖像材质展示，其他Actor Renderer关闭。**这不是完整正式游戏画面或完成的入场动画**。

源FX_EdgeMotes有0.1秒延迟，最初在0.1秒断言有粒子的测试失败，修正采样到0.15秒；没有去掉原延迟。原AnimationPlaythroughAction要播放Actor的AllyInPlaySpawn，该外部对象动画尚未恢复；新增明确动画缺口诊断，不能以跳过动作报告零缺口。落地音频未恢复。现有兼容Shader并非完整原Shader。

最终Windows构建：既有客户端4106、原资源/运行时78172、新效果表34检查通过。Python698（20.81秒）通过。回归GPU：Actor18、生命周期7、显隐4、ScaleFrom8、通用模板12、新表13检查4帧、标准GPU41检查/146帧；iTween调查40Prefab/188动作/133生命周期/798帧，0异常、46空白场景保留。日志 `work/build-native-spell-table-release.log`、`work/native-spell-table-release-gpu.log`。后台测试未使用Computer Use，不代表人工交互或60FPS验收。

首轮Windows双端 `nativespelltable_1/2` 17回合/v53完成，Shrink2术士16HP胜、Shrink1法师-1HP负；match `62beb95c86294a0a9f317f300c130fcb`，public_hash `03d4f3bc34cee5d4cb7a12cdb6989e62d781080d79f5c3f53d36c99d8f8d5934` 两端一致，队列峰值1。测试47391进程已清理，用户37240服务器保留。两个启用原效果缓存SHA与用户校准SHA未变。

补齐inline判定和外部动画缺口诊断后的最终构建，双端 `nativespelltablefinal_1/2` 完成12回合/v35：Shrink2术士21HP胜、Shrink1法师-3HP负；match `940edb52b0d44fcab6cfb4ca434cffe1`，public_hash `a4eb65b013057d49c9970ebf78dde66ac5d665716f92ef909f9a5abac799164c` 两端一致，队列峰值1。后台自动对局不能替代正式GUI、人工交互或性能验收。仅清理47392本节点测试进程。
