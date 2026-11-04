# 视觉兼容等级与验收

| 等级 | 含义与必要证据 |
| --- | --- |
| V0 Placeholder | 占位 |
| V1 Approximate | 肉眼近似，明确非原行为证明 |
| V2 Asset Accurate | 主要原资源/身份正确 |
| V3 Behavior Accurate | 资源、控制链、生命周期与运动验证 |
| V4 Shader Accurate | Shader/Material/Render State 高度还原并有证据 |
| V5 Frame Accurate | 同状态同相机关键帧的时序/位置/色彩等高度一致 |
| V6 Native Equivalent | 自主客户端达到目标完整效果，不依赖原运行时 |
| F Original Runtime Fallback | 可选原运行时执行，单列，不是 V7 |

分项记录 Asset / Projectile / Path / Material / Shader / Timing / Hit / Audio / Overall。整体等级不是均值或最大值；不得绕开完整控制链缺口。数据库初始使用 conservative claim 与证据范围；未知等级为 Unknown，未经独立验收标 provisional。没有 V5 证据，不因达到自主运行而越级 V6。

比较固定场景状态、版本、摄像机、分辨率、随机种子、音频环境；按 Birth / Early / Mid / Arrival / Hit / Death，必要时 T=0/.1/.25/.5/.75/1 采样。时长不同应同时保存绝对时间和事件相位，不强行归一化掩盖节奏差异。

检查位置/轨迹/大小/旋转/持续时间/颜色/亮度/Alpha/粒子数量/Trail/Shader/音频与节奏。像素差只能用于可比帧，遮挡、色彩空间、后处理等环境差异要解释。

当前路径诊断不是官方比较；两个样板卡 Overall 仅暂按 V2 的资源来源范围记录。TASK-0003自主根/本项目投影与显式历史宿主已独立V003-r6通过，不能替代官方完整行为/视觉。V4/V5/V6整卡未完成。标准 GPU、规则测试、后台自动局都不独自证明视觉准确。

TASK-0004已保存双卡双视角单侧事件序列与分项结果；官方同条件序列实际缺失，位置/旋转/时间/颜色/Alpha/粒子/Trail/声音比较均Unknown，任务BLOCKED。28帧本项目重复一致只证明该诊断重放稳定。[机器入口](../../manifests/task-0004-visual-evidence.json)、[范围与解除条件](TASK4_EVIDENCE.md)。
