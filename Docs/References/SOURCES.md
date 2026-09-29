# 资料来源与内容校验

## 使用原则

首版目前没有合作机械专家。零件名称、结构关系和安全提示以厂商公开维修手册及通用自行车维修规范为依据；任何具体车型的差异都应在内容中注明“以厂商手册为准”。

游戏中的科普内容不能替代真实维修指导。涉及制动器、刹车油、碟片紧固和道路安全的操作，应由监护人或专业技师完成。

## 平台与引擎

- [团结引擎用户手册](https://docs.unity.cn/cn/tuanjiemanual/Manual/)
- [团结引擎微信平台支持](https://docs.unity.cn/cn/tuanjiemanual/Manual/Wechat.html)
- [团结引擎部署微信小游戏](https://docs.unity.cn/cn/tuanjiemanual/1.9/Manual/UploadWeixinMiniGame.html)
- [Unity Manual](https://docs.unity3d.com/Manual/index.html)
- [Blender Manual](https://docs.blender.org/manual/en/latest/)

## 自行车维修资料

- [Shimano Technical Documents](https://si.shimano.com/)
- [Shimano BR-MT200 / SM-BH59 / SM-RT26 爆炸图](https://si.shimano.com/en/pdfs/ev/BR-MT200-4349/EV-BR-MT200-4349D.pdf)
- [Shimano 液压碟刹经销商手册 DM-2VF0A](https://si.shimano.com/en/pdfs/dm/2VF0A/DM-2VF0A-01-ENG.pdf)：油管长度需保留车把转向余量；本样片外走线按自身车架几何布置，不声明复刻该手册某一车型。
- [Selle Italia Novus Boost Evo Ti 316 Superflow](https://www.selleitalia.com/novus-boost-evo-ti-316-superflow/)：仅参考宽后部、窄鼻部和中央减压结构的形态。游戏坐垫为自建 274 × 144 mm 通用曲面，非该产品的尺寸复制，未使用其网格或贴图。
- [SRAM Service](https://www.sram.com/en/service)
- [SRAM Boost 110/148 花鼓尺寸图](https://www.sram.com/globalassets/document-hierarchy/frame-fit-specifications/mtb/gen.0000000005566-2018-rev-a-mtb-road-hubs-build-specifications.pdf)
- [SRAM Guide RE 液压卡钳维修手册](https://www.sram.com/globalassets/document-hierarchy/service-manuals/sram-mtb/brakes/gen.0000000005128-rev-a-guide-re-service-manual-english)
- [Schwalbe 57-584 轮胎规格](https://www.schwalbe.com/en/tube-search)
- [Park Tool Repair Help](https://www.parktool.com/en-int/blog/repair-help)
- ISO 4210 自行车安全要求：只用于核对安全术语和设计边界，标准正文需通过合法渠道取得。

## Moveo 模型与转换来源

- [BCN3D Moveo 官方仓库](https://github.com/BCN3D/BCN3D-Moveo/tree/0866a92501277636f76000a195d8a16d44b5b476)：采用固定提交的 CAD 总装保存网格、装配变换、BOM 和手册；[MIT 许可](https://github.com/BCN3D/BCN3D-Moveo/blob/0866a92501277636f76000a195d8a16d44b5b476/LICENSE)随运行资产保存。
- [Wintaru/model_viewer 的三角条带解析研究](https://github.com/Wintaru/model_viewer/blob/9aabcab92cfaaac03b1276229e11ce798b60e6f9/research/d9-decode.py)：解析器参考其 MIT 代码，版权与许可保存在 `Tools/Content/ThirdParty/Wintaru-MIT-LICENSE.txt`。
- [Moveo 接入记录](Moveo/IMPORT_STATUS.md)：原始文件校验、隐藏和重复引用清理、米制尺寸、流水线与验证边界。运行模型为源 CAD 的保存网格快照；展示 BOM 保留源版本差异，不能作为采购清单。

## OpenTorque 模型与转换来源

- [OpenTorque 官方固定版本](https://github.com/G-Levine/OpenTorque-Actuator/tree/412762e9a4ca424564d3ebed882db95ef4b22ed9)：作者 Gabrael Levine；标准整机 STEP、替代低背隙 STEP、采购 BOM 与打印说明。首版仅接标准减速机构，不补造源缺失的电机和电子件。
- [原始 CC BY-SA 4.0 许可](https://github.com/G-Levine/OpenTorque-Actuator/blob/412762e9a4ca424564d3ebed882db95ef4b22ed9/LICENSE)：许可全文、固定来源与模型改编署名随运行资源保存。派生模型/Source/预览按相同许可分享，独立游戏代码单独管理许可；发行前核对平台的分发限制。
- [作者项目与 8∶1 参数](https://hackaday.io/project/159404-opentorque-actuator)：参数与本版 CAD 齿数核对一致；旧版装配教程和性能数据不等于本游戏完成的验证。
- [OpenTorque 接入记录](OpenTorque/IMPORT_STATUS.md)：源校验、13 定义/19 实例、5/10/17 步、材质/LOD、连续运动、复现命令与验证边界。

## 自行车样片假设

- 27.5 英寸山地车轮组，模型外径约 0.698 m。
- 轴距 1.120 m。
- 前轮采用 180 mm、六钉式碟片。
- 前制动器用双活塞液压卡钳表达工作原理。
- 传动系统视觉上采用 2×10 配置。
- 游戏不声明对应某一具体品牌或型号。
- 工具类别仅作为 BOM 科普元数据，不限制拖动拆装，也不呈现规格和扭矩。

## 内容审核记录要求

每个正式发布模块应记录模块名称和版本、参考厂商与手册编号、手册版本、采用的顺序、差异说明、复核人、复核日期和全部资产许可证。

在未完成上述记录前，内容只能视为研发样片，不能宣称为特定产品的维修教程。
