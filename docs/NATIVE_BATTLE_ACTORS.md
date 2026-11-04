# 原Actor接入客户端显示

2026-10-11。全卡原专属动画、完整Shader/脚本：**未完成**。

本节点接通英雄和战场随从的实体身份、原模型、原脚本预采样、动态显示与释放。只被原效果借用的实体走动态模型，普通实体继续静态缓存。用户UI校准SHA256仍为 `E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6`。

原资源为MODEL_MINION/MODEL_HERO及对应原画、已有原效果Prefab参数；动态纹理桥接、生命/攻击区域跟随和引用管理是本项目实现，没有执行官方DLL。

## 显示与释放

准备和合成阶段共用实例key、before/after快照和cue年龄。先退出过期效果，再采样当前效果，避免旧Dispose覆盖新缩放。实际Actor动态渲染采用固定取景、2倍透明边距，保留弹性超调；Scale为0时隐藏附属数字。静态和动态共用逐Mesh及UV校准。按存活效果引用释放模型、相机和纹理，不保留整局旧实体。GUI只在Repaint渲染模型。

并发同Actor写入仲裁未恢复，第二个写入者拒绝绑定；其他未完成项有：出生/死亡/弃牌/扳机的正式路由，武器/手牌/技能Actor，Actor初始化/显隐，附属Spell和世界坐标目标绑定，完整旋转后数值/碰撞投影，超过取景边距的效果，以及全卡原Shader和全局后处理。

## 验证

- Python：688通过（20.79秒）。
- 现有客户端C#：4106通过；原资源/运行时：78078通过；Windows构建成功。
- 战斗适配器Actor GPU：18通过，5帧；标准GPU：41通过，146帧。
- 40Prefab/133生命周期调查：798帧，0异常，46空白场景保留。
- 原审核启用数仍2；没有扩大名单，没有修改缓存审核指纹。

截图 `tests/visual/current/native-actor/RLK_086-target-CS2_179-*.png` 已查看，包含0秒隐藏、0.1秒超调及1.01秒复位。它们是使用正式适配器的离屏模型捕获，不是人工比赛截图，不证明RLK整套效果完成，该槽位仍禁用。

日志：`work/native-battle-actors-final-gpu.log`、`work/build-native-battle-actors-final.log`。没有使用Computer Use，官方安装只读。batchmode不能证明正常GUI的1080p性能。

最终Windows构建双客户端 `nativebattlefinal_1/2` 实际自动完成15回合/v48：Shrink2术士9HP胜，Shrink1法师-1HP负；match `645300d091964d8a99fd61fe64673d26`，两端public_hash `0ec79c11891e97dff6f91faf4e812a7a53523d32037a9f9ce5947a94e163ce6f` 一致，队列峰值1，日志无异常。仅后台规则/同步回归，不是人工GUI性能验收。
