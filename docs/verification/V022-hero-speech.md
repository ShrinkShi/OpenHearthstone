# V022 英雄语音与七表情独立复核

2026-10-26，独立只读 Agent `v022_protocol_speech`，模型依用户要求为 GPT-6 Luna/max。对象为 F022/TASK-0007；此报告不代替 TASK-0006 技能视觉验收。

## 固定构建与范围

r7 DLL SHA-256 `F9F2E5405D40CB32ED9F3008EF415D06219EBA66A77D5E33AD11A08E6B24E511`，构建输入见 `work/f022-build-r7/input-hashes.json`。实际工件为 `work/f022-speech-r2-*` 六组和 `work/f022-speech-r3-*` 五组，共22个Player；统一重验结果 `work/f022-speech-all-22-revalidation.json`。ClientApp后续测试probe/菜单输入隔离差异不冒充r7构建输入。

## 独立结果

- 22份JSON独立校验均PASS，座位/同match/双向14广播/七选项次序一致。每个Player记录2个START、14个表情、1个CONCEDE，共17次播放，均自然exit0。
- 22份日志共374条实际播放记录；去重恰好覆盖11英雄×9语义=99个键，包括分开的WOW和SORRY，无缺漏或额外键。
- 独立解析22个Listener WAV：IEEE float32、48kHz、双声道，RIFF/data长度正确；逐样本复算frames/peak/RMS/clipped与报告一致，全部非静音，clipped=0、overflow=0。
- 捕获的THANKS表情ID属于本座位/本英雄实际接收广播，文字与原字幕索引及已Repaint记录一致。CONCEDE owner/card、原字幕和结果气泡一致。抽查七选项菜单、22张表情图和新增反向投降结果，中文清楚。
- 先前独立TCP反例发现seat0投降离开后room压缩而peer.seat仍1，旧索引导致通用error。根控修复后按同一临时TCP序列重验为专用hero_emote_error、无额外state/error、版本不变。

旧r2六份summary保留的是初版分析器把首次THANKS截图与最终SORRY比较产生的FAIL；不覆盖历史文件。runtime捕获条件没有放松；新分析器核对截图的真实广播/字幕/重绘关系，正式统一重验为PASS。

## 实际边界

当前为本地端到端有界通过。Replay经过与Event.current相同的事件字段处理函数，但不是人工物理鼠标测试；Listener不等于设备扬声器输出或官方听感。原气泡贴图/原音频/原字幕有来源，原Actor绑定仍Unknown。mulligan期间气泡可能覆盖部分起手牌文字/牌面，保留为布局观察和人工视觉复验项。

验证者未修改业务文件，未启动Unity/Player/Importer；实际进程与构建由根控串行执行，独立验证者重读原始产物、逐样本重算并检查截图。
