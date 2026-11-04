# 原效果的Actor原画纹理变量

2026-10-11。全卡专属动画、完整原Shader/脚本未完成。此节点恢复GetCardPortraitTextureAction与SetMaterialTexture的FSM纹理通路，不增加专属法术启用数（仍2个）。

## 源证据及实现

只读IL `local/script-audit/Assembly-CSharp.dll.GetCardPortraitTextureAction.il.txt`：portraitObject未提供或Value为空时用FSM Owner；在对象或父级寻找Actor；Actor/HasCardDef/静态原画缺失时Finish，保留storedTexture原值；Premium仅保留Diamond=3，其余读取普通静态原画；成功后写storedTexture并Finish。官方DLL不执行。

NativeActorIdentity独立保存本地卡牌静态原画引用，CreateOriginalMinion设置该引用，不把完整Actor渲染RT当原画。非Diamond用普通原画；Diamond可独立传入，缺少时诊断未绑定，不冒用普通图。旧英雄适配器仍缺完整原Actor/CardDef。

运行时沿显式对象或Owner的父级找到Actor，写当前MachineClock纹理变量；Setter消费同一FSM变量或原静态Texture2D/null。变量跨状态保留、不同FSM隔离、倒放时恢复源初值和材质初值。已声明null与未知变量不同；Getter失败不覆盖原值。

SetMaterialTexture源IL见 `local/script-audit/Blizzard.T5.PlayMakerCustomActions.Runtime.dll.SetMaterialTexture.il.txt`：namedTexture为空用_MainTex，直接材质优先，否则OwnerDefault Renderer/materialIndex。本节点仅恢复效果实例拥有的Renderer材质；直接Material所有权、动态索引、全局FSM变量及其他纹理类型未恢复。传递原图引用，无重绘或复制。

## 导出及实际源资源

```powershell
.\.venv\Scripts\python.exe -X utf8 -m tools.native_effects --refresh-portrait-texture-cache
```

精确CAB/PathID和完整层级刷新4个缓存，0失败；补齐动作参数、FSM Owner和textureVariables声明。追踪 `manifests/native-portrait-texture-refresh.json`。首轮验证发现两个旧FSM缺少owner/owner_known，现按源恢复实际节点1，不默认节点0。源PortraitTexture初始声明null；Getter失败后Setter仍消费null，而未知变量保留未绑定诊断。一般machines导出也保存源纹理变量身份及不支持依赖。

| 卡牌槽 | 原Prefab | 缓存 |
| --- | --- | --- |
| RLK_060 / m_SubSpellEffectDefs:0 | ReuseFX_Necrotic_DeathKnight_OverrideSpawn_FromCorpse_Super_Random.prefab:e51c00c98fe156748a1e45b17b0fb182 | 2ac223d9b583771c9eb24338.native-effect.json |
| RLK_061 / m_SubSpellEffectDefs:0 | ReuseFX_Necrotic_DeathKnight_OverrideSpawn_FromCorpse_Super.prefab:5f189b59ddd96234ba95040813a40274 | b0cbd0aa93fedbfc28ee3f8e.native-effect.json |

其余两个缓存含静态SetMaterialTexture。启用标记、几何、用户校准、既有两专属法术程序未改。

## 验证与未完成部分

32项Unity/GPU检查：真实普通Actor静态原画、Premium选择、两源Prefab各3时间点Getter/Setter、实际材质引用、跨状态/倒放/前进、缺少Actor/Diamond时保留声明null、不同FSM变量隔离。6张实际Prefab图在 `tests/visual/current/native-portrait-texture/`。仍只有很小的静态原画部件，Timeline未执行，不能称动画完成或接近原版；不启用这些未完成效果。

712项Python通过（21.94秒），含7项声明null/精确依赖/不支持类型验证。完整GPU回归通过：32本节点、51战场高光、111召唤、837轮廓、77高光Shader、43Actor材质、19静态Actor、15时钟+4渲染；iTween798帧零异常。日志 `work/native-portrait-texture-final-gpu.log`。

最终Windows构建 `work/build-native-portrait-texture.log` Result: Success，退出0。portraittexture_1/2独立Profile、端口47409自动完成15回合/version51；MatchId=d49642b76d7041698f6c37ba067a83dc，Shrink1法师15血胜、Shrink2术士0血负。双方public_hash=ed4248a33e423e5f7822e4e5597fd5df57374386ce4b315c4a3aef51081628be，队列峰1，测试日志未见异常/原效果fallback。这是后台真实Player网络/规则回归，不是人工比赛，也不证明完整GUI的60FPS。源缓存再刷新4检查/0更新/0失败。UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

后续InitializeCustomSpawn/FinalizeCustomSpawn父级与姿态分支已恢复，见 [ORIGINAL_CUSTOM_SPAWN](ORIGINAL_CUSTOM_SPAWN.md)。仍未完成：TimelinePlay、ActorEliteEventAction、非传奇Mesh/材质分支、其余Shader、专属音频及正式事件路由。全卡专属动画、完整原Shader/脚本未完成。未操作桌面或进行人工视觉验收；继续恢复源脚本/Timeline，不靠截图阈值启用整套效果。
