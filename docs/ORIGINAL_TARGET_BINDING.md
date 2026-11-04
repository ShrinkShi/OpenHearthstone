# 原目标类型分支与可见性动作

全卡专属动画、完整原 Shader/脚本未完成。本节点按静态 IL 恢复事件参数和两个动作的已有路径，继续现有服务器/客户端架构，不使用 Computer Use，不修改官方安装或用户 UI 校对。

## 原证据

来源及官方 DLL 哈希见 manifests/original-script-audit.json，IL 位于忽略的 local/script-audit：

- PlayMaker.dll ActionData.LoadActionField / GetStringParam：dataVersion > 1 的 FsmEvent 从 stringParams[paramDataPos] 读取；无效下标/空值为无事件。
- PlayMaker.dll FsmUtility.ByteArrayToFsmEvent / ByteArrayToString：旧格式按 paramByteDataSize 的精确范围读取 UTF-8，没有长度前缀。
- Assembly-CSharp.dll SpellAction.GetCard：Which 1取目标、2取来源英雄、3取power target（缺失退回来源）、4取来源英雄、5取spell actor（缺失退回来源），其余取来源。缺失目标不猜随从。
- 来源卡存在但来源英雄缺失时返回空；actor不存在与actor存在但没有卡牌分开，后者返回空，不错误回退来源。
- Assembly-CSharp.dll SpellCardTypeAction.OnEnter：按原 TAG_CARDTYPE 3/4/5/7/10/39/40 派发已保存的英雄/随从/法术/武器/技能/地标/战棋奖励事件，然后完成动作。空事件不生成分支。
- Blizzard.T5.PlayMakerCustomActions.Runtime.dll SetVisibilityRecursiveAction：includeChildren选择当前对象或活动子树 Renderer；resetOnExit保存并恢复进入前 enabled 状态。不主动激活隐藏子对象。

静态审计从124扩至129类型，新增 SpellAction/Which、ActionData、FsmEvent、FsmUtility。只读 Cecil 元数据/IL，不直接执行官方程序集；清单 restored=false 保留。

## 当前执行

导出器支持新旧事件格式。NativeSpellContext从当前表现批次 before/after 的公开实体和现有 CardDef 类型获取来源、目标和所属英雄；卡牌已离开手牌时仍可从批次前值解析。上下文在实例创建时获取，禁用效果和后续帧不重复扫描实体。原效果仍只在客户端播放，服务器立即结算。

NativeEffectInstance按原事件参数选择分支，初始事件在首帧派发，回退Seek重复相同路径；缺失上下文或未解析参数记录 UnboundCardTypeActions，不用默认随从冒充绑定。递归可见性保持内部捕获隔离，并按原resetOnExit恢复状态。初始事件派发与每帧更新分开，保留GetAmbient进入时读棋盘、后续帧读RenderSettings的区别。

明确未完成：原 Spell 对象之间的完整所有权、实体姿态/纹理、power target与spell actor的正常比赛绑定、原完整生命周期派发、Animator Controller、音频动作和其他脚本。普通播放仍沿用已有 Birth/Action/Death 入口选择；诊断允许显式指定入口，不把此选择称为完整原 SpellController。

现有无纹理材质的兼容隐藏策略仍保留，可见性动作不能据此称所有Renderer行为完全一致。另已只读核对BT_035的三个原Clip为非legacy，原MuscleClip.StopTime分别1.5/1.25/2.5秒，但当前缓存全为0时长/0轨道；原AnimatorController PathID 4545653275405446762含Controller/TOS/AnimationClips。下一步必须恢复原Mecanim数据和绑定，不能按名字猜一条自制动画。

## 全缓存更新

```text
.venv/Scripts/python.exe -m tools.native_effects --refresh-event-cache
```

