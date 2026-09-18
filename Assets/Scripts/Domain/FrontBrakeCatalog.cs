using System.Collections.Generic;

namespace MechMaster.Domain
{
    public static class FrontBrakeCatalog
    {
        public static DisassemblyPlan CreatePlan(DifficultyLevel difficulty)
        {
            switch (difficulty)
            {
                case DifficultyLevel.Simple:
                    return new DisassemblyPlan(difficulty, CreateSimpleSteps());
                case DifficultyLevel.Standard:
                    return new DisassemblyPlan(difficulty, CreateStandardSteps());
                default:
                    return new DisassemblyPlan(difficulty, CreateAdvancedSteps());
            }
        }

        private static IEnumerable<PartDefinition> CreateSimpleSteps()
        {
            yield return Part("front_thru_axle", "前轮筒轴", ToolKind.HexKey,
                "它穿过前叉和花鼓，把前轮固定在车架上。",
                "筒轴把左右叉脚连接成一个更稳固的结构。",
                "样片按 15 mm 山地车筒轴的真实结构比例制作。");
            yield return Part("front_wheel", "前轮总成", ToolKind.Hand,
                "前轮负责滚动、转向并支撑车身。",
                "轮胎、轮圈、辐条和花鼓共同把地面的力传给前叉。",
                "辐条按真实数量显示，但在本关作为一个轮组操作。");
            yield return Part("front_brake_assembly", "前刹车总成", ToolKind.HexKey,
                "它夹紧碟片，让前轮慢下来。",
                "手捏刹把后，液压推动卡钳里的活塞和来令片夹住碟片。",
                "总成包含卡钳、来令片、固定件和油管接口。");
            yield return Part("front_rotor_assembly", "前刹车碟片总成", ToolKind.TorxKey,
                "碟片跟随车轮旋转，被来令片夹住后产生制动力。",
                "摩擦把车轮的运动能量转化为热量。",
                "六颗固定螺栓按组操作，真实维修应按厂商要求交叉紧固。");
        }

        private static IEnumerable<PartDefinition> CreateStandardSteps()
        {
            yield return Part("front_thru_axle", "前轮筒轴", ToolKind.HexKey,
                "它把前轮固定在前叉上。",
                "筒轴穿过封闭式叉脚和花鼓轴承中心，提高横向刚度。",
                "拆轴前需要先让车辆稳定支撑。");
            yield return Part("front_wheel", "前轮总成", ToolKind.Hand,
                "前轮负责滚动、转向并支撑车身。",
                "轮胎、轮圈、辐条和花鼓共同传递载荷。",
                "取下车轮后不要随意捏刹把，否则活塞可能过度伸出。");
            yield return Part("pad_retaining_pin", "来令片固定销", ToolKind.HexKey,
                "固定销防止来令片从卡钳中掉出。",
                "固定销穿过来令片耳部，并由防松结构保持位置。",
                "真实型号可能使用螺纹销或插销，结构以厂商手册为准。");
            yield return Part("brake_pads_group", "来令片与弹簧组", ToolKind.Hand,
                "来令片摩擦碟片，弹簧帮助它们回位。",
                "摩擦材料贴在金属背板上，两片之间的弹簧保持间隙。",
                "来令片属于磨损件，沾油会明显降低制动力。");
            yield return Part("caliper_mount_bolts", "卡钳固定螺栓组", ToolKind.HexKey,
                "它把刹车卡钳固定在前叉上。",
                "两颗螺栓共同保持卡钳位置，使来令片分布在碟片两侧。",
                "安装位置会影响碟片是否蹭擦来令片。");
            yield return Part("front_caliper", "前刹车卡钳", ToolKind.Hand,
                "卡钳推动来令片夹住碟片。",
                "液压让两侧活塞向中间移动并放大手指的控制力。",
                "卡钳壳体通常使用轻质铝合金。");
            yield return Part("rotor_bolts_group", "碟片固定螺栓组", ToolKind.TorxKey,
                "六颗螺栓把碟片固定在花鼓上。",
                "均匀分布的螺栓把制动力矩传给花鼓。",
                "游戏按组操作；真实安装通常需要交叉、分次紧固。");
            yield return Part("front_rotor", "前刹车碟片", ToolKind.Hand,
                "碟片为来令片提供摩擦表面。",
                "较大的直径能用更小的夹紧力产生相同制动力矩。",
                "碟片常用不锈钢，表面应避免油污和弯折。");
        }

