# 原敌方随从召唤链

2026-10-11。全卡原版专属动画、完整原Shader/脚本仍**未完成**。

## 来源与执行

直接读取 essential_base_global-prefab-0.unity3d 的catalog根对象：没有独立Card_Play_Enemy根，实际共用Card_Play_Ally Actor。原SpellType枚举SUMMON_IN_OPPONENT=42；其SpellTable槽36引用 `Card_Play_Ally_SummonIn_Opponent.prefab:b68ecaea3f52740beac5429761699e02`。源GameObject PathID=-5504954194274176513，CAB-69c88b234dfa427e02d1fcee9337ae34。类型68～72及190～192也引用该资源，本节点只恢复普通敌方召唤42，不冒充恢复所有规格。

敌方程序 `d8a6fae1472426f61e3f501e.actor-spell.json` 使用AllyInPlaySpawn原Clip，但有独立原粒子及Wait=0.4300000071525574秒。SpellFinishAction.delay=0在落地状态解除阻塞，后续Wait=2秒清理。当前阻塞0.49秒、清理2.49秒含0.06秒帧余量；友方维持0.56/2.56秒。各实例保存自己的边界，避免敌方尾段按友方时间回收。

NativeBattleSummons以可见实体所在玩家与本地seat比较选2/42，校验实际Actor表slot文件与恢复定义一致后创建本地原Spell。源对象初始化、动画、粒子、落地SoundDef在同一时钟执行；旧通用召唤表现与音效不再重复。Server仍立即结算，不等待动画。

运行 `python -m tools.native_audio --opponent --bind-actor-spell` 可重复提取敌方源组件、PCM和音频对象参数；独立保存 `manifests/original-opponent-summon-audio.json`，保留友方音频来源记录。解包文件均在local/runtime，官方安装只读。

## 验证与差异

真实生产适配器GPU检查100项/14帧通过，其中6帧敌方捕获位于 `tests/visual/current/native-battle-summon/CS2_182-opponent-*.png`。敌方也经过实际Presentation.Accept/Tick调用链，检查独立0.43秒音效、原动作绑定无缺口、源清理边界、同Actor并发拒绝、暂停、实体移除和多个尾段回收。检查发现并修复原队列把所有召唤强制至少0.5秒的问题：有已恢复源定义时采用其真实完成时间，后备效果仍为0.5秒。离屏图不是完整对战窗口，也不替代原版视觉/听感认证。

完整原Actor材质、共享Animation组件/多写入者、完整SpellManager和SoundManager、金色/大型/快速敌方召唤分支仍**未完成**。源粒子确实执行，不声称当前兼容Shader输出与原版逐帧一致。专属法术启用数仍为2，通用友方与敌方召唤是另外两条生产路径。

最终704项Python通过（37.03秒），86项原召唤C#检查、100项召唤GPU/14帧、既有4106客户端检查/78172原运行时检查、34表/26Actor动画/29音频检查及Windows构建通过。最终完整GPU回归通过，iTween40程序/188动作/133生命周期/798帧0异常，46个旧空白源场景仍待恢复，没有冒充本轮修复。

最终版后台双Windows Player自动比赛：enemysummon_final_1/2、端口47398，10回合/v34结束；Shrink2术士17生命胜，Shrink1法师-1生命负。match_id `9aa8cfca2cca4d8bba1e76f50137af0d`，双方public_hash `4488f2999d42f758d932153a336b3b1844d9d6087782bdfc15b987424274c640`一致，队列峰值1，日志无Original summon fallback或异常。此为自动比赛，不是人工视觉/音效/正式GUI 60FPS验收。修改旧时长下限前另一次自动比赛15回合/v50通过。

日志 `work/build-native-opponent-final.log`、`work/native-opponent-queue-gpu.log`、`work/native-opponent-final-full-gpu.log`。本轮测试客户端/服务器已按PID和命令行核对清理，用户服务器10536保留。UI校准SHA256仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6；两项既有已启用专属程序指纹不变，无Computer Use、官方安装写入或原DLL执行。
