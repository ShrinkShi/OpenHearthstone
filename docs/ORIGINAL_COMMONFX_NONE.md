# 原 CommonFX 无渐变 RGBR 分支

2026-10-12。全卡原专属动画、完整原 Shader 与脚本未完成。

原 FX/CommonFX：essential_base_global-shader-0.unity3d / CAB-c394926ba464822396a05cbfae08a8b3 / PathID-402334618889270515。本节点仅恢复关键词完全匹配 _COLORING_NONE、_DISSOLVE_NONE、_SECONDTEXTURETYPE_RGBR、_SECONDTEXTURE_NONE、_TEXTURETYPE_RGBR 的分支（family19）。原 Windows VS114/PS1202、参数块0/1151；Glow VS2240/PS2242及参数2239/2241。编译程序与参数字节哈希见 manifests/native-commonfx-none-variants.json。

## 源依赖与恢复

22原效果程序、55材质引用、12唯一原材质。逐一验证实际原材质关键词及其 Shader 指针；所有目标阶段和渲染状态与审计相同。没有凭缓存名推断版本，源依赖见 native-commonfx-none-source-bindings.json。

顶点保留原HDR颜色/AlphaMul变换和MainTex_ST/SwapUV。片元按照原程序用贴图红通道作为alpha：RGB=(texture.rgb+ColorAdd)*max(texture.r,PremultipliedRGBTexture)*ColorMul*vertex.rgb，alpha=texture.r*vertex.a。没有Ramp、第二贴图、环境光或Intensity；基础阶段忽略贴图保存的alpha。ClampUV与原MipBias继续保留。

_SrcBlendOutline/_DstBlendOutline是原基础RGBA混合属性。导出现在保存属性名，22缓存程序刷新；重复零更新。客户端读取当前材质属性，基础及仿射捕获同步，重播还原源值。其它CommonFX关键词分支没有借本节点套用这一公式。Shared Glow保留原独立程序。

## 验证

1156项Python回归通过，11项UnityPy版本回退警告保留。之后加强原材质关键词核对，12项相关源依赖回归再次通过。work/native-commonfx-none-final-gpu.log退出0：256既有/新增Shader浮点检查（本节点新增20）、56Additive/透明检查、120Glow、491仿射、78193原运行时检查、41Prefab检查146离屏帧。真实源CommonFX材质绑定、运行中混合属性变化及重播恢复都有检查。首次运行的测试错误地把其它_COLORING_NONE分支也当作family19；改为完整关键词集合后通过，没有放宽正式分支匹配。

NativeCommonFXNoneRenderChecks.Run从实际审计清单加载22个完整原Prefab，采样66帧，输出 tests/visual/current/native-commonfx-none。11程序有可见帧，11程序在默认上下文为空白；部分有明显黑alpha区域，最多65014像素。实际查看3cb07efcffc2d55dbd6283ec的一秒帧有红紫粒子，但仅是离屏局部，正常对战位置、目标合成和原后处理未验收。空白不是已解决；需继续原目标、Actor、生命周期与其余Shader。正式整效果启用数仍2。

Windows构建work/build-native-commonfx-none.log退出0，原混音器实际Bundle9组编译常量核对通过。个人UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。未使用Computer Use，未修改官方安装，未执行官方程序集。

新构建后台commonfx_none_1/2客户端在47426端口完成比赛9727db20bb4e4cdaa9742a1a0530b1b3：15回合/version52，Shrink2术士22血胜、Shrink1法师0血负。双方公共hash=d10195709f7bfb4639c6a73a9b1e936d87238d6baffb9be46b64354d8e020175，日志无异常/fallback。后台比赛不证明正常GUI完整效果或1080p FPS，不替代人工验收。
