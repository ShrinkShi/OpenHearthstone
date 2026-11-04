# 网络协议

2026-10-09：keywords 增加有效 deathrattle、poisonous，沉默后移除。Play 实体已在 Zone.PLAY 时补公开 Summon/visual/owner/index，index 缺省 -1 沿用追加，支持战吼前入场与插入位置。不增加规则动作或动画等待，不公开对手手牌。新局应使用同版服务器/客户端，正在运行的旧局不强制重启。

2026-10-09 交战反馈增加 attack_buffed / combo_ready：前者比较当前攻击与底层卡牌的基础攻击，后者读取实际 has_combo 与 controller.combo。属于状态表现字段，客户端不自行推断连击或执行效果；对方手牌仍隐藏。指向性随从用现有 play/entity/index/target 协议，不增加动画网络消息。原服务器进程应重启后再开始新局，双方继续使用相同版本；正在进行的局不强制重启。

2026-10-09 时序字段补充：公开 EntityView 增加 used，表示技能本回合已使用且无剩余使用次数；缺法力不等于已使用。Hit/Heal/GainArmor/Buff/Summon/Morph/EquipWeapon/Silence 在目标是公开场上实体时附加 visual（当时数值）、card_type；targeted event 增加 owner（座位索引，未知为 -1）。Draw/Give 不附加牌名或 visual，不泄露对手手牌。协议版本仍为 1；字段为增量扩展，旧客户端忽略新增字段，更新客户端缺少字段时沿用回退/最终快照。双方应使用同一版本以比较公共哈希。

单次动作事件保留上限从 80 增为 512：七个铸甲师受到刀扇伤害有 49 次触发，旧上限会丢掉出牌起点。超出 512 仍可能截断，最终状态不依赖事件重放。服务器立即完成整次合法结算，客户端按 visual 和本地时间线显示，服务器不等待动画。

2026-10-09 表现字段补充：EntityView 增加 `max_health`、`max_durability`、`health_buffed`、`durability_buffed`，包含于同一公共快照哈希；旧字段不删除，缺失上限的旧截图 fixture 按印刷值回退。`keywords` 的 `trigger` 表示未沉默的实际脚本监听器；`silenced` 和 `stealthed` 使用现有规则值。`Trigger` 事件携带监听随从 EntityId，只是视觉提示，客户端不能用它触发伤害/护甲结算。

TCP UTF-8 JSON Lines。客户端提交命令，服务器校验、执行并返回各玩家可见快照，隐藏对手手牌、牌库顺序和奥秘身份。

每个动作必须关联 MatchId、ActionId、预期 GameStateVersion。错误不能推进版本；重复动作不能重复执行。单个消息限制长度。掉线必须退出匹配队列，并明确处理正在进行的比赛。

默认 TCP 37240，协议版本 1。服务器 `hello` 后客户端发 `join`（name/class_id/deck），收到 `queued`，配对后各收自己的 `state`。命令 `type=action` 的 kind 支持 mulligan/choose/play/attack/power/end_turn/concede。

服务器发送合法 moves，客户端选择其中一项并添加 match_id/version/action_id。C# 序列化默认零字段在服务器按动作类型归一化。服务器根据座位隐藏手牌与奥秘；一致的公共 hash 用于定位同步差异。比赛日志与完整开发 dump 写入本地 `local/matches`，包含 RNG seed/state，不广播给客户端。

同 action_id/同 payload 返回最新状态，不重复执行；不同 payload 被拒绝。错误回复后重新发送权威快照，供客户端恢复版本。正在进行的比赛掉线当前按认输处理，尚不支持重连恢复。结束后 `cancel` 返回菜单。非法动作、双 TCP 完整比赛、断线认输与房间清理、畸形 JSON 后继续 ping 均有自动测试。

随从 `play` 增加零基 `index` 插入位置；非法位置拒绝。公共 hash 包含回合、行动方、技能、法力上限与尸体。公开 CardPlay 事件带 card_id/card_type，供双方播放对应声音与动画；抽牌事件不公开卡牌身份。

双方同时确认换牌时，允许尚未确认的玩家提交较旧的非负版本；仍必须携带本人的有效起手实体，服务器串行执行并递增版本。此例外仅覆盖待确认的 mulligan，其他动作继续严格拒绝过期版本。真实双 TCP 同版本换牌与状态一致性已有测试。

Windows 异常断线时，关闭阶段的连接重置不会逃出清理流程。客户端连接超时为 5 秒；网络中断后停止发送动作，并提供返回菜单重新匹配的入口。重新匹配会创建新比赛，尚未提供原比赛重连。
