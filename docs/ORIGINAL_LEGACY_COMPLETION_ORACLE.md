# 原旧版动画完成边界：Unity 实际播放对照

2026-10-11。本节点只增加 Editor 测试，未改正式播放器、服务器或启用数量。

## 原脚本证据

本地静态 IL：`local/script-audit/Blizzard.T5.PlayMaker.Runtime.dll.PlayAnimation.il.txt`。不加载或执行官方程序集。

`OnUpdate` 在 AnimationState 未启用时派发 finishEvent 并 Finish；WrapMode 为 8 时另检查 time **严格大于** length。其他 WrapMode 的 loopEvent 分支检查 time > length 且进入时保存的 prevAnimtTime < length。当前 IL 的 OnUpdate 未发现 prevAnimtTime 更新，不能按常见实现自行改成每圈一次。

## 实际引擎证据

`NativeLegacyCompletionChecks.Run` 在隐藏 Unity 2022.3.60f1c1 Play Mode 中创建独立测试 Clip，逐真实帧读取状态。Clip 长度 0.2 秒，分别 Once、Loop、ClampForever；另一个 ClampForever 在约 0.08 秒速度设零、约 0.3 秒改为 2。

- Once 到末尾后 disabled。
- ClampForever 的枚举为 8，到末尾后保持 enabled，time 继续超过 length。
- Loop 到末尾仍 enabled，time 持续增长，不是每圈归零的时钟。
- speed=0 保持 enabled 和原 time；改为 2 后从暂停点继续增长。
- 记录 JSON 在 `work/native-legacy-completion-oracle.json`，含每帧、设置速度时刻和通过数量。检查数量随实际帧调度变化。

两次独立 Unity 进程均通过 94 检查，日志分别为 `work/native-legacy-completion-oracle.log` 与 `work/native-legacy-completion-repeat.log`。UI 校对文件 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

运行时必须不传 `-quit`，由测试收集完成后自行 Exit；30 秒超时退出失败。未使用 Computer Use，也没有操作用户游戏窗口。

## 正式实现差异与下一步

当前 NativeEffectInstance 的 PlayAnimation 完成仍使用固定 duration/abs(speed)，缓存仅保留 loop，缺少完整源 WrapMode。暂停、动态速度、已停用/被其他动画替代、负速度和原事件的边界尚未恢复。

下一步须保存原 Clip 和 Animation 组件的 WrapMode 及覆盖关系，再用播放状态驱动动作完成和 finishEvent；不能直接把本测试的 Clip 默认状态套用到全部原资源。原循环事件行为须按实际源语义及外部 FSM 派发路径核对。

后续节点已接入基于ClipClock的动态完成、停止/替换通知和单pending事件路径，见 [ORIGINAL_DYNAMIC_ANIMATION_COMPLETION](ORIGINAL_DYNAMIC_ANIMATION_COMPLETION.md)。本页前述固定duration差异为该对照建立时的状态；旧缓存回填和完整事件分发仍未完成。

本节点没有增加已还原卡数。完整原动画、完整 Shader/脚本仍未完成。规则测试最后验证为 674 Python、4101 既有 C#、77215 原资源/运行时；本节点没有修改对应代码，未重复完整规则测试或双客户端比赛。
