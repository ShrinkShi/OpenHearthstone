# 原 SetScale 脚本与曲线联动

2026-10-10，在 266711c 后继续全卡原专属动画/Shader/脚本恢复。全卡目标仍未完成；没有桌面控制，没有修改官方安装或用户布局。

## 原证据与实现

原静态 IL：`local/script-audit/Blizzard.T5.PlayMaker.Runtime.dll.SetScale.il.txt`、`PlayMaker.dll.NamedVariable.il.txt`、`PlayMaker.dll.FsmUtility.il.txt`、`Blizzard.T5.PlayMaker.Runtime.dll.AnimateFloat.il.txt`。只用 Cecil 读元数据/IL，不执行原程序集。新增 NamedVariable 审计后共有 130 个类型；审计中的 restored=false 不因实现一部分方法而改成整个类型恢复。

原 `NamedVariable.IsNone` 为 useVariable 且 name 为空。None 向量应读取当前 localScale，None 轴应保留当前轴；实际数字 0 应写成 0，不能混为“未提供”。原 SetScale OnEnter 立即写 localScale，一次性动作立即 Finish，everyFrame 动作保持未结束，lateUpdate 控制写入阶段。AnimateFloat OnEnter 先采样原曲线 Evaluate(0)，防止同状态随后 SetScale 读取陈旧默认值。

恢复内容：

- 新版对象表保留 none_known/is_none；旧版 packed Float/Int/Bool/Vector3 按原字段长度、UseVariable 字节与尾部 UTF-8 名称解析。名称存在时读取原 FSM 默认变量；截断/缺失变量保持 unresolved，绝不越过当前参数范围读下一参数。
- 客户端 SetScale 写入原本地目标，依次使用向量、再覆盖非 None 的 x/y/z，支持原 float/vector 默认值与持续曲线联动。
- 普通帧持续缩放跟随 FSM 变量更新；lateUpdate 写入在 Clip 阶段之后。当前原 119 个动作无 lateUpdate=true 样本，原 lateUpdate 的完整跨系统时序验收未完成。
- 每帧 SetScale 阻止 FINISHED；一次性动作不增加表现等待。服务器仍即时结算，不依赖动画。
- Seek 回退重置原 Transform 和变量默认值再重放。动态对象变量/外部对象绑定仍未完成，明确 UnboundScaleActions；未知 None 元数据不能猜测成全零向量。

## 全量提取结果

`python -m tools.native_effects --refresh-scale-cache` 核对源 CAB/PathID，完整重新遍历原主层级和 Prefab 引用的独立根，确认节点 name/parent/path 序列后再解码。只刷新 SetScale 参数和 vector 默认值，不重写几何/材质/Clip/启用索引。

实际 **55 个 Prefab、119 个动作、833 个参数、119 个 None 标记、0 unresolved、0 失败**。重复运行零更新。109 个动作有本地目标，10 个目标依赖外部/动态对象；这不是 109 张卡动画已完成。追踪见 [native-scale-refresh.json](../manifests/native-scale-refresh.json)。

测试曾发现刷新时空 node_ids 丢失目标引用，构建失败；已按源层级重建节点索引并修复全部缓存，完整测试重新执行后通过。原自然伤害样本的 `ImpactOffset/Plane_Thorns`（节点 23）恢复准确引用。

## 实际验证

- **662 Python 测试通过**，包含 None 与 0、UTF-8 packed 名称及参数边界检查。
- Windows 构建成功，原有 C# **4101** 检查、原资源/运行时 **76996** 检查通过。新增 11 项检查：真实自然伤害原曲线在 0/.3/.75/1.2 秒和回退时驱动荆棘缩放；仅写 X 保留 Y/Z；连续动作阻止 FINISHED；缺失变量不归零。
- 原 Nature 的 ThornsScale 曲线从 2.5 起，0.7 秒 2.8，0.8 秒 2.1；运行时按原曲线切线采样，没有另写简化 Tween。
- GPU 离屏 **29 项 / 78 帧**，新 Nature-Thorns 六帧诊断，6/6 可见、最多 2794 像素、57 粒子、黑色 alpha 0。查看 0.60 秒图仍是小型绿金冲击，不代表正常棋盘尺寸、目标位置与完整原后处理已恢复，未启用新特效。
- 前一次成功构建的后台双客户端完成 15 回合/v45，Match 8c0b7104b9cd456898c8320c01876fdf；公开哈希 `1320074e2bf2c57c7563a60c3940101ea652e8dee7c5ba9eeae81224647a7540` 相同。Shrink1 法师 11HP 胜，Shrink2 术士 -2HP 负。最终构建再做一局，见下方追加结果。

日志 `work/build-native-scale-verified.log`、`work/gpu-native-scale-verified.log`；图 `tests/visual/current/native-effects/Nature-Thorns-0.60.png`。后台比赛不绘制正常 GUI，不算人工比赛或正式 1080p 性能验收。其他 Shader 专项 GPU 套件本节点未重新运行。

最终构建后台复验：Profile `scalefinal_1/2`、端口 47346，14 回合/v44，Match 06fbbbd4c45b4fb79f899623819f52f3，双方公开哈希 `a474d3e36bc0081b2c9db14862c6fd6ab66ccbe4b01be18ca49b5f1ee761e59c` 一致。Shrink1 法师 -1HP 负，Shrink2 术士 5HP 胜。仅清理本节点测试进程，默认服务保留。

## 未完成

原动态纹理动作经核对只出现于两个死亡骑士特效，仍需 Actor/CardDef/premium/原画所有权绑定；没有猜测纹理来源。全卡目标绑定、完整原 Animator/PlayMaker、Shader 其他变体与后处理、专属音效/脚本及逐卡场景验收仍未完成。
