# F022-A 输入时序与英雄技能目标

## 本轮范围

本提交先交付 H01/H10 的短输入门与视觉尾段隔离、H10 的 1.15 倍视觉时钟、战士与恶魔猎手的真实后续操作回放合同，以及 H09 牧师目标事件解析。德鲁伊按钮效果按 0.9 倍显示；萨满和盗贼代码未改。

## 实现记录

- H01、H10、H11 的 native source 不再延长输入门或 gameplay batch 生命周期；输入门最长 0.2 秒。source 仍通过独立视觉尾段继续播放，尾段移除时不写回旧 Display 状态。
- H10 将同一个 `PlaybackAge(age) = age * 1.15` 映射用于 source 准备和 source/target 绘制，未调整音高。
- 战士 Player 合同要求在 source active、Presentation busy 且 `InputLocked` 已打开时实际调用 `SendMove(EndTurn)`；记录 0.2 秒加一帧预算、action counter 增量、authority 回应及 source tail 清理后的 authority/display hash。
- H10 Player 合同同样要求 source active 时提交合法 End Turn，并验证 authority 回应和两席位最终 hash。此前“等待 source release 后才提交”已移除。
- H09 使用同批次唯一 `HeroPowerTarget` 选取 native cue 目标；严格核对真实 H09 source、`HERO_09bp`、owner、唯一性和 before/after 中真实 hero/field 角色实体，再信任服务器已验证的目标，不用 observer 的本地 `moves` 复验。若同批有 Heal，要求唯一且目标相同。仅含 Heal 的旧快照沿用其唯一目标。满血无 Heal 时只创建视觉目标 cue，不构造 Heal 或 health change。
- 德鲁伊 `HERO_06bp` 按 `DruidHeroPowerButtonScale = 0.9` 缩小按钮 composite。
- H11 Player 合同改为在输入门打开、source route 与 native visual tail 仍 active 时，经生产 `SendMove(EndTurn)` 提交合法动作。记录 power authority state 到提交的时延、action counter、authority 响应、响应时 source/tail 是否仍 active、tail 清理后双方 hash 与 display hash，且不等待 observer 才提交。
- H11 两席位采样 source Node 17 与真实 Ghoul Actor 的 world/screen 坐标、Actor parent、按钮与召唤点 GUI rect、主/离屏 camera mask；约 `.04/.12/.28/.55/1.0s` 留下来源/投影记录。源码证据显示 Node 17 会被放到世界坐标，PresentationCamera 原先可见 layer 30，而 H11 同时把 source effect 合成到按钮和召唤点；这符合截图中的错位重复效果路径。没有施加世界平移常数。
- H09 observer 反例覆盖 `seat=1`、owner 0、`moves=[]`，并分别路由友方与敌方英雄；错误 owner 和重复 authority target 拒绝。

### H11 source 尾钟复查

- 导出 source 的 FSM 仍按原时序推进：`ActionState` 在 `1/6s` 后进入 `Minion Spawn`；再过 `1/3s` 时开始 Actor Reveal；Reveal 持续 `.5s` 后在累计 `1.0s` 执行 `FinalizeCustomSpawn`。Editor 合同在 `.5s` 前后实际检查 Actor 可见状态，并在 `1.0s` 检查 Finalize 次数。
- `ParticlePlayAction` 从年龄 0 启动 Node 18 和子级 Node 19；两者非循环，最晚排放/粒子寿命结束年龄分别为 3.0/3.3s。Timeline 的动画和控制片段最晚结束年龄不超过 3.25s。Node 20 的 token WAV 为 192634 帧、48kHz，时长 4.013208s；Timeline 在 source 年龄 `1/6s` 开始，因此完整声音结束年龄为 4.179875s。
- H11 播放时钟完整保留 4.013208s token WAV，并额外保留 Unity 实际 `DSP buffer × buffer count / output sample rate` 的已排队缓冲时间，再推进到原 Timeline 结束年龄 5s。剩余空尾因此约 `.820125s - DSP 缓冲时间`；Prepare、主 FX 绘制和按钮绘制共享映射，声音 pitch 不变，输入门继续独立于该 source 尾段。
- Player report/analyzer 记录实际 source 年龄、映射年龄、wall time 和 Unity DSP 配置，并核对单调性、完整 WAV 与 DSP 排空窗口及 source 释放时长。`Supports` 同时校验源 FSM Wait、Timeline 与 WAV 时长，源数据不匹配则关闭此路线。

## 定向验证

使用项目虚拟环境运行：

```powershell
E:/HS/HearthstoneClone/.venv/Scripts/python.exe -m pytest tests/test_warrior_player_replay_contract.py tests/test_demon_hunter_player_replay_contract.py -q
```

首提交结果：37 passed（战士/恶魔猎手 Player 合同及 Presentation metadata、Death Knight importer 合同）。

H11 回放/投影合同与 importer 合同：

```powershell
E:/HS/HearthstoneClone/.venv/Scripts/python.exe -m pytest tests/test_death_knight_player_replay_contract.py tests/test_death_knight_hero_power_import.py -q
```

结果：20 passed。H09 observer metadata 合同另行运行 `tests/test_presentation_metadata.py`，9 passed；`git diff --check` 通过。

## 未验证项

- 本提交没有运行 Unity、Build 或 Player。H01/H10 双席位真实 Player 的实际提交时延、native source 同时 active、authority 接受和尾段清理仍须由 root 串行执行正式回放。
- 本轮尚未运行 Unity、Build 或 Player。root 需在 layer 30 主相机隔离已整合后，实际构建并验证 H11 时钟全程、两席位 `.2s` 输入门、完整 token WAV/DSP 排空窗口和 `input-unlocked-during-source.png`；截图左下幽灵是否消失仍待 Player 图像确认。
- Editor 的 H11 Actor Reveal/Finalize 边界检查和 DH 时钟回归夹具修复尚未由本 worktree 运行 Unity。不得将源时钟合同或 focused pytest 结果描述为 Editor/Player 验收通过。
- 德鲁伊 0.9 倍缩放只记录参数值，尚未通过 Player 截图复核。
