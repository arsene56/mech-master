"""Bolt's Chinese service groups, derived from the pinned STEP assembly."""

import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
MODEL_ID = "robot.odri.bolt.6dof.v1"
ASSEMBLIES = {
    "body_frame": ("躯干框架", "托住左右腿和控制板。", "框架把两腿的载荷传到躯干，轴承支承髋部。", "框架尺寸和关节间距来自官方装配，观察缩放不改变比例。"),
    "electronics": ("控制与感知", "告诉电机怎样动，并感知身体姿态。", "主控板发送指令，驱动板控制电流，惯性传感器测量姿态。", "控制板和焊接元件按整板处理，源 CAD 未包含完整线束。"),
}
for side, label in (("left", "左"), ("right", "右")):
    ASSEMBLIES.update({
        f"{side}_hip_aa": (f"{label}髋侧摆", f"让{label}腿向内或向外摆。", "电机经过两级同步带减速，把转动传给髋部。", "侧摆与屈伸属于不同转轴；演示角度是教学范围。"),
        f"{side}_hip_fe": (f"{label}髋屈伸", f"让{label}腿向前抬起或向后摆。", "带轮输出转矩，上腿绕髋部轴承转动。", "传动比来自带轮齿数，不由预览画面推算。"),
        f"{side}_thigh": (f"{label}上腿与膝驱动", "连接髋部和膝部，并驱动小腿。", "上腿中的电机与同步带把转矩传到膝关节。", "髋与膝轴心间距为二百毫米，内部驱动件随上腿运动。"),
        f"{side}_shin": (f"{label}小腿与踝轴", "连接膝部和脚掌。", "小腿传递支承力，底端的钢轴允许脚掌被动摆动。", "膝与踝轴心间距为二百毫米；踝部没有独立驱动电机。"),
        f"{side}_foot": (f"{label}脚掌", "与地面接触，帮助支承身体。", "细长脚掌和硅胶接触件提供缓冲，脚掌绕踝轴摆动。", "演示固定躯干并展示关节动作，不计算地面接触或平衡。"),
    })

TYPE_NAMES = {
    "body_structure_biped_front": "躯干前板", "body_structure_biped_bottom": "躯干底板",
    "body_structure_biped_back_rev_a": "躯干后板", "body_structure_biped_left_side": "躯干左侧板",
    "body_structure_biped_right_side": "躯干右侧板", "body_structure_biped_top": "躯干顶板",
    "body_structure_master_board_protection": "主控板保护架", "vicon_marker_10mm": "运动捕捉标记球",
    "micro_driver_90_deg_hirose": "电机驱动板", "spacer_micro_driver_stack": "驱动板间隔柱",
    "master_board": "主控制板", "imu_3dm_cx5_25": "惯性测量模块",
    "motor_antigravity_4004_stator": "无刷电机定子", "motor_antigravity_4004_rotor": "无刷电机转子",
    "motor_antigravity_4004_custom_shaft": "电机定制轴", "motor_antigravity_4004_brass_spacer": "电机黄铜间隔套",
    "encoder_codewheel_pwb_mount": "编码盘安装座", "encoder_codewheel_5000cpr_pwb_7mm": "光学编码盘",
    "encoder_avago_aedt_9810": "光学编码器读头",
    "transmission_pulley_at3_t10_motor": "十齿电机带轮", "transmission_pulley_at3_t30_output": "三十齿输出带轮",
    "transmission_pulley_at3_t10_center": "十齿中间带轮", "transmission_pulley_at3_t30_center": "三十齿中间带轮",
    "transmission_timing_belt_at3_150_4": "一级同步带", "transmission_timing_belt_at3_201_6": "二级同步带",
    "transmission_belt_tensioner_roller": "同步带张紧滚轮",
    "_hip_aa_structure_left_side_bl_biped": "左髋侧摆壳体", "_hip_aa_structure_right_side_bl_biped": "右髋侧摆壳体",
    "_hip_fe_structure_left_side_bl_biped": "左髋屈伸壳体", "_hip_fe_structure_right_side_bl_biped": "右髋屈伸壳体",
    "_upper_leg_200mm": "二百毫米上腿壳体", "lower_leg_structure": "二百毫米小腿结构",
    "pin_5mm_28mm": "五毫米踝部钢轴", "foot": "脚掌支架", "foot_silicone_tube_elements": "脚掌硅胶接触件",
}
FASTENERS = re.compile(r"^fasteners?_")
ROLE_NAMES = {"whole": "", "structure": "承载结构", "covers": "前后盖板", "markers": "标记球组",
              "mainboard": "主控板组件", "drivers": "驱动板组", "imu": "惯性模块",
              "housing": "壳体与连接件", "drive": "驱动系统", "motor": "电机与输入组件",
              "reduction": "同步带减速组件", "output": "输出带轮与支承", "feedback": "位置反馈组件",
              "ankle": "被动踝轴组件", "foot": "脚掌与缓冲件"}
