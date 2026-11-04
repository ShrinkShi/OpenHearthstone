# 原生视觉回归

2026-10-09：新增 hero_combat、hero_combat_trigger、status_badges、arcane_explosion、blizzard、flamestrike、polymorph、holy_nova、nightblade、si7_agent、big_game_hunter，均由 Match.apply 生成动作快照。

`WindowRenderChecks.Run` 离屏检查 1920×1080 / 1220×1110 / 2335×515 的原菜单与棋盘组件，输出 current/window-feedback。`AssociatedRenderChecks.Run` 检查全部可见关联纹理的边缘/可见性并输出 7 个材质合成样板到 current/associated-effects。两个 GPU 入口都不能加 -nographics，均不操作桌面。它们不包含完整 GUI/输入，不能代替人工比赛或原动画连续帧核验。当前结果和恢复边界见 [WINDOW_FEEDBACK](../../docs/WINDOW_FEEDBACK.md)。

固定 Windows 客户区 **1920×1080**。生成权威测试快照并启动实际 Windows Player：

```powershell
.\.venv\Scripts\python.exe -X utf8 -m tools.visual_scenarios
.\.venv\Scripts\python.exe -X utf8 -m tools.visual_capture
.\.venv\Scripts\python.exe -X utf8 -m tools.visual_capture --scenes hand1,hand2,hand3,hand5,hand6,hand8,hand9,hand10,minions2,minions3,minions5,minions6,minions7,card_draw
.\.venv\Scripts\python.exe -X utf8 -m tools.visual_capture --scenes mulligan,hand4,minion_summon,minion_attack,hero_power,end_turn --reference tests/visual/reference/internal
```

工具检查退出码、日志异常、截图新鲜度、1920×1080 分辨率及黑帧。过程场景产生 `visual-<scene>-action.png` 与结束帧 `visual-<scene>.png`，过程帧使用 Presentation.Seek 固定采样。旧 1600×1000 图不能直接比较。

- [x] mulligan：真实引擎固定起手快照
- [x] board：固定英雄、随从、法术/随从/武器手牌
- [x] card_gallery：法术、随从、武器、传说、死亡骑士正向卡面
- [x] main_menu：官方主菜单原生 1080p 图，保存在 reference/oracle
- [x] card_draw / minion_summon / minion_attack：固定动画过程帧
- [x] spell_cast / death / hero_power：固定动画过程帧
- [x] hand1～10 / minions1～7：布局样本
- [ ] 官方战斗参考与逐帧时序比对

TASK-0004双卡关键帧：运行 `tools/Invoke-Task4VisualEvidence.ps1`，实际规则输入、两次新Unity图形进程、Birth/Early/Mid/Arrival/Hit/Death及Cleanup共28主帧，另存C/K、遥测和4张contact sheet。输出 `work/task4/`，机器索引 `manifests/task-0004-visual-evidence.json`；只读官方输入清点和Shader审计一并生成。已有18张官方UI图不包含双卡序列，官方分项均Unknown、Task BLOCKED；不能以内部重放一致称官方通过。来源、机位/时钟/种子、继续命令和完整限制见 [TASK4_EVIDENCE](../../docs/visual/TASK4_EVIDENCE.md)。

截图为原生 Windows 客户端输出，保存在忽略目录。`--visual-state` 是测试入口；完整联机验收使用正常匹配与动作，未使用此入口或改写状态。

默认覆盖 match_start、mulligan、turn_start、hand4、hand7、minion_on_board、multiple_minions、card_hover、card_drag、minion_summon、minion_attack、hero_power、spell_cast、damage、death、end_turn、victory、defeat。`card_drag` 固定真实手牌实体的姿态，截图本身不能代替鼠标拖放验收。

`baseline/` 存改动前截图；`reference/oracle/` 只放官方图（main_menu 及用户提供的七张 interaction 参考）；`reference/internal/` 放明确标注的本项目基线；`current/` 是新截图；`diff/` 是像素差/dHash。内部图不能冒充官方 Reference。不同卡牌/棋盘状态不能直接当作同场景相似度。详见 [VISUAL_DIFF](../../docs/VISUAL_DIFF.md)。

UI 校对器的真实 Player 页面与数量边界截图：

```powershell
.\.venv\Scripts\python.exe -X utf8 -m tools.ui_editor_capture --counts
```

目前生成 16 页、1~10 张手牌、1~7 个随从共 33 个场景。上一版已验证 30 张，新三页整图采样未完成。可通过 `--layout` 指定校对 JSON，使用 `--output` 指定独立输出目录；截图工具将输出路径解析为绝对路径，避免 Unity 将相对路径保存到 Player Data 目录。操作说明见 [UI_EDITOR](../../docs/UI_EDITOR.md)。

新增交互场景：mulligan_hover、power_hover、power_disabled、minion_hover_left/right、end_turn_exhausted、history/history_hover、stat_icons。后台运行可用 `--background`，只以不激活窗口的方式启动本项目进程；后台/遮挡下整图可能不可渲染，黑图按失败处理。本轮后台图是黑帧，不计入通过。

`InteractionRenderChecks.Run` 在 Unity 编辑器图形批处理下将实际资产渲染到 RenderTexture，独立输出 `current/interaction-models/` 和 render-report.json。不要加 `-nographics`。十项检查包括技能卷轴/圆盘/组合、护甲、耐久、生命、两类历史、随从牌和轮廓；它们验证资源方向和模型组合，不能代替 GUI 悬停、点击或完整比赛截图。
