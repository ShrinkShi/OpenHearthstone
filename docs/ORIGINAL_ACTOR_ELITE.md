# 原 Actor Elite 事件分支

后续更新：非传奇SetMesh / SetMaterial已恢复，实现和限制见 [ORIGINAL_GEOMETRY](ORIGINAL_GEOMETRY.md)。下文保留本节点当时的验证记录。

2026-10-11：恢复两个原 FromCorpse 入场子 Prefab 的 ActorEliteEventAction。完整全卡专属动画、原 Shader / 脚本仍未完成，整套 RLK 入场效果仍未启用。

## 原代码证据

`manifests/original-script-audit.json` 保存只读 Cecil 的 ActorAction、ActorEliteEventAction、Actor、EntityBase 程序集哈希及调用，IL 在 local/script-audit。不加载或执行官方程序集。

ActorAction 从指定 Owner 及其父级找 Actor。ActorEliteEventAction 在 Actor 缺失时只 Finish，不发送两类事件；存在时按 Actor.IsElite() 分别发送 m_EliteEvent / m_NonEliteEvent，再 Finish。

原 Actor.IsElite() 先排除 LettuceMercenary / LettuceAbility，然后优先 EntityDef，其次 Entity，均不存在时才返回 m_isDiamondViewer。EntityBase.IsElite() 读取标签114并判断>0；佣兵标签1665>0、佣兵技能的CARDTYPE标签202等于23。Diamond PremiumType 与独立 diamond-viewer 标志不是同一个字段。没有以稀有度5替代原标签。

## 实际接入

NativeActorIdentity 增加定义/实体标签的独立配置与源优先级判断。当前真实原随从 Actor 从现有历史卡池数据库读取 EliteTag / MercenaryTag；这些标签由既有 fireplace/hearthstone 数据源原样输出，不改变规则、卡池或组牌限制，也不宣称它们是本轮从官方 CardDef 新提取的标签。

NativeEffectInstance 执行原事件路由、父级搜索及缺 Actor 完成语义，并区分未解析参数。`python -m tools.native_elite` 重新解析原 FSM Owner/事件/变量，保留其他缓存字段；首次2缓存更新，重复0更新、0失败。来源见 `manifests/native-actor-elite-refresh.json`，两缓存为2ac223d9b583771c9eb24338及b0cbd0aa93fedbfc28ee3f8e。原输入为Actor变量，事件为On/Off。

## 验证与限制

95项实际Unity检查通过：负/零/正标签、佣兵/技能排除、EntityDef优先级、DiamondViewer回退、原普通随从CS2_179与传奇随从EX1_116在两个真实源程序中按原Initialize产生的Actor变量选择事件、倒放重建、提前释放及缺Actor只完成。它们验证源动作与分支，不是完整原FSM/整卡视觉验收。

新增Python检查逐卡核对9603条显示记录的Elite/佣兵标签与既有数据库，以及两原动作的精确参数。非传奇SetMesh / SetMaterial、其余Shader、音频和完整专属入场事件路由仍有缺口。正式完整法术程序启用数仍2。未操作桌面、未修改官方安装或用户UI校准。

全量759项Python通过；完整GPU日志work/native-elite-final-gpu.log退出0，包含95本节点、1034 Timeline、104 CameraShaker2、156 ColorBySpeed、16原粒子时钟及iTween798帧零异常。Windows构建work/build-native-elite-final.log为Success、退出0。

47415端口、独立actorelite_1/2后台Player完成比赛2a1d2fdb3c404654865e2f2046e6e80e：15回合、version54，Shrink1法师2血获胜，Shrink2术士-1血失败；双方公共hash 781b73746aa1abaa044285418e738b0df9f5462826f54da378f3896b908bfe92相同，队列峰值1，日志无异常或原效果fallback。card_render_count=0，不用后台比赛宣称实际GUI性能或整套新效果实战展示。自有测试进程已清理，UI校准SHA256仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

下一处源证据：NonLegendary状态的四个SetMesh确有原FsmObjectParams.typeName=UnityEngine.Mesh及外部m_FileID=1引用；旧缓存缺少这些源引用的类型/路径。需要完善kind24 Mesh导出、读取原SetMesh/SetMaterial代码后接入，不能以当前默认网格代替。
