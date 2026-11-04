# 长期工作流初始化证据（2026-10-15）

## 范围与事实

用户将当前工作切换为文档/技术路线初始化；原全卡无限 Goal 已 paused。本轮没有继续开发命中草稿或下一大型功能，没有使用 Computer Use。

真实仓库 E:\HS\HearthstoneClone，master，起点 0329198。检查 git status / branch / log -10、根 README、gitignore、docs/ARCHITECTURE/ROADMAP/BUILD、tests/visual/README、requirements、Unity 版本与 BuildClient、路径/飞行/命中草稿/播放器代码和清单。根 AGENTS 原不存在；旧研究专题与 .dev-records 存在。

原 dirty 文件仅：original-script-audit.json（新增 WaitAndSpawnImpact d__89 静态审计）及 NativeSuperImpact.cs/.meta。保存为独立 checkpoint **da081b6**，中文提交明确未验收。草稿不接入正式播放器，本轮没有编译/验收草稿，不能继承 0329198 的通过结论。

## 本轮验证

命令：`.\.venv\Scripts\python.exe -X utf8 -m pytest tests/test_match.py -q`。

结果：**16 passed in 16.24s**，退出 0。这是规则 Match 小范围重跑，不是全量 baseline。当前任务为文档初始化，不运行新的 GPU/构建/人工对局。完整测试入口已写 AGENTS。

文档检查通过：JSON 解析；两卡源 Prefab 身份/节点数/路径摘要对照；21 份 Markdown 的 239 个本地链接存在；四个 Task 均有 14 项必备章节；git diff --check 无空白错误（仅工作树换行提示）。后续另外读取两卡根 spell_transforms.raw.m_ImpactInfo，确认 delay/game delay 摘要与本地源缓存一致。数据库参数是便于阅读的十进制摘要，完整精度以 source manifests 为准；未展开图不冒充完整依赖闭包。

快速重查入口：`Get-Content docs/hearth_visual_db/effects.json -Raw | ConvertFrom-Json`；对每个 source_graphs 路径读取 JSON，核对 reference、nodes.Count、根 spell_transforms.raw.m_MissileInfo/m_ImpactInfo；`git diff --check`。完整来源重读使用 `python -m pytest tests/test_super_missile_source.py -q`，本轮未重新执行这项昂贵检查。

UI 校对 SHA256 核对仍为 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。不改用户配置。

## 已有验证节点与限制

本轮读取 work/pytest-missile-isolated.log 终态：1187 passed / 4 deselected / 33 warnings / 372.30s。它属于原 0329198 节点；两个新增源重读测试另进程通过的记录见 ORIGINAL_MISSILE_PATH，不能合并伪称本轮单次全量 1189。

读取已有 GPU 成功退出和 Windows Result: Success；专题记录 27 路径/GPU、41 标准效果/146 帧。已有双客户端比赛 76fce3ae9d4040578effc4e8e26dd907，15 回合/version56，两端 hash 一致，详见 ORIGINAL_MISSILE_PATH。它们不是本轮新验收，也不能代表刀扇/刺骨整卡或人工视觉通过。

## App 项目操作

本轮 list_projects 与 set_thread_title 均返回 MCP -32602 / Invalid app tool request，工具目录无 create_project。故 Project 创建、仓库归属、聊天改名和 Feature Chat 创建均**未完成**。这只是当前宿主工具能力/调用失败事实，不推断产品整体不支持这些功能；不修改 App 内部数据库冒充 UI 操作。

人工最小收纳步骤：在 Codex 将 E:\HS\HearthstoneClone 添加/收纳为“炉石开源复刻”Project，并将本聊天移入、改名“00-总控-架构与任务分发”。下一步只在该 Project 打开 F002-SuperSpell命中分派与生命周期契约，启动词见 tasks/WORKFLOW。没有创建项目外 Feature Chat。

## 资源边界

gitignore 已覆盖 local/build/work、原音频/纹理/bundle/程序集和测试图片目录；git ls-files local build UnityClient/Assets/StreamingAssets 无结果。现有 manifests 115 个受跟踪，主要为研究元数据/映射，不等于逐文件公开授权审计已完成。没有删除资产、强制改写 Git 历史或发布仓库。

## 变更与交接

新增 AGENTS、概览/状态/决策/问题，原架构和路线增加当前入口；新增五份视觉策略、机器数据种子和任务协议/模板/索引/四 Task。旧专题不搬迁。TASK-0001 仓库部分完成，App UI 外部项如实开放；TASK-0002 为下一有限执行任务，0003/0004 等待依赖。不立即继续开发。

文档初始化提交由 `git log --oneline -- docs/tasks/TASK-0001-workflow-initialization.md` 定位，避免文档提交自引用自身 hash。
