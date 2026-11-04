# V024：账号、收藏与经济独立验收

**最终结论：固定候选 `37228af1cc4ef343d43c060600df52eb22a1ba72` 的 V024 有界 Python 后端验收通过。** 账号授权、资产/事务、对局结算、23 项 CardSet 标签映射与 16 个随机包来源均有行为验证；不代表 Unity UI、Player、官方视觉或公开部署验收。

## 固定对象与验证环境

- 初始候选 HEAD：`2ad6fcc7cd6c9fdde0788c1db1e92a8d6354e466`；账号实现：`057a5c86e5089a9d42fc7df950f117a9b1d67dae`。后续标签候选 `24c0adfde55314af592d4098f284e17a4d20f70b`，最终商店过滤候选 `37228af1cc4ef343d43c060600df52eb22a1ba72`（父提交 `24c0adf`）。
- 验证分支按序集成标签和过滤候选为 `06e4317866a730aadae3047ff1fdf6b48b19940c` 与 `8b44674a29b985d0cf7b97304afe83bd03585b7c`；本报告最终更新另有独立提交。
- 分支：`verification/v024-accounts`；验证工作区：`E:/HS/agent-worktrees/v024-accounts`。
- 按要求阅读 `AGENTS.md`、TASK-0010、账号协议、经济规则及后端审查技能；以候选运行代码和行为测试验证文档声明。
- Python：`E:/HS/HearthstoneClone/.venv/Scripts/python.exe -X utf8`。测试数据库和 token-key 均在 pytest 临时目录；TCP 服务绑定随机空闲 localhost 端口。
- 私有证据：`work/v024-accounts/pytest-focused.txt`、`pytest-labels-final.txt`、`pack-source-fail.txt`、`packset-crosswalk.txt`、`packsets-final.txt`。未触碰 PID 17492、37240 服务、默认 `local/accounts.sqlite3`、用户 profile/cache；未运行 Unity、Player 或原版 DLL。

## 验证结果

- 固定实现原定向基线：`tests/test_accounts.py tests/test_network.py tests/test_match.py`，**38 passed**。
- 独立新增 `tests/test_v024_accounts_acceptance.py` 的 5 项行为测试后，完整定向命令 **43 passed**：

  ```powershell
  E:/HS/HearthstoneClone/.venv/Scripts/python.exe -X utf8 -m pytest tests/test_accounts.py tests/test_network.py tests/test_match.py tests/test_v024_accounts_acceptance.py -q
  ```

- `py_compile` 覆盖服务端、账号模块和相关测试，exit 0；`git diff --check` 通过。

| 检查面 | 结果与行为证据 |
|---|---|
| 初始收藏与卡组 | 注册后 1000 金币、0 尘、185 个 BASIC+CORE CardId；没有 EXPERT1 免费卡；11 个初始牌组均为合法 30 张且库存足够。商店包卡牌类型过滤测试通过。 |
| 身份与对象授权 | 未 resume 时伪造 `account_id` 买包或带他人 `deck_id` join 均失败；认证 A 在请求体伪造 B 的 `account_id` 仍只读 A；A 删除或 join B 的保存卡组失败，B 的金币和卡组不变。 |
| 会话与幂等 | 原测试覆盖密码 hash、resume、过期/撤销 token、logout 幂等、同 ID 不同 payload 拒绝及相同请求并发重放。新增注册成功请求在 logout 后重放：响应虽重放旧 token，但 DB 会话仍标记撤销，resume、state、join 均不能使用该 token。删除有活动/历史 session 的数据库 sidecar key 后，重启明确失败。 |
| 经济并发与回滚 | 新测试并发 16 次单包购买恰有 10 次成功并扣足 1000 金；并发开包恰消费 10 包并加入 50 张卡；同一卡并发分解仅一次成功；预置隔离测试尘后并发制作仅能成功支付的 5 次。原测试在开包中注入异常，确认库存与包数量一起回滚。 |
| 服务端卡组规则 | 新测试保存合法卡组后从服务端按 ID 重新加载成功；29 张、未持有但在 scope 内的卡、scope 外卡均不能保存。原测试验证跨账号卡组删除保护和匹配使用服务端保存卡组。 |
| 任务与时区 | 原测试用 `Match.apply` 接受的真实事件推进 CardPlay 进度；事件/比赛结果重复投递不重复发奖；UTC+8 日界及周一刷新时间、六个奖励档位和 reroll 限制断言通过。 |
| 网络输入与恢复 | 已认证请求的 bool、负数和超过 1000 的购买数量均拒绝且未改变资产。包含 NaN 的账号 JSON 被拒绝；1200 层嵌套输入后同一 TCP 连接仍可 ping/pong。最大收藏/卡组/包快照测试断言序列化行小于 1,000,000 字节。 |
| 对局网络语义 | 原 TCP 测试覆盖双连接 resume、同账号双席拒绝、伪造 join 字段被忽略、显式 concede 的广播和奖励。新增真实 socket 断开测试：对手收到 finished/Lost/Won 广播；两账号分别获得完成奖励，胜者另获胜利奖励。 |

