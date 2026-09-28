# 机械大师

> 拆解万物，解锁机秘

面向 6–12 岁儿童与青少年的写实机械拆装科普游戏。项目按微信小游戏立项，首个完整机械模块是一台无品牌、M 码、27.5 英寸、2×10 铝合金硬尾山地车。

![工程自行车整车预览](Docs/Preview/BicycleEngineering.png)

![传动系统细节](Docs/Preview/BicycleEngineeringDrivetrain.png)

第二个模型为 **BCN3D Moveo 机械臂**，使用官方 MIT 开源 CAD 的保存网格和装配位置，通过免费本地解析接入。

![Moveo 运行分件预览](Docs/Preview/Moveo.png)

## 当前交付

- 1:1 米制源模型，允许自由旋转和缩放。
- 自行车：14 个机械模块、195 类零件、595 个独立实体单元。
- 前后各 32 根辐条、110 节链条、10 片飞轮及前后制动内部件均有稳定 ID。
- 简单 / 进阶 / 探索分别为 15 / 30 / 45 个拆装步骤；模型仍保留 595 个独立实体。
- 所有步骤都绑定真实模型对象，并提供名称、作用、原理和进阶说明；每档零件讲解不超过 50 个字。
- 可自由选择机械单元并直接拖入对应分类托盘；拆解和组装都不限制先后顺序，也不校验工具。
- 提供全局一键爆炸和点击单件爆炸两种三维观察方式；爆炸视图不改变拆解进度或存档。
- 提供自行车固定挡位的原地踩踏演示：曲柄、脚踏、链条、后拨导轮、飞轮与前后轮联动；按住左刹把让后轮减速，按住右刹把让前轮减速，松开后各自逐渐恢复，也可暂停、调速和结束，不改变拆装进度或存档。
- 本地保存拆解等级、准确的已拆零件集合、组装模式和讲解开关。
- 运行时自动发现模型清单；模型资源、三级目录和分类由各模型配置驱动，存档按模型分别保存。
- Moveo：9 个模块、87 个源 CAD 零件定义、366 个独立实体；简单 / 进阶 / 探索分别为 9 / 20 / 42 步，支持拆装、爆炸、观察及中文讲解。
- Moveo 关节演示：底座回转、肩、肘、腕部旋转和腕部俯仰五轴循环联动；夹爪按两侧齿轮与四连杆开合，支持暂停、继续、结束和 0.50×–1.50× 调速，不修改拆装进度。
- Source、模块 LOD0、整车 LOD1 和 LOD2 四层资产流水线。
- 不包含星级、徽章、排行榜、付费激励、工具规格或扭矩考核。

当前还没有微信小游戏 AppID。自行车与 Moveo 已通过本机团结引擎的导入、编译及目录绑定；Moveo 三档拆装和关节演示已通过自动回归。Game View 的人工画面复核及微信开发者工具、真机测试仍需完成。

## 三档拆解等级

| 拆解等级 | 拆装粒度 | 自行车步骤数 | Moveo 步骤数 |
| --- | --- | ---: | ---: |
| 简单 | 整机主要机械模块 | 15 | 9 |
| 进阶 | 模块内按功能系统分组 | 30 | 20 |
| 探索 | 更细的机械子总成，紧固件和重复件仍成组 | 45 | 42 |

每个模型的三个等级都覆盖相同的完整实体集合：自行车 595 个，Moveo 366 个，只改变一次拖动所包含的范围。探索等级也以机械子总成为单位，链节、辐条、紧固件和内部小件不会逐个操作；焊接、粘接、硫化和铆死结构不作为常规拆装操作。刹车油流程计划在后续覆盖所有等级。

## 自行车工程基准

| 项目 | 数值 |
| --- | ---: |
| 轮胎 | ETRTO 57-584 |
| 轮外径 | 698 mm |
| 轴距 | 1120 mm |
| 前叉 | 120 mm 气压避震 |
| 前 / 后花鼓 | 15×110 / 12×148 mm |
| 中轴 | BSA 73 mm |
| 牙盘 | 36/22T |
| 飞轮 | 11–36T，10 速 |
| 前 / 后碟片 | 180 / 160 mm，六钉式 |

这里的“工程级”指维修训练级：比例、接口、部件层级和装配关系可信，不宣称具备原厂制造公差、有限元分析或真实油液计算精度。

## 技术方案

- 团结引擎 / Unity 2022 LTS 技术基线，运行时使用 C#，不是 Java。
- Blender 4.5 LTS + Python 重建可重复的 3D 资产。
- .NET 8 控制台测试领域规则和内容目录，无第三方测试依赖。
- 首版使用 `Resources` 和 `PlayerPrefs`；正式微信版本再接分包、缓存和平台存储。

## 项目结构

