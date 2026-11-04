# V025 Hub 独立验收

日期：2026-10-30。固定候选 `ac95cedf94b8a18e9ef077606bda56572f99721a`，Unity Player DLL SHA-256 `F07640E52B623AD035784D662E94568308FE0D133A809E6266515E803B9FE13A`。结论：本轮四项用户反馈在下述本地有界范围内通过；TASK-0011/0012仍不代表完整旧版视觉验收。

## 本轮结果

1. 商店：Editor截图目录`work/f024-account-editor-gui-f025-r7/captures`及同DLL Standalone目录`work/f025-account-player-r7/captures`中的top/middle/bottom截图显示16种真实包面素材（6+6+4），方向正、紫底连续，未见包外黑底。Editor `shop-top`在y=500、x=1675/1690/1705的R6像素为`(9,5,11)/(7,4,9)/(5,3,6)`，R7为`(56,34,71)/(56,34,71)/(53,31,68)`；与截图中连续紫底一致。R7只屏蔽已定位的`Shop_Box_3D/Shop_Box_Shadow`，不是全局提亮。
2. 任务与初始账号：GUI截图`work/f024-account-editor-gui-f025-r7/captures/f024ed-32980-f7f4de0e-account-replay-quests.png`显示每日4项、每周4项，日奖40/60/80/60金币、周奖80/120/160/120。42项定向pytest记录于`work/f025-focused-tests.log`（42 passed）。`test_legacy_three_slot_quests_migrate_without_resetting_or_repaying`核对既有六条任务ID/progress/completed/rerolls及1240金币不变，新槽补入、重复resume仍为8条且不重复补发；同组用例覆盖事件幂等与reroll。新账号源码/测试仍为185种基础与核心CardId（副本数按规则）、1000金币、0尘；grant仅来自基础免费卡集。
3. 收藏：`work/f024-account-editor-gui-f025-r7/captures/f024ed-32980-f7f4de0e-account-replay-collection-overview.png`显示原生纸页上的4×2卡页与独立右侧卡组栏；详情通过上层modal打开，`work/f024-account-editor-gui-f025-r7/captures/f024ed-32980-f7f4de0e-account-replay-collection-detail-closed.png`为关闭后状态，卡组栏仍在右侧。Editor原生事件收据中`CloseCollectionDetail`的mouseUp被消费。木框复用原`Frame_PC`资源并由当前场景布局派生；这支持本轮构图修正，不证明恢复了旧PC完整相机。
4. 设置与账号：右下“设置”可进入账号/会话页完成注册、退出、登录；同目录`settings-register.png`、`settings-account.png`与`persisted.png`记录相关界面，最后一张显示`settings.close`后回到先前组牌页。关闭mouseUp实际命中`CloseSettings`并消费事件；overlay打开时底层卡牌/卡包处理有显式拦截。账号请求在Editor回放中成功。

## 运行证据与边界

`work/f025-unity-build-r7.exit`为0。Editor分析器`work/f025-editor-r7-analysis.json`结果`ok`，7/7项为true：153条输入收据、51次手势、26个成功RPC、25张1920×1080图。输入模式是Unity Editor原生GameView队列，实际由生产OnGUI handler处理；账户字段由回放预填，未证明键盘输入或OS鼠标/键盘行为。Editor provenance `work/f024-account-editor-gui-f025-r7/provenance.json`前后固定同一源码快照、271份源码和11项运行工件，DLL一致。

Windows Standalone `work/f025-account-player-r7/summary.json`结果`ok`：8步、14个成功RPC、22张FullHD图、Player exit 0；输入模式为`direct-production-handler`且`gui_inputs`为空，故它证明同DLL生产handler与画面运行，不是Standalone GUI输入验收。Standalone provenance `work/f025-account-player-r7/provenance.json`前后源码、DLL、EXE、包元数据和registry均未变化，DLL与Editor归档一致。R1–R6退修与本轮完整工件路径见`docs/verification/F025-hub-reference-corrections.md`。

本报告通过范围不包含完整旧PC Collection相机、未解析Shader/动态、逐像素官方保真，也不包含OS输入或Standalone真实GUI点击。原相机与完整视觉保真仍为Unknown，因此TASK-0011/0012继续`IN_PROGRESS`。
