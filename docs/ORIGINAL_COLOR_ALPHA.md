# 原亮度遮罩粒子 Shader

2026-10-12。全卡原专属动画、完整 Shader 与脚本未完成。

原 Hero/Particle/Particles_Transparent_ColorAsAlpha 来自 essential_base_global-shader-0.unity3d / CAB-c394926ba464822396a05cbfae08a8b3 / PathID9166864898872631166。Windows 基础 VS/PS 为条目1/3，Glow为4/5，参数块0/2。五个唯一原材质的实际 Shader 指针、编译阶段与参数字节逐一核对；覆盖六个程序、九处材质引用。来源和哈希见 manifests/native-color-alpha-variants.json 与 native-color-alpha-source-bindings.json。

## 恢复的原运算

family18 顶点使用 MainTex_ST；颜色为 vertex*TintColor*Intensity，环境光采用绝对 SH L0 的 gamma 转换与 LightingBlend，没有 MainLightColor 加法。基础 RGB 为插值顶点 RGB 的两倍，不乘贴图颜色。alpha 为 saturate(dot(texture.rgb, (.3,.59,.11))*AlphaIntensity)*插值顶点alpha；基础阶段忽略贴图 alpha，先截断遮罩再乘顶点 alpha。RGB 不截断，Intensity 同时影响顶点 RGB 和 alpha。

Glow 使用原独立 RGBA 采样及 _Color.a，不能套用基础亮度遮罩或 TintColor。原基础 RGBA 混合为 SrcAlpha/OneMinusSrcAlpha，Glow为One/One。这里没有重写规则、服务器或个人 UI 校准。

## 验证与实际差异

`python -m tools.color_alpha_shader_audit` 只读官方资源并核对依赖；未执行官方程序集。1153项Python测试通过，9项UnityPy版本回退警告保留。work/native-color-alpha-gpu.log退出0：56项Additive/透明浮点检查（本节点新增20）、120项Glow、236既有Shader、491仿射、78187原资源运行时检查与41Prefab检查146离屏帧。

NativeColorAlphaRenderChecks.Run 对六个未经删改的原Prefab各采样0.3/1/2秒，18帧保存于 tests/visual/current/native-color-alpha。五个程序有可见粒子；Patchwerk飞刀三帧为空白，尚需原目标/生命周期绑定。霜之哀伤、死亡之翼、污染谷物等部分帧仍有黑alpha像素，不能计为整效果完成。实际查看术士SpawnToHand一秒帧：紫色粒子可见，但只有离屏局部，未验证正常手牌区合成、目标尺寸及原后处理。此节点没有增加完整效果启用数，仍为2。

Windows构建work/build-native-color-alpha.log成功退出0；实际原混音器Bundle的9组编译常量再次精确核对通过。个人UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6；Scene ID归一，没有操作桌面。

新构建后台color_alpha_1/2客户端在47425端口完成比赛d67686f6f5b642d2a64524f7532d7dda：11回合/version37，Shrink2术士11血胜、Shrink1法师-1血负。双方公共hash为d92ba49c89ddbddc6c245285ededcf6f21eed534f1e9f2f7794220784e1579df，日志无异常/fallback。后台比赛不计为完整UI人工验收或1080p性能证明。

完整原 Shader 的其他平台/变体、全部专属脚本与Actor联动、原后处理及全卡正式对战视听验收未完成。
