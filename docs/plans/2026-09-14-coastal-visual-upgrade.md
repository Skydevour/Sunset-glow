# 海岛环境质感升级

用户反馈：M1纯色海面、空天空和简单光照达不到参考图质感。优先补齐实际环境表现，用户已授权调整原里程碑顺序；不是等待鼠标验收才继续。

目标：在现有样板区完成可运行的晴天、晚霞和冬季环境；浅海青绿→深海蓝、可辨波纹/浪花、太阳反射、立体云与暖冷光照关系、地面与植被材质细节，实际Player截图和性能测量作为交付证据。

实现：
1. 保存现有场景/管线基线；创建独立Coastal HDRP资源，启用真实Water、SSR、Volumetric Clouds。保持1080p目标，先测量再决定细节降级。
2. CoastalWaterSetup使用HDRP17原生水面、折射/吸收色和岸线衰减；太阳高光从实际光照产生。
3. CoastalLandArt使用连续地形纹理、程序化松树/草/岩石与木纹细节替换纯色粗模；冬季仅室外地面、针叶上部与屋顶积雪，不给海面/墙面统一刷白。
4. CoastalEnvironmentPresenter使用明确环境预览状态驱动日间/晚霞/冬季太阳、天空、云、曝光、材质。预览切换使用真实时间平滑过渡；不创建第二套游戏时钟，不修改Time.timeScale。尚未实现的世界日历/天气日程/玩法降水与存档仍按主路线后续接入，不以视觉预设冒充完整M2—M5。
5. 固定西海岸斜向看岛/海面的构图及树林、小屋机位，在独立构建录制日间、晚霞、冬季PNG；检查水面反射、地平线、云轮廓、室内外明暗、冬季雪覆盖。
6. 运行现有碰撞冒烟回归，采样实际性能，记录达标范围和已知限制。

文件边界：Assets/_Game/Editor/Coastal*.cs；Assets/_Game/Scripts/Environment/CoastalEnvironmentPresenter.cs；Assets/_Game/Art/；独立开发场景仍IslandPrototype，原M1备份保留。构建复用Builds/M1/IslandPrototype.exe，验证证据写入artifacts/coastal。本轮不实现建造玩法或完整海岛扩张。
