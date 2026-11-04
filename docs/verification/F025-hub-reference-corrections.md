# F025：用户实图反馈修正

2026-10-30，承接TASK-0011/0012，状态IN_PROGRESS。用户复测明确否决r24收藏布局与商店构图，既有功能有界通过不代表这些视觉问题通过。

本轮范围：商店16种包预览方向、原生木框紫底和黑边；每日/每周各4任务及旧账号同周期补槽兼容；收藏完整背景、四列两行卡册、右侧卡组列表与可关闭上层详情；右下设置中的登录/注册/退出入口。新账号基础卡与1000金币、100金币购包、每日40/60/80及每周双倍奖励保持约定。

已实际检查原生collaboration及Git worktree可用；独立Luna/max执行F025-native、F025-hub、F025-quests，基线refs/verification/f024-final（1667447）。原生资源/GUI分文件并行，root串行共享导出、Unity与Player，完成候选后自动独立验证。DevSpace open_workspace当前Internal error，回退原生工具，不视为没有多Agent能力。

参考：用户四图分别为现收藏、现商店、原收藏、原商店；只作为视觉参考，不铺为产品背景。原版安装只读、禁止原DLL执行与桌面控制。保留用户SQLite、profile、Main场景和校准文件；验证使用隔离数据库与profile。

运行证据、固定提交、失败与修正将在此追加。当前尚未构建或宣称本轮通过。

## 首两轮实际退修

首集成6879e06：Unity Build exit0，Player DLL D4A93D435A96DB3BC37967102A5266ACB926E93E932208CC1B528A8D0E2E02CE；42项账号/迁移/启动器/包导出/Editor证据定向pytest通过。Luna独立验证8项隔离任务/启动器检查通过；其worktree缺主目录自定义卡牌scope，完整注册断言153而非185是环境边界，主目录整合结果不受影响。

`work/f024-account-editor-gui-f025-r1`明确FAIL：注册与4+4任务、Shop顶部/中间/底部图已生成，但新增滚动截图后的回放坐标未等下一OnGUI刷新，购包目标在屏外；没有购买请求，不能算购包通过。`work/f025-account-player-r1`同DLL direct-handler退出0，仅为功能/渲染证据，非GUI输入。Shop包方向已正，但底色与内框仍错误。

R2固定b62aa0d，DLL8D372977630718A50C9593558C16939A58F97B99AEA52529C78CF6416A05882A、Build0。`work/f024-account-editor-gui-f025-r2`以实际Editor原生事件完成注册、任务、购包、槽外不请求/槽内一次5卡、翻牌、详情关闭、制作分解、组牌保存、设置内退出；最终仍FAIL：busy=False/refreshQueued=True永久等待。报告的logout revision0不能直接当作服务端快照；复核服务协议实际snapshot:null，需检查Unity JsonUtility是否将null实例化为rev0占位对象。修复明确让logout不参与snapshot应用/stale比较、已注销状态不再排认证刷新，并以真实JSON解析后的Editor回归验证；不能绕过回放等待冒充通过。

两轮实图的收藏仍有大片黑区、第四列与托盘交叠、header控件互盖，已退修Native分区与Hub排版。独立V025确认，不因4x2和右列表存在而升级为完整原版一致。

Shop黑底根因已定位：两个指定SpriteRenderer的导出mesh UV全0，背景采样恒为贴图(0,0)近黑像素，原底图中央为紫色；不是全局亮度或tint双乘。Native增量b6329e7对精确材质/尺寸/退化UV条件实例克隆并重建UV，尚待新构建实图。Collection四个关键mesh UV均非退化，不泛化该修复。

R3 6b033fa编译FAIL（CS0136，收藏相机分支distance变量名冲突），没有新Player或GUI。root仅将分支变量重命名collectionDistance后固定R4 a7a693e重新Build，失败日志保留。收藏原组分按设计纸页/托盘分开拟合，原BookBack红条停用；外框为原CollectionManagerFrame_Texture下部UV裁片派生，属于行为重建，非原版完整相机/动态恢复。

R4 a7a693e/DLL B2C958699174E36235402A9BA2DBA5C5FC0EC6A5362C8415889FB951405EB034，Build0，Editor27状态检查/32战斗/142武器聚合通过；日志实证`F024 logout JSON snapshot parse: account_id=, revision=0`，确认Unity将实际null转为空对象。GUI `work/f024-account-editor-gui-f025-r4`完整0，144原生事件/48手势/25成功RPC/25张1920×1080图，分析7项全true；设置注册/退出重登/显式settings.close回原组牌页、详情关闭均有真实事件收据。独立V025确认有界功能通过，但仍有顶端源控件/底部PageTurn黑条和临时灰框，继续退修。

