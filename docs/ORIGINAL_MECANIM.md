# 原 Mecanim 曲线与 Controller 增量

本节点修复原非 legacy Clip 导出为零时长/零轨道的缺口，未完成全卡原动画或完整 Shader/脚本。未使用 Computer Use，未修改官方安装，未覆盖用户校对。

## 原始来源与恢复内容

BT_035/BT_175 共用同一原 Prefab。三个原 Clip 为：

| 原 Clip | StopTime | 已解码轨道 |
| --- | --- | --- |
| GenFX_BuffImpact_DemonHunter_Small_Anim_Minion | 1.5 秒 | 9 个 scale 分量 + 3 个 _AlphaMul |
| GenFX_BuffImpact_DemonHunter_Small_Anim_Weapon | 1.25 秒 | 同上 |
| Bacon_Illidan_BuffImpact_DemonHunter_VerySmall_Anim | 2.5 秒 | 同上 |

Controller PathID 4545653275405446762，MinionBoof/WeaponBoof/BaconBoof 三个直接叶状态按原 TOS、ClipID、速度、循环和偏移绑定。三个 Animator 共享这个 Controller，因此审计为 9 个节点状态绑定，不是 9 张完成卡。Clip CAB/Bundle/PathID、缓存身份、属性与时长见 [native-animation-audit.json](../manifests/native-animation-audit.json)。

`tools/native_animation.py` 解码原 StreamedClip 的 uint32 数据、帧/通道及 cubic 系数，恢复入切线，独立保留进入/离开 step；负 FLT_MAX 预滚值与正 Infinity 尾帧不作为实际关键帧。DenseClip 按原开始时间和采样率恢复线性样本，ConstantClip 保留原常量和 StopTime。Transform 绑定按原类型占用 3/4 个分量，路径 CRC32 相对于原 Animator 根解析。

格式及算法核对了 [AssetStudio AnimationClip.cs](https://github.com/Perfare/AssetStudio/blob/master/AssetStudio/Classes/AnimationClip.cs) 与 [ModelConverter.cs](https://github.com/Perfare/AssetStudio/blob/master/AssetStudioUtility/ModelConverter.cs)。本项目保留 Unity 坐标，不使用其 FBX 轴转换；实现自行编写，没有复制原仓库代码。

材质 customType 22 的属性不是完整字符串 CRC32：低 28 位为属性名 CRC28，高位为标量/向量/颜色分量标记。按 [AssetRipper CustomCurveResolver](https://github.com/AssetRipper/AssetRipper/blob/master/Source/AssetRipper.Processing/AnimationClips/CustomCurveResolver.cs) 的格式核对，原 2369216915 对应材质实际声明的 `_AlphaMul`。只匹配唯一声明，不反向猜名称。这里只参考格式，自写解析，没有复制该仓库实现。

`NativeEffectInstance` 的 AnimatorPlayAction 只播放已解析的直接状态；原静态 IL 为调用 Animator.Play 后立即 Finish，不等待 Clip，不让服务器等待动画。客户端时间推进和回退重播通过原 MinionBoof 节点的实际 scale 检查。

## 明确未完成

- 原 `_AlphaMul` 已解码，但当前兼容 Shader 没有其原始程序。测试曾尝试检查材质接收值并失败（work/native-mecanim-material-render.log）；没有通过添加一个无实际运算的属性掩盖问题。最终运行仍跳过未声明的 Shader 属性。
- 不支持完整 Animator 的 BlendTree、自动状态转移、多层混合、参数驱动、Mirror、Avatar、根运动、WriteDefaultValues 全语义、IsNone 负 Infinity 继续播放语义；这些不可称完整恢复。
- 未恢复 PPtr/序列化引用、任意自定义属性、CompressedRotation 等完整曲线格式。
- 本节点只重新导出两个卡槽共用的一个 Prefab。254 个缓存共 346 次 Clip 引用，其中仍有 139 次零轨道，不能假定旧缓存已全面更新。审计显示本节点 3 次 Mecanim、36 轨道、0 未识别绑定；0 仅指这一已解析范围。
- BT_035/175 的主体仍缺失，黑遮罩未合格，继续禁用。只恢复了数据和部分真实播放，没有增加完成卡数。原版连续帧认证未完成。

## 复现与验证

```powershell
.\.venv\Scripts\python.exe -m tools.native_effects --ids BT_035,BT_175
.\.venv\Scripts\python.exe -m tools.native_animation_audit
.\.venv\Scripts\python.exe -m pytest -q
```

最终 Python 641 项通过（37.87 秒）；33 项为 native animation/native effects 定向测试，覆盖原 cubic 采样、独立 step、预滚、截断、通道顺序、CRC28/路径和直接状态限制。Windows 构建 work/build-native-mecanim-final.log Result: Success，4101 条既有 C# 断言、76976 条原数据/行为断言另计；本节点增加 6 条行为检查，包含真实原 Prefab Animator 播放和倒退重播，不把 76976 全称新增测试。

初次构建端口 47334 后台双 Windows Player 完成 14 回合/v48，MatchId afc0bf1b7a44474eaea4a97551ed0bd6：Shrink1 法师 -3HP 败，Shrink2 术士 3HP 胜，公共哈希 509bd1ac91b74988b05b194665561431209e230cf8ed5c140f35839c7df0025e。batchmode 不绘制正式 GUI，不代表人工实机动画或 1080p 性能验收。

最终构建端口 47336：12 回合/v39，MatchId e7c8d1322cc346738b0f19e565561e03，Shrink2 术士 20HP 胜，Shrink1 法师 -1HP 败，双方公共哈希 4079b251164bba8fec8fbb6ef79851c6f17981bcd1f41bd4ea4cedb4589f1545。队列峰值均为 1；后台约 60FPS 不算正式渲染性能。两局日志未发现运行异常。

work/native-mecanim-final-render.log 原 Prefab GPU 检查 26 条通过、66 帧截图。最终 BT_035/175 SHA256 fe14f7b327e4c915c093ec2b40696cea0c887654d275e3f48bd2dcda69d828dc，仍只有 308 亮像素、808 黑 alpha 像素，继续禁用。奥术爆炸/地狱烈焰的启用缓存和 SHA256 未改变。其他 Shader 数学 GPU 检查本节点未重跑，不把历史 649 条算本轮结果。

用户校对 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。全卡目标继续，优先处理原 _AlphaMul 程序和旧零轨道缓存。
