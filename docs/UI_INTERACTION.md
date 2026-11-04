# 战斗高亮、详情与历史（2026-10-09）

本轮在现有 `cff2635` 架构上增量修改。用户已保存的 29 个 UI 校对元素保持不变；文件 SHA256 为 `05A3B53B95C57D93203D09A7AA9BFEAC64880E86D2ACFAFE2E6323332318B488`。不修改规则、服务器、协议和官方安装。

## 实际实现

| 用户要求 | 行为 |
|---|---|
| 起手留牌 | 可交互且保留的牌带绿色轮廓；悬停叠加较窄白光；替换牌取消高亮，继续显示原版红 X 和“替换” |
| 纵向空间 | 相机横向投影倍率 0.92、纵向 1.035；手牌中心从 1118 提到 1048；英雄/随从/技能同步上移，保持非 Hover 手牌不盖英雄血量 |
| 回合按钮 | 本方可操作且无 `play/power/attack/choose` 合法动作时为绿色并发光；存在任何上述动作时为黄色；对方回合禁用 |
| 技能就绪 | 根据服务器合法 `power` 动作显示绿色轮廓；已用、法力不足、目标不合法和非本方回合均不会自行猜测可用 |
| 技能详情 | 悬停己方或敌方技能显示蓝色卷轴卡框、圆形技能、费用、名称及说明；不会重复“英雄技能”前缀 |
| 场上随从详情 | 悬停叠白色轮廓；左半场随从的卡牌详情在右侧，右半场在左侧；边缘限制在设计画面内 |
| 左侧历史 | 出牌、攻击、英雄技能、奥秘揭示/触发形成图标；悬停显示当时的卡牌，攻击另显示目标；8 行视窗，滚轮回看，最多 40 条 |
| 护甲 / 武器耐久 | 独立使用 `Hero_Armor` 银盾和 `Wep_SheildBroken` 破裂盾网格；不使用生命水滴，也不按灰色推断为蓝色法力宝石 |

普通牌库、回合、换牌确认和动作仍使用原协议。绿色回合按钮是提示，仍须玩家点击结束回合。

## 校对与保存

沿用 `Start-UIEditor.cmd` / F6 / Ctrl+S / F7。新增“技能详情卡”“护甲图标”“武器耐久图标”三页，战场页增加操作历史区域。详情卡的卡框、头像、费用、名称、说明可分别校对。

既有 `card.weapon/health` ID 保留用于耐久位置，避免丢失用户保存的武器布局；图标本身使用新的 `durability/model`、`durability/number`。护甲使用 `armor/model`、`armor/number`，不会继承旧 `health/model` 的翻转。布局变更仍是默认值与校对矩阵叠加，不写用户 JSON。

## 数据与可见性

`BattleInteraction` 只读取权威合法动作。`BattleHistory` 保留每个包的动作及 before 快照副本，按 MatchId/version 去重；显示限制为已播放到的版本。死亡目标仍能查看，详情实体 ID 为 0，不覆盖真实实体的输入区域。

对方抽牌/生成牌不进入历史；未揭示敌方奥秘只显示卡背。返回菜单/切换比赛清空历史。原服务器事件可能含隐藏牌 ID，此处主动按事件类型及奥秘变化过滤，历史不展示这些信息。

轮廓使用 GPU 从当前模型 alpha 生成，缓存最多 64 张。原画 alpha 常承载专用 Shader 参数，因此原画采用网格遮罩，避免绿色穿进脸部/衣服。外层绿色与较窄白色独立混合；不是矩形描边。回合按钮的圆角光晕是本项目重实现。

## 验证与限制

本轮 Python 规则/网络回归 **577 passed / 21.55 秒**。构建通过 **245 条表现断言、21 条校对断言、33 条新交互断言**，包括合法动作提示、详情左右侧、Hover 可见性、历史去重、快照独立性、敌方隐藏信息、死亡目标和换 MatchId 清理。

最终构建的后台原生双客户端通过正常匹配/动作协议完成比赛 `71cd7f4c1b7e46b5bae71425a86055fe`：13 回合 / v44，Shrink1 法师 LOST（0 HP），Shrink2 战士 WON（17 HP）；独立 Profile 为 interactionfinal1/2，公共哈希均为 `5f56c38418e9993652be84b2ff72dba84d9f9046957d1ef661082c8ed086a2df`。没有改写比赛状态或使用视觉 fixture；自动输入，不是人工完整对局。运行日志无规则/表现异常，但 batch Player 的三次整屏截图各自失败，不能当作画面证据。测试进程和独立端口 37255 的服务器已关闭，不影响用户正在运行的游戏。

用户要求本轮不使用 Computer Use；未调用桌面操作工具。后台整屏 Player 采样产生 1680×1050 黑帧，已被截图验证拒绝，不能算作合格实机截图。改用 Unity 图形设备直接渲染到 RenderTexture，检查模型方向、图标、详情卡组合和历史框；这些 **10 张离屏模型图不是完整比赛截图**，保存于 `tests/visual/current/interaction-models/`。

六项交互已接入代码；最终完整画面的人工悬停/点击核验**未完成**，不能声称已与官方完全一致。整屏背景缩放、白绿光晕强度、手牌与棋盘的比例仍需在正常窗口复核。原 Shader 的历史裁切参数已读取并由本项目 UV Shader 解释，精确采样一致性未完成。

新增固定场景：`mulligan_hover`、`power_hover`、`power_disabled`、`minion_hover_left/right`、`end_turn_exhausted`、`history/history_hover`、`stat_icons`。快照通过现有引擎合法动作构造。

```powershell
.\.venv\Scripts\python.exe -X utf8 -m tools.interaction_assets
.\.venv\Scripts\python.exe -X utf8 -m tools.visual_scenarios
.\.venv\Scripts\python.exe -X utf8 -m tools.visual_capture --scenes mulligan_hover,power_hover,minion_hover_left,minion_hover_right,end_turn_exhausted,history_hover,stat_icons --output tests/visual/current/interaction-final
```

`--background` 使用不激活窗口的启动标志；隐藏、被遮挡或独占全屏下的原生 GUI 渲染可能不可用，脚本不会把黑图当成功。离屏资源检查使用 Unity 编辑器 `-batchmode -executeMethod InteractionRenderChecks.Run`，不要加 `-nographics`。
