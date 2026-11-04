# 原 Glow 与环境光程序恢复

后续 Distortion 节点扩展到第四个基础家族和第三个原 Glow 家族，验证总数及真实 Prefab 差异见 [ORIGINAL_DISTORTION](ORIGINAL_DISTORTION.md)。下文数字保留本节点的历史范围。正值 Glow 保存属性中，20条 AddMultiply 属于未声明残留值，4条 Distortion 和3条 AreaEffect 为声明属性；后者家族仍未恢复。

全卡专属动画、完整原 Shader/脚本仍未完成。本节点继续现有架构，不修改服务器或官方客户端，不使用 Computer Use。

## 原程序证据

| 原 Shader | Bundle / CAB / PathID | 程序 |
|---|---|---|
| Hero/Particle/Particle Additive + 1 | essential_base_global-shader-0.unity3d / CAB-c394926ba464822396a05cbfae08a8b3 / -230275896195282385 | 平台 4，vertex 1/4、pixel 3/6；Glow pixel SHA256 44189c42f62d1c12a473449e52f0027a03eb83d61902664d3b171ed71cfe9688 |
| Hero/Particle/Particle Additive | 同一全局 Shader bundle，PathID 240134070624251049 | 已核对 Glow vertex/pixel 运算及对应 uniform 偏移 |
| Hero/AddMultiply/AddMultiply | 同一全局 Shader bundle，PathID 3410991767739636778 | 基础 vertex 的环境光和 _MainLightColor 分支 |

源字节码、反汇编、parsed-form 留在 local/compiled-shader-audit；来源和完整哈希见 manifests/original-shader-audit.json。这里只恢复核对过的运算，不把 UnityPy 的伪 Shader 文本当可编译源码。

Glow 使用 energy=2×vertexColor×texture，brightness=dot(energy.rgb,0.333)，再乘 energy.a、_Color.a、_GlowColor.rgb、_GlowAddMul（仅 alpha）、_GlowIntensity 和 0.25。原 _Color.rgb 不参与 Glow；UV 使用 MainTex_ST，采样使用 _GlobalMipBias.x。原混合为 One/One。

环境光基础 vertex 使用 SH L0 常量绝对值，pow(x,0.416667) 后执行 saturate(1.055×x−0.055)，再用 _LightingBlend 插值。AddMultiply 额外加 _MainLightColor，且不在相加后重新饱和。alpha 不受这一环境光插值影响。

## 当前执行与边界

NativeEffect.shader 的三个基础家族接入原环境光数学和原 mip bias；Glow 参数参与已有 FSM/Clip 材质更新。NativeEffectGlowCapture / NativeEffectInstance.RenderGlow 提供独立 HDR 原 Glow 输出，逐帧读取原材质参数，保持源 Renderer、纹理 ST、排序和可见性。只接受已经核对的两个粒子家族及原 GlowTransparent Pass，不为缺失 Pass 生成光晕。普通 Render 不叠加这个输出。

**未完成：原全局 Glow 后处理、Bloom 合成和正常棋盘接入。** Raw Glow 不等于最终 Bloom。RenderToTexture 的 Hidden/R2TBloom 是另一路原组件流程，也未恢复。ScreenEffectGlow 的原 layer 23、完整渲染器特性和后处理顺序尚未接入，不能用提高基础亮度替代。

基础 Shader 从 Unity SH 和 _MainLightColor 全局输入读取光照。当前棋盘没有完整原光照服务；原主光和棋盘 SH 来源未恢复，不能称光照完全一致。现有两项启用特效的 LightingBlend=0，保留既有画面行为。

GetBoardAmbientColorAction 按原 IL 在进入时优先读取显式棋盘上下文，缺失时读 RenderSettings.ambientLight。GetAmbientColorAction 进入时读棋盘上下文，every-frame 更新时读 RenderSettings；开启 every-frame 时不自动完成动作。原 Board 构造函数明确将 m_AmbientColor 初始化为 white；正常 NativeEffectPlayer 因此提供白色默认棋盘上下文，保留显式颜色覆盖接口，离屏 Prefab 场景同样使用这一上下文。原 SpecialEvents.AmbientColorOverride 及完整光照服务未恢复。原 GetAmbient 在缺失 Board 时的异常行为没有复制。SetAmbientColorAction / ResetAmbientColorAction 仍未恢复，不修改全局环境光冒充实现。

静态 IL 审计包含 Glow、ScreenEffectGlow、环境光相关行为和 Board 的初始化证据。原脚本未直接加载或执行；清单 restored=false 保留。运算重实现和原二进制脚本执行分开记录。

## 验证

NativeGlowRenderChecks 独立 GPU 56 条检查覆盖原 Glow RGBA、tint RGB 忽略、动态强度/alpha、零输出、缺失 Pass、可见性、真实 mip level 采样、环境光转换/插值与 HDR 主光相加。原 Glow 样本误差上限 0.0003，环境光 HDR 样本 0.001，包含半浮点量化。数值来自原 DXBC 固定样本。

NativeEffectChecks 新增 6 条运行行为检查：棋盘默认值、棋盘优先、无棋盘 fallback、GetAmbient 进入/持续更新、backward Seek 重新进入。未将数据完整性断言或数值样本当成整屏原版逐帧认证。

全缓存 --all 刷新最终成功：352 效果槽、254 不同 Prefab、1879 粒子对象、96 捕获组件，零导出失败；313 依赖错误仍如实保留。所有缓存获得队列/Pass/uniform/捕获参数更新。两项原启用配置按新数据重新验证，其他效果仍禁用，没有新增完成卡数。

最终 work/native-glow-full-cache-regression.log：619 条 GPU 检查全部通过（563 既有 + 56 新增）；76934 原数据/行为断言另计。Python 全量623项通过，43.27秒（与Unity GPU检查同时运行）。Windows work/build-native-glow-full-cache.log 为 Result: Success，4101 条既有 C# 断言通过。静态原脚本审计124类型。

八个原 Prefab 的48个时间样本也执行独立 RenderGlow，glow_pixels 全部为零；不能把数值样本通过解释成这些卡已经获得新发光效果。两个已核对家族在全缓存中没有正值 _GlowIntensity 材质；另外27条保存的正值属于其他 Shader 家族，尚未验证其声明或 Glow 程序。原版后处理、全卡其他家族仍未完成。三个组件合成图重新查看，冰霜新星继续禁用；奥术爆炸和地狱烈焰仍是原范围内的有界接入。

独立端口47324，两个最终构建的后台 Windows 客户端完成11回合/v38，比赛e43ca2b22f6747209ecb69c619c5fbe1：Shrink1 法师胜（18HP），Shrink2 术士败（-1HP）。两个Profile最终公共哈希 c9af47137201fa688ff380753eb0e8d7acb2919cc1787c6bc8f3b609ae81d362，日志无运行异常。后台对局验证规则/协议/队列，不执行正常GUI绘制，与GPU证据分开。测试进程核对命令行后关闭，原37240服务器未触碰。用户校对 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。
