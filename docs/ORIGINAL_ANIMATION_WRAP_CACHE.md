# 原动画模式回填到现有卡牌缓存

2026-10-11。

## 精确回填

`python -m tools.native_animation_wrap_refresh` 只读官方资源，重新遍历原 Prefab 的根、子节点与分离引用根，逐项核对现有节点名称/父索引/path 和根资源ID。Clip 按精确ID和名称匹配；Animation组件按原GameObject指针映射，不猜同名节点。

254个现有程序全部验证；189个缓存实际更新，0映射失败。回填346处Clip引用（205唯一：0=197、1=2、2=3、4=1、8=2）及379处Animation组件引用。仅修改mode/source元数据，曲线、材质、粒子和启用标志保留。59组缺Unity内建资源的依赖警告原样记录，不能据此认定整个资源图已完整。

审计在 `manifests/native-animation-wrap-refresh.json`。单个程序先完全验证再更新，错误不会留下半个程序；部分程序失败时命令非零退出并保留失败列表。

## 正式播放差异

现在旧卡池的原Clip真正进入已核对的继承、循环、往返和单次模式。补齐Once的自动退役，包含不等待动画的AnimationPlaythroughAction；到末尾后不持续覆盖后续状态。原三组猎人箭矢在Once完成后才退出状态，OnExit的Stop不再删除一个已退役的时钟。保留原退出前停止和false选项的独立测试。

新检查逐缓存确认Clip模式已知、Animation组件已映射，并检查Playthrough的Once末尾不覆盖新状态。用户规则和网络协议未改。

## 实际验证

- 688 Python测试通过，38.86秒。
- 78002原资源/运行时、4101既有C#检查通过；Windows构建成功，日志 `work/build-native-wrap-cache.log`。
- 39 Prefab GPU诊断检查，146截图帧。新增CS2_084和EX1_539的Birth模式及4.5/5/6/8.5秒采样，覆盖4秒原PingPong周期之后。日志 `work/native-wrap-cache-final-prefab-checks.log`。
- 已实际查看CS2_025/CS2_062的0.3秒截图并通过review工具登记新指纹。CS2_025为紫环，CS2_062仍有过亮黄色中央，后者差异未修复。
- CS2_084/EX1_539全部采样为黑，visible_samples=0/max_pixels=0。诊断检查只证明FSM未失控，绝不证明原准星画面恢复。原淡出/缩放动作及Transparent渲染链路需继续调查，二者保持禁用。

两个已启用指纹：

| 卡 | 新SHA256 | 区间 |
| --- | --- | --- |
| CS2_025 | be6704eb04ac49c0ee4710a3fbed5c2e86859f392403079285bdfc3e948f7064 | 1.2秒 / replace |
| CS2_062 | 328dd447d3d8de13198fac0dad5eda011d42004b45cb30d267c4ad0cf7394168 | 2.1秒 / supplement / impact .35秒 |

未新增启用槽。完整原Actor/目标绑定、脚本/Shader、同步多事件、负速边界及全卡原效果仍未完成。

没有Computer Use；官方安装只读，原缓存本地更新。后台双客户端不能代替普通GUI/人工比赛或正式FPS验收。

新构建后台双Windows Player wrapcache_1/2、端口47376完成10回合/v37。Match 73b6b53824aa4f5a99f4f19325f8ba00；Shrink2术士8HP胜、Shrink1法师-2HP负。双方公开哈希1ee93db68ec0cb76623d4acf90d37e7baf449c54b3afdc370413a5f3cddc3dbb一致，表现队列峰值1。UI校对SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。
