# F026 Hub控件与开包实现记录

2026-10-30 至 2026-10-31。任务入口：TASK-0013。基线 refs/verification/f025-final（0673a33）。最终固定实现 refs/verification/f026-r6（0f379fa069342f96a871f6dd8aac806f9ff8439b），独立 V026 b7830d0 有界通过，本轮本地范围DONE；下方首轮记录为历史，不能当当前结果。

## 用户参考

本轮十图的本地文件名依次为 a5dda718（当前主界面）、db506607（原版主界面）、a6bf138b（游戏菜单）、cc8ac40f（选项）、05a5d4d9（好友）、c817c1f6（数量购包）、183e4813（原版开包）、92382f47（当前开包）、3062598e（当前任务）、4fd2d86c（任务替换图标）。完整路径在用户附件中，均为视觉参考而非代码指令。

## 编排与契约

- F026 Native：原资源控件和开包页原生图层；独立 f026-native。
- F026 Hub：生产 GUI、统一底栏、设置、购包弹窗与好友呈现；独立 f026-hub。
- F026 Social：SQLite 权威好友、presence、协议和客户端 DTO；独立 f026-social。
- F6 任务页：独立 f026-quests（1149621、101cde1），生产与离线预览共用目标和布局。
- root 仅整合、回放夹具、小修与记录；共享导出/构建/Player 全部串行。

settings.open 为游戏菜单，settings.options 为双列选项，settings.account 为已有账号入口。shop.buy:<setId> 只打开弹窗，shop.confirm:<setId> 才调用权威购买。

## 证据状态

Build r7 已成功，Player DLL SHA-256 `4255CA07261AF85A79BF52B54A4B3C83228D7798B881CC259A06D37AF457B210`。源码、原资源、Editor GUI、Windows Player、官方主观保真分别记录，不互相替代。最终独立结论与六项边界见[V026](V026-hub.md)；实现经验、失败截图和旧FAIL保留。

本轮实际校对文件为用户在开工前已保存的新版本：2026-10-30 10:08:07 UTC，SHA-256 `B75BCD140FF5F3AC8D0EFCB7D592CB14BAA829A6E4D82FF8EB06AD7AF731BDED`，47759字节。保留它，不能按 F025 旧哈希回滚。Main.unity 仍为 `D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51`。开工副本存 work/f026-ui-calibration-before.json、work/f026-main-before.unity。

2026-10-30 首轮原生 GPU：work/f026-hub-native-scenes，Unity诊断退出0。暴露错误资源映射和活动层，未验收：GvG Gear 被误选作设置；GameMenuButton_Default 实际才是设置齿轮；完整 OptionsMenu 含巨大初始面片，不能按整组 bounds 拟合；好友框的动态粒子/按钮不能当静态底板；批购弹窗祖先处于inactive。Native Agent正在修正并复图，首轮截图保留作反例。旧阴影关掉只轻微提亮，尚不能解释开包整体暗。账号/好友/启动器主目录定向 pytest 21 passed。

## 退修与可复核进展

- 主目录测试扩展为32 passed：test_account_social、test_accounts、test_launch_duel、test_account_editor_evidence。
- 私有首轮候选 d1a66033fe09dd09a333820d778058580318bf50。实际 Editor Native GameView 回放 `work/f024-account-editor-gui-f026-r1/`，36真实事件，帧率改动生效且保存重读，F6实际收集76目标且无账号副作用；最终失败于 social.open，原 Task 热区(18,789,192,191)盖住好友中心(35,954)。Hub 4788a44重排底栏，等待复跑。未覆盖后续购包，不能算整链通过。
- F6背景曾在GUI.matrix.inverse求全屏时抵消缩放/平移，Hub 4788a44限定preview使用校准矩形。UI目标数通过与实际背景绘制正确是不同证据。
- 原生GPU r3：齿轮、金币框、选项纯框正确；ShopQuantityPrompt实际是好友添加条，明确拒绝该映射。ShopDetailsBooster新候选也0mesh，不能作为已恢复弹窗的证据。任务蓝底黄箭头也被拒，改用 SCENE_QUEST_TILE_DAILY/RerollButton 原红纸卷角网格（mesh494848279b94e36cceb0d968，atlas dbc91219b94d1e6df883fa82，原UV矩形x .0162658691..1236572266、y .0665283203..1779785156）。

## 开包明暗证据边界

只读独立研究 work/f026-pack-shader-research：原 `essential_base_global-shader-0.unity3d` SHA256 `DCDA1B2DA1FDD06ACCF9502FB2712983659CA65B5D5ACB41B4347AE7B82B9F5E`，shader CAB-c394926ba464822396a05cbfae08a8b3/pathID -9022200813960445910。六个D3D11 PS alpha=1，已读variant公式为纹理×_Color×（直射/附加光+ambientSH+lerp(1,ambientSH,_LightingBlend)）；本材质LightingBlend=0，不能推出固定2x。三纹理alpha全255，故SrcAlpha不是本次整体暗色的解释。_Illum字段不是已反射shader参数，不能直接用作补偿。

