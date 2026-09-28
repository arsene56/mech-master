# 开发与验证指南

## 环境

- Windows 10/11。
- Blender 4.5 LTS。
- .NET 8 SDK 或更高版本。
- 团结引擎或兼容 Unity 2022 LTS 编辑器。
- 微信 AppID、微信构建支持与开发者工具：平台接入阶段需要。

## 首次打开

1. 克隆仓库并确认当前分支为 `main`。
2. 用引擎 Hub 打开仓库根目录。
3. 等待自行车与 Moveo 的 FBX、JSON 和 C# 脚本导入。
4. 打开 `Assets/Scenes/Main.unity` 并进入 Play Mode。
5. 运行入口由 `MechMasterApp.Bootstrap` 创建，不需要在场景中手工绑定脚本。

## 完整资产流水线

### 自行车

按以下顺序执行：

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' `
  --background --python Tools/Blender/generate_engineering_bicycle.py

& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' `
  --background --python Tools/Blender/export_engineering_lods.py

& Tools/Content/Generate-BicycleInteractionCatalog.ps1

& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' `
  --background --python Tools/Blender/validate_engineering_bicycle.py

dotnet run --project Tools/Tests/MechMaster.Domain.Tests.csproj -c Release
```

生成顺序不能互换：交互目录依赖最新模型清单，LOD 与验证依赖最新 `.blend`。

### Moveo 机械臂

已有运行资产可直接导入，无需 SolidWorks。重建资产时还需 Python 3.9 或更高版本；以下命令在仓库根目录执行：

```powershell
python Tools/Content/fetch_moveo_sources.py
python -m pip install --target Library/MechMaster/MoveoSource/parser/python olefile==0.47
python Tools/Content/convert_moveo_solidworks.py
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' `
  --background --python Tools/Blender/import_moveo.py
dotnet run --project Tools/Tests/MechMaster.Domain.Tests.csproj -c Release
```

获取、解析与 Blender 导入依次执行。源 CAD 下载和转换缓存位于忽略的 `Library/MechMaster/MoveoSource/`；正式模型、目录、来源与许可由导入脚本写入 `Assets/`。修改中文名称、知识和分组时编辑 `Tools/Content/moveo_content.py`；修改几何清理、材质或 LOD 时编辑 `Tools/Blender/import_moveo.py`。固定提交、尺寸、清理记录及验证边界见 [Moveo 接入记录](References/Moveo/IMPORT_STATUS.md)。

## 预期验证结果

Blender 验证应以以下文本结束：

```text
ALL ENGINEERING BICYCLE VALIDATIONS PASSED
```

自行车关键基线：

- 14 个模块、595 个唯一零件 ID。
- 轮外径 0.698 m、轴距 1.120 m。
- 前 / 后花鼓开档 0.110 / 0.148 m。
- 前 / 后碟片约 0.180 / 0.160 m。
- Source 111,694 面、LOD1 71,305 面、LOD2 26,671 面。
- Simple / Standard / Advanced 为 15 / 30 / 45 步；每档都覆盖 595 个模型对象。

Moveo 基线：9 模块、87 个源零件定义、366 实体；三档为 9 / 20 / 42 步，每档完整覆盖同一批实体。木底板为 550 × 550 × 16 mm；LOD0 / LOD1 / LOD2 为 447,667 / 268,593 / 34,999 三角面。

.NET 测试共 15 项，应全部显示 `PASS`，覆盖状态机、BOM、继承数量、尺寸基准、三档计数、模型绑定、Moveo 内容完整性及 Moveo 关节轴 / 四连杆关键帧配置。

## 修改工程模型

1. 在 `bicycle_engineering.json` 修改尺寸、部件或数量。
2. 在 `generate_engineering_bicycle.py` 更新对应几何。
3. 每个实体使用唯一 `mm_part_id`，格式为 `bike.<assembly>.<component>.<index>`。
4. 同步更新 `validate_engineering_bicycle.py` 的新不变量。
5. 重跑完整资产流水线。

不要直接编辑生成后的 FBX、模型清单或交互目录。

## 修改知识内容

当前三档知识由 `Generate-BicycleInteractionCatalog.ps1` 按零件类型生成。修改通用解释时编辑 `Get-FunctionText`；需要逐件出版级文案时，应在 BOM 增加显式知识字段，并让生成器优先使用显式内容。

所有制动、转向和承载结构的文案都需要专家复核。面向 6–8 岁的基础说明应使用具体动作和结果，避免无解释术语。

## 引擎内检查

完整 Editor 可用后至少检查：

- C# 无编译错误、JSON 可被 `JsonUtility` 读取。
- 自行车 14 个模块、Moveo 9 个模块的 FBX 在世界坐标中正确拼成整机。
- 三档均能命中正确对象并完整拆解。
- 拆解与组装均可自由选择零件，最终能回到完整状态。
- 45 步探索模式的组合机械单元没有异常爆炸距离。
- 细小件、内部件和重叠件的触摸热区可用。
- 切换拆解等级、重启应用后本地进度恢复正确。
- 镜头旋转、双指缩放与零件拖动不冲突。

`PrototypeValidator.ValidateFromCommandLine` 会校验所有模型清单、资源与对象绑定，并对自行车检查 15/30/45 步及 595 个对象。`PrototypeValidator.ValidateRuntimeBootstrapFromCommandLine` 可在批处理 Play Mode 中检查当前已保存模型的启动、分件加载与计划绑定；它不会执行拆装或重置存档。两者都通过后仍须人工复核菜单、拖动与画面。

Moveo 专用自动回归在停止 Editor 后运行：

```powershell
& 'C:\Program Files\Tuanjie\Hub\Editor\2022.3.62t15\Editor\Tuanjie.exe' `
  -batchmode -projectPath . `
  -executeMethod MechMaster.Editor.MoveoImportValidator.ValidateFromCommandLine `
  -logFile Library/MechMaster/MoveoSource/converted/editor-runtime.log
```

