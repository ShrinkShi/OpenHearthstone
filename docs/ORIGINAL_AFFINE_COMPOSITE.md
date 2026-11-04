# 原特效彩色透射合成

2026-10-10，接续 ce32dd2。恢复原乘色结果进入棋盘的合成链路；全卡专属动画、完整 Shader/脚本目标仍未完成。

## 原因与实现

原 Multiply 是目标四通道分别乘 K。旧 NativeEffectComposite 的 One/OneMinusSrcAlpha 只能存一份颜色和标量 alpha，不能保留不同的 R/G/B 透射。

NativeAffineCapture 按同一相机、同一源材质与顺序，在零背景及一背景上重放。C=零背景结果，K=一背景结果-C，任意背景 D 的结果是 C+D*K。目标使用 ARGBFloat/Linear，避免 C>1 时在获取 K 之前被钳制；内部原 RenderToTexture 保持原格式与状态。

正式 NativeEffectPlayer 先以 Zero/SrcColor 乘彩色 K，再以 One/One 加 C。透明淡出使用 K'=1+fade*(K-1)、C'=fade*C，零淡出完整保留背景。GUI RGB tint 只调 C，矩阵/UV 与原元素保持一致。旧 Render/标量合成保留供普通头像及历史诊断使用，原比赛特效走新双通道。

当前 48 组合法 source/destination 枚举覆盖 Zero、One、DstColor、SrcColor、OneMinusDstColor、SrcAlpha、OneMinusSrcColor、OneMinusSrcAlpha 的对角仿射组合。DstAlpha/OneMinusDstAlpha 的 RGB 跨通道耦合、目标自身参与二次运算及 SrcAlphaSaturate 被显式拒绝，不用近似曲线替代。现有原缓存材质没有这些被拒绝组合。

每次捕获恢复原材质 blend、相机 target/clear/color、RenderTexture.active，包括失败路径。状态数组复用，不每帧分配一组字典/数组；纹理随实例销毁，正式播放器继续最多四个活动实例。

## 验证

- 新 GPU 487 项：真实 Shader 有序直接绘制与生产双通道合成对照，48 个 RGB/Alpha 混合枚举组合、不同颜色/alpha 背景、HDR>1、彩色 K、fade 0/.5/1、未知组合拒绝及状态恢复。
- 原 Prefab GPU 30 项/84 帧通过；真实棋盘+英雄组件合成三幅图改用生产 RenderAffine，检查矩形外像素不改变。
- 666 Python（19.80 秒）、4101 既有 C#、77150 原资源/运行时检查通过。Windows 构建成功：work/build-native-affine.log。
- 日志 gpu-native-affine-math-verified.log、gpu-native-affine-prefab.log。既有两个启用槽的源程序 SHA 不变，没有启用其它特效。
- 14 个样本各 30 帧的 CPU 捕获提交时间已记录 mean_affine_ms/p95_affine_ms，最慢 p95 约 1.205ms；这不是 GPU 完整帧耗时，更不能当作正式 1080p 60 FPS 认证。

## 当前视觉差异

已查看 tests/visual/current/native-effects/CS2_062-board-components.png：彩色目标作用进入真实棋盘，但有明显深色环及过亮黄色区域。恢复正确的合成数学没有消除原材质/捕获/几何/生命周期缺口，不将它宣称为原地狱烈焰完成。此图只是离屏棋盘+英雄+特效组件图，不是完整正常客户端 UI 截图。

仍未完成：全卡目标与 Actor 绑定、全部原 Shader/脚本/Animator、全局 Bloom/Glow、原 SRP/平台变体和逐卡原版连续帧验收。双通道针对当前对角仿射绘制；原管线是否逐次采用 LDR 钳制/特定后处理未认证，不能以 HDR 数学等价替代整条原渲染管线认证。

未使用 Computer Use，没有修改官方客户端或用户 UI 校对。

后台双 Windows Player 完成一局：affine_1/2、端口 47354，14 回合/v45，Match 0cc4827a39264629bc976fde75a7f815。Shrink1 法师 9HP 胜、Shrink2 术士 -1HP 负，双方公开哈希 36ca93cd6ebc3f5ae87432aa88b7b07cfda55c25a57a436997df1d6f84c6d1bb 相同。队列峰值 1；不是人工整局，batchmode 不绘制普通 GUI。
