# V016: H11最终来源缓存哈希独立验收

日期：2026-10-21  
对象：H11 source manifest/cache 导出顺序更正与拒播状态。  
结论：**PASS（来源导出与安全拒播边界；不代表H11运行时恢复）**

## 独立核验

- `manifests/hero-power-native-sources.json`包含11张基础英雄技能卡，未被H11单卡导出覆盖。H11 program manifest SHA-256与`local/runtime/40ac8d77701fdbccc4033483.hero-power-source.json`的原始文件SHA-256都为`DB9BB73755AE9C693D58174166A7AC492DF9643C168DCF9C10EDFBE9F3C21482`，完全一致。
- 最终source仍含node 1、wrap=2、duration=5秒的Timeline，共5轨：1条`AnimationTrack`、2条`ControlRandomizedTrack`、1条含2片段的`CameraShaker2Track`、1条`SoundTimelineTrack`。
- `local/runtime/native-effect-index.json`有363项，`HERO_11bp`仍为`enabled=false`、`duration=0`、`replace=false`；播放器的H11无条件拒播门仍在。
- 独立重跑：`.venv\Scripts\python.exe -X utf8 -m pytest tests\test_death_knight_hero_power_import.py tests\test_match.py -q`，退出码0，25 passed。

## 前轮失败及修复

V015发现manifest记录了audio绑定前的中间cache哈希`08A828…`，而最终source cache为`DB9BB…`。生成器现先写入并绑定H11 audio，再计算program哈希；root随后重跑全量来源导出及H11 importer。V016对落盘产物独立计算并比较哈希，确认已一致。全量来源manifest SHA-256为`9A84E7D077F43106D3F233FB182F2F32D040DE8DDD544C55D050BDA5B37D96E7`；当前native index SHA-256为`8E13B6CA8F34C359B4B0A420EF67B9FF5622FF406D43D54E13B1E11ACE74A18B`。

## 验收边界

未运行source exporter、Unity、Build、Player或原版客户端。该验收只确认当前manifest与cache一致、Timeline仍在、全量清单完整、H11继续fail-closed以及既有聚焦回归通过。H11生产`Presentation` cue、权威Summon到Actor的专属借用路由、`SpellPlayerSideEventAction` dispatcher和`SoundTimelineTrack`播放器仍未实现；动画/音频/GUI及回合末双端Player验收仍缺，TASK-0006整体保持`IN_PROGRESS`。
