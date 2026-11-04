# 正式播放器原区域分派接入

全卡专属动画、完整原 Shader/脚本未完成。此节点保持两张审核启用卡牌的范围，改变它们的实际播放路径，没有新增整卡一致性通过数量。

## 改变的正式行为

此前 NativeSuperArea/NativeSuperAction/NativeAreaInstance 只在诊断宿主运行，正式 NativeEffectPlayer 直接采样展开后的根层级。现在奥术爆炸 CS2_025、地狱烈焰 CS2_062 默认走 NativeAreaPlayback：读原根区域配置、按控制方选择一个原 Prefab、独立克隆、挂接源/原任务上下文、激活原根 FSM、收集真实子效果完成回调，再通知父控制器。

奥术爆炸没有可用敌方区域配置，按原 Determine 回退友方 BothSides；地狱烈焰分别选择 BothSide/BothSide_Opponent，不同时播放两个配置。保留已审核的 GUI 矩形合成、手工呈现区间与规则/网络层，未改服务器结算或 UI 校准。

仅允许原 start/missile/impact/chain 均禁用、没有根 SpellState 内容、启动/区域延迟均0、定位不需要外部服务的根进入此路径；其余根不以默认操作或延时代替缺失原服务。完整延迟、启动、导弹、冲击/连锁、全局事件等待及原相机投影仍未接入。

NativeAreaInstance 在首次 Seek 前绑定 Timeline 相机，修复正式接入时的“must be bound before playback”回退。子上下文保留卡牌类型、角色解析、目标和原任务列表，仅初始生命周期切为 None；已有来源/目标缓存继续通过当前播放器角色借用门禁。

原区域克隆前进播放，回退时重建克隆而不在现有实例上倒退并丢失 source/task 设置。克隆、离屏根和材质按播放器现有 EndFrame/Dispose 释放。

## 完成通知与动画的区别

最初检查错误地假设动画未结束时父完成通知一定不能发出。静态重读 AnimationPlaythroughAction 与真实原 FSM 纠正此假设：原 OnEnter 调用 Animation.Play/CrossFade 后立即 Finish，奥术爆炸随后进入 SpellFinishAction，再进入 Wait 状态，视觉动画继续播放。记录的原通知时间是0.0001秒，而非初始化 None 通知。

正式播放器因此按原回调释放父等待，继续保留区域视觉至呈现区间结束，不修改原动作强行等待整段动画。初始化 None 阶段已有通知不会交付给后来注册的克隆监听器。

## 验证与限制

`work/native-area-player-verified.log`：48 项正式播放器检查通过、退出0，包括两卡两侧选择、原子效果回调、完成后 GPU 仍可见、原历史/角色上下文、回退与独立同龄实例状态一致。既有 NativePowerTargetChecks 检查链同时通过。

固定离屏512×256截图保存在 `tests/visual/current/source-area-player/CS2_025-0.png`、`CS2_025-1.png`、`CS2_062-0.png`、`CS2_062-1.png`；已读取实际 GPU 图片，都是原 Prefab/Shader/纹理组成。它们是离屏特效截图，不是原版 Oracle、完整战斗客户端截图或整体视觉一致性证明。

完整原脚本、场景投影、Shader/后处理、自动呈现区间与全卡专属效果仍未完成。没有 Computer Use，没有修改官方安装。

## 最终回归

原 SuperSpell 分派51项、区域克隆32项及其后既有检查链通过，`work/native-area-player-super-regression.log` 退出0。原效果GPU41项通过，Python常规1181 passed/4 deselected/28 warnings/205.02秒；四项专属定位/区域/启动/RGBA大型来源重读未重跑。`work/build-native-area-player.log` Windows构建退出0，原混音bundle9项常量核对通过。

最终后台 area_player_1/2 在 localhost:47463 完成一局，match_id=1765ef70935442ca9d25f7d9fd29a5e1；双方finished、回合16、版本59、英雄生命4/-1，最终public_hash均为08b6d5354ae85617d19737dbd9aabf93115ea29847dea1395cafd28088ce6235。双方和服务器日志没有Exception/fallback/Error。后台自动比赛不证明正式GUI的整局画面、人工听感或60 FPS；正式播放器的区域执行由前述直接检查验证。

UI校准 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。全卡目标保持未完成，正式启用范围仍两项。
