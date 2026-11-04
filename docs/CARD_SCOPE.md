# 卡池定义

目标池：原九职业采用历史 Basic + Classic（包含迁移到 Hall of Fame 的历史卡）；中立同源。恶魔猎手采用上线 Basic + Initiate。死亡骑士采用上线 Core + Path of Arthas，单独定义，绝不称为经典卡。

历史 ID 清单从版本化 HearthSim CardDefs 生成；当前客户端提供本地中文名称、美术和声音。历史数值与当前数值冲突必须记录，不能按当前 LEGACY 标签直接推断。

目标池不等于已验证可用池；未实现卡在状态文档中保留，客户端不能默认为空效果。

当前可运行卡池为 Fireplace 自带 build 53261 历史快照的 431 个 collectible ID：Basic/Classic、按历史 ID 前缀恢复的 Hall of Fame，以及 Demon Hunter Initiate。这里明确采用 2020 年经典体系数值，不声称为 2014 首发版本；后续逐一审计补充/迁移牌。`data/scope.json` 保存准确 ID。

死亡骑士目标已从 HearthSim `hsdata` tag 159202（提交 `742c0010782909dbfc3ee689d46e71bbb94e3c15`）确认：Core 32 张 + Path of Arthas 26 张，共 58 个不同 ID。此池属于 2022 上市卡池。已接入尸体、符文、技能与事件扩展，基础执行与专项测试通过；完整历史发现/随机生成池仍待核验。目标总池为 489 张。

显示数据库包含已加载快照的全部 9,603 条定义，以便显示随机生成的池外卡。此处的元数据覆盖不开放池外卡组，也不代表历史随机生成池或生成牌效果已经全部核验；Playable 与服务器组牌验证继续限定上述 489 张。

执行 `python -m tools.prepare_data` 取得固定快照并校验 SHA256 `96c094b6971b44c8238a7aa05aa2831d809b7ea2d943e50eee8b165e1db1b099`。元数据来自 [HearthSim 固定提交](https://github.com/HearthSim/hsdata/tree/742c0010782909dbfc3ee689d46e71bbb94e3c15)，不从当前 UI 的 CORE/LEGACY 标签推断历史。
