"""Conservative service boundaries for the authored full-suspension bike.

Mesh primitives are occurrences, not dismantling instructions. Bonded carbon,
sealed bearings, pressurized shock, riveted rotors and cassette stay intact.
"""

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MODEL_ID = "bike.carbon.full-suspension.v1"
ASSEMBLIES = {
    "frame": ("主车架", "主车架承载骑手，并连接整车部件。", "碳纤维主架作为一个整体拆装。", "模型展示结构，不是承载测试或维修指南。"),
    "rear_suspension": ("后摇臂与转点", "后轮随摇臂摆动，减轻路面冲击。", "摇臂绕主转点转动，轴承让运动更平顺。", "左右摇臂和金属连接件保持源装配关系。"),
    "shock": ("后避震器", "后避震器在后轮受冲击时压缩。", "阻尼抑制反复弹跳，弹性元件帮助回弹。", "避震器是封闭单元，不展示带压内部拆解。"),
    "fork": ("避震前叉", "前叉支承前轮，并吸收前轮冲击。", "叉腿沿内管滑动，导向结构保持轮轴位置。", "上叉组件和下叉组件按源结构分组。"),
    "front_wheel": ("前轮", "前轮滚动，并随车把转向。", "辐条连接轮圈和花鼓，轮胎接触路面。", "重复辐条与辐条帽成组，花鼓不拆密封内部。"),
    "rear_wheel": ("后轮", "后轮滚动，传动系统驱动它前进。", "轮圈、辐条和花鼓共同支承车轮。", "本车前后轮不同规格，不套用硬尾车尺寸。"),
    "front_brake": ("前制动器", "前制动器让前轮减速。", "夹器压紧碟片，把运动能量转成热。", "铆接碟片作为整件，演示不模拟制动力。"),
    "rear_brake": ("后制动器", "后制动器让后轮减速。", "夹器随摇臂运动，并与后轮碟片保持对齐。", "夹器是封闭组件，不拆液压内部。"),
    "transmission": ("传动与变速", "曲柄和飞轮把踩踏传到后轮。", "后拨导向链条，不同飞轮齿数改变传动比。", "飞轮作为整体拆装，不把每片齿轮算成一步。"),
    "pedals": ("脚踏", "脚踏承托双脚，传递踩踏力量。", "脚踏绕自身轴转动，便于脚面保持姿态。", "每侧脚踏连同轴作为一个服务单元。"),
    "chain_guide": ("链条与导链", "链条和导轮把动力传到后轮。", "导轮改变链条路径，帮助链条稳定运行。", "链条为源模型的整体网格，不逐节拆装。"),
    "cockpit": ("车把与操控", "车把用于转向，手柄用于操控制动。", "把立夹紧车把，碗组支承前叉转向。", "这里不推断源模型的左右刹把对应关系。"),
    "seat": ("坐垫与座管", "坐垫支承身体，座管连接坐垫和车架。", "座轨和夹持结构共同固定坐垫。", "坐垫粘合层与座轨保持为一个完整单元。"),
    "cables": ("柔性管线", "柔性管线把操控传到相应部件。", "弯曲管线为转向和悬架运动留出空间。", "悬架演示暂隐管线，不模拟液压或软管形变。"),
}

