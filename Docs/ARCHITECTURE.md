# 机械大师：系统架构

本文描述通用机械模型运行时，以及当前的工程自行车、BCN3D Moveo 机械臂和 Bolt 双足机器人。三者已通过本机引擎编译、导入及目录绑定；Moveo 与 Bolt 三档也通过射线拾取、拖放、爆炸、存档、完整拆装和关节演示的批处理 Play Mode 回归。实际画面与触控仍需人工复核；微信 AppID、开发者工具及真机测试属于后续工作。

## 1. 架构目标

1. 机械尺寸、零件身份、模型对象和交互步骤使用可校验的单一数据链。
2. 同一台车能按主要模块、功能系统、机械子总成呈现三种拆解等级；重复小件保留独立几何，但交互时成组操作。
3. 拆解与组装共用一套状态，玩家可自由选择尚未操作的零件。
4. 领域规则不依赖 Unity/Tuanjie 或微信 API，可由 .NET 直接测试。
5. 高精度源模型与微信运行 LOD 分离，增加细节不直接拖累运行端。
6. 新增合格机械模型只增加模型清单、交互目录和资源，不修改主程序。

## 2. 数据与资产主链

```mermaid
flowchart LR
    Manual[公开维修资料] --> BOM[bicycle_engineering.json<br/>14 模块 / 595 实体]
    BOM --> SourceScript[generate_engineering_bicycle.py]
    SourceScript --> Blend[BicycleEngineeringSource.blend]
    SourceScript --> ModelMap[bicycle_model_manifest.json]
    Blend --> LODScript[export_engineering_lods.py]
    LODScript --> LOD0[14 个模块 LOD0]
    LODScript --> LOD1[整车 LOD1]
    LODScript --> LOD2[整车 LOD2]
    LODScript --> RuntimeMap[bicycle_runtime_assets.json]
    BOM --> CatalogScript[Generate-BicycleInteractionCatalog.ps1]
    ModelMap --> CatalogScript
    CatalogScript --> Plans[BicycleInteractionCatalog.json<br/>15 / 30 / 45 步]
    Plans --> Runtime[Unity/Tuanjie 运行时]
    LOD0 --> Runtime
```

手工修改生成后的 FBX 或交互目录会在下一次生成时被覆盖。修改应进入 BOM 或生成脚本。

Bolt 的平行资产链为固定提交 STEP → OCCT/XCAF 叶定义、实例及装配矩阵 → Blender Source、12 模块 LOD0、整机 LOD1/LOD2 → 中文三级目录与关节配置。56 个叶定义展开成 345 个稳定实例；12/23/42 步均覆盖全部实例。源路径、矩阵、定义 ID 与校验值进入独立 `bolt_*.json`，详细边界见 [Bolt 接入记录](References/Bolt/IMPORT_STATUS.md)。

## 3. 运行时分层

```mermaid
flowchart TB
    UI[PrototypeUI]
    Input[PartInteractionController<br/>OrbitCameraController]
    App[MechMasterApp]
    Registry[MechanicalModelRegistry<br/>逐模型清单]
    Loader[MechanicalCatalogLoader]
    Domain[PartDefinition<br/>DisassemblyPlan<br/>OperationResult]
    Binding[MechanicalModelView<br/>AssemblyLayout<br/>MechanicalPartView]
    Infra[LocalProgressStore<br/>FeedbackAudio<br/>VoiceNarrator]
    Assets[JSON + 模块 FBX]

    UI --> App
    Input --> App
    Registry --> App
    App --> Loader
    Loader --> Domain
    Registry --> Assets
    Loader --> Assets
    App --> Binding
    Binding --> Assets
    App --> Infra
```

### 3.1 领域层

`Assets/Scripts/Domain` 不引用 Unity。核心不变量：

- 计划至少包含一个步骤，步骤 ID 在计划内唯一。
- 拆解可处理任意尚未拆下的步骤。
- 组装可处理任意仍处于拆下状态的步骤。
- 重复操作不改变状态；工具信息不参与操作校验。
- `RemovedCount == Steps.Count` 表示完全拆开。
- `RemovedCount == 0` 表示完整组装。

`PartDefinition` 除名称、工具和三层科普文本外，还携带 `AssemblyId`、`ComponentId` 和模型对象名集合。旧的前刹车目录仍可作为小型领域样例使用，但正式启动流程读取整车目录。

