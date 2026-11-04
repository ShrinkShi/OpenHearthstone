# 装备状态与战斗结算节奏（2026-10-09）

在现有 master/f7df7e5 上修复菜单正文、场上武器、技能用后背面、刀扇连锁时序和牌库。基线实跑 585 项规则测试；没有扩展卡池或重写服务器。未使用 Computer Use，未操作官方窗口。

## 画面修正

- 菜单缺块：ButtonsA_Dynamic 正文 alpha 用于原 Shader，直接 GUI 透明绘制留下两个端头。在缓存的衍生纹理恢复正文 alpha，三段拉伸保留金属边框；红色认输按钮不改正文颜色。原文件不修改。
- 场上武器：Card_InPlay_HeroWeapon_v2 圆形框，第二 submesh 用实际武器原画。攻击为白色描边数字，耐久用 Wep_SheildBroken 专用图标和状态颜色。手牌继续使用完整卡面，新增独立 weapon 编辑页。
- 技能已用：Card_InPlay_HeroPowerV2 第三 submesh 的 Hero_Power_D 棕色符文背面，隐藏费用；悬停详情仍显示正常正面/文字。used 取实际已使用且耗尽状态，不能以低法力推断。
- 牌库：用 GenFX_Deck_1/25/50/75/Full 的真实厚度网格替换重复平面卡背。保持竖向卡片和侧边厚度，沿用用户牌库校对。空牌库不画实体，数量提示仍可用。
- 武器动画按实体类型绘制，避免变成随从。离屏材质不二次克隆，测试恢复 active RenderTexture 后销毁。

## 刀扇纵切片

EX1_129 的 CardDef m_PlayEffectDef.m_SpellPath 指向 `FanOfKnives_FX.prefab:22bcac556a7b1cc4a9c378da1e677e0c`。解析关联音轨、材质、纹理及动画/粒子参数；播放原 Rogue_FanOfKnives_Cast_1 / Impact_1，绘制原 GenFX_ValeeraDagger / GenFX_FanOfKnivesTrail。飞行、混合与时序由本项目重实现，不等于执行原控制脚本。

固定真实规则场景：盗贼释放刀扇，对面有淡水鳄、铸甲师和野猪。服务器即时结算三次伤害、三次增甲、抽牌及死亡。客户端调校值如下：

| 阶段 | 本项目时轴（秒） | 可见状态 |
|---|---|---|
| 卡牌离手 / 显牌 / 淡出 | 0.00～0.72 | 扣费、移出手牌，离手动画继续绘制原画 |
| 刀扇飞行 / 施放声 | 0.72～1.57 | 匕首/拖尾飞向已确认的受伤目标 |
| 受伤 / 数字 / 命中声 | 1.57～2.12 | 各目标读取公开伤害后数值 |
| 铸甲师闪电 | 2.16、2.84、3.52 开始，各 0.50 | 三次实际监听器触发分别播放 |
| 增甲 / 护甲更新 | 2.40、3.08、3.76 开始，各 0.36 | 护甲依次 0→1→2→3 |
| 抽牌 | 4.16～4.78 | 己方已知牌从牌库飞入手牌 |
| 野猪死亡 | 4.56～5.20 | 塌缩结束才移除实体 |
| 历史记录 | 整批结束后，约 5.26 | 写入公开出牌记录 |

这些不是已测得的官方时长。普通输入等待播放；认输可跳过队列立即提交。F3 开发模式下 F4 两倍速、F5 跳过，服务器不等动画。

## 数据与同步

Match 适配器共用 card_view，保存公开实体在 Hit/Heal/GainArmor/Buff/Summon/Morph 等回调的中间数值。Draw/Give 不带牌名，不泄露隐藏手牌。Unity 复制 before，按 cue.apply 更新 Display，最后合并完整 after；BatchCompleted 驱动历史，收网络包时不提前记历史。

事件上限 80→512，覆盖七铸甲师的 49 次触发；更长连锁仍可能截断。未覆盖的光环、控制权变化等中间状态仍在末尾校正，不声称完成任意复杂连锁全状态重放。客户端不计算权威快照或 RNG。

