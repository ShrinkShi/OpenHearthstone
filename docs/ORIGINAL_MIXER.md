# 原显式声音模板与混音器

2026-10-12：恢复两个显式AudioSource模板及原LegendaryDucking混音器。完整全卡原版动画、Shader与脚本仍未完成。

## 原资源与重建

原控制器位于essential_base_global-prefab-0.unity3d、CAB-69c88b234dfa427e02d1fcee9337ae34、PathID-8058976051137792987。完整来源数据见manifests/native-sound-limits.json。原Mixer含Master、L_Stinger、L_SFX、L_VO四组；7效果为3个Attenuation、2个Duck Volume、2个Send；L_VO侧链分别送入L_Stinger和L_SFX的Duck Volume。原快照24个值，包括组音量/音高及原阈值、Ratio、Attack、Release、Make-up Gain、Knee和Sidechain Mix。

`python -m tools.native_mixer`从编译常量生成原拓扑程序。NativeMixerBuilder通过本地Unity Editor资产创建API生成可运行混音器，用Unity原生DSP执行，不用手写滤波器或通用音效替代。反射只访问本机Unity Editor自身API，不加载或执行炉石官方程序集。派生.mixer写在忽略目录Assets/LocalAudio，bundle写在local/runtime/native-audio-mixer.assetbundle；构建客户端时自动重建。

`python -m tools.native_mixer --verify-bundle`重新解析实际构建bundle，并与原编译常量逐项对照。9组常量字段（组、效果、侧链数量、完整快照、名称缓冲和暴露参数等）完全相同，控制器名称、暂停阈值、启停及更新模式相同。仅生成的新group/effect/snapshot GUID排除比较；映射保留源Bundle / CAB / PathID，不声称bundle字节或资产GUID与原版相同。结果与当前bundle SHA256在manifests/native-mixer-bundle-audit.json。

首次重建在新效果尚未挂入分组时写参数，Unity没有保存Ratio；检查拒绝了结果。连接效果链后再写快照，原24参数均保留，并通过编译常量对照。没有改原Ratio或放宽原数据要求来迎合失败。

## 显式模板路径

`python -m tools.native_audio_templates`只刷新两个原非空m_TemplateSource参数，从原FSM重新解析AudioSource，并保存原组件与Mixer引用；第二次零更新。来源在manifests/native-audio-templates.json，原组件缓存为*.native-audio-template.json。

原SoundUtils.CopyAudioSource复制输出混音组、bypassEffects、loop、priority、volume、pitch、pan、spatialBlend、reverb、rolloff、Doppler、Min/MaxDistance与spread；不复制custom curves、mute、spatialize或listener/reverb bypass。客户端保留这一区别，不把显式复制分支与默认模板GameObject实例化混为一谈。两个原模板路由到实际重建L_SFX，随后覆盖动作自身参数和原SoundOptions。

## 验证与未完成项

1141项Python通过，含精确原组件读取、模板重复刷新、编译bundle对照和既有规则回归。78项实际Unity音频检查通过，包括两个模板的实际AudioSource输出混音组、空间参数、reverb和标志；完整GPU回归work/native-mixer-audio-final-gpu.log退出0。客户端构建work/build-native-mixer-audio.log为Success、退出0；构建后再次核对实际bundle成功。

183源程序347声音动作全部隔离运行：347请求、347完成、344音源启动、3请求被原实例限制抑制、0未解析动作。这里隔离其他FSM和渲染行为，不能证明183整卡效果完整；报告work/native-audio-source-census.json明确whole_effects_verified=false。音频测试静音，audible_playback_verified=false。

原混音器拓扑与参数和两个模板已恢复，但全局SoundManager的分类、VO抑制、生成池与全部生命周期，以及现有其他语音管线向L_VO / L_Stinger的路由仍未完整恢复。没有验证真实信号下的侧链听感、版本间DSP波形一致或动态快照控制。完整全卡原Shader、脚本、动画和视觉/听觉验收：未完成。完整整卡效果启用数仍2。

47421端口、mixeraudio_1/2后台客户端完成比赛42b8df159c8e4d7e9482de425cfe5ee2：16回合、version53，Shrink2术士12血获胜、Shrink1法师-3血失败，双方公共hash均为0ba7937a576f7990420e134da1a0e2ba87108853587fba7d6e63d20396fa148b。日志无异常或fallback；后台比赛不证明新效果视觉一致、人工比赛或1080p性能。自有20152/29784/29744测试后清理。

个人UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6，构建场景ID归一；没有操作桌面或官方安装。