KNOWLEDGE = {
    "motor": ("把电能变成旋转运动。", "无刷电机的定子磁场推动转子，轴带动输入带轮。", "电机内部几何保留，拆装时按电机与输入组件整体操作。"),
    "reduction": ("把快速转动变成更慢、更有力的转动。", "两级十齿到三十齿带轮组成九比一减速传动。", "同步带和张紧组件成组操作；当前演示不计算带的弹性。"),
    "output": ("把驱动力传给下一段腿。", "输出带轮传递转矩，配对轴承支承旋转。", "密封轴承按完整维修单元处理，不逐颗拆内部滚动体。"),
    "feedback": ("告诉控制系统电机转到了哪里。", "编码盘与光学读头配合，把旋转变成位置反馈。", "编码器按组件处理；教学演示没有接入真实电机控制器。"),
    "ankle": ("让脚掌能顺着腿部姿态摆动。", "脚掌绕钢轴被动转动，接触件提供缓冲。", "踝部没有电机；演示姿态是示意，不是接触动力学计算。"),
    "markers": ("帮助外部设备看见机器人的位置。", "运动捕捉相机识别标记球，用来估计躯干姿态。", "标记球是选装测量部件，本模型保留官方装配中的实例。"),
    "mainboard": ("把动作指令送给各个驱动板。", "主控制板连接各关节驱动器，协调机器人动作。", "焊接电子件随主控板整体处理，不能逐个拆装。"),
    "drivers": ("控制电机中的电流。", "三块驱动板分别连接成对的电机，驱动六个主动关节。", "驱动板与间隔柱成组操作，当前 CAD 未建出完整导线。"),
    "imu": ("感知身体转动和加速度。", "惯性测量模块提供姿态估计需要的角速度与加速度。", "惯性模块不直接保证平衡，真实机器人还需要控制算法。"),
}


def type_name(name):
    if name in TYPE_NAMES:
        return TYPE_NAMES[name]
    if name.startswith("bearing_"):
        numbers = name.split("_")[1:4]
        return "×".join(numbers) + " 毫米轴承（外径×内径×宽度）"
    if FASTENERS.match(name):
        label = name.removeprefix("fasteners_").removeprefix("fastener_")
        for old, new in (("shcs", "内六角螺钉"), ("shs", "开槽螺钉"), ("fhs", "沉头螺钉"),
                         ("locknut", "锁紧螺母"), ("washer", "垫圈"), ("plastic", "塑料")):
            label = label.replace(old, new)
        return label.replace("_", " ").upper()
    raise ValueError("Missing Chinese name: " + name)


def assembly(instance, name):
    root = instance["ancestry"][1]
    if root == "_body_structure_biped":
        return "electronics" if ("micro_driver_stack" in instance["ancestry"] or
            name in ("master_board", "imu_3dm_cx5_25", "fastener_shcs_m2_5", "fastener_shcs_m2_20",
                     "fastener_shs_m2.5_12", "body_structure_master_board_protection")) else "body_frame"
    side = "left" if instance["matrix"][0][3] < 0 else "right"
    if root.startswith("_hip_aa_"): return side + "_hip_aa"
    if root.startswith("_hip_fe_"): return side + "_hip_fe"
    if root == "_upper_leg_200mm": return side + "_thigh"
    if root == "_lower_leg_200mm": return side + "_shin"
    if root == "_foot": return side + "_foot"
    raise ValueError("Unclassified source assembly: " + root)


