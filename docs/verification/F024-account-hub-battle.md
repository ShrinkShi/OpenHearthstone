# F024：账号收藏经济与PC主界面

## 2026-10-29：r24 本地可测试版

最终代码 `6dcbfab7fbc9e14a40c730a45cd64f2a02db0e07`（`refs/verification/f024-r24`），Player DLL SHA-256 `DE00EF49750E154361902F6D98339C3AC243DD5C40B7D1172123A582DC931489`。完整Unity/GPU/Build exit0；41项定向Python一次组合通过（`work/f024-final-focused-tests.log`），未声称本轮全量测试。Main场景和用户校准仍保持下文基线SHA。

实际 `work/f024-account-editor-gui-r7/` exit0/result=ok：9步骤、99条Editor原生GameView输入、22成功RPC、19张1920×1080图，新增未搜索时的完整八卡收藏页截图。只读分析工具exit0，事件收据、热控件、坐标、外放无请求/槽内一次5卡和全部截图检查通过。前后provenance绑定同一固定代码的271源文件及6项程序集/场景/校准工件。独立V024最终提交 `651a671ee26a8b06d56c23625f4ed58f5cced6e6` 已逐项复核R7/R9源码、输入、哈希和实图；推荐有界结果AWAITING_HUMAN_REVIEW，完整任务继续IN_PROGRESS。最终报告见 `V024-hub.md`。

同一最终DLL的实际Windows Standalone `work/f024-account-player-r9/` 也exit0：8步骤、14个已记录成功请求、18张1920×1080图、无超时/错误。它调用生产handler来验证账号/绘制/持久化，不是鼠标输入测试。`summary.json`在启动时记录EXE/DLL哈希；结束后root写入明确 `written_after_run=true` 的provenance，将这些原启动记录与fresh结束哈希对照，两者一致。不得把该记录称作夹具本身已内建结束采样。R23的direct-r8亦通过，保留为此前实测。

r23/r24修正：按源节点隐藏PackCount_mesh和ShopNewNotice，只关闭TheBox_TrayPacksCards自身Renderer，保留可点击的开包/收藏子网格并恢复离页状态；消除实际R22白块、空badge和宽条。收藏八卡/筛选移进单纸页、纸上深色字/纸下浅色字、按钮按中央纸面缩字号，均由新图核实。F024预览规则文字在try/finally作用域中使用原调用方深棕色且不画描边/阴影；用户原fillOverride、字号、字体、几何与校准文件全部保留，战斗分支不改变。主菜单重复status已移除。独立Agent实现提交9f1f6d0/22b6d8ab与分析工具a0acd56已增量集成。

功能可从根目录 `Start-LocalDuel.cmd` 体验；新注册两账号，各获基础/核心卡、1000金币、0尘，库存/金币/卡组归本地账号服务。TASK-0009和0010已DONE的有界范围保留；0011/0012完整原版视觉不提前DONE。现仍没有OS鼠标注入证据、完整双Unity账号对战证据或旧PC同机位精确视觉比较；旧Standalone合成队列失败为工具限制历史，未据此认定实际用户鼠标不可用。Collection源相机/Shader/动态与现有单页+详情构图的原版差异仍开放。

## 2026-10-29：r22 与实际 Editor GUI-r5

最新固定候选 `e45067a03b00adf47affc90a70fcb5f4d15fe7f3`（`refs/verification/f024-r22`），完整 Unity/GPU/Build exit0。Player DLL SHA-256 `C85CDA658909DF14256421A70E6FC003F4EA0AABC9FF68F760CA51163A1E8D44`；Mesh6、AccountState24、Battle32、Weapon142通过。主场景只恢复构建自动ID，SHA保持 `D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51`；用户校准保持 `6B62A6713047FEE1CBB949E4AC9305BE1F79497019584AAE8A23856FA5CC6D28`。

`work/f024-account-editor-gui-r5/` 实际退出0：9步骤、123原生Unity输入记录、26成功RPC、18张1920×1080截图。输入通过 `UnityEditor.EditorGUIUtility.QueueGameViewInputEvent(Event)` 在 `EditorApplication.delayCall` 投递到 PlayUnfocused GameView，由生产 OnGUI/GUI.Button/正常拖包分支消费；hotControl由控件实际取得、保持和释放，测试没有手动设置hotControl或调用handler伪造点击。外放包不产生请求、内放包恰一次返回5卡，购包、揭示、收藏、制作/分解、存组、注销登录/持久化、双账号resume入局均有记录。表单是夹具预填，未声称键盘输入；第二席是独立TCP peer，入局/VS不冒称双Unity整局。

