# 原卡牌缓动分支与生命周期调查

后续节点：[完整原曲线分发](ORIGINAL_ITWEEN_COMPLETE_CURVES.md) 已扩展到 35 个声明枚举并恢复完成帧精确写入。本文件的 10 种/其余 25 种描述是 2026-10-11 历史状态；完整 iTween 与全卡表现仍未完成。

2026-10-11，继 d3cb558 的原准星执行节点继续扩展；全卡原 Shader/脚本仍未完成。

## 源曲线

对去重后的40个原Prefab、187个ScaleTo/FadeTo动作重新调查，实际枚举只有10种。现在全部有对应的原库数值实现：

| 枚举 | 原方法 | 动作引用数 |
| --- | --- | --- |
| 0 | easeInQuad | 8 |
| 1 | easeOutQuad | 26 |
| 2 | easeInOutQuad | 5 |
| 7 | easeOutQuart | 6 |
| 12 | easeInSine | 2 |
| 13 | easeOutSine | 2 |
| 21 | linear | 131 |
| 24 | easeOutBounce | 1 |
| 27 | easeOutBack | 3 |
| 30 | easeOutElastic | 3 |

逐条核对 `Blizzard.T5.iTween.dll.iTween.il.txt` 原IL；公开源证据SHA见 [ORIGINAL_ITWEEN_SOURCE](ORIGINAL_ITWEEN_SOURCE.md)。Bounce保留三段分界和第四段double比较，Back保留1.70158系数及超出1的回弹，不使用通用EaseFloat枚举映射。32项新数值检查覆盖每条新增曲线的1/4、1/2、3/4及负向增量。其余25种原枚举尚未实现，未知模式继续拒绝计数。

## 源参数与退出

ScaleTo新增原speed分支：speed优先于time，在delay结束捕获localScale，时长为Vector3.Distance(from,to)/speed。transformScale有效时在动作OnEnter读取其localScale加vectorScale，之后不追踪目标变化；time=None且speed=None默认1秒。8项新运行时检查验证优先级、完成边界、默认时间、目标快照、父子停止和重放。

FadeTo不再把整棵子树绑定到一个停止时钟：每个后代有自己的color时钟，只有父节点注册FSM回调。原ColorToChild递归遍历，原动作GetArgs设置includechildren=false；OnExit调用Stop(owner,type)且manager参数includeChildren=false，所以只停止owner，后代继续。已去除先前对includeChildren=true/stopOnExit=true的拒绝。

仍未完成零时间FadeUpdate、loop、UberText/Light、多同步FSM事件重入和原Update/Animation冲突顺序。外部角色/SpellObject绑定也未完整恢复。

## 实际调查与差异

`NativeITweenSurvey.Run` 是独立离屏诊断：按每个原Prefab声明的Birth/Death/Action/Cancel进入原FSM，源卡类型来自已有数据库、目标类型固定为随从；没有修改源cache，不自动启用效果。40程序、187动作、133生命周期场景、798帧、0异常；所有声明的easing都已支持。

实际132场景unbound_itween_actions=0；RLK_086@m_TriggerEffectDefs:1的Death为1，已定位其ScaleTo的gameObject node=-1，是对象绑定缺口。另有46场景所有采样为空白，不能视为视觉完成。即使有像素，也可能只是取消标记或少量粒子，不能把87个非空场景算成87个完整原效果。当前正式启用原Prefab仍只有2个。

调查输出 `work/native-itween-survey.json`，实际128×128采样在 `tests/visual/current/native-itween/`。已查看BT_753 Birth .30秒，只有小型绿色粒子，尚不作完整卡牌验收。正常512×512原效果GPU回归仍为41检查/146帧通过；Windows构建4101既有C#、78060原资源/运行时检查通过。688Python通过，21.02秒。检查数不等于已还原卡牌数。

日志 `work/build-itween-branches.log`、`work/native-itween-survey.log`、`work/native-itween-branches-render.log`。未使用Computer Use，官方资源只读；人工GUI/60FPS验收未进行。

最终Windows构建双Profile实跑：`itweenbranches_1/2`，match `9c9d109993de4264b990749dcf5b4127`，14回合/v48，Shrink2术士12HP胜，Shrink1法师-3HP负。两端public_hash `43716add1946a801e4b9c6870b22bea260096a43f6eb03faff286d894912cc15` 一致，队列峰值1。按PID/路径/Profile/端口清理本次测试进程，保留用户服务器和UI校准。
