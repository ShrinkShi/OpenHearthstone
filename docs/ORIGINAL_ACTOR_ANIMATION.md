# 原 Actor 动画调用恢复

后续已接正式友方Summon事件，原表、Clip、粒子和落地声通过真实LiveActor调用链执行，见 [ORIGINAL_BATTLE_SUMMON](ORIGINAL_BATTLE_SUMMON.md)。以下保留恢复节点当时记录；全卡专属脚本/Shader仍未完成。

2026-10-11。现有全卡原版专属动画、完整原 Shader/脚本：**未完成**。

## 原资源与脚本

`python -X utf8 -m tools.actor_templates`现在独立导出两套模板的 `.actor-animation.json`，场景Actor元数据保存原动画缓存文件。随从收集32个Clip，英雄16个Clip；收集不等于全部播放验证。来源与缺失依赖写入 `manifests/original-actor-templates.json`，英雄的Unity内建网格依赖仍未恢复。

本节点实际验证 `AllyInPlaySpawn`：AnimationClip PathID `-8105555823431109035`，CAB `CAB-69c88b234dfa427e02d1fcee9337ae34`，Bundle `essential_base_global-prefab-0.unity3d`。缓存Clip ID `44747e7ca2cade6b401cca65`，时长0.483333379秒，7条通道（位置xyz、旋转xyzw），WrapMode Once。原曲线保留，不用新Tween替代。

原 `AnimationPlaythroughAction` IL按桌面模式读取名称：空名称直接Finish，目标没有Animation时警告后Finish；有组件时按CrossFadeSec和PlayMode调用Animation.Play/CrossFade，然后立即Finish。它不是等Clip播完的动作。`Card_Play_Ally_SummonIn`因此能一边播放Actor Clip，一边继续原FSM及粒子；服务器不等待动画。

## 实际执行

源Actor挂入AnimationProgram定义后，NativeEffectInstance从当前Machine对象变量解析真实目标。动画数据按该Actor原Animation引用查找，使用实际目标GameObject作为Mixer键；停止和退出也按真实对象身份，避免多个外部对象共用负数node发生混淆。位置、旋转、缩放曲线仍使用现有Unity原生Animation.Sample和时间时钟；首次0秒也采样源首帧。

外部Clip目前只接受全部可绑定的Transform通道。未恢复外部材质/Renderer通道、已有Animation组件或并发写入者时明确记录缺口，不覆盖现有动画控制。借用目标的姿态快照用于回退重建和Dispose恢复；创建的外部Mixer与Clip在释放时清理，Actor仍归原场景所有。下一个效果可重新取得绑定。

**未完成**：全部48 Clip的逐个运行验证、外部材质/显隐动画、完整Actor Animation/Animator及层/速度语义、正式Spawn控制与共享SpellTable调度、原音频链、原Actor Shader。正式对战仍用旧展示模板，原效果启用数保持2，不把此恢复fixture冒充对战已全面使用原入场效果。用户UI校准不改。

## 验证与画面

新增26个C#检查，原SUMMON_IN未改写FSM，在真实Actor上按0/0.1/0.25/0.4/末帧/0.6秒核对原位置与归一化旋转曲线、Actor绑定、回退、并发拒绝、释放恢复、Mixer清理及下次复用。原效果表34、原运行时78172、既有客户端4106检查通过。Python699（21.13秒）通过；新增Python回归证明缓存来源/GUID及原错误审计不丢失。

GPU13检查/4帧，SUMMON_IN原粒子与Actor入场Clip共同执行，动画绑定缺口从1变为0。实际截图 `tests/visual/current/native-spell-table/SUMMON_IN-CS2_179-*.png` 已查看。测试相机按棋盘槽位固定：源Prefab根保存编辑器位置(-18.2983,1.3305,-10.6953)，直接按该位置框选后原Clip归零会离开相机；fixture在捕获前把Actor放到槽位原点，源曲线未改。主要Mesh放入源肖像，其他Actor Renderer关闭，仍不是正式GUI。

完整GPU回归Actor18、生命周期7、显隐4、ScaleFrom8、模板12、标准41/146帧；iTween调查40Prefab/188动作/133生命周期/798帧0异常，46空白保留。日志 `work/native-actor-animation-final-gpu.log`、最终构建 `work/build-native-actor-animation-final.log`。未使用Computer Use、未执行官方程序集；离屏测试不证明人工表现或60FPS。

最终构建双客户端 `nativeactoranim_1/2` 完成14回合/v55：Shrink2术士2HP胜、Shrink1法师-2HP负；match `2a5b6f0506e140728eb0af88ef429d81`，public_hash `e98c31f2f0cf84f7c45fca138b654ce94effe49795d4dd89c7e6a7ddc81f974d` 两端相同，队列峰值1。仅清理本轮47393测试进程，用户37240服务器保留。用户校准和两个启用缓存指纹不变。
