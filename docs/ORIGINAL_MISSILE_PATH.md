# 原 SuperSpell 投射物路径与独立克隆

本篇记录独立路径端点阶段的证据。TASK-0003 已实现两卡根编排与正式本项目棋盘投影，等待独立 V003，当前事实见 [ORIGINAL_SUPER_SPELL](ORIGINAL_SUPER_SPELL.md)。下文“完整根未接入/LookUpdate 未恢复”描述前序端点的范围；不代表 Task3 当前结果。全卡专属动画、完整原 Shader 与官方视觉仍未完成，不能将刀扇/刺骨整卡标记完成。

## 根问题与来源

刀扇EX1_129和刺骨EX1_134的根组件均是SuperSpell，两者启用独立MissileInfo和ImpactInfo。原诊断把引用对象摊入一个图并直接进入各子FSM，缺少真正的投射物克隆与路径，不能代表完整动画链；Shader修复不能弥补这个缺口。

`python -m tools.super_missile_source` 逐一核对232个根组件的完整源类型树，解析正向/反向Prefab身份，记录于 original-super-missile-config.json。针对两张样板卡导出2个独立源图，不修改已审核根图：

- ReuseFX_Sneaky_FanOfKnives_Missile：原刀扇匕首模型、材质、Legacy动画和根FSM。
- ReuseFX_Sneaky_Attack_Missile_02：原刺骨投射物、粒子、状态机及声音引用。

新增静态IL来源为SuperSpell/WaitAndSpawnMissile迭代器及SpawnReverseMissile迭代器；只用Cecil读取，未执行原DLL。原GenerateMissilePath、GenerateMissilePathCenterPoint、FireMissileOnPath、OnMissileSpellStateFinished和OnMissileTargetReached，以及Blizzard iTween的GenerateMoveToPathTargets、PathControlPointGenerator、Interp/ApplyMoveToPathTargets为行为依据。

## 恢复的路径行为

路径包含开始、中点、目标三个点。目标在手牌区时按源设置选择原手牌槽坐标或卡牌坐标；未绑定手牌槽服务时明确报错。中点按未截断的CenterOffsetPercent插值，距离除以DistanceScaleFactor后影响高度和横向偏移。起终点相同且高度区间均近零时原分支固定加2高度。左右方向随开始/目标Z方向反转，保留零区间强制侧别的顺序、DebugForceMax与随机抽样顺序。

三个点经原iTween前后控制点扩展后以Catmull-Rom采样，不使用常见的二次贝塞尔替代。闭合路径保留原首尾包绕。使用现有已核对的NativeITween枚举曲线，先Ease再Clamp到0~1；lookahead先加到percentage再Ease，与按世界距离取朝向不同。

NativeMissileFlight订阅真实原根Spell的Birth完成事件后生成路径并抽取Duration，按宿主帧时钟移动克隆。到达时先通知宿主做impact/chain/next，再激活原Death；到达回调仅发一次。位置服务与原LookUpdate转向服务分开，缺少所需转向或关节服务时明确拒绝，不伪造朝向。

## 真实克隆与截图

`work/native-missile-flight-gpu.log` 27项路径、回调和GPU检查通过，退出0，并继续运行既有音源/相机/区域/历史检查链。纯数学误差检查保持1e-4；现有离屏父物体位于20000单位，世界坐标消去带来约.001的浮点误差，因此仅真实克隆位置检查允许.002的已知隔离误差，不改变原路径公式。

固定960x540相机在平色诊断背景上拍摄实际原刀扇模型，不是原游戏背景截图：

- tests/visual/current/native-missile-flight/knife-start.png：.01秒，位置约(.0195,.0195,.6602)，1258可见像素。
- knife-middle.png：.0451秒，位置约(.0742,.0752,3)，1190可见像素。
- knife-arrived.png：到达后按原Death转None，匕首消失。

对应JSON保留实际世界坐标/时间与whole_effect_verified=false。该诊断只记录朝向服务请求，未复刻iTween.LookUpdate旋转，因此图片不代表完整原投射物朝向。原模型本身的Legacy动画继续采样。

232根参数及2个源Prefab/音频身份的Python重读测试2项通过，121.16秒。

## 未完成

根SuperSpell的完整WaitAndSpawnMissile调度、metadata任务门、pending计数聚合、正反向/顺序/chain分派、手牌实际槽位绑定、原LookUpdate/target joint、完整原Shader族及正式比赛整卡验收未完成。TASK-0002 已实现并局部测试 Impact 分派/命中克隆/游戏延时宿主边界与清理，见 [命中契约](ORIGINAL_SUPER_IMPACT.md)；仍未绑定完整历史或根控制器。NativeMissileFlight 为独立克隆端点，不是正式比赛的完整SuperSpell控制器。不能为截图直接给根FSM猜测一串时间，也不能把原m_PathDuration改长冒充完整动画。

没有Computer Use，没有原DLL执行，官方安装只读，用户UI校准未变；整卡原效果启用仍2项。

## 构建与比赛验收

`work/native-missile-regression.log` 标准效果GPU回归41项、146帧通过，退出0；`work/build-native-missile.log` Windows构建Result: Success，退出0。原混音bundle9项常量一致。UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

首次常规命令work/pytest-missile-path.log没有终态输出，后续核对进程已不存在；missile_path_1/2也只留下起手状态、进程已不存在，均未计为通过。没有根据超时自行认定失败后重复启动同一活跃进程。

重验采用独立Profile missile_verify_1/2和localhost:47474，完成比赛76fce3ae9d4040578effc4e8e26dd907：15回合/version56，Shrink1以0生命失败，Shrink2以4生命胜。两端public_hash均3d7fcc08d9204fab5bdada22bb34143bd2b88a7e41c36b9b8167a72de2f9139b；日志未检出Exception/fallback/Error。后台对局不证明新端点已在正式比赛接入，也不代替人工画面与全卡效果验收。

最终常规回归按进程隔离大规模原资源重读：work/pytest-missile-isolated.log 1187 passed、4 deselected、33 warnings，372.30秒，退出0；本节点新增232根配置与2个Prefab的两个来源测试另一个进程已通过（121.16秒）。常规命令以--ignore排除该新增文件，不能写成单次1189项全量命令通过。四项既有大型定位/区域/启动/RGBA来源测试本节点未重跑。已按PID和命令行仅清理本轮自建终局进程。
