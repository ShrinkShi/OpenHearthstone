# 原 PlayAnimation 退出停止

2026-10-10，接续 b7f4066。完整原脚本、Shader 和全卡原专属动画仍未完成。

## 原证据

Blizzard.T5.PlayMaker.Runtime.dll / HutongGames.PlayMaker.Actions.PlayAnimation 的静态 IL：OnExit 在 IL_0001 读取 stopOnExit，条件为真在 IL_0009 调用 StopAnimation；后者在 IL_001a 读取 animName 并于 IL_001f 调用 UnityEngine.Animation.Stop(string)。没有加载或执行官方程序集。

254 个缓存 Prefab 中，3 个 PlayAnimation 动作的 stopOnExit 为真，8 个非空 finishEvent 都是 FINISHED；没有非空 loopEvent。源动作/目标身份可从 native-animation-refresh.json 复现。本轮不将这些固定事件当作完整通用事件机制的恢复。

| 原效果 | 卡 ID | 缓存 | 动画拥有节点 |
| --- | --- | --- | --- |
| Arrow_Targeted_Arcane_FX | DS1_185 | 1ca86742da8eb534a697ead5 | 28 |
| Arrow_Targeted_ExplosiveShot_FX | EX1_537 | 2dc0f32c42c2aec252d77e39 | 17 |
| Arrow_Targeted_FX | DS1_183 / EX1_617 / CS2_189 | eb0bf826aae72bb9b8a0a26f | 16 |

三者使用源动画 a5ae3d081c5cf7614c830e06 / Take 001，duration 0.633333325 秒、22 个通道、非循环。此表为实际源动作范围，不是认证了全部关联卡的完整专属效果。

## 实现

成功播放时记录该 FSM 状态的节点/动画名退出停止要求。进入下一状态前按准确状态退出时间采样整体混合，再删除该节点的指定旧版时钟；不停止 Animator 或其他名称。之后恢复状态退出的其他临时属性，再运行新状态动作。

StoppedOnExitClips 是本地审计计数，回退清除并确定性重建。stopOnExit=false 不登记停止。未解析选项计入未绑定动画动作。服务器没有等待动画，规则/网络架构未改变。

## 实际验证

- 77215 原资源/运行时检查通过，新增 active 状态不提前停止、退出保护新状态缩放、false 保留、回退，以及三组真实 Death 分支各停止一个 Take 001。
- 37 Prefab GPU 检查通过，126 帧。日志 work/native-animation-exit-prefab-checks.log。
- 674 Python 测试通过（40.07 秒）；Windows 构建成功，4101 既有 C# 检查通过，日志 work/build-native-animation-exit.log。
- 新看 DS1_185-0.30.png 为小紫色环；EX1_537/DS1_183-0.30.png 为小橙色亮点。采样分别 5/4/3 帧可见，最大 1219/1101/357 像素；完整目标、尺度和专属场景仍未恢复，保持禁用。

新构建后台双 Windows Player exit_1/2、端口 47368 完成 13 回合/v39。Shrink1 法师 -2HP 负，Shrink2 术士 19HP 胜。Match 207849ee386b4ab9b00ce18dee820d68；公开哈希 61fcabef0e6518306f3eb36044efa62e8b84ee636ec8c1a337ce6c532d25bfd6 相同，表现队列峰值 1。后台不绘制普通 GUI，不作为正式帧率或人工操作验收。

## 未完成

自定义 Finish/LoopEvent 通用派发、动态完成时间、原 WrapMode 的自动停用及逐帧边界、AnimationPlayAction、完整 Animator/PlayMaker/Actor/外部所有权、多层混合、全局后处理和全部现有卡牌原动画仍未完成。启用槽仍为原有两个，不能将 StopOnExit 修复数量计为完成卡数。

未使用 Computer Use；官方安装和用户 UI 校对文件没有修改。GPU 离屏图不代表普通 GUI 整场验收。