新增 `provenance.json` 前后记录271个Unity C#/game Python的Git blob和字节SHA、Editor两程序集、Player EXE/DLL、Main场景和校准文件；与固定提交逐项匹配且前后不变。独立V024提交 `5f5cf9b` 复核上述证据，通过有界功能范围；整体视觉仍IN_PROGRESS。只读分析工具 `python -m tools.account_player --analyze-editor work/f024-account-editor-gui-r5` 已集成，作者11项负例/只读测试与root重跑均通过。

此前输入失败保留：Standalone GUI-r5的 Event.QueueEvent 20秒未进入OnGUI、0RPC；Editor SendEvent从Update递归失败，delayCall虽到控件但GameView裁切/缩放偏移使坐标错；最终改Editor原生输入队列后，GUI-r4也已实际9步骤/99记录/22RPC/18图通过。两种Unity队列不是同一API，Editor成功不覆盖Standalone合成队列失败，也不证明OS鼠标。r18曾有GetFloat默认参数缺失编译FAIL，已补0f，r19及后续构建通过。

实际截图仍暴露界面瑕疵：r22激活默认inactive托盘后，原计数器白块/空badge及原托盘宽底条露出；9f1f6d0按具体源节点清理，待r23实际复图。收藏单纸页投影约x188..847，而旧四列网格75..892超页；用户原card text校准描边/阴影在小预览过重，纸上筛选白字和小按钮截字需局部修正。由独立Luna/max Agent处理，保留所有用户校准值和战斗绘制行为。旧PC整页精确相机/Shader/逐帧比较未闭合，不将原资源来源或功能通过写成完整还原。

以下为此前过程历史。

2026-10-27，按五张新参考实施TASK0009/0010/0011，IN_PROGRESS。三GPT-6 Luna/max独立worktree，root串行local/Unity/Player；实现和独立验收分别记录，不提前标完整还原。

范围：F6英雄气泡、Summon语音/报告兵、通用装备声；账号统一收藏/卡组/制作分解/卡包/金币/每日周任务；PC主界面与收藏/开包/商店/任务页面，冒险/竞技/其它暂不开放，不要暗月宝藏。协议见F024_ACCOUNT_PROTOCOL；用户已选择初始仅基础卡牌+1000金币、0尘，不送完整专家preset。

原生collaboration已实查，fork/read/worktree工具元数据存在。DevSpace本轮两次Internal error，自动重试后改本地工具/Git。保留r9现有脏工作区、源资源、校对和用户对局；不要求人工创建后续线程。

后端候选057a5c8/交接2ad6fcc，独立V024 fd9d403后端43项PASS，0010按有界后端DONE登记，0011依赖满足且持续实施。root合入后原38项定向检查通过。曾发现制作误扣gold，交候选前修dust；原账号/37240服务未触碰。

战斗候选eedae15 + 有界导出1564fac已合入（ClientApp hooks由Hub Agent后续合入）。root串行导出358个当前可玩随从/相关衍生体的449条原PLAY候选，errors0；CS2_101t确为`VO_CS2_101t_Play_01` zhcn，SHA34674c4dfe872d677bbb30def687a8ecf9125e46bb078fc353412d305ecd4613。CardDef其它皮肤条件分支未误选。武器slot41/42/7/5导出完成，实际新Player未跑，不能提前认定可听/时机PASS。

0012资源分为背景/收藏/开包台/任务/商店（Battle Agent）及23套包封面/金币尘图标（Pack Assets Agent）；只写导出工具，root统一执行local。原相机映射有Unknown，用户图和atlas不会充当整屏产品背景。

后续纠错：SET_NAMES_ZH从set20错位，改CardSet键；23个集合不能全当随机包。V追加反例在24c0真实FAIL，最终37228af/35db953独立46项PASS，商店收口16真包，7特殊来源仅收藏制作。23条封面研究保留，运行时只导16；首轮1403/1414 selector错选无mesh根节点，D修6e3f632后第二轮16pack+币/尘导出errors0。页面r3资源9Scene+3variant、13原纹理全部errors0。