```text
Assets/
├─ Art/Models/Source/             # BicycleEngineeringSource.blend、MoveoSource.blend
├─ Resources/
│  ├─ MechanicalCatalog/BicycleInteractionCatalog.json
│  ├─ MechanicalCatalog/Models/Bicycle.json # 自动发现的模型清单
│  ├─ MechanicalCatalog/Models/Moveo.json
│  ├─ MechanicalCatalog/MoveoInteractionCatalog.json
│  ├─ MechanicalCatalog/MoveoMotionRig.json
│  ├─ Models/Bicycle/             # 14 个模块 LOD0 + 整车 LOD1/LOD2
│  └─ Models/Moveo/               # 9 个模块 LOD0 + 整机 LOD1/LOD2
├─ StreamingAssets/MechanicalCatalog/
│  ├─ bicycle_engineering.json    # 工程 BOM
│  ├─ bicycle_model_manifest.json # 595 个对象映射
│  ├─ bicycle_runtime_assets.json # 运行资产统计
│  ├─ moveo_*.json                # Moveo BOM、对象映射和运行统计
│  └─ Moveo/                      # 官方来源记录和 MIT 许可
└─ Scripts/
   ├─ Domain/                     # 纯 C# 拆装状态机
   ├─ Runtime/                    # 3D、输入、UI、存档
   └─ Editor/
Docs/
Tools/
├─ Blender/                       # 生成、LOD 导出、验证
├─ Content/                       # 三档交互目录生成
└─ Tests/                         # .NET 规则与目录测试
```

## 重新生成与验证

自行车流水线需要 Blender 4.5 LTS 和 .NET 8 SDK。

```powershell
# 1. 生成 595 分件工程源模型与预览图
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' `
  --background --python Tools/Blender/generate_engineering_bicycle.py

# 2. 导出 14 个模块 LOD0 和整车 LOD1/LOD2
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' `
  --background --python Tools/Blender/export_engineering_lods.py

# 3. 根据 BOM 与模型清单生成三档交互目录
& Tools/Content/Generate-BicycleInteractionCatalog.ps1

# 4. 校验零件、尺寸、LOD 与目录
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' `
  --background --python Tools/Blender/validate_engineering_bicycle.py

