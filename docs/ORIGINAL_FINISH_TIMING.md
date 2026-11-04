# 原完成通知、视觉退场与命名动画时钟

2026-10-10，接续 86cdf28。全卡原版专属动画、完整原 Shader/脚本目标仍未完成。

## 原证据与上一轮判断修正

只读 Cecil IL：AnimationPlaythroughAction.OnEnter 的 IL_0079/007a 在 Animation.Play/CrossFade 后立即 Finish。SpellFinishAction.OnEnter 启动延迟回调但自身立即 Finish；嵌套 DelaySpellFinished.MoveNext 在 IL_002a 创建 WaitForSeconds(m_Delay)，随后 IL_004a 调用 Spell.OnSpellFinished。

新增导出该嵌套类型，文件名过滤 Windows 不允许的尖括号。original-script-audit.json 现含 132 类型，均保持 restored=false；数量是静态调查范围，不能称完整原脚本恢复。

实际地狱烈焰两台 FSM 启动动画后立即进入完成通知动作，m_Delay 约 .35 秒，然后各自独立 Wait 2 秒，关闭目标捕获平面。Wipe2 Clip 虽长约 6.033 秒，**Clip 长度不是状态等待时长**。上一轮仅凭 6 秒 Clip 与 1.2 秒播放区间差异，不能推出应播放 6 秒；现已由原动作/协程 IL 和实际 FSM 验证修正。

## 实现

NativeEffectInstance 保存带 machine/node/time 的本地完成通知。延迟不阻塞 FSM；Seek 到到期时间才将通知标为 delivered，回退清除并确定性重建，不重复累计。未解析或非有限延迟计入 UnboundFinishActions。此处没有执行官方 Spell/程序集，完整外部 Spell 所有权和 OnSpellFinished 后续行为仍未恢复。

SetAnimationSpeed 现在按节点与 Clip 名保存，核对原 Animation[animName].speed 赋值。零速暂停、负速倒放保留，不再强制最小 .001；改变速度时重定基准而不是将全部过去时间乘新速度。everyFrame 从 FSM 浮点变量更新速度，不重新启动 Clip，并保持该动作未 Finish。对未知 Clip 不猜测。

客户端表现队列增加独立 NativeImpactDelay。已有已核对地狱烈焰槽使用 .35 秒伤害延迟、2.1 秒视觉区间，保留原约 2 秒捕获关闭动作。未审阅的卡沿用现有区间末端伤害行为；没有全池自动启用。批次等待视觉尾部结束，伤害可提前显示，历史记录和跳过仍只收敛一次。服务器仍立即完成规则结算。

review 验证器检查 impact_delay 有限且位于区间内，源程序 SHA 核对保持。两启用槽及源文件 SHA 未改变，地狱烈焰仍为 supplement。完整原同状态连续帧没有认证，2.1 是覆盖已证实时序的审阅区间，不宣称原逐帧认证。

## 实际验证

- 672 Python 通过（54.15 秒）；新增非法延迟和独立延迟审阅测试。
- 77182 原资源/运行时检查通过。新增命名速度隔离、暂停、倒放、变量每帧、回退，以及真实地狱烈焰 .34/.36 秒通知与 2.02 秒捕获隐藏、队列伤害早于视觉退场。
- 30 Prefab GPU/84 帧通过；Windows 构建成功，4101 既有 C# 检查通过。日志 native-clock-prefab-checks.log、build-native-clock.log。
- 新构建后台双 Windows Player clock_1/2 在端口 47360 完成 11 回合/v42。Shrink2 术士 14HP 胜，Shrink1 法师 -1HP 负，Match a71040623bbe4a93b600e928331f7846。公开哈希 e4a3a229e4970046716172f02a24a3a146605f95e617866bc07c201d7bf934e3 相同，队列峰值 1。后台不绘制正式 GUI，不作为人工比赛或正式帧率认证。

没有改变 Shader 运算，中央过曝仍未解决；当前动作播放器仍缺完整 Animation.CrossFade/PlayMode、StopAnimation、自动停用、Animator、所有外部 Spell/Actor 所有权及后处理。全卡原动画仍未完成。未使用 Computer Use，没有修改官方安装或用户校对数据。
