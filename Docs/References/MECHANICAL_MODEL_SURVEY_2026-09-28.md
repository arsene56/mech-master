# 免费精细可拆解机械 3D 资源检索

初次检索：2026-09-28；接入状态修订：2026-09-29。

免登录软尾与其他机械的补充结果见 [2026-09-29 资源检索](MECHANICAL_MODEL_SURVEY_2026-09-29.md)：Carbon Frame Bike、Voron V0.2r1、Solo12、TriFingerEdu、BCN3D Sigma 与 Adjustabike，区分实际文件可访问与完成接入验收。

用户已明确：目前只查免费且可用于 Mech.Master 工程的机械。筛选条件为源文件无需购买、许可允许修改和随项目发行（包括商业用途）、来源方提供实际 CAD/分件文件。此前付费资源已从推荐名单移除。

用户随后选择先接入 Moveo。官方资料已下载，通过直接解析总装保存网格与装配变换完成免费本地转换，生成 9 模块、366 实体和三档拆装目录。最新状态以 [Moveo 接入记录](Moveo/IMPORT_STATUS.md)为准。

用户之后确认接入 Bolt，现已通过固定 STEP 的 OCCT/XCAF 转换交付 12 模块、56 个叶定义、345 个装配实例与 12/23/42 步。六主动/两被动轴的固定躯干教学演示、三级拆装及 Editor 自动回归通过；详细来源、许可、尺寸和限制见 [Bolt 接入记录](Bolt/IMPORT_STATUS.md)。

初次检索核实了原作者/机构发布页、许可证、文件目录及下列四项代表性源文件的匿名访问响应（HTTP 200）。Moveo、Bolt 与 OpenTorque 随后完成本项目接入与自动回归；Solo12 仍未完成整套 CAD 分件/尺寸及引擎验收，不能标为已导入或开箱即用的 Unity 资源包。所有模型的微信真机性能与触摸验收仍待完成。

## 优先候选：免费且许可义务较简单

