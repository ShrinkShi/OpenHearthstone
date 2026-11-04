# 原 Particle Multiply 程序恢复

2026-10-10，接续 c7eda74，继续全卡原专属动画、完整原 Shader/脚本目标。本节点恢复一个实际 Shader 家族，不把引用数量当作卡牌动画完成数量。

## 原资源证据

冰霜新星 GenFX_Frost_WipeMotes 材质：essential_base_global-material-0.unity3d / CAB-3563ba8b89db13ca6eb196aa111591f1 / PathID -2269162697920647822。其 Shader 指针解析到 Hero/Particle/Particle Multiply：essential_base_global-shader-0.unity3d / CAB-c394926ba464822396a05cbfae08a8b3 / PathID -5773009347599192354。

Windows 原编译 Base VS/PS BlobIndex 1/3，Glow VS/PS 4/6；参数块 0/2/5。真实 programs=4、parameter_records=3，无解析错误。来源和编译 bytes 的 SHA256 见 [native-multiply-variants.json](../manifests/native-multiply-variants.json)。完整 bytes/反汇编仅在忽略的 local/compiled-shader-audit，不执行原官方程序集。

原 Base 混合为 RGB/Alpha 均 Zero / SrcColor，Cull Off、ZWrite Off。Glow 为 One / One，独立 GlowTransparent。不是普通 SrcAlpha 透明粒子。

## 实现

VS 使用原 MainTex ST；顶点颜色乘 Color 与 Intensity，RGB 再乘绝对 SH L0 的 gamma 转换及 LightingBlend。没有原主灯加法，不使用遗留 TintColor。

PS 令 texture=T、插值后顶点颜色=V、weight=T.a*V.a；输出四通道 `1 + weight * (T*V - 1)`，再由原 Zero/SrcColor 混合乘到目标。Alpha 也使用原四通道运算，不套亮度透明近似或 AddMultiply alpha 减法。

NativeEffect 的 family 7 与 CommonFX 5/6 分开。原内部 RenderToTexture 使用真实原混合，保持旧透明捕获后的材质状态恢复。原同一 Glow 程序接入已有独立 HDR 通道，忽略 Base Intensity 与 RGB tint。

当前缓存 **44 个 Prefab / 61 次材质引用** 使用这条路径，源身份逐项记录。该数量只证明绑定覆盖。没有新增启用卡；既有两个正式槽保持原程序 SHA。

## 编译块解析修正

Multiply 的 Blob 2 是 52 字节、kind 2 的短片元参数块；旧分类只接受 kind 3/4，导致读取失败并退回整段 DXBC 搜索。现按 Windows modern header 与独立条目边界区分 kind 2/3/4，参数不作为 GPU 程序，不跨条目读取。

重新读取现有 Additive、Additive+1、AddMultiply 时同时纠正旧程序数量：分别 4/4/2 GPU 程序，3/3/2 参数块；没有把参数块中恰好出现的下个 DXBC 计为新程序。其它未重新核对的历史审计不能自动视作正确。

## 真实验证

- Python 全套 666 项通过（40.40 秒），其中 kind 2/3/4 块边界分别检查。
- 原 Shader GPU 数值 128 项，新增 20 项：RGBA 乘数、二次 opacity、ST、Zero/SrcColor 实际目标混合、零透明保留目标、忽略遗留 tint/未声明属性。
- Glow GPU 76 项，新增 8 项：Multiply 原 Glow 输出及排除 Base Intensity。
- 既有 C# 4101 项、原资源/运行时 77150 项通过；包含真实冰霜新星材质 family 与原 RGB/Alpha blend。
- 原 Prefab GPU 30 项 / 84 帧通过。冰霜新星 5/6 可见、最大 49779 像素；地狱烈焰 5/6 可见、最大 28668 像素，两者黑 alpha 均 0。
- Windows 构建成功：work/build-native-multiply.log。GPU 日志分别 gpu-native-multiply-math/glow/prefab.log。

已查看 CS2_026-2.00.png 与 CS2_062-0.30.png：冰霜新星现在有蓝白整块表面，仍明显过亮、几何与目标场景不完整，继续禁用。地狱烈焰仍是偏亮黄面与橙色环，仅保留既有补充效果，不宣称全原版还原。

## 未完成

后续节点已将当前对角仿射 Shader 的彩色透射接入正式棋盘，见 [ORIGINAL_AFFINE_COMPOSITE](ORIGINAL_AFFINE_COMPOSITE.md)。以下缺口是本节点当时的记录；全管线/全卡认证仍未完成。

原内部 Shader 运算和 RGBA 混合已验证，但最终透明 board composite 只有标量 alpha，尚不能完整保存原 Multiply 的彩色目标透射因子。因此原 Shader Kernel 不等于整场最终合成完成。原全局 Bloom/Glow、全部 Shader/平台变体、目标捕获/动态 Actor、完整脚本和所有卡牌逐场景验收仍未完成。

没有使用 Computer Use、没有修改官方客户端；用户校对 SHA 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

后台双 Windows Player：multiply_1/2、端口 47352，Match b1526aecec2e47bd97fa253bed3fb184，16 回合/v48。Shrink2 术士 6HP 胜，Shrink1 法师 -1HP 负；双方公开哈希 d0d8ade4f92ca24ea5ecdc12a67023247c54ffab51bb161cfdb621a6dc173a3a 相同，队列峰值 1。是自动对局，不是人工比赛；batchmode 不绘制普通 GUI，不能证明正式 1080p FPS。
