# 原内部 RenderTexture 链路恢复

后续 Shader 数学、原 RGB/alpha 混合和最新验证见 [ORIGINAL_SHADER_PROGRAMS](ORIGINAL_SHADER_PROGRAMS.md)。本文件的116类型、620项等为该捕获节点的历史记录。

2026-10-10 增量，沿用现有表现队列。没有使用桌面操作工具，没有修改官方安装、服务器协议或用户校对。

## 来源与实际实现

冰霜新星 CS2_026 有两套原 RenderToTexture：隐藏 Offscreen 模型经过内部正交投影，结果绑定到 PlaySurface 输出网格。导出器按 CAB + PathID 解析 owner/source/target，不按同名节点猜测；完整原参数和组件身份保留在本地缓存。原 IL 静态审计现为 116 个类型。

已按原 RenderToTexture.CreateCamera/RenderTex 的 IL 恢复局部 X 轴 90 度旋转、13.96 × 7.33 捕获范围、350 × 184 纹理、-0.54 / 1.84 有符号裁剪、位置偏移、颜色清除、CommandBuffer 绘制和目标纹理绑定。只绑定目标自身的 Renderer，缺少时选一个子 Renderer，避免覆盖子粒子图集。

原 Animation/FSM 的可见状态独立保存。Offscreen Renderer 在最终棋盘相机中始终禁用，但内部捕获仍遵循动画可见状态。回退 seek 重放相同链路。内部材质采用原 src/dst 混合，Custom/UnlitColor 的常量颜色及深度写入得到恢复。

## Shader 证据与边界

`tools.inspect_compiled_shaders` 只读 Unity Shader 对象，保存原平台字节码、解析表、DXBC 指令、SHA256 和来源；调用 Windows D3DDisassemble，不执行官方脚本。当前审计 8 个 Shader、45 个平台程序，35 个 DXBC 程序成功反汇编。Custom/UnlitColor 有两条 UnityPy 解析错误，保留错误并从完整 DXBC 容器恢复两个程序。其他平台或空程序不能计为 DXBC 成功。

已据原 AddMultiply 像素指令恢复 `alpha = texture.a * (tint.a - texture.a)` 子公式。完整顶点光照、平台分支与所有混合输出尚未恢复。Hidden/R2TAlphaClip/Gradient/Bloom/Blur 的指令已导出，运行行为尚未接入。

`NativeEffectAlpha.shader` 是最终棋盘透明捕获的兼容处理：根据 RGB 限制捕获 alpha，清除内部黑色清屏残留。它不是官方 Shader。黑块检查曾失败，修复后未降低原检查阈值。

冰霜新星现在能显示真实原节点渲染，但颜色、冰霜形态、位置与原版仍不一致，因此保持禁用。GPU 非空和没有黑块不能作为原版还原成功。仍只有奥术爆炸、地狱烈焰两项经过此前画面核验的配置启用。

## 可重复检查

```powershell
.venv/Scripts/python.exe -m tools.inspect_effect_sources --ids CS2_026
.venv/Scripts/python.exe -m tools.inspect_compiled_shaders --card CS2_026
.venv/Scripts/python.exe -m tools.inspect_compiled_shaders --bundle '@player/resources.assets' --pattern '%R2TAlpha%'
.venv/Scripts/python.exe -m pytest -q
```

Unity 执行 VisualRegressionChecks.Run、BuildClient.Build；实际日志为 work/native-capture-regression.log、work/build-native-capture.log。620 项 Python 测试通过；4101 条既有 C# 断言通过；76925 条原数据/行为断言单独统计；490 项 GPU 检查通过，候选采样 48 帧。新 C# 检查包括两个真实捕获、带符号裁剪、目标纹理尺寸、子粒子图集保护及重放隔离。

图片在 tests/visual/current/native-effects。它们是 GPU 离屏组件图，不是正常 GUI 整屏或人工对局录像。性能仅覆盖特效路径，不能证明全游戏 60 FPS。

独立端口 47316 后台双 Windows 客户端使用最终构建和原两项启用配置完成一局：Shrink2 胜（9 HP），Shrink1 败（0 HP），16 回合 / v51，比赛 0999723187e24b7d96599722e9b1c6d7。两个 Profile 最终公共哈希均为 de31870807a44444d285dcfb695b7e62a52745b8383c7fe2dd1cd4e35f927827。batchmode 验证规则/协议/队列追赶，不运行正常 GUI 特效绘制；画面证据来自上述 GPU 检查。校对文件 SHA256 与前轮一致。

**未完成：全卡原版专属动画、完整原 Shader/脚本、原内部后处理/AlphaMesh/Stencil/Bloom、外部实体绑定、音频 FSM、人工逐帧核对。**
