# 原 Distortion Shader 恢复

后续目标类型节点恢复了BT_035/175的原分支发射，但主体动画和黑遮罩仍未合格，三项保持禁用；最新缓存哈希和验证见 [ORIGINAL_TARGET_BINDING](ORIGINAL_TARGET_BINDING.md)。下文保留本节点历史证据。

全卡专属动画、完整原 Shader/脚本未完成。本节点恢复一个已核对的基础 Shader 家族，不改规则、网络或用户校对，不使用桌面操作。

## 来源与执行

只读导出 essential_base_global-shader-0.unity3d 中 CAB-c394926ba464822396a05cbfae08a8b3 的编译程序：

| Shader | PathID | 当前状态 |
|---|---|---|
| Hero/AddMultiply/AddMultiplyDistortion | -8802441782308490694 | 基础 RGBA/UV、混合和独立 Glow 运算重实现 |
| Hero/AddMultiply/AddMultiplyDistortion2Layer | -6129736032622147059 | 仅导出审计，未恢复 |

两个 Shader 各有 6 个平台程序、5 个可反汇编 DXBC。累计清单为 10 个 Shader、57 个程序、45 个反汇编程序；哈希及来源在 manifests/original-shader-audit.json，字节码和反汇编留在 local/compiled-shader-audit。未取得原 HLSL 源文件，也没有直接执行官方脚本程序集。

第一家族按 vertex 1 / pixel 3 的原 constant-buffer 偏移恢复：

```text
baseUV = UV * MainTex_ST.xy + MainTex_ST.zw
mainUV = baseUV * ScaleXY + Time.x * ScrollXY
fxUV = baseUV * FxScaleXY + Time.x * FxScrollXY
offset = (sample(FxTex, fxUV).rg - 0.5) * FxTex_TexelSize.xy * FxIntensity
tex = sample(MainTex, mainUV + offset)
rgba = (tex.rgb * Color.rgb, tex.a * (Color.a - tex.a))
output = saturate(rgba * vertexColor * (2 * Intensity * Color.a))
```

采样使用原 GlobalMipBias.x；FxTex_ST 不参与这一路 UV 运算。RGB 使用上一节点恢复的环境光和主光分支。原基础混合为 One/OneMinusSrcAlpha，内部捕获暂用原状态，棋盘捕获保留已有预乘合成。原 Glow vertex 4 / pixel 5 使用未扭曲的 MainTex_ST 和原 Glow 运算，独立 HDR 输出，不把基础效果扭曲重复用于 Glow。

原 Time.x 为场景时间秒数/20；当前确定性重放使用表现年龄/20。原场景时间起点、完整光照服务及全局 Glow/Bloom 合成未恢复。这一差异保留，不能称完整原 Shader 管线一致。

## 实际 Prefab 与未完成项

第一家族出现在 17 个现有效果槽中。此数是材质覆盖范围，不是完成卡数。11 个 Prefab 共采样 66 个基础帧并独立读取 Glow；新增 BT_035、BT_175、BT_801 的诊断结果：

| 样本 | 有效基础帧 | 最大可见像素 | 原 Glow 输出 | 状态 |
|---|---:|---:|---|---|
| BT_035 | 0 | 0 | 零 | 保持禁用 |
| BT_175 | 0 | 0 | 零 | 保持禁用 |
| BT_801 | 3 | 504 | 零 | 保持禁用，未通过外观认证 |

前两个 Prefab 由 Death 事件进入 Get Target，再通过 SpellCardTypeAction 区分 Minion/Hero/Weapon；当前外部目标卡牌类型绑定未恢复，事件参数在缓存中 unresolved。没有强行显示隐藏节点来制造非空截图。下一步应恢复这一原分支的数据导出与实体绑定，并检查 Animator/材质纹理/可见性动作。原启用范围仍只有奥术爆炸和地狱烈焰，其他 350 槽禁用。

## 验证

新增 20 条基础 GPU 数值检查覆盖纹理尺寸偏移、独立 FX scale/scroll、主纹理 scroll、忽略 FxTex_ST、原额外 tint alpha；新增 4 条 Glow RGBA 检查覆盖这一家族独立未扭曲输出。数值依据原 DXBC，没有降低既有可见性或黑遮罩阈值。

work/native-distortion-prefabs.log：646 条 GPU 检查通过，76934 条原数据/行为断言另计。Python 全量 623 项通过（39.63 秒，与 GPU 检查同时运行）；Windows 构建 work/build-native-distortion.log 为 Result: Success，4101 条既有 C# 断言通过。GPU 样本不等于全卡、正常 GUI 或原版同状态逐帧认证。

端口47326的两个后台Windows客户端完成18回合/v57，比赛9df97458b0ad44f28b926d501b053579：Shrink1法师胜（1HP），Shrink2术士败（0HP）。两个Profile最终public_hash均为b49671239f397338b18fee0d40eb9b0f2071681525f47672ef82369031311ec8，日志未发现运行异常。后台对局不执行正常GUI绘制，与GPU证据分开。测试进程核对命令行后关闭，原37240服务器未触碰。用户校对SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。
