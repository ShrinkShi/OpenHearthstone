# F024 账号/经济JSON协议v1

保留现有逐行TCP/权威Match。正式CLI默认启用`accounts_v1`，监听127.0.0.1:37240、DB为local/accounts.sqlite3，可用`--host/--port/--logdir/--account-db`覆盖。既有测试`Server(logdir)`不传AccountService保持访客。菜单与对战连接分别resume同token，再以`join,deck_id`加入；共享SQLite仅Server管理。数据库与同名`.token-key`需一并备份。

每请求有`type,request_id`，变更幂等。统一响应：

```json
{"type":"account_result","operation":"account_login","request_id":"...","ok":true,"error":"","token":"...","snapshot":{"account_id":"...","username":"...","display_name":"...","gold":1000,"dust":0,"revision":1,"cards":[],"decks":[],"packs":[],"quests":[],"shop":[]},"opened_cards":[],"open_id":""}
```

账号取认证Peer，不信客户端account_id/余额/进度/抽卡。error不泄露凭据/堆栈；token仅必要响应，server存hash。严格验证ID/数量/布尔，测试最大snapshot在客户端1MB行限制内，若需分页先协调UI owner。

请求与字段：
- account_register: username,password,display_name。
- account_login: username,password；account_resume: token；account_logout撤销当前token。
- logout成功及同请求重放只回`ok=true,snapshot=null`；其它变更的幂等缓存返回前仍验证当前有效会话，过期或已撤销凭据不得读取快照或复活会话。
- account_state读取snapshot。
- account_deck_save: deck_id(新建空),name,class_id,cards(CardId数组)；account_deck_delete:deck_id。
- account_craft/account_disenchant:card_id,golden,count。
- account_shop_buy:set_id,count，每包100；account_pack_open:set_id，一次1包/5卡，返回opened_cards/open_id并入库存。`set_id`为严格JSON整数（CardSet数值），snapshot.packs/shop同样是整数；不接受字符串或布尔。CardId/deck_id/request_id仍是字符串。
- account_quest_reroll:quest_id，服务端限制次数/刷新。

snapshot.cards项`card_id,normal,golden,free`，free不可分解但可组牌；decks项`deck_id,name,class_id,cards,revision`；packs项`set_id,count`；shop项`set_id,name,price`（可加原素材逻辑ID）；quests项`quest_id,period,difficulty,title,description,progress,target,reward_gold,completed,reroll_available`；opened_cards项`card_id,golden,new_card`。可加daily/weekly_reset_at、unread_rewards等兼容字段。

默认规则（未指定项明确为本服设计，不冒认官方概率）：
- 每日/周各3槽easy/normal/hard。每日40/60/80金，周80/120/160。UTC+8零点/周一刷新；真实已接受对局事件/结果推进，完成同事务自动发金一次，可注入时钟测试。
- 用户已选择新账号仅赠基础卡牌、1000金/0尘，其余通过开包和制作获得。已核基础BASIC153种与免费CORE32种，共185种；11职业starter均只用免费持有且通过现有对战校验的30张卡。旧profile只读保留，不因旧preset或自定义卡组自动赠送专家/扩展卡牌；本轮未实现旧profile迁移工具，不提供不受信赠卡RPC。
- 普通制作消耗奥术之尘40/100/400/1600、分解产尘5/20/100/400；金色制作消耗奥术之尘400/800/1600/3200、分解产尘50/100/400/1600，按Common/Rare/Epic/Legendary。Free/core按metadata不可制作/分解。
- 卡包5张至少1稀有或更高，服务器随机与重复保护；权重/保底配置化并记录本服默认。商店仅可抽收藏池扩展包，无真实货币。收藏可含未实现对战卡；匹配仍严格原可玩范围。
- 商店当前16种真实普通随机包；NAXX/BRM/LOE/KARA/YEAR_OF_THE_DRAGON/DEMON_HUNTER_INITIATE/PATH_OF_ARTHAS七特殊来源合集不卖随机包，卡牌仍留收藏/制作。名称按CardSet枚举，禁止手写ID错位或冒险书冒普通包。
- 正式join使用账号保存deck_id，server取class/deck/昵称并验证库存。相同账号不可占两个席位。旧join仅测试访客。hero_emotes_v1/Match version/hash不变，账号进度不让规则等待动画。