按本机安装位置调整 Editor 路径。该入口自动进入和退出 Play 并关闭 Editor，因此不添加 `-quit`；使用正常图形模式以执行拾取和拖入托盘。它检查三档绑定、爆炸不改进度、存档、完整拆装和姿态复原，验证 Moveo 五轴关节、夹爪四连杆、暂停、调速、循环无漂移和视图切换复位，并切回自行车检查动态演示可用性；退出 Play 后恢复验证前的全部模型偏好及进度。成功日志包含 `MECH_MASTER_MOVEO_TIER_OK` 三档记录、`MECH_MASTER_MOVEO_MOTION_OK` 和 `MECH_MASTER_MOVEO_RUNTIME_OK`。

## 界面文字清晰度

- 原型 UI 使用 `PixelUILayout` 将 1920×1080 设计坐标转换为整数像素坐标，字号按当前渲染分辨率重新生成；不要再用 `GUI.matrix` 整体缩放文字位图。
- 例如正文在 1920×1080 使用 20 px，在 3840×2160 使用 40 px。绘制位置和分类托盘命中区域使用同一套像素映射。
- 普通 Play 会关闭 Game View 的 `Low Resolution Aspect Ratios`，并恢复 `Scale = 1x`。也可使用菜单“机械大师 → 修复 Game 预览清晰度”。无需修改 Windows DPI 设置。
- 注意 Unity 2022 LTS 的 `m_LowResolutionForAspectRatios` 是按构建平台分组的数组，不可直接对它设置 `SerializedProperty.boolValue`；开发工具通过对应属性设置当前平台。
- 使用“机械大师 → 验证文字像素布局”检查 8 种分辨率、字号与像素对齐；Play 中额外验证 14 个分类托盘的实际命中坐标。
- 3D 的 MSAA 不能替代正确的文字像素密度。验证清晰度时使用原生分辨率和 1x 预览，不要把放大的低分辨率画面当成最终效果。
- 当前仍使用本机动态中文字体；微信发布前需另外接入具有明确再分发许可的中文字体，并做真机清晰度和字体缺字检查。