### 3.2 三档粒度

| 拆解等级 | 生成规则 | 步骤数 | 一个步骤绑定 |
| --- | --- | ---: | --- |
| Simple（简单） | 主要机械模块 | 15 | 大型总成内全部实体 |
| Standard（进阶） | 模块内按功能系统分组 | 30 | 多类关联实体集合 |
| Advanced（探索） | 更细的机械子总成 | 45 | 小型关联实体集合 |

三档计划都完整且无重复地绑定 595 个模型对象。生成器按 BOM 中相邻、相关的组件合并机械单元；即使在探索等级，辐条、链节、紧固件和内部小件也不会逐个操作。

上述表是自行车的基线；Moveo 使用 9/20/42 步覆盖 366 实体，Bolt 使用 12/23/42 步覆盖 345 实例。步骤数来自模型自身可逆拆装与维修分组，不在通用领域层写死。

组装不维护第二份清单，而是直接操作同一个已拆零件集合。拆解与组装都不限制顺序，BOM 增减零件时也不会产生两份清单漂移。

### 3.3 应用协调

`MechMasterApp` 是当前组合根：

1. 自动发现 `Resources/MechanicalCatalog/Models` 中的模型清单，读取所选模型、等级、进度和讲解设置。
2. 通过 `MechanicalCatalogLoader` 加载该模型自己的三级交互目录。
3. 从清单列出的 `Resources` 路径实例化分件模块；当前自行车为 14 个 LOD0。
4. 由 `MechanicalModelView` 将步骤绑定到 FBX/Prefab 对象，`AssemblyLayout` 根据模型尺寸布置 3D 收纳区。
5. 恢复准确的已拆零件 ID 集合并刷新分类托盘位置。
6. 接收 UI 和输入命令，保存新状态。

当前原型为验证 595 实体交互会一次装载全部 LOD0。微信量产版本应先显示整车 LOD1，只在进入某个模块时替换成该模块 LOD0，并在退出时释放。

## 4. 交互绑定

模型源对象同时具有：

- Blender 对象名，例如 `MM_wheel_front_spoke_01`。
- 稳定逻辑 ID，例如 `bike.wheel_front.spoke.01`。
- 装配 ID、维修边界和 LOD 元数据。

生成器把逻辑 ID 与对象名写入 `bicycle_model_manifest.json`；内容生成器再把所需对象名写进每个 `PartDefinition` 对应的 JSON 步骤。运行时不猜测 Blender 自动名称。

`MechanicalModelView` 为每个计划步骤建立一个 `MechanicalPartView`。若一个步骤控制多个对象，所有对象都通过 `MechanicalPartHitProxy` 指向同一个视图；因此点击一根辐条或一个总成中的任一可见实体，都能找到正确的逻辑步骤。

运行时为各零件网格建立独立 `MeshCollider`，避免车架等中空结构被包围盒误选；几何表面接近时优先选择较小的精确目标。真机阶段需要按模块启用并评估简化碰撞网格的性能。

`PartInteractionController` 统一分配观察和拆装手势：默认观察模式下拖动车身也只旋转，轻点才选中；切换“拆装零件”后拖动模型操作零件，空白处和桌面右键仍用于旋转。鼠标使用逐事件坐标，触摸使用同一状态机，第二根手指加入或触摸被取消时不提交拆装。

`OrbitCameraController.Pan` 以设计像素保存平移，叠加到投影矩阵的光心偏移，不修改轨道中心和自动构图距离；屏幕上的上下左右不随旋转改变。四向按钮与其输入屏蔽区域均来自 `WorkshopLayout.PanControls`。取消平移保留角度和缩放，整机/收纳取景则一并复位。回归验证包含平移方向、距离、射线命中一致性、边界和复位。

`WorkshopLayout` 统一提供面板绘制、命中排除、底部分类槽和相机可用区域。`OrbitCameraController` 根据模型包围盒角点与可用区域调整取景中心和距离，保持 1:1 模型尺度不变；整车视图和收纳视图分开，左右面板可折叠。

