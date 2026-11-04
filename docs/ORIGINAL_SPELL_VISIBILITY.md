# 原法术表显隐与状态绑定

2026-10-12。全卡原专属动画、完整Shader与脚本未完成。

原静态IL依据为 local/script-audit/Assembly-CSharp.dll.SpellTable.il.txt、Actor.il.txt、Spell.il.txt、SpellState.il.txt；程序集身份见 original-script-audit.json，未执行官方程序集。SpellTable.Show仅调用已加载、存活、类型非0实例的Show；Hide包含类型0。两者都不加载新槽，也不操作GetSpell(local=false)返回的非本地实例。Actor的ownedSpells规则不同：Show包含类型0、Hide排除类型0，不能与本地表混用。

## 已实现

NativeActorSpellTable增加Show/Hide及显式原法术回调。保留缓存、释放、存活判定、懒加载和源类型语义。缺少回调时明确抛出未绑定错误，不把Root.SetActive假装为原Spell.Show/Hide。新增8个Unity检查，涵盖类型0差异、非本地实例、销毁实例、释放后遍历和缺失回调。

`python -m tools.spell_visibility_source`只读调查205个现有actor-spell程序，记录192个Spell与23个SpellState组件的精确CAB/PathID、GameObject/ObjectContainer、状态类型、StartDelay、外部动画对象和声音列表。205个主层级的名字/父级/路径逐项与缓存匹配后才写节点索引；7个外部owner为-1，未猜测替代，已有容器指针全部位于主层级。private runtime m_shown/m_initialized/m_playing没有从序列化数据伪造。

原绑定清单为 manifests/original-spell-visibility-source.json。44程序保留原依赖警告；没有将其隐藏或计为完整资源恢复。此次仅检查Spell/SuperSpell/SpellState的确切类名，其他子类的状态语义未恢复。

## 未完成

正式NativeBattleSummons尚未绑定Spell.Show/Hide回调，NativeActorPresentation仍只控制视觉根。新表API尚未让正常客户端的角色联动生效。原Spell.Show/Hide通知活动SpellState；状态的PlayImpl还涉及StartDelay协程、外部Actor动画、声音和StopImpl清理，不能用根节点开关替代。护甲、阴影、Highlight、ownedSpells与m_IgnoreSpells完整分发仍未接入。

四个未经修改的原入场图（普通/随机两个图×CS2_179/EX1_116）仍在NoneState结束，第二声均在Actor Visible，Actor借用后恢复；每例仍报告2个显隐副作用缺口。因此本节点不增加完整效果启用数，仍2，也不宣称入场已完整还原。

## 验证

1158项Python通过，12项UnityPy版本回退警告；增加主层级节点索引后2项源重放检查再次通过。work/native-spell-visibility-final.log退出0：42项SpellTable、78193既有原效果运行时与四个原入场完整图检查通过。首次启动检查时入口类尚未创建，日志work/native-spell-visibility.log退出1；补建专用入口后最终运行通过，该失败不是客户端回归。

Windows构建work/build-native-spell-visibility.log退出0，实际Mixer Bundle9组编译常量核对通过。个人UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6；未使用Computer Use，未修改官方安装。

新构建后台spell_visibility_1/2客户端在47427端口完成比赛e6bfb8272b71418bb7bcb68f9d448156：15回合/version47，Shrink2术士19血胜、Shrink1法师0血负。双方公共hash=63b556f84f1b1cc1387a924fd4a526a34f72b4a0a1b47aad13e57ce54f7c96f3，日志无异常/fallback；不作为新增UI效果、人工比赛或1080p性能验收。
