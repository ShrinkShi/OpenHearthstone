# 原手动滚动、旋转、双层采样与 Glow

2026-10-10，接续 67c8530。全卡原版专属动画、完整原 Shader/脚本未完成。

## 原证据

只读 essential_base_global-shader-0.unity3d，CAB-c394926ba464822396a05cbfae08a8b3。原 DXBC 静态反汇编与命名常量偏移交叉核对，没有加载或执行官方程序集。

| Shader（Hero/AddMultiply/ 前缀） | PathID | 本地家族 | 恢复 Pass |
| --- | --- | --- | --- |
| AddMultiplyFXScrollingManual | -2418971604062054018 | 12 | 基础 |
| AddMultiplyFXRotation | 3752617471019072207 | 13 | 基础 |
| AddMultiplyFXScrolling2Layer | -8673478984178380409 | 14 | 基础、Glow |
| AddMultiplyFXScrolling | -8682043857861559096 | 9 | 本轮补 Glow |

三种新增基础程序均为 VS/PS 条目 1/3、参数记录 0/2；两种 Glow 为 VS/PS 4/5。manifests/native-fx-coordinate-shaders.json 保存五个家族的源身份、程序/参数 SHA、阶段绑定，以及每个 Pass 的常量字节偏移。

复现：先用 tools.inspect_compiled_shaders 只读导出上述 bundle 的 `%AddMultiplyFX%`，再运行 `.venv\Scripts\python.exe -m tools.fx_coordinate_shader_audit`。原字节留在忽略的本地缓存。

## 恢复的差异

- Manual：主 UV 先应用 MainTex_ST，再乘 ScaleX/Y 并直接加 ScrollX/Y。滚动不乘时间；顶点颜色乘 2 × Intensity × FxIntensity，不额外乘 Color.a。原 PS 不钳制，保留 HDR 与负 alpha。
- Rotation：主 UV 忽略 MainTex_ST；FX 围绕 .5 + RotPivotXY 缩放，用 seconds/20 × RotSpeed 的弧度进行顺时针矩阵旋转，再加 .5。顶点额外乘 Color.a；原 PS 不钳制。
- TwoLayer：两组 UV 从原 UV 独立缩放、按 seconds/20 滚动。原 PS 第二采样读取打包 TEXCOORD1.yz，即第一组 Y 和第二组 X，而非第二组 XY。按原指令保留该行为；两个 FxIntensity 相乘，最终 saturate。
- Scrolling/TwoLayer Glow：核对为已有独立 Glow 程序的相同运算，复用本地 Glow Shader。使用主纹理 ST、原顶点色、GlowColor/Intensity/AddMul 与 Color.a；不套入基础层 FX 坐标、FX 增益或 Intensity。Glow 输出不等于完成了原全局 Bloom。

实例只按精确原 Shader 名分配新家族，原材质参数进入相应程序。服务器不等待 Shader 或动画。没有新增启用卡槽。

## 实际验证

- 672 Python 测试通过，49.63 秒。
- 224 Shader GPU 通道检查通过；浮点 RT 验证时间独立、旋转正负方向、HDR 大于 1、负 alpha、双层特殊坐标及 saturate。
- 92 Glow GPU 检查通过，新增两种家族实际捕获绑定，检查基础 FX 参数变化不污染 Glow。
- 487 仿射合成 GPU 检查通过。
- 77191 原资源/运行时检查、32 Prefab GPU 检查通过，生成 96 帧。截图文件名过滤 Windows 非法字符，报告仍保留完整源效果 ID。
- 日志：work/native-extended-fx-shader-checks.log、native-extended-fx-glow-checks.log、native-extended-fx-affine-checks.log、native-extended-fx-prefab-checks.log。

Windows 构建成功（work/build-native-extended-fx.log），4101 项既有 C# 检查通过。新构建后台双 Windows Player extendedfx_1/2、端口 47362 完成 14 回合/v43：Shrink1 法师 -5HP 负，Shrink2 术士 22HP 胜。Match 5d3a4a95d602475da17532be293db9d1；双方公开哈希 888b8568ebd19b808e6fad22345d51c2bc1868b74a1c3a7f9dafbd8c082307b0 一致，队列峰值 1。后台不绘制普通 GUI，其帧率不代表正式渲染性能。

实际图片已查看：EX1_180-0.30.png 是孤立的小紫色符文，六个采样可见、最大 1442 像素；EX1_044@m_TriggerEffectDefs_0-0.30.png 是小绿黄飘带，四个采样可见、最大 1503 像素。原 Rotation/Manual 材质家族映射已验证，但正确目标、几何尺度和完整整场效果仍未完成，两者保持禁用。TwoLayer 当前没有对应缓存卡牌材质场景，只有原源证据和 GPU 样本验证，不计作真实卡动画验收。

## 未完成

489 张现有牌、352 效果槽仍只有两槽正式启用。全卡原专属动画、完整 Actor/目标/所有权、Animator/PlayMaker、全部 Shader 变体、全局 Bloom、正式 1080p FPS 与原版同状态连续帧认证未完成。地狱烈焰中央过曝仍未解决。

未使用 Computer Use；官方安装、用户校对文件、现有服务器/规则架构没有修改。离屏检查与后台自动比赛不能代替普通 GUI 人工验收。
