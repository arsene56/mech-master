"""Verify source fidelity, material/mark removal, FBX names/poses and LODs."""

import json
from pathlib import Path
import sys
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/Blender"))
from audit_carbon_frame_bike import glb_json
from import_moveo import triangles


def read(path): return json.loads(path.read_text(encoding="utf-8"))


def clear():
    # glTF armature visualization shapes can be hidden/unselected.
    for obj in list(bpy.data.objects): bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.orphans_purge(do_recursive=True)


def main():
    data = ROOT / "Assets/StreamingAssets/MechanicalCatalog"
    manifest = read(data / "carbon_frame_bike_model_manifest.json")
    runtime = read(data / "carbon_frame_bike_runtime_assets.json")
    rig = read(ROOT / "Assets/Resources/MechanicalCatalog/CarbonFrameBikeMotionRig.json")
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / "Assets/Art/Models/Source/CarbonFrameBikeSource.blend"))
    named = {o.name: o for o in bpy.data.objects if o.type == "MESH"}
    assert len(named) == 307 and all(image.type == "RENDER_RESULT" for image in bpy.data.images)
    source_positions, source_vertices = {}, {}
    for part in manifest["parts"]:
        o = named[part["object"]]
        assert o["mm_part_id"] == part["id"] and o["mm_source_node"] == part["sourceNodeIndex"]
        assert triangles(o.data) == part["sourceTriangles"] > 0
        assert o["mm_motion_role"] == part["motionRole"]
        assert (o.location - Vector(part["centerM"])).length < 1e-7
        assert o.parent is None and not o.modifiers and o.animation_data is None
        assert all(m.name.startswith("CarbonBike ") and not any(n.type == "TEX_IMAGE" for n in m.node_tree.nodes) for m in o.data.materials)
        source_positions[o.name] = o.matrix_world.copy()
        source_vertices[part["sourceName"]] = [o.matrix_world @ v.co for v in o.data.vertices]
    a, b = bpy.data.objects[rig["shockUpperAnchor"]].location, bpy.data.objects[rig["shockLowerAnchor"]].location
    assert abs((a - b).length - .2) < .00015
    assert runtime["sourceTriangles"] == runtime["lod0Triangles"] == 114871
    assert runtime["lod0Triangles"] > runtime["lod1Triangles"] > runtime["lod2Triangles"] == 22000
    # Compare every vertex against the actual authored, evaluated closed GLB.
    clear()
    bpy.ops.import_scene.gltf(filepath=str(ROOT / "Library/MechMaster/CarbonFrameBikeSource/official/CarbonFrameBike.glb"))
    bpy.context.scene.frame_set(0)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for name, expected in source_vertices.items():
        original = bpy.data.objects[name]
        evaluated = original.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        points = [original.matrix_world @ v.co for v in mesh.vertices]
        assert len(points) == len(expected)
        error = max((p - q).length for p, q in zip(points, expected))
        assert error < 3e-7, (name, error)
        evaluated.to_mesh_clear()
    clear()
    for module in runtime["modules"]:
        path = ROOT / "Assets/Resources" / (module["resourcePath"] + ".fbx")
        assert path.stat().st_size == module["bytes"]
        bpy.ops.import_scene.fbx(filepath=str(path))
    imported = {o.name: o for o in bpy.data.objects if o.type == "MESH"}
    assert set(imported) == set(source_positions), (set(imported) - set(source_positions), set(source_positions) - set(imported))
    for name, o in imported.items():
        assert max(abs(o.matrix_world[r][c] - source_positions[name][r][c]) for r in range(4) for c in range(4)) < 1e-6, name
    for key in ("rearPivotAnchor", "rearAxisAnchor", "rearWheelAnchor", "shockUpperAnchor", "shockLowerAnchor", "forkAxisStartAnchor", "forkAxisEndAnchor"):
        assert rig[key] in bpy.data.objects, key
    for suffix, count, face_count in (("LOD1", 14, runtime["lod1Triangles"]), ("LOD2", 1, runtime["lod2Triangles"])):
        clear()
        bpy.ops.import_scene.fbx(filepath=str(ROOT / f"Assets/Resources/Models/CarbonFrameBike/CarbonFrameBike_{suffix}.fbx"))
        meshes = [o.data for o in bpy.data.objects if o.type == "MESH"]
        assert len(meshes) == count and sum(triangles(m) for m in meshes) == face_count
    print("CARBON_SOURCE_LOD_VALIDATION_OK 307 occurrences; original GLB vertices; no marks/maps; metre pose; LOD0/1/2", flush=True)


if __name__ == "__main__": main()
