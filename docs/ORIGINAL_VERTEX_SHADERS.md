# 原英雄遮罩与顶点光芒 Shader

2026-10-12。全卡原专属动画、完整原 Shader/脚本未完成。

## 来源

两者均从 essential_base_global-shader-0.unity3d、CAB-c394926ba464822396a05cbfae08a8b3 读取原 D3D 编译阶段与参数记录，只静态反汇编：

| Shader | PathID | family | 缓存程序 | 独立材质 |
|---|---|---|---|---|
| Custom/FX/FX_HeroMask_VertColorOnly | 5264888736503454533 | 21 | 28 | 1 |
| Effects/FX_AdditiveVertColor | -64755960916519659 | 22 | 24 | 2 |

数量包括普通效果和启动效果缓存，不是已完成原动画的卡牌数量。`python -m tools.vertex_shader_audit` 可重建来源记录并逐一回读原材质 Shader 指针、编译阶段/参数哈希及状态；保存 native-vertex-shaders/source-bindings manifest。

## 执行恢复

HeroMask 顶点输出 `1 + vertex.a*(vertex-1)`，像素直接输出插值颜色。RGB和alpha均 Src=Zero、Dst=SrcColor；写深度、背面剔除、LEqual。它没有纹理和材质颜色采样，不能用遗留的 _Color tint 重现。

AdditiveVertColor 顶点输出 `2*vertex*Color`，像素直接输出插值颜色。Src=SrcAlpha、Dst=One，关闭深度写入和剔除，LEqual。其原图捕获合成保留已有目标透明度的项目表示，完整原场景后处理仍未恢复。

两个分支绕开粒子光照与纹理采样；没有原指令未声明的 saturation。它们被识别为无需纹理的可渲染程序，修复此前以“没有纹理依赖”过滤的问题。仍保留原 Renderer.enabled，未强行打开真实 Prefab 中默认禁用的mask/glow来伪造完整画面。

## 实测

work/native-vertex-shader-gpu.log：284 Shader GPU 通道检查及56原Additive检查通过，退出0。新增16通道检查覆盖两种公式、忽略纹理和遗留参数、真实目的色调制/alpha、加法源贡献。

work/native-vertex-bindings.log：16个实际 OffScreenRenderFX/材质绑定检查与既有执行链通过，退出0；检查真实启动Prefab两材质family、无纹理可见性条件、遮罩深度/剔除/alpha以及原禁用状态。work/native-vertex-effects-gpu.log：41项效果GPU检查通过，退出0。

Python常规组1174 passed/4 deselected/22 warnings/148.50秒。四项此前通过且未修改的专属定位/区域/启动/RGBA来源重读未在本命令重跑；没有宣称本次全量同命令通过。

work/build-native-vertex.log Windows构建退出0，原Mixer九编译常量一致。UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。没有ComputerUse，官方安装只读。

最终构建vertex_shader_1/2后台47450完成923dac443e43495cbcb6550e64ee33a2，15回合/version50。Shrink1法师21血胜、Shrink2法师-1血负，两端public_hash均e62381e1edaa2cb537af57f7d013e90393cb296a35512d6a41535763ef31d060；日志无Exception/fallback/Error。命令行与终局确认后只清理自有17664/18684/8712。后台不绘制GUI，不代表人工/视觉整卡验收。

## 未完成

目标Card/英雄原画绑定、HeroMask/Glow实际激活路由、原场景深度排序与后处理、整卡连贯播放未完成。正式审核启用仍只有此前两项。独立Prefab截图不能证明完整原卡动画一致。
