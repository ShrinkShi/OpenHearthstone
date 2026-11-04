# 任务登记

编号永久稳定；下一个可分配编号 TASK-0018。Feature/Verification 聊天共用被验收任务编号，不另造一套完成状态。未开始任务先写可核实背景，启动时总控核对入口和范围。

| Task | 当前状态 | 优先级 / 依赖 | 聊天 / branch/worktree | 验证 / 合并 |
| --- | --- | --- | --- | --- |
| [0017 UABEA 收藏界面研究](TASK-0017-uabea-collection-research.md) | IN_PROGRESS | P1；服务0016 | UABEA 原生库和程序集静态研究 | PC 主相机、托盘、音源及翻页蒙皮已接入；原着色器、逐帧和 Player 验收待完成 |
| [0016 收藏界面对照修正](TASK-0016-collection-reference-alignment.md) | IN_PROGRESS | P1；承接0014/0015 | 原资源、界面流程和底栏对齐并行研究 | 待实现、实际回放与独立复核 |
| [0015 卡组列表和收藏式组卡](TASK-0015-deck-editing.md) | IN_PROGRESS；本轮功能有界验证 | P1；承接0014 | 独立 UI/账号协议及独立复核，总控串行整合 | 5b8aab3；Build r9、63 Python、Editor GUI及Player通过；标准轮换、游客和视觉边界仍开放 |
| [0014 收藏界面与任务校对](TASK-0014-collection-and-quest-layout.md) | IN_PROGRESS | P1；依赖0013 | 独立任务布局、收藏列表、详情及总控整合 | 9e1aa8a；Build r18、38 Python、Editor r16与Windows direct r16通过；完整原版视觉和关联数据仍开放 |
| [0013 Hub底栏、设置、好友与购包交互](TASK-0013-hub-chrome-options-social-packs-quests.md) | DONE（本轮本地有界范围） | P1；依赖0010 DONE，承接0011/0012 | F026 Native/Hub/Social/Quest及V026独立worktree；root串行整合与运行 | 固定0f379fa/DLL4255CA07；Build r7、41 Python、Editor207输入33RPC34图及Windows13步24请求29图通过；独立V026 b7830d0有界PASS，旧FAIL保留 |
| [0012 PC界面原生资源](TASK-0012-hub-native-resources.md) | IN_PROGRESS；本轮实物AWAITING_HUMAN_REVIEW | P1；服务0011并行 | F025独立Native/Hub与V025，root串行构建 | ac95ced/F07640E5；包轴/UV、原木紫底/旧阴影、收藏木框亮纸/题签清理由V025有界通过；完整原相机/Shader/动态/逐像素仍开放 |
| [0011 PC主界面与账号页面](TASK-0011-hub-collection-packs-shop-quests.md) | IN_PROGRESS；四项本地修正独立通过 | P1；依赖0010已DONE | F025 Hub/Quest与独立V025/worktree | GUI-r7真实153输入/26RPC/25图、同DLL direct-r7八步/14请求/22图通过；4+4任务/收藏弹层/设置账号等有界通过，不扩大完整官方保真 |
| [0010 账号与收藏经济](TASK-0010-accounts-and-economy.md) | DONE（Python后端本地有界范围） | P1；现有Server/卡牌元数据 | F024 Account及F025 Quest，独立V024/V025 | 原37228af/46项PASS保留；F025四槽迁移/能力判别已测，42项定向组合及独立8项通过，用户旧DB只补槽，0011继续 |
| [0009 气泡校对与召唤语音](TASK-0009-battle-calibration-summon-audio.md) | DONE（本地有界范围） | P1；承接0006/0007 | F024 Battle及独立V024 8bf73a8 | F6双页面/文字布局持久化、Summon报告兵原声、CS2_106/EX1_567实际8Player与原装备/耐久/损坏声通过；不扩为完整官方音画保真 |
| [0008 对阵开场与起手流程](TASK-0008-versus-opening.md) | DONE（本地有界范围） | P1；复用0007原声基础及现有规则 | F023独立Agent/worktree及V023 | r9原网格/UV/Transform派生VS、原三段播报与Keep/START并行双端PASS；V023独立通过，原shader/动态Actor不扩为已恢复 |
| [0007 英雄语音与七种表情气泡](TASK-0007-hero-voices-emotes.md) | DONE（F023本地有界范围） | P1；复用当前网络/原资源基线 | F023独立Agent/worktree及V023 | F022原99键有界DONE保留；24Player覆盖11英雄Attack/原PCM，r9对齐左3右4、7项/Keep/投降双端PASS，V023独立通过 |
| [0001 长期工作流初始化](TASK-0001-workflow-initialization.md) | DONE（仓库初始化）；App UI 外部项未完成 | P0 | 00-总控；master / E:\HS\HearthstoneClone | 文档/机器数据检查；串行提交，无待合并分支 |
| [0002 命中分派与生命周期契约](TASK-0002-superspell-impact.md) | DONE | P1a | 串行 master / E:\HS\HearthstoneClone；Agent 见 AUTOMATION_RUN | F002B 修复 752f909；V002-r2 PASS 213acba；总控登记完成 |
| [0003 双卡根编排](TASK-0003-superspell-orchestration.md) | DONE | P1b；0002 DONE 已满足 | 独立 F003 / V003-r6；串行 master | 修复3868df3；V003-r6 PASS 59ae909；总控登记完成，视觉边界未升级 |
| [0004 关键帧与 Shader 方法闭环](TASK-0004-visual-comparison.md) | BLOCKED；缺官方可比序列 | P1c；0003 DONE 已满足 | 独立F004 / V004-r2；串行master | 实现16b1804；V004-r2局部PASS/阻塞确认ae97e87；旧回合在此停止，整体Unknown |
| [0005 法师火焰冲击与指示器](TASK-0005-mage-fireblast.md) | READY_FOR_REVIEW；用户复验火球/命中特效基本正常；输入锁子项由V006通过；F005C其余改动仍待独立V005-r3；整Task官方保真未闭合 | P1；不依赖0004，复用0002/0003 DONE基线 | F005C → 新V005-r3；串行master | 首轮修复d4fc538、V005-r2 PASS 5e34c98为历史；第二轮五帧原图/哈希与延迟根因已记录 |
| [0006 全职业基础英雄技能表现与输入锁](TASK-0006-all-hero-powers.md) | IN_PROGRESS；完整官方还原未完成 | P1；承接当前技能表现/规则基线，不等待0004 | F023三Feature及独立V023；主目录串行Unity/Player | H03旧武器Death/无Arrival永锁已修，双端v18/19/绿色按钮/结果退场PASS；H04/H09原Start音源位置与Listener复核，H10热帧约4ms但cold约263ms/偶发慢帧仍有；H04 halo、原Shader/Actor动态边界保留，H02/H03源JSON仍原哈希 |

自动编排已启用（DEC-005）：总控实际核对能力并派发独立上下文 Agent，正常阶段自动继续；不以创建可见 UI 聊天为前提，不预建空 Agent。仅确实无法创建独立任务时写 docs/handoffs/NEXT_THREAD.md 并请求人工。运行/Agent/固定提交见 [AUTOMATION_RUN](AUTOMATION_RUN.md)。总控用 `git log --oneline -- docs/tasks/<file>` 查询交接与文档提交。协议见 [WORKFLOW](WORKFLOW.md)，新任务复制 [TEMPLATE](TEMPLATE.md)。
