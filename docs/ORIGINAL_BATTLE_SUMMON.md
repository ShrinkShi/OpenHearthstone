# 原召唤进入正式比赛调用链

后续已按原OnSpellFinished解除区域阻塞的IL拆分清理尾段：召唤cue现在0.56秒，原2秒Wait独立继续，不再整段2.56秒阻塞。见 [ORIGINAL_SUMMON_COMPLETION](ORIGINAL_SUMMON_COMPLETION.md)。以下为正式路由节点当时的记录。

2026-10-11。现有全卡原专属动画、完整原Shader/脚本**未完成**。本节点将已经恢复的原随从Actor、SUMMON_IN、入场Clip、粒子和落地声接到真实友方Summon事件，不声称恢复所有卡牌专属效果。

## 已接入

NativeBattleActors对需要动态表现的随从使用MODEL_ACTOR_MINION，保留原NativeActorIdentity、AnimationProgram和SpellDefinition；英雄暂仍使用既有显示适配器。LiveActor增加独立固定棋盘槽位容器，实际原Actor在其内以localPosition=0入场，避免源Prefab编辑器位置或入场动画把模型移出取景。固定相机共同捕获Actor及子级原粒子，数值位置读取实际Actor姿态。

只保留源主Mesh渲染，其余原Actor视觉组件目前仍关闭；角色原画和现有minion/art校对沿用，不写校对文件。场上数值/嘲讽/标志仍由当前UI绘制。源Actor材质仍经AssetSurface兼容，**完整原Actor Shader未恢复**。

NativeBattleSummons按真实Summon.target和after玩家所属找到角色，在友方原SpellTable中取得SUMMON_IN（类型2），SetSpellType并以实际Actor为本地父级挂接。Presentation.PrepareNative在正式Repaint中推进源FSM，表实例和角色供给按事件key保留、回放去重、完成/退出释放。入场AllyInPlaySpawn、EdgeMotes、Dust和0.5秒落地声按源时钟执行；旧二维缩放、贴图召唤环/粒子及通用SFX_SUMMON在这条原路径生效时关闭，原卡牌台词沿用。

队列时长来自已核对源BIRTH→wait→Kill Dust, Play Sound→NONE路径的Wait 0.5和Wait 2，加0.06秒帧余量，当前2.56秒。**已知差异**：这包含原清理尾段；完整原SpellManager的并行清理/可交互边界未恢复，当前可能比原版阻塞更久，不能称原版时长完全一致。Server立即结算，不等待客户端动画。

敌方专用Actor/SpellTable仍**未完成**，敌方召唤保留原有表现，不拿友方SUMMON_IN假充敌方效果。正式专属法术native-effect-index启用数仍2；通用友方召唤是新增独立生产路由。跨Actor完整共享SpellManager、原效果表所有槽位、源原Actor脚本/状态组件和完整Shader仍**未完成**。

## 验证证据

新增45项实际Presentation事件/Actor/表调用检查：真实CS2_182召唤、2.56秒源等待、入场动画绑定、旧二维缩放关闭、唯一声音时序、保留/释放、重新调用，以及敌方明确回退。新增51项GPU检查/6帧（0、0.15、0.30、0.60、1.20、2.50秒），使用与正式比赛相同的LiveActor和NativeBattleSummons；画面非空且可见倾斜入场/落地尘土。文件 `tests/visual/current/native-battle-summon/`，离屏透明取景，不是完整桌面GUI截图。

704项Python通过（39.38秒）；Windows构建、既有4106客户端、78172原运行时、34效果表、26原Actor动画、29音频检查通过。完整GPU：Actor18、生命周期7、显隐4、ScaleFrom8、模板12、效果表13、标准41检查/146帧；iTween40程序/188动作/133生命周期/798帧，0异常。

日志 `work/build-native-battle-summon.log`、`work/native-battle-summon-gpu.log`、`work/native-battle-summon-checks.json`、`work/native-battle-summon-gpu-checks.json`。

最终双Windows Player后台自动比赛：nativesummon_1/2，端口47395，19回合/v64，Shrink2术士3生命胜、Shrink1法师-3生命负。match_id `8e77610ee0804defbc78b81876e4dfc8`，双方public_hash `73e376be7fa18430a880ebbedf1d13cda6b712ca0ce47508a5247b0444328e98` 一致，队列峰值1。batchmode不绘制完整GUI，因此这个结果证明同步/队列追赶，不能单独证明可见源召唤效果或正式GUI 60FPS；可见路线由上述同生产适配器GPU检查证明，完整人工比赛/听感未完成。测试客户端14048/28728和服务器10780按PID/命令行核对清理，用户服务器10536保留。

用户校对文件SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6；官方安装只读，不使用Computer Use，不执行原客户端DLL。
