# V026 Hub 独立复验 · r5

日期：2026-10-31。固定候选 `a8b832b79f61ebc155049efb398c195bec5eb752`（`refs/verification/f026-r5`），Player DLL SHA-256 `B346F5C2A072F78B3B4C1F6FE2FCE71239EE14D7DCC1F5222DC83B6E00E6D0F1`。结论：**FAIL，开包页背景纹理烘焙的 Back 按钮仍露在公共 gear 右侧；其余完成项为有界证据**。本轮只读审阅源码与运行证据，未修改业务代码。旧候选 r3 的失败报告保留在 `V026-hub.md`。

## 六项验收

1. **主界面底栏：有界通过。** r5菜单截图显示右下 gear、左侧钱包及左下好友/时钟。任务页截图的 gear 右侧是黑色底，没有相同按钮残片；该页不应与开包页问题混为一谈。
2. **菜单与选项：有界通过。** r5 Editor GameView 回放实开游戏菜单与双列选项，保留账号/会话入口；帧率值修改后保存并重读。退出入口显示并绑定退出处理器，未实际关闭进程。
3. **好友：有界通过，界面路径有缺口。** r5 Editor GUI 回放包含好友申请、第二账号接受、在线/离线及移除。拒绝、取消、隐私切换没有在这两份生产回放中点击；这些功能仍需以服务测试/API证据为准，不能由当前截图单独证明。
4. **批量购买：有界通过。** Editor 回放打开多数量弹窗并显示数量2/200金币；回放包含取消且未购买，以及随后一次成功购买请求。Player 直接 handler 回放共26条成功请求，包含购买、开包及五张卡牌揭晓。该 Player 回放没有OS鼠标输入。
5. **开包页：FAIL，标题/裁切已修。** r5 两份库存截图在原生标题牌显示“开包”，木框外溢灰沿已裁去；左侧卡包托盘、钱包、gear和5张卡牌揭晓可见，页面无外侧黑边，也无旧说明文案。但gear右侧仍露出金色横按钮残片。只读核对 `E:/HS/HearthstoneClone/local/runtime/e08f3f20f4954688221184c4.png` 后确认，PackOpeningTray源纹理右下已烘焙英文Back按钮；动态BackButton未覆盖该纹理区域。圆盘/托盘颜色使用静态neutral材质与Repeat纹理兼容处理；原版完整光照、Shader与动画控制器未恢复，不宣称逐像素/动态一致。
6. **任务及F6：有界通过，实际GUI持久化操作未闭合。** 生产回放显示每日/每周各4项、红卷角重抽图标，F6离线预览收集76个布局目标且不触发账号请求。Build r6 的 `UICalibrationChecks` 临时文件测试通过110项，覆盖任务布局保存、重读、复位及保留其他页校准；字号、字体样式编辑控件存在。但本轮未在F6界面实际编辑位置/字体再保存、重读与复位，因此不把模型检查等同于GUI端到端验证。

## 证据身份与边界

- Editor：`E:/HS/HearthstoneClone/work/f024-account-editor-gui-f026-r5/`，`result=ok`，14步、189条GameView输入（63次手势）、31/31成功请求、34张截图。独立分析器为7/7检查通过。输入是Editor生产OnGUI队列，不是OS鼠标或Standalone GUI。
- Windows Player：`E:/HS/HearthstoneClone/work/f026-account-player-r5/summary.json`，`result=ok`，13步、26/26成功请求、29张截图、无错误/超时，Player exit 0。`input_mode=direct-production-handler`、`gui_inputs=[]`，故不证明Windows GUI输入。
- 关键截图：Editor `E:/HS/HearthstoneClone/work/f024-account-editor-gui-f026-r5/captures/f024ed-26640-790552f1-account-replay-packs-inventory.png`；Player `E:/HS/HearthstoneClone/work/f026-account-player-r5/captures/f024acct-37172-f902033b35-account-replay-packs-inventory.png`。另对照 Editor `.../f024ed-26640-790552f1-account-replay-quests.png`，未见同一金色按钮残留。
- Editor 与 Player provenance 的 before/after 均固定在候选 `a8b832b…`，各314个记录文件一致；Player DLL为上列哈希。主目录 `Main.unity` 两侧 SHA-256 均为 `D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51`，用户校准文件两侧均为 `B75BCD140FF5F3AC8D0EFCB7D592CB14BAA829A6E4D82FF8EB06AD7AF731BDED`。Build r6 exit 0，日志报告 `UI CALIBRATION CHECKS PASSED: 110`。