LABELS = {
    "main_frame": "碳纤维主架", "protector": "下管护板", "bumpers": "缓冲件与固定件",
    "swingarm": "后摇臂总成", "left_arm": "左摇臂", "right_arm": "右摇臂", "bridge": "摇臂连接件",
    "arm_fasteners": "摇臂紧固件组", "dropouts": "后轮轴座与尾钩", "pivot": "主转点组件",
    "pivot_axle": "主转点轴与端盖", "pivot_bearing": "密封转点轴承", "pivot_hardware": "转点衬套与紧固件",
    "shock": "后避震器总成", "upper": "前叉上组件", "lower": "前叉下组件",
    "tire": "轮胎", "wheel": "轮圈辐条花鼓总成", "rim": "轮圈", "spokes": "辐条与辐条帽组", "hub": "花鼓总成",
    "rotor": "碟片总成", "caliper": "制动夹器总成", "cassette": "十二片飞轮总成", "derailleur": "后拨总成",
    "left_crank": "左曲柄组件", "right_crank": "右曲柄组件", "right_drive": "右曲柄与牙盘", "chainring": "单片牙盘",
    "left_pedal": "左脚踏", "right_pedal": "右脚踏", "chain": "链条整体", "drive_guide": "上导链组件",
    "return_guide": "下导链组件", "drive_idler": "上导轮与轴套", "drive_cage": "上导轮支架",
    "return_idler": "下导轮与密封轴承", "return_mount": "下导轮安装件",
    "bars_grips": "车把与握把", "bar": "车把", "left_grip": "握把组件一", "right_grip": "握把组件二",
    "stem": "把立", "headset": "转向碗组", "headset_upper": "上碗组", "headset_lower": "下碗组",
    "brake_controls": "制动手柄组", "brake_control_1": "制动手柄一", "brake_control_2": "制动手柄二",
    "saddle": "坐垫与座轨", "post": "座管组件", "post_lower": "座管下段", "post_upper": "座管上段与夹持件",
    "front_lines": "前部管线", "rear_lines": "后部管线",
}


def classify(node):
    path = node["ancestry"] + [node["name"]]
    text = "/".join(path)
    if node["name"].startswith("Schlauch_") or node["name"].startswith("Bremsschlauch"):
        return "cables", "front_lines" if node["name"] in ("Schlauch_01", "Schlauch_02") or node["name"].startswith("Bremsschlauch") else "rear_lines"
    if "Daempfer_Cane-Creek" in text or "Daempferaufnahme_oben" in text: return "shock", "shock"
    if "Bremsscheibe" in text: return ("front_brake" if "RadVorn" in path else "rear_brake"), "rotor"
    if "Magura_MT7" in text: return ("front_brake" if "Federung" in path else "rear_brake"), "caliper"
    if "HR_Ritzelpaket" in text: return "transmission", "cassette"
    if "Schaltwerk" in path: return "transmission", "derailleur"
    for side, assembly in (("VR", "front_wheel"), ("HR", "rear_wheel")):
        if "ZB_" + side in path:
            return assembly, "tire" if "Reifen_" in text else "rim" if "Felge_" in text else "spokes" if "Speichen" in text else "hub"
    if "Federgabel" in text: return "fork", "lower" if "Federung" in path else "upper"
    if "Pedal_Funn" in text: return "pedals", "left_pedal" if "Pedal_Funn_Bigfoot_le" in path else "right_pedal"
    if "Kurbel_X01_DH_li" in path: return "transmission", "left_crank"
    if "Kurbel_X01_DH_re" in path: return "transmission", "right_crank"
    if "Kettenblatt" in text: return "transmission", "chainring"
    if "RobertS2016_Kette" in text: return "chain_guide", "chain"
    if "Umlenkrolle" in text or "inafag_626" in text:
        if "Umlenkrollenhalter_Leertrum" in text or "Umlenkrolle_Nabe" in text: return "chain_guide", "return_mount"
        if "Umlenkrolle_Leertrum" in text or "inafag_626" in text: return "chain_guide", "return_idler"
        if "Umlenkrolle_Kaefig_838" in text: return "chain_guide", "drive_cage"
        return "chain_guide", "drive_idler"
    if "Hauptlager_Zwischenhuelse" in text or "ISO_7380" in text: return "rear_suspension", "pivot_hardware"
    if "Hauptlager_Achse" in text: return "rear_suspension", "pivot_axle"
    if "inafag_61805" in text: return "rear_suspension", "pivot_bearing"
    if "Schwinge" in text:
        if "Schwinge_Verbinder" in text: return "rear_suspension", "bridge"
        if "ISO_" in text: return "rear_suspension", "arm_fasteners"
        if "HR-Aufnahme" in text or "Schaltwerkauge" in text: return "rear_suspension", "dropouts"
        return "rear_suspension", "left_arm" if "Schwinge_links" in text else "right_arm"
    if "Bremsgriff" in text: return "cockpit", "brake_control_2" if "Bremsgriff 1" in path else "brake_control_1"
    if "Lenkergriff" in text: return "cockpit", "right_grip" if "Lenkergriff 1" in path else "left_grip"
    if "Lenker_23103" in text: return "cockpit", "bar"
    if "Vorbau_Hope" in text: return "cockpit", "stem"
    if "Steuersatz_" in text: return "cockpit", "headset_upper" if "Steuersatz_ob" in text else "headset_lower"
    if "Sattelstange" in path:
        if any("Sattel_" + suffix in text for suffix in ("736", "24139", "26656")): return "seat", "saddle"
        return "seat", "post_lower" if "Sattel_1879" in text else "post_upper"
    if "Unterschutzplatte" in text: return "frame", "protector"
    if "Gummi-Metall-Element" in text or "ISO_10642_M6x12" in text: return "frame", "bumpers"
    if "Hauptrahmen_837" in text: return "frame", "main_frame"
    raise ValueError("Unclassified authored geometry: " + text)


