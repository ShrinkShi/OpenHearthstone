# 原 SetColorRGBA 与动态材质透明度

2026-10-12。完整全卡专属动画、原 Shader/脚本未完成。

## 原来源与执行

静态读取 Blizzard.T5.PlayMaker.Runtime 的 SetColorRGBA.OnEnter/OnUpdate/DoSetColorRGBA。原动作只在 FsmColor 对象为 null 时立即返回；它不以目标 IsNone 作为跳过条件。逐通道检查 FsmFloat.IsNone，None 通道保留旧值，其余读取当前数值，不夹取 HDR/负值。OnEnter 总会写入一次；everyFrame 为 false 时立即完成，true 时继续 OnUpdate。

NativeRGBAAction 和 NativeEffectInstance 已接入这组规则。具名颜色更新当前机器变量；匿名本地颜色按动作保存在实例私有字典，退出状态仍保留，重建播放时重置。共享原程序没有被修改。通道 None 标记或具名 float 缺失时记录 UnboundColorActions，不猜测源值。

当前采样器在原 float 曲线更新后执行 everyFrame 的 RGBA，再写 everyFrame 材质属性；真实 CardHeroAlpha 曲线已到达 Card_Hero 的 _TintColor.a。完整原逐动作/逐帧调度仍未恢复，不能以赋值成功宣称整个 PlayMaker 调度一致。

## 旧缓存修复

第一轮新增 86 项检查通过，但既有祝福颜色检查失败：旧普通效果 cache 的 SetColorRGBA 没有保存 NamedVariable.IsNone，严格执行器拒绝缺失数据。没有降低检查要求。

`python -m tools.refresh_rgba` 复用既有 refresh_program_assets/原 FSM 解析器，精确刷新 22 程序、56 动作，全部22缓存更新，失败0。公开来源为 manifests/native-rgba-refresh.json，保留每个源 CAB/PathID/Bundle。使用独立 rgba 报告，不覆盖原 AnimateColor/Ease 报告；缓存只将已经恢复的 SetColorRGBA 移出 unknown_actions，没有修改启用名单。

## 实际验证与差异

work/native-rgba-states.log：新增 88 Unity 检查及全部既有检查链通过，退出0。覆盖16种通道组合、None 不读值、HDR/负值不截断、null目标、缺失None信息、真实原Prefab每帧材质写入与共享源数据不变。everyFrame=false 的材质透明度保持一次写入；true 保持动作未完成，没有显式事件时不合成 FINISHED。

work/native-rgba-gpu.log 已跑 41 项标准 GPU 检查通过；随后补上 everyFrame=true 的未完成语义并验证88项，再以 work/native-rgba-gpu-final.log 复测41项标准GPU全部通过，退出0。没有把普通效果的隐式自动结束当作原行为。

实际捕获 tests/visual/current/native-rgba/holy-start.png：相比先前只有小红点，现在能看到一块卡形轮廓；尚缺目标原画、原神圣光芒及完整 Shader/屏幕合成。它是独立原Prefab诊断图，不是正式比赛截图或原版同状态参考，没有新增整卡视觉一致性通过项目。

原启动图与普通效果图已接入相同执行器，正式效果启用仍2。完整目标仍包含原Shader、目标Card、原战场/相机、任务历史与全卡专属动画；本节点不扩大启用范围。

未 Computer Use，官方只读，用户 UI 校准未改。

Python：1172 passed/1 deselected/20 warnings/661.45秒，新增22程序原RGBA字段/None重读一致性通过。排除的唯一项为此前通过且代码/原缓存未变的254程序715定位组件全层级重读；未宣称本次同命令全量绿色。

Windows work/build-native-rgba.log退出0，Mixer九个实际编译常量一致。用户UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

下一处明确缺口：真实启动图的 SetProperty.targetProperty 为kind36，目前unresolved。须解析原复杂属性与组件绑定，不根据小红点猜测值或任意增大粒子/提高亮度。已确认BigGodRays原size scalar13，并非因为已知零尺寸可以直接修正。

最终构建后台rgba_1/2独立Profile在47446完成68a9f8b00453413e9be2fdd1b458216d，13回合/version40。Shrink2法师26血胜、Shrink1法师-4血负，两端public_hash均2b65e738abf9264e7e6d0c39b87578ab6ac7f014e368133ba229d843d77ffbf8。日志无Exception/fallback/Error，终局核对命令行后仅清理自有33064/34924/14120。后台不绘制GUI，不作为新祝福效果正式视觉或FPS验收。
