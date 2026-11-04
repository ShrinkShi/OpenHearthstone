# V026 Hub 独立验收

## 当前结论：有界通过

日期：2026-10-31。固定候选 `0f379fa069342f96a871f6dd8aac806f9ff8439b`（`refs/verification/f026-r6`），Build r7 exit 0，Player DLL SHA-256 `4255CA07261AF85A79BF52B54A4B3C83228D7798B881CC259A06D37AF457B210`。本轮复验未发现阻断缺陷，六项按下列证据边界有界通过；不据此宣称完整原版光照、Shader、动画控制器或逐像素一致。

### 六项结果

1. **主界面底栏：有界通过。** Editor与Player截图显示原齿轮、左侧钱包和左下好友人数/时钟；金币及奥尘在gear左边。当前截图没有发现r5开包页纹理Back残片。
2. **菜单与选项：有界通过。** Editor GameView回放实际打开游戏菜单和双列选项，保留账号与会话入口；帧率被修改、保存并重读。退出按钮显示且绑定退出处理，本轮没有实际退出进程。
3. **好友：有界通过。** Editor GUI回放覆盖申请、第二账号接受、在线/离线显示和移除；左列表右邻Add/Requests/Remove/Privacy。拒绝、取消和隐私选项没有在r6 GUI回放中点击。服务层这些路径由之前同一后端代码版本的临时DB测试覆盖（41项通过，见下方r3历史证据）；r6未重跑这组测试。
4. **批量购买：有界通过。** Editor GUI回放显示数量弹窗的加、减、取消与2包/200金币确认；取消无购买RPC，确认发出一次购买请求。服务端扣款、库存更新和原子/幂等测试见同一后端版本的临时DB历史证据。
5. **开包页：有界通过。** Editor与Player新截图显示原生牌匾“开包”、左侧卡包托盘、公共底栏中的钱包/gear及原生返回按钮；gear右侧不再露出金色残片，场景无外侧黑边/旧说明文字，成功开出并逐张揭示5张牌。r6恢复源BackButton位置，并把原生场景收在公共底栏上沿。材质仍以局部静态neutral兼容渲染，原版动态光照/SH和完整开包控制器未知。
6. **任务及F6：有界通过。** 每日/每周各4项及红卷角重抽图标在实际回放可见；F6离线预览显示4+4任务与76个布局目标，账号请求和快照不变。Build r7的 `UICalibrationChecks` 临时文件测试110项通过，覆盖布局保存、重读、复位、字号/字体模型与其他页保留；UI有布局/字体编辑控件。但本轮没有通过F6 GUI实际拖动/改字体后保存、重读、复位，因此不把模型检查说成F6 GUI端到端操作。

### 当前运行证据与输入边界

- Build：`E:/HS/HearthstoneClone/work/f026-unity-build-r7.log` / `.exit`，exit 0；`UI CALIBRATION CHECKS PASSED: 110`。实际 Player DLL SHA-256 与候选清单一致。
- Editor：`E:/HS/HearthstoneClone/work/f024-account-editor-gui-f026-r6/`，14步、207条GameView输入（69手势）、33/33成功请求、34张1920×1080截图、exit 0。独立 `tools.account_player --analyze-editor` 重跑7/7检查通过；包边界验证包含一次槽外释放不产生请求，以及一次槽内释放对应一条成功开包请求和5张结果。R5为189输入/31请求，r6增加的输入与请求来自不同收藏分解交互；各候选分别记数，不强行归一。
- Windows Player：`E:/HS/HearthstoneClone/work/f026-account-player-r6/summary.json`，13步、24/24成功请求、29张截图、无错误/超时，Player exit 0。`input_mode=direct-production-handler` 且 `gui_inputs=[]`，只证明生产handler与窗口画面，不证明Windows OS鼠标输入。
- Editor与Player provenance before/after均固定为 `0f379fa069342f96a871f6dd8aac806f9ff8439b`，314个文件与278个源码记录一致。主目录 `Main.unity` 两侧SHA-256均为 `D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51`；用户校准文件两侧均为 `B75BCD140FF5F3AC8D0EFCB7D592CB14BAA829A6E4D82FF8EB06AD7AF731BDED`。
- 关键开包图：Editor `E:/HS/HearthstoneClone/work/f024-account-editor-gui-f026-r6/captures/f024ed-9628-ccadf40d-account-replay-packs-inventory.png`；Player `E:/HS/HearthstoneClone/work/f026-account-player-r6/captures/f024acct-19228-cd5aff91f8-account-replay-packs-inventory.png`。Editor回放是Unity生产OnGUI的GameView输入队列，Player回放是直接生产handler；两种证据不能合称OS真实鼠标交互。

### 历史失败候选

- **r3 (`a19cacfe…`) FAIL：** 顶部原生标题牌缺少“开包”。r4从 `UberText_HeaderText` 源锚点绘出标题并修复圆盘溢出裁切；r4的槽外输入测试目标跑出屏幕，保留为失败证据，不算通过。详见下方r3历史记录。
- **r5 (`a8b832b…`) FAIL：** 开包纹理右下烘焙的英文Back在gear右侧露出金色残片；资源 `local/runtime/e08f3f20f4954688221184c4.png` 验证该烘焙内容。r6恢复源BackButton位置并把页面裁在公共底栏上方，两种运行证据确认残片消失。任务页对照图没有同类残留；r5报告和复核更正保存在 [V026-hub-r5.md](V026-hub-r5.md)。

