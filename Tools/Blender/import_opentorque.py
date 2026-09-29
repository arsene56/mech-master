"""Build standard OpenTorque source, runtime modules, LODs, content and preview."""

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
from opentorque_content import ASSEMBLIES, MODEL_ID, TYPES, write_content
from import_moveo import export, material, select, simplify, triangles, write_json
from fetch_opentorque_sources import COMMIT

CACHE = ROOT / "Library/MechMaster/OpenTorqueSource"
SOURCE = ROOT / "Assets/Art/Models/Source/OpenTorqueSource.blend"
ASSETS = ROOT / "Assets/Resources/Models/OpenTorque"
DATA = ROOT / "Assets/StreamingAssets/MechanicalCatalog"
PREVIEW = ROOT / "Docs/Preview/OpenTorque.png"


def runtime_budget(name, count):
    # Keep all involute tooth faces at LOD0; bearing internals dominate CAD cost.
    if name in ("Sun Gear", "Planet Gear", "Actuator Housing"): return count
    if name == "F625ZZ": return min(count, 1800)
    if name == "RA-8008C Cross Roller Bearing": return min(count, 2400)
    if name == "M5x30 Dowel Pin": return count
    return min(count, 6500)


def build_rig(parts):
    def one(name): return next(part["object"] for part in parts if part["sourceName"] == name)
    bindings = []
    for part in parts:
        name = part["sourceName"]
        role = "planet" if name == "Planet Gear" else "sun" if name in ("Sun Gear", "Encoder Magnet Holder") else (
            "carrier" if name.startswith("Planet Carrier") or name in ("F625ZZ", "M5x30 Dowel Pin") else "fixed")
        bindings.append({"objectName": part["object"], "role": role, "planetIndex": part["planetIndex"]})
    rig = {"schemaVersion": 1, "modelId": MODEL_ID, "sunTeeth": 9, "planetTeeth": 27, "ringTeeth": 63,
           "inputRpm": 60, "closedCycleInputTurns": 24, "orbitRadiusM": .027,
           "axisStartObject": one("Backplate"), "axisEndObject": one("Planet Carrier A"),
           "sunObject": one("Sun Gear"),
           "planetObjects": [p["object"] for p in sorted(parts, key=lambda p: p["planetIndex"]) if p["sourceName"] == "Planet Gear"],
           "transparentObjects": [p["object"] for p in parts if p["sourceName"] in (
               "Actuator Housing", "Bearing Retainer", "Backplate", "Planet Carrier A", "Planet Carrier B",
               "Planet Carrier C", "RA-8008C Cross Roller Bearing", "Encoder Cover")],
           "bindings": bindings,
           "limitations": ["Prescribed ideal kinematics, not motor, torque, contact or backlash simulation.",
                           "Sealed bearings orbit as service units; internal rolling elements are not animated.",
                           "Transparent support parts are a viewing layer; no assembly removal is recorded.",
                           "Motor, input shaft, encoder PCB, magnet and most fasteners are absent from source CAD."]}
    write_json(ROOT / "Assets/Resources/MechanicalCatalog/OpenTorqueMotionRig.json", rig)
    return rig


def render(scene, objects, rig):
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    low = Vector([min(p[a] for p in points) for a in range(3)])
    high = Vector([max(p[a] for p in points) for a in range(3)])
    center = (low + high) / 2
    bpy.ops.object.camera_add(location=center + Vector((.17, -.27, .15)))
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type, camera.data.ortho_scale = "ORTHO", (high - low).length * 1.12
    scene.camera = camera
    for offset, power in [((.2, -.3, .35), 4), ((-.22, -.12, .2), 2), ((.1, .24, .28), 4)]:
        bpy.ops.object.light_add(type="AREA", location=center + Vector(offset))
        light = bpy.context.object
        light.data.energy, light.data.size = power, .25
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = 1200, 1200
    scene.world.color = (.11, .13, .17)
    scene.render.filepath = str(PREVIEW)
    bpy.ops.render.render(write_still=True)
    # Actual exploded CAD parts, not an AI illustration or fabricated cutaway.
    matrices = {obj.name: obj.matrix_world.copy() for obj in objects}
    for obj in objects:
        name = obj["mm_source_model"]
        if name == "Actuator Housing": obj.location.y += .1
        elif name == "Bearing Retainer": obj.location.y -= .085
        elif name == "Planet Carrier A": obj.location.y -= .055
        elif name == "Planet Carrier B": obj.location.y -= .027
        elif name == "RA-8008C Cross Roller Bearing": obj.location.y -= .07
        elif name == "Backplate": obj.location.y += .035
        elif name in ("Encoder Cover", "Encoder Magnet Holder"): obj.location.y += .05
    camera.data.ortho_scale *= 1.65
    scene.render.filepath = str(PREVIEW.with_name("OpenTorqueExploded.png"))
    bpy.ops.render.render(write_still=True)
    for obj in objects: obj.matrix_world = matrices[obj.name]