Unity r1重复f024ReplayUsername编译FAIL，C246148e修；r2 F024DrawPageFrame名称缺失编译FAIL，C5d65858修；r3编译过，F024BattleChecks首次router生命周期检查FAIL，B修复中。独立VUI在f1694d7发现同revision不同账号收藏缓存/未保存卡组草稿串号，已退修C，不把后端PASS覆盖客户端FAIL。未跑新实际Player。

## 最新实际集成（r8）

固定源码206be1bd6dffbe07b7a705ec609f435c6f9f0325（有界index，主branch/index未动），r8业务DLL A040EDF8219C7287AF270F7C5A958F9B1394D3AADC8AEB19151080C850E51161，Unity完整检查/GPU/Build exit0。Mesh6、AccountState24、Battle31、Weapon135通过。主场景仅恢复自动ID为原值，SHA D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51；用户校准SHA仍6B62A6713047FEE1CBB949E4AC9305BE1F79497019584AAE8A23856FA5CC6D28。

Independent V旧State20passed/3FAIL已由Cdbf2（账号cache/draft）和B05b1e3a（inverse hit）修，r4真实State24PASS。CORE craft另被V发现API漏guard，Adcda2b0已合；同反例旧FAIL、新V20PASS、root整合54定向Python通过。service/catalog blob分别f989cc60ad5c7c609f64e9ea57e5d9328d54e3a6/757c539d56d1ea30734c9841497abd7d3aaa361f，与dcda2b0相同。不得把旧cc90be4归为已含该guard。

实际Player r2曾11RPC成功至login后refresh门超时，OnGUI还因静态mesh字段被JsonUtility从null读为空数组而抛InvalidDataException，截图shop/pack空白。Caf6a修认证refresh门、0a6a修空skin判定；B独立MeshChecks6真实验证null→len0、静态加载、完整Hunter蒙皮、partial仍拒绝。r6 State fixture默认Type0被新普通卡过滤拒绝，经V2e33098补真实Type4/5后r8全通过。

实际direct Player r3（r8DLL）注册/买包/5卡入藏/制作分解/保存deck/注销重登/双账号resume+deck_id入场共7steps15RPC通过，Player exit0无异常；harness仍FAIL，因所有15张capture实际1680×1050而非1920×1080。这里仍是handler/TCP回放，不能当鼠标输入。Ccb9c1de已提交测试profile强制resolution+Update显隐修，尚未合入r8。r3实图卡名费用描述攻血已恢复，仍有页面composition与TheBox背景错误，继续修。

633 Repaint/hotControl合成adapter未当作GUI通过；经V审查后4e84改Event.current MouseDown/Drag/Up真实IMGUI事件注入。r7三API编译错经C19f6b84按本机Unity程序集修，r8成功。实际GUI r1在shop.buy:3 MouseDown没有取得hotControl而FAIL；注册和主菜单商店入口两个真实三帧事件序列已消费且RPC正常，不能扩成全GUI通过。C已定位ScrollView target映射漏滚动位移，修复中；证据work/f024-account-gui-r1。

原生Agent曾BadRequest/额度退出，用户继续后实际重试恢复；V连续BadRequest改派新独立v024_hub_retry，成果保留。另派f024_hub_scenes独立worktree（base206be1b）只修NativeHubScene/页面几何，C拥有控件布局/输入/账号与分辨率，root继续串行所有Unity/Player/共享导出。

## r9 实际回放与退修

固定源码 `ca8051ca121117b10d692b9132207d1b294da35c`，业务 DLL `9C1AAE8186488EC4D119DBA59AA81712141378EE0BBCBDA5F7ED1E2590C89784`，完整 Unity/GPU/Build exit0，Mesh6 / Account24 / Battle31 / Weapon135。Main 自动ID还原后SHA仍D1C39DB1…；用户校准未改。

`work/f024-account-gui-r2`：严格1920×1080截图与旧TheBox显隐已修，实际注册和商店导航 MouseDown/Drag/Up 成功。购买 shop.buy:3 仍未取得hotControl，production ScrollView处事件为Ignore、内容坐标不匹配，保持FAIL并退修C；没有购买副作用。`Event(int)`经本机Unity程序集反射确认参数为displayIndex，不能用作EventType构造器，错误假设已撤回，未合入主树。

