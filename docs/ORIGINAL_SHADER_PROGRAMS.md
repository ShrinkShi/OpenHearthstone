# 原粒子 Shader 程序恢复

2026-10-10 在现有 NativeEffectInstance 上继续恢复。全卡原动画/完整 Shader/脚本目标未改变，当前仍未完成。

后续 Glow、环境光数学和取色动作增量见 [ORIGINAL_GLOW_LIGHTING](ORIGINAL_GLOW_LIGHTING.md)。下文旧验证数字按稳定节点保留，不视为当前全量验收。

## 本轮实际恢复

根据原 DXBC 和常量缓冲区参数索引恢复三个 Shader 家族的基础颜色/UV 运算：

| 原家族 | 已核对的基础运算 | 未恢复 |
|---|---|---|
| Hero/Particle/Particle Additive + 1 | 原始 UV；纹理 × 顶点颜色 × _Color × _ColorMul，包含 alpha；后续恢复环境光数学与独立 Glow 输出 | 原光照服务、全局 Glow 后处理 |
| Hero/Particle/Particle Additive | MainTex_ST；纹理 × 顶点颜色 × _Color × 2，包含 alpha；后续恢复环境光数学与独立 Glow 输出 | 原光照服务、全局 Glow 后处理 |
| Hero/AddMultiply/AddMultiply | MainTex_ST；顶点乘 2×Intensity；原 alpha 为 texture.a×(Color.a−texture.a)，随后饱和；后续恢复环境光与主光相加数学 | 原光照服务与平台分支 |

上述家族在现有缓存的 184 个不同 Prefab 内出现，共 904 条材质引用。这是数据覆盖范围，**不是已启用或完整还原 184 张牌**。

已修正保存材质的旧属性误用：例如冰霜 Ring_mesh 留有 _Intensity=12.5，而其原 Particle Additive + 1 未声明此属性。初始参数、重放、FSM 和 Clip 都检查原声明，避免旧值进入当前 Shader。声明包括编译参数表的隐藏 uniform，不能只查编辑器 Properties。冰霜新星重新导出了完整 uniform 名单；其他既有导出需后续按需刷新。

三个家族的棋盘合成改为保存 C + destination×K：加法粒子不累计背景遮挡，AddMultiply 保留原 source One / destination OneMinusSrcAlpha。内部 RenderTexture 使用原 RGB 与 alpha 混合状态，完成后恢复棋盘捕获状态。其他未恢复家族和内部最终 alpha 修正仍采用明确的兼容行为。

## 验证与可见帧定义

NativeShaderRenderChecks 的 48 条 GPU 数值检查使用冻结的 DXBC 运算样本，覆盖 RGBA、旧 Intensity 忽略、UV/ST、UnlitColor、带背景的加法/乘加合成。比较误差上限为 0.015，含 ARGB32 量化。

加法捕获允许 RGB 非零而 alpha=0，真实 NativeEffectComposite 使用 One / OneMinusSrcAlpha 会显示它。离屏候选检查据 RGB 能量计可见像素，黑色不透明遮罩检查阈值未降低。原 alpha>0 判定曾错误拒绝真实加法输出，已由带背景 GPU 合成样本证明并修正。普通 RGBA 图片查看器可能隐藏这些零 alpha 像素，应查看实际棋盘合成图；不能用 PNG 外观断定纯加法捕获空白。

本轮最终 VisualRegressionChecks 日志 work/native-affine-blend-regression.log：490 条既有 GPU 加 48 条新增 GPU，共 538 条通过；76928 条原数据/行为断言单独统计。Python 全量 621 项通过，20.01 秒。Windows 构建 work/build-native-affine-final.log 成功，4101 条既有 C# 断言通过。原脚本静态审计扩展到 119 类型，包括之前遗漏的 LightWeightCamera 嵌套类型。

原 LightWeightCamera/RenderToTextureUtils IL 确认使用 Matrix4x4.Ortho 与 SetViewProjectionMatrices；当前投影路径不额外猜测平台变换。原 RenderCommandLists 的捕获排序已在下述后续节点恢复。

本轮保留两项启用配置（奥术爆炸、地狱烈焰），重新核对其离屏棋盘图。冰霜新星仍有白色饱和、半圆形态及布局差异，保持禁用。没有 Computer Use，没有官方安装改动，校对文件 SHA256 保持一致。

最终构建在独立端口47320完成后台双 Windows 客户端比赛：Shrink1 法师败（-4 HP），Shrink2 术士胜（4 HP），14回合 / v45，比赛5c76d6098f294615992957659cc6bb51。两个Profile的最终公共哈希为7bd00cb8bccd4bcc995d5d262130eb1bcec17636e13092d94a9ef86b207b2f86，日志无运行异常。前一构建在47318完成13回合/v45比赛，最终公共哈希亦一致；该局早于最终混合修改。后台比赛验证规则/协议/队列，不执行正常GUI绘制，GPU证据与后台比赛分开统计。

**未完成：全卡专属分支、全部原 Shader/脚本、Glow/环境光/Stencil/Bloom、外部实体绑定和整屏人工逐帧验收。**

## 后续节点：原捕获绘制指令

依据 RenderCommandLists 原 IL，先选 SRPDefaultUnlit、UniversalForward、LightweightForward 各自第一个 Pass，缺失 LightMode 视作 SRPDefaultUnlit。GlowTransparent 被排除。renderQueue 小于 3000 属于不透明组，其余属于透明组；分别按 Renderer.sortingOrder、Material.renderQueue 升序，先画不透明再画透明。分界并非通常猜测的 2500。

导出保留原 Shader Queue 标签、队列偏移、材质自定义覆盖、各 Pass LightMode 和 Renderer sortingOrder。未知队列标签保留 null，不猜数值。奥术爆炸、冰霜新星、地狱烈焰三个主 Prefab 刷新了这些数据；其他缓存未全面刷新。旧缓存没有 Pass 元数据时仍明确采用兼容 Pass 0。

NativeEffectCapture 缓存绘制指令，逐帧重排并核对活跃/可见状态，明确 DrawRenderer 的 submesh 与 Pass。当前 Shader 只恢复了原基础 Pass 0；被选中的其他原 Pass 暂时跳过，不能把它们当基础 Pass 重复绘制。原材质替换、完整多 Pass、排序层、全部渲染状态仍未完成。

GPU 新增 25 条检查：LightMode 选择、重复模式只取首个、空列表不回退、重叠实体实际像素、3000 分界、排序值变更、隐藏实体和捕获后混合状态恢复。work/native-capture-order-regression.log 全部通过，共 563 条 GPU 检查。原数据/行为断言 76928 条另计；Python 全量 623 项通过（19.80 秒）。48 张诊断帧及三个真实组件合成图重新生成并人工查看：冰霜新星仍有白色饱和和半圆差异，维持禁用。原两项启用配置经新导出哈希复核后恢复，没有新增完成卡数。

Windows 构建 work/build-native-capture-order.log 显示 Result: Success，4101 条既有 C# 断言通过。独立端口 47322 的后台双 Windows 客户端完成 17 回合 / v64，比赛 91f41a9c7aa24d7ca16f3dbfb11a162f：Shrink1 法师败（-7 HP），Shrink2 术士胜（5 HP）；两个 Profile 最终公共哈希 04e26682ecb16c75dfbe7d98e0204ee50974bc83faae591505d146f029a93c45，日志无运行异常。后台对局未执行正常 GUI 绘制，与 GPU 渲染验证分开。测试进程核对命令行后关闭，原 37240 服务器未触碰。校对 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。