## 审查发现与限制

### 阻塞问题

未发现可复现的阻塞问题。按本次范围，账号隔离、经济事务、请求幂等、token 撤销、卡组可玩 scope 和任务结算行为均通过上述证据。

### 非阻塞加固建议

- `game/accounts/service.py:378-406` 的登录路径同步运行 PBKDF2（默认 310,000 次），并由 `game/server.py:113-120` 的 asyncio 请求 handler 直接调用；未见登录失败节流/冷却。默认只监听 `127.0.0.1` 且此版本不得公开部署，因此不阻断本地验收。若以后允许不受信任的 LAN 客户端，建议增加按账户/来源限流，并把密码验证移出事件循环，避免登录洪泛拖住对局网络。
- 测试将 PBKDF2 迭代数降为 10,000 以控制时长；没有对生产迭代数下的登录延迟或压力作基准测试。
- 只运行账号、网络、规则相关定向测试，未运行全量 pytest，也未执行 Unity、Player、正式 UI 或视觉回归。不得将本报告解读为客户端交付验收。

## 验收变更

本分支仅新增独立行为测试和本报告；未改 `game/` 业务实现。失败输入、并发操作和断线场景全部使用临时数据库及随机 TCP 端口。工作区证据不含测试密码输出。

## 标签修正增量复核

**标签结论：23 个 CardSet 的 ID、枚举名、中文标题和本地原始卡牌 metadata 对应通过。商店集合范围结论：候选 `24c0adfde55314af592d4098f284e17a4d20f70b` 仍未通过；其商店把 7 个特殊来源集合当作随机包，独立新增的 16-set 规则测试复现失败。等待修正后的固定候选再做最终 Task-0010 验收。**

- F024 标签候选 `24c0adf` 的父提交为 `2ad6fcc`；本验证分支将其 cherry-pick 为 `06e4317866a730aadae3047ff1fdf6b48b19940c`。在候选运行代码中直接读取 `hearthstone.enums.CardSet` 与 `fireplace.cards.db`，新增 `test_all_shop_titles_match_cardset_enum_and_metadata_card_ids` 核对 23 个枚举、中文标题和锚点 CardId 的真实 set_id。该测试 **1 passed**。
- 独立映射如下。样例 CardId 均存在于原始可收藏 metadata；每张的 `card_set` 与表中十进制 ID 相同。

