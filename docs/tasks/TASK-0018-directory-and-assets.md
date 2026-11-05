# TASK-0018：目录与资源整理

- 状态：IN_PROGRESS；资源包导出及本机基本检查完成，干净克隆和独立验收尚未完成。
- 主目录迁移至 `E:\HS\OpenHearthstone`，历史工作树位于 `E:\HS\agent-worktrees`，已用 `git worktree repair` 修复登记。
- 原 `local/runtime`、`local/cache` 已迁往 `assets/runtime`、`assets/cache`；研究目录迁往 `assetsresearch`。旧路径使用目录联接兼容。账号数据库和 UI 校对仍位于 `local`。
- `tools.export_all_art` 从只读游戏安装逐包导出资源至 `assets/extracted`，逐包报告位于 `assetsresearch/bulk-export`。输出使用临时文件、按包续跑并检查磁盘余量；不能将运行过程视作完整覆盖。
- 全部 5,155 个源资源包均已处理，逐包报告现无错误；`assets/extracted` 约 283,584 个文件、58.8 GB。可解析对象按包写为 JSONL，贴图、音频、网格等按类型单独导出。特殊 BC6H 与 RGBA64 纹理保留原始数据并生成 8 位预览，预览并非无损 HDR。空纹理和空网格以元数据记录。
- 验证：16 项定向测试通过，其中包含空白目录重建运行时联接及保留现有卡牌数据库；迁移后后台 Player 启动 15 秒未记录异常。用户 UI 校对与 Main 场景哈希保持不变；未进行人工界面或对局验收。
- 未完成：独立核验所有文件的语义和完整精度，验证干净克隆的素材路径与完整使用流程。GitHub 对大文件和仓库体积有限制，未来发布需单独设计存储与上传方案。
