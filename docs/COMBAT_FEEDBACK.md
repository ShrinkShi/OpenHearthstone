# 手牌与交战反馈（2026-10-09）

继续 master/e8611c5，开始时 Git 干净、588 项规则基线实跑通过。没有扩展卡池、替换架构、操作官方窗口或使用 Computer Use。

## 七项反馈

1. 手牌单步上限 92、中心跨度上限 590，仍有扇形旋转/重叠/Hover。经本机校对后计算法力计数框，整个扇形向左避让，不逐张挤到同一点；数量、Hover、配置 Revision 变化才重算。编辑器使用同一函数。保留用户配置 SHA256 `6D90878CA6393C738FCCA7207C80BCA1350541365BA69AAC9637D0CD520B6710`。
2. 原 `GenFX_Taunt_Shield` 网格及 `GenFX_Taunt_Shield-mat_tauntshield` 材质绘制在肖像后，自画灰环已移除。新 `minion/taunt` 校对槽允许人工调整。
3. 服务器从 Fireplace 的 `has_combo` / `controller.combo` 输出 `combo_ready`；可打出的连击牌显示黄光，其他可打牌绿光。未满足连击或不可打出不冒充黄光，换牌阶段继续绿光。
4. 普通交战双方伤害在同一命中时刻显示，纯交战死亡并行；如存在实际监听器，其动画仍逐个播放。服务器始终一次性完成合法结算，不等待动画。死亡连锁等额外结算保持顺序。
5. `attack_buffed` 来自当前与基础攻击的比较，卡面、场上随从、武器、英雄统一使用绿字；数值恢复后回白。编辑器的自定义填充仍优先，描边/阴影不改变状态色。
6. 带非零合法目标的随从先保存放置位置和 index，再等待目标。当前连击未激活时 SI:7 Agent 的合法动作为 target=0，不显示多余目标箭头。箭头在卡面/VFX 后绘制；已放置的随从不再带着完整卡面跟鼠标移动。输入/TCP 重放与真实服务器两侧测试覆盖这些分支。
7. 背刺、刺骨、冷血新增小集合。实际关联音轨：背刺 Shared_SneakyMissileStandard2_Cast_1 / Shared_SneakyAttackImpactStandard_1；刺骨 Rogue_Eviscerate_Cast_1 / Impact_1；冷血 Shared_Rage_PreCast_1 / Shared_Rage_Start_1。刺骨用原匕首与刀痕；冷血用原光点与脸部贴图。背刺视觉复用刺骨匕首/刀痕，不是自己的全部原素材恢复。卡牌离手后播放效果，伤害/增益随后显示。

## 来源与复现

[combat-feedback-assets.json](../manifests/combat-feedback-assets.json) 保存 11 条使用来源、58 个研究对象和三个 CardDef GUID 引用；本机 Registry 1026 项。原贴图/音轨、AnimationClip/ParticleSystem 参数只在忽略目录 local/runtime。参考仓库及官方安装未写入。效果注册使用锁内增量合并，不覆盖其他导出器的卡牌。

```powershell
.\.venv\Scripts\python.exe -X utf8 -m tools.combat_feedback_assets --research
.\.venv\Scripts\python.exe -X utf8 -m tools.combat_feedback_assets
.\.venv\Scripts\python.exe -X utf8 -m tools.visual_scenarios
.\.venv\Scripts\python.exe -m pytest -q
```

新增固定状态：taunt、combo_inactive、combo_ready、ordinary_combat、combat_trigger、backstab、eviscerate、cold_blood；有动作场景附 after 快照，动作经过现有 Match.apply。后台 Player 不支持可靠完整 GUI 截图；未把黑帧当成功截图。

## 实测

- Python：594 passed，44.03 秒；连击重置、SI:7 条件目标、战吼目标、攻击增益/沉默、同动作双方死亡和效果增量合并均通过。
- Windows x64 构建成功：271 表现 + 21 校对 + 33 交互 + 33 反馈 + 18 输入/TCP + 1794 交战反馈 = 2170 条断言。其中 1760 条是默认及用户校对下，1～10 张/每个 Hover 槽的排序与计数避让组合，不能当作 1760 个新规则功能。
- Unity GPU 离屏 63 项通过，检查嘲讽/连击组合、十张手牌避让、原新增纹理、冷血粒子边缘透明，以及原有模型/音画资产。
- 双原生后台自动局一：盗贼/战士，端口 47251，Profile combat_oct09_1/2；MatchId `26df63a75fcc4159a8995f3a38e620a3`，14 回合/v57，Shrink1 LOST（-1 HP）、Shrink2 WON（10 HP），双方公共哈希 `ab98fdb32358664821e58b4aa9d4149c2651fb7a343be775a81e2151ee26423e`。
- 双原生后台自动局二：盗贼/法师，端口 47252，Profile combat_mage_oct09_1/2；MatchId `3080d1dfc9504c95a45121ec6ca288ac`，16 回合/v53，Shrink1 WON（20 HP）、Shrink2 LOST（-2 HP），双方公共哈希 `45b68b26f5a4c72c6014cbc92140b6ad5a891d234f5fcc447b68eea29638163e`。
- 两局使用实际 Windows exe、独立 Profile、正常 TCP 动作与本项目队列，未修改规则状态；不是人工局。四份客户端日志未发现 Exception/NullReference/KeyNotFound/error。第一局双方队列峰值 1、托管内存约 7.76/7.74 MB；card_render_count=0，后台 GUI 帧率不能作为整屏性能验收。
- 测试完成后清理本轮隐藏进程。默认端口 37240 的旧项目服务器确认只有监听、没有玩家连接，更新为空闲的新版本；Start-LocalDuel.cmd 可沿用，不终止正在进行的对局。

## 截图性质与未完成

`tests/visual/current/interaction-models/taunt-composite.png` 是原嘲讽框与森金肖像的 GPU 组合；`combo-yellow-components.png` 是迪菲亚头目的黄光卡框组合；`hand10-mana-components.png` 是原棋盘、十张手牌模型与校对计数框组合。未绘制 GUI 文字、英雄和其他 UI，不是完整运行截图，也没有使用官方截图铺底。

未完成：本轮整屏人工操作和完整局；原连续帧/音画时间点核验；嘲讽 Gleam/盾破全动画；背刺全部原视觉；全部现有卡牌的专用动画、原 Animator/Shader/ParticleSystem 完整重放。冷血的软边混合、匕首轨迹、黄光和布局是本项目重实现。后台测试的帧率不能证明正常 GUI 1080p/60 FPS。极端人工卡面缩放/移动、拖动卡跨过计数框及混合抉择目标还需人工核对。