# 5. 执行领域与内容绑定测试
dotnet run --project Tools/Tests/MechMaster.Domain.Tests.csproj -c Release
```

自行车当前验证基线：595 个零件对象、585 个网格对象、119,306 顶点、111,694 面；LOD1 为 14 个对象 / 71,305 面，LOD2 为 1 个对象 / 26,671 面。刹车油管和变速线管沿车架外走线，水壶架螺栓固定于下管安装座，坐垫采用带减压凹槽的曲面模型；传动系统采用 36T/22T 双盘和圆头链片包络，前叉内部件保持同轴，脚踏为镂空平台结构。

Moveo 的免费源 CAD 获取、保存网格解析、Blender 重建和 Play Mode 回归命令见 [开发与验证](Docs/DEVELOPMENT.md#moveo-机械臂)及 [Moveo 接入记录](Docs/References/Moveo/IMPORT_STATUS.md)。已有分件 FBX 可直接运行，无需 SolidWorks。

## 在编辑器中运行

1. 使用团结引擎或兼容 Unity 2022 LTS 编辑器打开仓库根目录。
2. 等待 FBX、JSON 和脚本导入完成。
3. 打开 `Assets/Scenes/Main.unity`。
4. 点击 Play。运行时按当前模型加载模块 LOD0，并自动创建摄像机、灯光、UI 和碰撞热区。
   顶部“模型”菜单位于“旋转视角”左侧，可选择“自行车”或“Moveo 机械臂”；各自保存拆装进度。
5. 默认进入“旋转视角”：在车身或空白处按住鼠标左键 / 单指拖动即可旋转，轻点零件查看百科；滚轮 / 双指可缩放。
6. 点击顶部“拆装零件”后，直接把机械单元拖入对应分类区；空白处仍可旋转，桌面端右键也可旋转。“整机归位”恢复完整取景，不修改拆装进度。
   整机默认位置比原居中位置向左偏移两个平移步长（96 个设计像素）。右下角“画面平移”四向按钮按屏幕方向移动画面，可配合旋转和缩放；“中”回到当前视图的默认位置，保留旋转和缩放。“整机归位”同时重置三者。
7. 左侧“三维爆炸视图”可选择“全局一键爆炸”或“点击单件爆炸”。全局模式按当前模型和等级的全部逻辑机械单元整体展开；局部模式轻点一个零件弹开，再点一次收回。两种模式都支持继续旋转、缩放和平移，只改变观察位置，不写入拆解进度。
8. 左侧“拆解等级”和右侧“零件百科”可收起，底部分类区压缩为两行。相机根据中间可用区域自动构图；“查看收纳”将已拆零件和 3D 展示托盘纳入取景，进入组装时自动切换到此视图。
9. Windows Editor 内置本地中文讲解：点击零件、成功拆装或点击“再次讲解”即可播报；每条零件讲解不超过 50 个字；关闭讲解会停止当前声音。
10. 自行车的顶部“运转演示”可在整车组装完整时启动固定 36T / 24T 的原地踩踏；默认前后轮按相同目标转速转动，不额外生成脚撑或支架。按住左刹把仅使后轮及联动的踩踏传动逐渐减速，按住右刹把仅使前轮逐渐减速；松开任一刹把后，其对应车轮逐渐加速至设定速度。顶部可暂停、继续和结束，左侧可在 30–90 转/分间调速。进入拆装、爆炸或收纳视图会结束演示并恢复静态姿态。演示链条单独显示为绕过两只后拨导轮的闭合链路，原有 110 个可拆链节及 595 个实体 ID 保持不变。
11. Moveo 的顶部“运转演示”可在 366 个实体全部装回后启动五轴循环：底座回转、肩关节、肘关节、腕部旋转和腕部俯仰依次联动，夹爪按官方装配中的两侧齿轮和四连杆同步开合。左侧调速按钮为 0.50×–1.50×；可暂停、继续和结束。进入拆装、爆炸、收纳、切换模型或等级会结束演示并恢复原始姿态，不改变进度和存档。此演示是基于官方保存网格与装配轴的运动学教学视图，不宣称电机控制器、负载、碰撞和真实轨迹仿真。

本地讲解使用已安装的 Windows 中文语音，不联网；首次导入自动编译语音辅助程序到 `Library/MechMaster/`。这不是微信语音实现，其他平台会明确显示“语音待就绪 / 当前平台语音待接入”。详细依赖和排查方式见[开发与验证](Docs/DEVELOPMENT.md)。

通用模型重构后请在完整编辑器中再做一次 Play Mode 交互复核；自动化结果不替代手势与画面检查。

## 微信小游戏后续接入

1. 申请微信小游戏 AppID。
2. 安装与项目版本匹配的团结引擎 Editor、微信小游戏构建支持和微信开发者工具。
3. 将当前 `Resources` 原型加载替换为“整车 LOD1 + 当前模块 LOD0”的按需加载和微信分包。
4. 做纹理压缩、Shader 裁剪、资源释放、安全区和生命周期适配。
5. 在中低端真机测量首包、峰值内存、帧率、发热和触摸体验。

## 后续增加机械模型

新增合格模型时，在 `Assets/Resources/MechanicalCatalog/Models/` 增加一个 JSON 清单，提供独立的三级交互目录与分件 FBX/Prefab；运行时会自动发现并加入“选择模型”，无需修改主程序或自行车目录。清单字段、命名约束及验证流程见[架构说明](Docs/ARCHITECTURE.md#13-新增机械模型)。

[OM10 机芯和 V8 爆炸发动机](Docs/References/MODEL_CANDIDATES.md)目前仍是候选：尚未取得可审计的原始 3D 文件，未纳入模型菜单，也不能声称已达到 1:1 中高精度仿真。取得合法原件并完成尺寸、分件、许可与移动端性能验收后才能接入。

## 内容与安全

- 结构依据厂商公开维修资料和通用自行车规范，来源记录在文档中。
- 游戏用于科普和拆装乐趣，不替代真实车辆维修指导。
- 制动系统属于安全关键部件，真实维修应由监护人或专业技师完成。
- 首版不主动收集账号、位置、通讯录或行为画像。
- 新增第三方模型、字体、图片或音频前必须记录来源和许可。

## 文档

- [项目交接摘要](Docs/PROJECT_SUMMARY.md)：对话决策、当前状态、关键参数与下一步优先级。
- [系统架构](Docs/ARCHITECTURE.md)
- [整车工程 BOM](Docs/ENGINEERING_BOM.md)
- [产品规格](Docs/PRODUCT_SPEC.md)
- [开发与验证](Docs/DEVELOPMENT.md)
- [资料来源](Docs/References/SOURCES.md)
- [Moveo 接入记录](Docs/References/Moveo/IMPORT_STATUS.md)：免费转换、源 CAD 清理、复现命令与验证结果。
- [Moveo 关节演示配置](Assets/Resources/MechanicalCatalog/MoveoMotionRig.json)：五轴、夹爪四连杆、关键帧与 366 个对象绑定。
- [OM10 与 V8 模型候选验收](Docs/References/MODEL_CANDIDATES.md)：仅完成来源初筛，尚未取得原始模型或接入运行时。

## 版本控制

当前直接在 `main` 分支开发，由项目所有者提交和推送。不要提交 `Library/`、`Temp/`、`obj/`、`bin/`、`__pycache__/` 或 Blender 自动备份文件。
