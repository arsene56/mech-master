"""Build the Moveo source, separable runtime LODs, Chinese catalog and preview.

Run fetch_moveo_sources.py and convert_moveo_solidworks.py first. Input geometry
is the official saved assembly mesh, in metres, with its original transforms.
"""

import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/Content"))
from moveo_content import ASSEMBLIES, FASTENERS, MODEL_ID, assembly_from_ancestry, role, type_name, write_runtime_content

CACHE = ROOT / "Library/MechMaster/MoveoSource"
SOURCE = ROOT / "Assets/Art/Models/Source/MoveoSource.blend"
ASSETS = ROOT / "Assets/Resources/Models/Moveo"
DATA = ROOT / "Assets/StreamingAssets/MechanicalCatalog"
PREVIEW = ROOT / "Docs/Preview/Moveo.png"


def material(name, color, metal=0, rough=.4):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = next(node for node in mat.node_tree.nodes if node.type == "BSDF_PRINCIPLED")
    shader.inputs["Base Color"].default_value = (*color, 1)
    shader.inputs["Metallic"].default_value = metal
    shader.inputs["Roughness"].default_value = rough
    return mat


def choose_material(name, mats):
    if name == "Base fusta": return mats["wood"]
    if name == "PCB TB6560": return mats["pcb"]
    if name.startswith(("Nema", "Servo Futaba", "Capacitor", "Chip", "Fan")) and name != "Fan Module": return mats["black"]
    if name == "Terminal": return mats["terminal"]
    if FASTENERS.match(name) or any(token in name for token in ("Bearing", "Spacer", "bar", "llisa", "Coupling", "Pulley", "Arandela", "Sink")):
        return mats["brass"] if name.startswith("Brass") else mats["metal"]
    if name.startswith(("Tapa", "T2", "T3", "T4")) or "Gear" in name or name in ("1M2A", "Potes base B", "Cilinder"):
        return mats["orange"]
    return mats["polymer"]


def triangles(mesh):
    mesh.calc_loop_triangles()
    return len(mesh.loop_triangles)


def select(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0] if objects else None


def export(path, objects):
    select(objects)
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False,
                             mesh_smooth_type="FACE", path_mode="STRIP", use_custom_props=True)


def simplify(mesh, budget):
    obj = bpy.data.objects.new("TemporaryLOD", mesh.copy())
    bpy.context.collection.objects.link(obj)
    select([obj])
    count = triangles(obj.data)
    if count > budget:
        modifier = obj.modifiers.new("CAD display mesh reduction", "DECIMATE")
        modifier.ratio = budget / count
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    result = obj.data
    bpy.data.objects.remove(obj, do_unlink=True)
    return result


def budget(name, source_triangles):
    if FASTENERS.match(name): return min(source_triangles, 500)
    if "Bearing" in name: return min(source_triangles, 3200)
    if name.startswith(("Nema", "Servo Futaba")): return min(source_triangles, 5000)
    if "Gear" in name: return min(source_triangles, 4500)
    return min(source_triangles, 10000)


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")


