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
3. 等待 16 个工程 FBX、JSON 和 C# 脚本导入。
4. 打开 `Assets/Scenes/Main.unity` 并进入 Play Mode。
5. 运行入口由 `MechMasterApp.Bootstrap` 创建，不需要在场景中手工绑定脚本。

## 完整资产流水线

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

## 预期验证结果

Blender 验证应以以下文本结束：

```text
ALL ENGINEERING BICYCLE VALIDATIONS PASSED
```

关键基线：

- 14 个模块、595 个唯一零件 ID。
- 轮外径 0.698 m、轴距 1.120 m。
- 前 / 后花鼓开档 0.110 / 0.148 m。
- 前 / 后碟片约 0.180 / 0.160 m。
- Source 101,472 面、LOD1 59,487 面、LOD2 21,657 面。
- Simple / Standard / Advanced 为 14 / 195 / 595 步。

.NET 测试应全部显示 `PASS`，覆盖状态机、BOM、继承数量、尺寸基准、三档计数和模型绑定。

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
- 14 个模块 FBX 在世界坐标中正确拼成整车。
- 三档均能命中正确对象并完整拆解。
- 拆解与组装均可自由选择零件，最终能回到完整状态。
- 595 步模式的爆炸距离没有异常放大。
- 细小件、内部件和重叠件的触摸热区可用。
- 切换难度、重启应用后本地进度恢复正确。
- 镜头旋转、双指缩放与零件拖动不冲突。

当前仓库保留 `PrototypeValidator` 作为旧样片检查器；它尚未替代新的工程目录验证。完整 Editor 就绪后应将其升级为读取运行清单，而不是继续检查 4/8/12 旧步骤。

## 界面文字清晰度

- 原型 UI 使用 `PixelUILayout` 将 1920×1080 设计坐标转换为整数像素坐标，字号按当前渲染分辨率重新生成；不要再用 `GUI.matrix` 整体缩放文字位图。
- 例如正文在 1920×1080 使用 20 px，在 3840×2160 使用 40 px。绘制位置和分类托盘命中区域使用同一套像素映射。
- 普通 Play 会关闭 Game View 的 `Low Resolution Aspect Ratios`，并恢复 `Scale = 1x`。也可使用菜单“机械大师 → 修复 Game 预览清晰度”。无需修改 Windows DPI 设置。
- 注意 Unity 2022 LTS 的 `m_LowResolutionForAspectRatios` 是按构建平台分组的数组，不可直接对它设置 `SerializedProperty.boolValue`；开发工具通过对应属性设置当前平台。
- 使用“机械大师 → 验证文字像素布局”检查 8 种分辨率、字号与像素对齐；Play 中额外验证 14 个分类托盘的实际命中坐标。
- 3D 的 MSAA 不能替代正确的文字像素密度。验证清晰度时使用原生分辨率和 1x 预览，不要把放大的低分辨率画面当成最终效果。
- 当前仍使用本机动态中文字体；微信发布前需另外接入具有明确再分发许可的中文字体，并做真机清晰度和字体缺字检查。

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
- 三档计数仍为 14 / 195 / 595。
- 595 个进阶步骤与模型对象一一对应。
- 文档统计与运行清单一致。
- 新资料与第三方资产记录来源和许可。
- 不提交 AppID、密钥、个人账号、`Library`、`Temp`、`obj`、`bin`、`__pycache__` 或 `.blend1`。
