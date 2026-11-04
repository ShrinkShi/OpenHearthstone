# 账号与经济服务

## 本机体验入口（F026）

双击仓库根目录 `Start-LocalDuel.cmd`，启动本地账号服务及两个独立profile的客户端。点右下齿轮→“选项”→“账号与会话”，分别注册两个账号；登录和退出也在这里，关闭设置返回原页面。新账号自动获得基础/核心卡牌、1000金币、0奥尘及可用基础卡组。主机默认127.0.0.1；遇仍运行的旧访客、三槽任务或不含好友能力的服务时，启动器保留旧进程并选择新版服务端口。

底栏左下为好友按钮/人数及本地时钟，右下齿轮左侧为金币与奥尘。好友面板支持账号名申请、接受/拒绝/取消、移除和隐私；打开/重新打开面板会刷新在线状态。游戏菜单的双列选项可保存画面与声音设置。商店点击卡包弹出详情，调整数量后确认一次购买多包，取消不扣款。F6左侧选择“任务页”，可调整背景、八个任务及标题/描述/进度/图标等布局和字体，Ctrl+S保存、F7重读；离线预览的替换图标不会修改账号任务。需要独立试调时可用 `Start-UIEditor.cmd --ui-layout E:/HS/HearthstoneClone/work/my-test-layout.json` 指定校准文件。

主界面“传统对战”使用账号中保存的卡组匹配；“我的收藏”可筛选卡牌，点击或右键卡牌打开上层详情进行制作/分解，关闭后继续浏览；右侧为卡组列表，“组建卡组”可编辑保存。“商店”购买100金币卡包，“开包”将左侧库存拖到槽内、逐张揭示五张卡。卡牌在服务器开启卡包时即写入收藏，揭示动画不会重复发卡。每日、每周各四个任务：每日40/60/80/60金币，每周80/120/160/120金币，第四项复用普通难度；旧账号补槽保留已有任务进度和奖励。进度来自服务器接受的对局事件。冒险、竞技和其它模式仍为按需求预留的入口。

账号数据位于 `local/accounts.sqlite3`，会话密钥sidecar为同路径`.token-key`；客户端登录会话按profile加密保存。F6战斗校对器已增加双方英雄对话气泡及左三右四的七种表情布局/文字，F7重读。既有用户卡组文件和`local/ui-calibration.json`保留，不作为测试夹具覆盖。

本轮实测与输入证据边界见 `docs/verification/F026-hub-controls-and-packs.md` 和独立 `docs/verification/V026-hub.md`；它们区分Editor原生GameView输入、Standalone handler回放与人工桌面操作。

F024在现有逐行TCP服务器中增加账号权威层。服务器拥有SQLite连接和卡牌规则；Unity只提交账号操作、请求编号及保存的卡组编号。账号进度与match状态分开保存，金币任务不会阻塞规则结算或表现动画。

## 初始收藏来源

卡牌目录从已初始化的完整`fireplace.cards.db` metadata生成，包含收藏性、CardId、职业、扩展集合、稀有度、费用和死亡骑士符文。当前本机数据有9,603条定义、2,602条收藏卡、489条当前可玩scope卡。

初始免费卡仅来自`BASIC`、`CORE`和可能存在的`LEGACY` set：

| CardSet | collectible | 在当前playable scope | 处理 |
| --- | ---: | ---: | --- |
| BASIC (2) | 153 | 143 | rarity为FREE；按每卡最大2份免费收藏 |
| CORE (1637) | 32 | 32 | 含高稀有度卡；按普通卡2份、传说1份免费收藏 |
| LEGACY (1635) | 0 | 0 | 当前metadata没有卡 |
| EXPERT1 (3) | 240 | 240 | 不作为赠卡来源 |

每个账号获得185个基础CardId，另有1000金币和0尘。`free`副本可组牌，永远不参与分解。CORE set和FREE rarity卡的普通/金色制作及分解请求均拒绝，不扣资源；同一请求编号重试也不改变库存、余额或revision。不会读取、删除或自动上传`local`下的旧profile/deck文件。

服务端只为能从免费基础库存和当前playable scope构成合法30张卡组的职业创建starter deck。当前11个职业都满足，职业限制、最大复制数及死亡骑士符文仍经过`game.database.validate_deck`；死亡骑士构建器排除Blood/Frost卡。正式匹配时还会重新验证库存，因此之后拆卡或改卡组不会绕过检查。

## 存储与授权

`AccountService`每次操作使用独立SQLite连接，以`BEGIN IMMEDIATE`配合外键、CHECK约束和事务保存账号、session、收藏、卡组、卡包、任务、事件去重和请求去重。每个账号的金币、尘、卡牌数量不可为负；卡组/收藏/任务记录都按account_id限定。

