# 原 FXWorld / FXScrolling 基础程序

2026-10-10，接续 77538c5。全卡原版专属动画、完整 Shader/脚本仍未完成。本页保留该节点证据；后续 Manual/Rotation/TwoLayer 及 Scrolling Glow 进展见 [ORIGINAL_EXTENDED_FX](ORIGINAL_EXTENDED_FX.md)。

## 原始证据

只读导出 essential_base_global-shader-0.unity3d 的原 Shader。CAB 为 CAB-c394926ba464822396a05cbfae08a8b3：

- Hero/AddMultiply/AddMultiplyFXWorld：PathID 4422613114386012427，基础 VS/PS 条目 1/3，参数块 0/2。
- Hero/AddMultiply/AddMultiplyFXScrolling：PathID -8682043857861559096，基础 VS/PS 条目 1/3，参数块 0/2；另有 Glow VS/PS 4/5，未恢复。

原 DXBC 静态反汇编与 m_CommonParameters 的命名字节偏移相互核对。公开 manifests/native-fx-coordinate-shaders.json 保存源身份、程序及参数 SHA、实际阶段绑定和常量偏移。原字节位于忽略的 local 缓存；没有执行官方程序集。

复现：先运行 tools.inspect_compiled_shaders，指定上述 bundle 和 pattern `%AddMultiplyFX%`；再运行 `.venv\Scripts\python.exe -m tools.fx_coordinate_shader_audit`。

## 恢复的运算

FXWorld 的主纹理使用原始 UV，忽略 MainTex_ST；FX 坐标使用透视除法之前的 clip.xy，乘 ScaleX/Y，再加 ScrollX/Y × seconds/20。旧兼容实现错误使用 world XZ、中心化 UV 和完整秒数。

FXScrolling 的主纹理使用 MainTex_ST；FX 使用原始 UV × ScaleX/Y + Scroll × seconds/20，忽略 FxTex_ST。两者顶点颜色乘 2 × Intensity × FxIntensity，Scrolling 额外乘 Color.a，World 不乘。环境光沿用原 SH gamma 与 MainLightColor 混合。

两者基础 PS 使用 GlobalMipBias，结果为 saturate(FX_RGBA × (Main_RGB × Color_RGB, Main_a × (Color_a - Main_a)) × vertex)。没有套用旧 TintColor 或兼容亮度 alpha。实例按精确 Shader 名选家族 8/9，原材质属性仍按原声明过滤；内部捕获和棋盘仿射合成沿用现有流程。

## 实际验证

- 152 Shader GPU 通道检查通过，新增 24 项：clip/raw UV、透视除法前坐标、主纹理 ST、忽略 FX ST、独立尺度、seconds/20、不同顶点 alpha 与输出钳制。
- 487 仿射合成 GPU 检查通过；30 Prefab GPU 检查/84 帧通过。
- 666 Python 通过（42.20 秒）；Windows 构建成功，4101 既有 C# 检查、77156 原资源/运行时检查通过。新增 6 项检查真实地狱烈焰材质选择家族 8/9。
- 日志：work/native-fx-shader-checks.log、native-fx-affine-checks.log、native-fx-prefab-checks.log、build-native-fx.log。

已查看 tests/visual/current/native-effects/CS2_062-board-components.png：原先实心深色双环消失，火环内部透出棋盘；中央黄色区域仍过曝。该图是实际原 Prefab 与棋盘/英雄组件的离屏组合，不是普通客户端完整 UI 截图，也不是正版同状态连续帧认证。

新构建后台双 Windows Player 完成一局：fx_1/2，端口 47356，16 回合/v51，Match fa32c7574ed44ebf9efcfe1e1844e2f1。Shrink1 法师 -1HP 负，Shrink2 术士 9HP 胜。双方公开哈希 ce5be899d79dc46d2daaa318a95946cb11c649d0f9837a3c579e135d7c7ecba3 一致，表现队列峰值 1。后台模式不绘制普通 GUI，不当作人工验收或正式 FPS 测试。

## 未完成

FXScrolling Glow、其他 FXScrollingManual/2Layer/Rotation、Effects/AddMulDissolveSingleTex、全局后处理与全部外部目标/Actor/生命周期仍未完成。实际原动画完整性和正式游戏帧率未认证。现有 352 效果槽仍只有 2 槽启用，没有把审计或程序家族数量计为完成卡数。

未使用 Computer Use；没有修改官方安装、用户校对布局或现有服务器/规则架构。