显式fireblast/hero-power/hero-attack/hero-speech/H10/H11诊断只允许loopback guest fixture；正常accounts_v1认证不放宽。原Paladin r1因改造后进入账号页而未入队，r9修入口后r2双Player exit0、driver25 checks，两端各一次`VOICE_CS2_101t_PLAY_0`。初次analyzer错误要求observer权威与旧tail显示始终相同；ae7b6e9现严格以authority journal的power v7和end_turn v8、同帧TURN_START+旧tail证明时间差，独立两端分析通过。Listener属于整场混音，不能等同隔离语音。

`work/f024-ui-r2`：speech/emotes两个真实1920×1080 Editor Player页面exit0，无异常；英雄候选限11基础英雄，speech坐标转换和左3右4预览已修。截图不等于实际鼠标编辑/保存重载，更不替代真实战斗回放。

`work/f024-native-weapons`：战士/萨满基本攻击及CS2_106/EX1_567武器夹具4对共8Player全部exit0；原slot41/42装备cue发生，但slot7/5缺失导致独立分析FAIL。B定位`NativeBattleWeapons.Slot`旧武器查找还硬编码CS2_082，仅盗贼能掉耐久/损坏发声，已退修，不能把正常退出当武器整体通过。

原生页面经3ee5721与77ad9c6两轮实际GPU检查：收藏册/Manager组合、开包台/任务板/PC商店框架从原mesh/UV恢复；默认hover光框、教程箭头、误作普通可见mesh的DepthReserve白条已去除。第二轮`work/f024-hub-scenes-r2.log` exit0，截图在`work/f024-hub-native-scenes`；77之后尚未新Player构建。原版动态布局/相机与完整旧UI视觉一致性继续未验收。

## r14 / r15 继续实测

r12新增Editor检查误把JsonUtility反序列化的空weapon对象引用当null而失败，改成实体移除断言并真正执行ObserveState(before)→ObserveState(after)→ObserveHeroAttackAccepted；r13缺Reflection using编译失败，已补。r14完整Build/GPU0，Battle32（新增enemy/七表情text持久化）/Weapon142；业务DLL DE48B3DF8C8BCF96B1745E2B46D4D7CEAF984C6A92A3BB14A0C0244A384FFC49。实际NativeWeapons-r2四对8Player0/analyzer PASS。独立V024报告8bf73a8据此通过TASK0009本地有界交付，root已登记DONE；0011继续。

r15固定源码4b696fce63dbf321caf3f62201dee8ccfe342782 / DLL1FF41517E7B65F146072FB11F1338EE0E0809D4CA799276D4AB80B120FC29EDF，完整Build0。原生Button_Back_V3按钮已绘制接线且持久缓存，文字命中区沿用GUI.Button；首版RT几何只占按钮半宽，经97e0e71以源几何比例取景再铺满control rect修。实际direct Player-r4已完整7steps/13个已记录请求、15张1920×1080图、exit0/无错误；这仍是handler/TCP证据（步骤中的“拖入”文字不能证明鼠标拖动）。

同一r15 DLL重新实跑Paladin-r3双端并analyze通过；新run.json绑定1FF41517，VOICE_CS2_101t_PLAY_0各一次，Listener总混音10ms包络相关0.766814/offset1.17秒。NativeWeapons-r3新8Player0/analyzer PASS，新增启动前/后exe与Assembly-CSharp路径/SHA的provenance相同，DLL均1FF41517，补齐上一轮工具未内嵌绑定的缺口。30项账号/独立账号/Paladin合同/Attack工具Python组合检查通过；此前54项其它focused范围不宣称本轮全量重跑。

Standalone输入仍FAIL：GUI-r3 rawType证实为MouseDown但BeginScrollView后type为Ignore、指针重置；GUI-r4官方坐标逆变换已使point在button内，但native clip仍Ignore，未操作hotControl或绕过handler。2000ad4改反射Event.QueueEvent后，GUI-r5首条account.register在20秒内没有进入原生OnGUI（队列调用成功不等于分发）；保留0RPC/0截图失败，不退回假点击。下一项明确Editor-only GameView.SendEvent证据，不冒充Standalone或OS输入。

查看direct-r4图又发现切页首帧activeScene落后导致整atlas fallback，Scene58f3ae9已修Draw即时激活，HubPages禁止裸Collection atlas回退；这些在r15之后尚待重建实图。用户Main场景与UI校准SHA仍分别D1C39DB1…、6B62A671…，旧37240服务未动。
