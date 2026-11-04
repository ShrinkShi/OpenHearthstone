# 视觉逆向基础与首套案例

## 已核实成果

Confirmed：`tools/super_missile_source.py` 与两个 `tests/test_super_missile_source.py` 用原资源重读核对 232 根配置及两个独立投射物图；清单为 `manifests/original-super-missile-config.json`、`original-super-missile-prefabs.json`。这是配置/依赖来源验证，不是 232 个已还原完整效果。

Confirmed：刀扇 EX1_129 / 刺骨 EX1_134 根为 SuperSpell，均有 MissileInfo/ImpactInfo。刀扇原 Missile 为 ReuseFX_Sneaky_FanOfKnives_Missile，刺骨为 ReuseFX_Sneaky_Attack_Missile_02。详细配置、源身份见 [数据库](../hearth_visual_db/README.md) 和 [投射物原记录](../ORIGINAL_MISSILE_PATH.md)。

Confirmed（静态 IL/代码对照）：原 GenerateMissilePath/CenterPoint、FireMissileOnPath、OnMissileSpellStateFinished、OnMissileTargetReached 与 iTween 控制点/Interp 是路径依据。采用 Catmull-Rom；不是二次贝塞尔。中心偏移百分比不截断，距离缩放、横向方向、随机顺序和先 Ease 后 Clamp 已恢复到 NativeMissilePath。

Observed：NativeMissileFlight 的实际原刀扇克隆在固定相机 GPU 中完成 Birth→飞行→到达回调→Death。27 项检查及开始/中段/到达截图见原记录。LookUpdate 服务只记录请求；完整旋转没有验证。世界离屏 20000 坐标导致已知浮点消去，纯公式 1e-4、真实克隆 .002 容差分别记录，不能混淆。

Confirmed（静态重读）：`SuperSpell/<WaitAndSpawnImpact>d__89`、metadata d__126/129/128、SpellManager 克隆/释放已入 original-script-audit。NativeSuperImpact 已补成局部可执行端点，两张样板配置与独立命中图来源见 original-super-impact.json。Observed：实际克隆完成 GPU/完成回调/None/物理释放检查；详见 [命中契约](../ORIGINAL_SUPER_IMPACT.md)。完整根发射、metadata 聚合和正式比赛未接入，待 V002 独立复验。

## 首套案例研究顺序

来源版本/身份 → 根控制系统与组件树 → Source/Target/任务输入 → Birth → projectile spawn → path/rotation/trail → Arrival → Hit → Death → 回调/回收。对每个状态写调用者、输入、时间、完成事件和销毁责任；缺失填 Unknown。

现有自动化已能扫描/导出依赖、源组件、材质 Shader、音频和独立图；不应重做全量人工查包。下一步依赖图工具应复用这些清单，使用 CAB/pathid/版本作为资源身份，输出 typed nodes/edges 与外部缺失引用，保留循环/共享节点而非硬拼一棵树。当前尚无宣称“完整通用依赖图工具”。

失败案例保留：摊平引用直接运行子 FSM 无法表达根调度；修复 Shader 后刀扇 .30 秒帧仍可能不变、刺骨中心仍白，不能归因为单纯参数。详见 ORIGINAL_SMOKE_SHADERS。未来标准工作流由 TASK-0004 在完整案例验收后定稿，本文件只是当前研究入口。
