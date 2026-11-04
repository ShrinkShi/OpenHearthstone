# 原 PlayAnimation 动态完成通知

2026-10-11。依据 `Blizzard.T5.PlayMaker.Runtime.dll.PlayAnimation.il.txt` 的 OnEnter/OnUpdate/OnExit 和真实 Unity 完成/WrapMode 对照接入。

## 实现

PlayAnimation 动作持有实际 ClipClock，退出状态时清除观察记录。命名变速更新同一时钟，完成判断使用当前未取模 time，而非进入状态时计算 duration/abs(speed)。停止或被其他播放替换后，原观察者也能完成。

ClampForever 检查严格 time > length，零速不凭墙钟完成。Loop/PingPong 保持活动；原 loopEvent 条件采用 OnEnter 的 prevAnimtTime 快照，未擅自添加每圈更新。finishEvent 和 loopEvent 进入现有 FSM 事件路径；没有自定义事件时，FINISHED 还需等待其他延迟/颜色/缓动动作完成。

已知 Once 的正向越过末尾先采样末帧并删除该播放时钟，避免旧曲线覆盖新状态。未刷新的旧源缓存仍按原兼容 WrapMode 路径，不据此宣称源模式已全部恢复。CompletedAnimationActions 是本地审计计数，回退清除并重建。

服务器结算保持立即执行，动画时钟仅在客户端。AnimationPlaythroughAction 仍按原 IL 立即 Finish，不被错误变成等待动画的动作。

## 实际验证

- 新增12项检查：暂停、变速超出旧 deadline、自定义事件、回退、命名停止、其他播放替换、循环严格边界/不完成、ClampForever严格边界/聚合FINISHED，以及停止后旧循环事件不覆盖完成通知。真实Unity的Stop状态time归零也已检查。
- 最终77253原资源/运行时检查通过；4101既有C#检查通过、最终Windows构建成功，日志 `work/build-native-completion-final.log`（当时77252，之后只增加一项Editor原生Stop检查）。
- 最终37 Prefab GPU检查/126帧通过，日志 `work/native-completion-final-prefab-checks.log`，包含最终77253检查。
- 683 Python测试通过，37.82秒。
- 用户校对文件SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6；官方安装未修改，未使用Computer Use。

最终构建后台双Windows Player completionfinal_1/2、端口47374完成16回合/v57。Match 0eb62eef0fad483d808d80652dd8195c；Shrink1法师0HP负、Shrink2术士15HP胜。双方公开哈希5c8e1c7b5e50e9dde5eead3038938227ff3fee1256b9f2afde8248e3db873039相同，表现队列峰值1。前一次构建也完成16回合/v54；最终验收以最后构建为准。

## 未完成

旧缓存源模式回填、Once负速末端与原自动禁用的精确逐帧边界、多个同帧事件/同步嵌套状态切换、完整状态覆盖动作、组件自动播放、完整Animator/PlayMaker/Actor和全卡专属原效果仍未完成。现有FSM单个pending槽不能冒充完整原事件分发器。

启用槽仍为两个，没有新增完整还原卡牌。后台测试不作为普通GUI、人工对局或正式帧率验收。