## 视角与工作台回归检查

- 默认“旋转视角”模式：按住车身或空白处拖动旋转，轻点零件查看百科并讲解；桌面右键始终用于旋转。
- “拆装零件”模式：直接拖动机械单元到对应底部分类区，变绿时松开；拖空白处旋转。“查看收纳”将已拆零件和 3D 托盘完整纳入中央区域，组装仍从这些 3D 展示位置拖回工作区，进入组装会自动调整取景。
- 鼠标通过 `OnGUI` 的独立按下、拖动、抬起事件记录坐标，避免一帧内短拖动被帧轮询误认为点击；触摸通过同一手势状态机处理，双指切入时取消零件拖动，所有手指离开后才开始新手势。
- `WorkshopLayout` 是面板、托盘命中与相机构图的共同布局来源。左右栏可收起；“整机归位”只重置取景，不改变进度。相机保持全屏绘制，零件跨进底部分类区时不会被裁掉。
- “画面平移”提供上下左右点按按钮，每次移动 48 个设计像素，按屏幕方向移动，不改变旋转中心、缩放和拆装状态。整机默认取景向左偏移两个步长（96 个设计像素）；“中”取消用户平移并回到当前视图默认位置；“整机归位”恢复该整机取景，“查看收纳”使用居中取景。平移区域与其它 UI 一样拦截拆装/旋转手势。
- “全局一键爆炸”应展开当前等级全部未拆逻辑单元（完整整车时为 15 / 30 / 45），再次点击完整收回；“点击单件爆炸”每次只允许一个逻辑单元处于弹开状态，再点同一单元收回。
- 整车完整时检查顶部“运转演示”：曲柄与脚踏连续转动、链条绕过两只后拨导轮、飞轮与前后轮默认按 36T / 24T 齿比联动，画面不生成脚撑或支架；可暂停、继续、结束及调节 30–90 转/分。按住左刹把时仅后轮和联动的传动系统逐渐停转，前轮继续转动；按住右刹把时仅前轮逐渐停转，后轮继续转动；同时按住则两轮都停。松开后对应车轮逐渐恢复设定速度，失焦、双指缩放或退出模式都会释放按压。退出后原有链节、碰撞热区和所有零件姿态恢复，拆装计数与存档不变。拆下一件后按钮应提示先组装完整。
- 两种爆炸模式都必须保持拆解计数和已拆 ID 集合不变，并保留拖动旋转、滚轮/双指缩放与四向平移；切到“拆装零件”、整机归位、查看收纳或切换等级后应自动复位。
- 品牌标题使用独立无内边距、不换行的样式和 42 像素高区域，不再复用百科大标题样式；Play 验证会检查 8 种分辨率下完整标题的字体测量尺寸。
- 模型回归额外检查：水壶架两颗螺栓与下管接触、外走线 Bezier 实际采样间距、坐垫三个闭合曲面层及减压凹槽。预览见 `Docs/Preview/BicycleEngineeringSaddle.png`。沿管布线为当前通用车型的静态示意，未模拟转向/悬架运动时软管形变，也不是具体品牌维修装配图。
- Play 中使用“机械大师 → 验证视角与面板交互”：检查完整取景、折叠布局、鼠标与单指手势、UI 拦截、取消拖动、中文语音就绪，并联动文字像素检查。验证器不提交拆装操作，不重置存档。
- 修改脚本前先停止 Play，等待编译完成后重新 Play。当前原型不保证运行时领域对象能跨脚本热重载恢复。
- 使用“机械大师 → 验证自行车动态演示”可自动检查转轴绑定、约 126 个运转显示链片、播放/暂停/恢复及退出后姿态复原。命令行入口为 `MechMaster.Editor.PrototypeValidator.ValidateMotionFromCommandLine`；自动检查仍需配合实际画面观察。
- Moveo 动态演示由 `MoveoMotionController` 读取 `Assets/Resources/MechanicalCatalog/MoveoMotionRig.json`；自动回归检查五轴、支承轴、夹爪两套四连杆、暂停 / 调速 / 循环复位、碰撞状态和切换视图。该演示没有刹车热区，也不会在非完整装配时启动。
- 手机端仍须实机验证双指缩放、触摸取消、前后台切换与窄屏布局；Editor 模拟触摸不等同于微信真机验收。

