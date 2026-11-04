# 原 SpellState 播放

后续状态结束/完成和粒子 AnimationEvent 接入及当前缺口见[ORIGINAL_ANIMATION_EVENTS](ORIGINAL_ANIMATION_EVENTS.md)；下文保留本节点验证记录。

2026-10-12。全卡原专属动画、完整 Shader/脚本未完成；完整效果启用数仍为 2。

只读 Cecil IL 依据为 SpellState、SpellStateAnimObject、SpellStateAudioSource、SpellState/<DelayedPlay>d__24。未执行官方程序集，未修改安装，未使用 Computer Use。原程序集身份见 manifests/original-script-audit.json。

`python -m tools.spell_visibility_source --export-states` 从 8 个真实附属 Prefab 导出 23 个状态、51 条动画对象绑定，保留 CAB/PathID、节点、目标种类、动画层、速度、CrossFade、粒子控制、组件 enabled。所有动画引用解析成功；原状态声音列表均为空，延迟均为零。一个原组件 disabled，保持禁用，不为测试擅自激活。

NativeSpellStatePlayback 恢复 Play/Stop/Show/Hide/Initialize：隐藏保留 playing，重新显示重启播放；延迟由组件拥有，停止或隐藏取消。NativeEffectInstance 的显式状态入口播放真实源曲线，使用原层/速度/淡入参数；切换时按下一状态的同对象、同层绑定决定是否停止旧动画。原粒子控制使用既有固定时钟。借用 Actor 的动画组件和位姿在释放时归还。m_LocalEulerAnglesHint 是编辑器提示，不替代四元数旋转曲线。

## 未完成

显式 SelectSourceSpellState(type,time) 不是完整 SpellController。调用方尚未接通正式攻击、取消、死亡等状态；反向 Seek 重置显式选择，调用方需重新发送。原 AnimationEvent 的脚本通知/声音、原状态 AudioSource 路由、Actor 的护甲/阴影及 ownedSpells 显隐未完成。本轮没有新增完整卡牌效果，也没有新增正常对战画面验收。

## 验证

1159 Python 通过，12 项 UnityPy 版本回退警告。work/native-spell-state-playback-pass.log 退出 0：78 检查，遍历 8 个源程序、23 个状态（其中一个禁用保留），80 次实际动画启动；78193 既有运行时检查及四组原入场状态链通过。此前失败暴露旋转提示被误判为不支持、禁用源组件被测试错误地要求激活；修正后重跑通过，失败日志保留。

Windows 构建 work/build-native-spell-state.log 退出 0。实际 Mixer Bundle 的 9 个编译常量字段与原数据一致。个人 UI 校准 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

新构建后台 spell_state_1/2 在端口47428完成比赛1213485d2a3a4de487cbbf3a04fcca17：12回合、version42，Shrink2术士18血胜、Shrink1法师-2血负。两端公共hash均为919948ffb81ac8da849586cc49eccbf865eecab9bcc00376587eb08f54419e9a；日志无异常/fallback。后台比赛不等于人工画面、听感或 1080p 60 FPS 验收。
