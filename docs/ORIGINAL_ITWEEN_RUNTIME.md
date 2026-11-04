# 原准星淡入与弹性缩放执行

2026-10-11。接入了原库中已经核对的时间驱动分支，不代表完整 iTween 或全卡脚本恢复。

## 实际实现

`NativeITween` 单独使用原枚举：21=linear，30=easeOutElastic。弹性周期 .3、相位 .075，保留超过目标的回弹，首尾精确取源值/目标值。证据来自 `ORIGINAL_ITWEEN_SOURCE.md` 所列官方 iTween IL，未执行官方程序集。

`NativeEffectInstance` 接入 iTweenScaleTo 的 speed=None、transformScale=None、vectorScale/time 分支，以及 iTweenFadeToAction 的 Renderer `_Color` alpha 路径。保留原 RGB，在 delay 结束后捕获初始材质颜色/缩放；材质必须是原 Shader 声明的属性。includeChildren 收集原节点子树材质，源材质文件保持不变。

动作 startEvent/finishEvent 与开始/完成时间关联。退出状态注销回调，stopOnExit=true 的缩放按 owner/type 停止，false 的淡入继续到完成而不再结束先前状态动作。完成 tween 从活动列表退役；反向 Seek 从源材质和层级重放。Wait 的显式 finishEvent 现在有独立时钟，因此不再被同状态的循环 Animation 阻塞。

## 原资源与截图

CS2_084 与 EX1_539 的原 Reticle Birth：SetScale(.1)，ScaleTo(1.4)/.8 秒/原曲线30，FadeTo alpha=1/.5 秒/原曲线21/includeChildren=true。两个原 ring 材质初始 alpha=0；以前 10/10 帧为空白，接入后各 10/10 可见，max_pixels=1135，unbound_itween_actions=0，completed_itween_actions=2。

当前截图在 `tests/visual/current/native-effects/CS2_084-0.30.png`、`CS2_084-0.60.png` 与同名 EX1_539 文件。已查看实际 GPU 输出，是小型绿色准星。离屏自动构图仍未恢复场上目标对象绑定，**这些槽位仍禁用**，不把准星可见记作卡牌完整动画验收。

## 明确未完成

- 此节点后续扩展了8个原easing及speed/transformScale分支，详见 [ORIGINAL_ITWEEN_BRANCHES](ORIGINAL_ITWEEN_BRANCHES.md)；其余25个原easing和loop仍未完成。
- time=None 的默认时间与 time<=0 的原 FadeUpdate 平滑语义。
- includeChildren=true/stopOnExit=true的子级独立时钟后续已恢复，详见上述分支记录。
- 原 UberText/Light 的淡入、其他 namedColor 路径。
- 多个同步 FSM 事件的重入分派、Unity 原 Update 与 Animation 冲突时的完整执行顺序。
- 原 SpellObject/目标角色绑定；准星尚未正式启用。
- 完整全卡原 Shader/脚本、全卡专属效果：**未完成**。

不支持的曲线和参数明确计数，不降级为线性动画。正式启用原 Prefab 的数量仍是2；本节点不增加启用数量，也没有修改用户 UI 校准。

## 验证

688 Python 测试通过（20.61 秒）。最终 Windows 构建执行4101既有 C#检查与78020原资源/运行时检查，包含18项新增 iTween/独立 Wait检查：源采样、回弹端点、淡入、退出停止、退出后继续、反向重放、未核对曲线拒绝、delay和finishEvent。离屏 GPU 41检查、146帧通过，含两个准星可见性/动作绑定检查。测试数量不是已恢复卡牌数量。

日志：`work/build-itween-runtime-final.log`、`work/native-itween-runtime-verified.log`。全屏 GUI、人工比赛、正式60FPS性能验收未进行，本节点未使用 Computer Use。

最终构建的两个独立 Profile `itweenfinal_1/2` 实际完成自动比赛：match `541a6f2b1b494adeb5afb9036dac40be`，16回合/v58，Shrink2术士2HP胜，Shrink1法师-2HP负，两端 public_hash `d9fc164032e1d96085a4bfdb9e99e61fb478a5f1cabea4ebf67f5dc032d9efbd` 一致，队列峰值1。仅后台自动回归，不作人工GUI性能验收。已按PID、路径、Profile和端口清理本节点测试进程，保留用户37240服务器。