## 来源与复现

[battle-pacing-assets.json](../manifests/battle-pacing-assets.json) 记录 12 条使用来源、17 个研究对象。原二进制、动画/粒子类型树和截图只留本地；沿用既有索引、Resource Lab、UnityPy、写锁和缓存，官方安装与参考仓库不修改。

```powershell
.\.venv\Scripts\python.exe -X utf8 -m tools.research_battle_pacing --card EX1_129
.\.venv\Scripts\python.exe -X utf8 -m tools.battle_pacing_assets
.\.venv\Scripts\python.exe -X utf8 -m tools.visual_scenarios
.\.venv\Scripts\python.exe -m pytest tests -q
```

新增 `--visual-test weapon_equipped / weapon_summon / power_used / power_used_hover / fan_of_knives / fan_triggers`，支持固定实体/过程帧/结尾截图。后台 Player GUI 不可靠，未把黑帧冒充有效截图。`tests/visual/current/interaction-models` 是实际 GPU 离屏组件渲染，不是完整战斗截图。

六张用户附件分开归档：前两张复刻问题图在 `tests/visual/baseline/pacing`，后四张原版图在 `tests/visual/reference/oracle/pacing`。局部裁切、不同棋盘主题不能做同状态像素评分。

## 实测

- Python：588 passed，38.92 秒；刀扇中间数值/隐私、49 次触发、技能重置。
- Windows x64 构建：271 表现 + 21 校对 + 33 交互 + 33 反馈 + 14 输入/TCP = 372 条断言。覆盖离手→飞行→伤害→扳机、逐次增甲、死亡清除、延迟历史、武器不进随从槽和忙时认输。
- GPU 离屏：51 项；菜单正文 alpha、武器非白块、技能背面、五档牌库、刀扇纹理及原有资源。材质克隆和 active RT 销毁警告已修复。
- 前次构建原生自动局：MatchId `6bd46dc7461f46fd850e3388e7dd3b19`，13 回合/v54，Shrink1 盗贼 LOST（-1 HP），Shrink2 战士 WON（5 HP）；双方 public_hash `215339371af2092986ca09e91768a8dfa53df76dbaaac736a4f3341852ca1588`。独立 Profile pacing_oct09_1/2、端口 47248，普通协议动作，不是人工局。
- 最终构建原生自动局：MatchId `e7a5b492990c4e118cac02fa63f3222c`，13 回合/v53，Shrink1 盗贼 WON（11 HP），Shrink2 战士 LOST（-1 HP）；双方 public_hash `488b2dc67065c8d3014fb7a67785ebf65b46d745adba57c0839b46b6546a4ad2`。独立 Profile pacing_release_oct09_1/2、端口 47249；两份客户端日志无 Exception/error/NullReference/KeyNotFound。
- 最终局两侧队列峰值均为 1，托管内存样本约 7.48/7.76 MB。后台不绘制正常 GUI（card_render_count=0），约 60 FPS 样本不能作为整屏性能验收，也不代表长期 GC/泄漏测试。
- 用户最新校对 SHA256 保持 `BCCA7E3DCCCB0A2418F487C2281B93913B96EC7260EEA44B91EEFF58CE48F2F9`，不覆盖配置、原玩家 Profile 或原游戏文件。
- 最后确认默认端口 37240 的旧项目服务器只有监听、没有客户端连接，清理该空闲实例并后台启动更新服务器；Start-LocalDuel.cmd 可以沿用默认入口。运行中的官方游戏窗口未操作。

## 未完成

原动画状态机、全部专用 Shader、粒子完整重放未完成；不是所有卡牌原特效完成声明。牌库用原模型，但槽口遮挡、不同棋盘全屏位置和原光照人工核对未完成。整屏菜单/背面悬停、连续帧比较和人工完整比赛未完成。后台自动局不绘制正常 GUI，不能证明完整画面 1080p/60 FPS。
