# 项目概览

项目名：炉石开源复刻 / Hearthstone Open Replica。现有仓库目录名 HearthstoneClone 保持不变。

目标是高还原度、可自由修改、可扩展、支持单机与局域网的开源炉石复刻。客户端、游戏逻辑、视觉运行时和数据系统由本项目自主运行；长期支持自定义卡牌、冒险、Boss、视觉效果与 Mod。上述长期能力不代表目前全部可用。

现有 Unity Windows 客户端与 Python 权威服务器是基础，不重写。开源代码采用仓库 LICENSE 所示 AGPL-3.0-or-later；参考项目许可分别保留于 licenses。原版专有资源不属于项目代码许可证。

视觉技术优先顺序：自主逆向/等价实现；本机原资源导入后由自主运行时执行；少量未还原效果可选隔离 Original Runtime Fallback。原版最终角色是研究样本、Ground Truth、可选资源来源，不能成为核心游戏必需进程。没有原版安装也完整运行是目标，目前尚未证明。

长期上下文以 Git 中的文档/代码为准。入口： [当前状态](PROJECT_STATE.md)、[架构](ARCHITECTURE.md)、[路线](ROADMAP.md)、[决策](DECISIONS.md)、[任务](tasks/INDEX.md)。