## 历史复验：r3（FAIL）

日期：2026-10-31。固定候选 `a19cacfe9052bf2ecd20c4ce28801382911d948d`（`refs/verification/f026-r3`），Player DLL SHA-256 `D78730240EF838D983F18FE007DBED5D1CAED80643C945DC91897F65FFE86B8E`。该轮结论：FAIL，退回修正。以下原始证据为历史，不代表当前候选结果。

### r3 六项历史结果

1. **主界面底栏：有界通过。** Editor 与 Windows Player 截图显示右下原齿轮、金币/奥尘栏在其左，左下好友计数与本地时钟。齿轮、好友按钮、货币背景与图标映射见 `manifests/hub-native-widgets.json`；画面与GUI输入模式不证明旧版全屏动态或逐像素一致。
2. **菜单与选项：有界通过。** 原生事件实际打开“游戏菜单”和双列设置；帧率选项被修改、保存并重读，账号与会话入口仍在。菜单退出按钮显示且绑定 `Application.Quit`，本轮没有实际触发退出。
3. **好友：功能通过，界面路径覆盖不全。** 临时数据库测试覆盖申请、接受、拒绝、取消、移除、隐私拒收、在线多会话计数、隐藏在线、最后在线时间及重启持久化。生产OnGUI回放实际通过好友申请、第二账号接受后的在线/离线展示和GUI移除；接受由第二账号连接直接发送服务请求，拒绝/取消与隐私开关未在GUI回放中点击。左列表与右侧 Add/Requests/Remove/Privacy 邻接，选中好友后移除按钮启用。
4. **多包购买：有界通过。** Editor生产OnGUI回放打开数量弹窗，执行加、减、取消；取消没有购买RPC。随后购买2包恰好发出一次权威请求，钱包扣200金币、库存加2。临时数据库测试另覆盖并发和幂等购买。
5. **开包页：FAIL。** 主目录生产截图 `work/f024-account-editor-gui-f026-r3/captures/f024ed-42284-fe75e62b-account-replay-packs-inventory.png` 和 Windows Player 截图 `work/f026-account-player-r3/captures/f024acct-38796-ca6ab72215-account-replay-packs-inventory.png` 中，顶部原生金属标题牌为空白。用户原图 `codex-clipboard-183e4813-4b0d-48d2-abb0-5a80b001ca10.png` 在同一标题牌显示“开包”。复现位置为 `UnityClient/Assets/Scripts/ClientApp.HubPages.cs:1212`：`F024DrawPageFrame("", "PackOpeningTray", ...)` 传入空标题；`F024DrawPageFrame` 仅在标题非空时绘字（第476行）。去说明文案不应去掉页面标题。其余可见子项：左侧托盘、全屏铺底、无外侧黑边、钱包/齿轮，且没有多余槽位描边；生产回放拖入槽位后开出5张并逐张呈现。原Shader灯光/SH仍未知，当前局部neutral材质和Repeat纹理副本属于兼容重建。
6. **任务与F6：部分通过。** 服务返回每日/每周各4槽；重抽图标来自原红色卷角网格；F6离线预览显示4+4任务、收集76个布局目标，账号快照与请求数不变。`UIEditor` 已提供布局/字体属性、保存、重读、重置控件；本轮只拍摄预览并核对无副作用，没有实际执行字体/位置编辑后保存、重读和复位，也没有在GUI中点击隐私开关。因此这几项的交互持久化证据未闭合。

### r3 运行证据与边界

- Unity构建日志 `work/f026-unity-build-r4.log` / `.exit`：exit 0；Player DLL 与上列哈希一致。
- Editor GameView生产OnGUI回放 `work/f024-account-editor-gui-f026-r3/`：14步、189条输入记录（63次手势）、31/31成功RPC、34张1920×1080截图，exit 0。独立 `tools.account_player --analyze-editor` 重跑为7/7检查通过、errors=[]。这是Unity Editor原生GameView队列，不是OS鼠标或Standalone GUI证据。
- Windows Player回放 `work/f026-account-player-r3/summary.json`：result=ok、13步、24/24成功RPC、29张1920×1080截图、Player exit 0、errors=[]。`input_mode=direct-production-handler` 且 `gui_inputs` 为空，故只证明该DLL的生产handler与窗口画面，不证明Windows OS输入。测试服务在收尾时由 `stop_child()` 发送 terminate，server exit code为1；Player无超时，日志无运行错误，Harness errors为空。
- 在主项目虚拟环境独立重跑 `tests/test_accounts.py tests/test_account_social.py tests/test_launch_duel.py tests/test_account_editor_evidence.py tests/test_v024_accounts_acceptance.py -q`：41 passed / 10.77s；测试使用临时数据库。
- Editor与Player provenance 的 before/after 均固定在候选 `a19cacfe…`，314个文件与278个源文件未变。主目录 `Main.unity` 两侧SHA-256均为 `D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51`；用户校准文件两侧均为 `B75BCD140FF5F3AC8D0EFCB7D592CB14BAA829A6E4D82FF8EB06AD7AF731BDED`。

控件资源来源已在manifest区分：齿轮、好友框/按钮、金币尘图标和任务重抽网格有索引资源映射；批购弹窗由原纹理九切片组合，未冒充原生完整控制器。官方完整相机、Shader、光照与动态及逐像素对比仍不在本轮通过范围。
