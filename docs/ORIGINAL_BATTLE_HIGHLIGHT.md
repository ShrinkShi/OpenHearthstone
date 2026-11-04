# 普通随从原高光战场接入

2026-10-11。全卡专属动画、完整原Shader/脚本未完成。本节点把已恢复的普通随从原高光接入ClientApp正常战场分支；原ActorStateMgr其余脚本、英雄/手牌高光仍未恢复。

只读ActorStateMgr IL证明ChangeState_NewState先存activeStateType，变化时转交HighlightState.ChangeState，相同时直接返回；有源HighlightState时使用新分支。来源保存在 `manifests/original-script-audit.json` 的Assembly-CSharp.dll.ActorStateMgr。当前客户端在自身服务器合法动作和悬停/选中语义上选原枚举：idle=1、己方普通hover=2、对方hover=3、选中=5、可行动=6、可行动hover=7。使用源21状态表中的原材质，不复制调色；这层输入映射属于客户端适配，不宣称原版全部输入/ActorStateMgr状态已恢复。

## 实际入口与生命周期

ClientApp.Battle每次Repaint解析当前Presentation.Display双方场上随从，供NativeBattleActors保留真实Actor，连同原法术/召唤借用集合统一Retain。先取得场上Actor，再PrepareNative，故召唤和正常渲染引用同一个实例。法术/召唤仍可借用暂未在场的Actor；死亡/变形后不再显示且无借用时释放。服务器与规则无修改。

PortraitContent对实际普通随从调用NativeHighlightState并用同一LiveActor/camera渲染，关闭该随从旧GUI可攻击/悬停/选中光圈，避免双层高光；动画中的原Actor仍保持位移/旋转/缩放。英雄、详情、UI校对预览保持原路径。失败按实体只记录一次并回退旧高光，避免异常无限刷屏；释放先恢复高光材质/可见性，再释放Actor拥有的材质与RT。

UI校准变化或RuntimeAssetLoader完成原画导入时标记已有Actor；下一帧只重建未被原效果借用的对象，正在执行的Spell/召唤不被强制销毁。归还后再刷新，避免持久Actor一直引用旧UV或缺失原画。原全局ShaderTime持续驱动原flare；正式状态交互使用原R8多级动态轮廓，细节见 [ORIGINAL_HIGHLIGHT_SILHOUETTE](ORIGINAL_HIGHLIGHT_SILHOUETTE.md)、[ORIGINAL_SHADER_TIME](ORIGINAL_SHADER_TIME.md)。

## 验证范围

接入后进一步查源flare Texture2D发现通用PNG缓存把原Repeat变为Clamp，滚动UV越界会停住。`tools.actor_highlight`现直接读取GenFX_Lines原RGB24（128×128、49152字节）和GenFX_Black32x32原DXT1（32×32、512字节），均1 mip，并记录CAB/PathID/Bundle、payload SHA256和filter/aniso/mipBias/U/V/W wrap。NativeActorMaterial仅为源Highlight的_FlareTex使用这份独立原数据缓存，保留Repeat、Trilinear、aniso1、mipBias=-0.5，不改变其他PNG消费者。实际Texture2D raw bytes SHA与源一致；无解码重绘、无新生成mip链。来源见 `manifests/original-highlight-flare.json`。

源TextureColorSpace=1按sRGB传给构造器（linear=false），枚举语义核对 [Unity官方参考代码](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Runtime/Export/Graphics/GraphicsEnums.cs)。此两纹理只有一个mip，不能据此宣称其他资源的全部mip/压缩/全局颜色管线已恢复。

新增51项GPU检查，覆盖六种实际源状态与原Shader/R8绑定、同Actor/camera保留、两边完整7随从（14）保留/高光/清理、校准借用保护与重建；其中10项检查两份实际原纹理格式/mip/原字节SHA/采样/缓存隔离。六张图保存 `tests/visual/current/native-battle-highlight/`。这些是实际Actor组件GPU验证图，不是人工完整GUI截图或正式对战录像。召唤GPU检查增至111项，六个时间点验证idle高光与源召唤共享同一Actor，声音/位置/阻塞cue/尾段既有验证保持通过。时钟GPU检查增至4项，第四帧推进到2.1，验证UV越界后实际原flare继续变化。

705项Python通过（20.89秒），完整GPU回归通过：51战场高光、111双方召唤、837原轮廓/状态、77高光Shader、43原画/边框、19静态比较、15时钟+4渲染；iTween798帧零异常。日志 `work/native-battle-highlight-sampler-gpu.log`。

Windows最终构建日志 `work/build-native-battle-highlight-sampler.log` 为Result: Success、退出码0。新构建后台双Player以独立flarebattle_1/2 Profile、端口47408自动完成一局：match_id=2dbd986fc20c4071a5f137e8eeb33e00，11回合、version36；Shrink2术士2血胜，Shrink1法师0血负。双方public_hash一致：6859f51b53f4622116f3d15b46bac2a73b9cb81f8fab93b52d5234190b5dfa8a，表现队列峰值1。仅证明真实Player网络/规则自动比赛回归；batchmode未绘制完整GUI，不能证明人工视觉效果或正式渲染性能。用户校准文件SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

未完成：原ActorStateMgr完整旧状态/组合状态脚本、合法目标与突袭等剩余状态映射、英雄/手牌的原高光、特殊/premium轮廓、全卡专属效果及其余Shader/脚本。完整GUI端到端人工视觉验收和1080p 60FPS验证未完成；后台Player无完整GUI绘制，不能借其帧率证明本轮所有持久Actor的实际绘制性能。正式专属法术程序仍2个，不把随从高光或召唤计成专属法术增加。