def role(part):
    name, group, ancestors = part["sourceName"], part["assemblyId"], part["ancestry"]
    if group == "body_frame":
        if name == "vicon_marker_10mm": return "markers"
        return "covers" if name in ("body_structure_biped_front", "body_structure_biped_back_rev_a") else "structure"
    if group == "electronics":
        if "micro_driver_stack" in ancestors: return "drivers"
        return "imu" if name in ("imu_3dm_cx5_25", "fastener_shs_m2.5_12") else "mainboard"
    if group.endswith("_foot"): return "foot"
    if group.endswith("_shin"):
        return "ankle" if name == "pin_5mm_28mm" or "fastener_m2.5_20_assembly" in ancestors else "structure"
    if name.startswith("motor_antigravity_") or "motor_module" in ancestors: return "motor"
    if "transmission_output_pulley" in ancestors: return "output"
    if "encoder" in ancestors: return "feedback"
    if name.startswith("transmission_") or "transmission_center_pulley_at3_t10_t30" in ancestors: return "reduction"
    return "housing"


def standard_role(group, role_name):
    if group == "body_frame": return "markers" if role_name == "markers" else "structure"
    if group == "electronics" or group.endswith(("_shin", "_foot")): return role_name
    return "housing" if role_name == "housing" else "drive"


def write_content(parts):
    plans = []
    for level in ("Simple", "Standard", "Advanced"):
        groups = {}
        for part in parts:
            part_role = part["role"]
            key = "whole" if level == "Simple" else standard_role(part["assemblyId"], part_role) if level == "Standard" else part_role
            groups.setdefault((part["assemblyId"], key), []).append(part)
        steps = []
        for group in ASSEMBLIES:
            for (assembly_id, key), members in groups.items():
                if assembly_id != group: continue
                label, *texts = ASSEMBLIES[group]
                texts = KNOWLEDGE.get(key, texts)
                if any(not text or len(text) > 50 for text in texts):
                    raise ValueError("Invalid short narration")
                steps.append({"id": f"bolt.{level.lower()}.{group}.{key}",
                              "displayName": label + ("·" + ROLE_NAMES[key] if key != "whole" else ""),
                              "tool": "Hand", "simpleSummary": texts[0], "mechanism": texts[1], "advancedNote": texts[2],
                              "assemblyId": group, "componentId": key, "objectNames": [part["object"] for part in members]})
        names = [name for step in steps for name in step["objectNames"]]
        if len(names) != len(set(names)) or set(names) != {part["object"] for part in parts}:
            raise ValueError("Incomplete tier coverage")
        plans.append({"difficulty": level, "steps": steps})
    target = ROOT / "Assets/Resources/MechanicalCatalog"
    catalog = {"schemaVersion": 1, "moduleId": MODEL_ID, "plans": plans}
    (target / "BoltInteractionCatalog.json").write_text(json.dumps(catalog, ensure_ascii=False, indent=2), encoding="utf-8")
    model = {"schemaVersion": 1, "id": MODEL_ID, "displayName": "Bolt 双足机器人", "displayOrder": 20,
             "catalogResourcePath": "MechanicalCatalog/BoltInteractionCatalog",
             "moduleResourcePaths": [f"Models/Bolt/Modules/{key}_LOD0" for key in ASSEMBLIES],
             "assemblies": [{"id": key, "displayName": value[0]} for key, value in ASSEMBLIES.items()],
             "motion": {"kind": "bolt-articulation-v1", "rigResourcePath": "MechanicalCatalog/BoltMotionRig"}}
    (target / "Models/Bolt.json").write_text(json.dumps(model, ensure_ascii=False, indent=2), encoding="utf-8")
    return {plan["difficulty"]: len(plan["steps"]) for plan in plans}
