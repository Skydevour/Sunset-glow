# M1 海岛样板区执行方案

Goal：以海岸、树林、小屋验证后续天气，不扩张完整海岛。

Architecture：Editor API 生成约 120×100 米连续地形、远海、18 棵简化树和固定小屋。CharacterController 负责米制步行、重力和安全重置；身体表现独立于相机旋转。移除 M0 飞行控制，复用独立性能记录器。Legacy Input 单一路径，Time.timeScale 始终为 1。

Tech Stack：Unity 6000.0.23f1c1、HDRP 17.0.3、C#、Windows Development Build。

1. 保存 M0 构建及证据；M0 基线满足后搭建 M1，保留原模板。
2. `Assets/_Game/Editor/IslandSampleBuilder.cs`：连续网格地形+MeshCollider、沙滩/浅滩/内陆/坡顶、简化海面、树林、小屋、材质板、固定观测点。Unity 正常生成所有 asset/meta。
3. `Assets/_Game/Scripts/Player/FirstPersonController.cs` 和 `FirstPersonBody.cs`：1.8m 高，眼高1.65m，半径0.3m，WASD/鼠标、落水重置、暂停光标、FOV/灵敏度/晃动配置；头部不遮挡相机。
4. 复用遮蔽边界：屋顶使用命名 Shelter 层与独立 Collider，未来动态房顶可直接复用该层和查询；M1 仅屋顶空间验证，不实现天气。
5. 加入观测点 F1—F5、截图与自动路径/碰撞冒烟验证入口。自动验证报告与实际PNG相互补充，不用断言代替画面检查。
6. 单独输出 `Builds/M1`，运行验证东岸/西岸/树林/坡顶/屋内、低头身体、门洞/墙角/坡面/落水/暂停；同机同配置测量并记录限制。通过后继续 M2。

美术范围：粗模资产可接受，但方向、尺度、通路和室内遮蔽必须足以测试天气。简化不透明海面，不启用完整 HDRP Water。固定世界方向 +Z 北、+X 东；小屋约6×5m，独立带外挑的屋顶。
