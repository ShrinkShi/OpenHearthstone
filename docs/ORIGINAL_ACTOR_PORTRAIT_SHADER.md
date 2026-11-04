# 原随从原画 Shader 与源环境光

后续已统一静态随从与原动画Actor的创建入口；本文关于静态路径未统一的段落为当时记录，当前见 [ORIGINAL_STATIC_ACTOR](ORIGINAL_STATIC_ACTOR.md)。

2026-10-11。全卡原动画、完整原Shader/脚本**未完成**。

## 原证据与恢复

原随从主Mesh槽0使用 `Custom/Card/Unlit_Desaturate_2Texture2uv`，Shader PathID=6322982158558142423，Bundle=essential_base_global-shader-0.unity3d。已解压原DXBC并静态反汇编，按无关键词版本的顶点blob1/参数blob0、片元blob3/参数blob2恢复数学。SHA256、源常量字节偏移保存在 `manifests/original-actor-portrait-shader.json`；不执行原二进制。

顶点保留两个独立UV/ST、原顶点色与SH常量项gamma转换、主光照输入、LightingBlend及_Color。片元按原顺序计算：第二纹理×SecondTint×BlendIntensity，以其alpha混合主图与乘积，0.3/0.59/0.11灰度在DarkColor/Color之间混色，Desaturate、Contrast/AvgLum，再取 `min(c,(c+0.15)*vertexColor)`，最后Intensity。RGBA均参与运算，不把alpha或过量混色简单截成0～1。纹理读取保留源GlobalMipBias输入。

此前场景只导出一个显示纹理及UV0，遗漏原SecondTex/UV1和有效Shader参数。actor_templates现在恢复源portrait MeshRenderer每个材质的全部参数/纹理/身份及Mesh第二UV，写回原缓存，未覆盖用户校准。NativeActorMaterial仅为已核对的普通随从原画Shader创建专用Material，仍保留未恢复材质的既有显示路径。

## 光照缺口的定位

初次原Shader离屏原画变黑，但边框可见，旧非空检查错误地通过。检查原Actor.SetPortraitMaterial IL证实ZonePlay使用LightingBlend=1，不能为通过测试而强行改成0。

读取官方globalgamemanagers的BuildSettings场景映射，Gameplay为level3；其RenderSettings PathID=3有非零27项原环境光探针系数。将其按RGB各9项恢复SphericalHarmonicsL2，以CustomProvided及MaterialPropertyBlock.CopySHCoefficientArraysFrom绑定到实际随从Renderer。没有修改全局RenderSettings，没有臆造一个白色灯补亮。来源数据保存在 `manifests/original-gameplay-lighting.json`，运行缓存为同名local/runtime文件。

修复后原画重新可见。双方实际召唤检查新增原画中心亮度检查，避免以后只靠边框判定成功。

## 复现

`python -m tools.inspect_compiled_shaders --bundle essential_base_global-shader-0.unity3d --pattern '%Custom/Card/Unlit%'` 静态恢复14种Card Shader审计（不代表14种已移植）。`python -m tools.actor_templates`恢复源材质/UV，`python -m tools.actor_shader_audit`恢复精确程序映射和Gameplay环境探针。Unity执行NativeActorMaterialChecks.RunGpu核对独立RGBA、UV2与光照输入；NativeActorRenderChecks.RunAll包括该项与真实召唤取景。

## 仍未完成

原URP运行时棋盘光照改变、主光选择、探针更新、Renderer全生命周期，边框/金色反射/影子等其余原Shader，全卡专属效果及完整Actor脚本仍未完成。当前仅恢复Gameplay初始烘焙探针，主光enabled与GlobalMipBias缺省为0，不能声称连续原版场景完全一致。用户UV校准是额外适配层，既有source数学没有伪造Shader来源。

本节点作用于NativeBattleActors借用的真实原Actor，包括双方原召唤及其清理尾段。动画外的旧静态MODEL_MINION捕获仍使用既有显示材质，尚未与真实Actor的Shader统一；该切换差异也属于未完成项，不宣称所有静态场上原画已经替换。

## 验证

最终28项专用Shader GPU检查通过：18组不同第二纹理强度/去饱和/对比输入，3组独立SH gamma/主光/顶点色混合，以及实际Actor Shader/UV2/源纹理/源强度/非零原探针绑定。105项实际双方召唤GPU检查/14阶段帧通过，包含新增原画中心检查，86项无GPU召唤检查保持通过。

704项Python通过（40.61秒），Windows构建及既有4106客户端/78172原运行时/34表/26Actor动画/29音频检查通过。完整GPU回归与iTween40程序/188动作/133生命周期/798帧0异常通过；完整GPU批次运行时Shader检查为25项，之后追加的3组光照数学独立GPU检查通过。原场景/声音人工验收与正式GUI 60FPS验收未做。

实际后台双Windows Player比赛：actorportrait_1/2、端口47399，13回合/v49结束，match_id `ccec4062686d4ca296df6ab3854c99f2`，公开哈希 `f33be2f8d07d0ae303eefc5c3be27572a86575b5ccefda3acbd6f37c0f586775`一致，队列峰值1。服务器结果Shrink1法师WON、Shrink2术士LOST，但最终双方生命均0；这是当前服务器裁决，不作为原版同时死亡/平局规则正确性的证据，本轮未改服务器规则。双端日志没有Shader error、Original summon fallback或Exception。

日志 `work/build-native-actor-portrait.log`、`work/native-actor-portrait-final-gpu.log`、`work/native-actor-portrait-light-math.log`。本轮测试客户端29760/14528及服务器24416按命令行核对清理；用户服务器10536保留。用户校准SHA256仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6，既有两项专属法术程序指纹不变。没有Computer Use，不修改官方安装，不执行原客户端DLL。