按原机器的 Bundle/CAB/PathID读取，核对状态、动作、参数的序列与身份，仅更新事件参数和支持情况，不重新提取网格/纹理或改校对。原冰霜FSM存在两个同名Action 1，按序列处理，不能以名字字典覆盖。来源漂移即记录失败，失败文件不发布。

254个不同Prefab缓存共2877个事件参数全部刷新。重复执行为0更新/0失败，记录见 manifests/native-event-refresh.json。352效果槽中22槽含目标类型动作、47槽含递归可见性动作；这些是覆盖数量，不是完成卡数。原313项依赖错误按不同Prefab计保留，按效果槽重复计为420项。

## 真实 Prefab 诊断

| 样本 | 显式诊断上下文 | 有效帧 | 最大亮像素 | 最大不透明黑像素 | 状态 |
|---|---|---:|---:|---:|---|
| BT_035 | 随从目标、Death | 4/6 | 308 | 717 | 从空白恢复原发射；未启用 |
| BT_175 | 随从目标、Death，同一Prefab | 4/6 | 308 | 717 | 从空白恢复原发射；未启用 |
| BT_801 | 随从目标、Birth | 6/6 | 505 | 695 | 原发射增多；未启用 |

三个样本未绑定类型动作计数均为0，粒子峰值分别24/24/109，原Glow仍为零。BT_035的离屏图已查看，仍是很小的绿色粒子，缺少原主体动画；黑遮罩也未合格。新增三个非空检查只验证原分支确实运行，不替代外观和原版连续帧验收。全部三项保持禁用，其他350槽同样禁用。

全刷新后的奥术爆炸、地狱烈焰组件合成图重新查看并通过原范围内核验；冰霜新星继续禁用。两项启用程序的新SHA256分别为 e0feeaed8dc6e24583dc9a84946058dc856e13aaea2e7b67ed9b35a902f362ad / 8af364ab49afc76206fd90567d797da4d0f6f6cf316a2b9df2da2b692249b3b4。没有新增完成卡数。

## 验证

Python全量630项通过（44.80秒）。新增7项覆盖新旧事件解析、空事件、坏UTF-8/截断、禁用动作、身份漂移与重名状态。NativeEffectChecks新增36条实际行为断言，覆盖7种目标类型、来源/目标选择、首帧和回退派发、空/缺失/未解析事件与可见性退出恢复；累计76970条原数据/行为断言另计。

work/native-card-type-all-cache.log：649条GPU检查通过（69+9+343+49+26+68+25+60）。其中3条新增是实际原Prefab目标分支发射检查，未降低既有可见/黑遮罩阈值。Windows构建和后台对局记录另列；离屏GPU与后台规则回归不等于正常GUI人工逐帧认证。

最终构建work/build-native-card-type-null-check.log为Result: Success，4101条既有C#断言通过，76970条原数据/行为断言另计，包含缺失英雄与actor空卡检查。端口47332，两个最终构建的后台Windows客户端完成11回合/v34，比赛92195d0ca69f4d7a9660f9ec24e80a87：Shrink1法师胜（28HP）、Shrink2术士败（-1HP），两个Profile最终公共哈希a98d6a0cd28664be42e5177bf52618926ff1082c46350a5bd08f8f623a166f02，日志无运行异常。

此前构建work/build-native-card-type-final.log也成功：端口47330完成11回合/v28，比赛e83676a40bc944fb8b2ff1c778ae9e64，Shrink1法师败（-3HP）、Shrink2术士胜（25HP），公共哈希d6d11817248c8c67df676d71612106b0c40eb6fbc3a0d64eafe72f0c189393a1。更早端口47328完成19回合/v69，Shrink1法师1HP胜、Shrink2术士-1HP败，公共哈希5285a5feb521506b6a918cfddda3950a5fe4784e78fedbaf867676a31f0d201d。后台对局不执行正常GUI原效果绘制。

测试进程按命令行核对后关闭，原37240/PID10536服务器保留。用户校对SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。全卡原专属动画、完整原Shader/脚本目标继续，未宣告完成。