        private static IEnumerable<PartDefinition> CreateAdvancedSteps()
        {
            yield return Part("front_thru_axle", "前轮筒轴", ToolKind.HexKey,
                "它把花鼓锁在前叉之间。",
                "较大的中空轴能兼顾强度、刚度和重量。",
                "轴与叉脚螺纹必须保持清洁，真实维修由成人完成。");
            yield return Part("front_wheel", "前轮总成", ToolKind.Hand,
                "前轮承担滚动、转向和部分制动载荷。",
                "辐条张力把轮圈载荷传到中心花鼓。",
                "重复辐条完整显示，但按轮组统一操作。");
            yield return Part("pad_retaining_clip", "固定销保险卡", ToolKind.Hand,
                "保险卡防止固定销意外退出。",
                "小型弹性卡扣利用自身弹力锁住固定销。",
                "它很小但关系安全，拆下后要单独收好。");
            yield return Part("pad_retaining_pin", "来令片固定销", ToolKind.HexKey,
                "固定销把来令片保持在卡钳中。",
                "固定销穿过两片来令片和中间弹簧。",
                "不同产品可能使用螺纹销或无螺纹插销。");
            yield return Part("pad_spring", "来令片回位弹簧", ToolKind.Hand,
                "弹簧让两片来令片保持适当间隙。",
                "弹性金属片在松开刹车时帮助来令片离开碟片。",
                "弹簧变形会造成异响或持续蹭碟。");
            yield return Part("left_brake_pad", "左侧来令片", ToolKind.Hand,
                "它从左侧摩擦碟片。",
                "活塞推动背板，摩擦层接触旋转的碟片。",
                "摩擦材料会磨损，厚度不足时需要成对更换。");
            yield return Part("right_brake_pad", "右侧来令片", ToolKind.Hand,
                "它从右侧摩擦碟片。",
                "左右来令片共同夹紧才能让受力均匀。",
                "更换时应避免用手触摸摩擦面。");
            yield return Part("caliper_mount_bolts", "卡钳固定螺栓组", ToolKind.HexKey,
                "它把卡钳固定在前叉上。",
                "安装孔允许小范围校正，让卡钳中心对准碟片。",
                "游戏不要求输入工具规格或扭矩。");
            yield return Part("front_caliper", "双活塞卡钳壳体", ToolKind.Hand,
                "壳体容纳活塞和刹车油通道。",
                "密闭油路把刹把产生的压力传递给两侧活塞。",
                "本样片展示壳体结构，刹车油模拟将在后续版本加入。");
            yield return Part("rotor_bolts_group", "六颗碟片固定螺栓", ToolKind.TorxKey,
                "它们把碟片固定在花鼓法兰上。",
                "六点连接均匀传递制动力矩并限制碟片位移。",
                "游戏将六颗螺栓作为一组，视觉上保留真实数量。");
            yield return Part("front_rotor", "前刹车碟片", ToolKind.Hand,
                "碟片与来令片摩擦以降低车速。",
                "开孔帮助减重、排水和清理摩擦表面。",
                "连续制动会升温，真实操作时不能触摸刚使用过的碟片。");
            yield return Part("hub_end_caps", "花鼓端盖组", ToolKind.Hand,
                "端盖保护轴承并帮助花鼓定位。",
                "左右端盖与花鼓轴配合，为叉脚提供正确支撑间距。",
                "端盖属于配对小件，视觉分开显示、操作时按组处理。");
        }

        private static PartDefinition Part(
            string id,
            string name,
            ToolKind tool,
            string summary,
            string mechanism,
            string advanced)
        {
            return new PartDefinition(id, name, tool, summary, mechanism, advanced);
        }
    }
}

