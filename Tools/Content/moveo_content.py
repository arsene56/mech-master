"""Chinese Moveo content and grouping rules shared by the asset pipeline."""

import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
MODEL_ID = "arm.bcn3d.moveo.v1"
ASSEMBLIES = {
    "base_platform": ("安装底座", "木质底板和支脚托住整台机械臂。", "宽底板分散载荷，让机械臂保持稳定。", "机械臂伸出时会产生倾覆力矩，底座需要可靠固定。"),
    "rotary_base": ("回转机构", "让机械臂绕底座转向。", "电机驱动回转件，轴承和滚轮支承转动。", "支承件分担载荷，驱动电机负责控制角度。"),
    "shoulder": ("肩关节", "抬起或放下上臂。", "两台步进电机通过同步传动驱动肩关节。", "同步带张力和关节支承都会影响运动稳定性。"),
    "upper_arm": ("上臂", "连接肩部和肘部，形成机械臂的一段。", "两片壳体连接成承载结构，关节轴允许相邻部件转动。", "壳体、连接轴和紧固件一起传递机械臂的载荷。"),
    "elbow": ("肘关节", "让机械臂在中间位置弯曲。", "带减速机构的步进电机驱动肘部转动。", "减速传动增加输出力矩，也会带来传动间隙。"),
    "forearm": ("前臂", "连接肘部和腕部，伸向要抓取的物体。", "壳体包住电机和连接轴，联轴器传递旋转。", "同轴连接可以减少轴和电机承受的额外弯曲载荷。"),
    "wrist": ("腕关节", "调整夹爪朝向。", "小步进电机、带轮和支承轴组成腕部传动。", "腕部质量会增加前方关节的负担，应尽量轻且刚。"),
    "gripper": ("夹爪", "用两根手指夹住或放开物体。", "舵机带动齿轮和连杆，使两侧手指配合运动。", "齿轮与连杆的装配角度决定夹爪的开合范围。"),
    "controller": ("驱动电控", "接收控制信号，驱动电机并散热。", "驱动板把控制脉冲变成电机绕组中的电流。", "焊接电子件作为整板处理，不能当作独立拆装件。"),
}

TYPE_NAMES = {
    "1M2A": "回转盘", "1M1B": "肩部支架", "1M3A": "底座电机支架",
    "2M1D": "肩关节壳体", "2M2HA": "上臂外壳 A", "2M2MA": "上臂外壳 B",
    "3M1D": "肘关节壳体", "3M2C": "前臂外壳 A", "3M2CC": "前臂外壳 B",
    "4M1D": "腕关节壳体", "4M2B": "夹爪安装架", "4M2CB": "夹爪安装盖",
    "T2M1BI": "肩部左张紧器", "T2M1BD": "肩部右张紧器", "T3M1C": "肘部张紧器",
    "T4M1E": "腕部张紧器", "TBC": "腕部传动连接件",
    "Tapa 2M1C": "肩部关节盖", "Tapa 3M1C": "肘部关节盖", "Tapa 4M1D": "腕部关节盖",
    "Bearing 5mm": "5 毫米内孔轴承", "8mm Bearing": "8 毫米内孔轴承",
    "Bearing 4Bore 13Ext 5": "4 毫米内孔轴承", "Bearing 3Bore 10Ext 4Height": "3 毫米内孔轴承",
    "Spacer M5x10": "M5 间隔套", "Spacer M8x20": "M8 间隔套", "Arandela": "张紧器垫圈",
    "Pulley 8mm IGNIS": "8 毫米孔带轮", "Pulley M5": "腕部带轮",
    "Coupling 5 to 8 mm": "5 转 8 毫米联轴器", "Nema 23": "NEMA 23 步进电机",
    "Nema 17 Reductor": "NEMA 17 减速步进电机", "Nema 17 Curt": "NEMA 17 短步进电机",
    "Nema 17 Llarg": "NEMA 17 长步进电机", "Nema 14": "NEMA 14 步进电机",
    "Bottom Plate C": "夹爪下支板", "Top Plate C": "夹爪上支板", "Cilinder": "夹爪连接柱",
    "Gripper Left B": "左夹指", "Gripper Right B": "右夹指", "Idol Gear B": "从动齿轮",
    "Servo Gear B": "舵机齿轮", "Pivot Arm B": "夹爪连杆", "Servo Futaba S3003": "S3003 舵机与引线",
    "Base fusta": "木质安装板", "Potes base B": "底座支脚", "Suport Drivers E": "驱动板支架",
    "PCB TB6560": "TB6560 驱动板", "Capacitor 1": "驱动板电容 A", "Capacitor 2": "驱动板电容 B",
    "Chip": "驱动芯片", "Sink": "散热片", "Terminal": "接线端子",
    "Box C": "电控盒", "Fan Module": "风扇安装架", "Fan": "散热风扇",
}

