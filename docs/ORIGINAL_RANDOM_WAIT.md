# 原随机等待脚本

2026-10-11：36个当前源效果程序的37个RandomWaitAction不再取固定平均时长，恢复随机抽样和严格结束边界。全卡原专属动画、完整Shader / 脚本仍未完成。

## 原代码证据

只读IL在local/script-audit/Blizzard.T5.PlayMakerCustomActions.Runtime.dll.RandomWaitAction.il.txt，程序集哈希和调用在manifests/original-script-audit.json。不执行官方程序集。

OnEnter在最小值和最大值均<=0时直接Finish，然后发送非空m_FinishEvent；否则保存FsmTime.RealtimeSinceStartup，用UnityEngine.Random.Range(float,float)抽取等待时长，累计时间归零。OnUpdate使用m_RealTime选择实时差值或累加Time.deltaTime；只有累计时间严格大于抽样时长才Finish并发送事件，等于边界不会完成。

## 实现与来源

NativeRandomWait保留这组输入和比较语义；NativeEffectInstance按原m_MinTime / m_MaxTime / m_FinishEvent执行动作，支持已有FSM浮点变量，等待和自定义事件独立于其他动作完成。等待完成通知不会重复；退出状态取消对应等待，倒放重建。

NativeRandomWaitSampler调用真实Unity浮点Range，同时保存并恢复全局随机状态。种子117是客户端可重复播放适配，不宣称恢复了官方运行时的全局随机序列。当前37个源动作的m_RealTime均为false，固定采样时钟累加客户端效果delta。独立时钟类已验证Realtime分支，但表现实例尚未接入独立实时输入；将来遇到m_RealTime=true时明确报告未绑定，不把效果时间伪装成墙钟。

`python -m tools.native_random_wait` 精确刷新36个源程序、148个参数；首次36更新、重复零更新零失败，来源Bundle / CAB / PathID保留在manifests/native-random-wait-refresh.json。没有修改规则、官方安装、用户校准或完整效果启用范围。

## 验证

816项Python通过，包含37项新增刷新/参数检查。261项实际Unity检查通过：64个抽样与原Unity.Range对照、严格边界、即时完成、实时/缩放时钟区别、全局随机状态保护，以及37个真实源动作的启动、完成、倒放、重播。work/native-random-wait-checks.json保留数量；完整Actor GPU回归work/native-random-wait-all-gpu.log退出0，包含既有网格、材质、Timeline、原粒子和法术检查。

这些检查隔离源等待动作，不证明36个整卡程序已完整恢复或视觉一致。完整法术启用数仍2；MaterialService、专属音频、其他脚本/Shader及完整事件路由继续恢复。

Windows构建work/build-native-random-wait.log为Success、退出0。下一处明确源证据是FromCorpse的Anim状态有两个AudioPlayClipAction，Random子图音量0.8、普通子图音量0.7，pitch1，第二个延迟0.8秒；当时m_Sound类型化资源引用缺失。后续声音恢复及实际验证见ORIGINAL_CLIP_AUDIO.md，不能将通用入场声音称为该专属声音。

47417端口、randomwait_1/2独立后台Player完成比赛b718f71adce94f81bf906d70fa63ea73：17回合、version58，Shrink1法师25血获胜、Shrink2术士0血失败。双方公共hash均为e8d5695c8035252cd87028fbaada2a42e44958f4bfac27ab1569248d5dd53c54，日志无异常或fallback。测试为后台模式，不能宣称人工比赛或全卡动画视觉验收。自有21244/31452/9196已清理。

用户UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6，构建场景ID已归一，工作树保留原历史。