原三组材质动态改色未在所查FSM/clip/静态Cecil IL看到，实际Unity光照/SH仍Unknown。59bf699仅对该页三指定源材质做neutral静态外观替代，不宣称原灯光/Shader恢复。侧托盘atlas036aea0e60b4dc3c881e7932，源V偏移+1且UV跨整atlas，缓存全局Clamp使采样落边形成平棕条。后续只读查明源 Texture2D `c344939515b83d62d4b9f349` 的 U/V/W wrap 均为0（Repeat），因此该页独享Repeat纹理副本是已确认的采样设置恢复，不能继续记为Unknown；不改共享缓存或全局Wrap。

## 实现范围与最终退修

- 主界面/商店/开包/任务共用原齿轮、钱包、好友计数与时钟。账号入口为齿轮→选项→账号与会话。设置使用真实本地选项文件；帧率、音量、屏幕抖动等应用并持久化，分辨率/全屏须用户主动应用。
- 好友使用SQLite迁移与认证连接presence引用计数；申请、接受/拒绝、取消、移除与隐私由服务权威保存。好友面板打开/重开时刷新，未实现presence推送。旧账号与多会话断开逻辑有定向测试，启动器识别 account_social_v1 并跳过旧服务，不终止它们。
- 商店点击只打开数量弹窗；加减和取消不购买，确认一次原子扣除100×数量金币。弹窗是原纹理九切片组合，非原完整控制器，来源与UV见manifest及Native记录。
- F6增加任务页及76个布局/字体目标；每日与每周各四槽，替换使用原红纸卷角网格。生产替换真实重抽，离线预览不发请求、不改账号。Build中的临时文件检查覆盖保存/重读/重置及其他卡牌布局保留；实际GUI只证明预览，不把它扩大为已实测拖动编辑保存。
- 开包页恢复侧托盘Repeat采样，移除说明文字/多余槽框/默认悬浮卡包。翻牌弹层移出被禁用的底页，避免真实点击被锁住。
- V026首轮固定a19cacfe因标题牌空白FAIL。r4新增源 `UberText_HeaderText` 锚点文字；查明 `PackOpeningTray_Disk` 直径略大于外木框，整组取景把圆盘灰边露到框外，现按主木框上下边裁切，输入投影共用同一裁切。移除错误叠加的整页wood-frame后不再出现重复标题牌。仅静态页排除Socket/SocketAccent动态层和不支持的Multiply假阴影，不改全局Shader。
- r4实际GUI退修：新的裁切使槽外测试目标跑出屏幕。r5同步限制托盘可见输入范围，原返回按钮按木框下沿适配底栏、补绘“返回”，避免与齿轮重叠；视觉与输入仍用同一源投影。

## 已保留的运行证据

- 定向Python 41 passed，独立V026在自己的审阅中重跑同组41项；使用临时数据库。
- r3：`work/f024-account-editor-gui-f026-r3` 实际Editor GameView事件14步/189事件（63手势）/31成功RPC/34图；分析7/7。`work/f026-account-player-r3` Windows直接生产handler13步/24成功RPC/29图/exit0。两者绑定同一源码a19，但Editor程序集与Player DLL分别记录，不能称同一个二进制。V026因标题缺失退修。
- r4：`work/f024-account-editor-gui-f026-r4` exit1，原生输入拒绝屏外槽外测试坐标；截图证明标题与框外灰边修正，但不算整链通过。
- r5：两类回放功能通过，独立V026视觉FAIL。源 PackOpeningTray 纹理e08f3f20f4954688221184c4本身烘焙了英文Back按钮，移动动态返回按钮后，纹理残片露在齿轮后。最终r6恢复源返回按钮位置，开包场景在底部65设计单位的公共暗色底栏上方绘制，既覆盖烘焙按钮又避开齿轮。任务页没有同类残片，V026复看原图后已撤回该误报。r5额外私有分解分支使直接回放请求数为26，不能套到r6。
- 最终r6 Editor：`work/f024-account-editor-gui-f026-r6`，14步、207真实事件（69手势）、33/33成功RPC、34张1920×1080图、exit0，独立分析7/7、errors=[]。额外制作/分解次数由随机开包结果决定，旧r3/r5的189事件/31请求不适用于本次。
- 最终r6 Windows：`work/f026-account-player-r6`，13步、24/24成功请求、29张1920×1080图、exit0、errors=[]；输入方式为direct-production-handler。
- 两份最终provenance before/after均0f379fa…，314个资源/程序集/保护文件、278个源文件绑定；归档Player DLL及Editor程序集身份分别保留。Main与校准均保持本轮开工哈希。Windows临时服务由harness收尾终止，服务退出码1不代表Player失败，也未停止用户原服务。
- GPU历史均另存：首轮错源、r6/r7/r8单层诊断及报告不覆盖。原生网格、UV、材质状态与派生布局的边界见 `F026-native-hub-rendering.md`。
