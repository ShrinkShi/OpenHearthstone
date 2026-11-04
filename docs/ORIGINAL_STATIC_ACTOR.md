# 静态随从与原动画 Actor 统一

2026-10-11。全卡原动画、完整原Shader/脚本**未完成**。

此前真实召唤/清理尾段用MODEL_ACTOR_MINION的原专用Shader与Gameplay环境光，而动画外CardVisuals.Render(minion)仍回到旧MODEL_MINION显示材质。两条路径的原画运算和取景结构不同，不能将上一节点的原Shader接入等同于整场一致。

CreateOriginalMinion现在是唯一普通随从模型创建入口；静态CreateActor与LiveActor都从原Actor建立相同主Mesh、当前卡牌原画、双纹理/双UV、原Gameplay烘焙SH、用户minion/art校准。静态以原既有256×320缓存取景，动态保持512×640且2倍留白，用ImageRect抵消留白。两者采用相同基础包围盒与校准单位。没有改动Client的数值/盾牌/交互图层，没有修改服务器。

模型初始化失败时释放已创建材质与槽位容器，避免此前缺失原Actor引用时遗留对象。完整AssetRegistry部分失败的回收、全部原Renderer/阴影/高光生命周期仍未完成，不将这个局部清理视为完整资源泄漏审计。

## 实际验证

NativeStaticActorChecks真实创建静态和Live模型，检查源Actor/Clip/Shader/当前卡牌绑定、缓存复用；对CS2_182、CS2_172、CS2_179的两条取景路径按真实留白关系比较RGBA平均误差，阈值0.02，原画中心必须可见。19项检查与6张图位于 `tests/visual/current/native-static-actor/`。该误差阈值用于检测内部切换差异，不证明与正版截图逐像素一致。

完整GPU回归保持通过：28专用Shader、105双方召唤/14阶段帧、既有Actor/生命周期/显隐/缩放/模板/效果表及41标准GPU检查；iTween40程序/188动作/133生命周期/798帧0异常。

704项Python通过（38.21秒），Windows构建及4106客户端/78172原运行时/34表/26Actor动画/29音频/86召唤C#检查保持通过。后台双Windows Player比赛14回合/v44结束，Shrink1法师6生命胜，Shrink2术士0生命负；match_id `2c8ad655e9f54b2a86ac2d3b245c3c90`，公开哈希 `82388e78656dc03100b992e125f512afdebc17e626b275c7df012856bc49fad1`一致，队列峰值1，无新异常/原召唤fallback。此为自动后台比赛，不是人工视觉、听感或完整GUI 60FPS验收。

日志 `work/native-static-actor-final-gpu.log`、`work/build-native-static-actor.log`。测试客户端29004/34752与服务器34740按命令行核对清理，用户服务器10536保留。用户校准SHA256仍E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6；没有Computer Use、官方安装修改或原DLL执行。

## 未完成项

原动态棋盘光照、其余Actor Shader、金色/钻石规格、原阴影/选择高光、完整原Actor脚本与全卡专属动画仍未完成。当前主Mesh以外原Renderer仍关闭，不伪称全Prefab已还原。现有GUI的攻击/生命/盾牌等叠层继续使用已有校准。用户存档未修改；静态模型部件编辑入口现对应原Actor层级，旧GenFX内部路径未自动迁移。
