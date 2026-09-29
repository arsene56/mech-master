"""Build Bolt's source, named LOD0 modules, LODs, catalog and real previews.

Run fetch_bolt_sources.py and convert_bolt_step.py first. CAD transforms are
preserved; one game object is made per leaf occurrence, not per solid shell.
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

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/Content"))
sys.path.insert(0, str(ROOT / "Tools/Blender"))
from bolt_content import ASSEMBLIES, FASTENERS, MODEL_ID, assembly, role, type_name, write_content
from build_bolt_motion_rig import build_rig
from import_moveo import export, material, select, simplify, triangles, write_json

CACHE = ROOT / "Library/MechMaster/BoltSource"
SOURCE = ROOT / "Assets/Art/Models/Source/BoltSource.blend"
ASSETS = ROOT / "Assets/Resources/Models/Bolt"
DATA = ROOT / "Assets/StreamingAssets/MechanicalCatalog"
PREVIEW = ROOT / "Docs/Preview/Bolt.png"


def choose_material(name, mats):
    if name.startswith("bearing_") or FASTENERS.match(name) or name.startswith("pin_"): return mats["steel"]
    if name.startswith("transmission_timing_belt") or name == "foot_silicone_tube_elements": return mats["rubber"]
    if name.startswith("motor_"): return mats["copper"] if name.endswith("stator") else mats["brass"] if "brass" in name else mats["motor"]
    if name in ("master_board", "micro_driver_90_deg_hirose"): return mats["pcb"]
    if name == "vicon_marker_10mm": return mats["marker"]
    if name.startswith("transmission_pulley_at3_t10"): return mats["steel"]
    if name.startswith(("encoder", "imu_")): return mats["sensor"]
    if name.startswith("transmission_"): return mats["polymer"]
    return mats["shell"]


def runtime_budget(name, count):
    if FASTENERS.match(name): return min(count, 350)
    if name.startswith("bearing_"): return min(count, 800)
    if name.startswith("motor_"): return min(count, 2000)
    if name.startswith("transmission_"): return min(count, 1800)
    return min(count, 6500)


def render(scene, objects, rig, parts):
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    low = Vector([min(point[a] for point in points) for a in range(3)])
    high = Vector([max(point[a] for point in points) for a in range(3)])
    center = (low + high) / 2
    bpy.ops.object.camera_add(location=center + Vector((.7, -1.1, .55)))
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = (high - low).length * 1.16
    scene.camera = camera
    for offset, power in [((.6, -.9, 1), 100), ((-.7, -.4, .6), 70), ((0, .7, .8), 100)]:
        bpy.ops.object.light_add(type="AREA", location=center + Vector(offset))
        light = bpy.context.object
        light.data.energy, light.data.size = power, 1.1
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = 1200, 1200
    scene.world.color = (.09, .11, .14)
    scene.render.filepath = str(PREVIEW)
    bpy.ops.render.render(write_still=True)
    named = {obj.name: obj for obj in objects}
    original = {obj.name: obj.matrix_world.copy() for obj in objects}
    joints = {}
    for definition in rig["joints"]:
        pivot = named[definition["pivotObject"]].location.copy()
        if definition["axisObject"]:
            obj = named[definition["axisObject"]]
            size = obj.dimensions
            mesh_size = Vector([max(v.co[a] for v in obj.data.vertices) - min(v.co[a] for v in obj.data.vertices) for a in range(3)])
            direction = Vector([int(a == max(range(3), key=lambda i: mesh_size[i])) for a in range(3)])
            axis = (obj.matrix_world.to_3x3() @ direction).normalized()
        else:
            axis = (named[definition["axisEndObject"]].location - named[definition["axisStartObject"]].location).normalized()
        joints[definition["id"]] = (pivot, axis * definition["axisSign"], definition["parentId"])
    for index, keyframe in enumerate((min(rig["keyframes"], key=lambda frame: abs(frame["phase"] - phase))
                                      for phase in (.25, .75))):
        transforms = {"fixed": Matrix.Identity(4), "": Matrix.Identity(4)}
        for definition, angle in zip(rig["joints"], keyframe["angles"]):
            pivot, axis, parent = joints[definition["id"]]
            transforms[definition["id"]] = transforms[parent] @ Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(angle), 4, axis) @ Matrix.Translation(-pivot)
        for binding in rig["bindings"]:
            named[binding["objectName"]].matrix_world = transforms[binding["jointId"]] @ original[binding["objectName"]]
        scene.render.filepath = str(PREVIEW.with_name("BoltMotion" + str(index + 1) + ".png"))
        bpy.ops.render.render(write_still=True)
    for obj in objects: obj.matrix_world = original[obj.name]


def main():
    geometry = json.loads((CACHE / "converted/geometry.json").read_text(encoding="utf-8"))
    if geometry["units"] != "m": raise ValueError("Expected metre geometry")
    for directory in (SOURCE.parent, ASSETS / "Modules", DATA, PREVIEW.parent): directory.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    bpy.context.preferences.filepaths.save_version = 0
    scene.unit_settings.system, scene.unit_settings.scale_length = "METRIC", 1
    mats = {"shell": material("Bolt printed structure", (.78, .8, .82), rough=.5),
            "polymer": material("Bolt printed transmission", (.07, .085, .1), rough=.45),
            "steel": material("Bolt steel hardware", (.5, .54, .6), .8, .26),
            "motor": material("Bolt motor rotor", (.075, .09, .11), .6, .3),
            "copper": material("Bolt motor stator", (.46, .24, .12), .65, .35),
            "brass": material("Bolt brass spacer", (.6, .4, .14), .75, .28),
            "pcb": material("Bolt control PCB", (.035, .23, .12), rough=.46),
            "sensor": material("Bolt position sensors", (.025, .034, .055), .25, .35),
            "marker": material("Bolt reflective markers", (.88, .88, .88), .4, .2),
            "rubber": material("Bolt belts and contact rubber", (.018, .022, .028), rough=.72)}
    meshes, centers = {}, {}
    for key, definition in geometry["meshes"].items():
        raw, indices = definition["vertices"], definition["indices"]
        mesh = bpy.data.meshes.new("Bolt_" + definition["name"])
        mesh.from_pydata([raw[i:i + 3] for i in range(0, len(raw), 3)], [], [indices[i:i + 3] for i in range(0, len(indices), 3)])
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
        bmesh.ops.dissolve_degenerate(bm, dist=1e-8, edges=list(bm.edges))
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        for face in bm.faces: face.smooth = True
        for edge in bm.edges: edge.smooth = edge.is_manifold and edge.calc_face_angle(0) < math.radians(35)
        bm.to_mesh(mesh)
        bm.free()
        low = Vector([min(v.co[a] for v in mesh.vertices) for a in range(3)])
        high = Vector([max(v.co[a] for v in mesh.vertices) for a in range(3)])
        center = (low + high) / 2
        for vertex in mesh.vertices: vertex.co -= center
        mesh.materials.append(choose_material(definition["name"], mats))
        mesh.update()
        meshes[key], centers[key] = mesh, center
    parts, objects = [], []
    conversion = Matrix.Rotation(math.pi / 2, 4, "X")
    for instance in geometry["instances"]:
        key = instance["modelId"]
        definition = geometry["meshes"][key]
        digest = hashlib.sha256(instance["sourcePath"].encode()).hexdigest()[:10]
        slug = re.sub(r"[^a-z0-9]+", "_", definition["name"].lower()).strip("_")[:36]
        obj = bpy.data.objects.new(f"MM_bolt_{slug}_{digest}", meshes[key])
        bpy.context.collection.objects.link(obj)
        # Avoid Euler decomposition error at the CAD's many near-90-degree poses.
        obj.rotation_mode = "QUATERNION"
        obj.matrix_world = conversion @ Matrix(instance["matrix"]) @ Matrix.Translation(centers[key])
        group = assembly(instance, definition["name"])
        obj["mm_part_id"] = "bolt." + slug + "." + digest
        obj["mm_component_id"], obj["mm_assembly_id"] = key, group
        obj["mm_source_instance"], obj["mm_source_model"] = instance["sourcePath"], definition["name"]
        obj["mm_lod"] = "source"
        part = {"id": obj["mm_part_id"], "object": obj.name, "componentId": key,
                "sourceName": definition["name"], "displayName": type_name(definition["name"]),
                "sourceInstancePath": instance["sourcePath"], "ancestry": instance["ancestry"],
                "assemblyId": group, "sourceTriangles": triangles(obj.data),
                "sourceMatrix": instance["matrix"], "sourceCenter": list(centers[key])}
        part["role"] = role(part)
        obj["mm_service_boundary"] = "sealed-bearing" if definition["name"].startswith("bearing_") else "grouped-service-part"
        parts.append(part)
        objects.append(obj)
    if len(parts) != 345 or len(meshes) != 56: raise ValueError("Unexpected pinned STEP inventory")
    bpy.context.view_layer.update()
    rig = build_rig(objects, parts)
    counts = write_content(parts)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    reduced = {key: simplify(mesh, runtime_budget(geometry["meshes"][key]["name"], triangles(mesh))) for key, mesh in meshes.items()}
    runtime_objects = []
    for obj in objects:
        clone = obj.copy()
        clone.data = reduced[obj["mm_component_id"]].copy()
        clone.name = obj.name + "_runtime"
        clone["mm_lod"] = "lod0"
        bpy.context.collection.objects.link(clone)
        runtime_objects.append(clone)
    for obj, clone in zip(objects, runtime_objects):
        obj.name += "_source"
        clone.name = obj.name.removesuffix("_source")
    runtime = []
    for group in ASSEMBLIES:
        members = [obj for obj in runtime_objects if obj["mm_assembly_id"] == group]
        if not members: raise ValueError("Empty source module: " + group)
        path = ASSETS / "Modules" / f"{group}_LOD0.fbx"
        export(path, members)
        runtime.append({"assemblyId": group, "resourcePath": f"Models/Bolt/Modules/{group}_LOD0",
                        "partObjects": len(members), "triangles": sum(triangles(obj.data) for obj in members), "bytes": path.stat().st_size})
    for part, obj in zip(parts, runtime_objects): part["runtimeTriangles"] = triangles(obj.data)
    whole = []
    for group in ASSEMBLIES:
        members = []
        for original in runtime_objects:
            if original["mm_assembly_id"] != group: continue
            clone = original.copy()
            clone.data = original.data.copy()
            bpy.context.collection.objects.link(clone)
            members.append(clone)
        select(members)
        bpy.ops.object.join()
        merged = members[0]
        merged.name = "MM_Bolt_overview_" + group
        for key in list(merged.keys()): del merged[key]
        merged.data = simplify(merged.data, max(200, int(triangles(merged.data) * .55)))
        whole.append(merged)
    export(ASSETS / "Bolt_LOD1.fbx", whole)
    lod1 = sum(triangles(obj.data) for obj in whole)
    select(whole)
    bpy.ops.object.join()
    merged = whole[0]
    merged.name = "MM_Bolt_mobile_overview"
    merged.data = simplify(merged.data, 22000)
    export(ASSETS / "Bolt_LOD2.fbx", [merged])
    lod2 = triangles(merged.data)
    write_json(DATA / "bolt_model_manifest.json", {"schemaVersion": 1, "modelId": MODEL_ID,
               "units": "m", "sourceCommit": geometry["sourceCommit"], "stepSha256": geometry["stepSha256"],
               "partTypeCount": len(meshes), "partObjectCount": len(parts), "parts": parts, "excludedReferences": []})
    write_json(DATA / "bolt_runtime_assets.json", {"schemaVersion": 1, "modelId": MODEL_ID,
               "sourceTriangles": sum(part["sourceTriangles"] for part in parts),
               "modules": runtime, "interactionSteps": counts, "lod1Triangles": lod1, "lod2Triangles": lod2})
    write_json(DATA / "bolt_engineering.json", {"schemaVersion": 1, "modelId": MODEL_ID,
               "source": "open-dynamic-robot-initiative/open_robot_actuator_hardware", "license": "BSD-3-Clause",
               "units": "m", "activeJoints": 6, "passiveJoints": 2, "dimensions": rig["dimensions"],
               "types": [{"id": key, "sourceName": definition["name"], "displayName": type_name(definition["name"]),
                          "quantity": sum(part["componentId"] == key for part in parts)} for key, definition in geometry["meshes"].items()],
               "limitations": ["Pinned STEP snapshot, not manufacturing tolerances or a purchase BOM.",
                               "Six active joints and two passive ankles; fixed-body teaching poses, not balanced walking.",
                               "Complete wires, control algorithms, motor dynamics and ground contact are not simulated.",
                               "Electronics, bearings, motor input assemblies and repeated hardware retain service grouping."]})
    notices = DATA / "Bolt"
    notices.mkdir(exist_ok=True)
    shutil.copyfile(CACHE / "official/LICENSE", notices / "ODRI-BSD-3-Clause-LICENSE.txt")
    shutil.copyfile(CACHE / "source_manifest.json", notices / "source_manifest.json")
    for obj in objects + [merged]: bpy.data.objects.remove(obj, do_unlink=True)
    render(scene, runtime_objects, rig, parts)
    print("BOLT_IMPORT_OK", counts, "source", sum(part["sourceTriangles"] for part in parts),
          "LOD0", sum(record["triangles"] for record in runtime), "LOD1", lod1, "LOD2", lod2, flush=True)


if __name__ == "__main__":
    main()