R5 83b5287/DLL 0D6073E03732ABAB4E29C7C3272659AA29BC1BE4CABB434235D44F6288D19AFF，Build0，GUI `work/f024-account-editor-gui-f025-r5`完整0，135事件/45手势/24成功RPC/25图，分析7项全true。清理Collection BookBack/ClassTabContainer/PageTurn和Shop Tabs/Scroller后原黑块消失。棕木框来自原Frame_PC（asset3a2dcd3275eb72a9335e94aa、CAB/pathid在manifest），root先导出独立research，再导单产品Scene，registry1992/errors0；source132顶点UV退化，按textureRect重建并保留alpha，不生成新图片。Frame_PC属于派生复用，旧收藏原Camera关联仍未证实。

R5独立视觉仍退修：透明木框露出了BasePage原.552941灰Tint，顶部仍有active ClassFlavorHeader。Native a94c86c限定纸面用同纹理inactive PageTurnFront白Tint作静态完成态、隐藏精确题签、将Shop_Backing屏幕X/Z拟合Border并保留后方Y深度。R6 a8ff75d实际Build/GUI通过，117事件/22RPC/25图；同DLL E8232987的Windows direct也8步/15RPC/22图/exit0。纸面与题签已修，窄黑带却仍在，实际像素否定了先前“只是露相机底”的推断，不能据背板拟合自报该项修复。未知source uvTransform仅记录原值，未猜解为UV公式。

## 最终R7本地候选

固定代码`ac95cedf94b8a18e9ef077606bda56572f99721a`（refs/verification/f025-r7），DLL`F07640E52B623AD035784D662E94568308FE0D133A809E6266515E803B9FE13A`。完整Unity Build exit0，Editor状态27/战斗32/武器142及既有聚合检查通过；本轮42项定向Python通过，后续仅C#渲染/状态检查与manifest增量，未虚称全仓库测试。

最后黑带根因是精确`Shop_Box_3D/Shop_Box_Shadow`旧3D整屏Multiply阴影。原纹理该UV边缘乘源tint0.3与紫底后逐点吻合R6黑带；ec5c86f仅隐藏这个节点。R7同点y500的x1675/1690/1705从(9,5,11)/(7,4,9)/(5,3,6)变为(56,34,71)/(56,34,71)/(53,31,68)，紫底连续到木框，旧相机底猜测已撤销。

实际Editor原生GUI：`work/f024-account-editor-gui-f025-r7` result=ok/exit0，153事件、51手势、26成功RPC、25张1920×1080图。`work/f025-editor-r7-analysis.json`七项检查全true。覆盖设置注册、4+4任务、100金币购包、槽外不请求/槽内恰好一次5卡、五卡揭示、收藏4×2与右卡组、详情弹层开关、制作分解、组牌保存、设置退出重登与关闭回原页、双账号入局。账号输入值由夹具预填，未声称键盘输入；第二席是独立TCP peer，不是两个Unity完整对局。

实际Windows最终Player：`work/f025-account-player-r7` result=ok/exit0、8步/14成功RPC/22张FullHD图，input_mode=direct-production-handler、gui_inputs为空。root执行前后provenance与工具summary同绑上述DLL和代码，EXE/包元数据/registry均前后不变，DLL已在run目录归档。此证据不替代Standalone GUI或OS鼠标。

Editor provenance绑定271源码与11项程序集/场景/校准/runtime工件；包元数据F747BF93、registry710EB43E均前后不变。Main场景仍D1C39DB149D7CFF4D87F50B980C7C545DE1A4828CB818435DC8D8BC0F9641C51，用户校准仍6B62A6713047FEE1CBB949E4AC9305BE1F79497019584AAE8A23856FA5CC6D28。未操作用户账号DB/profile、未结束原37240服务；启动器能力判别只跳过旧三槽服务，读探针实际会选择37241启动新版。

独立V025已实际核对源码、两类运行工件、收据/哈希及关键实图并通过本轮四项有界修正，见`V025-hub.md`。此结论不等于完整旧PC相机/Shader/动态或逐像素官方保真；完整TASK-0011/0012保持IN_PROGRESS，当前实物可交用户对照复核。

独立提交98035d3e52e00ce0f4526133e322e640b49a2f92；后续bbbd950只改报告计数为185种基础/核心CardId（副本按规则）并移除错误集合名称，未amend或改变结论。全部手动源代码修改已停止在ac95ced；后续整合只有文档。入口Start-LocalDuel.cmd，设置→账号与会话；完整使用步骤见ACCOUNT_ECONOMY。
