# 原启动 Prefab 与真实生命周期回调

2026-10-12。全卡原专属动画、完整原 Shader/脚本未完成。

## 数据与原执行顺序

`python -m tools.super_start_source` 从真实原根组件重读 232 个启动/Action 配置，复用区域导出器的 CAB/PathID/Bundle/GUID/节点身份解析，避免独立按名称猜资源。区域数据解析改为共享 helper，输出合同保持不变。

智慧祝福与王者祝福原根共享 ReuseFX_Holy_Idle_01.prefab:506c0a66b79385a4391e1db1b1f7ed59。独立导出一个 12 节点的 `.start-spell.json`、原音频及六种原 FSM 生命周期；原音频导出错误为零，未人工听音认证。不覆盖普通已审核 `.native-effect.json`。

NativeSuperSpawnStart 按原 SpawnStart 顺序增加待完成数、独立克隆、注册状态/完成回调、挂接、设置来源与原目标列表，然后按配置覆盖世界位置，必要时局部 Rotate 后 UpdateOrientation。目标列表保留 null 与重复项。与区域效果共享 CloneSpell 的回调/父子关系登记，完成通知与进入 None 后释放继续独立处理。

NativeStartInstance 使用真实源图，连接 NativeSuperStart 的 Birth 完成回调与 Action 完成后安全进入 Death。源 ChangeStateImpl 的 previous 状态完成回调在停止旧状态后、播放新状态前触发；列表按原逆序可变遍历，进入 None 后清除。回调中的即时状态切换不重跑 Seek，避免重入采样；普通已有播放器没有绑定这个新事件。

实际 HasUsableState 的原 Card/Override/Zone 服务尚未完整恢复，因此适配器要求宿主显式提供原可用性判断。本次源 Prefab 为普通 Spell、ZonesToDisable 为空，诊断使用原 FSM content 判断；不能将此判断推广到派生 Spell 或禁用区域的全部卡牌。

## 实际验证

work/native-spawn-start-final.log：16 项新增 Unity 检查通过，退出 0；智慧祝福/王者祝福各验证真实克隆、来源、目标列表、位置、Birth 播放、原 FSM 进入 Action、完成等待解除、Death → None 与克隆释放。继续执行既有 33 启动门禁、51 分派、32 区域、20 相机、11 定位、460 曲线及所有既有检查链。

首次 0.1/0.3 秒的捕获未达可见像素阈值，未计为通过；0.8 秒可捕获小红色亮点。原 Birth 动画 Wait 为 1 秒，DeathFadeOut Wait 为 2 秒；初次在 Death 结束前就要求释放，改按原时长在 4 秒核对，没有缩短原动画。真实生命周期验证不等于原 Shader 视觉一致性通过。

截图 tests/visual/current/native-start/wisdom.png 与 kings.png 为实际原 Prefab 独立捕获。当前主体仅小红点，缺少原神圣光芒与目标 Card 的完整呈现，仍使用兼容 Shader；颜色/FSM 属性写入、原屏幕捕获、目标 Card 渲染与完整 Shader 需要继续恢复。这两项未加入正式启用名单，也没有把官方截图当背景。

Python：1171 passed/1 deselected/19 warnings/383.75 秒；新 232 启动配置原资源重读与独立 Prefab 原状态核对通过，共享 helper 的全部区域来源核对也通过。排除的唯一项为之前已通过且本次代码/源缓存未变的 254 程序/715 原层级定位重读，不宣称本次同命令全量绿色。

## 未完成

原目标 Card/Controller 语义、完整对象池、源任务/历史绑定、派生 Spell 可用性、真实战场/相机合成与完整原 Shader、全卡专属动画、正式播放器绑定仍未完成。目标列表身份保存不能代替 Card 的实际渲染。

官方安装只读；没有 Computer Use；用户 UI 校准保持不变。

最终 Windows 构建 work/build-native-spawn-start.log 退出0，实际 Mixer 九个编译常量一致。UI 校准 SHA256 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6 未变。

后台最终构建在端口47445、spawn_start_1/2 独立Profile完成 bec322d390e0413fb9f9067e83cba194，17回合/version60，Shrink2法师7血胜、Shrink1法师-2血负。两端 public_hash 均 7b6c8a8b52c0fa3c7dfecf4526b2e228041d4efc7c5232acc9c7530f41266229，日志无Exception/fallback/Error。终局核对命令行后只清理自有12084/3680/18760；后台不绘制GUI，不作为新启动入口正式比赛播放或FPS验收。

后续已只读核对原 SetColorRGBA：它保留 IsNone 的颜色通道，只更新被指定的通道，并按 everyFrame 控制更新。当前运行时没有此动作分支；属于明确待恢复项，不能用整体改色或提高亮度代替原脚本。
