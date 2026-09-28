"""Build a kinematic teaching rig from the official, assembled Moveo meshes.

No community URDF coordinates are used. Joint centres come from the real shaft
and bearing meshes. The gripper uses its four physical pivot screws per side.
Run with Blender --background --python Tools/Blender/build_moveo_motion_rig.py.
"""

import json
from pathlib import Path
import sys

import bpy
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/Content"))
from moveo_content import FASTENERS

TARGET = ROOT / "Assets/Resources/MechanicalCatalog/MoveoMotionRig.json"


def main_joint(part):
    name, assembly = part["sourceName"], part["assemblyId"]
    if name == "Smooth bar 8mm x 140mm": return "shoulder"
    if name == "Smooth bar 8mm x 121mm": return "elbow"
    if name in ("Smooth bar 8mm x 50mm", "Coupling 5 to 8 mm"): return "wrist_roll"
    if name == "Barra llisa 8mm x 80mm" or "/4M2-" in part["sourceInstancePath"]: return "wrist_pitch"
    return {"shoulder": "base", "upper_arm": "shoulder", "elbow": "shoulder",
            "forearm": "elbow", "wrist": "wrist_roll", "gripper": "wrist_pitch"}.get(assembly, "fixed")


def build_rig(objects, parts):
    named = {obj.name: obj for obj in objects}
    if set(named) != {part["object"] for part in parts}:
        raise ValueError("Motion source inventory differs from the disassembly inventory")
    by_name = {part["object"]: part for part in parts}

    def find(source_name, path_contains=None):
        matches = [part["object"] for part in parts if part["sourceName"] == source_name
                   and (not path_contains or path_contains in part["sourceInstancePath"])]
        if len(matches) != 1:
            raise ValueError(f"Ambiguous motion anchor: {source_name} / {path_contains}")
        return matches[0]

    def joint(key, label, parent, pivot, axis_start, axis_end, low, high):
        return {"id": key, "displayName": label, "parentId": parent,
                "pivotObject": pivot, "axisStartObject": axis_start, "axisEndObject": axis_end,
                "minimumDegrees": low, "maximumDegrees": high}

    joints = [
        joint("base", "底座回转", "", find("8mm Bearing", "8mm Bearing-1@Rotary Plate"),
              find("8mm Bearing", "8mm Bearing-1@Rotary Plate"),
              find("8mm Bearing", "8mm Bearing-2@Rotary Plate"), -30, 30),
        joint("shoulder", "肩关节", "base", find("Smooth bar 8mm x 140mm"),
              find("8mm Bearing", "8mm Bearing-1@2M1 assembly"),
              find("8mm Bearing", "8mm Bearing-2@2M1 assembly"), -12, 12),
        joint("elbow", "肘关节", "shoulder", find("Smooth bar 8mm x 121mm"),
              find("8mm Bearing", "8mm Bearing-2@3M1"),
              find("8mm Bearing", "8mm Bearing-1@3M1"), -18, 18),
        joint("wrist_roll", "腕部旋转", "elbow", find("Smooth bar 8mm x 50mm"),
              find("8mm Bearing", "8mm Bearing-1@3M2"),
              find("8mm Bearing", "8mm Bearing-1@4M"), -35, 35),
        joint("wrist_pitch", "腕部俯仰", "wrist_roll", find("Barra llisa 8mm x 80mm"),
              find("8mm Bearing", "8mm Bearing-2@4M"),
              find("8mm Bearing", "8mm Bearing-3@4M"), -20, 20),
    ]
    gripper_groups = {
        find("Idol Gear B"): "left_driver",
        find("Pivot Arm B", "Pivot Arm B-2@"): "left_follower",
        find("Gripper Left B"): "left_finger",
        find("Servo Gear B"): "right_driver",
        find("Pivot Arm B", "Pivot Arm B-1@"): "right_follower",
        find("Gripper Right B"): "right_finger",
    }
    bindings = {}
    supports = []
    for part in parts:
        if FASTENERS.match(part["sourceName"]): continue
        name = part["object"]
        bindings[name] = {"jointId": main_joint(part), "gripperGroup": gripper_groups.get(name, "")}
        obj = named[name]
        vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        polygons = [tuple(poly.vertices) for poly in obj.data.polygons]
        supports.append((BVHTree.FromPolygons(vertices, polygons), name))
    # Screws, washers and nuts follow the actual surface they fasten, including
    # hardware shared across catalog module boundaries. The catalog is unchanged.
    for part in parts:
        name = part["object"]
        if name in bindings: continue
        point = named[name].location
        distance, host = min(((tree.find_nearest(point)[3], host) for tree, host in supports),
                             key=lambda item: item[0])
        bindings[name] = {**bindings[host], "supportObject": host, "supportDistanceM": distance}

    def screw(prefix):
        matches = [name for name in named if name.endswith(prefix)]
        if len(matches) != 1: raise ValueError("Missing audited gripper pivot " + prefix)
        return matches[0]

    rig = {"schemaVersion": 1, "modelId": "arm.bcn3d.moveo.v1",
           "description": "Official CAD shaft/bearing axes; conservative relative teaching angles, not actuator limits.",
           "cycleSeconds": 20, "joints": joints,
           "gripper": {"parentId": "wrist_pitch", "minimumDegrees": 0, "maximumDegrees": 40,
                       "axisStartObject": screw("39952b43aa"), "axisEndObject": screw("d77d4673db"),
                       "left": {"driverPivotObject": screw("39952b43aa"),
                                "driverTipObject": screw("c4466d3214"),
                                "followerPivotObject": screw("46c546cf57"),
                                "followerTipObject": screw("d250089828"), "direction": 1},
                       "right": {"driverPivotObject": screw("d609ea9da9"),
                                 "driverTipObject": screw("3c8c01762e"),
                                 "followerPivotObject": screw("a1c77b42a8"),
                                 "followerTipObject": screw("c5957bd90f"), "direction": -1}},
           "bindings": [{"objectName": name, **bindings[name]} for name in sorted(bindings)],
           "keyframes": [
               {"phase": 0, "angles": [0, 0, 0, 0, 0], "gripperDegrees": 0},
               {"phase": .2, "angles": [-28, 8, -8, -28, 14], "gripperDegrees": 0},
               {"phase": .35, "angles": [-28, 8, -8, -28, 14], "gripperDegrees": 40},
               {"phase": .65, "angles": [28, -8, 14, 28, -14], "gripperDegrees": 40},
               {"phase": .8, "angles": [28, -8, 14, 28, -14], "gripperDegrees": 0},
               {"phase": 1, "angles": [0, 0, 0, 0, 0], "gripperDegrees": 0},
           ]}
    # Store original mesh centres for a reproducible Blender/engine visual audit.
    for binding in rig["bindings"]:
        obj = named[binding["objectName"]]
        binding["sourceCadInstance"] = by_name[obj.name]["sourceInstancePath"]
    TARGET.write_text(json.dumps(rig, ensure_ascii=False, indent=2), encoding="utf-8")
    print("MOVEO_MOTION_RIG_OK 5 axes, 2 four-bars,", len(bindings), "stable objects", flush=True)
    return rig


def main():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / "Assets/Art/Models/Source/MoveoSource.blend"))
    parts = json.loads((ROOT / "Assets/StreamingAssets/MechanicalCatalog/moveo_model_manifest.json")
                       .read_text(encoding="utf-8"))["parts"]
    build_rig([bpy.data.objects[part["object"]] for part in parts], parts)


if __name__ == "__main__":
    main()
