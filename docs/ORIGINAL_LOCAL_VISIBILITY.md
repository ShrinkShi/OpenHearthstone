# 原局部 Renderer 可见性

2026-10-13。全卡原专属动画、完整原 Shader/脚本未完成。

## 原脚本与改正

只静态读取 Blizzard.T5.PlayMakerCustomActions.Runtime.dll.SetVisibility.il.txt。原 DoSetVisibility 使用 ComponentAction<Renderer>.UpdateCache，操作对象自身 Renderer，没有递归；无Renderer时返回。保存实际 enabled，toggle非零时取反，否则用visible；OnExit在resetOnExit时恢复保存值。

此前 NativeEffectInstance 把 SetVisibility 和 ActivateGameObject 合在一起并递归操作所有子Renderer，漏掉toggle和退出恢复。现分开执行：ActivateGameObject仍改变GameObject.active；SetVisibility只查自身Renderer、保存实际enabled、按原toggle/visible写入，并加入既有状态退出恢复队列。无纹理过滤仍作为已有兼容边界，完整原Actor服务未恢复。

NativeVisibilityChecks 用真实神圣Prefab的节点/材质，内存中替换为受控FSM以验证原执行语义，未改原缓存。这是执行器测试，不冒充原完整动画：无Renderer父物体不改变子GodRays，普通显示、从开/关两种状态切换、退出恢复/不恢复以及重放，共20检查。实际原Prefab检查链仍执行。

## 原画调查的纠正

原 GenFX_HolyHeroRender 材质：CAB-08424291406c59c12c970e0a93e42644、PathID7490880646996946682。其_MainTex指针 FileID4/PathID-1179172249994696049，实际解析到CAB-572cbaceb2612afe464bd92ebbf0736a、GenFX_white32x32，32×32，原解码RGBA四通道均255，缓存bad556d8a09e588ab8fa08b6.png一致。

该Prefab组件包含Spell、PlayMakerFSM、OffScreenRenderFX和SoundDef，没有目标头像绑定脚本；Spell.SetSource静态IL只写Source属性。此前把此Card_Hero白色纹理直接推断成缺失目标英雄原画的依据不成立，撤回此推断，不随意换成头像。完整外部Card/对象/场景路由仍待追踪，不能由默认白色纹理证明运行时从不覆盖它。

## 实测

work/native-visibility-local.log：20局部可见性检查及已有执行链通过，退出0；work/native-visibility-gpu.log：41效果GPU检查通过，退出0。

常规Python1174 passed/4 deselected/22 warnings/151.29秒；四项此前通过且未改的专属定位/区域/启动/RGBA来源重读未在本命令重跑。work/build-native-visibility.log Windows构建退出0，原Mixer九编译常量一致。

最终构建后台visibility_local_1/2在47451完成885043230e934731986cf1a1ece766c1，16回合/version53。Shrink2法师30血胜、Shrink1法师0血负；两端public_hash均c5925051323660fe670632892c8f412f394e766823cad95db28ce94c313d49da。后台不绘制GUI，不证明原完整卡牌动画已验收。

没有ComputerUse，官方安装只读。UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。