| set ID | CardSet enum | 商店中文标题 | metadata CardId 样例 |
|---:|---|---|---|
| 3 | `EXPERT1` | 经典卡包 | `CS1_069` |
| 12 | `NAXX` | 纳克萨玛斯的诅咒 | `FP1_001` |
| 13 | `GVG` | 地精大战侏儒 | `GVG_001` |
| 14 | `BRM` | 黑石山的火焰 | `BRM_001` |
| 15 | `TGT` | 冠军的试炼 | `AT_001` |
| 20 | `LOE` | 探险者协会 | `LOE_002` |
| 21 | `OG` | 上古之神的低语 | `OG_006` |
| 23 | `KARA` | 卡拉赞之夜 | `KAR_004` |
| 25 | `GANGS` | 龙争虎斗加基森 | `CFM_020` |
| 27 | `UNGORO` | 勇闯安戈洛 | `UNG_001` |
| 1001 | `ICECROWN` | 冰封王座的骑士 | `ICC_018` |
| 1004 | `LOOTAPALOOZA` | 狗头人与地下世界 | `LOOT_008` |
| 1125 | `GILNEAS` | 女巫森林 | `GIL_113` |
| 1127 | `BOOMSDAY` | 砰砰计划 | `BOT_020` |
| 1129 | `TROLL` | 拉斯塔哈的大乱斗 | `TRL_010` |
| 1130 | `DALARAN` | 暗影崛起 | `DAL_007` |
| 1158 | `ULDUM` | 奥丹姆奇兵 | `ULD_003` |
| 1347 | `DRAGONS` | 巨龙降临 | `DRG_006` |
| 1403 | `YEAR_OF_THE_DRAGON` | 迦拉克隆的觉醒 | `YOD_001` |
| 1414 | `BLACK_TEMPLE` | 外域的灰烬 | `BT_002` |
| 1443 | `SCHOLOMANCE` | 通灵学园 | `SCH_199` |
| 1463 | `DEMON_HUNTER_INITIATE` | 恶魔猎手新兵 | `BT_173` |
| 1869 | `PATH_OF_ARTHAS` | 阿尔萨斯之路 | `RLK_015` |

旧 `2ad6fcc` 中的数字表把名称从 ID 20 起错位：例如 20 的 enum 是 `LOE`、卡牌 `LOE_002`，旧表却显示“上古之神的低语”；1001 的 enum 是 `ICECROWN`、卡牌 `ICC_018`，旧表却显示“狗头人与地下世界”。本次对旧字典复算还确认 21、23、25、27、1004 等 ID 同样错位。标签候选改用 `CardSet` enum 键后，这 23 项的关联通过。早期买包回归夹具曾把 enum 名称字符串传给只接受整数 `set_id` 的协议；最终候选已修为传 `int(CardSet)`，不把该夹具错误算作业务失败。

### 卡包来源与标题分开验收

