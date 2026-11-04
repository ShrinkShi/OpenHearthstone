# TASK-0013 Hub底栏、设置、好友与购包交互

- 状态：DONE（本轮本地有界范围；READY_FOR_REVIEW→VERIFIED→DONE，完整旧PC保真仍在0011/0012）。
- Feature：F026；Verification：独立 V026 r6 有界通过，报告提交 b7830d09a3d76e987b68acec71d2e4b73eedc4b1。
- 基线：refs/verification/f025-final（0673a33），F025 实现 ac95ced。
- 依赖：TASK-0010 DONE；继承 TASK-0011/0012 的原生资源与 UI，不替代其完整保真边界。

## 本轮范围

用户最新十张截图为对齐参考，不作为产品整页贴图。

1. 主界面及商店、开包、任务统一原风格设置齿轮、其左侧金币栏、左下好友与时钟。
2. 游戏菜单“选项/退出”，双列选项；实际生效并持久化本地选项，保留账号登录退出入口。
3. 本地账号好友列表、申请与接受/拒绝/移除、在线状态与隐私。
4. 点击商店卡包打开数量与价格弹窗，确认后原子购买多包，100 金币/包。
5. 开包页原生侧栏、背景覆盖、正常颜色；移除说明文字与多余槽位描边。
6. 每日/每周各四槽；替换使用原生图标，任务页纳入 F6 校对并保留用户校对文件。

## 实施与验收

- 独立分支：agents/f026-native、agents/f026-hub、agents/f026-social；实现互相约定 API，禁止并发写共享 local、Unity 构建和 Player。
- 原安装只读，原 mesh/UV/材质来源有据；等价重建和 Unknown 明示，不能用全局增亮掩盖渲染问题。
- 测试真实权威服务与 GUI：好友隔离/在线计数/迁移、批量原子性/幂等、弹窗取消和确认、选项与账号、开包与 F6；记录固定源码/资源/DLL身份。
- 保留 local/ui-calibration.json 和 Main.unity 原哈希；不抢桌面焦点、不执行原版 DLL、不停止用户服务。
- Feature 完成后新独立上下文 V026；通过后自动登记，不在交接处等待用户。

## 事实与限制

2026-10-30 至31：已实查 collaboration 原生 subagent，Native/Hub/Social/Quest及独立V026使用独立worktree，执行模型gpt-6-luna/max。DevSpace open_workspace 返回Internal error后使用原生shell/Agent/worktree。主索引与分支未重置，整合采用私有临时index快照。

最终实现固定 `refs/verification/f026-r6 = 0f379fa069342f96a871f6dd8aac806f9ff8439b`，Build r7 exit0，Player DLL `4255CA07261AF85A79BF52B54A4B3C83228D7798B881CC259A06D37AF457B210`。41定向Python通过；最终Editor GUI14步/207输入/69手势/33成功RPC/34图/7项分析通过，Windows直接生产handler13步/24成功请求/29图/exit0。独立V026 b7830d0已核准本轮有界范围，旧r3/r5 FAIL作为历史保留。总控已登记VERIFIED→DONE，未将该结论扩为原版全局保真。

F6实际GUI已绘八任务/76目标并验证无账号副作用，临时文件布局/字体保存/重读/重置经UICalibrationChecks110项验证；未声称已在GUI拖动保存重读复位。好友presence通过打开/重开面板刷新，未提供服务器推送。局部静态neutral材质替代并不代表原灯光/SH恢复；Repeat采样设置已从源序列化字段确认。完整官方相机、Shader、动态与逐像素保真仍属0011/0012开放范围。最终实测、来源、失败和限制见[F026记录](../verification/F026-hub-controls-and-packs.md)及独立[V026](../verification/V026-hub.md)。
