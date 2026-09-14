# M0 工程基线执行方案

Goal：构建独立 HDRP 开发场景，保存实际画面与固定配置性能数据，通过 M0 后进入 M1。

Architecture：保留 OutdoorsScene，使用 Unity Editor API 复制场景及独立环境 Profile；Editor 构建入口生成尺度参照物、观测点和运行测量组件。运行时输入采用现有 Legacy Input 单一路径，世界方向 +Z 北、+X 东、+Y 上，1 单位 = 1 米。M0 不实现世界时钟；M2 才引入独立 60 倍时钟。

Tech Stack：Unity 6000.0.23f1c1 / HDRP 17.0.3 / Windows x64 Development Build / C#。

1. 审查包、输入、HDRP GUID、已运行编辑器所属工程及硬件；不操作其他工程编辑器。
2. 在 Assets/_Game/Editor 增加可重复执行的场景生成与构建入口；通过 AssetDatabase 生成资产及 meta，不手造场景 YAML。
3. 在 Assets/_Game/Scripts/Debug 增加基线运行组件：输入冒烟、固定配置、预热后帧间隔分位数、硬件报告、运行截图、正常退出；性能报告不得以编辑器帧率替代。
4. 构建到 Builds/M0，日志到 artifacts/m0；启动独立程序验证生成画面、日志和测量结果。初测本机 i7-14700KF / RTX 4070 Ti SUPER / 32 GB，1920×1080，Balanced，关闭 VSync 和帧率上限测吞吐；目标 60 FPS / 16.67 ms。此硬件是首台基准机，不宣称为最低配置。
5. 检查实际截图与运行错误，核实输入及退出。阻断项修复后再判断 M0，不把编译通过当视觉通过。
6. M0 通过后按主计划进入 M1：仅海岸/树林/小屋样板区、第一人称身体和固定观测点，逐项验证后继续 M2—M6；P0 验收前不进入 P1。

约定：Default 层用于 M0 地面；M1 引入命名层前核对现有占用；角色高 1.8 m，眼高 1.65 m；后续屋顶使用独立可复用遮蔽查询。测量中记录其他应用干扰，实测数据未生成前均为待验收。
