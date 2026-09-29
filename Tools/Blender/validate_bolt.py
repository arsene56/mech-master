"""Verify the Bolt source, source transforms, stable identities and LOD chain."""

import json
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/StreamingAssets/MechanicalCatalog"


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def main():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / "Assets/Art/Models/Source/BoltSource.blend"))
    manifest = read(DATA / "bolt_model_manifest.json")
    runtime = read(DATA / "bolt_runtime_assets.json")
    rig = read(ROOT / "Assets/Resources/MechanicalCatalog/BoltMotionRig.json")
    objects = {obj.name: obj for obj in bpy.data.objects if obj.type == "MESH"}
    assert len(objects) == 345 == len(manifest["parts"])
    assert len({part["id"] for part in manifest["parts"]}) == 345
    assert len({part["componentId"] for part in manifest["parts"]}) == 56
    conversion = Matrix.Rotation(3.141592653589793 / 2, 4, "X")
    triangles = 0
    max_rotation_error = 0.0
    max_position_error = 0.0
    for part in manifest["parts"]:
        obj = objects[part["object"]]
        assert obj["mm_part_id"] == part["id"]
        assert obj["mm_assembly_id"] == part["assemblyId"]
        expected = conversion @ Matrix(part["sourceMatrix"]) @ Matrix.Translation(Vector(part["sourceCenter"]))
        rotation_error = max(abs(obj.matrix_world[row][column] - expected[row][column]) for row in range(3) for column in range(3))
        position_error = (obj.matrix_world.translation - expected.translation).length
        max_rotation_error = max(max_rotation_error, rotation_error)
        max_position_error = max(max_position_error, position_error)
        assert rotation_error < 1e-6, (obj.name, "rotation", rotation_error)
        assert position_error < 1e-7, (obj.name, "position metres", position_error)
        obj.data.calc_loop_triangles()
        assert len(obj.data.loop_triangles) == part["sourceTriangles"]
        triangles += len(obj.data.loop_triangles)
        assert obj.data.vertices and obj.data.materials
    assert triangles == runtime["sourceTriangles"]
    assert len(runtime["modules"]) == 12
    assert sum(module["partObjects"] for module in runtime["modules"]) == 345
    lod0 = sum(module["triangles"] for module in runtime["modules"])
    assert triangles > lod0 > runtime["lod1Triangles"] > runtime["lod2Triangles"] > 0
    for module in runtime["modules"]:
        path = ROOT / "Assets/Resources" / (module["resourcePath"] + ".fbx")
        assert path.stat().st_size == module["bytes"] > 0
    for side in ("left", "right"):
        assert abs(rig["dimensions"][side]["upperLegAxisDistanceM"] - .2) < .0003
        assert abs(rig["dimensions"][side]["lowerLegAxisDistanceM"] - .2) < .0003
        pin = next(part for part in manifest["parts"] if part["assemblyId"] == side + "_shin" and part["sourceName"] == "pin_5mm_28mm")
        assert abs(max(objects[pin["object"]].dimensions) - .028) < .00001
    print(f"BOLT_SOURCE_VALIDATION_OK 56 definitions / 345 instances / source={triangles} LOD0={lod0} LOD1={runtime['lod1Triangles']} LOD2={runtime['lod2Triangles']}", flush=True)
    print(f"BOLT_TRANSFORM_VALIDATION_OK max rotation matrix error={max_rotation_error:.3g}, max position error={max_position_error:.3g}m", flush=True)


if __name__ == "__main__":
    main()
