"""Authentic OpenTorque service groups and concise Chinese teaching content."""

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MODEL_ID = "gearbox.opentorque.planetary.v1"
ASSEMBLIES = {
    "housing": ("壳体与盖板", "保护齿轮，并支承内部零件。", "壳体内壁的齿圈固定不动，供行星轮啮合。", "内齿圈与壳体为一体打印件，不作为独立零件拆装。"),
    "carrier": ("行星架与输出支承", "托住三个行星轮，把慢转动传出去。", "行星架围绕太阳轮旋转，成为减速器的输出端。", "三片行星架保留真实分件；交叉滚子轴承按整件拆装。"),
    "sun": ("太阳轮", "位于中央，把转动传给行星轮。", "九齿太阳轮与三个二十七齿行星轮同时啮合。", "电机和输入轴未包含在源模型中，演示仅驱动已有太阳轮。"),
    "planets": ("行星轮组件", "一边自转，一边绕太阳轮转。", "三个行星轮间隔一百二十度，在内齿圈中滚动。", "行星轮、轴承和销轴保留源位置；不模拟轴承内部滚动体。"),
    "encoder": ("编码器安装件", "为位置感知部件提供安装位置。", "磁体安装座随输入端转动，可配合传感器测角度。", "源模型仅含盖和安装座，没有编码器电路板或完整线束。"),
}
TYPES = {
    "Actuator Housing": ("housing", "带内齿圈壳体"),
    "Bearing Retainer": ("housing", "输出轴承压圈"),
    "Backplate": ("housing", "后盖板"),
    "Planet Carrier A": ("carrier", "行星架外侧输出板"),
    "Planet Carrier B": ("carrier", "行星架中间连接板"),
    "Planet Carrier C": ("carrier", "行星架内侧支承板"),
    "RA-8008C Cross Roller Bearing": ("carrier", "交叉滚子输出轴承"),
    "Sun Gear": ("sun", "九齿太阳轮"),
    "Planet Gear": ("planets", "二十七齿行星轮"),
    "F625ZZ": ("planets", "带法兰密封轴承"),
    "M5x30 Dowel Pin": ("planets", "五毫米行星轮销轴"),
    "Encoder Cover": ("encoder", "编码器保护盖"),
    "Encoder Magnet Holder": ("encoder", "编码器磁体安装座"),
}
KNOWLEDGE = {
    "Actuator Housing": ("给行星轮提供固定的内齿圈。", "六十三齿内齿圈与行星轮内啮合，约束它的转动。", "壳体和齿圈是一体打印件；演示透明只是观察效果。"),
    "Bearing Retainer": ("把输出轴承固定在壳体中。", "压圈限制轴承轴向移动，使行星架保持支承。", "CAD 未包含多数安装螺钉，不能照此当作现实维修步骤。"),
    "Backplate": ("盖住减速器后侧。", "后盖与壳体连接，为输入侧部件提供安装边界。", "源装配没有电机，后盖附近的空位不是漏导入的零件。"),
    "RA-8008C Cross Roller Bearing": ("支承行星架的输出转动。", "交叉滚子轴承可承受多个方向的载荷。", "轴承按密封维修单元处理，演示不计算内部滚动接触。"),
    "F625ZZ": ("让行星轮能绕销轴转动。", "带法兰轴承支承行星轮，随行星架一起公转。", "不把轴承内的滚动体拆成操作步骤，也不模拟其实际转速。"),
    "M5x30 Dowel Pin": ("为行星轮提供转轴。", "三个五毫米销轴连接行星架，定位行星轮的轴线。", "销轴长三十毫米，三个重复销轴在探索档也成组拆装。"),
}


def write_content(parts):
    plans = []
    for level in ("Simple", "Standard", "Advanced"):
        groups = {}
        for part in parts:
            name, group = part["sourceName"], part["assemblyId"]
            if level == "Simple":
                key = "whole"
            elif level == "Standard":
                if group == "housing": key = "backplate" if name == "Backplate" else "housing"
                elif group == "carrier": key = "bearing" if "Bearing" in name else "plates"
                elif group == "planets": key = "planet_" + str(part["planetIndex"] + 1)
                else: key = name
            else:
                key = name + ("_" + str(part["planetIndex"] + 1) if group == "planets" and name != "M5x30 Dowel Pin" else "")
            groups.setdefault((group, key), []).append(part)
        steps = []
        for group, info in ASSEMBLIES.items():
            for (assembly, key), members in groups.items():
                if assembly != group: continue
                label = info[0] if level == "Simple" else (
                    "行星轮组件 " + key[-1] if level == "Standard" and group == "planets"
                    else "行星架三片板组" if key == "plates"
                    else "壳体与轴承压圈" if key == "housing"
                    else TYPES[members[0]["sourceName"]][1]
                         + (" " + key[-1] if group == "planets" and len(members) == 1 else "组" if len(members) > 1 else ""))
                texts = KNOWLEDGE.get(members[0]["sourceName"], info[1:]) if len({p["sourceName"] for p in members}) == 1 else info[1:]
                if any(not text or len(text) > 50 for text in texts):
                    raise ValueError("Invalid concise narration")
                # Identity uses the original stable part IDs, not display names.
                identity = "whole" if level == "Simple" else members[0]["id"].removeprefix("opentorque.")
                steps.append({"id": f"opentorque.{level.lower()}.{group}.{identity}", "displayName": label,
                              "tool": "Hand", "simpleSummary": texts[0], "mechanism": texts[1],
                              "advancedNote": texts[2], "assemblyId": group, "componentId": key,
                              "objectNames": [part["object"] for part in members]})
        names = [name for step in steps for name in step["objectNames"]]
        if len(names) != 19 or len(set(names)) != 19 or set(names) != {p["object"] for p in parts}:
            raise ValueError("Incomplete tier coverage")
        plans.append({"difficulty": level, "steps": steps})
    target = ROOT / "Assets/Resources/MechanicalCatalog"
    target.mkdir(parents=True, exist_ok=True)
    (target / "Models").mkdir(exist_ok=True)
    catalog = {"schemaVersion": 1, "moduleId": MODEL_ID, "plans": plans}
    model = {"schemaVersion": 1, "id": MODEL_ID, "displayName": "OpenTorque 行星减速器", "displayOrder": 30,
             "catalogResourcePath": "MechanicalCatalog/OpenTorqueInteractionCatalog",
             "moduleResourcePaths": [f"Models/OpenTorque/Modules/{key}_LOD0" for key in ASSEMBLIES],
             "assemblies": [{"id": key, "displayName": info[0]} for key, info in ASSEMBLIES.items()],
             "motion": {"kind": "opentorque-planetary-v1", "rigResourcePath": "MechanicalCatalog/OpenTorqueMotionRig"}}
    for path, data in ((target / "OpenTorqueInteractionCatalog.json", catalog), (target / "Models/OpenTorque.json", model)):
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    counts = {plan["difficulty"]: len(plan["steps"]) for plan in plans}
    if list(counts.values()) != [5, 10, 17]: raise ValueError("Unexpected service groups")
    return counts
