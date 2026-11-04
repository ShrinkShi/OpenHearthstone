# Hearthstone Open Replica 操作规程

## 首先阅读

本项目是长期维护的高还原度开源炉石复刻，拥有自主客户端、权威规则服务器和视觉运行时。不得重建项目或推翻现有架构。总控为 `00-总控-架构与任务分发`；项目名称为“炉石开源复刻 / Hearthstone Open Replica”。

新聊天依次读取本文件、`docs/PROJECT_STATE.md`、自己的 `docs/tasks/TASK-XXXX-*.md`、任务指定专题，然后检查 `git status`、`git branch`、相关代码与事实。文档与代码冲突时核实运行证据并修正文档，不盲信完成声明。无需阅读全部旧聊天或全部研究文档。

## 环境、构建、运行、验证

仓库当前机器路径 `E:\HS\HearthstoneClone`；不要以聊天目录代替仓库。Unity 版本见 `UnityClient/ProjectSettings/ProjectVersion.txt`，依赖见 `requirements.txt`。

```powershell
# 在仓库根目录
.\.venv\Scripts\python.exe -X utf8 -m pytest tests -q
.\.venv\Scripts\python.exe -X utf8 -m game.server
.\build\client\HearthstoneClone.exe --profile player1 --name Shrink1
& 'F:\Program Files\unity\2022.3.60f1c1\Editor\Unity.exe' -batchmode -quit -projectPath E:\HS\HearthstoneClone\UnityClient -executeMethod BuildClient.Build -logFile E:\HS\HearthstoneClone\work\unity-build.log
```

双开：`Start-LocalDuel.cmd`；校对器：`Start-UIEditor.cmd` / F6；F7 重读本地校对。完整步骤见 `docs/BUILD.md`、`docs/ASSET_PIPELINE.md`、`tests/visual/README.md`。GUI 和 GPU 检查分开；GPU RenderTexture 检查不可加 `-nographics`。Windows 后台构建使用隐藏进程并跟踪 PID/退出码，不终止用户游戏或无关 Unity 进程。

## 目录与实现规则

- `game/`：Python 权威规则、协议；`UnityClient/Assets/Scripts/`：客户端、表现与原行为等价实现；`UnityClient/Assets/Editor/`：构建/检查。
- `tools/`：扫描、导入、静态研究、验证工具；`manifests/`：可审阅来源/映射清单；`tests/`：自动测试与视觉入口。
- `local/`：用户本机原资产/导出缓存；`build/`、`work/`：本地生成物，均忽略。`docs/` 是跨聊天长期记忆；`.dev-records/` 是忽略的个人过程记录，重要结论必须同步进已跟踪 docs。
- 按现有语言和代码风格做小步修改；手动编辑使用 apply_patch。新解释性注释用简洁中文，标识符/日志保留原语言；不批量改已有英文注释。
- Server 不等待动画；客户端表现时钟、队列和权威状态分开。不得为动画改规则/网络结算语义。
- 资源路径经过 Importer / Registry，不在业务代码硬写本机原版绝对路径。自主等价实现优先，其次本机资源兼容，极少数隔离原运行时 Fallback；详见 DECISIONS。
- 禁止删除有效逆向成果、大爆炸重构、横向扩卡代替完整案例研究。禁止执行原 DLL 作为自主实现；静态读取与可选 Bridge 研究必须标明区别。
- 原版安装只读。本阶段不使用 Computer Use，不抢焦点、不控制桌面。保留 `local/ui-calibration.json` 用户校对结果。

## 任务、Git 和交接

任务编号只增不重用，注册于 `docs/tasks/INDEX.md`。中等以上任务先有 Task；一个独立 Agent 一个主要问题。总控同时承担 Orchestrator：维护路线/状态/依赖、派发 Feature/Verification、等待与处理结果，不承担大型实现或独立验收。Feature 为 F编号，Verification 为 V编号，Bug 为 BUG编号，续代追加 B/C。独立 Agent 不要求创建可见 UI 聊天；原生 subagent 可满足上下文隔离。小文档/明确小修可由总控直接完成。

重要任务状态：TODO → IN_PROGRESS → READY_FOR_REVIEW → VERIFIED → DONE；BLOCKED 必须写真实阻塞和证据。执行者不自行宣称独立验收通过。验证默认不改业务代码，失败退回 IN_PROGRESS 并记录复现。自动编排开启后，READY_FOR_REVIEW 立即派发独立 Verification；通过后总控登记 VERIFIED→DONE 并自动启动依赖已 DONE 的最高优先级 Task，不在正常交接处询问用户。主观视觉确认用 AWAITING_HUMAN_REVIEW，方向选择用 NEEDS_DECISION。具体协议与模板见 `docs/tasks/WORKFLOW.md`、`TEMPLATE.md`。

每次恢复编排先实际检查原生 multi-agent/subagent/task/fork/worktree 能力。Feature/Verification 优先使用新 Agent 和最小上下文（本环境 collaboration.spawn_agent 的 fork_turns="none"）；验证者只获得仓库入口、Task、固定提交和验收职责，不继承 Feature 推理。并行写用独立 branch/worktree；共享 Unity 项目、local 缓存及构建目录必须串行。只有实际无法派发独立任务时才生成 docs/handoffs/NEXT_THREAD.md 并请求人工建线程；禁止用 Computer Use 创建聊天。编排状态落 docs/tasks/AUTOMATION_RUN.md。

先检查脏工作区，不撤销他人修改；保留未验收草稿并明确标记。提交使用中文，一个独立任务一个或少量清晰提交，不 amend、不 reset --hard。并行写线程使用不同 branch/worktree；串行小任务无需机械开分支。总控维护任务分支/工作区/合并和验证状态。不擅自发布、改写历史或推送专有资源。

结束任务前更新 Task 的事实、修改、测试、限制、Commit 与交接；重大决定进 `docs/DECISIONS.md`，状态进 PROJECT_STATE，真实未解决问题进 KNOWN_ISSUES。Commit 可由后续文档提交记录前一实现提交哈希，或以 `git log -- docs/tasks/<file>` 定位，禁止伪造自引用哈希。长聊天先落盘失败方案/正确结论/下一步再切代，不复制聊天全文。只为明确、有限 Task 建立 Goal，不给总控设“完成全游戏”无限 Goal。

## 研究与视觉验收

事实标记 Confirmed / Observed / Inferred / Hypothesis / Unknown，并记录文件、版本/哈希、命令和证据。Shader 区分【直接恢复】【行为重建】【推测】，未知字段写 Unknown，不把推测升级为事实。参考 `docs/hearth_visual_db/README.md`。

从创建、初始化、Source/Target、Birth、路径、Arrival、Hit、Death 到销毁/任务同步追踪控制系统，不能 Instantiate 子 Prefab 就宣布完整恢复。尽量数据驱动，围绕既定 EX1_129 刀扇 / EX1_134 军情七处特工真实案例逐步抽象；旧资料的“EX1_134 刺骨”是名称误标，刺骨为 EX1_124，不据此替换本队列的 ID/源资源。

视觉等级见 `docs/visual/VISUAL_COMPATIBILITY.md`。自动测试、局部 GPU、正式对局、人工视觉、官方逐帧比较是不同证据。固定相机/随机种子/状态/时间，比较 Birth/Early/Mid/Arrival/Hit/Death；内部参考不可冒充官方 Ground Truth。没有证据不能标 V5/V6 或 DONE。原版缺失、Shader 未解析等应写真实未完成/阻塞，不造占位效果充数。
