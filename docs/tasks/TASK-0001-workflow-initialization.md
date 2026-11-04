# TASK-0001

## 任务名称
长期项目工作流与技术路线初始化。

## 背景
用户要求暂停横向卡牌特效，把长聊天转为总控，以 Git 文档恢复上下文。保留现有代码与逆向成果。

## 当前已知事实
Confirmed：仓库 E:\HS\HearthstoneClone，master，起点 0329198；原来无根 AGENTS。已有丰富 docs/ORIGINAL_*、架构/路线与忽略的 .dev-records。开始时有 original-script-audit 增量和 NativeSuperImpact 草稿。App list_projects / set_thread_title 调用失败 Invalid app tool request。

## 目标
建立操作规程、长期状态、技术决策、研究/结构化数据库种子和稳定 Task；保存 WIP；提供下一聊天入口。

## 非目标
不开发下一功能，不扩卡，不运行原版，不使用 Computer Use，不改写历史/搬迁旧专题。

## 相关文件
AGENTS.md；docs/PROJECT_*、ARCHITECTURE、ROADMAP、DECISIONS、KNOWN_ISSUES；docs/visual、hearth_visual_db、tasks、research。

## 实施约束
仅文档整理和未验收草稿 checkpoint。不把局部检查等同整卡恢复，不覆盖校准。

## 验收标准
- 根状态/构建/测试/相关代码与已有研究经过只读检查；新入口指向真实文件。
- 机器数据 JSON 可解析、来源路径存在；任务/路线一致；git diff --check。
- 文档与草稿分别提交；干净工作区；下一任务不自动执行。
- App 操作成功或明确说明实际工具失败及最小人工步骤。

## 当前状态
DONE（仓库初始化）；Project/改名/归属/Feature Chat UI 操作未完成，见下。

## 实施记录
2026-10-15：原无限全卡 Goal 已 paused。整理双卡、路径、Shader 和 GPU 证据；建立任务 0002~0004，不创建空聊天。旧文档保留并增加当前入口。原 .dev-records 默认忽略，重要结论改存跟踪 docs。

## 测试结果
本轮测试和文档完整性结果见 research/WORKFLOW_INITIALIZATION_2026-10-15.md；历史 1187/两项来源/GPU/比赛仅作为旧已验证节点证据，不为未验收草稿背书。

## 最终结果
仓库长期工作流已落盘。App 项目建立、聊天归属/改名、新 Feature Chat 未完成；工具请求被宿主拒绝，非已完成。人工操作：将 E:\HS\HearthstoneClone 加入/收纳到名为“炉石开源复刻”的 Project；将当前聊天归入并命名 00-总控-架构与任务分发。随后仅在该 Project 打开 F002。

## Commit
暂停草稿 checkpoint 与文档初始化分别提交；具体哈希见 research/WORKFLOW_INITIALIZATION_2026-10-15.md 和 git log -- 本文件（文档提交不自引用自身哈希）。

## 遗留问题
App UI 项未完成，KI-007。独立功能实现与独立验收分别在后续任务，不在总控继续。