FASTENERS = re.compile(r"^(M\d|Brass insert|Volandera)")
ROLE_NAMES = {
    "body": "壳体与支架", "plate": "安装板", "feet": "支脚", "support": "转盘支承",
    "motor": "驱动电机", "tensioner": "带张紧机构", "transmission": "传动轴与带轮",
    "covers": "关节盖", "fasteners": "紧固件组", "brackets": "末端安装架",
    "fingers": "左右夹指", "gears": "开合齿轮", "linkage": "连接柱与连杆",
    "servo": "舵机与引线", "frames": "夹爪支板", "boards": "驱动板组件", "fan": "散热组件",
    "frame": "承载结构", "drive": "驱动与传动", "housing": "外壳组件",
}
ROLE_KNOWLEDGE = {
    "fasteners": ("把相邻部件牢牢连接在一起。", "螺钉、螺母和垫圈配合，将连接处压紧。", "同一机构的重复紧固件成组操作，避免逐颗重复拖动。"),
    "motor": ("把电能变成机械臂的转动。", "步进电机按控制脉冲逐步转动，驱动关节。", "电机按整机维修单元处理，不拆开绕组和密封内部件。"),
    "servo": ("让夹爪按指定角度开合。", "舵机用位置反馈控制输出轴角度，带动夹爪齿轮。", "舵机壳体、内部件和引线作为同一个维修单元处理。"),
    "boards": ("控制电机绕组中的电流。", "驱动芯片、接线端子和电容共同构成驱动板。", "焊接元件保留真实几何，并随整块驱动板一起拆装。"),
    "fan": ("把电控盒内的热量带走。", "风扇推动空气经过散热片，帮助电机驱动板降温。", "风扇及安装架整体处理；驱动板的散热片随板保留。"),
    "support": ("托住回转盘，让它转动更平稳。", "滚轮和轴承支承转盘，减少滑动摩擦。", "密封轴承保持整体，不把内部钢球当作独立维修步骤。"),
    "tensioner": ("让传动带保持合适的松紧。", "张紧器改变支承位置，调整同步带的张力。", "这份 CAD 未建出完整带体，不能把显示网格当作带长依据。"),
}


def type_name(name):
    if name in TYPE_NAMES:
        return TYPE_NAMES[name]
    if name.startswith("Smooth bar") or name.startswith("Barra llisa"):
        return "8 毫米光轴（源标注 " + name.split(" x ")[-1].replace("mm", "") + " 毫米）"
    if name.startswith("Brass insert"):
        return name.split()[-1] + " 黄铜螺纹嵌件"
    if name.startswith("Volandera"):
        return name.split()[-1] + " 垫圈"
    if name.startswith("M") and name[1].isdigit():
        return name.replace(" Locknut", " 自锁螺母").replace(" Lock", " 自锁螺母").replace(" Autoblocant", " 自锁螺母").replace(" Nut", " 螺母") + (" 螺钉" if "x" in name else "")
    raise ValueError(f"Missing Chinese part name: {name}")


def assembly_from_ancestry(names):
    first = names[0]
    if first == "Base assembly":
        return "base_platform" if names[-1] in ("Base fusta", "Potes base B") else "controller"
    if first in ("Rotary Plate", "1M3"):
        return "rotary_base"
    if first in ("2M1 assembly", "1M1B", "Tapa 2M1C"):
        return "shoulder"
    if first == "2M2": return "upper_arm"
    if first in ("3M1", "Tapa 3M1C"): return "elbow"
    if first == "3M2": return "forearm"
    if first == "4M":
        return "gripper" if "Gripper B Assembly" in names else "wrist"
    return None


