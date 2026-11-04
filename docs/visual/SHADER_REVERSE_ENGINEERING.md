# Shader 逆向方法与证据

目标：相同输入、状态和等价数学行为产生一致输出；不要求恢复开发期原文本。全部源码逐字恢复未完成，也不是验收前提。

## 来源等级

- 【直接恢复】：Shader 名称/Properties、材质值、Stage/Keyword、编译字节、渲染状态等源数据。
- 【行为重建】：由 DXBC/静态行为和原输出推导本项目等价 Shader；同时用 Confirmed/Observed/Inferred 等标注事实可信度。
- 【推测】：未对照的数学或语义；必须给待验证方法，不混入 Confirmed。

记录 Shader 名、Pass/变体、纹理槽、Properties/Keywords、RenderQueue、Blend/BlendOp、Cull、ZWrite/ZTest、Stencil、Alpha Clip、UV/ST/Panner/Noise/Mask/Dissolve/Distortion/Fresnel/Emission/Ramp/Vertex/Screen Space；没有来源写 Unknown，不填常见默认值。

## 已有方法与真实边界

`tools/inspect_compiled_shaders.py` 用 D3DDisassemble 静态分析原 DXBC；`particle_alpha_shader_audit.py`、`smoke_source_textures.py` 与现有清单恢复编译程序、材质和原采样。未执行原程序集。

两例见 [ORIGINAL_SMOKE_SHADERS](../ORIGINAL_SMOKE_SHADERS.md)：

- Hero/Particle/FX Particle Additive Alpha 2Layer：family23；噪声坐标由顶点 R/A 和原速度/缩放驱动，RGBA SrcAlpha/One；原程序不读取时钟，不得改成按时间滚动。
- Custom/FX/FX_SneakySmoke：family24；MainTex.r×vertexAlpha×Color.a 的 mask，RGB 在两色间不截断插值，RGBA SrcAlpha/OneMinusSrcAlpha。

这两项是【行为重建】，编译状态/原纹理身份是【直接恢复】。8 原纹理 / 17 属性绑定恢复 raw/mip/sampler；79 局部 GPU 检查不能证明整卡场景光照/合成/后处理已还原。Custom/FX/FX_SneakySmokeParticle 等缺口仍需研究。

每次报告分 Source Recovery / Behavior Recovery / Visual Accuracy，不将原 Shader 同名或单公式通过当作 V4/V5 整效果。TASK-0004 用完整双卡关键帧验证方法，必要的独立 Shader 缺口再立 Task，不吞进无边界大任务。

2026-10-16 TASK-0004：family24新增可追溯审计，重读2个原编译阶段/参数与2材质来源，7个实际浮点GPU样本最大误差5.96e-8；对应Shader/材质bundle、原状态、属性/常量/采样器与输出入[机器审计](../../manifests/task-0004-shader-audit.json)。这是执行者的局部行为证据，独立V004尚未进行；完整官方双卡序列实际缺失，Task BLOCKED，不升级整卡。见[本轮证据](TASK4_EVIDENCE.md)与[正式工作流](VISUAL_REVERSE_ENGINEERING_WORKFLOW.md)。
