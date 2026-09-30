"""Inspect authored GLB hierarchy, assembled geometry and animation, before integration."""

import json
import math
from pathlib import Path
import struct
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / "Library/MechMaster/CarbonFrameBikeSource"


def glb_json():
    data = (CACHE / "official/CarbonFrameBike.glb").read_bytes()
    magic, version, size = struct.unpack_from("<III", data)
    if magic != 0x46546c67 or version != 2 or size != len(data): raise ValueError("Invalid GLB")
    length, kind = struct.unpack_from("<II", data, 12)
    if kind != 0x4e4f534a: raise ValueError("Missing JSON chunk")
    return json.loads(data[20:20 + length])


def bounds(obj):
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = [min(p[a] for p in points) for a in range(3)]
    hi = [max(p[a] for p in points) for a in range(3)]
    return {"min": lo, "max": hi, "center": [(lo[a] + hi[a]) / 2 for a in range(3)],
            "size": [hi[a] - lo[a] for a in range(3)]}


def ancestor_objects(obj):
    result = []
    while obj:
        result.append(obj)
        obj = obj.parent
    return result


def main():
    out = CACHE / "audit"
    out.mkdir(parents=True, exist_ok=True)
    data = glb_json()
    parents = {child: i for i, node in enumerate(data["nodes"]) for child in node.get("children", [])}
    nodes = []
    for i, node in enumerate(data["nodes"]):
        ancestry, cursor = [], i
        while cursor in parents:
            cursor = parents[cursor]
            ancestry.append(data["nodes"][cursor].get("name", str(cursor)))
        nodes.append({"index": i, **node, "ancestry": ancestry[::-1]})
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(CACHE / "official/CarbonFrameBike.glb"))
    scene = bpy.context.scene
    scene.frame_set(0)
    objects = [o for o in scene.objects if o.type == "MESH"]
    inventory = []
    for o in objects:
        inventory.append({"name": o.name, "parent": o.parent.name if o.parent else None,
            "mesh": o.data.name, "vertices": len(o.data.vertices), "triangles": sum(len(p.vertices) - 2 for p in o.data.polygons),
            "materials": [m.name if m else None for m in o.data.materials], "bounds": bounds(o),
            "matrix": [list(row) for row in o.matrix_world], "modifiers": [m.type for m in o.modifiers]})
    physical = [o for o in objects if o.name in {n.get("name") for n in data["nodes"] if "mesh" in n}
                and not any("Shadow" in p.name or "Logo" in p.name or "logo" in p.name for p in ancestor_objects(o))]
    print("ACTUAL_PHYSICAL_BOUNDS", len(physical), [max(bounds(o)["max"][a] for o in physical) - min(bounds(o)["min"][a] for o in physical) for a in range(3)], flush=True)
    signatures = {}
    for o in physical:
        verts = tuple(sorted(tuple(round(v, 7) for v in (o.matrix_world @ vertex.co)) for vertex in o.data.vertices))
        signature = (len(o.data.polygons), verts)
        if signature in signatures: print("COINCIDENT_DUPLICATE", signatures[signature], o.name, flush=True)
        else: signatures[signature] = o.name
    transforms = [{"name": o.name, "parent": o.parent.name if o.parent else None, "type": o.type,
                   "matrix": [list(row) for row in o.matrix_world], "localMatrix": [list(row) for row in o.matrix_local],
                   "position": list(o.matrix_world.translation)} for o in scene.objects]
    (out / "audit.json").write_text(json.dumps({"nodes": nodes, "inventory": inventory, "transforms": transforms,
        "materials": data["materials"], "images": data.get("images", []),
        "actions": [{"name": a.name, "range": list(a.frame_range)} for a in bpy.data.actions]}, indent=2), encoding="utf-8")
    print("CARBON_AUDIT", len(data["nodes"]), len(data["meshes"]), len(objects), sum(o["triangles"] for o in inventory), flush=True)
    for item in inventory:
        if any(t in item["name"] for t in ("Reifen", "Daempfer", "Hauptlager_Achse", "Hauptrahmen", "Federgabel", "Logo")):
            print(item["name"], item["bounds"], flush=True)
    for frame in (0, 1, 60, 120, 240, 360, 480):
        scene.frame_set(frame)
        points = [o.matrix_world @ Vector(c) for o in physical for c in o.bound_box]
        print("FRAME", frame, [max(p[a] for p in points) - min(p[a] for p in points) for a in range(3)], flush=True)
    scene.frame_set(0)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(out / "AuthorImport.blend"))
    for o in objects:
        if o not in physical: o.hide_render = True
    points = [o.matrix_world @ Vector(c) for o in physical for c in o.bound_box]
    lo = Vector([min(p[a] for p in points) for a in range(3)])
    hi = Vector([max(p[a] for p in points) for a in range(3)])
    center, size = (lo + hi) / 2, (hi - lo).length
    bpy.ops.object.camera_add(location=center + Vector((1.2, -2.8, 1.2)) * size)
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type, camera.data.ortho_scale = "ORTHO", size * 1.1
    scene.camera = camera
    for offset, power in [((1, -2, 3), 900), ((-1, -1, 2), 500), ((0, 2, 2), 800)]:
        bpy.ops.object.light_add(type="AREA", location=center + Vector(offset) * size)
        lamp = bpy.context.object
        lamp.data.energy, lamp.data.size = power * size * size, size * 2
        lamp.rotation_euler = (center - lamp.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = 1200, 800
    scene.render.resolution_percentage = 100
    scene.world.color = (.12, .14, .18)
    scene.render.filepath = str(out / "AuthorImport.png")
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__": main()
