# 原单纹理溶解与无纹理遮罩

2026-10-10，接续 558d126。完整全卡动画、原 Shader/脚本目标仍未完成。

## 源身份与运算

两者来自只读官方 essential_base_global-shader-0.unity3d，CAB-c394926ba464822396a05cbfae08a8b3。

Effects/AddMulDissolveSingleTex 的 PathID 为 4286994341503254994。原 VS/PS 为条目 1/3，参数块 0/2。VS 使用 MainTex_ST；vertex.rgb = color.rgb × Color.rgb × Alpha × Intensity，vertex.a 保持原顶点 alpha。Color.a 不参与计算，也没有环境光计算。

PS 只使用纹理红通道 r。edge=min((r+vertex.a-1)×DissolveIntensity,1)，最后输出 saturate(r×edge×vertex)。上限 min 不能提前替换成 saturate：负 gain 的结果也不同。Alpha 是 RGB 倍率，不是输出 alpha 倍率。原 Blend 为 One/OneMinusSrcAlpha，Cull Off、ZWrite Off。

Utility/Black 的 PathID 为 5031480652861743436。原基础 PS 常量输出 (0,0,0,1)，One/Zero、Cull Back、ZWrite On。无纹理依赖。原 Shader 参数类型 1 是空片元参数记录，现已通过有边界解析识别，不再依赖全 blob DXBC 搜索；参数/GPU 条目仍明确分开。

NativeEffectInstance 分别绑定家族 10/11，原声明属性仍过滤旧字段。Black 和已支持的 Custom/UnlitColor 无需纹理即可绘制，不再把无纹理常量 Shader 当资源缺失。原捕获排序、隐蔽子树和原材质混合保持现有流程。

复现：tools.inspect_compiled_shaders 指定上述 bundle，分别匹配 `%AddMulDissolveSingleTex%` 和 `Utility/Black`；再执行 `.venv\Scripts\python.exe -m tools.dissolve_shader_audit`。公开 manifests/native-dissolve-shader.json、native-black-shader.json 保存原阶段/参数 SHA、命名偏移及渲染状态，不包含官方二进制。

## 验证与边界

- 667 Python（39.68 秒）、180 Shader GPU 通道检查通过。新增 24 溶解样本通道及 4 Black 输出通道，覆盖红通道、顶点 alpha、颜色 alpha 无效、edge 上限、负 gain、Alpha 只改 RGB。
- 77166 原资源/运行时检查通过，真实地狱烈焰两个溶解材质绑定及原参数、两个黑色遮罩无纹理可渲染、剔除/深度状态均检查。
- 30 Prefab GPU/84 帧通过，Windows 构建成功，4101 既有 C# 检查通过。日志 native-dissolve-shader-checks.log、native-dissolve-prefab-checks.log、build-native-dissolve.log。
- 487 仿射 GPU 检查通过；新构建 dissolve_1/2 在端口 47358 完成 10 回合/v31。Shrink1 法师 14HP 胜，Shrink2 术士 -1HP 负，Match 2244a6265e5b46a18bab149778c0f034。双方公开哈希 7b16f5c479d82b10fe39432828570e211129cdee2205514c5e757b586887dd05 一致，队列峰值 1。后台模式不绘制完整 GUI，不能代表人工对局或正式 1080p 性能认证。

当前两类 Shader 出现在 21 效果槽、15 独立 Prefab 中，仅其中地狱烈焰槽已启用。全项目启用槽仍为 2，不能称 21 张卡动画完成。

实际查看 CS2_062-board-components.png 及 CS2_062-2.00.png：黑色遮罩影响捕获边缘，中央仍明显黄色过曝，2 秒样本仍有明亮地面。没有凭任意降亮度修改源参数，也没有宣称原版外观一致。

还发现 FX_HellFireWipe2 原 Clip 长约 6.033 秒，DissolveIntensity 轨道从 0.833 秒延伸至 6.017 秒；当前启用区间仅 1.2 秒。Clip 完整生命周期、原触发结束与自然退场尚未认证，不能把现区间称完整原动画。其它材质、全局后处理、目标/Actor/Animator/PlayMaker 和正式完整 UI 连续帧验收仍未完成。

本轮没有 Computer Use，没有修改官方安装或用户校对数据。离屏组件图不是完整客户端 UI 截图。
