# 原召唤完成边界与并行清理

后续节点已恢复共用Actor的敌方42槽及其0.43秒源边界；本文下面的敌方缺口为当时记录，当前状态见 [ORIGINAL_OPPONENT_SUMMON](ORIGINAL_OPPONENT_SUMMON.md)。

2026-10-11。全卡原动画、完整原Shader/脚本**未完成**。修正上一节点把原2秒清理Wait全部算作召唤阻塞的差异。

## 原静态证据

`Assembly-CSharp.dll.Spell.il.txt` 的Spell.OnSpellFinished：设置m_finished=true，移除服务器阻塞Spell（原客户端服务），BlockZones(false)，随后FireFinishedCallbacks。此方法不停止FSM、不销毁对象、不等待状态内其余动作。SpellFinishAction.OnEnter按m_Delay调用OnSpellFinished，然后立即Finish动作。

源SUMMON_IN的BIRTH→wait等待0.5秒，再进入Kill Dust, Play Sound；同状态内SpellFinishAction.delay=0，而Wait=2。前者交还控制，后者仍属于清理尾段。当前客户端Server本来即时结算，本节点只改变客户端表现队列，不引入原服务器阻塞机制。

## 当前执行

NativeBattleSummons沿已核对的原FINISHED路径分别计算首个SpellFinishAction时间和完整清理时间：BlockingDuration=0.56秒（原0.5秒+0.06秒帧余量），SourceDuration=2.56秒（原0.5+2秒+余量）。动态/未解析finish延迟明确拒绝，不用一个常量伪造未知边界。

Presentation只按BlockingDuration保留召唤cue。cue结束时推进至完成通知，确认已送达，再将实例保留为tail；原FSM及粒子继续按独立时钟前进。ClientApp每帧按表现Speed推进tail，Paused时delta=0。Actor、表实例及音频决定仍按各事件保留，音效不重复。可见实体消失/变形则取消尾段，达到原清理时间后释放；比赛退出清空。

因此清理尾段不会单独使Presentation.Busy为true；整个出牌批次仍包含卡牌离手和其他真实表现，不能把0.56秒当作整张牌从点击到解锁的总时间。当前保留帧余量，不声称与原版逐帧完全一致。

## 尚未恢复

完整原SpellManager/Zone服务、原完成回调监听器集合、同一Actor的多个原动画写入者共享、敌方专用表、全卡专属效果及完整Actor/效果Shader仍**未完成**。当前尾段仍通过LiveActor取景，完整棋盘空间中的粒子/攻击运动组合未认证；后台自动比赛不是人工视觉/听感/GUI 60FPS验收。

## 验证

最终62项实际召唤检查、70项GPU检查/8帧通过，验证首完成边界、旧缩放关闭、源声唯一、cue结束后尾段仍可见、队列不因尾段Busy、暂停、实体移除和清理；并行两个独立Actor/音频时钟及大步快进回收通过。8张离屏图保存在 `tests/visual/current/native-battle-summon/`，包含两个parallel取景，未使用原版整图铺底。

704项Python通过（40.02秒）。既有4106客户端、78172原运行时、34表、26Actor动画、29音频及Windows构建通过。完整GPU回归、iTween40程序/188动作/133生命周期/798帧0异常保持通过。

真实双Windows Player后台比赛：summonunblock_1/2、端口47396，13回合/v45；Shrink2术士21生命胜，Shrink1法师-2生命负。match_id `ffe066cae6b747ffb9cf546fbb34ab99`，双方public_hash `e0f1d275327c419d49ad037cfdffb58ff0a53b8f0834e83661435bcda79df596`一致，队列峰值1。客户端31360/17800与服务器36932按PID/命令行核对清理，用户服务器10536保留。

日志 `work/build-native-summon-unblock-final.log`、`work/native-summon-unblock-gpu.log`、`work/native-summon-unblock-final-gpu.log`；最后追加两张并行截图只改变Editor检查，不改变已构建Windows运行时代码。用户校对SHA256仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。没有Computer Use，不修改官方安装、不执行原客户端DLL。
