# 技术决策

## DEC-009 — 客户端表现统一采样单调帧间隔（2026-10-19）

- 证据：本轮r4/r5真实双端逐帧记录中，realtime每帧约.095秒，ClientApp.Update最早Time.unscaledDeltaTime仍约.0041625，Tick前后/Replay相等、Speed1/captureDelta0；Hit年龄0→.354用了约8秒而后跳13.72，选择3秒也未按实际速率推进。故“仅Update中途改变读值”的先前假设不成立，最早快照该引擎值仍不足。为何引擎值异常仍Unknown，不将手动Simulate污染假设写成已证根因。
- 决策：ClientApp每次Update最早从realtimeSinceStartupAsDouble采样一次单调帧差，作为本地表现/FX/UI消费者共享的帧delta；保留现有Presentation/Scene拥有的时钟和原Pause/Speed应用位置，不改权威规则/网络结算、不重建多时钟架构。仅给当前卡牌换墙钟而同帧其它消费者仍读异常引擎值会保留不同步，故在客户端入口统一输入。
- 边界：正确初始化/场景切换基线；暂停期间仍刷新基线，恢复不补偿停留时间；非负有限输入、速度只按原路径应用一次。原typed clock、真正倒带、停止/异常/所有权、音轨和旧双卡合同不变。每帧原引擎值/单调输入/表现增量/Speed/Pause可诊断，原引擎值仅作证据不做施放特例分支。
- 验收：实际正常非Mage、选择/施放/取消、暂停恢复与常用倍速的速率和完整反馈窗口；共享停止/根/GPU必要回归、新构建的法师术士双视角连续GUI及自然整局，再由空历史V005-r2独立验。该有界修复直接服务用户报告的动画/尺度及重复受击问题，已由总控根据实证作技术决策，无需用户产品确认。

## DEC-008 — 人工失败恢复Task5并追加共用目标指示器/肖像修复（2026-10-19）

- 输入：用户明确不通过，报告法师对术士敌方视角双重受击、火球与选择特效过小/错误、技能和战场随从肖像暗色；认可原法师目标指示器，要求所有带目标指示器的卡牌/技能复用。两图是用户实际客户端问题证据，不当官方gold。
- 决策：Task5退IN_PROGRESS，原Feature修复后派全新独立V005-r2。允许最小共用Presentation/肖像颜色与投影修复及NativeTargetArrow复用，保留各入口权威合法性/来源锚点/取消和清理；不扩其它卡牌自身规则或特效、不重建架构。此追加范围由本轮用户直接授权，不需重复询问。
- 验收：法师对术士真实双视角连续命中时序与唯一受击、选择/火球分阶段尺度和源链、绿洲钳嘴龟卡面/战场同屏色域、跨技能/法术/战吼等目标入口GUI及共用回归。优先查纹理色域/材质/父变换/显示时钟真实根因，不用任意放大/增亮或改变权威结算掩盖。旧V005-r1局部PASS不覆盖本次失败；原官方资料缺口与可复现程序问题分开处理。
- 本轮投影边界：漏掉的自动plane/原RT/Bloom恢复后，旧32world正交仍令主体偏小。允许仅HERO_08bp的Scene采用已用于原指示器的authored Main Camera透视，以当前GUI坐标射向明确y0棋盘平面取得Source/Target并由同一源相机绘制；保留源plane/粒子/parent尺寸，不改用户校准，不影响旧双卡场景。此为可追溯本地投影适配，不宣称原运行相机动态输入已恢复；必须正式GUI量化相对英雄尺寸/锚点、选择和飞行连续帧后另交独立V005。

## DEC-007 — 标准技能Actor按当前GUI目标直接绘制（2026-10-18）

- 背景：TASK-0005实物检查发现Highlight原UNorm8颜色被专项导出为0..255，已按原channel格式修正为0..1；修复后源单片元公式仍允许Alpha超过1。同一原mesh/state在ARGB32直接绘制与ARGBFloat C/K重构的有界GPU对照maxRGB差0.490196，不能用单片元数学相等证明完整合成等价。作者相机m_HDR=false见manifests/original-battle-cameras.json，原运行时是否覆盖及目标格式仍Unknown。
- 决策：在本Task的NativePowerVisual按钮/详情路径内，复用原Renderer、材质与来源投影，增加直接绘制到当前GUI framebuffer的CommandBuffer分支。当前目标格式的实际混合与逐draw范围处理交由GPU执行，不在末端夹取C/K、任意降色或另绘白环遮盖。
- 边界：不替换共用NativeEffect或其他卡牌渲染，不更改TASK-0004浮点C/K证据与结论；不借此扩建全局渲染框架。须保存并恢复目标、viewport、矩阵、裁剪及材质状态，隔离深度/跨widget影响，不清除其他画面颜色。
- 验收：同源GPU对照、正式两视角按钮/详情/已用/选择状态与费用文字、1920和1280缩放、原UI校准及清理；新代码对应的新Windows构建与必要整局，再交空历史独立V005。此为用户已授权完整技能恢复的必要有界实现，无需额外产品确认；不提升官方视觉/原运行目标格式等级。
- 依据：原Shader数学与字段、专项来源报告及实际对照见ORIGINAL_FIREBLAST和Task5本机证据。Microsoft Output-Merger说明blend输入按目标格式范围处理，参见https://learn.microsoft.com/en-us/windows/win32/direct3d11/d3d10-graphics-programming-guide-output-merger-stage；该通用规范不证明原游戏实际运行格式。

## DEC-006 — 火焰冲击选择期与施放期衔接（2026-10-17）

