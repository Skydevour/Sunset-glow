# 工程与需求记录

## 最新专项需求与代码核对
- 新需求：米浴绑定角色、真实云风、树木风动、沙雪音效脚印、允许涉水与局部波纹；本轮交付开发文档。
- 用户提及角色图1/图2，但当前消息未附可读图；外观匹配待补。
- 当前 CoastalLightingSetup 已使用 VolumetricClouds，用户的贴图感是待改善的视觉表现。
- CoastalWaterSetup 已有 WaterSurface，但 scriptInteractions/cpuEvaluateRipples 关闭；查询和扰动是独立接入项。
- FirstPersonBody 是 Transform 摆动；正式绑定模型需替换占位生成入口。
- 当前控制器包含低于 y=-0.4 或半径超过90米回岸；涉水需拆分状态并更新旧安全测试。
- 下方早期工程信息为历史快照，当前已经存在 _Game 场景与实现，不能据早期记录认定还是空工程。

## M0 实施检查
- 实际独立 Player 确认 RTX 4070 Ti SUPER / DX12 和 Balanced 1080p；隐藏窗口模式不渲染有效画面，得到黑图和失真超高循环速率，该次数据作废。
- HDRP Graphics/GlobalSettings/三档 Quality GUID 均找到对应资源；仍需运行验收。
- Test Framework 1.4.5、Performance Tests 3.0.3 有依赖及缓存。
- 所有 HDRP 档位当前关闭水、体积云和 SSR；M1 必须明确简化海面方案或独立开启支持，不能直接放组件假定可见。
- 模板 StaticLightingSky 与静态环境光必须在 M3 动态昼夜阶段处理。
- 输入 Both 但新 Input System 包缺失，采用 Legacy；默认 Standalone 质量从 High Fidelity 调为 Balanced，运行组件再次固定并记录实测档位。

## 已确认需求
- 3D 海岛休闲游戏，第一人称，可看到手脚；游戏名未定。
- 现实 1 秒 = 游戏 60 秒，倍率 60；一天现实 24 分钟。
- P0：地图环境，然后细化昼夜、朝阳晚霞、四季、雨雪。
- P1：木石采集、基础房屋、床、衣服、鱼竿、鱼饵；P0 验收后启动。
- 后续：种田、钓鱼、生存、家具与工艺成长。

## 2026-09-14 文件检查
- Unity 工程目录：Sunset Glow/。
- 前轮读取版本：Unity 6000.0.23f1c1；manifest 声明 HDRP 17.0.3。
- Assets/OutdoorsScene.unity 包含 Sun、Main Camera、Sky and Fog Volume、StaticLightingSky。
- Assets/Settings/SkyandFogSettingsProfile.asset 已存在。
- GraphicsSettings 与 QualitySettings 均有渲染管线引用；引用是否有效尚未在编辑器验证。
- 构建列表目前含 OutdoorsScene；可见 C# 文件为 TutorialInfo 模板说明脚本。
- 初始 git status --short 为空；README 只有工程标题。
- 本次搜索未发现仓库内及已检查父目录中的 AGENTS.md。
- 未启动 Unity，未运行场景、测试或性能测量。

## 规划假设，不能作为用户已确认事实
- 暂按 PC 单机、小团队、温带海岛、轻度生存组织路线。
- 季节长度、目标硬件、正式美术规格与开发工期尚未确定。
- 1.0 暂按环境天气里程碑定义；商业发行版本需另定范围。

## 海岛视觉迭代发现
- 原Balanced未启用Water/SSR/VolumetricClouds；Coastal管线独立启用。
- 低视角浅海偏黑主要来自海底过陡和长水下光程；真正浅滩+吸收距离18m恢复透沙青绿渐变，maxRefractionDistance只影响屏幕扭曲。
- HDRP云shapeFactor越低越趋近阴天；晴天改0.95，晚霞0.925。
- 小贴图Graphics.Blit混合在本运行环境输出仍像夏季；最终改Compute逐像素Load/写Linear ARGBHalf并生成mip，GPU读回0.5及1覆盖率通过。未把未经证实的原因归咎于UV翻转。
- 参考图仍有更丰富的地形与资产；当前是环境样板，程序化植被和岩石并非最终美术资产。