密码以PBKDF2-HMAC-SHA256、每账号随机salt保存。session表存token SHA-256、到期时间和撤销时间。明文token只在必要的登录/注册/resume响应以及当前Peer内存出现。token由本地密钥对账号、操作、request_id和请求指纹作HMAC生成，因此认证响应重试时可恢复同一token而无需在SQLite保存明文。密钥文件与数据库同路径后缀`.token-key`；密钥缺失且数据库已有session时服务器拒绝启动。

账号操作的request_id限定1到128位安全ASCII字符。同一账号和操作重放时返回之前保存的结果；相同编号若请求指纹变化则拒绝。业务写入、返回快照、幂等响应均处于同一SQLite事务；异常会回滚，后续可用同一请求编号重试。匹配事件使用`match_id + version + event_index`去重，完成任务和发金币在同一事务提交。

## 卡组与经济规则

- 卡组保存/匹配要求30张，服务端验证当前playable scope、职业/中立、同卡上限、死亡骑士符文，以及`normal + golden + free`库存。
- 每账号最多30副卡组。新建时`deck_id`传空字符串；服务端生成ID，并在响应顶层和snapshot.decks中返回。更新/删除其他账号的deck_id失败。
- 普通制作消耗奥术之尘，按Common/Rare/Epic/Legendary为40/100/400/1600；分解产尘5/20/100/400。金色制作消耗奥术之尘400/800/1600/3200；金色分解产尘50/100/400/1600。购买卡包才消耗金币。
- 卡包每包100金币，一次开一包、五张卡，至少一张Rare或更高。商店只列出有原版普通卡包来源且含MINION/SPELL/WEAPON收藏卡的16个set；冒险合集、恶魔猎手新兵、阿尔萨斯之路等7个特殊集合不出售随机卡包，其可收藏卡仍可制作/收藏。免费Basic、HOF、Core、Battlegrounds、Lettuce/Mercenaries和特殊卡型同样不作为普通卡包。Expert1可购买经典包，但不会作为初始赠卡。
- 默认稀有度权重(Common/Rare/Epic/Legendary)为70/22/7/1，金卡率5%。每个包至少一格从Rare+权重中抽选。默认重复保护先排除已收藏及本包已抽出的CardId；某稀有度没有新卡时在本格允许的稀有度中找未拥有卡，只有不存在未拥有候选时才允许重复。每张返回`new_card`标志。
- pack权重、金卡率、重复保护由`AccountService`构造参数和server CLI配置；它们是本服掉率，不声明官方概率。

## 任务

任务按UTC+8刷新，daily在本地零点，weekly在周一零点。每组四槽，包含easy/normal/hard各一项，第四槽复用normal难度和奖励：

| 周期 | 难度 | 默认目标 | 奖励 |
| --- | --- | --- | ---: |
| daily | easy | 完成1场对局 | 40金币 |
| daily | normal | 打出10张卡牌 | 60金币 |
| daily | hard | 赢得1场对局 | 80金币 |
| weekly | easy | 完成3场对局 | 80金币 |
| weekly | normal | 打出30张卡牌 | 120金币 |
| weekly | hard | 赢得3场对局 | 160金币 |

可在零进度时对每个槽位免费重置一次，重置后保留相同难度和奖励。事件来源是`Match.apply`已接受动作的服务端EventObserver以及比赛终局结果；无客户端提交进度接口。无效动作和action_id重放不会推进任务。任务模板与奖励定义在`game/accounts/economy.py`，UTC+8时钟可注入测试。

## 启动与验证边界

`python -m game.server`默认账号模式，使用`--account-db`指定SQLite文件，默认`local/accounts.sqlite3`。token密钥sidecar为`local/accounts.sqlite3.token-key`。默认监听127.0.0.1:37240；可设置`--host`、`--port`、`--logdir`。默认包权重可用`--common-weight`、`--rare-weight`、`--epic-weight`、`--legendary-weight`、`--golden-rate`调整；`--no-duplicate-protection`关闭重复保护。`Server(logdir)`在没有AccountService时继续用于旧guest测试，不作为正式CLI模式。

全收藏压力夹具将2,602条卡、30副卡组、23个包集合及六项任务同时序列化，JSON行实测154,105字节，低于客户端1MB限制。账号测试只在pytest临时目录创建SQLite数据库和token密钥；TCP集成测试用独立随机端口。未在F024测试中启动现有服务端口、读取/修改旧profile或缓存，亦未运行Unity客户端或原版DLL。详细JSON字段见`docs/F024_ACCOUNT_PROTOCOL.md`。
