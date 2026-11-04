# 原 iTween 参数和准星空白证据

2026-10-11。本节点恢复源参数和静态调用证据，尚未接入FadeTo/ScaleTo执行；不能认定准星已恢复。

## 原数据

`python -m tools.native_effects --refresh-itween-cache` 按既有精确层级校验刷新iTweenScaleTo/iTweenFadeToAction。40个Prefab、187动作、2573参数，未解析参数为0。首次40更新/0失败；第二次0更新/0失败。来源和每个owner节点在 `manifests/native-itween-refresh.json`。曲线、材质和启用标志未改变。

CS2_084与EX1_539共享FX_ReticleIdle1；Birth的目标节点Reticle为2。原动作先SetScale(.1,.1,.1)，再ScaleTo(1.4,1.4,1.4)、time=.8、speed=None、easeType=30；FadeTo alpha=1、time=.5、includeChildren=true、easeType=21。原材质GenFX_GlowRing3/GenFX_Reticle1的_Color.a均为0，因此没有淡入执行时仍为透明。当前Renderer确实未执行原FadeTo，不把它归咎于窗口亮度或任意提高材质亮度。

## 原程序集静态证据

扩展 `tools/export_script_il.ps1` 的ExtraTypes/ExtraAssemblies，并导出枚举常量。Cecil只读元数据/IL，未加载执行官方程序集。使用官方 `Blizzard.T5.iTween.dll`，同时读取动作基类和iTweenFSMEvents；当前静态审计142类型，restored=false。

关键IL SHA256：

| 类型 | SHA256 |
| --- | --- |
| iTween | 097fd7ac19d17e913ac92d3a26c394d41747475a8e72bdba8c60443cee24d057 |
| iTweenFsmAction | 88f56315a9e5a94e93fb65da2bfec18f1f593fadd7e405e88b30ea51aea5b31f |
| iTweenFSMEvents | 854599aac4ca9f11bc25d9bd4bab3c7e090c6e34cb404276aa892c0f8eb34a55 |
| iTweenFadeToAction | 73514a69d1de814b3668f9ed3f00e83c246c98c28e353a12997733a5054f9f18 |
| iTweenScaleTo | eda482aadbc1193e71a099ddd816fa0567d3125fbb1e3462addd135fb4255c6a |

枚举证明21=linear，30=easeOutElastic，而非通用EaseFloat的同号曲线。ScaleTo的transformScale有效时是其localScale加vectorScale；speed非None优先于time。FadeTo通过ColorTo执行，按原namedColor属性读材质颜色，仅改变指定alpha；子级调用与父级回调分开。time<=0走FadeUpdate并直接派发父级start/finish与Finish。

基类OnExit先注销回调ID；stopOnExit=None或true时按owner与itweenType停止，不能粗略停止全场动画。完整回调/循环和时间、材质作用域仍需原执行代码恢复，不把这些静态证据当成已实现。

## 验证和下一步

688 Python测试通过，55.30秒。CS2_025/CS2_062已启用缓存SHA保持此前审核值，不需要凭空重登记。正式C#未变，本节点没有重新构建或比赛；准星上一节点所有采样仍为黑并禁用。

下一步按原库恢复FadeTo/ScaleTo、原枚举曲线、子级材质写入、owner/type停止与回调，并用真实原准星前后截图验证；不得直接将源材质alpha改1当成完整动画。本节点未使用Computer Use，官方安装只读；完整全卡原Shader/脚本仍未完成。

后续执行进度：已接入原linear/easeOutElastic的时间驱动子集并验证准星变为可见，详细范围与剩余缺口见 [ORIGINAL_ITWEEN_RUNTIME](ORIGINAL_ITWEEN_RUNTIME.md)。以上原始源节点记录保留，不视作所有Fade/Scale分支现已恢复。
