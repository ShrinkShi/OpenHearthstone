# Hearth Visual Database v1

这是可扩展研究数据库种子，不是全部资源的完整导出。机器入口 [effects.json](effects.json)，已存在的大规模源参数/依赖清单继续位于 manifests；不复制或搬迁它们。

## 数据契约

顶层 schema_version、updated、confidence_values、effects。每效果有稳定 effect_id/card_id/name、原 Prefab 与缓存/清单引用、组件/依赖/控制器/生命周期、时间/路径/Shader 状态、来源与可信度、还原等级与关联 Task。没有核实的数据用字符串 `Unknown`，不填推测默认值；本地路径不存在时不能视为恢复成功。

可信度：Confirmed 实际源码/源数据/测试验证；Observed 指定运行观察；Inferred 证据推导；Hypothesis 待验证；Unknown 未知。每条关键结论应指向 sources 与 evidence_scope；静态 IL 只能证明该版本程序表达，不能证明我们的实现已经运行正确。

原图节点/组件/材质/纹理/粒子/音轨依赖通过 source_graphs 和 source_manifests 关联现有 JSON；导入缓存是忽略的 local/runtime/*.json。本种子不完整展开这些数据，unexpanded_dependencies=true，不得宣称图闭包完整。

Shader records 单列 Source Recovery / Behavior Recovery / Visual Accuracy；render_state 中未知项仍 Unknown。未来依赖图工具应从现有 Importer/清单输出统一 typed nodes/edges、稳定身份与未解析边，不每次重复人工翻包。

## 当前索引

| Effect ID | 卡 | 已核实 | 未完成 |
| --- | --- | --- | --- |
| hs.ex1_129.superspell | EX1_129 刀扇 | 原资源/独立路径命中、自主根/投影/显式宿主已独立V003-r6；Task4单侧双视角关键帧 | 官方同条件序列、完整Shader/场景与官方整卡逐帧 |
| hs.ex1_134.superspell | EX1_134 军情七处特工 | 原资源/独立路径命中、自主根/显式宿主已独立V003-r6；Task4单侧双视角关键帧 | 官方同条件序列、部分Shader/完整场景；旧白中心为内部诊断，新根仍小暗 |
| hs.hero_08bp.fireblast | HERO_08bp 火焰冲击 | 首轮五项由V005-r2本地有界PASS；用户新提供按钮/飞行/命中/数字参考并追加发射音效、拖尾粒子、命中与数字反馈修复，当前IN_PROGRESS | 发射源轨、原飞行Trail/粒子、伤害数字链待新Feature核对；原全局灯/四阴影/动态相机/遮挡及完整动态视听仍未闭合 |

整体视觉等级仍暂定；子系统独立验收以 effects.json 的分项scope为准。Task4新增 visual_comparison、官方输入缺项、单侧重放/Shader审计机器入口，缺官方序列而BLOCKED，八项官方比较全Unknown；执行者GPU与重复性不等于独立V004。完整 source hash 在 manifests 中，原产品版本仍Unknown。数据模型稳定后再做 Browser。