爆炸视图复用当前拆解等级的 `MechanicalPartView` 逻辑单元，不直接操作 595 个原始实体。`MechanicalModelView` 按清单中的总成计算径向、切向和高度偏移，并按模型尺寸缩放；局部模式只设置一个逻辑单元的观察偏移。`OrbitCameraController` 根据偏移后的包围盒重新构图，同时保留当前观察角度。爆炸偏移与拆装托盘偏移在 `MechanicalPartView` 中独立叠加，因此观察状态不会污染拆装状态。

动态控制器通过 `IMechanicalMotionController` 统一暂停、继续、结束、调速和姿态恢复接口。自行车清单的 `motion.kind = bicycle-pedaling-v1` 声明固定 36T / 24T 踩踏演示；`BicycleMotionController` 在完整整车上驱动曲柄、脚踏、飞轮、前后轮和后拨导轮。Moveo 使用 `motion.kind = moveo-articulation-v1`，引用 `MoveoMotionRig.json`；`MoveoMotionController` 从官方保存网格中的轴 / 支承对象读取五个关节轴，按关键帧计算底座回转、肩、肘、腕旋转、腕俯仰，并以两套闭合四连杆驱动夹爪。Moveo 的固定底座、电控箱和其它未参与关节的对象保持源姿态。两种演示都只修改运动学观察状态，不修改 `DisassemblyPlan`、`LocalProgressStore` 或模型实体基线；拆装、爆炸、查看收纳、切换模型和等级都会停止并复位演示。

中文讲解在 Windows Editor 中由 `VoiceNarrator` 调用本地 `WindowsSpeechHost`，使用已安装的系统中文语音。`LocalSpeechBuilder` 从仓库源码编译辅助程序到 `Library`。切换零件会取消旧播报；正常取消不能当成音频故障。此适配不进入微信构建，微信语音与音频资源仍需单独接入。

Bolt 使用 `motion.kind = bolt-articulation-v1` 和 `BoltMotionRig.json`，由 `BoltMotionController` 从输出轮、支承轴承和踝销重建六主动/两被动轴，以源姿态矩阵驱动完整父子链。左右髋—膝、膝—踝的轴距均为 200 mm；躯干固定。`PeriodicMotionCurve` 在初始化时预计算周期、保形三次 Hermite 切线，关键帧与循环接缝保持一阶连续，只在各关节真正转向处将其速度降至零，不逐段重复 `SmoothStep`。连续双腿动作由 17 个关键帧定义，髋膝错峰转向；被动踝根据当前插值后的髋膝角度补偿，关键帧也校验同一补偿约束。曲线求值不在每帧分配数组。它共享暂停/调速/结束/停止恢复接口与存档隔离，不模拟平衡行走、地面接触或电机/皮带的传动自转。

## 5. 拆装状态与表现

演示只临时进入旋转视角：`MechMasterApp` 在新一轮播放前记录操作模式，结束时恢复；暂停/继续不重写记录。结束或显式点击“拆装零件”时，`MechanicalModelView.RestorePartHitTargets` 重新启用当前计划所属零件的碰撞热区，不改收纳区等无关碰撞体，随后同步物理位置。输入控制器先于界面处理未消费的鼠标事件，仍显式排除按钮和面板区域。观察模式与领域层拆解/组装模式仍是两种独立状态，演示不重置练习进度。

```mermaid
stateDiagram-v2
    [*] --> 完整整车
    完整整车 --> 拆解中: 拖入正确分类槽位
    拆解中 --> 拆解中: 任选未拆零件
    拆解中 --> 完全拆开: 最后一步拆下
    完全拆开 --> 组装中: 切换模式
    组装中 --> 组装中: 任选托盘零件装回
    组装中 --> 完整整车: 最后一步装回
```

`MechanicalPartView` 保存每个目标 Transform 的初始局部位置，并用确定性插值移动到所属模块的分类托盘。没有启用刚体自由掉落，因为儿童科普需要稳定、可恢复的关系展示，微信端也能避免额外物理开销。

`ExplosionViewMode` 仅存在于运行时应用协调层，取值为 `None / Global / Local`。它不进入 `LocalProgressStore`；执行拆装、整机归位、查看收纳、重建计划或切换等级时都会显式清除，保证存档仍只描述真实拆装进度。

当前自行车托盘按 14 个模块分区；其他模型按各自清单生成分类，同一模块内部使用稳定槽位索引，分类超过 14 个时界面分页。正式美术阶段仍应在源模型中增加模块级拆装轴与停靠点。

