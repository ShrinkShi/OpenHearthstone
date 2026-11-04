# 原全局 Shader 时间服务

后续普通随从正式战场高光已接入，见 [ORIGINAL_BATTLE_HIGHLIGHT](ORIGINAL_BATTLE_HIGHLIGHT.md)。以下保留时钟节点当时的验证范围。

2026-10-11。全卡专属动画、完整原Shader/脚本未完成。本节点恢复实际原ShaderTime时间/陀螺仪运算，并接入Windows客户端；高光正式状态路由尚未开启。

来源为只读官方Assembly-CSharp.dll中的ShaderTime及嵌套Initialize状态机。`manifests/original-shader-time.json`保留程序集SHA256、IL缓存、常量、入口及明确缺口；`tools/export_script_il.ps1`现支持精确FullName筛选嵌套类型，避免只按Initialize简单名称带入其他类型。旧审计记录保留，原DLL不执行。

原构造器设置enabled=true/maxTime=65535，time默认0。UpdateShaderAnimationTime以Unity的**Time.deltaTime / 20**加到float累积值；仅当值严格大于65535时减一次最大值（不是取模），结果<=0时设置0.0001。disabled每帧强制为1。每帧绑定_ShaderTime；UpdateGyro同时把Input.gyro.gravity绑定_Gyroscope，隐式Vector4的w为0。Shutdown只把_ShaderTime清零，不改陀螺仪或实例累积值。不是简单把Time.time、_Time或表现队列时间写给原材质。

NativeShaderTime在ClientApp.Update调用，使用Time.deltaTime；正常菜单与战场都推进。原全局服务没有关联表现队列暂停、2x速度或Seek，本节点也不擅自绑定它们。ClientApp.OnDestroy清零。原Initialize枚举器MoveNext直接结束，GetDependencies列IGraphicsManager；原服务定位器/注册框架没有在复刻中执行，不宣称完整原框架已恢复。

## 验证

15项独立检查覆盖构造器、20倍分母、global绑定、gravity w、禁用/重启、严格边界、单次回绕与巨大delta不取模、连续帧累积、关闭及关闭时保留gyro/实例值。测试结束恢复旧global，避免污染其他GPU测试。

另3项实际GPU检查：真实CS2_182 Actor + NativeHighlightState绿材质 + 原flare/R8动态轮廓，按真实clock进到0/0.05/0.10，后两帧各验证至少20像素不同，保存三图 `tests/visual/current/native-shader-time/`。不是替代纹理滚动，也不是正式战场截图；这是恢复的全局uniform实际驱动原Shader的证据，不能据此证明全卡动画或原场景逐帧一致。

705项Python通过（36.36秒）；完整GPU回归中15时间/3实际渲染、837轮廓/状态、77高光、43原画/边框、19静态比较、105双方召唤通过，iTween798帧零异常。日志 `work/native-shader-time-gpu.log`。

Windows构建成功，15时间及既有78172原运行时检查通过，日志 `work/build-native-shader-time.log`。两实际Windows Player后台自动匹配：`shaderclock_`，11回合/v33，Shrink2术士11生命胜，Shrink1法师-4生命负；match_id `d839cd8f602f443f9cd4143beba6d9f4`，双方公开hash一致 `9ce34f180c3b6e85020d4906cc041b9f424e26d59d8069aea62cd111d5c45647`，队列峰值1。后台Player无完整GUI绘制，此结果不证明完整GUI 60FPS、人工比赛或新高光实战启用。用户UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。

未完成：正式ActorStateMgr交互映射、特殊高光轮廓、其余专属效果/完整Shader与原场景逐帧比对。正式专属法术程序启用数仍2，不把全局时间服务计为新增专属法术。
