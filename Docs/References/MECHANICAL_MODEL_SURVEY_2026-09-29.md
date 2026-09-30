# 免登录机械资源补充检索

检索及接入修订：2026-09-29。用户没有 Sketchfab 账号，希望沿现有来源寻找软尾或其他机械。沿用免费、允许修改和随项目发行的条件，不创建账号。后续用户明确选定 Carbon Frame Bike，可行则接入，现已完成该软尾的本机样片接入；其他候选尚未接入。

## 结论

- Santa Cruz V10（Arsen Ismailov）的官方免费文件仍需 Sketchfab 登录；本次未找到作者发布的免登录替代下载入口。
- 作者 GitHub 的 **Carbon Frame Bike 全避震山地车**已匿名下载、固定源校验和本机接入：14 模块、307 网格、14/34/51 步与连续悬架演示。派生资产实际移除四个可见标识网格和全部作者贴图，保留 CC BY-SA 署名/修改说明；Khronos 标识问题仍未关闭，不能称为所有权利或微信发行均已审定。详见 [Carbon 接入记录](CarbonFrameBike/IMPORT_STATUS.md)。
- 其他机械中，**Voron V0.2r1、Solo12、TriFingerEdu、BCN3D Sigma** 均有无需账号访问的实际 CAD 文件，可进入下一轮源模型审计。另有 CC BY 4.0 的 Adjustabike 城市/货运自行车概念设计，但不是软尾。
- 除 Carbon 已完成原件、源尺寸/姿态、引擎与悬架验证外，其他候选的 HTTP 200、文件大小和目录检查只确认可取得文件；没有解析完整装配包或验证其引擎/尺寸/运动。所有模型的微信真机性能尚未验收。

## 软尾候选：Carbon Frame Bike