## 6. 3D 资产架构

### 6.1 Source

- `BicycleEngineeringSource.blend`
- 595 个有稳定 ID 的零件对象，其中 585 个网格对象。
- 119,306 顶点，111,694 面。
- 保留齿形、胎纹、紧固件、密封、活塞、轴承、弹簧和重复件。
- 用于特写、教学渲染和派生运行资产，不直接在微信端整体加载。

### 6.2 LOD0

- 每个装配模块一个 FBX，共 14 个。
- 保留该模块所有独立交互对象和 Source 级局部结构。
- 预期按当前学习模块加载；原型为全量交互暂时一起装载。

### 6.3 LOD1

- 整车观察模型。
- 过滤内部密封、轴承、活塞等不可见细节。
- 每个模块合并为一个对象，共 14 个对象、71,305 面。

### 6.4 LOD2

- 菜单缩略图或远景。
- 过滤细小重复件并合并整车。
- 1 个对象、26,671 面。

FBX 使用 `-Z Forward / Y Up`，引擎中 `1 unit = 1 m`。自由缩放通过摄像机距离完成，不改变机械物理尺度。

## 7. 尺寸与模块边界

自动验证锁定：

- 轮外径 698 mm、轴距 1120 mm。
- 前后花鼓开档 110 / 148 mm。
- 前后碟片约 180 / 160 mm。
- 前后各 32 根辐条。
- 10 片飞轮、2 片牙盘、110 节链条。
- 前后制动各展开为 37 个实体单元。

焊接、粘接、硫化和铆死结构是制造边界；链条以快拆扣作为拆装边界；密封轴承作为维修单元。详细约定见 `ENGINEERING_BOM.md`。

## 8. 内容生成

`Generate-BicycleInteractionCatalog.ps1` 同时读取 BOM 和模型清单：

- 展开后刹车继承前刹车的 37 个零件定义。
- 按安全的整车拆解模块顺序生成三档计划。
- BOM 可保留真实工具类别作为科普元数据，但运行时不要求选择或匹配工具。
- 根据零件类型生成儿童可读的基础作用、进阶原理和探索边界。
- 校验每个物理实体都存在模型对象。

当前知识文本由规则生成，保证覆盖完整；进入正式内容制作时应由自行车维修人员和儿童教育编辑逐条审校，而不是把规则生成文本视为最终出版稿。

## 9. 本地存档

`LocalProgressStore` 使用 `PlayerPrefs`，保存当前模型、拆解等级、讲解开关，以及按「模型 ID + 等级」区分的精确已拆步骤集合与模式。恢复时重建计划并重放仍然存在的步骤 ID；旧自行车存档可读取并在下次保存时写入新键。

已拆集合不是固定前缀，因为允许自由顺序拆装，所以必须保存步骤 ID 而不只保存数量。正式微信版本应抽象 `IProgressStore`，加入 schema 版本、损坏回退和微信存储容量处理。

## 10. 验证体系

| 层级 | 工具 | 当前覆盖 |
| --- | --- | --- |
| 领域与目录 | .NET 8 | 17 项：状态机、BOM、三模型身份/完整覆盖、自动发现、路径及关节配置 |
| 源模型 | Blender Python | 唯一 ID、模块数量、尺寸、几何统计；Bolt 源矩阵与腿部轴距 |
| 运行资产 | Blender Python / Editor | 自行车 14、Moveo 9、Bolt 12 个 LOD0 文件、文件大小、绑定与 LOD 面数递减 |
| 编辑器导入与启动 | 团结引擎 Editor | C# 编译、全部资源绑定、Moveo/Bolt 三档拆装和关节回归；手势与 Game View 全流程待人工复核 |
| 微信平台 | 待 AppID/工具 | 分包、内存、帧率、生命周期、触摸与音频 |

自动化当前可以证明数据、几何和文件链闭合，但不能替代微信真机验证。

## 11. 性能与微信演进

各模型的全量 LOD0、独立 GameObject/Collider 和 IMGUI 不应直接作为微信量产终态；Bolt LOD0 仍有 399,230 三角面，不能仅因桌面验证通过就宣称真机达标。平台阶段应实施：