def role(assembly, name):
    if FASTENERS.match(name): return "fasteners"
    if name.startswith("Nema"): return "motor"
    if name.startswith("Tapa"): return "covers"
    if assembly == "base_platform": return "plate" if name == "Base fusta" else "feet"
    if assembly == "rotary_base":
        return "support" if "Bearing" in name or name.startswith("Spacer") else "body"
    if "Bearing" in name or name.startswith(("Smooth bar", "Barra llisa", "Spacer", "Pulley", "Coupling")) or name == "TBC":
        return "transmission"
    if name.startswith(("T2", "T3", "T4")) or name == "Arandela": return "tensioner"
    if assembly == "wrist" and name in ("4M2B", "4M2CB"): return "brackets"
    if assembly == "gripper":
        if name.startswith("Gripper"): return "fingers"
        if "Gear" in name: return "gears"
        if name in ("Cilinder", "Pivot Arm B"): return "linkage"
        if name.startswith("Servo Futaba"): return "servo"
        return "frames"
    if assembly == "controller":
        if name in ("PCB TB6560", "Capacitor 1", "Capacitor 2", "Chip", "Sink", "Terminal"): return "boards"
        if name in ("Fan", "Fan Module"): return "fan"
    return "body"


def standard_role(assembly, part_role):
    if assembly == "base_platform": return "frame"
    if assembly == "rotary_base": return "support" if part_role in ("body", "support") else "drive"
    if assembly in ("shoulder", "elbow"):
        return "motor" if part_role == "motor" else "drive" if part_role in ("transmission", "tensioner") else "housing"
    if assembly == "upper_arm": return "frame"
    if assembly in ("forearm", "wrist"):
        return "drive" if part_role in ("motor", "transmission", "tensioner") else "housing"
    if assembly == "gripper":
        return "servo" if part_role == "servo" else "drive" if part_role in ("fingers", "gears", "linkage") else "frames"
    return part_role if part_role in ("boards", "fan") else "housing"


def generate_catalog(parts):
    plans = []
    for difficulty in ("Simple", "Standard", "Advanced"):
        groups = {}
        for part in parts:
            grouping = "whole" if difficulty == "Simple" else standard_role(part["assemblyId"], part["role"]) if difficulty == "Standard" else part["role"]
            groups.setdefault((part["assemblyId"], grouping), []).append(part)
        steps = []
        for assembly in ASSEMBLIES:
            for (group_assembly, grouping), members in groups.items():
                if group_assembly != assembly: continue
                label, *knowledge = ASSEMBLIES[assembly]
                if grouping in ROLE_KNOWLEDGE:
                    knowledge = ROLE_KNOWLEDGE[grouping]
                display = label if grouping == "whole" else label + "·" + ROLE_NAMES[grouping]
                if any(len(text) > 50 for text in knowledge):
                    raise ValueError("Narration exceeds 50 characters")
                steps.append({"id": f"moveo.{difficulty.lower()}.{assembly}.{grouping}",
                              "displayName": display, "tool": "Hand",
                              "simpleSummary": knowledge[0], "mechanism": knowledge[1], "advancedNote": knowledge[2],
                              "assemblyId": assembly, "componentId": grouping,
                              "objectNames": [part["object"] for part in members]})
        names = [name for step in steps for name in step["objectNames"]]
        if len(names) != len(set(names)) or set(names) != {part["object"] for part in parts}:
            raise ValueError("Incomplete or duplicate tier coverage")
        plans.append({"difficulty": difficulty, "steps": steps})
    return {"schemaVersion": 1, "moduleId": MODEL_ID, "plans": plans}


def write_runtime_content(parts):
    catalog = generate_catalog(parts)
    target = ROOT / "Assets/Resources/MechanicalCatalog"
    target.mkdir(parents=True, exist_ok=True)
    (target / "MoveoInteractionCatalog.json").write_text(json.dumps(catalog, indent=2, ensure_ascii=False), encoding="utf-8")
    model = {"schemaVersion": 1, "id": MODEL_ID, "displayName": "Moveo 机械臂", "displayOrder": 10,
             "catalogResourcePath": "MechanicalCatalog/MoveoInteractionCatalog",
             "moduleResourcePaths": [f"Models/Moveo/Modules/{assembly}_LOD0" for assembly in ASSEMBLIES],
             "assemblies": [{"id": key, "displayName": value[0]} for key, value in ASSEMBLIES.items()],
             "motion": {"kind": "moveo-articulation-v1", "rigResourcePath": "MechanicalCatalog/MoveoMotionRig"}}
    (target / "Models/Moveo.json").write_text(json.dumps(model, indent=2, ensure_ascii=False), encoding="utf-8")
    return {plan["difficulty"]: len(plan["steps"]) for plan in catalog["plans"]}