| 机械 | 官方资源与格式 | 适合开发的拆装/动态内容 | 许可与接入条件 |
| --- | --- | --- | --- |
| [BCN3D Moveo 教学机械臂](https://github.com/BCN3D/BCN3D-Moveo) | 整机与子总成 SolidWorks 装配、零件文件、STL、BOM、装配手册 | 已接入五轴关节与夹爪四连杆教学循环；后续可做抓取任务 | [MIT](https://github.com/BCN3D/BCN3D-Moveo/blob/master/LICENSE)，允许商业使用、修改和分发，保留版权与许可。已完整获取并免费转换为本项目分件资源 |
| [Bolt 六自由度双足机器人](https://github.com/open-dynamic-robot-initiative/open_robot_actuator_hardware/blob/master/mechanics/biped_6dof_v1/README.md) | 完整 STEP 文件、SolidWorks ZIP、分件 STL、BOM、尺寸和装配说明 | 已接两腿六主动轴与被动踝补偿的屈伸教学循环；不是平衡步行 | [BSD-3-Clause](https://github.com/open-dynamic-robot-initiative/open_robot_actuator_hardware/blob/master/LICENSE)，完整版权、条件和免责声明随派生资产保留；已用固定 STEP 免费转换并通过三级自动回归 |
| [Solo12 四足机器人](https://github.com/open-dynamic-robot-initiative/open_robot_actuator_hardware/blob/master/mechanics/quadruped_robot_12dof_v1/README.md) | 整机 SolidWorks ZIP、分件 STL、部分零件 STEP、BOM、腿部和执行器装配资料 | 四腿、十二自由度、皮带传动；关节拆解及原地踏步演示 | 同一硬件仓库的 BSD-3-Clause。整机 SolidWorks 格式需转换；目前未核实完整 Solo12 STEP 总成 |

Moveo 与 Bolt 的已交付教学演示以各自接入记录为准；Solo12 动态内容仍是开发评估，尚需制作关节轴、运动限制与动画。CAD 不等于已经配好游戏动画。

## 免费可用，但需落实衍生模型的许可条件

[OpenTorque 行星减速执行器](https://github.com/G-Levine/OpenTorque-Actuator)提供整机 STEP、齿轮 STEP、独立 STL、BOM 和装配资料。[作者工程页](https://hackaday.io/project/159404-opentorque-actuator)展示了实际制造与运行，并明确设计文件采用 [CC BY-SA 4.0](https://github.com/G-Levine/OpenTorque-Actuator/blob/master/LICENSE)。

- 可免费用于商业项目，也允许修改和分发，无需购买模型。
- 使用时需署名、附许可链接、保留相关说明并注明修改；发布改编模型时，需要遵守相同方式共享条件，不对该模型另加限制许可权利的条款或技术措施。
- 适合讲解太阳轮、行星轮、齿圈、行星架和减速传动；规模较小，适合先做精细拆装与齿轮联动。
- 标准版源文件转换、分件检查、材质与运动绑定已完成，详见 [OpenTorque 接入记录](OpenTorque/IMPORT_STATUS.md)。后续变更仍需先固定 CAD 版本，再对应装配说明；作者工程页部分说明明确针对旧版本，不能与新版混用。

## 可直接访问的源文件与版本记录

这些链接指向官方仓库文件页，可使用 Download raw file 获取。SolidWorks 装配必须连同引用零件一起获取，单个 SLDASM 不构成完整模型包。

| 候选 | 原始文件入口 | GitHub 目录记录的文件大小 |
| --- | --- | --- |
| Moveo | [整机 SLDASM](https://github.com/BCN3D/BCN3D-Moveo/blob/master/CAD%20files/BCN3D%20Moveo%20assembly.SLDASM)；[完整 CAD 目录](https://github.com/BCN3D/BCN3D-Moveo/tree/master/CAD%20files) | 整机装配文件 10,930,688 字节，不含引用零件 |
| Bolt | [整机 STEP](https://github.com/open-dynamic-robot-initiative/open_robot_actuator_hardware/blob/master/mechanics/biped_6dof_v1/cad_files/STEP/biped_6dof_v1.STEP)；[SolidWorks ZIP](https://github.com/open-dynamic-robot-initiative/open_robot_actuator_hardware/blob/master/mechanics/biped_6dof_v1/cad_files/Solidworks/biped_6dof_v1.zip) | STEP 26,548,352 字节；ZIP 37,583,492 字节 |
| Solo12 | [整机 SolidWorks ZIP](https://github.com/open-dynamic-robot-initiative/open_robot_actuator_hardware/blob/master/mechanics/quadruped_robot_12dof_v1/solidworks_files/quadruped_12dof_v1.zip) | 37,989,879 字节 |
| OpenTorque | [整机 STEP](https://github.com/G-Levine/OpenTorque-Actuator/blob/master/STEP/opentorque.step)；[齿轮 STEP](https://github.com/G-Levine/OpenTorque-Actuator/blob/master/STEP/low_backlash_gears.step) | 整机 7,168,329 字节；齿轮 4,724,421 字节 |

本次查询的仓库提交：

- BCN3D/BCN3D-Moveo：`0866a92501277636f76000a195d8a16d44b5b476`。
- open-dynamic-robot-initiative/open_robot_actuator_hardware：`66af1522b4fba0ec4a1d7790e66f5e4652208d30`。
- G-Levine/OpenTorque-Actuator：`412762e9a4ca424564d3ebed882db95ef4b22ed9`。

文件大小与 HTTP 响应只证明公开源文件入口存在，不代表分件完整度或游戏运行质量。以上动态分支链接后续可能变化；正式入库需固定提交，并记录实际下载文件校验值。

## 未进入本轮可直接采用清单的历史线索

- Fraens 蒸汽机、3D-Horse V6/GTF、Annin AR4 等付费资源：移出当前推荐；不再作为本轮采购建议。
- TurboSquid R-11：页面为 Editorial Uses Only，不作为普通可发行游戏资产采用。
- Muncaster 蒸汽机：CC BY-NC-SA，非商业限制不满足本轮筛选条件。
- JPL Open Source Rover：仓库为 Apache-2.0，但最新整机依赖 Onshape 入口，免费导出过程尚未实际核实；本轮不列为已确认可直接取得的完整 CAD。
- Voron 2.4r2：有免费 STEP，但采用 GPL-3.0；模型分发所需的源文件和许可安排尚未完成，暂不列为本轮优先项。这不表示 GPL 资源收费或一概禁止商业使用。
- OM10 机芯、Tomaso V8：保留在 [历史候选与入库门槛](MODEL_CANDIDATES.md)。OM10 的实际 CAD 许可仍待核实；V8 虽标注免费 CC BY，但需账号认证下载，工程尺寸与独立分件未核实，暂不能声称已满足本项目要求。
- Thingiverse Toyota 22RE 及变速箱：当前原始页面的文件与许可尚未核实，不以第三方转载作为可用证明。

## 本项目的建议顺序

1. **Moveo 与 Bolt 已按用户选择接入**：两套免费 CAD 流水线及三档拆装/关节演示自动回归通过；后续安排 Game View 人工复核和微信真机验收。
2. **OpenTorque 已按用户选择接入（2026-09-29）**：标准整机 STEP 实际解析为 13 定义 / 19 实例，110×110×95 mm；5/10/17 步完整覆盖，8∶1 连续齿轮联动、透明观察、拆装及停止后交互自动回归通过。仅接减速机构，不补造缺失的电机/电子件，不混入替代低背隙齿轮；改编模型资产的 CC BY-SA 4.0 许可、署名和修改说明随运行资源保留。详见 [OpenTorque 接入记录](OpenTorque/IMPORT_STATUS.md)。微信分发条款与真机仍待验收。
3. **之后 Solo12**：可以复用 Bolt 腿部/执行器的内容和 STEP 构建经验，但已取得的整机入口为 SolidWorks ZIP，不应假定 Moveo 保存网格解析器可直接兼容；转换路线、动画、线缆和材质仍有较大工作量。

其它候选正式接入仍需完成分件与尺寸审计、运行网格和材质、中文 BOM、三档拆装分组、运转演示及授权说明；Moveo、Bolt、OpenTorque 已完成本机样片接入，仍需用户实际 Game View 和微信平台验收、按需加载及真机优化。