## Windows Editor 中文讲解

- `VoiceNarrator` 不再只写日志；Windows Editor 启动本地语音辅助进程，通过标准输入传入 Base64 编码的中文文案，使用系统默认音频设备播放。切换零件会打断旧讲解，关闭开关立即停止，退出 Play 后关闭辅助进程。
- 依赖 Windows .NET Framework 4.x 编译器、`System.Speech` 和已安装的 `zh-CN` 桌面语音。本机使用 Microsoft Huihui Desktop。不重新分发系统语音文件，不调用网络或 PowerShell，不修改脚本执行策略。
- `LocalSpeechBuilder` 在导入和进入 Play 前，将 `Tools/Audio/WindowsSpeechHost.cs` 编译到忽略提交的 `Library/MechMaster/WindowsSpeechHost.exe`；也可在停止 Play 后运行“机械大师 → 构建本地中文语音”。
- 左侧状态应显示“系统中文语音 · 就绪”。点击零件或“再次讲解”后，日志依次出现 `MECH_MASTER_NARRATION_SPEAKING` 和 `MECH_MASTER_NARRATION_FINISHED`；每条零件讲解限制在 50 个字以内；若系统静音或输出设备不正确，事件成功也不能证明扬声器可听见，需人工试听。
- 失败时显示“语音不可用，请查看日志”，不会把讲解开关的保存状态当成播放器已就绪。查找 `MECH_MASTER_NARRATION_UNAVAILABLE` 或 `Local speech build:`。
- 当前适配仅覆盖 Windows Editor。微信发布版需另接已授权中文音频或平台语音服务，并验证音频解锁、缓存和前后台恢复；不能将本地进程方案打包进小游戏。
- 鼠标事件设计参考 [Unity Event 文档](https://docs.unity.com/en-us/engine/6000.7/script-reference/unityengine/event)；本地语音接口参考 [Microsoft SpeechSynthesizer](https://learn.microsoft.com/en-us/dotnet/api/system.speech.synthesis.speechsynthesizer)。

## 代码边界

- `Domain` 不引用 Unity 或平台 API。
- `Runtime` 负责目录装载、视图、输入、声音和本地存档。
- `Editor` 只包含导入和开发工具。
- Blender 与内容生成脚本是资产的可重复来源。
- 微信能力通过适配接口进入，不能污染领域层。

## 微信性能检查

未取得真机数据前不能写“性能达标”。至少测量：

- 首包、分包和下载资源大小。
- 首次与二次进入耗时。
- 峰值内存和模块退出后的回落。
- 中低端手机帧率、发热与耗电。
- Draw Call、SetPass、纹理显存和 Shader 变体。
- 595 个碰撞热区对物理和 GC 的影响。
- 前后台、来电打断、音频恢复和缓存失败。

微信版本应从“全量 LOD0”切换为“整车 LOD1 + 当前模块 LOD0”。

## 提交前清单

- Blender 工程验证通过。
- .NET 测试全部通过。
- 在可用时完成 Editor 编译和 Play Mode 检查。
- Editor 运行时验证通过全局/局部爆炸目标数、重复点击收回、自动复位以及拆解进度不变。
- 自行车三档为 15 / 30 / 45 步，Moveo 为 9 / 20 / 42 步。
- 每档完整且无重复地覆盖本模型全部实体：自行车 595，Moveo 366。
- 文档统计与运行清单一致。
- 新资料与第三方资产记录来源和许可。
- 不提交 AppID、密钥、个人账号、`Library`、`Temp`、`obj`、`bin`、`__pycache__` 或 `.blend1`。
