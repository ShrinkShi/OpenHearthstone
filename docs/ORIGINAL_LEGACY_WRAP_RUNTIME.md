# 原旧版动画模式的采样接入

2026-10-11。

## 实际 Unity 证据

`NativeLegacyWrapChecks.Run` 在隐藏 Play Mode 测试 17 个组合：Clip 模式 Default/Once/Loop/PingPong/ClampForever，各配组件 Default/Once/Loop，再测试两个显式状态覆盖。39 项检查通过，原始数据在 `work/native-legacy-wrap-oracle.json`。

已验证覆盖顺序为状态 > Clip > Animation 组件；全 Default 时表现为 Once。原状态 getter 有时仍返回 Default(0)，不能直接把 getter 当成最终有效模式。Loop/PingPong 的状态 time 持续增长，区别发生在姿态采样。

## 正式客户端变更

NativeEffectData 读取新 Clip 的 wrap_mode/wrap_known 和节点 legacy_animation。NativeLegacyWrap 按上述覆盖顺序解析原枚举；采样区分 Repeat、PingPong 和末端 Clamp。正式旧版混合的变换、材质与可见性通道使用同一采样时刻。PingPong 的 PlayAnimation 不按单次长度提前结束。

未知枚举、已知 Default 但缺少组件模式的动作计入 UnboundAnimationActions，不能擅自推断。未刷新的旧缓存继续用原 loop/末帧保持兼容路径；Animator 路径未改变。原缓存字节、启用标志和用户校对文件保持不变。

## 验证

原资源/运行时 77241 检查通过，包含 26 项新增覆盖顺序、反向时刻、实际实例 PingPong、回退、继承 Loop 和缺模式拒绝。37 Prefab GPU 检查/126 帧通过；Windows 构建成功，日志 `work/build-native-wrap.log` 与 `work/native-wrap-prefab-checks.log`。既有 C# 检查也在构建中运行。

构建中的 4101 项既有 C# 检查通过。后台双 Windows Player wrap_1/2、端口47370完成15回合/v48，Match 58423dc9f3ca47f497010f889d45c83d。Shrink2术士1HP胜、Shrink1法师-3HP负；双方公开哈希68a25d473f0282c786ebaf44f1b692fb64ba04201c44a389c2a35aae7c15a6ab相同，表现队列峰值1。后台未绘制普通GUI，不作为正式FPS或人工验收。

本节点未改 Python，最后完整测试为 683 项通过；未重复 Python 全套。

## 未完成

新模式还没有回填已有源缓存，已启用两个效果仍用已审核旧数据，没有增加已还原卡数。Once 的自动停用、动态速度与完成事件、严格 time>length 边界、完整状态模式覆盖动作、loopEvent 派发、组件自动播放、完整 Animator/PlayMaker/Actor、多层混合和全卡专属效果仍未完成。

本节点没有使用 Computer Use，没有人工 GUI/正式帧率验收。

后续动态完成节点已接入正向完成、变速/暂停、命名停止/替换和原事件条件；负速及完整事件路径仍未完成，见 [ORIGINAL_DYNAMIC_ANIMATION_COMPLETION](ORIGINAL_DYNAMIC_ANIMATION_COMPLETION.md)。

后续源缓存回填及Playthrough单次自动退役已完成本节点范围，见 [ORIGINAL_ANIMATION_WRAP_CACHE](ORIGINAL_ANIMATION_WRAP_CACHE.md)；完整全卡效果仍未完成。
