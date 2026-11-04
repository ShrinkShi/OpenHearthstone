# 棋盘、菜单和战斗提示修正（2026-10-09）

沿用现有 Unity 客户端、Python 权威规则和 LAN 协议，未扩展卡池。官方安装及 Resource Lab 只读，未使用 Computer Use。开始时 master 工作区干净，基线 e110eb9，实际运行 583 项规则测试通过。

## 棋盘来源与角度

棋盘不是游戏截图或 full_board.png。`tools.presentation_assets` 从本机 `essential_base_qualityhigh_global-content-0.unity3d` 的 `Board_STR` 恢复层级、网格、UV 和材质纹理，缓存为 `local/runtime/SCENE_BOARD.scene.json`。桌面由八块 PlaySurface Projected 网格组成，边框、英雄托盘及四角道具另有网格；木桌来自同包 TableTop。原素材是 3D 几何结合绘制纹理，部分透视/明暗已烘在贴图中。

此前相机额外偏移 `-distance * .105`，产生约 6° 倾斜。现在相机旋转固定为 `(90,0,0)`，从正上方取景；用户保存的相机/棋盘/背景校对叠加仍有效。Shader、动画脚本和光照并未完整恢复。

## 实际改动

- 右下菜单使用原 GameMenuButton_Default 齿轮网格/ChatUI 图集。游戏菜单使用原 OptionsFrame、ButtonsA_Dynamic、Button_Concede_Red，提供选项、认输、继续；选项可调音乐/音效音量，目前只在当前进程保留。F6 仍进入校对。菜单阻断底层拾牌、技能、换牌和结束回合，不暂停服务器。
- 回合按钮使用 Button_EndTurn 原网格/贴图，独立相机渲染厚度和正背面。读取原 ENDTURN_YOUR_TURN / ENDTURN_WAITING 四元数采样，旋转段分别为 0.5 / 0.583 秒；翻转中淡去平面文字、禁用按钮。服务器立即结算，规则不等动画。
- 描述默认正常字重，只给关键词 `<b>`，保留英文原大小写。编辑器人为指定整段粗体/厚描边仍是可控制样式。
- 敌方法力只显示数字，己方保留水晶。FrameManaEnemy 裸网格材质无贴图，蓝色计数气泡实际在 STR_FRAME 图集中；原 UV 范围重新映射到该区域，不使用白色占位或程序生成气泡。
- 移除技能整张乘 0.65 灰色及扳机深金色叠乘，清理离屏材质残留 Multiply/Additive 状态。就绪绿光、悬停白光和实际 Trigger 闪光保留。
- 己方牌库悬停同时显示牌库/手牌数；敌方手牌、牌库分别显示公开数量。灰色提示框来自 TooltipFrame1；命中使用校对后的牌库矩阵和敌方扇形，不泄露隐藏牌。
- Summon cue 使用原召唤环、蓝色爆发、实际原粒子材质引用的蓝色光点。睡觉 Z 从 Card_Play_Ally_Zzz 的实际粒子材质恢复，向上漂浮并淡入淡出。`asleep` 直接取 Fireplace 属性：冲锋/突袭不睡，次回合醒，攻击次数耗尽不重新睡。
- 编辑器分类/部件独立滚动：分类 122~540，标题 551~585，部件 593~1008，不重叠；战场随从页也可调整睡觉 Z。

## 验证及来源

提取命令：`.venv\Scripts\python.exe -X utf8 -m tools.board_ui_assets`。来源见 [board-ui-assets.json](../manifests/board-ui-assets.json)；PNG、网格、动画采样及粒子参数只留本机，不提交 Git。

- Python 最终回归 585 passed，18.86 秒，包括睡觉/醒来/冲锋/突袭及双方公开哈希。
- Unity 构建：245 表现 + 21 校对 + 33 交互 + 33 反馈 + 12 输入/TCP 重放 = 344 条断言。
- GPU 离屏 37 项通过：按钮正/中/背面、气泡蓝色、齿轮、召唤素材/透明角、正上方相机、1080p 棋盘。
- 清理临时网格/材质/RenderTexture 时按 Unity 运行或编辑模式分别销毁，避免离屏检查的编辑模式销毁警告；原资产缓存不受影响。
- Windows 双原生客户端后台自动完整比赛共两局。前次构建：独立 Profile `boardui_oct09_1/2`、端口 47247，MatchId `9ea1164e10a94fe59b11c61c94afac69`，14 回合/v53，Shrink1 WON（10 血）、Shrink2 LOST（0 血）。双方 `public_hash` 同为 `96b1f7568d9f5db0133fdd4bf65c3f883272154bc715db863f80d6a595a8123c`。
- 清理警告后最终构建：独立 Profile `boardui_final_oct09_1/2`、端口 47247，MatchId `205025ef280445428df1338f94cdd0cd`，14 回合/v51，Shrink1 LOST（0 血）、Shrink2 WON（1 血）。双方 `public_hash` 同为 `ee690eaf279a75ba264481eff80636f12bdd2ebfc9d0d555662db8e22b01280f`，客户端日志无异常。后台样本不能作为整屏 UI/60 FPS 验收。
- 用户校对 SHA256 保持 `3E78EFDF549275A590A9430B7F4143732C30111092712DDFF6921F6874600E08`，未覆盖布局或现有玩家 Profile。

新增固定场景 `game_menu`、`game_options`、`deck_hover_own`、`deck_hover_enemy`、`hand_hover_enemy`、`minion_sleep`；通过 `tools.visual_scenarios` 生成，可用 `--visual-test <name>` 在正常窗口核对。

`tests/visual/current/interaction-models/board-top-down.png` 是棋盘离屏渲染，**不含战斗 GUI**；end-turn-front/flip/back 是模型采样，不能冒充完整实机截图。本轮整屏鼠标/菜单/滚动人工验收未完成，新的整场 60 FPS 指标未测。

## 未完成

原 GameMenu 控制脚本、完整专用 Shader、Animator 和 ParticleSystem 执行未恢复。菜单文字布局、召唤光点轨迹、睡觉运动由本项目组合/重实现，需继续人工校对；召唤粒子时序不是已测得的原版时序。