- [作者发布目录](https://github.com/prefrontalcortex/glTF-Sample-Models/tree/carbon-frame-bike/2.0/CarbonFrameBike)、[README 与许可](https://github.com/prefrontalcortex/glTF-Sample-Models/blob/carbon-frame-bike/2.0/CarbonFrameBike/README.md)、[原始项目](https://prefrontalcortex.de/en/projects/mixed-reality-bike/)。
- 作者：Robert Schweier；实时模型和动画由 Felix Herbst / prefrontal cortex 制作，Needle 提供支持。
- [GLB 文件](https://github.com/prefrontalcortex/glTF-Sample-Models/blob/carbon-frame-bike/2.0/CarbonFrameBike/glTF-Binary/CarbonFrameBike.glb)：12,183,440 字节，匿名 HEAD 返回 HTTP 200；目录另有 glTF、纹理与多种压缩版本，作者还链接 USDZ。
- 固定提交：`93c72b11cf78dd6a3cd50b875d752cd7e6dd4ab2`。这是作者维护的发布分支，不是已经合入 Khronos 官方样本库的型号。
- 本次实际读取 `glTF/CarbonFrameBike.gltf`：757 个节点、312 个网格定义、31 个材质、11 个图像引用、4 套蒙皮，动画 `Holobike_Loop` 有 356 个通道。**节点/网格数量不等于可拆零件数量**；包含分组、骨骼、线管和阴影辅助对象。
- 有独立分组及子节点：`RobertS2016_Rahmen_Schwinge_links/rechts`（左右后摇臂）、`Daempfer_Cane-Creek_DB_200-57`（后避震器）、`Federgabel`（避震前叉）、`RobertS2016_Rahmen_Hauptlager_Achse`（主铰轴）、`RadHinten/RadVorn`（后/前轮）。结构上比只有整车预览的资源更值得继续验证。
- README 采用 [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)；改编模型需署名、附许可、记录修改并按相同许可发布。
- [Khronos 审查问题 #83](https://github.com/KhronosGroup/glTF-Sample-Assets/issues/83)仍为 open、无后续评论，指出模型中标识的许可问题。不能把“已标注 CC BY-SA”写成全部标识权利已经澄清；也不据此断言模型整体禁止使用。先技术审计，再落实实际使用资产和标识的分发安排。
- 后续需要导入几何、辨别可逆拆装边界、审计尺寸和原始静止姿态，确认动画是爆炸展示还是可信悬架运动；不能把现有拆装动画直接当作避震仿真。

## 其他免登录 CAD 候选

以下排序是基于格式、资料和题材差异的初步判断，不是已完成转换后的性能结论。

| 建议顺序 | 机械 | 已核实资源 | 对本项目的价值 | 许可与主要待办 |
| --- | --- | --- | --- | --- |
| 1 | [Voron V0.2r1 CoreXY 3D 打印机](https://github.com/VoronDesign/Voron-0) | `CAD/V0.2R1_Master_Assembly_STEP.zip`（32,088,462 字节）、Fusion 源文件、分件 STL、装配资料 | 扩展直线导轨、双皮带 CoreXY、Z 轴与挤出齿轮等机械教学，整机 STEP 可复用现有转换工具 | [GPL-3.0](https://github.com/VoronDesign/Voron-0/blob/Voron0.2r1/LICENSE)；派生资源需遵守对应源文件和同许可分发要求，整机分件与规模待审计 |
| 2 | [Solo12 四足机器人](https://github.com/open-dynamic-robot-initiative/open_robot_actuator_hardware/tree/master/mechanics/quadruped_robot_12dof_v1) | `quadruped_12dof_v1.zip`（37,989,879 字节），SolidWorks 整机、STL、BOM/装配资料 | 与已接入 Bolt 同源，十二自由度，能复用部分机构知识与运动框架 | [BSD-3-Clause](https://github.com/open-dynamic-robot-initiative/open_robot_actuator_hardware/blob/master/LICENSE)；整机 SolidWorks 转换仍需实测，不声称已经有完整 STEP |
| 3 | [TriFingerEdu 三指机械手平台](https://github.com/open-dynamic-robot-initiative/open_robot_actuator_hardware/tree/master/mechanics/tri_finger_edu_v1) | `tri_finger_edu_v1.zip`（32,550,292 字节）、STL、尺寸和装配说明 | 与 Bolt 同源，三个相同手指模块、共九自由度，可做多指开合/协同操控教学 | BSD-3-Clause；审计 SolidWorks 装配、皮带和指节边界，动态内容需另做绑定 |
| 4 | [BCN3D Sigma 双喷头 3D 打印机](https://github.com/BCN3D/BCN3DSigma-Mechanics) | 顶层 `SLDASM`（14,251,520 字节）、多级子总成/零件、BOM、制造图纸 | 与 Moveo 同厂商，独立双喷头、XY 导轨、Z 丝杆和挤出机构；适合更复杂的设备拆解 | [CERN OHL 1.2](https://github.com/BCN3D/BCN3DSigma-Mechanics/blob/master/LICENSE/cern_ohl_v_1_2.txt)，不是 Moveo 的 MIT；保留许可及修改记录、提供对应可编辑资料，整套转换与优化成本较高 |

上述四项的代表性源文件本次均匿名 HEAD 返回 HTTP 200。Sigma 顶层装配必须连同引用零件获取；单独下载顶层文件不是完整资源。

固定目录版本：

- VoronDesign/Voron-0，分支 `Voron0.2r1`：`a53fc87562fd630c846af38d7de850c894dc3d85`。
- ODRI 硬件仓库，分支 `master`：`66af1522b4fba0ec4a1d7790e66f5e4652208d30`。
- BCN3D/BCN3DSigma-Mechanics，分支 `master`：`12c0bbe6fa76a9d80eff126617de8b759aab5d23`。

## 自行车补充与未列为首选的线索

- [Adjustabike](https://github.com/JamieTaylor23/Adjustabike)：作者 README 明确仓库内容为 CC BY 4.0；有 `Bike Assembly.stp.zip`（7,080,870 字节，匿名 HTTP 200）、货运扩展和装配图。固定提交 `c7afd314d078b1848dce9b7ddc48dd6d3ab553fd`。这是可重组的城市/货运自行车概念，不是山地软尾，也未被作者宣称为成熟量产设计。
- [Project Mjolnir](https://projectmjolnir.com/mobile.html)：轮椅使用者的适应性山地车，官网全避震 CAD 下载仍指向 GrabCAD。本次没有核实免登录文件及满足项目需求的许可，不当作普通两轮软尾替代。
- [LumenPnP](https://github.com/opulo-inc/lumenpnp)：可扩展贴片设备题材，但仓库对 CAD、代码、库素材与商标分别授权；[许可文件](https://github.com/opulo-inc/lumenpnp/blob/main/LICENSE)要求衍生物移除 Opulo/LumenPnP 标识，CAD 为 CERN-OHL-W v2。本次未审计完整装配源，不排到上述四项之前。
- GitHub 上发现的多项 Stirling 发动机 CAD 仓库没有明确模型许可；未把“仓库公开”当成允许随游戏分发的依据。

## 建议

Carbon 已按用户选择完成本机软尾接入，可继续人工观感与微信性能验收。若后续希望增加另一类机械，可优先验证 Voron V0.2r1；与现有机器人衔接则可考虑 Solo12。未获新的接入指示前，不默认启动这些候选。