1. 整车默认加载 LOD1。
2. 当前模块切换到 LOD0，其他模块保留 LOD1 或 LOD2。
3. 只为当前交互模块启用碰撞热区。
4. 模块 FBX 进入微信分包或版本化远程资源。
5. 合并材质、纹理图集、Shader 变体裁剪和移动端压缩。
6. 用真机数据确定内存、Draw Call 和面数预算。

## 12. 已知边界

- 自行车为程序化维修训练模型；Moveo 与 Bolt 派生自官方开源 CAD。三者均不含制造公差、材料仿真或完整动力学。
- 曲面、铸件外形、齿片镂空和线缆走向仍可继续做美术级精修。
- 自动生成知识文本需专家和教育编辑审核。
- UI 仍是 IMGUI 样片，应迁移到 UGUI 或 UI Toolkit。
- 当前运行时全量加载 LOD0；按需模块加载接口是微信接入前的高优先级工作。
- 自行车动态演示目前是固定挡位的运动学样片；真实换挡、链条张紧和微信端运转资源合批仍待实现并验收。Moveo 关节演示也是运动学教学循环，不模拟电机控制器、负载、碰撞、动力学或真实轨迹；其 Game View 和微信真机体验仍待验收。
- 本次通用化改造已通过本机团结引擎 Editor 编译、模型绑定和批处理 Play Mode 启动校验；手势与画面复核、微信导出和真机验证仍待执行。

## 13. 新增机械模型

新增模型不需要在 `MechMasterApp`、`PrototypeUI` 或存档类里添加 `switch` 分支。运行时通过 `Resources.LoadAll<TextAsset>("MechanicalCatalog/Models")` 自动发现模型；每个 JSON 清单使用 `schemaVersion: 1`，并包含：

- `id`：全局唯一且版本化，必须等于交互目录的 `moduleId`；改名会创建新的存档命名空间。
- `displayName`、`displayOrder`：模型菜单的名称与顺序。
- `catalogResourcePath`：不带扩展名的 `Resources` JSON 路径，目录必须提供 `Simple / Standard / Advanced` 三档。
- `moduleResourcePaths`：每个分件 FBX/Prefab 的 `Resources` 路径，不带扩展名。
- `assemblies`：分类槽 ID 与中文显示名，交互目录中的每个 `assemblyId` 必须在此定义。
- 可选 `motion`：仅已制作运转演示的模型填写；自行车使用 `bicycle-pedaling-v1`、前后齿数和原静态链节数，Moveo 使用 `moveo-articulation-v1` 与 `rigResourcePath`。没有该字段的模型不显示“运转演示”。新运动种类需实现 `IMechanicalMotionController` 对应控制器。
- Bolt 使用 `bolt-articulation-v1` 与 `rigResourcePath`；两个关节演示种类均要求存在独立关节 JSON。新增静态模型仍只需配置，新增运动种类须在组合根接入对应控制器。
- `JsonUtility` 可能为缺省的内联 `motion` 创建全空对象，加载器将这种空值归一化为 `null`；包含实际字段的不合法配置继续按规则校验。

可复制 [`Bicycle.json`](../Assets/Resources/MechanicalCatalog/Models/Bicycle.json) 作为字段示例，但不要复用自行车 ID。正式入库还必须提供机器可读 BOM、稳定对象名、1:1 尺寸基准、来源许可、Source 与移动端 LOD、三级科普文本以及微信真机预算测试。[候选模型状态](References/MODEL_CANDIDATES.md)记录了 OM10 与 V8 尚未通过的环节。

`dotnet run --project Tools/Tests/MechMaster.Domain.Tests.csproj -c Release` 会自动扫描所有模型清单并检查目录、路径、ID、三级步骤与分类；编辑器的“验证工程自行车”入口还会对所有已入库模型核对 FBX/Prefab 对象绑定，然后执行自行车特有的 15/30/45 步和 595 实体断言。新增模型可有自己的步骤数，不继承自行车计数。

小部件先组装再进入整机的需求，后续可用子计划表达：变速器或避震子计划完成后产出“总成”，整车计划再引用该总成。

## 14. 相关文档

- [项目说明](../README.md)
- [整车工程 BOM](ENGINEERING_BOM.md)
- [产品规格](PRODUCT_SPEC.md)
- [开发与验证](DEVELOPMENT.md)
- [资料来源](References/SOURCES.md)
