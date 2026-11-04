# 原 SuperSpell 启动门禁与协程

2026-10-12。全卡专属动画、完整原 Shader/脚本仍未完成。

## 原代码与恢复范围

只读静态导出 SuperSpell/WaitForStartDelayThenDoAction d__65 和 SpellActionInfo，并核对 DoAction、CheckAndWaitForStartDelayThenDoAction、CheckAndWaitForStartPrefabThenDoAction、OnStartSpellBirthStateFinished、DoStartSpellAction、OnStartSpellActionFinished。原程序集未执行。

NativeSuperStart.DoAction 保留游戏事件等待 → 启动延时 → 启动 Prefab → DoActionNow 的顺序。游戏事件等待由显式服务提供，未把缺失的原任务服务当作默认不等待。

启动延时判断为 Mathf.Min(max,min) 与当前 Unity Mathf.Epsilon 的原比较顺序，保留 IL bgt.un 的无序比较进入等待行为。计数先 +1，再启动协程。协程读取 min/max 并采样 Unity Random.Range，yield 真正的 Unity WaitForSeconds；恢复时计数先 -1，只检查启动 Prefab，不再重跑游戏事件与延时门禁。不使用 Thread.Sleep、墙钟延时或擅自夹取原时长。

BindSourceDelays 从程序原根 Spell 的 raw.m_ActionInfo 读取两端值，各控制器独立保存，不修改共享程序。真实 EX1_384 复仇之怒使用 1.5 秒，EX1_304 虚空恐魔的 Warlock_Missiles_Suckin 使用 0.5 秒；两卡正式原效果仍禁用。Suckin 是效果名称，不应据名称误认为生命吸取卡牌。

启动 Prefab 已处于 Idle 时直接继续；否则按原顺序生成、注册 Birth 完成回调，再安全激活 Birth。Birth 回调先移除自身再 DoActionNow，其他状态的完成不触发该回调。没有可用 Birth 而进入 None 时清除启动引用并继续。

DoStartSpellAction 无启动引用即返回；没有可用 Action 时先 UpdateTransform，再安全激活 Death。有 Action 时仅具体 Spell 注册完成回调，然后 ActivateState(Action)，最后清除父启动引用。完成回调仅在子效果当前仍为 Action 时安全激活 Death。

## 实际验证与界限

work/native-super-start-source.log：33 项新增 Unity 检查通过，退出 0；包括两张真实卡牌源参数产生的 WaitForSeconds 内部时长与计数释放。串行执行既有 51 分派、32 区域、20 相机、11 定位、460 曲线及既有检查链通过。

编辑器检查手动推进协程以核对 yield 类型、参数和回调顺序，没有证明真实帧时间下 0.5/1.5 秒的视觉播放。启动状态端点用协议探针验证，未恢复具体原启动 Prefab 的生成、位置/目标/旋转与完整生命周期回调；不能称为整张卡牌动画完成。生产宿主需用真实 MonoBehaviour.StartCoroutine 调度。

未完成：原游戏事件等待实现、具体 SpawnStart、原启动子 Prefab 的实际回调连接、导弹/冲击/连锁、原任务与战场坐标、原后处理及正式播放器绑定。此节点未改正式播放入口或扩大审核名单；没有新增正式视觉一致性截图。

work/build-native-super-start.log Windows 构建退出 0；实际 Mixer 九个编译常量一致。用户 UI 校准 SHA256 保持 E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6；未 Computer Use、未改官方安装。

Python 常规回归：1168 passed/2 deselected/17 warnings/159.81 秒。排除已在之前通过且本节点 Python 代码/原缓存未变的全定位与全区域来源重读；本次没有宣称全量同一次命令全部通过。

最终构建后台双客户端：端口 47444，super_start_1/2 独立 Profile，比赛 b236d015158b462db77edee19987d2e6，15 回合/version48。Shrink2 法师 13 血胜、Shrink1 法师 0 血负；两端 public_hash 均 e08cd713c9e849a62be744b4d0f6872d9551204a64a5bae8a2044874d7a7adb2。日志无 Exception/fallback/Error，终局命令行核对后只清理自有 PID 22872/32268/27076。后台不绘制 GUI，不证明新启动入口已在正式对战播放，也不是人工比赛或 60FPS 验收。
