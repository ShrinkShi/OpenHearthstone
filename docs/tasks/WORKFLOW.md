# 多 Agent 自动任务工作流

## 角色与边界

2026-10-16 起采用用户授权的自动编排规则，替代默认人工创建后续 UI 聊天的要求。00 总控同时为 Orchestrator：读取队列/依赖、派发独立 Feature、等待、派发独立 Verification、处理状态和修复循环，再选择下一 Task。总控原则上不实现大型 Feature、不承担独立验收。一个 Agent 一个主要 Task；独立上下文/任务/输入输出/可追踪身份是要求，可见 UI 聊天不是要求。小拼写/配置/明确小修可由总控直接做。

并行写：每 Task 独立 branch/worktree，例如 feature/task-0002-superspell-impact；启动前在 INDEX 写明真实路径与合并基点，不在共享工作树双写。串行可沿用当前树。验证固定提交；默认只读代码，允许更新 Task 验收记录。验证者不边验收边大型修复。

## 能力检查和上下文

开始/恢复自动编排时实际查看工具并调用可用的只读检查，不能从旧工具失败推断全部 Agent 能力不可用。优先原生 multi-agent/subagent；新 Feature 和每个 Verification 使用新上下文，本环境用 collaboration.spawn_agent(fork_turns="none")，不无条件继承总控历史。原生 fork task 仅在能满足所需隔离时使用。Agent 先读 AGENTS、PROJECT_STATE、对应 Task、指定专题，再核对 Git/提交与运行事实；验证者不接收 Feature 私有推理或整段聊天。

本机共享 UnityClient、local 缓存及 build/work 有共享可变资源，编译/GPU/构建需串行；独立上下文不等于独立文件系统。编排者在派发单个 Agent 后不同时修改其负责文件。需要并行写时建立独立 branch/worktree 并登记缓存/输出隔离，不能复制用户 UI 校准覆盖原文件。没有并发写时可串行复用现有工作树以复用本机资产。

能力实际失败且无其他可用独立任务机制时，完成当前阶段并生成 docs/handoffs/NEXT_THREAD.md：下一线程名称、Task、启动提示词、目标、工作树/分支、固定提交和真实工具错误。明确请求人工创建，这是降级方案。禁止假称派发成功，禁止用 Computer Use 点击 UI。原生 Agent 可用时不因 App UI 管理接口失败而降级。

## 状态

TODO → IN_PROGRESS → READY_FOR_REVIEW → VERIFIED → DONE。阻塞写 BLOCKED / BLOCKED_BY TASK-XXXX、原因/证据/解除条件；不能因难或慢就伪造完成。重要任务缺独立证据不可 DONE。验证失败退 IN_PROGRESS，交给原 Feature 或新 Bug Task。简单文档初始化允许 IN_PROGRESS→DONE，外部未完成事项必须显式分列。

自动模式下 Feature 返回 READY_FOR_REVIEW 后总控立即创建独立 V编号 Agent。Verifier 通过并登记 VERIFIED 后，总控核对其验收提交/范围/未完成项，登记 DONE 并重新读取依赖图，自动派发依赖全部 DONE 的最高优先级 Task。Verifier 失败记录证据、退 IN_PROGRESS；优先把明确失败内容发回原 Feature Agent，原 Agent 不适合继续则新建 F编号B。修复后必须新建独立 Verification 回合，不能让修复者自己补成 VERIFIED。

只在真实 BLOCKED、AWAITING_HUMAN_REVIEW、NEEDS_DECISION、无可执行 Task 或实际缺少编排能力时停止。也可在用户产品/方向决策、人工授权、缺少文件/账号/资源或不可逆操作前询问。正常完成、测试通过、READY_FOR_REVIEW、VERIFIED 或下一任务明确时不停止等“继续”。用户明确暂停/撤销的指令始终优先。

人工视觉例外：程序正确性验收与用户主观视觉判断分别记录；自动部分通过但仍需主观判断时进入 AWAITING_HUMAN_REVIEW，输出可追踪原版参考/当前实现/contact sheet/关键帧/差异摘要后等待。没有原版资料则按真实缺失写 BLOCKED，不能造官方参考或以人工例外掩盖程序失败。

## 队列运行记录

docs/tasks/AUTOMATION_RUN.md 登记运行模式、能力实测、Agent 标识/角色/上下文隔离、Task、代码提交、分支/工作树、文件所有权、阶段/结果/验收报告、修复回合和下一步。INDEX 继续作为长期任务图。不要预建空 Agent；仅派发已可执行的真实工作。进度写仓库和简短用户更新，长日志留 work，重要事实不可只留个人记录。

中途发现新问题先落 Task，不擅自扩范围；路线/API/网络/存档重大改变先提建议，由总控写 DECISIONS。长任务切代先更新 Task 完成/失败/事实/下一步/测试，再开 F002B，不复制旧聊天全文。只有有限且成功标准明确的执行 Task 可用 Goal，总控不设无限 Goal。

## Feature 启动词

```text
请执行 docs/tasks/TASK-0002-superspell-impact.md。
先读 AGENTS.md、docs/PROJECT_STATE.md、Task 及其指定资料；检查 git/branch/worktree，核实关键事实。
只处理本 Task；不使用 Computer Use。完成后运行验收、更新 Task、中文 Git 提交，状态设 READY_FOR_REVIEW，给简短交接后停止。
```

功能交接只写 Task/状态、关键实现、验证、Commit、限制与文档路径。不得顺手开发其他任务。实施者跑测试不是独立验收。

## Verification 启动词

```text
请独立验收 TASK-0002。先读 AGENTS.md、PROJECT_STATE、Task、相关 Commit/Diff。
不要相信实现摘要；自己编译、测试并按 Task 检查异常/回归和视觉证据。默认不改业务代码，不使用 Computer Use。
通过则 VERIFIED 并记录命令/退出码/证据；失败退 IN_PROGRESS，写复现、失败项与证据。提交验收文档后结束该 Verification Agent，由总控继续队列或修复循环。
```

视觉/Shader/复杂动画/资源解析/规则/网络/高风险重构采用独立验收。无法得到官方参考时不得宣布高还原通过；可先验证明确范围的子系统，把整卡视觉留给对应 Task。

## 文档和证据

Task 保留关键结论，失败实验进 Task 或 research；终端长日志留本地 work 并记录可重复命令。重要结论同时进入已跟踪专题/数据库，不能只存忽略的 .dev-records。PROJECT_STATE 保持短，旧短期结果留专题。真实运行与代码优先于旧文档，修改前确认具体版本。

Task 的 Commit 引用前一实现提交；最终文档可用路径 git log 定位，避免自引用哈希。截图/原资源本地忽略，清单保留相机/时钟/版本/hash/测量和限制。缺少本机证据时写 Unknown/不可重现，不伪造。