- 背景：TASK-0005用户要求完整技能/指示器。只读原InputManager.HandleClick→ActivatePlaySpell→Card.GetPlaySpell(0,true)→Birth表明点选标准英雄技能即出现Start；确认后进入Action，取消另走Death/Cancel。仅在权威确认后新建Birth会重放或漏掉真实选择阶段。证据入口为F005源码研究与work/f005-ui-research，最终可追溯来源由Task5专题保存，尚未独立V005。
- 决策：在TASK-0005内有界增添BeginSelection/Cast/CancelSelection及同一root/scene的所有权衔接；复用已有Start/根/实际原Prefab，当前Begin保留旧调用合同。不另建全局大框架，不将所有技能分支一并打开。
- 行为：选择期不发权威power请求、不飞行/Hit；权威确认后同一实例只Cast一次，不用选择时长猜测/快进重播。明确拒绝、非法目标、晚结果、重复回调、目标消失、右键/Esc与场景退出。敌视角无本地选择的路径需明确并验证。
- 所有权：UI持有、等待与正式播放器接管之间不允许双释放/悬空；晚到对象仍先接管后判停，错误原因不能丢。CancelSelection的原Death/取消声音与旧hard Cancel/Dispose分别约束，旧Task2/3的停止/清理和显式失败重试合同不变。
- 验收：新完整输入/GUI/实际音源和资源生命周期，以及旧归档停止探针/正常链按共用改动风险回归；再交空历史独立V005。原场景几何/相机或官方时序不能由API恢复自行升级为已知。此决策属于现用户授权必需实现，不需要额外产品确认。

## DEC-005 — 原生 Agent 自动编排（2026-10-16）

- 决策：按用户新指令，总控承担 Orchestrator；中大型 Feature 和重要 Verification 派发独立 Agent，使用最小上下文，新验证回合不继承 Feature 推理。独立 Agent 可替代可见 UI 聊天。
- 原因：正常阶段无需用户反复创建聊天/输入继续；独立验收与文件化证据要求仍保留。
- 状态：READY_FOR_REVIEW 自动启动 V；验证通过后 VERIFIED→DONE 并继续依赖全部 DONE 的下一 Task；验证失败回修复后重新独立验收。人工视觉用 AWAITING_HUMAN_REVIEW，方向选择用 NEEDS_DECISION，缺失资源等真实阻塞用 BLOCKED。
- 能力：本环境提供原生 collaboration 的 spawn/list/message/wait/interrupt，支持 fork_turns="none"；已成功列举 Agent。App list_artifacts 实测仍为 Invalid app tool request，不能据此判定 subagent 不可用。Git 当前仅主工作树，串行复用；并发写才隔离 worktree/资源。实际派发和回合记录见 AUTOMATION_RUN。
- 影响：更新 AGENTS/WORKFLOW；自动运行 TASK-0002 验证及满足依赖的后续队列，禁止 Computer Use、自动发布/推送或冒充原版参考。仅实际缺少独立任务能力时生成 NEXT_THREAD 降级交接。旧 DEC-002 的独立性与 Task 协议保留，默认人工建 UI 聊天要求被替代。

## DEC-001 — 混合视觉路线（2026-10-15）

- 决策：Level 1 自主等价实现优先；Level 2 本机原资源导入到自主运行时；Level 3 极少数可选隔离 Original Runtime Fallback。
- 原因：兼顾高还原与独立运行、自定义扩展，利用已有真实逆向成果。
- 否决：整体重写；以截图近似替代已能确认的行为；必须启动原客户端的私服；直接 LoadLibrary(Hearthstone.exe) 或向业务层扩散 Hook。
- 影响：规则核心不依赖原客户端；Importer→Registry→Runtime 边界保留。Bridge 仅规划，IPC 方式须待实际研究，不在本轮实现。无原版独立运行尚未验收。

## DEC-002 — 文件化任务与独立验收（2026-10-15）

- 决策：总控维护路线；独立 Feature Chat 执行单个 Task；重要变更独立 Verification；同一 Codex Project，编号永久稳定。
- 原因：旧长聊天不能作为长期唯一上下文；防止局部测试和实现者自述被误当完整验收。
- 否决：所有功能堆一个聊天；大量预建空聊天；重要任务实现者自标 DONE。
- 影响：AGENTS/PROJECT_STATE/Task 为恢复入口；并行写用独立 worktree，串行不强制；遇到工具限制明确记录，不能假称创建 Project。本轮仅文档初始化可由总控完成。

## DEC-003 — 首个完整案例与证据等级（2026-10-15）

- 决策：停止横向扩卡，优先刀扇/刺骨/SuperSpell 控制系统，再验证 Shader 方法与关键帧闭环；复用现有类与研究。
- 原因：子 Prefab、路径公式、材质正确不等于整个法术生命周期正确。
- 否决：继续每轮补十张卡；凭空设计巨大通用运行时；用任意延迟冒充原生命周期。
- 影响：通用化由真实案例推动；参数/调用链/未知项入视觉数据库；Compat 分项记录，整体不靠测试数升级。

## DEC-004 — 等价 Shader 与资源隔离（2026-10-15）

- 决策：恢复输入/渲染状态/数学行为，不要求原始 Shader 源码逐字复原；保留原资源哈希与编译阶段证据。原资产与派生缓存留本机忽略目录。
- 原因：发布版主要是编译变体和参数，等价输出才是目标；开放仓库与本机资源须分开。
- 否决：找不到 .shader 文本即失败；将大批原专有资源随源码公开；本轮直接重写已有历史。
- 影响：来源分【直接恢复】【行为重建】【推测】；现有 manifests 的公开适宜性仍需审计，未授权外发/历史清洗。未来无资产路径和发布审计独立立项。
