# M0 工程基线

状态：已建立可运行工程及可见窗口性能基线，进入 M1；鼠标实际手感/完整交互验收待人工复核。安全提示已由用户处理。

## 本次实际结果

- Windows Development Build 成功，零错误/零警告，构建耗时 187.09 秒，大小 185,979,952 字节；见 `artifacts/m0/build.json`。
- 第一次隐藏窗口运行确认 RTX 4070 Ti SUPER / DX12、1920×1080、Balanced、Legacy Input、TimeScale=1，日志无运行错误。
- **该次性能数据无效，不能用于验收**：全程失焦，60 秒采样超出缓冲区，丢弃 712,738 个样本；GPU 计时无数据，截图为黑色。异常的平均 0.043 ms 不代表真实渲染性能。
- 该次运行已自行结束，失败证据保留在 `artifacts/m0/run/`；下一次必须显示前台 Player 窗口，不能以 Hidden 方式启动。
- 后续两次可见窗口各采样60秒，均值2.155/2.038ms、P95 3.277/3.089ms、GPU均值1.839/1.769ms，无采样溢出及运行错误。已打开实际PNG，确认地面、天空、尺度柱与阴影；W移动、Escape、F10退出在input-player.log中记录为True。
- 局限：工具切换造成两次均有失焦帧，窗口仍可见且GPU有有效样本；不宣称纯前台独占性能。鼠标操作工具报SetIsBorderRequired/coordinate geometry错误，真实鼠标手感仍待人工复核。M1已开始，最终完整交互验收保留此项。

## 固定测试配置

| 项目 | 配置 |
|---|---|
| Unity | 6000.0.23f1c1 |
| 管线 | HDRP 17.0.3，Balanced |
| 平台 | Windows x64，Mono，Development Build |
| 首台测试机 | i7-14700KF / RTX 4070 Ti SUPER / 32 GB |
| 显示 | 1920×1080，窗口，VSync 关闭，无帧率上限 |
| 目标 | 60 FPS；帧预算 16.67 ms，先比较平均及 P95，记录 P99/最大值 |
| 测量 | 实时预热 15 秒，连续采样 60 秒；Update 间隔、CPU/GPU FrameTiming、内存 |
| 输入 | Legacy Input 单一路径；不依赖模板遗留 InputSystem_Actions |

该机器是第一台基准机，不是已证实的最低配置。其他工程编辑器保持原样，必须在最终报告注明潜在竞争负载。M0 空场景性能不是 P0 天气性能验收。

## 场景与坐标

Unity 工程在 `Sunset Glow/`。开发场景为 `Assets/_Game/Scenes/IslandPrototype.unity`，原 `Assets/OutdoorsScene.unity` 保留参考；开发 Volume 使用复制的 Profile。

1 单位 = 1 米，+Z 北、+X 东、+Y 上；眼高 1.65 m，尺度柱高 1.8 m。M0 地面在 Default 层。ObservationPoints 为 M1 名称占位，M1 搭图时再设置真正可复用的机位，不计作地图完成。

## 重复构建

关闭本工程的编辑器后，可在仓库根目录 PowerShell 执行（不需要关闭其他工程）：

```powershell
& 'D:/Unity/Editor/6000.0.23f1c1/Editor/Unity.exe' -batchmode -quit -projectPath "$PWD/Sunset Glow" -executeMethod SunsetGlow.Editor.PrototypeBuild.BuildBaseline -logFile "$PWD/artifacts/m0/build.log"
```

也可在编辑器执行 `Sunset Glow > M0 > Build Windows development player`。生成场景命令仅用于首次创建，会拒绝覆盖已存在的开发场景。BuildBaseline 对已有场景直接构建，保留场景编辑。

输出：`Builds/M0/IslandPrototype.exe`；构建摘要 `artifacts/m0/build.json`。`CreateBaseline` 异常时先检查日志与已生成资产，不手工删除用户后来编辑的场景。

## 实际运行

交互检查直接启动 exe：WASD 平移，Q/E 升降，Shift 加速，按住鼠标右键看向，Escape 释放鼠标，F10 退出。M0 是飞行观察器，不具有 M1 的步行/碰撞/身体验收能力。

自动测量：

```powershell
& './Builds/M0/IslandPrototype.exe' -baselineOutput "$PWD/artifacts/m0/run" -logFile "$PWD/artifacts/m0/player.log"
```

约 75 秒后截图并写 JSON、自动退出。报告含实际 GPU/分辨率/质量、平均/P50/P95/P99/最大帧时、CPU/GPU 样本有效性、内存和错误计数。无 GPU 数据时明确标为 unavailable。失焦帧须作为局限记录；不得将程序最小化后测得的数据当作正常前台负载。

输入未观察到的 false 不是通过。另一次交互运行需检查移动、右键视角、Escape、F10，Player 日志 `M0_INPUT_EXIT` 保存本次观察标志。自动测量途中 F10 返回取消码 2。

## 后续门槛

M0 实际构建、画面、输入、退出和性能通过后进入 M1。M2 引入唯一世界时钟和版本化存档；M4 引入天气数据/表现分离和通用屋顶遮蔽；M6 正常跑满现实 48 分钟并完成跨季跨年、雨雪过渡读档和固定点视觉验收，之后才进入 P1。
