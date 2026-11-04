# 原延迟显隐动作

2026-10-13。全卡原专属动画、完整原Shader/脚本未完成。

## 原来源与时序

静态核对 Blizzard.T5.PlayMakerCustomActions.Runtime.dll.SetVisibilityRecursiveDelayAction.il.txt。OnEnter只置计时器0；OnUpdate累加scaled deltaTime，每次重新读取m_Delay，None取0；达到阈值后获取对象，按includeChildren只选本Renderer或原默认GetComponentsInChildren（不含未激活后代），写visible，再Finish。空对象也Finish。该类没有OnExit恢复，不能套用上一轮局部SetVisibility的resetOnExit。

实际使用者是 SpawnToHand_BabblingBook.prefab:8f581f7ccd7bcc640b9b3884a7f24eec 的Death Poof。原FSM源 CAB-4a70eaee9f36c479d7b8e75f8b18b902、PathID-9048167119699251965、initial_base_global-41caf0cf-prefab-7.unity3d。目标是节点2 BabblingBook_mesh，includeChildren=true、visible=false、m_Delay=.25且非None。

Death Init先Wait .3，Death Card再Wait .5，Death Poof内再延迟.25，因此不是从整个Death入口.25秒就隐藏。首次实际源测试误按总入口.25断言失败，核对原状态后修正为完整相对状态时序；1.04秒仍显示、1.10秒已隐藏。

## 恢复执行

NativeEffectInstance按状态记录延迟动作，在原动作更新顺序中重新读取延迟，执行一次后完成；FINISHED聚合会等待它完成。提前离开状态会清除未完成动作，不迟到执行，也不恢复已经写入的值。缺少None元数据或未绑定延迟变量明确计入UnboundDelayedVisibilityActions并保持未完成，不猜成零。

沿用本项目1/120秒固定采样，不宣称完全复制原客户端帧间调度。服务器不等待此动作；只有客户端表现时钟承担时间。

`python -m tools.refresh_visibility_delay` 用既有带真实层级映射的refresh_scale_cache重读4参数，保留目标节点2及None标记；同步该引用的unknown_actions审计。首次使用不带node_ids的颜色刷新入口造成目标-1，改回层级刷新后修复，并由来源重放测试明确断言节点2。没有留住该错误缓存，也没有直接硬写节点索引代替原指针。

最终重复运行报告1程序、0更新、0失败，证明幂等。未因此开启更多正式效果。

## 实测

work/native-visibility-delay-final.log：39新增执行检查及已有链通过，退出0。覆盖阈值前后、None、未知None元数据、空目标、未激活子物体、一次完成、提前退出以及实际书本完整源状态序列。work/native-visibility-delay-gpu.log：41效果GPU检查通过，退出0。

Python常规运行1174 passed/1 failed/4 deselected/23 warnings/154.37秒；唯一失败为新增测试的无关资源回调返回None，修正为无修改的空字典后，tests/test_visibility_delay_source.py单独1 passed/1 warning/1.56秒。没有宣称同一个全量命令最终绿色。原节点/字段/None/未知动作审计重放通过；四项此前未改的专属定位/区域/启动/RGBA重读未在本常规命令重跑。

work/build-native-visibility-delay.log Windows构建退出0，原Mixer九编译常量一致。UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。没有ComputerUse，官方安装只读。

最终构建后台visibility_delay_1/2在47452完成1a39f6ae68bd482a9fd4a224501f5645，13回合/version41。Shrink2法师27血胜、Shrink1法师-1血负；两端public_hash均8cd909db017c82a3596be80fcf98cd1403c5841402fdbdcdf80853539edaf95b。日志无Exception/fallback/Error；终局与命令行确认后只清理自有37676/22672/3240。后台不绘制GUI，不代表书本整卡视觉或人工操作验收。

## 未完成

本节点恢复书本延迟隐藏，ColorInterpolate、完整原材质/脚本、目标Card与原场景和正式整卡播放仍未完成。正式审核效果仍只有此前两项；没有新增整卡视觉一致性通过场景。

后续已恢复44个原ColorInterpolate动作的数组与游戏时间执行，书本材质渐变实测通过；完整整卡仍未完成，见 [ORIGINAL_COLOR_INTERPOLATE.md](ORIGINAL_COLOR_INTERPOLATE.md)。
