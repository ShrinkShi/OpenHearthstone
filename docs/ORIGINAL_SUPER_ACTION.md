# 原 SuperSpell 分派与完成等待

2026-10-12。完整全卡专属动画、原 Shader/脚本未完成。

## 恢复的实际分支

从本地官方 Assembly-CSharp 的静态 IL 核对 SuperSpell.DoActionNow、AreEffectsActive、UpdatePendingStateChangeFlags、FinishIfPossible、OnSpellFinished、OnStateFinished、OnFsmStateStarted、CanPurge。未执行官方程序集。

NativeSuperAction.DoActionNow 按原顺序先选择/生成一个区域效果，再查询 Missile、Impact、Chain 和视觉目标数量。无目标或没有这三个效果时调用启动效果 Action；导弹优先选择连锁，其次顺序，最后全部生成。没有导弹时，冲击按连锁只生成当前目标或生成全部，再生成连锁并进入启动效果 Action。导弹分支由原导弹操作负责启动效果，不在分派器重复激活。

每个底层操作由显式服务提供。缺少原导弹/目标/历史执行服务不能当作已经完成；本节点没有用通用粒子或默认延时替代它们。

完成等待按原 m_effectsPendingFinish > 0 判断，包括保留非正数不活跃的行为。StateContent 缺失时同时置 PendingSpellFinish 和 PendingNoneStateChange；存在时清除两者。SettingUpAction 或存在待完成子效果时 FinishIfPossible 返回；随后先完成 Spell，再结束 State，再 CleanUp，且分别在回调返回后清除相应标记。没有额外去重或重新排列回调。

OnStateFinished 只在下一状态为 None 且子效果仍活跃时挂起；若有可用下一状态，则仍调用原基类状态完成逻辑。OnSpellFinished 独立挂起，不能把两种完成通知混为一谈。

OnFsmStateStarted 先忽略与当前 activeStateChange 相同的状态；仅在意外进入 None 且仍有活跃子效果时置两个挂起标记。其他状态继续原基类处理。CanPurge 有活跃克隆时直接拒绝，数量为零时调用原基类；不以已完成回调代替克隆释放。

## 真实执行验证

work/native-super-action-final.log：51 项新增 Unity 检查通过，并串行执行既有 32 区域克隆、20 相机、11 定位、460 曲线和完整既有检查链。覆盖导弹/冲击/连锁/顺序/目标数量全部分派组合、设置期间同步回调、完成与 None 通知先后顺序、FSM 回调及克隆清理门禁。

两个真实原区域 Prefab 分别通过 NativeSuperAction.DoActionNow → NativeSuperArea.Spawn → NativeAreaInstance → 原 FSM → 子效果完成回调 → FinishIfPossible，确认父完成通知在真实子效果结束前不发出，结束后顺序恢复且只交付一次。该诊断宿主没有任务或可用启动/导弹/冲击/连锁，符合奥术爆炸和地狱烈焰原根的对应禁用配置；不能因此证明含这些组件的其他卡牌也完整执行。

## 未完成

DoAction 的游戏事件等待、随机启动延时、启动 Prefab 的 Birth/Action/Death、原导弹路径、冲击/连锁实现与任务/目标收集、完整对象池/清理、原战场投影、正式播放器切换及全卡 Shader/脚本仍未完成。正式客户端未绑定此新分派器；现有审核启用名单保持 2。

最终 Windows 构建 work/build-native-super-action-final.log 退出 0，实际 Mixer 九常量一致。用户 UI 校准 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。没有使用 Computer Use。

Python 常规回归 1168 passed/2 deselected/17 warnings/167.76 秒。排除项是此前已通过、本节点代码和缓存未变的全部定位及全部区域配置来源重读；没有宣称本次全量同命令全部绿色。

后台 super_action_1/2 独立 Profile 在端口 47443 完成 268559dcd6754d599bfd334862a4b41f，14 回合/version47。Shrink1 法师 8 血胜、Shrink2 法师 -2 血负，两端 public_hash 均 6232b8c2b58877421eca37ff04d90cf0b01eaf8f7e1fe8c1ea2091b8ee805b5c；日志无 Exception/fallback/Error。比赛使用初次本节点构建，最终构建随后补入 FSM/CanPurge 门禁；这些门禁仍未绑定正式播放器，不影响比赛路径。该后台回归不证明新原脚本在正式 GUI 播放，也不是人工比赛或 FPS 验收。终局核对命令行后只清理自有 PID 8600/23376/5372。
