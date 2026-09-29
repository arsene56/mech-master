"""Derive Bolt's six active axes and two passive ankle axes from source parts."""

import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/Content"))
from bolt_content import MODEL_ID


def teaching_keyframes():
    # Continuous alternating motion, with knee flexion offset from hip travel.
    # There is no preparation/return hold. Both ends retain the source pose.
    frames = []
    for sample in range(17):
        phase = sample / 16
        sine = math.sin(2 * math.pi * phase) if sample not in (0, 8, 16) else 0.0
        knee_lead = 4 * math.sin(4 * math.pi * phase) if sample % 4 else 0.0
        angles = []
        for direction in (1, -1):
            stride = direction * sine
            hip = 8.5 * stride + 3.5 * stride * stride
            knee = -14 * stride - 6 * stride * stride + knee_lead
            angles.extend((4 * stride, hip, knee, -hip - knee))
        frames.append({"phase": phase, "angles": [round(value, 6) for value in angles]})
    return frames


def build_rig(objects, parts):
    named = {obj.name: obj for obj in objects}
    joints, bindings, dimensions = [], [], {}

    def find(group, source_name):
        return [part for part in parts if part["assemblyId"] == group and part["sourceName"] == source_name]

    def center(part):
        obj = named[part["object"]]
        return obj.matrix_world.translation.copy()

    for side, label in (("left", "左"), ("right", "右")):
        anchors, axes = [], []
        for group, key, display, parent, limit in (
                (side + "_hip_aa", side + "_hip_aa", label + "髋侧摆", "", 10),
                (side + "_hip_fe", side + "_hip_fe", label + "髋屈伸", side + "_hip_aa", 20),
                (side + "_thigh", side + "_knee", label + "膝屈伸", side + "_hip_fe", 28)):
            pulley = find(group, "transmission_pulley_at3_t30_output")
            bearings = find(group, "bearing_32_25_4_sbn_61705RS")
            if len(pulley) != 1 or len(bearings) != 2:
                raise ValueError("Missing paired source output support: " + group)
            p = center(pulley[0])
            a, b = (center(part) for part in bearings)
            if (b - a).length < .002 or (p - a).cross((b - a).normalized()).length > .0003:
                raise ValueError("Output pulley is not supported on the bearing axis: " + group)
            target_axis = Vector((0, -1, 0)) if key.endswith("hip_aa") else Vector((1, 0, 0))
            sign = 1 if (b - a).dot(target_axis) > 0 else -1
            joints.append({"id": key, "displayName": display, "parentId": parent, "axisSign": sign,
                           "passive": False, "pivotObject": pulley[0]["object"],
                           "axisStartObject": bearings[0]["object"], "axisEndObject": bearings[1]["object"],
                           "axisObject": "", "minimumDegrees": -limit, "maximumDegrees": limit})
            anchors.append(p)
            axes.append((b - a).normalized() * sign)
        pin = find(side + "_shin", "pin_5mm_28mm")
        if len(pin) != 1: raise ValueError("Missing ankle shaft")
        pin_axis = named[pin[0]["object"]].matrix_world.to_3x3() @ Vector((0, 0, 1))
        joints.append({"id": side + "_ankle", "displayName": label + "被动踝", "parentId": side + "_knee",
                       "axisSign": 1 if pin_axis.x > 0 else -1,
                       "passive": True, "pivotObject": pin[0]["object"], "axisObject": pin[0]["object"],
                       "axisStartObject": "", "axisEndObject": "", "minimumDegrees": -40, "maximumDegrees": 40})
        upper_delta = anchors[2] - anchors[1]
        lower_delta = center(pin[0]) - anchors[2]
        upper = (upper_delta - axes[1] * upper_delta.dot(axes[1])).length
        lower = (lower_delta - axes[2] * lower_delta.dot(axes[2])).length
        if abs(upper - .2) > .0003 or abs(lower - .2) > .0003:
            raise ValueError(f"Invalid source leg centres: {side} {upper} {lower}")
        dimensions[side] = {"upperLegAxisDistanceM": upper, "lowerLegAxisDistanceM": lower}

    for part in parts:
        group, name = part["assemblyId"], part["sourceName"]
        if group in ("body_frame", "electronics"):
            joint = "fixed"
        else:
            side = group.split("_")[0]
            if group.endswith("hip_aa"): joint = "fixed"
            elif group.endswith("hip_fe"): joint = side + "_hip_aa"
            elif group.endswith("thigh"): joint = side + "_hip_fe"
            elif group.endswith("shin"): joint = side + "_knee"
            else: joint = side + "_ankle"
            # Output bearings belong to the upstream support; only the output
            # pulley rotates with the driven downstream segment.
            if name == "transmission_pulley_at3_t30_output":
                joint = side + ("_hip_aa" if group.endswith("hip_aa") else "_hip_fe" if group.endswith("hip_fe") else "_knee")
        bindings.append({"objectName": part["object"], "jointId": joint, "sourceCadInstance": part["sourceInstancePath"]})
    # Relative movements around the original assembly pose. Passive ankle
    # angles compensate hip/knee pitch for a visibly articulated teaching view;
    # these are prescribed poses, not a simulated motor or ground constraint.
    rig = {"schemaVersion": 1, "modelId": MODEL_ID, "cycleSeconds": 12,
           "description": "Source CAD axes; continuous alternating, fixed-body teaching cycle with periodic shape-preserving cubic interpolation, not locomotion dynamics.",
           "joints": joints, "bindings": bindings,
           "keyframes": teaching_keyframes(),
           "dimensions": dimensions}
    (ROOT / "Assets/Resources/MechanicalCatalog/BoltMotionRig.json").write_text(
        json.dumps(rig, ensure_ascii=False, indent=2), encoding="utf-8")
    print("BOLT_MOTION_RIG_OK 6 active + 2 passive axes", dimensions, flush=True)
    return rig


if __name__ == "__main__":
    # Rebuild just the motion rig from the existing, unchanged source asset.
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / "Assets/Art/Models/Source/BoltSource.blend"))
    manifest = json.loads((ROOT / "Assets/StreamingAssets/MechanicalCatalog/bolt_model_manifest.json").read_text(encoding="utf-8"))
    build_rig([obj for obj in bpy.data.objects if obj.type == "MESH"], manifest["parts"])
