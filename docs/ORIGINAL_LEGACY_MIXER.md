# 原旧版动画混合接入

2026-10-10，接续 b8ba007。全卡原动画、完整原 Shader/脚本未完成。

原动画动作 IL、CrossFade 阈值和真实 Unity 对照见 [ORIGINAL_LEGACY_BLEND_ORACLE](ORIGINAL_LEGACY_BLEND_ORACLE.md)。本节点将原默认层的混合接入正式 NativeEffectInstance，不再仅新增探针。

## 实现

播放 CrossFade 保留旧 ClipClock，按动作时刻记录实际起始权重，分别向 0/1 推进。已经播放的同名 Clip 保留 entered/offset，不重置时间；完全淡出的旧时钟退场。停止动作先采样整体混合姿态，再停止指定名称。回退恢复源节点和权重历史，命名速度仍独立。

从原变换曲线构建本地 legacy AnimationClip，原节点添加禁用自动更新的 Animation 组件；每次表现时钟推进明确设置 AnimationState.time/weight，并调用 Unity 原生 Sample 混合位置、缩放、欧拉和四元数。没有执行官方程序集或用固定旧姿态插值冒充动态混合。

材质浮点和 RGBA/向量通道按实际权重累积后归一化，只有一个正权重 Clip 时输出完整样本。保留原 Shader 属性声明过滤；不可绑定的外部目标不会被改成自身。Clip 缓存按节点/源 ID 限定于效果实例，释放时销毁；不会重复创建每帧 Clip。复用混合字典减少临时分配。

当前原动作池使用默认旧版层 0；0=StopSameLayer 与 4=StopAll 在该范围等价，其他枚举计入未绑定。自定义非零层、完整 PlayMode/StopOnExit/LoopEvent/自动停用、全部 Animator 行为、交错中断权重曲线的原逐帧认证仍未完成。没有把默认层实现说成完整原脚本恢复。

运行时需要内置 com.unity.modules.animation。原项目只在 Editor 中引用该模块，正式脚本初次编译缺 Animation 类型；已显式加入 1.0.0 内置依赖及 Unity 生成锁文件。

## 验证

- 674 Python 测试通过，45.91 秒。
- 77208 原资源/运行时检查、4101 既有 C# 检查通过。
- 新增半程旧/新姿态、淡出退场、回退、单 Clip 归一化、材质 float/RGBA、重复同名时间保留。
- 材质样本初次测试使用另一棵源层级树的路径，没有绑定到目标。已改为直接以真实材质节点为动画拥有者，验证实际绑定后数值通过，没有放宽期望值。
- 34 Prefab GPU 检查/108 帧通过。Windows 构建成功，日志 work/native-mixed-prefab-checks.log、work/build-native-mixed.log。

新构建后台双 Windows Player mixed_1/2、端口 47366 完成 11 回合/v36。Shrink2 术士 13HP 胜，Shrink1 法师 -2HP 负，Match bc8f5184ca7a4771992e261917893395；公开哈希 f9c54bf3979d1804465bf21c22782a1184f11d5f3d82edeb24f0eb35e216406f 相同，队列峰值 1。后台不绘制普通 GUI，不作为正式渲染帧率或人工比赛验收。

已查看新的 CS2_025-0.30.png 与 CS2_062-board-components.png。紫色圆环可见；地狱烈焰中央亮黄色过曝仍存在。本轮没有新增启用槽，原完整目标场景、全局 Bloom 和全卡动画认证仍未完成。

未使用 Computer Use，官方安装、规则/网络与用户校对文件未修改。正式 1080p GUI 性能与长期泄漏未验收。
