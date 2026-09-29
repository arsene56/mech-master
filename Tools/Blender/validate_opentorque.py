"""Validate real CAD transforms, geometry, gear layout and all exported LODs."""

import json
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/StreamingAssets/MechanicalCatalog"


def read(path): return json.loads(path.read_text(encoding="utf-8"))


def main():
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / "Assets/Art/Models/Source/OpenTorqueSource.blend"))
    manifest = read(DATA / "opentorque_model_manifest.json")
    runtime = read(DATA / "opentorque_runtime_assets.json")
    rig = read(ROOT / "Assets/Resources/MechanicalCatalog/OpenTorqueMotionRig.json")
    objects = {obj.name: obj for obj in bpy.data.objects if obj.type == "MESH"}
    assert len(objects) == 19 == len(manifest["parts"])
    assert len({p["id"] for p in manifest["parts"]}) == 19
    assert len({p["componentId"] for p in manifest["parts"]}) == 13
    conversion = Matrix.Rotation(math.pi / 2, 4, "X")
    source_triangles, max_error = 0, 0
    for part in manifest["parts"]:
        obj = objects[part["object"]]
        assert obj["mm_part_id"] == part["id"] and obj["mm_assembly_id"] == part["assemblyId"]
        expected = conversion @ Matrix(part["sourceMatrix"]) @ Matrix.Translation(Vector(part["sourceCenter"]))
        error = max(abs(expected[r][c] - obj.matrix_world[r][c]) for r in range(4) for c in range(4))
        assert error < 1e-6, (obj.name, error)
        max_error = max(max_error, error)
        obj.data.calc_loop_triangles()
        assert len(obj.data.loop_triangles) == part["sourceTriangles"] > 0
        source_triangles += part["sourceTriangles"]
        assert obj.data.materials and all(material is not None for material in obj.data.materials)
        if part["sourceName"] in ("Sun Gear", "Planet Gear", "Actuator Housing"):
            assert part["sourceTriangles"] == part["runtimeTriangles"], "LOD0 must preserve tooth faces"
        if part["sourceName"] == "M5x30 Dowel Pin": assert abs(max(obj.dimensions) - .03) < 1e-6
    assert source_triangles == runtime["sourceTriangles"]
    points = [obj.matrix_world @ Vector(corner) for obj in objects.values() for corner in obj.bound_box]
    size = [max(p[a] for p in points) - min(p[a] for p in points) for a in range(3)]
    assert all(abs(a - b) < 1e-5 for a, b in zip(sorted(size), [.095, .11, .11])), size
    sun = objects[rig["sunObject"]].location
    axis = (objects[rig["axisEndObject"]].location - objects[rig["axisStartObject"]].location).normalized()
    planet_offsets = [objects[name].location - sun for name in rig["planetObjects"]]
    planar = [offset - axis * offset.dot(axis) for offset in planet_offsets]
    assert all(abs(offset.length - .027) < 1e-6 for offset in planar)
    assert all(abs(math.degrees(planar[i].angle(planar[(i + 1) % 3])) - 120) < .001 for i in range(3))
    source_matrices = {name: obj.matrix_world.copy() for name, obj in objects.items()}
    lod0 = sum(module["triangles"] for module in runtime["modules"])
    assert source_triangles > lod0 > runtime["lod1Triangles"] > runtime["lod2Triangles"] > 0
    # Re-import exports to detect Blender/FBX scale and node-identity mistakes.
    bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete(use_global=False)
    for module in runtime["modules"]:
        path = ROOT / "Assets/Resources" / (module["resourcePath"] + ".fbx")
        assert path.stat().st_size == module["bytes"] > 0
        bpy.ops.import_scene.fbx(filepath=str(path))
    imported = {obj.name: obj for obj in bpy.data.objects if obj.type == "MESH"}
    assert set(imported) == set(source_matrices)
    for name, obj in imported.items():
        assert max(abs(obj.matrix_world[r][c] - source_matrices[name][r][c]) for r in range(4) for c in range(4)) < 1e-6
        assert obj.data.vertices and obj.data.materials
    for suffix, expected_count, expected_triangles in (("LOD1", 5, runtime["lod1Triangles"]), ("LOD2", 1, runtime["lod2Triangles"])):
        bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete(use_global=False)
        bpy.ops.import_scene.fbx(filepath=str(ROOT / f"Assets/Resources/Models/OpenTorque/OpenTorque_{suffix}.fbx"))
        meshes = [obj.data for obj in bpy.data.objects if obj.type == "MESH"]
        assert len(meshes) == expected_count
        for mesh in meshes: mesh.calc_loop_triangles()
        assert sum(len(mesh.loop_triangles) for mesh in meshes) == expected_triangles
    print(f"OPENTORQUE_SOURCE_VALIDATION_OK definitions=13 instances=19 maxMatrixError={max_error:.3g}", flush=True)
    print(f"OPENTORQUE_LOD_VALIDATION_OK source={source_triangles} LOD0={lod0} LOD1={runtime['lod1Triangles']} LOD2={runtime['lod2Triangles']} dimensions=110x110x95mm orbit=27mm", flush=True)


if __name__ == "__main__": main()
