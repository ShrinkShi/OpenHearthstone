# 原攻击动画事件与状态链

后续节点恢复了四攻击Prefab的PlayDefaultSound和等待解除；下文保留本节点历史结果，最新边界与验证见[ORIGINAL_ATTACK_EVENT_AUDIO](ORIGINAL_ATTACK_EVENT_AUDIO.md)。事件对象参数类型以导出原记录为准，不能统一视为SoundDef。

2026-10-12。全卡原专属动画、完整 Shader/脚本未完成；完整效果启用数仍为 2。

## 原数据与实现

只读原 Spell.ChangeStateImpl、OnStateFinished、OnSpellFinished、HasStateContent、SpellStateAction、PlayNewParticles 的静态 IL。原程序集身份在 manifests/original-script-audit.json；未执行官方程序集。源导出仍使用 tools.spell_visibility_source --export-states。

8 个真实源程序导出 50 个动画片段的 48 条 AnimationEvent、43 个事件接收组件；事件的时间、函数、字符串、浮点/整数参数、MessageOptions、原对象指针与完整原记录保留。原始 CAB/PathID 与缓存片段逐项匹配。声音对象参数仍是 SoundDef 指针，不擅自替换成随机音效。

事件曲线为空时，原回调也可能发生在 0.533 秒。播放器保留独立 event_horizon，不把此类片段在首帧删除；曲线 duration 保持原值。事件采用实例固定时钟，停止、隐藏后不再发送，重新显示重新计时。源回调可以在播放中替换当前状态，因此按片段快照遍历并在状态切换后停止旧片段的余下回调。

原 OnStateFinished 按 GuessNextStateType 进入下一可用状态；OnSpellFinished 先通知完成，视觉尾段继续。原 PlayNewParticles3/PlayNewParticles3andChilds 根据原组件目标启动粒子，空目标保留原无操作语义。SpellStateAction 根据实际全局转移绑定识别类型；显式选择同一类型仍执行原先停止、随后重新播放的顺序。组件禁用不删除源状态槽，仅不初始化其播放。

## 实际验证

work/native-spell-state-events-final.log 退出 0。四个真实攻击 Prefab 测试 57 项：出生事件约0.533333进入 Idle；死亡约0.516667通知完成、0.533333退出；取消约0.016667通知完成、0.300000退出。c710 源 Cancel 的全部动画对象 disabled，测试确认它不会产生伪造完成事件。重选、隐藏/显示、粒子回调均验证。

四程序事件诊断分别为：23d38186 12事件/3粒子启动/2未处理；2a965e27 6/1/0；bf21da97 8/2/0；c7109e87 4/0/0。未处理项是声音及攻击等待同步路径，仍明确记录。78 既有源状态检查通过，实际动画启动90次；78193运行时及四原入场状态链通过。最终1161 Python测试通过，13项UnityPy版本回退警告，42.57秒。

## 未完成

PlayDefaultSound 的原 AudioSource/SoundManager 路由和 StopWaitingToAct 的攻击同步未恢复。原 SpellController 的来源/目标/任务列表、FSM 启动等待、派生状态方法、位置/方向、重复完成回调语义及完整生命周期仍待核对。当前事件执行只覆盖这8个导出的源程序及支持的正速度非循环片段；循环/反向播放不能算完整。正式对战尚未启用这些攻击附属实例，不计为新增正常对战动画验收。

work/build-native-spell-events.log 退出0；实际 Mixer Bundle 的9个编译常量与原数据一致。UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。未使用 Computer Use、未修改官方安装。

新构建后台spell_event_1/2在47429端口完成比赛4a43c48c111f436cac212a66d298c420：15回合/version49，Shrink2术士11血胜、Shrink1法师-3血负；两端hash均为d674654bca18391cda40b0d8b1314f07a2fc9648890101ae1cfe2b44fb6b804a，日志无异常/fallback。仅作为可玩性回归，不是新增攻击画面/人工比赛/性能验收。

新增真实源重放测试：八缓存复制至独立临时目录，从本机原客户端重新解析事件、对象引用及接收组件，生成结果与缓存逐项一致；未以清单自我比较代替原文件核对。加入此检查后全量测试再次运行通过。