def main():
    geometry = json.loads((CACHE / "converted/geometry.json").read_text(encoding="utf-8"))
    for directory in (SOURCE.parent, ASSETS / "Modules", DATA, PREVIEW.parent):
        directory.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    bpy.context.preferences.filepaths.save_version = 0
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    mats = {
        "polymer": material("Moveo printed polymer", (.055, .11, .16), rough=.5),
        "orange": material("Moveo contrasting polymer", (.86, .32, .035), rough=.43),
        "metal": material("Moveo steel", (.48, .53, .58), .75, .27),
        "brass": material("Moveo brass inserts", (.65, .4, .12), .72, .32),
        "wood": material("Moveo wooden base", (.52, .34, .16), rough=.65),
        "black": material("Moveo purchased modules", (.035, .043, .055), .25, .4),
        "pcb": material("Moveo driver PCB", (.028, .21, .09), rough=.48),
        "terminal": material("Moveo screw terminals", (.028, .28, .43), rough=.5),
    }
    centers, meshes = {}, {}
    for key, definition in geometry["meshes"].items():
        raw, indices = definition["vertices"], definition["indices"]
        mesh = bpy.data.meshes.new("Moveo_" + definition["name"])
        mesh.from_pydata([raw[i:i + 3] for i in range(0, len(raw), 3)], [],
                         [indices[i:i + 3] for i in range(0, len(indices), 3)])
        bm = bmesh.new()
        bm.from_mesh(mesh)
        # CAD strips duplicate edge vertices; weld at one micron and discard
        # strip connector triangles before calculating smooth normals.
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
        bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=list(bm.edges))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        for face in bm.faces:
            face.smooth = True
        for edge in bm.edges:
            edge.smooth = edge.is_manifold and edge.calc_face_angle(0) < math.radians(40)
        bm.to_mesh(mesh)
        bm.free()
        low = Vector([min(v.co[a] for v in mesh.vertices) for a in range(3)])
        high = Vector([max(v.co[a] for v in mesh.vertices) for a in range(3)])
        center = (low + high) / 2
        for vertex in mesh.vertices:
            vertex.co -= center
        mesh.materials.append(choose_material(definition["name"], mats))
        for polygon in mesh.polygons:
            polygon.use_smooth = True
        mesh.update()
        centers[key], meshes[key] = center, mesh

    parts = []
    objects = []
    cad_to_blender = Matrix.Rotation(math.pi / 2, 4, "X")
    for instance in geometry["instances"]:
        key = instance["modelId"]
        definition = geometry["meshes"][key]
        digest = hashlib.sha256(instance["sourcePath"].encode()).hexdigest()[:10]
        slug = re.sub(r"[^a-z0-9]+", "_", definition["name"].lower()).strip("_")[:31]
        obj = bpy.data.objects.new(f"MM_moveo_{slug}_{digest}", meshes[key])
        bpy.context.collection.objects.link(obj)
        obj.matrix_world = cad_to_blender @ Matrix(instance["matrix"]) @ Matrix.Translation(centers[key])
        assembly = assembly_from_ancestry(instance["ancestry"])
        obj["mm_part_id"] = "moveo." + slug + "." + digest
        obj["mm_component_id"] = key
        obj["mm_source_instance"] = instance["sourcePath"]
        obj["mm_source_model"] = definition["name"]
        obj["mm_service_boundary"] = "purchased-module" if definition["name"].startswith(("Nema", "Servo")) or "Bearing" in definition["name"] else "removable-part"
        part = {"id": obj["mm_part_id"], "object": obj.name, "componentId": key,
                "sourceName": definition["name"], "displayName": type_name(definition["name"]),
                "sourceInstancePath": instance["sourcePath"], "assemblyId": assembly,
                "sourceTriangles": triangles(obj.data), "sourceMatrix": instance["matrix"]}
        parts.append(part)
        objects.append(obj)
    bpy.context.view_layer.update()

    # Top-level fasteners have no functional assembly in SolidWorks. Assign
    # them to the closest actual support surface, retaining the source pose.
    supports = []
    for obj, part in zip(objects, parts):
        if not part["assemblyId"] or FASTENERS.match(part["sourceName"]):
            continue
        vertices = [obj.matrix_world @ vertex.co for vertex in obj.data.vertices]
        polygons = [tuple(poly.vertices) for poly in obj.data.polygons]
        supports.append((BVHTree.FromPolygons(vertices, polygons), part))
    for obj, part in zip(objects, parts):
        if not part["assemblyId"]:
            candidates = [(tree.find_nearest(obj.location)[3], host) for tree, host in supports]
            distance, host = min(candidates, key=lambda item: item[0])
            part["assemblyId"] = host["assemblyId"]
            part["classificationHost"] = host["object"]
            part["classificationDistanceM"] = distance
        part["role"] = role(part["assemblyId"], part["sourceName"])
        obj["mm_assembly_id"] = part["assemblyId"]
        obj["mm_lod"] = "source"
    if len(parts) != 366 or len({part["object"] for part in parts}) != 366:
        raise ValueError("Invalid stable part inventory")
    counts = write_runtime_content(parts)
    sys.path.insert(0, str(ROOT / "Tools/Blender"))
    from build_moveo_motion_rig import build_rig
    build_rig(objects, parts)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    print("SOURCE: 366 objects /", sum(part["sourceTriangles"] for part in parts), "triangles", flush=True)

    # Every runtime LOD0 node preserves its source instance ID and centre.
    reduced = {key: simplify(mesh, budget(geometry["meshes"][key]["name"], triangles(mesh)))
               for key, mesh in meshes.items()}
    runtime_objects = []
    for obj in objects:
        clone = obj.copy()
        # Unique export meshes avoid Blender FBX's material-slot ambiguity for
        # linked instances split across separate module files.
        clone.data = reduced[obj["mm_component_id"]].copy()
        clone.name = obj.name + "_runtime"
        clone["mm_lod"] = "lod0"
        bpy.context.collection.objects.link(clone)
        runtime_objects.append(clone)
    # Rename only the local export copies so FBX binds to stable source names.
    for original, clone in zip(objects, runtime_objects):
        original.name += "_source"
        clone.name = original.name.removesuffix("_source")
    runtime = []
    for assembly in ASSEMBLIES:
        members = [obj for obj in runtime_objects if obj["mm_assembly_id"] == assembly]
        if not members: raise ValueError("Empty mechanical module: " + assembly)
        path = ASSETS / "Modules" / f"{assembly}_LOD0.fbx"
        export(path, members)
        runtime.append({"assemblyId": assembly, "resourcePath": f"Models/Moveo/Modules/{assembly}_LOD0",
                        "partObjects": len(members), "triangles": sum(triangles(obj.data) for obj in members),
                        "bytes": path.stat().st_size})
    for part, obj in zip(parts, runtime_objects):
        part["runtimeTriangles"] = triangles(obj.data)

    whole = []
    for assembly in ASSEMBLIES:
        members = []
        for original in runtime_objects:
            if original["mm_assembly_id"] != assembly: continue
            clone = original.copy()
            clone.data = original.data.copy()
            bpy.context.collection.objects.link(clone)
            members.append(clone)
        select(members)
        bpy.ops.object.join()
        merged = members[0]
        merged.name = "MM_Moveo_overview_" + assembly
        for key in list(merged.keys()): del merged[key]
        merged.data = simplify(merged.data, max(300, int(triangles(merged.data) * .6)))
        whole.append(merged)
    export(ASSETS / "Moveo_LOD1.fbx", whole)
    lod1_triangles = sum(triangles(obj.data) for obj in whole)
    select(whole)
    bpy.ops.object.join()
    merged = whole[0]
    merged.name = "MM_Moveo_mobile_overview"
    merged.data = simplify(merged.data, 35000)
    export(ASSETS / "Moveo_LOD2.fbx", [merged])
    write_json(DATA / "moveo_model_manifest.json", {"schemaVersion": 1, "modelId": MODEL_ID,
               "units": "m", "sourceCommit": geometry["sourceCommit"], "assemblySha256": geometry["assemblySha256"],
               "partTypeCount": len(meshes), "partObjectCount": len(parts), "parts": parts,
               "excludedReferences": geometry["excludedReferences"]})
    write_json(DATA / "moveo_runtime_assets.json", {"schemaVersion": 1, "modelId": MODEL_ID,
               "modules": runtime, "interactionSteps": counts, "lod1Triangles": lod1_triangles,
               "lod2Triangles": triangles(merged.data)})
    write_json(DATA / "moveo_engineering.json", {"schemaVersion": 1, "modelId": MODEL_ID,
               "source": "BCN3D/BCN3D-Moveo", "license": "MIT", "units": "m",
               "types": [{"id": key, "sourceName": definition["name"], "displayName": type_name(definition["name"]),
                          "sourceFile": definition["sourceFile"], "quantity": sum(part["componentId"] == key for part in parts)}
                         for key, definition in geometry["meshes"].items()],
               "limitations": ["Saved tessellation, not parametric solids or manufacturing tolerances.",
                               "Official assembly has no complete belt or wiring harness geometry.",
                               "Sealed motors, servos and bearings remain complete service units; soldered electronics move with their PCB."]})
    notices = DATA / "Moveo"
    notices.mkdir(exist_ok=True)
    shutil.copyfile(CACHE / "official/LICENSE", notices / "BCN3D-MIT-LICENSE.txt")
    provenance = json.loads((CACHE / "source_manifest.json").read_text(encoding="utf-8"))
    provenance["sources"] = [item for item in provenance["sources"] if item["name"] == "official"]
    write_json(notices / "source_manifest.json", provenance)

    # Render only the LOD0 assets used by the game, for honest visual review.
    for obj in objects + [merged]: bpy.data.objects.remove(obj, do_unlink=True)
    points = [obj.matrix_world @ Vector(corner) for obj in runtime_objects for corner in obj.bound_box]
    low = Vector([min(point[a] for point in points) for a in range(3)])
    high = Vector([max(point[a] for point in points) for a in range(3)])
    center = (low + high) / 2
    bpy.ops.object.camera_add(location=center + Vector((1.2, -1.7, 1)))
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = (high - low).length * 1.07
    scene.camera = camera
    for offset, power in [((1, -1, 2), 230), ((-1, -.5, 1), 170), ((0, 1, 1.5), 230)]:
        bpy.ops.object.light_add(type="AREA", location=center + Vector(offset))
        light = bpy.context.object
        light.data.energy = power
        light.data.size = 2
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1100
    scene.world.color = (.11, .12, .14)
    scene.render.filepath = str(PREVIEW)
    bpy.ops.render.render(write_still=True)
    print("MOVEO IMPORT COMPLETE", counts, "LOD0", sum(record["triangles"] for record in runtime), flush=True)


if __name__ == "__main__":
    main()
