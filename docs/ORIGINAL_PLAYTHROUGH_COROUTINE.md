# 原播放动作协程时序

2026-10-13。全卡原专属动画、完整原Shader/脚本未完成。

## 原证据

只静态读取Assembly-CSharp.dll中AudioPlaythroughAction及其嵌套<Delay>d__17。tools/export_script_il.ps1新增该完整类型的Cecil审计，原程序集SHA256为3F67779510122DBCE79EC431E4E7494643083160EC9E7592F6ECB899B2B69728；未执行原程序集。

原Play完成时调用Fsm.Event(m_FinishedEvent)与Finish，不等待声音实际播放完。当前原Play也没有读取遗留m_OneShotClip字段，不能因为它有值或未解析就拒绝此动作。

OnEnter在m_Delay>0时把值写入动作自身m_DelayTime并启动协程；否则直接Play。协程先检查剩余值，正数时减Time.deltaTime后yield null，下一轮才检查并Play。m_DelayTime属于动作对象，重入创建的多个迭代器共享同一个字段，各自仍会执行Play。原类没有OnExit取消。

## 改正

旧AudioWatch在入场时提前解析音源、音量和随机选择，并且状态变化后直接取消；还预测了一个精确截止时间。现在将音源、音量、随机选择推迟到真正Play时，协程跨状态继续，完成事件交给当前FSM。NativePlaythroughDelay保存原共享计时字段，各watch保存自己的完成标志。状态退出只解除该状态的FINISHED等待，仍保留组件拥有的协程；实例销毁/重建才清理。

FINISHED聚合现在等待真实Play完成，而非简单把延迟当作Wait。移除已完成的全局watch，保留有界的每动作计时记录。重新采样仍用既有去重key，避免重放物理声音。客户端独立承担时间，服务器不等待音频或动画。

本项目仍用固定1/120秒采样、入场逻辑零增量和显式表现时间，不宣称逐帧完全复制原Unity协程调度。完整SoundManager、Actor/VO/队友条件、OneShotSound覆盖和实例限制分支仍未完成，继续显式计入未绑定音频；没有伪造缺失的声音。

## 实测

work/native-playthrough-final.log：13新增检查及已有完整执行检查链通过，退出0；原NativeAudioChecks的29检查也通过。覆盖原共享timer、跨状态播放、延迟时最新音量、当前状态完成事件、忽略未使用legacy clip、自动FINISHED正确等待、动作重入两次播放及重放不重复回调。

旧音频检查的精确.2秒断言已改为原“减到零后下一轮”的边界；没有把等待改为音频文件长度。物理AudioSource既有检查仍运行，听感与桌面验收未进行。

work/native-playthrough-gpu.log：41效果GPU检查通过，退出0。Python常规组1176 passed/4 deselected/24 warnings/128.58秒；四项此前未改的专属定位/区域/启动/RGBA来源重读未在本命令重跑。

没有ComputerUse，官方安装只读。UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。未新增整卡原版视觉一致性通过场景，正式启用仍两项。

work/build-native-playthrough.log Windows构建退出0，原Mixer九编译常量一致。最终构建后台playthrough_1/2在47455完成c6bfc8dfc8604075b64eb23619bab1d4，13回合/version51。Shrink2法师8血胜、Shrink1法师-2血负，两端public_hash均19b544dde2247ef1274f04a943cda0d34e9f6e8be1346dc766e31a5501c75100；日志无Exception/fallback/Error。终局及命令行确认后只清理自有30576/26800/30936。后台不绘制GUI，不证明原音画整卡或人工操作验收。
