# 原随从边框 Shader 恢复

2026-10-11。全卡原专属动画、完整原 Shader／脚本未完成。

普通随从主 Mesh 的第二材质现在使用从 `Custom/Card/UnlitTexture` D3D11 编译程序恢复的 Shader；静态缓存与实际双方召唤共用该材质。原画继续使用此前恢复的双纹理 Shader。未修改用户校准、官方安装或服务器。

来源：`essential_base_global-shader-0.unity3d`，PathID `-4440234706065032223`。`manifests/original-actor-frame-shader.json` 保存四个程序及参数记录的 SHA256、两 Pass 原状态、常量字节偏移。执行 `python -X utf8 -m tools.actor_frame_shader_audit` 可从已有只读解包审计缓存重新生成来源记录。

主 Pass 恢复 UV/ST、GlobalMipBias 采样、原顶点色与烘焙 SH 的混合、主光颜色相加、RGBA 相乘。边框与原画的原光照公式不同：边框没有 LightData.z 门控，没有额外 Color 乘色。两 Pass 原状态均为 Cull Off、ZWrite On、LEqual、One/Zero；阴影 Pass ColorMask 0。阴影顶点恢复方向/法线偏移、归一化和 clip.z=min(w,z)，片元返回白色。

当前项目使用 Built-in 管线，源 URP 的主 Pass 调度标签适配为 Always；阴影仍标为 ShadowCaster。原 URP 阴影图调度、动态主光绑定与完整棋盘光照未恢复。默认主光/GlobalMipBias 为零，Gameplay 烘焙 SH 保留。不能把恢复阴影公式视为已完成原版棋盘阴影。

新增 15 项 GPU 检查：实际 Shader 编译/双 Pass、六组顶点色/SH/主光/RGBA/正反面渲染、六组共享阴影函数偏移/法线/深度裁剪、实际生产边框 Shader 绑定。阴影探针在 Editor 中运行，不进入 Player。连同原画检查共 43 项；该探针验证源公式，不替代原版全场阴影验收。

AssetRegistry 只对同时具有匹配 native.shader 来源记录的主材质启用恢复，避免 JsonUtility 为缺失字段创建空对象时误选其他兼容材质。同时释放当前活动 RenderTexture 前解除其活动绑定，消除离屏测试的释放警告。

金色反射、选择高光、主 Mesh 外其余 Renderer、原动态光照、全卡专属动画及完整 Actor 脚本仍未完成。原源公式使边框颜色随原环境光变化，当前尚不能证明与正版全场亮度逐像素一致。

## 本节点实测

704 项 Python 通过（42.08 秒）；43 项原画/边框 Shader、19 项静态与动画图像比较（6 张截图）、105 项双方召唤（14 阶段帧）及完整 GPU 回归通过。iTween 40 程序/188 动作/133 生命周期/798 帧零异常。Windows 构建与既有客户端、78172 原运行时检查通过。日志 `work/native-frame-final-gpu.log`、`work/build-native-frame.log`；解除活动 RenderTexture 后完整 GPU 重跑通过，无此前释放警告。

两个真实 Windows Player 自动比赛 16 回合/v63 完成：Shrink2 术士 4 生命胜，Shrink1 法师 -4 生命负。match_id `6f6af70a6c7841e582949029478b47fc`；公开哈希均为 `fc529296472d2250c84a2317a432c63ca8a7f9aabffa7564982c7e54724b124f`，表现队列峰值 1。后台 Player 不绘制完整 GUI，此比赛不证明完整 GUI 60FPS 或人工视觉验收。

测试客户端 39444/38568 与服务器 38600 按命令行核对清理，用户服务器 10536 保留。用户校准 SHA256 保持 `E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6`；未使用 Computer Use，未执行官方 DLL。