def main():
    geometry = json.loads((CACHE / "converted/geometry.json").read_text(encoding="utf-8"))
    if geometry["units"] != "m" or geometry["sourceCommit"] != COMMIT:
        raise ValueError("Unexpected geometry provenance / units")
    for directory in (SOURCE.parent, ASSETS / "Modules", DATA, PREVIEW.parent): directory.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    bpy.context.preferences.filepaths.save_version = 0
    scene.unit_settings.system, scene.unit_settings.scale_length = "METRIC", 1
    mats = {"shell": material("OpenTorque printed nylon housing", (.12, .19, .24), rough=.68),
            "carrier": material("OpenTorque printed carrier plates", (.6, .65, .71), rough=.62),
            "gear": material("OpenTorque printed nylon gears", (.8, .61, .28), rough=.64),
            "steel": material("OpenTorque steel bearings and pins", (.48, .53, .6), .85, .26)}
    meshes, centers = {}, {}
    for key, definition in geometry["meshes"].items():
        raw, indices, name = definition["vertices"], definition["indices"], definition["name"]
        mesh = bpy.data.meshes.new("OpenTorque_" + name)
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
        kind = "gear" if name in ("Sun Gear", "Planet Gear") else "steel" if name in ("RA-8008C Cross Roller Bearing", "F625ZZ", "M5x30 Dowel Pin") else "carrier" if name.startswith("Planet Carrier") else "shell"
        mesh.materials.append(mats[kind])
        mesh.update()
        meshes[key], centers[key] = mesh, center
    parts, objects = [], []
    conversion = Matrix.Rotation(math.pi / 2, 4, "X")
    # Match coaxial planet bearings and pins geometrically, not traversal order.
    gear_centers = [(Matrix(i["matrix"]) @ centers[i["modelId"]]) for i in geometry["instances"]
                    if geometry["meshes"][i["modelId"]]["name"] == "Planet Gear"]
    for instance in geometry["instances"]:
        key, name = instance["modelId"], geometry["meshes"][instance["modelId"]]["name"]
        digest = hashlib.sha256(instance["sourcePath"].encode()).hexdigest()[:10]
        slug = re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_")
        obj = bpy.data.objects.new(f"MM_opentorque_{slug}_{digest}", meshes[key])
        bpy.context.collection.objects.link(obj)
        obj.rotation_mode = "QUATERNION"
        obj.matrix_world = conversion @ Matrix(instance["matrix"]) @ Matrix.Translation(centers[key])
        group, label = TYPES[name]
        obj["mm_part_id"], obj["mm_component_id"], obj["mm_assembly_id"] = "opentorque." + slug + "." + digest, key, group
        obj["mm_source_instance"], obj["mm_source_model"], obj["mm_lod"] = instance["sourcePath"], name, "source"
        obj["mm_service_boundary"] = "sealed-bearing" if name in ("RA-8008C Cross Roller Bearing", "F625ZZ") else "source-part"
        center = Matrix(instance["matrix"]) @ centers[key]
        planet_index = min(range(3), key=lambda index: (Vector(center[:2]) - Vector(gear_centers[index][:2])).length) if group == "planets" else -1
        part = {"id": obj["mm_part_id"], "object": obj.name, "componentId": key, "sourceName": name,
                "displayName": label, "sourceInstancePath": instance["sourcePath"], "assemblyId": group,
                "sourceTriangles": triangles(obj.data), "sourceMatrix": instance["matrix"], "sourceCenter": list(centers[key]),
                "planetIndex": planet_index, "serviceBoundary": obj["mm_service_boundary"]}
        parts.append(part)
        objects.append(obj)
    if len(parts) != 19 or len(meshes) != 13: raise ValueError("Unexpected source inventory")
    bpy.context.view_layer.update()
    counts = write_content(parts)
    rig = build_rig(parts)
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    reduced = {key: simplify(mesh, runtime_budget(geometry["meshes"][key]["name"], triangles(mesh))) for key, mesh in meshes.items()}
    runtime_objects = []
    for obj in objects:
        clone = obj.copy()
        clone.data = reduced[obj["mm_component_id"]].copy()
        clone["mm_lod"] = "lod0"
        bpy.context.collection.objects.link(clone)
        original_name = obj.name
        obj.name += "_source"
        clone.name = original_name
        runtime_objects.append(clone)
    runtime = []
    for group in ASSEMBLIES:
        members = [obj for obj in runtime_objects if obj["mm_assembly_id"] == group]
        path = ASSETS / "Modules" / f"{group}_LOD0.fbx"
        export(path, members)
        runtime.append({"assemblyId": group, "resourcePath": f"Models/OpenTorque/Modules/{group}_LOD0",
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
        merged.name = "MM_OpenTorque_overview_" + group
        for key in list(merged.keys()): del merged[key]
        merged.data = simplify(merged.data, max(200, int(triangles(merged.data) * .58)))
        whole.append(merged)
    export(ASSETS / "OpenTorque_LOD1.fbx", whole)
    lod1 = sum(triangles(obj.data) for obj in whole)
    select(whole)
    bpy.ops.object.join()
    merged = whole[0]
    merged.name = "MM_OpenTorque_mobile_overview"
    merged.data = simplify(merged.data, 16000)
    export(ASSETS / "OpenTorque_LOD2.fbx", [merged])
    lod2 = triangles(merged.data)
    write_json(DATA / "opentorque_model_manifest.json", {"schemaVersion": 1, "modelId": MODEL_ID, "units": "m",
               "sourceCommit": COMMIT, "stepSha256": geometry["stepSha256"], "partTypeCount": 13,
               "partObjectCount": 19, "parts": parts, "excludedReferences": [],
               "missingSourceComponents": ["motor", "input shaft", "encoder PCB", "encoder magnet", "most fasteners"]})
    write_json(DATA / "opentorque_runtime_assets.json", {"schemaVersion": 1, "modelId": MODEL_ID,
               "sourceTriangles": sum(p["sourceTriangles"] for p in parts), "modules": runtime,
               "interactionSteps": counts, "lod1Triangles": lod1, "lod2Triangles": lod2})
    write_json(DATA / "opentorque_engineering.json", {"schemaVersion": 1, "modelId": MODEL_ID, "units": "m",
               "license": "CC-BY-SA-4.0", "dimensionsMm": [110, 110, 95], "sunTeeth": 9, "planetTeeth": 27,
               "ringTeeth": 63, "reductionRatio": 8, "planetOrbitRadiusM": .027,
               "printedMaterial": "Nylon or other engineering polymer per author's print instructions",
               "types": [{"id": key, "sourceName": d["name"], "displayName": TYPES[d["name"]][1],
                          "quantity": sum(p["componentId"] == key for p in parts)} for key, d in geometry["meshes"].items()],
               "limitations": rig["limitations"]})
    notices = DATA / "OpenTorque"
    notices.mkdir(exist_ok=True)
    shutil.copyfile(CACHE / "official/LICENSE", notices / "CC-BY-SA-4.0-LICENSE.txt")
    shutil.copyfile(CACHE / "source_manifest.json", notices / "source_manifest.json")
    (notices / "ATTRIBUTION.txt").write_text(
        "OpenTorque Actuator — Gabrael Levine\n"
        "Source: https://github.com/G-Levine/OpenTorque-Actuator/tree/" + COMMIT + "\n"
        "License: Creative Commons Attribution-ShareAlike 4.0 International\n"
        "https://creativecommons.org/licenses/by-sa/4.0/\n\n"
        "Mech.Master modifications (2026-09-29): STEP tessellation in metres, preserved occurrence transforms, "
        "Chinese service groups, polymer/metal display materials, bearing mesh simplification, runtime LODs "
        "and teaching previews. These adapted OpenTorque model assets and previews are CC BY-SA 4.0.\n"
        "This notice does not relicense independent game code or other models. No endorsement is implied.\n",
        encoding="utf-8")
    for obj in objects + [merged]: bpy.data.objects.remove(obj, do_unlink=True)
    render(scene, runtime_objects, rig)
    print("OPENTORQUE_IMPORT_OK", counts, "source", sum(p["sourceTriangles"] for p in parts),
          "LOD0", sum(r["triangles"] for r in runtime), "LOD1", lod1, "LOD2", lod2, flush=True)


if __name__ == "__main__":
    main()
