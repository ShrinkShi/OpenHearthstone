# 原透明粒子Shader与属性混合状态

2026-10-12。全卡原专属动画、完整Shader与脚本未完成。

原Hero/Particle/Particles_Transparent位于essential_base_global-shader-0.unity3d / CAB-c394926ba464822396a05cbfae08a8b3 / PathID1610204260223125505。Windows基础VS/PS条目1/3、Glow VS/PS条目4/5，参数块0/2。源字节SHA、常量布局和106程序314材质引用见manifests/native-transparent-variants.json；65个唯一原材质的实际Shader依赖均解析到同一源编译程序，见native-transparent-source-bindings.json。

## 原运算与旧缺陷

基础顶点为2*vertex*_TintColor，使用MainTex_ST。环境光采用绝对SH L0的gamma转换与LightingBlend；与Additive家族不同，没有MainLightColor加法。基础片元直接乘纹理RGBA与插值颜色，Intensity仅乘RGB、Alpha仅乘alpha，不做saturate。Glow沿用另一个_Color.a及Glow参数，不能把基础Intensity/Alpha/TintColor混入Glow。

原ShaderLab混合状态是属性引用：_RgbSrcBlend、_RgbDstBlend、_AlphaSrcBlend、_AlphaDstBlend。其编译状态val=0只是属性占位，不是实际Zero/Zero。旧缓存保存占位而非读取材质值，会抹掉透明粒子。现在从原Shader默认属性和原材质SavedProperties重新解析四个值及属性名，106源程序已刷新，重复执行零更新。没有改原贴图、几何、动画曲线或正式效果启用范围。

NativeEffect family17恢复上述运算；客户端在Seek后读取当前混合属性，同步普通捕获和原内部捕获状态。仿射捕获读取当前状态，不再用固定导出枚举覆盖运行中的属性变化。重播按原材质值恢复。普通透明捕获的显示alpha与原RGBA目标混合仍分开；仿射路径保留原RGB及alpha混合。

## 复现与验证

源程序：`python -m tools.inspect_compiled_shaders --bundle essential_base_global-shader-0.unity3d --pattern Hero/Particle/Particles_Transparent`。依赖核对与缓存刷新：`python -m tools.transparent_shader_audit`。此工具只读官方安装，生成资源均在忽略的local目录，不执行官方代码。

1151项Python回归通过，8项UnityPy版本回退警告。新增3项Python检查验证原阶段/常量、65源依赖、刷新幂等及现有两启用效果的程序哈希。GPU日志work/native-transparent-final-gpu.log退出0：36项Additive/Transparent浮点检查（本节点新增12）、236既有Shader、112Glow、491仿射、78184源运行时检查与41Prefab检查146离屏帧。实际源材质绑定、运行中混合值改变、alpha捕获同步和重播恢复都有检查。

Windows构建work/build-native-transparent.log成功退出0；构建后原混音器实际Bundle9组编译常量再次精确核对。个人UI校准SHA256保持E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6；生成Scene ID归一，无桌面操作。

## 未完成

新Windows后台客户端transparent_1/2在47424端口完成比赛f533d73c4ca446af9ae7cef9a161f520：17回合、version57，Shrink2术士11血胜、Shrink1法师-4血负。双方公共hash为4e9847ea594ca6e54fcfd368de46339b8988b19c73c0ed269fefde257dfea980，日志无异常/fallback。后台card_render_count=0，不计为新效果画面或人工比赛验收。

314材质引用不是314张完整原动画。专属目标/Actor/生命周期、原全局光照与后处理、其它Shader及平台变体仍不完整。射击等部分离屏场景仍有黑色alpha区域（EX1_537诊断6892像素），不能因Shader运算通过便放行完整效果。现有正式整效果启用仍2，程序哈希保持有效。人工比赛、新效果完整UI画面及1080p性能尚未在本节点验收。
