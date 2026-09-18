# 开发与验证指南

## 1. 环境

- Windows 10/11。
- 团结引擎或兼容 Unity 2022 LTS 编辑器。
- Blender 4.5 LTS。
- .NET 8 SDK 或更高版本。
- 微信开发者工具：取得 AppID 后安装。

项目不依赖外部 NuGet 测试框架，领域测试可离线编译。

## 2. 首次打开

1. 克隆仓库并确认分支为 `main`。
2. 使用引擎 Hub 打开仓库根目录。
3. 如编辑器提示版本迁移，先备份或创建新分支，再让编辑器更新项目文件。
4. 等待模型和脚本导入完成。
5. 打开 `Assets/Scenes/Main.unity` 并进入 Play Mode。

运行入口由 `MechMasterApp.Bootstrap` 自动创建，因此场景只保留一个空根节点。

## 3. 日常验证

### 3.1 领域规则

```powershell
dotnet run --project Tools/Tests/MechMaster.Domain.Tests.csproj -c Release
```

测试覆盖三档步骤数、错误顺序、错误工具、正向拆解、严格倒序组装和分层科普内容。

### 3.2 Blender 资产

```powershell
& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' `
  --background `
  --python Tools/Blender/generate_bicycle.py

& 'C:\Program Files\Blender Foundation\Blender 4.5\blender.exe' `
  --background Assets/Art/Models/Source/BicyclePrototype.blend `
  --python Tools/Blender/validate_bicycle.py
```

校验脚本检查稳定对象名、真实尺寸、辐条完整度和网格统计。

### 3.3 引擎资产

打开编辑器后执行菜单 `机械大师 → 验证首版样片`。

命令行编辑器可调用：

```text
-executeMethod MechMaster.Editor.PrototypeValidator.ValidateFromCommandLine
```

## 4. 代码边界

- `Domain` 不引用 Unity API，确保规则可直接由 .NET 测试。
- `Runtime` 负责视图、输入、声音、存档和流程协调。
- `Editor` 只包含导入检查和开发工具，不能进入运行包。
- 微信平台 API 必须通过适配接口进入，不能写入领域对象。

## 5. 新增零件

1. 在 Blender 中创建或拆分对象，使用稳定、唯一的英文对象名。
2. 在目录类中增加 `PartDefinition`。
3. 在模型视图中增加逻辑 ID 到对象名的映射。
4. 更新拆装顺序和知识文本。
5. 增加领域测试和资产验证项。
6. 在三档难度下分别检查点选区域、爆炸方向和倒序组装。

## 6. 新增机械模块

新模块应独立提供目录或工厂类、步骤列表、逻辑与 3D 映射、模型验证器、分层知识内容以及资料来源记录。

不要在 `MechMasterApp` 中为每台机械堆积条件分支；进入第二个正式模块前，应抽取 `IMechanicalModule`、`IModelProvider` 和 `IProgressStore`。

## 7. 性能检查

微信真机阶段至少测量：

- 首包与分包大小。
- 首次进入耗时。
- 峰值内存和资源释放后的内存回落。
- 中低端手机帧率、发热和耗电。
- Draw Call、材质数量、透明度和实时光源成本。
- 高频触摸下的 GC Alloc。

未经真机测量不要写“性能达标”。

## 8. 提交前清单

- `dotnet run` 六项测试通过。
- Blender 验证输出 `MECH_MASTER_ASSET_VALIDATION_OK`。
- 引擎控制台无编译错误。
- 三档难度都能完成拆解和组装。
- 文档中的本地链接有效。
- 没有提交 `Library`、`Temp`、`obj`、`bin`、`__pycache__` 和 `.blend1`。
- 新资产已记录来源和许可证。
- 不提交 AppID、密钥、个人账号或构建缓存。