def service_key(part, level):
    assembly, key = part["assemblyId"], part["serviceKey"]
    if level == "Simple": return "whole"
    if level == "Advanced": return key
    if assembly == "rear_suspension":
        if key in ("left_arm", "right_arm", "bridge", "arm_fasteners"): return "swingarm"
        if key.startswith("pivot_"): return "pivot"
    if assembly in ("front_wheel", "rear_wheel") and key != "tire": return "wheel"
    if key in ("right_crank", "chainring"): return "right_drive"
    if key in ("drive_idler", "drive_cage"): return "drive_guide"
    if key in ("return_idler", "return_mount"): return "return_guide"
    if key in ("bar", "left_grip", "right_grip"): return "bars_grips"
    if key.startswith("headset_"): return "headset"
    if key.startswith("brake_control_"): return "brake_controls"
    if key in ("post_lower", "post_upper"): return "post"
    return key


def write_content(parts):
    plans = []
    for level in ("Simple", "Standard", "Advanced"):
        groups = {}
        for part in parts: groups.setdefault((part["assemblyId"], service_key(part, level)), []).append(part)
        steps = []
        for assembly, info in ASSEMBLIES.items():
            for (owner, key), members in groups.items():
                if owner != assembly: continue
                steps.append({"id": f"carbon.{level.lower()}.{assembly}.{key}", "displayName": info[0] if key == "whole" else LABELS[key],
                    "tool": "Hand", "simpleSummary": info[1], "mechanism": info[2], "advancedNote": info[3],
                    "assemblyId": assembly, "componentId": key, "objectNames": [p["object"] for p in members]})
        names = [name for step in steps for name in step["objectNames"]]
        if len(names) != len(parts) or len(set(names)) != len(parts): raise ValueError("Incomplete or duplicated tier coverage")
        if any(len(step[field]) > 50 for step in steps for field in ("simpleSummary", "mechanism", "advancedNote")):
            raise ValueError("Narration too long")
        plans.append({"difficulty": level, "steps": steps})
    target = ROOT / "Assets/Resources/MechanicalCatalog"
    (target / "Models").mkdir(parents=True, exist_ok=True)
    catalog = {"schemaVersion": 1, "moduleId": MODEL_ID, "plans": plans}
    model = {"schemaVersion": 1, "id": MODEL_ID, "displayName": "Carbon 软尾山地车", "displayOrder": 5,
        "catalogResourcePath": "MechanicalCatalog/CarbonFrameBikeInteractionCatalog",
        "moduleResourcePaths": [f"Models/CarbonFrameBike/Modules/{a}_LOD0" for a in ASSEMBLIES],
        "assemblies": [{"id": a, "displayName": info[0]} for a, info in ASSEMBLIES.items()],
        "motion": {"kind": "carbon-suspension-v1", "rigResourcePath": "MechanicalCatalog/CarbonFrameBikeMotionRig"}}
    for name, data in (("CarbonFrameBikeInteractionCatalog.json", catalog), ("Models/CarbonFrameBike.json", model)):
        (target / name).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    return {p["difficulty"]: len(p["steps"]) for p in plans}
