# 原 FsmProperty 描述与组件属性写入

2026-10-12。完整全卡原专属动画/Shader/脚本未完成。

## 来源与数据

原 SetProperty.OnEnter 调用 FsmProperty.SetValue；everyFrame=false 时完成，true 时保留动作并继续 OnUpdate。FsmProperty 按实际成员类型选择对应参数，并保留各参数的 IsNone。只静态读取原 IL，未执行官方程序集。

原 actionData 的 kind36 通过 fsmPropertyParams 存储对象、组件类型、属性名与完整参数集合。新增 tools/fsm_property.py 复用原指针和节点索引，保存 TargetObject、TargetTypeName、PropertyName、全部原字段、目标CAB/PathID/类型及目标节点。不按名字猜目标。普通参数解析器接入同一格式。

`python -m tools.refresh_properties` 已刷新99程序/554动作/1108顶层参数，失败0，其中552 SetProperty、2 GetProperty。目标类型为423 ParticleSystem、70 TrailRenderer、36 GameObject、25 RenderToTexture；这组数量是数据覆盖，不是全部执行完成。schema补充严格空目标与原Int32字面值，按保留raw字段自动归一化；未改官方资源。

`python -m tools.super_start_source --prefabs-only` 重新导出当前原启动Prefab，保留已有232根配置；原复杂属性不再被标为未知kind36。

## 执行与正确性

NativePropertyAction 只对当前Unity SDK的已知组件/普通成员进行真实赋值，不加载原自定义程序集。Float/Int字面值/Bool/Color/Vector等已接入；None参数不读值、不写入。原整数字面值单独保存，避免通过float损失精度。动态Int/String/Rect/Quaternion、枚举/数组/资源对象、嵌套成员、GetProperty及原自定义组件仍未恢复；不以近似值冒充支持。

只写实例自有节点；借用实体的完整属性快照未恢复，明确计入UnboundPropertyActions。缺组件、未知类型或缺变量也明确计数。自有粒子属性在重建时恢复完整MinMaxCurve/Gradient，不以标量覆盖原曲线模式；实际SDK检查验证了TwoConstants模式及上下限恢复。

原启动图两段AnimateFloat写同一个 BirthRaysPlaySpeed，SetProperty位于两者之间。原来的按动作类型分组采样会读到后一段值；现已将支持的浮点/颜色/缓动/属性/材质更新按原动作列表顺序执行。原事件立即中断、完整动作状态与所有帧调度仍未恢复，不能宣称整个PlayMaker完全一致。

粒子模拟沿用现有增量delta，每步先写属性再推进粒子；没有把当前playbackSpeed应用于整段历史来重算轨迹。

## 实际验证与视觉差异

work/native-property-ordered.log：10项新增SDK/真实源图检查及已有全部检查链通过，退出0。真实原启动Prefab的四个playbackSpeed写入全部绑定，原曲线读数到达实际ParticleSystem.main.simulationSpeed。原SDK setter、None不读值、曲线完整恢复、缺组件/自定义类型/嵌套拒绝也验证。

初次Unity编译失败是新.meta GUID长度错误，已修复。源图检查初次Single断言失败，揭示原变量双重写入；按原次序修正执行而非删除后一条源动作。

tests/visual/current/native-property/holy-start.png 已查看：卡形轮廓和红色粒子变化更明显，仍没有原神圣光芒/目标原画/完整Shader和屏幕合成。没有新增整卡视觉一致性通过，不扩大正式效果名单。

新Python原资源核对1项通过，2.10秒：554原属性payload、目标身份、类型/成员名、None与Int32字段一致。节点索引由原完整层级刷新得到，此检查不冒充重新走全部原场景层级。

work/native-property-gpu.log：41标准GPU检查通过，退出0。完整原目标Card/TrailRenderer/RenderToTexture属性与Shader、正式全卡播放仍未完成。

没有ComputerUse，官方安装只读，用户UI校准保持不变。

常规Python复测1170 passed/4 deselected/18 warnings/149.13秒，新增原属性来源检查包含其中。四项此前通过的层级定位/全部区域/全部启动/全部RGBA原重读本次排除；其专属恢复函数未变，源原始组件、层级和这四组既有参数未改。本次kind36属性扩展不作为它们重新通过的证据，不宣称全量同命令绿色。

Windows work/build-native-property.log退出0，Mixer九编译常量一致；UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

最终构建后台property_1/2独立Profile在47447完成196f7fc02a124e348c94d3ad25fdefec，15回合/version51。Shrink2法师14血胜、Shrink1法师-1血负；两端public_hash为3aef6e90b74b3bbcbd9f4c433c48d7deccc5db30c1d8e92d3dc8895da8ef7441，日志无Exception/fallback/Error。终局核对命令行后只清理自有33888/36144/34960。后台不绘制GUI，不证明原祝福在正式界面已还原。

下一步已定位的25个RenderToTexture属性包括m_AlphaClip、m_BlurAmount、m_BloomColor、m_ClearColor、m_TintColor、m_AlphaClipAlphaIntensity、enabled；其实际组件/捕获数据与属性写入尚未恢复。70个TrailRenderer属性及两个GetProperty同样未完成，不混入SDK赋值通过数。
