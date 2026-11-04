# 原颜色数组插值

2026-10-13。全卡原专属动画、完整原Shader/脚本未完成。

## 原脚本行为

只静态读取 Blizzard.T5.PlayMaker.Runtime.dll.ColorInterpolate.il.txt，不执行官方程序集。OnEnter：空数组Finish且不写store，单色写第一色并Finish，多色先写第一色。OnUpdate累积游戏时间或读取实时钟，重新读取时长和颜色。

```
position = (colors.Length - 1) * currentTime / time.Value
color = Color.Lerp(colors[floor(position)], colors[ceil(position)], fraction(position))
```

区间等长，精确首尾用原端点。Color.Lerp只限制插值参数，不限制HDR/负颜色通道。只有currentTime严格大于time.Value才Finish、精确写末色并发送finishEvent；等于总时间时还没完成。零增量更新也重新读取变量。少于两色的OnEnter完成不发送显式finishEvent。

NativeColorInterpolation按此恢复游戏时间分支，NativeEffectInstance在原动作顺序中更新变量后才执行后面的SetMaterialColor，包含FINISHED聚合与显式事件。离开状态清除执行记录。原完整帧调度/实时钟分支未恢复；当前全部44源动作均realTime=0，没有把Realtime强行当作游戏时间。

## 原数组与来源

原ActionData的kind12表示数组头，paramDataPos索引arrayParamSizes/arrayParamTypes，后续顺序记录FsmColor元素。原头此前标记unresolved，数组未执行；现在仅恢复已核对的HutongGames.PlayMaker.FsmColor类型和边界，保留平铺原顺序与每个元素的变量/None元数据，其余数组类型仍未恢复。

`python -m tools.refresh_color_interpolate` 通过既有真实层级刷新器回读17程序、44动作、311参数；首次17更新/0失败，再次17程序/0更新/0失败。同步该引用的unknown_actions和实际unresolved_parameters，未自动开启更多正式效果。来源源CAB/PathID/Bundle保存在native-color-interpolate-refresh.json，全部原层级/字段重放见tests/test_color_interpolate_source.py。

来源包括呓语魔典、扭曲虚空、愤怒、星辰坠落、刺杀、奉献、闪电风暴、照明弹、炎爆等相关Prefab。它们的数组执行恢复不代表这些卡牌完整原专属动画已完成。

## 实测

work/native-color-interpolate-final.log：55检查及已有执行链通过，退出0。覆盖多区间、等于/超过时长、动态颜色/时长、零增量读取、空/单色、零时长正增量、HDR和实际书本材质。

书本Death Poof在Death入口约.8秒开始，.9秒材质_TintColor为原两色40%插值，1.1秒为原精确末色，并与上一轮.25秒隐藏配合；实测一启动一完成、无未绑定数组。不是完整棋盘/原版截图验收。

常规Python1176 passed/4 deselected/24 warnings/153.77秒，包括17程序44动作的原字段/节点重放。四项此前未改的专属定位/区域/启动/RGBA重读未在本常规命令重跑。更新审计计数后，新增来源测试单独再过1项/3.43秒。零增量修正由最终55 Unity检查验证。

work/build-native-color-interpolate-final.log Windows构建退出0。原Mixer九编译常量一致；UI校准SHA256仍为E85CE40298C0FD45916D73FB1CC547AD9C46D1E3E124D2E0465F976C6B547DA6。没有ComputerUse，官方安装只读。

零增量修正后work/native-color-interpolate-gpu-final.log的41效果GPU检查通过，退出0。最终构建后台color_interpolate_final_1/2在47454完成31632ad7c26d4580bc5e3911ddcd3c9d，16回合/version56。Shrink2法师4血胜、Shrink1法师0血负，两端public_hash均bce17d3121518b487f304e10f35953e127d5938d6093cadc3af6d1bcfdaa6a16；日志无Exception/fallback/Error。终局与命令行确认后仅清理自有37440/22512/21960。后台不绘制GUI，不证明原全卡视觉或人工操作已验收。

## 未完成

实时钟、其他数组类型、完整Actor/场景/Shader和全卡正式播放仍未完成。正式审核启用仍只有此前两项，没有新增整卡视觉一致性通过场景。
