# 原Hero/Additive/Additive基础与Glow程序

2026-10-12。本节点恢复真实高频Shader家族，全卡原专属动画、完整原Shader与脚本仍未完成。

## 源程序与依赖

只读材质GenFX_FelAE_Embers解析Shader依赖，首次源位置为initial_base_global-41caf0cf-material-2.unity3d / CAB-eafd1cdad45fcefbd0ab155f627ea3d7 / PathID-666151408197827071。它是嵌入材质Bundle的Shader，不能仅搜索essential_base_global-shader-0得到完整来源。

manifests/native-additive-variants.json记录基础VS/PS条目1/3、Glow VS/PS条目4/6、参数块0/2/5的原字节SHA256、常量偏移、混合/深度/剔除状态及147源程序404材质引用。manifests/native-additive-source-bindings.json进一步逐一解析100个唯一原材质到12个CAB内的Shader，核对四阶段程序字节、参数块、阶段绑定与渲染状态完全一致。首次只要求CAB相同的检查拒绝了复制资源；后续比较实际编译字节并保留所有不同CAB身份，没有因同名便假定等价。

复现：`python -m tools.inspect_compiled_shaders --bundle initial_base_global-41caf0cf-material-2.unity3d --pattern Hero/Additive/Additive`，然后`python -m tools.additive_shader_audit --verify-source`。编译原字节、反汇编与解析数据仅缓存到忽略的local目录，不执行官方程序集，也不修改官方安装。

## 原运算

基础VS使用MainTex_ST，顶点颜色为2*vertex*_TintColor。RGB再乘原绝对SH L0的gamma转换与MainLightColor合成，按LightingBlend混合；alpha不乘环境光。基础PS使用GlobalMipBias采样，直接输出纹理RGBA乘插值后的顶点颜色，不做saturate、alpha减法，也不使用旧Intensity、ColorMul或AlphaIntensity属性。原RGB与Alpha混合均SrcAlpha/One，Cull Off、ZWrite Off、ZTest LEqual。

Glow层不使用基础_TintColor；它使用_Color.a、GlowColor/GlowIntensity/GlowAddMul及原亮度运算，混合One/One。旧兼容实现曾将_Color与_TintColor相乘，从而让_Color.a=0的部分原材质消失；现在基础与Glow分别恢复，Shader family=16，并接入既有独立HDR Glow捕获与仿射合成。404引用不是404张已完成动画。

## 验证

1148项Python完整回归通过（6项UnityPy版本回退警告）；最后增加原渲染状态比较后，4项该家族测试重新通过。24项新浮点GPU数值检查覆盖未截断1.08亮度、忽略遗留增益和_Color、真实SrcAlpha/One四通道混合、零alpha保留目标及ST采样。既有236项Shader GPU检查通过；Glow最终104项通过，包含基础_TintColor.a=0不影响原Glow输出。78178项原运行时检查包含实际缓存材质绑定family16，41项Prefab GPU检查146帧通过。

日志：work/native-additive-all-gpu.log、work/native-additive-glow-final-gpu.log均退出0。Windows构建work/build-native-additive.log成功；构建后实际混音器Bundle9组编译常量再次核对通过。Scene生成ID归一；个人UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

新后台双客户端additive_1/2在47423端口完成比赛e814d933550446b894b655af7e5deb4a：16回合、version54，Shrink2术士6血胜、Shrink1法师-4血负，双方public_hash均170a1ade44e98ad35512db7ff79e96c7a6efda0d0c13537522e349af4fd57eaa。日志无异常或fallback；后台测试card_render_count=0，不声称人工比赛、新效果完整画面或1080p帧率验收。

## 当前差异

已查看实际离屏CS2_062-0.30.png：橙色火环可见，但中心黄面仍明显过亮、普通PNG捕获有截断；原全局光照、后处理及场景合成仍不完整。没有将截图调暗来伪造原Shader参数，没有自动增加启用范围。正式整效果启用仍2。全卡专属效果、全部Shader/平台变体、原脚本及角色附属可见性联动：未完成。本轮未操作桌面。
