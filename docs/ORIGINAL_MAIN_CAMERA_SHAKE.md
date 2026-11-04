# 原主相机震屏动作

全卡原专属动画、完整原 Shader/脚本未完成。

## 原来源和算法

静态读取 MainCameraShakerAction、CameraShakeMgr 的本地官方 IL，没有执行原程序集。动作 OnEnter 仅清零 timer/shakeFired；OnUpdate 使用游戏时间累加，达到当前 Delay 后一次读取 Amount/Time 启动震屏，达到当前 Delay+Time 后发送 FinishedEvent 并 Finish。动作退出不停止已经启动的相机管理器。

CameraShakeMgr 的 duration 重载构造 Linear(0,1,time,0)。每次 Update 使用当前曲线强度，分别调用三个世界轴随机范围，设置 initialPos+offset，随后累计时间；完成后下一次 Update 还原初始位置。不是 Timeline/ShakerHelper 的30点轨迹，也不是相机相对向量。

按原平方幅度比较拒绝较弱替换；isOverride 绕过幅度比较，但已有 override 锁拒绝所有新请求。替换保留最初位置并重新计时。hold 达到阈值后不再推进时间，仍逐帧生成随机偏移；零时长/空曲线先停止现有震屏。

## 实际接入

NativeEffectInstance 原 FSM 已执行 MainCameraShakerAction，并等待原动作完成参与 FINISHED。NativeCameraShakeDriver 绑定正式 TimelineCamera/主相机，独立于特效实例在布局相机复位后更新；原动作和实例退出不会取消已启动管理器。离开战斗进入菜单或校对编辑器时停止，避免共享相机的旧战斗位姿覆盖新场景。

当前棋盘采用3D相机而部分实体仍通过GUI合成，GUI层尚未全面恢复原3D场景关系；只恢复真实世界轴相机移动，不伪造全场景一致性。完整原Options服务（含原选项110）、管理器与Timeline争用、原全场景相机/投影/后处理尚未恢复。当前原管理器路径默认开启。MonoBehaviour运行在LateUpdate以适配既有客户端的相机布局，不宣称原组件顺序逐帧一致。

## 原参数恢复与时序修正

`python -m tools.refresh_main_shake` 回读普通/Actor/启动/区域程序，共66程序87动作348参数，0失败、0未解析。CAB/PathID/Bundle来源见 native-main-shake-refresh.json。新的完整缓存来源重放通过1项/17.41秒。

发现之前跳过动作导致地狱烈焰后续 Wait 提前：原根两侧各有1秒 MainCameraShakerAction，然后2秒 Wait，捕获平面在约3秒后隐藏；原 .35 秒 SpellFinishAction 通知不依赖这条视觉尾段。修改检查为2.02秒仍可见、3.03秒隐藏，并将已有审核呈现区间更新为3.1秒，impact_delay仍.35秒。

新原参数刷新使审核文件指纹失效，常规测试正确拦截。已重新生成GPU报告、读取实际奥术爆炸.30秒及地狱烈焰.30/3.00秒PNG，确认两个已有审核效果无黑透明遮罩错误；再通过现有 verify_native_effects 更新审核指纹。未跳过验证或扩大启用名单。

## 检查

`work/native-main-shake-final.log`：13新增Unity检查及既有正式区域/历史/角色检查链通过，退出0。覆盖世界轴、随机调用、强弱替换、原点保持、结束还原、hold/override锁、零时间停止、动作延迟和动态参数、真实区域动作的FSM完成、实例退出后的独立管理器、物理相机组件可挂接。

`work/native-main-shake-gpu.log` 41项GPU通过、退出0。最终Python常规1182 passed/4 deselected/29 warnings/87.32秒，四项大型专属定位/区域/启动/RGBA来源重读未重跑。Windows构建 work/build-native-main-shake.log 退出0。正式原整卡启用仍两项，未新增整卡一致性验收通过数量。没有Computer Use，官方安装只读，UI校准保持不变。

新构建后台双客户端 main_shake_1/2 在 localhost:47464 完成一局自动比赛：Shrink1 WON，Shrink2 LOST，回合11、版本30。双方 match_id 为 2dd016f5725e42d8b85b561b74b3e3a1，public_hash 同为 a62623562df73439dc88180034e890deb437c8a5c0963b75d5c27cb25c6e8aaf；两客户端日志未检出 Exception/fallback/Error。此项为自动比赛，不代替人工验收。已停止本次自建后台进程。原混音器9项常量验证一致。
