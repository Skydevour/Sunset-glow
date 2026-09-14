# 海岛质感升级验证

本轮依据用户提供的海岸晴天与落日参考图，改进现有 M1 样板区。维持 120×100 米可行走岛屿，不扩张完整地图。

## 运行

- 构建：`Builds/M1/IslandPrototype.exe`。
- 场景：`Sunset Glow/Assets/_Game/Scenes/IslandPrototype.unity`。
- `1` 晴天、`2` 晚霞、`3` 冬季；切换约 8 秒。
- `WASD` 移动、鼠标转向、`Shift` 加速、`Escape` 暂停/恢复，`F1`—`F5` 固定机位，`F9` 截图、`F10` 退出。
- 修改视觉配置：`Assets/_Game/Art/ClearDay.asset`、`GoldenSunset.asset`、`WinterShore.asset`。
- 菜单 `Sunset Glow/Coastal/Build current environment` 保留已编辑资产并构建；`Upgrade environment and build` 会重新生成本轮样板资产及默认预设。

## 实现

独立 CoastalHDRP 管线启用 HDRP Water、折射、透明 SSR、体积云、物理天空与 SSGI。真实缓坡沙床和水体吸收距离产生浅海到深海的变化；浪速保持真实时间。地表采用连续颜色覆盖图及平铺微细节，松树、木屋、岩石与分区合并草丛替换初始纯色几何表现。

晴天、晚霞、冬季是表现层预设。太阳、大气、曝光、云和地表雪覆盖平滑过渡，GPU 计算逐像素混合季节贴图；稳定状态使用原贴图。地面、针叶上部和屋顶可积雪，墙壁和树干不整体刷白。飘雪根据预览降水强度发射，进入现有 Shelter 屋顶下停止发射并清除当地粒子；降水状态仍保持下雪。

## 验证范围

实机画面及报告位于 `artifacts/coastal`，逐轮保留调整证据。早期截图用于发现浅海过黑、云量过密、阴影过重及季节混合异常，不能当作最终效果。

最终画面：`artifacts/coastal/capture7/`。晴天/晚霞/冬季各含海面、岸线、全岛机位，另有冬季屋内和切换中途画面。`capture.json`：运行零错误，过渡、室外飘雪及室内遮雪检查通过，`timeScale=1`。`transition-textures` 保留约 50% 混合时的 GPU 读回结果；上部针叶及屋顶像素随覆盖率变化，下部无雪区域保持原色。

`artifacts/coastal/regression/validation.json`：原 M1 的 38 项运行检查全部通过，包含连续行走、坡面、门洞、墙壁、屋顶查询、暂停和落水复位。

硬件：i7-14700KF / RTX 4070 Ti SUPER / 32 GB；Windows DX12、1080p、Development Player、Coastal HDRP（质量槽名 Balanced）、关闭垂直同步。冬季海岸 15 秒预热 + 60 秒采样，12,846 帧均有窗口焦点，无丢弃采样、无运行错误/警告；平均 4.671 ms、P95 9.478 ms、P99 9.814 ms、最大 26.284 ms，GPU 平均 4.240 ms。多数帧满足 60 FPS 预算，存在单次长帧，不能称为绝对锁定 60 FPS；数据只代表本机样板区。

最后构建成功、0 错误、1 条编译警告：HDRP 17 的旧 MSAA 枚举被标记 obsolete，目前用于明确关闭旧 MSAA 路径，实际采用 TAA。

冬季树林补测同样预热 15 秒、采样 60 秒，12,323 帧全焦点、零运行错误/警告；平均 4.869 ms、P95 8.561 ms、P99 9.098 ms、最大 10.495 ms，GPU 平均 4.493 ms。证据：`artifacts/coastal/benchmark-forest/`。

本轮不把视觉预设标记为 M2—M5 完成。统一世界时钟、跨季跨年、雨雪中保存恢复及现实 48 分钟正常运行，仍属于后续 P0 工作；没有通过加速物理或动画模拟世界时间。
