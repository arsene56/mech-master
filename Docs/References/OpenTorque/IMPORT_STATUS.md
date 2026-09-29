# OpenTorque 行星减速器接入记录

更新日期：2026-09-29。首版接入固定版本的标准齿轮减速机构，不宣称完整执行器或真实动力学仿真。

## 来源、许可与范围

- 作者：Gabrael Levine；[官方仓库](https://github.com/G-Levine/OpenTorque-Actuator/tree/412762e9a4ca424564d3ebed882db95ef4b22ed9)。
- 固定提交：`412762e9a4ca424564d3ebed882db95ef4b22ed9`。
- 整机：`STEP/opentorque.step`，7,168,329 bytes，Git blob `037a81e60809baf60c1cdb45b7b885002e924a04`；SHA-256 `715965dc130555453d7d76d7ab51beeb8a5566a35cc37aae25cbb81151c879a3`。
- `STEP/low_backlash_gears.step` 仅是替代齿轮组件，4 定义 / 10 实例，不在首版中叠加或替换。作者部分旧装配说明对应上一版本，不能混用。
- 许可：[CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)。完整原许可、来源清单、作者署名与修改说明保存在 `Assets/StreamingAssets/MechanicalCatalog/OpenTorque/`，随运行资源发行。
- 由本工程转换、减面、赋材质的 OpenTorque 模型资产、Source 和预览按 CC BY-SA 4.0 分享；独立游戏代码和其它模型单独管理许可。发行前仍需核对平台分发条款，不给这些许可资产施加额外的使用限制。
- 下载源 CAD 和 OCCT 依赖只进入忽略的 `Library`；取源脚本以官方固定 Git blob 校验缓存。匿名 GitHub API 限额耗尽时，使用官方 Git 固定树取得 blob 身份，再下载并校验原件，不能跳过校验。

源装配包含减速机构核心，但没有电机、输入轴、编码器 PCB、磁体及多数螺钉/嵌件。盖板和磁体安装座不代表电子控制已完整。模型 ID 为 `gearbox.opentorque.planetary.v1`，菜单名称为 **OpenTorque 行星减速器**。不能将制造 BOM 的采购数量当作导入实例数量。

## 分件、比例与维修边界

源 STEP 经 OCCT/XCAF 解析为 **13 个叶定义 / 19 个叶实例**，所有 BRep 形体有效，源装配尺寸为 **110 × 110 × 95 mm**。每个实例保留源引用路径、装配矩阵、定义身份、哈希稳定 ID 与米制网格；运行时 1 unit = 1 m。

| 源零件 | 数量 | 边界 |
| --- | ---: | --- |
| 带内齿圈壳体、轴承压圈、后盖板 | 各 1 | 齿圈与壳体一体，不能另造独立拆装步骤 |
| 行星架 A / B / C | 各 1 | 保留三片真实源零件 |
| RA-8008C 输出轴承 | 1 | 完整维修单元，不拆内部实体 |
| 太阳轮 | 1 | 9 齿，输入端 |
| 行星轮 | 3 | 各 27 齿，公转半径 27 mm，间隔 120° |
| F625ZZ 带法兰轴承 | 3 | 整件，内部滚动体不逐个操作 |
| M5 × 30 销轴 | 3 | 重复件成组拆装 |
| 编码器盖、磁体安装座 | 各 1 | 不补造传感器、电路板或磁体 |

五个资源模块为 `housing / carrier / sun / planets / encoder`。三个等级均无遗漏、无重复地覆盖全部 19 个实例：

- 简单 **5 步**：功能模块。
- 进阶 **10 步**：壳体与后盖、行星架板组与输出轴承、太阳轮、三个行星轮组件、两件编码器安装件。
- 探索 **17 步**：真实零件维修单元；三根销轴仍为一个步骤，密封轴承内部不拆。

拆装允许任意顺序，是科普观察操作，不是可以照做的现实维修教程。三级中文知识每段不超过 50 个字。

## 分级爆炸观察（2026-09-29 优化）

OpenTorque 不再使用通用的小幅向外错开布局，而由 `OpenTorqueExplosionLayout` 根据源 CAD 主轴、三条 120° 行星轮方向及当前等级计算观察位移：

- 简单：五大模块完整分离；壳体、输出支承和编码器安装件沿主轴拉开，太阳轮单独抬出，三个行星轮仍保持整组。
- 进阶：后盖、输出轴承和两件编码器安装件独立分层；三套行星轮各自径向展开，每套齿轮、轴承、销轴仍保持内部装配关系。
- 探索：壳体、压圈、行星架三片板和输出轴承分别沿轴展开；三颗行星轮与各自轴承在对应径向通道内再轴向分开，轴承略向下错开以避开行星架遮挡，三根销轴仍作为完整组移出。

三档为 5 / 10 / 17 个独立观察单元，使用更侧向的初始取景，避免沿主轴前后遮挡。布局只平移、不旋转零件，不改变 CAD 尺寸、维修分组、拆装进度或存档；位移在完整源姿态时缓存，部分零件已拆下时，其余零件不会重新排列。其它三个模型继续使用原有布局和取景。

## 网格与材质

STEP 试三角化采用线性偏差 0.05 mm、角偏差 0.18 rad。清除重合点与退化面后统计如下；不是源作者给出的固定面数：

| 层级 | 三角形 | 用途 |
| --- | ---: | --- |
| Source | 275,926 | 完整源细节，19 个具名网格 |
| 分件 LOD0 | 139,050 | 五模块，保留全部身份与可拆网格 |
| 整机 LOD1 | 80,644 | 五个概览网格 |
| 整机 LOD2 | 16,000 | 一个移动概览网格 |

LOD0 保留壳体内齿圈、太阳轮和行星轮的齿面三角化，不把关键齿形粗减；主要简化轴承内部细节。打印件用尼龙/工程塑料外观，轴承和销轴用金属；不将整个装置做成全金属工业减速器。LOD1/2 是概览资产，不替代分件绑定；当前桌面样片仍一次加载 LOD0，微信按需加载和真机性能未验收。

`OpenTorqueAssetImportSettings` 对这套模型启用 `preserveHierarchy`，避免单网格太阳轮模块被引擎折叠成文件名节点；补齐默认 FBX 导入丢失的金属度，钢件为 0.85、打印件为 0。不修改其它模型导入配置。

## 连续演示

`OpenTorqueMotionRig.json` 和 `OpenTorqueMotionController` 绑定全部 19 个实例；纯 C# `PlanetaryGearKinematics` 按固定齿圈的运动学计算，不用逐关节缓入缓出关键帧：

- 内齿圈固定，太阳轮输入，行星架输出。
- 齿数 `9 / 27 / 63` 满足同轴齿数关系；减速比 `1 + 63/9 = 8`。
- 默认 **1.00×** 输入 60 rpm，行星架 7.5 rpm，行星轮相对固定坐标自转 -10 rpm；自转与公转同时连续进行。
- 0.50×–1.50× 调速、暂停/继续和结束共用现有控制界面。
- 全部实例姿态真正重复需要太阳轮转 24 圈，即默认 24 秒；不能用太阳轮一圈或行星架一圈作为所有 Transform 的复位周期。
- 销轴和密封轴承随行星架公转；轴承内部接触、滚动体及内外圈差速不模拟。磁体安装座仅随已有输入端示意转动，不补造连接轴。
- 演示只在完整装配时启动。八件遮挡支承/盖板使用临时透明观察材质，并停止投射遮挡阴影，仍处于装配状态，不改拆解计数或存档；退出恢复原阴影设置。
- 结束、切至拆装/爆炸/收纳、切换模型/等级时恢复源位置、旋转、父级、原材质/高亮和碰撞状态；沿用停止及拾取前的物理坐标同步。

这是理想运动学教学，不模拟电机、转矩、负载、齿面碰撞、弹性、低背隙或效率。8∶1 参数与[作者项目页面](https://hackaday.io/project/159404-opentorque-actuator)一致，不代表本游戏验证了作者公布的真实执行器性能。

## 可复现命令

已有正式 FBX 可直接打开工程运行。重建需要 Python 3.12、固定 `cadquery-ocp==7.8.1.1.post1` / `vtk==9.3.1` 和 Blender 4.5 LTS；当前转换器复用 Bolt 的构建依赖缓存，不把 CAD 库加入游戏。

```powershell
# 使用本机 Python 3.12 路径；已有 Bolt 依赖时跳过安装。
$torquePython = 'D:/MyConfiguration/TCLXUSER/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $torquePython -m pip install --target Library/MechMaster/BoltSource/parser/python cadquery-ocp==7.8.1.1.post1 vtk==9.3.1
& $torquePython Tools/Content/fetch_opentorque_sources.py
& $torquePython Tools/Content/convert_opentorque_step.py
& 'C:/Program Files/Blender Foundation/Blender 4.5/blender.exe' --background --python-exit-code 1 --python Tools/Blender/import_opentorque.py
& 'C:/Program Files/Blender Foundation/Blender 4.5/blender.exe' --background --python-exit-code 1 --python Tools/Blender/validate_opentorque.py
dotnet run --project Tools/Tests/MechMaster.Domain.Tests.csproj -c Release

# 原工程 Editor 未运行时执行，不加 -quit；验证器负责进入/退出 Play 并恢复用户存档。
& 'C:/Program Files/Tuanjie/Hub/Editor/2022.3.62t15/Editor/Tuanjie.exe' -batchmode -projectPath . -executeMethod MechMaster.Editor.OpenTorqueImportValidator.ValidateFromCommandLine -logFile Library/MechMaster/OpenTorqueSource/converted/editor-runtime-final.log
```

中文知识/分组修改 `Tools/Content/opentorque_content.py`；材质、LOD、运动绑定修改 `Tools/Blender/import_opentorque.py`；运动学修改两份专用运行时 C#。不要手改生成的 FBX、目录和清单。Source、正式 FBX、JSON 和对应 `.meta` 提交版本控制，第三方许可随包保留；用户自行提交，不提交构建缓存。

## 验证与边界

- Blender Source 与 FBX 回导：19 实例/13 定义、源变换、毫米到米、110×110×95 mm 尺寸、27 mm 公转半径、120° 间隔及三层导出通过。
- .NET **22 项**通过：新增标准版本身份/三级覆盖/许可、运动绑定和运动学连续性/真实闭合周期，旧自行车、Moveo、Bolt 测试继续通过。
- 本机引擎三档检查：绑定、米制尺寸、241 相位采样、8∶1、转轴/公转、暂停调速、无漂移、碰撞及材质复原、完整拆装与独立存档。
- 每档 6 组停止后的单件爆炸/再点收回、6 组真实射线拖放及恢复操作模式检查；关闭物理自动推进/自动同步，不由测试在停止后提前同步。
- 原生鼠标事件跨帧点击播放、结束、拆装，再拖入正确分类区；切换 Bolt/Moveo/自行车检查旧演示可用性。测试前后恢复所有模型偏好和进度。
- 最终三档日志为 `Library/MechMaster/OpenTorqueSource/converted/editor-runtime-view-final.log`，上述通过标记齐全，Editor 退出码 0；四模型切换后分别实际播放、暂停、继续、结束并检查源姿态复原，标记为 `MECH_MASTER_OPENTORQUE_SWITCH_OK`。通用目录/全模型绑定日志为同目录 `editor-catalog-final.log`，退出码 0。首次太阳轮节点折叠的失败日志保留为 `editor-runtime.log`，层级修复日志为 `editor-runtime-hierarchy.log`。最终没有 C# 编译错误或导入绑定缺失。
- 预览近景相机显式使用 1 mm 近裁剪，避免默认 300 mm 把这个小模型截掉；真实引擎透明观察预览保存在 `Docs/Preview/OpenTorqueMotion.png`，与 Blender 完整/爆炸预览区分。原默认相机已经根据模型尺寸自动调整，不是源比例被改大。
- 分级爆炸优化的最终日志为 `Library/MechMaster/MotionRegression/opentorque-explosion-final.log`，独立测试工程 Editor 退出码 0；三档 `MECH_MASTER_OPENTORQUE_EXPLOSION_OK` 与 `MECH_MASTER_OPENTORQUE_PARTIAL_EXPLOSION_OK` 齐全。检查独立位移、相邻档显著差异、分组内部姿态、包围盒分离、默认相机下各单元至少 35% 投影表面采样可见、展开/收回/转入拆装复原、部分已拆时稳定布局与存档不变；原有运动、原生 GUI、拾取/拖放、任意顺序拆装和四模型切换（含爆炸烟测）也通过。布局/验证器 SHA-256 与工作区相同，.NET 22 项继续通过。
- 新增 `Docs/Preview/OpenTorqueExplosionSimple.png`、`OpenTorqueExplosionStandard.png`、`OpenTorqueExplosionAdvanced.png`：相同观察角、1600×900 的真实引擎分件对比渲染，不含 UI；与当前屏幕的正式取景检查分开，不能当作微信真机截图。
- 引擎预览和日志位于忽略的 `Library/MechMaster/OpenTorqueSource/converted/`；自动化通过不等于用户实际 Game View 人工流程、微信触控或真机帧率验收。