- `24c0adf` 的 `catalog.pack_sets` 仍是 23 项，因为当前目录按可收藏 MINION/SPELL/WEAPON 类型把这些集合都归为随机卡包。用户目标要求各扩展包 100 金币、每包 5 张，但“卡牌集”不等于“卡包来源”。
- root 已确认本服 shop 应保留 16 个普通原版随机包扩展，并排除 `NAXX`、`BRM`、`LOE`、`KARA`、`YEAR_OF_THE_DRAGON`、`DEMON_HUNTER_INITIATE`、`PATH_OF_ARTHAS` 共 7 个特殊来源集合；这些卡仍可留在收藏/制作目录，不改 185 张初始 BASIC+CORE 免费卡。
- 独立新增 `test_shop_contains_only_actual_random_pack_expansions` 检查上述 16/7 集合划分。在 `24c0adf` 上 **1 failed**：实际 `catalog.pack_sets` 仍包含这 7 个集合。私有复现输出为 `work/v024-accounts/pack-source-fail.txt`，23 项 ID/enum/标题/CardId 全表为 `work/v024-accounts/packset-crosswalk.txt`。
- 来源依据：暴雪官方说明 Path of Arthas 26 张卡只能整套购买/制作、**不能开包**；Demon Hunter Initiate 是完成序章后免费获得；Galakrond's Awakening 的可收藏卡通过冒险关卡解锁，不是随机卡包。[官方 Death Knight Deep Dive](https://hearthstone.blizzard.com/en-us/news/23852696?blzcmp=blizzard-news)、[官方 Demon Hunter announcement](https://hearthstone.blizzard.com/en-us/news/23305788/introducing-the-demon-hunter)、[官方 Galakrond's Awakening announcement](https://hearthstone.blizzard.com/en-gb/news/23227142/galakrond-s-awakening-launching-january-21)。早期 NAXX/BRM/LOE/KARA 是冒险系列，标题对照可参见[卡牌系列列表](https://hearthstone.wiki.gg/zh/wiki/%E5%8D%A1%E7%89%8C%E7%B3%BB%E5%88%97?variant=zh-cn)。
- 候选自带的五来源买包/开包测试在本地协议类型修正后可通过，但其中 `LOE` 与 `PATH_OF_ARTHAS` 不应作为真实卡包来源验收；不能用该测试替代商店来源集合审查。

### 增量验证命令与状态

- 候选 24c0 的账号、V、网络与 Match 定向集合（在加入 16-set 拒绝断言前）：**45 passed**。命令：

  ```powershell
  E:/HS/HearthstoneClone/.venv/Scripts/python.exe -X utf8 -m pytest tests/test_accounts.py tests/test_v024_accounts_acceptance.py tests/test_network.py tests/test_match.py -q
  ```

- 当前候选新增的 16-set 规则拒绝断言在 24c0 上失败，修正版复测转 PASS；失败输入和输出保留在私有证据目录，细节见下文最终复核。
- 候选目录和标签 `py_compile` 通过；未运行 Unity、Player 或原版 DLL，未访问共享 `local` 账号/profile 数据。

## 最终商店来源修正复核

- 固定实现提交 `37228af1cc4ef343d43c060600df52eb22a1ba72` 只排除 7 个冒险/免费/固定获取集合，并把真实买包/开包用例从 `LOE` 和 `PATH_OF_ARTHAS` 改为 `GANGS` 和 `SCHOLOMANCE`。23 个 CardSet 中文标题仍全部可由 metadata 目录解析；特殊来源卡没有从收藏目录删除。
- `catalog.pack_sets` 当前恰好包含 16 个随机包集合：`EXPERT1`、`GVG`、`TGT`、`OG`、`GANGS`、`UNGORO`、`ICECROWN`、`LOOTAPALOOZA`、`GILNEAS`、`BOOMSDAY`、`TROLL`、`DALARAN`、`ULDUM`、`DRAGONS`、`BLACK_TEMPLE`、`SCHOLOMANCE`。`NAXX`、`BRM`、`LOE`、`KARA`、`YEAR_OF_THE_DRAGON`、`DEMON_HUNTER_INITIATE`、`PATH_OF_ARTHAS` 不在商店随机包集合。
- 真实买包/开包回归现覆盖 `GANGS`、`OG`、`ICECROWN`、`BLACK_TEMPLE`、`SCHOLOMANCE`，分别验证 100 金扣款、五张卡、每张卡 `card_set` 与购买 set ID 完全相同。独立 23 项 enum/title/CardId 表和 16/7 集合划分测试均通过。
- 对照旧候选：在 `24c0adf` 上 `test_shop_contains_only_actual_random_pack_expansions` **1 failed**，精确断言发现 23 个包来源与预期 16 个不同；在修正版 `37228af` 上整组相关测试 **46 passed**。原失败日志保存在 `work/v024-accounts/pack-source-fail.txt`，修正版测试日志在 `work/v024-accounts/pytest-labels-final.txt`。
- 最终命令：

  ```powershell
  E:/HS/HearthstoneClone/.venv/Scripts/python.exe -X utf8 -m pytest tests/test_accounts.py tests/test_v024_accounts_acceptance.py tests/test_network.py tests/test_match.py -q
  ```

- 服务目录及验收测试 `py_compile` exit 0，`git diff --check` 通过。该结论覆盖本机 Python API/SQLite/TCP 行为；不覆盖 Unity UI、Player、全量测试或视觉一致性。
