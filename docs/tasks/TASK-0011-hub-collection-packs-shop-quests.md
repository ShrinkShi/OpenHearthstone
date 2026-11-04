# TASK-0011

2026-10-31：子任务0013/F026本轮六项Hub修正已由独立V026 b7830d0有界核准，固定0f379fa/DLL4255CA07；底栏、选项、好友、数量购包、开包静态渲染与任务F6均有实现和运行证据。入口见TASK-0013与F026/V026报告。整0011仍IN_PROGRESS，原版完整动态与精确视觉不据子项通过升级。

F025最终ac95ced/DLL F07640E5，Build0、42定向Python与27Editor状态检查通过。用户四项：包面方向/商店紫木框及黑带、4+4任务、收藏木框亮纸/右卡组/上层详情、右下设置登录退出，均有最终Editor GUI-r7 153输入/26RPC/25图与Windows direct-r7八步/14请求/22图证据。新修复logout JSON null被Unity建空对象后挂刷新队列，实际重登/关闭回原页已过。独立V025 98035d3/bbbd950已通过本轮有界修正；整体仍IN_PROGRESS，完整原版Camera/动态/逐像素未升级。最新F025/V025记录优先于下方退修历史。

2026-10-30用户实图复测退修，F025继续IN_PROGRESS：商店方向/黑边、每日每周各4、完整收藏背景/右卡组与上层详情、账号设置入口。原r24仅功能有界通过，不能覆盖本次否决。独立Luna/max Hub/Native/Quest并行，root串行构建后独立V025。记录见`docs/verification/F025-hub-reference-corrections.md`。

PC主界面、收藏组牌、开包商店任务UI。IN_PROGRESS，F024，2026-10-29更新。

最新r24/6dcbfab已构建为可测试本地版，DLL DE00EF49。Editor GUI-r7实际9步/99输入/22成功RPC/19张FullHD图，独立复核通过有界功能与本轮显示修正；最终Standalone direct-r9也8步/14请求/18图/exit0，明确不是鼠标输入测试。主菜单白块/空badge、收藏卡超纸页、重描边/白字及小按钮截字已修并复图；用户校准与旧profile保留。完整旧PC视觉、OS输入和双Unity账号整局仍未被这些证据证明，整体状态不标DONE。入口与账号说明见ACCOUNT_ECONOMY；最新根记录与独立V024报告优先于下文旧阶段。

当前r22/e45067a已由独立V024 5f5cf9b核准Editor GameView有界功能：实际GUI-r5九步/123事件/26成功RPC/18张FullHD图，外放包0请求、槽内一次5卡、收藏制作分解/保存重登/双账号入局通过，源码与程序集前后provenance一致。旧Standalone合成队列失败保留，非OS鼠标/键盘/双Unity整局证据。实际图发现footer白块/空badge、收藏网格超单纸页和小字描边问题，已自动派Luna/max修正，待r23实图及独立复验。完整旧PC视觉尚未通过；以下旧阶段失败为保留历史。

按用户五张图：中心冒险/传统对战/竞技/其它，仅传统对战开放；底部我的收藏接收原卡组功能，职业/费用/搜索/翻页/拥有未拥有、卡组编辑保存、制作分解。开包左侧库存拖到右槽，5卡揭示并入收藏；商店仅扩展卡包100金币；左下任务每日40/60/80、周80/120/160。不做暗月宝藏。账号登录/注册/退出/会话。

复用Unity IMGUI、原Scene/CardVisuals/Importer/Registry，不重写Web。依赖0010最终集成，稳定契约可并行。ClientApp变partial，新页面/DTO/网关分文件，唯一Agent改核心。余额/随机抽卡/奖励不在客户端结算。保旧卡组，未支持对战收藏卡明确不可入当前对战，不造空脚本。共享导出/Unity/Player由root串行，独立V024核实际UI输入和账号闭环。未知原Actor/材质不伪称恢复。

2026-10-27集成进展：主界面四入口、账号登录/注册/会话、收藏/组牌/制作分解、库存拖包/5卡揭示、16真包100金币商店、每日周常六项均已接权威服务。新账号185基础/核心CardId+1000金币+0尘，11合法starter deck。r15实际direct Player-r4完成7步、13个记录请求、15张1920×1080图、exit0，含注销重登数据与双账号resume/deck_id入同队列；第二席为独立TCP peer，不伪称两个账号Unity画面均已实测。

输入验收保持IN_PROGRESS：旧Event.current合成能点注册/主导航但ScrollView的shop.buy未消费；坐标恢复仍Ignore；Event.QueueEvent调用成功却未进入Standalone OnGUI，GUI-r5为0RPC明确FAIL。生产GUI.Button未被绕过、未用手动hotControl伪造点击。后续Editor GameView.SendEvent仅可作为明确Editor-only证据，不能抹掉Standalone失败。独立V024报告与完整工件入口见F024-account-hub-battle。
