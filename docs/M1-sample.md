# M1 海岛样板区

状态：实现、38项自动运行检查、60秒全焦点性能采样、正常模式键盘/暂停恢复/退出均完成，实际画面已检查。鼠标转向与贴墙手感待用户实测，尚未宣布完整M1验收，M2未开始。

## 已实现

- 120×100米连续网格地形与碰撞，浅滩/东西海岸/内陆缓坡/坡顶；简化远海，不启用完整HDRP Water。
- 18棵带树干碰撞和树冠LOD的粗模树；6×5米固定测试屋，南门、西窗、0.5米外挑屋檐；木、石、土、布水平及竖直材质板。
- 身高1.8米、眼高1.65米、半径0.3米的CharacterController；步行、跑步、重力、落水安全复位；身体与相机独立，低头见手腿脚，头部只投影。
- Escape暂停菜单及光标、FOV与灵敏度、身体摆动开关；世界物理TimeScale保持1。M1不实现世界时钟或天气，M2必须把世界暂停接入同一暂停策略。
- 屋顶Collider位于Shelter层，角色位于Player层。现在验证的是屋顶几何查询，不代表雨雪粒子或湿润遮蔽已通过；后者在M4完成。

## 运行与构建

直接启动仓库的 `Builds/M1/IslandPrototype.exe`。操作：WASD移动、Shift跑步、鼠标看向、Escape暂停/继续、F10退出。F1—F5依次为东岸、西岸、树林、坡顶、屋内窗边。Development Build中F9保存实际PNG和状态JSON到`Application.persistentDataPath/M1Screenshots`，日志输出完整路径。

Unity开发场景：`Sunset Glow/Assets/_Game/Scenes/IslandPrototype.unity`。M0场景备份为同目录`M0Baseline.unity`，M0构建入口优先使用该备份，不会把M1覆盖为M0基线。

编辑器菜单 `Sunset Glow > M1 > Build Windows development player` 重复构建现有场景；`Create sample and build`只用于首次生成，已有M1SampleRoot时拒绝覆盖。`Refresh first person body`仅重建生成的身体，手工调整身体后不要重复执行。

关闭本工程编辑器后，在仓库根PowerShell构建：

```powershell
& 'D:/Unity/Editor/6000.0.23f1c1/Editor/Unity.exe' -batchmode -quit -projectPath "$PWD/Sunset Glow" -executeMethod SunsetGlow.Editor.M1Build.Build -logFile "$PWD/artifacts/m1/build.log"
```

自动验证必须使用新的空输出目录：

```powershell
& './Builds/M1/IslandPrototype.exe' -m1Capture "$PWD/artifacts/m1/new-capture" -logFile "$PWD/artifacts/m1/new-capture-player.log"
```

森林固定点性能测量，同M0预热15秒+采样60秒：

```powershell
& './Builds/M1/IslandPrototype.exe' -m1Capture "$PWD/artifacts/m1/new-benchmark" -m1Benchmark -baselineOutput "$PWD/artifacts/m1/new-benchmark" -logFile "$PWD/artifacts/m1/new-benchmark-player.log"
```

必须显示窗口，不要以Hidden启动。自动模式不接收正常移动输入，使用同一SimulateMove碰撞路径驱动验证；不可把自动模式的光标状态当作正常暂停菜单验收。

## 本轮证据

- 最后构建：零错误、零警告，9.11秒，187,058,653字节。摘要 `artifacts/m1/build.json`。
- 最终自动验证 `artifacts/m1/capture3/validation.json`：38项全部通过，Player日志未找到异常或错误。
- 性能 `artifacts/m1/benchmark1`：1080p/Balanced/DX12，15秒预热后全程有焦点采样60.002秒，31,393帧，均值1.911ms/P95 2.585ms/P99 2.798ms/最大9.882ms；GPU均值1.760ms/P95 2.276ms。零丢样/错误/警告，Unity分配内存约189.45MiB、保留324MiB。满足本机16.67ms初测预算，不代表未来天气最重负载。
- 已检查实际PNG：东西海岸、树林、坡顶、小屋窗边、贴近西墙、低头身体。
- 连续步行路线：出生点→东岸→南侧→西岸→树林→南侧空地→坡顶，过程中未触发安全复位。材质测试台有实体碰撞，路线从z=0空地绕行。
- `capture1`暴露低头躯干遮挡问题；`capture2`暴露验证路线穿过材质台，均保留历史证据；以`capture3`为当前结果。
- 正常交互构建已通过W移动、Escape暂停(cursor=None)、再次Escape恢复(cursor=Locked)、F4坡顶定位、F9截图及F10退出。证据在`artifacts/m1/interactive/`与`interactive-player.log`。暂停菜单已查看实际PNG，未以Capture模式替代。

## 尚需真实交互复核

桌面工具可发送键盘，但鼠标坐标操作因Windows截图API错误不可用；不能伪称手感验收完成。已为用户打开正常运行构建，需检查鼠标左右/低头及贴墙/斜坡时肢体与相机；可顺带Alt-Tab失焦暂停→恢复。鼠标输入观察标志在受控验证中仍为false，功能已实现但这些真实交互证据仍缺失。

美术为天气验证粗模：不代表正式资产效果。M3需要处理静态环境光、昼夜和曝光；M4加入真实降水遮蔽；P0跨年、雨雪过渡存档及现实48分钟稳定性验收均尚未执行。M1完整验收后才进入M2，不提前宣称P0通过。
