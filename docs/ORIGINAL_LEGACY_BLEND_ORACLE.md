# 原 CrossFade 的实际 Unity 对照

2026-10-10，接续 18273a5。全卡原动画、完整原 Shader/脚本未完成。本节点建立真实混合行为的运行证据，尚未改造正式动画混合播放器。

本页保留该节点运行记录；后续默认层正式混合已接入，范围和剩余缺口见 [ORIGINAL_LEGACY_MIXER](ORIGINAL_LEGACY_MIXER.md)。

## 原脚本与适用范围

静态 IL 表明 AnimationPlaythroughAction.StartAnimation 在 m_CrossFadeSec > Mathf.Epsilon 时调用原 Animation.CrossFade，否则 Play；PlayAnimation.OnEnter 则以 blendTime >= .001 为 CrossFade 分支。两者传递原 PlayMode。源类型和 IL 文件 SHA 见 manifests/native-legacy-blend-oracle.json；没有执行官方程序集。

当前 254 个缓存 Prefab 中，10 个含 11 个正值 CrossFade 动作（当前均约 .3 秒），涉及原冰系、火球、烈焰冲击、月火术、拉格纳罗斯和恶魔之火等资源。正式 NativeEffectInstance 目前仍删除该节点先前 Clip，再直接采样新 Clip；这不等价于原混合，仍未完成。

## 真实播放模式探针

NativeLegacyBlendChecks 在空场景进入 Unity Play Mode，创建自有 Animation/AnimationClip，在真实帧中执行 CrossFade，读取 AnimationState.weight、Transform 位置和其他层权重。不是以手工赋值权重或 Sample() 代替原 CrossFade 的时间推进，也没有加载正版游戏脚本。

复现：本机 Unity 2022.3.60f1c1 使用 `-batchmode -projectPath E:\HS\HearthstoneClone\UnityClient -executeMethod NativeLegacyBlendChecks.Run -logFile E:\HS\HearthstoneClone\work\native-legacy-blend-oracle.log`。不要加 -quit，探针收集真实帧后自行退出；30 秒超时会失败退出。只用于独立后台 Editor，不在正在编辑的 Unity 场景中运行。

原目标声明使用的 0/4 PlayMode，经本机引擎枚举核对：0=StopSameLayer，4=StopAll。初版对照测试将两者写反，因此失败；已纠正并重新运行。原多数动作默认 4，不能误读成只停止同层。

## 观测结果

首次完整扩展运行通过 299 项检查、74 个真实帧。基础探针通过后，又增加单 Clip 归一化、模式数值和重复同名播放对照。

加入 30 秒失败超时后的最终重跑通过 295 项检查、73 个真实帧，正常退出。差异来自实际帧调度，验证条件没有降低。

- .3 秒混合，在约 .075/.150 秒观测到 incoming 权重 .250006/.500011、outgoing .749994/.499989；位置分别为约 2.50006/5.00011，最终 10。
- 单一 Clip 的 rest X=7，目标 X=10：权重 0 时为 7；权重尚为 .25 时，实际 X 已为 10。引擎对唯一正权重动画归一化，不能用 Lerp(7,10,.25) 仿造。
- StopSameLayer 保留另一层权重 1；StopAll 逐渐降到 0。
- 再次 CrossFade 到正在播放的 B，B.time 不重置。这与“每次创建新 ClipClock”不等价。

帧时序权重误差阈值 .07 用于容纳引擎帧边界；位置与实际权重对照阈值 .1。帧数量和检查数量随运行调度变化，不应把固定数量作为成功条件。输出 `work/native-legacy-blend-oracle.json` 的 passed=0 表示仅保存了失败前观测，不代表成功；成功必须以最终检查日志及退出码为准。

## 尚未完成

原混合正式播放器、四元数/材质/离散通道混合、不同动画层、动态速度与同名时间保留、StopOnExit、自动停用和 LoopEvent 仍未完成。下一步应按上述引擎证据实现原混合，而不是冻结旧姿态做简单线性过渡。未新增启用卡槽，正式客户端仍使用上一节点构建，没有把离屏或 Play Mode 样本当作完整卡牌认证。

本节点只新增 Editor 探针与证据文档，规则、网络、正常 UI 未改变，因此没有重复进行完整规则/双客户端测试。上一节点 674 Python、77200 原资源/运行时、Windows 双客户端 12 回合/v38 的结果不冒称为本探针的新结果。未使用 Computer Use，未修改官方安装或用户 UI 校对。
