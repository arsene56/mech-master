"""Reproducible pinned GLB -> unbranded, metre-scale service meshes and LODs."""

import hashlib
import json
import math
from pathlib import Path
import shutil
import sys

import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools/Content"))
sys.path.insert(0, str(ROOT / "Tools/Blender"))
from carbon_frame_bike_content import ASSEMBLIES, MODEL_ID, classify, write_content
from fetch_carbon_frame_bike_sources import CACHE, COMMIT
from audit_carbon_frame_bike import glb_json, bounds
from import_moveo import export, material, select, simplify, triangles, write_json

SOURCE = ROOT / "Assets/Art/Models/Source/CarbonFrameBikeSource.blend"
ASSETS = ROOT / "Assets/Resources/Models/CarbonFrameBike"
DATA = ROOT / "Assets/StreamingAssets/MechanicalCatalog"
PREVIEW = ROOT / "Docs/Preview/CarbonFrameBike.png"


def neutral_material(source, materials):
    name = source.name.split(".00")[0]
    if name in materials: return materials[name]
    shader = next(n for n in source.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    color = tuple(shader.inputs["Base Color"].default_value[:3])
    metal, rough = shader.inputs["Metallic"].default_value, shader.inputs["Roughness"].default_value
    # Replace ALL authored image/procedural nodes with texture-free neutral PBR.
    # This removes wheel wordmarks as well as logo decals without bitmap editing.
    if name in ("Reifen", "Reifen_innen", "GummiDunkel", "Schlauch"):
        color, metal, rough = (.018, .022, .027), 0, .68
    elif name == "Carbon": color, metal, rough = (.033, .042, .052), 0, .44
    elif name == "Griffe": color, metal, rough = (.035, .043, .052), 0, .73
    elif name == "Getriebe": color, metal, rough = (.11, .13, .15), .72, .34
    elif name == "Rahmen": color, metal, rough = (.24, .29, .34), .8, .31
    elif name == "Sattel": color, metal, rough = (.024, .032, .045), 0, .64
    elif name == "FelgenStab": metal, rough = .65, .33
    materials[name] = material("CarbonBike " + name, color, metal, rough)
    return materials[name]


def motion_role(node):
    text = "/".join(node["ancestry"] + [node["name"]])
    # Keep the authored one-piece chain visible in the suspension lesson.
    # Only flexible lines need hiding until their deformation is modeled.
    if node["name"].startswith(("Schlauch", "Bremsschlauch")): return "hidden"
    if "Daempferaufnahme_oben" in text: return "fixed"
    if "Daempfer_Cane-Creek" in text:
        return "shock_upper" if any("_" + suffix in text for suffix in ("30305", "148918", "149470")) else "shock_lower"
    if "Federung" in node["ancestry"]: return "front_lower"
    if "Federachse" in node["ancestry"] and "Hauptlager_Achse" not in text: return "rear"
    return "fixed"


def render(scene, objects, rig, anchors):
    points = [o.matrix_world @ v.co for o in objects for v in o.data.vertices]
    lo, hi = Vector([min(p[a] for p in points) for a in range(3)]), Vector([max(p[a] for p in points) for a in range(3)])
    center, size = (lo + hi) / 2, (hi - lo).length
    bpy.ops.object.camera_add(location=center + Vector((.7, -2.8, 1.1)) * size)
    camera = bpy.context.object
    camera.rotation_euler = (center - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    view = camera.rotation_euler.to_matrix().transposed()
    projected = [view @ (p - center) for p in points]
    camera.data.ortho_scale = max(max(p.x for p in projected) - min(p.x for p in projected),
        1.6 * (max(p.y for p in projected) - min(p.y for p in projected))) * 1.14
    scene.camera = camera
    for offset, power in [((1, -2, 3), 130), ((-1, -1, 2), 80), ((0, 2, 2), 110)]:
        bpy.ops.object.light_add(type="AREA", location=center + Vector(offset) * size)
        light = bpy.context.object
        light.data.energy, light.data.size = power * size * size, size * 1.8
        light.rotation_euler = (center - light.location).to_track_quat("-Z", "Y").to_euler()
    scene.world.color = (.12, .14, .18)
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
    scene.render.resolution_percentage = 100
    scene.render.filepath = str(PREVIEW)
    bpy.ops.render.render(write_still=True)
    matrices = {o.name: o.matrix_world.copy() for o in objects}
    pivot = anchors["rearPivotAnchor"].location
    axis = (anchors["rearAxisAnchor"].location - pivot).normalized()
    rotation = Matrix.Rotation(math.radians(rig["rearMaximumDegrees"]), 4, axis)
    a, b = anchors["shockUpperAnchor"].location, anchors["shockLowerAnchor"].location
    moving_b = pivot + rotation.to_3x3() @ (b - pivot)
    shock_rotation = (a - b).rotation_difference(a - moving_b).to_matrix().to_4x4()
    front = (anchors["forkAxisEndAnchor"].location - anchors["forkAxisStartAnchor"].location).normalized() * rig["forkCompressionM"]
    for o in objects:
        role = o["mm_motion_role"]
        if role == "hidden": o.hide_render = True
        elif role == "rear": o.matrix_world = Matrix.Translation(pivot) @ rotation @ Matrix.Translation(-pivot) @ matrices[o.name]
        elif role == "front_lower": o.location += front
        elif role.startswith("shock_"):
            old, new = (a, a) if role == "shock_upper" else (b, moving_b)
            o.matrix_world = Matrix.Translation(new) @ shock_rotation @ Matrix.Translation(-old) @ matrices[o.name]
    scene.render.filepath = str(PREVIEW.with_name("CarbonFrameBikeSuspension.png"))
    bpy.ops.render.render(write_still=True)
    for o in objects:
        o.matrix_world = matrices[o.name]
        o.hide_render = False


def main():
    source_manifest = json.loads((CACHE / "source_manifest.json").read_text(encoding="utf-8"))
    source_sha = hashlib.sha256((CACHE / "official/CarbonFrameBike.glb").read_bytes()).hexdigest()
    if source_manifest["commit"] != COMMIT or source_sha != source_manifest["files"][0]["sha256"]:
        raise ValueError("Run authenticated fetch first")
    for path in (SOURCE.parent, ASSETS / "Modules", DATA, PREVIEW.parent): path.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(CACHE / "official/CarbonFrameBike.glb"))
    scene = bpy.context.scene
    scene.frame_set(0)  # Authored closed pose, not any exploded keyframe.
    scene.unit_settings.system, scene.unit_settings.scale_length = "METRIC", 1
    bpy.context.preferences.filepaths.save_version = 0
    source_objects = {o.name: o for o in scene.objects}
    data = glb_json()
    parents = {child: i for i, n in enumerate(data["nodes"]) for child in n.get("children", [])}
    nodes = []
    for i, n in enumerate(data["nodes"]):
        ancestors, cursor = [], i
        while cursor in parents:
            cursor = parents[cursor]
            ancestors.append(data["nodes"][cursor].get("name", str(cursor)))
        nodes.append({"index": i, **n, "ancestry": ancestors[::-1]})
    # Exact source pivot origins, not arbitrarily assigned geometry centres.
    pivot = source_objects["Federachse"].matrix_world.translation.copy()
    a = source_objects["Daempfer_Cane-Creek_DB_200-57_30305"].matrix_world.translation.copy()
    b = source_objects["Daempfer_Cane-Creek_DB_200-57"].matrix_world.translation.copy()
    if abs((a - b).length - .2) > .00015: raise ValueError("Shock mount distance / source metre scale invalid")
    anchor_positions = {"rearPivotAnchor": pivot, "rearAxisAnchor": pivot + Vector((0, -0.05, 0)),
        "rearWheelAnchor": source_objects["RadHinten"].matrix_world.translation.copy(),
        "shockUpperAnchor": a, "shockLowerAnchor": b,
        "forkAxisStartAnchor": source_objects["Federung"].matrix_world.translation.copy(),
        "forkAxisEndAnchor": source_objects["Federgabel"].matrix_world.translation.copy()}
    parts, objects, excluded, materials = [], [], [], {}
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for n in nodes:
        if "mesh" not in n: continue
        name, text = n["name"], "/".join(n["ancestry"] + [n["name"]])
        if "Shadow" in text or "logo" in text.lower():
            excluded.append({"nodeIndex": n["index"], "sourceName": name, "reason": "display-shadow" if "Shadow" in text else "visible-brand-mark"})
            continue
        original = source_objects[name]
        evaluated = original.evaluated_get(depsgraph)
        mesh = bpy.data.meshes.new_from_object(evaluated, depsgraph=depsgraph)
        mesh.transform(original.matrix_world)
        lo = Vector([min(v.co[a] for v in mesh.vertices) for a in range(3)])
        hi = Vector([max(v.co[a] for v in mesh.vertices) for a in range(3)])
        center = (lo + hi) / 2
        for v in mesh.vertices: v.co -= center
        original_mats = list(mesh.materials)
        mesh.materials.clear()
        for mat in original_mats: mesh.materials.append(neutral_material(mat, materials))
        assembly, key = classify(n)
        identity = f"n{n['index']:04d}"
        obj = bpy.data.objects.new("MM_carbon_" + identity + "_" + key, mesh)
        bpy.context.collection.objects.link(obj)
        obj.location = center
        obj["mm_part_id"], obj["mm_assembly_id"], obj["mm_service_key"] = "carbon." + identity, assembly, key
        obj["mm_source_node"], obj["mm_source_name"], obj["mm_motion_role"], obj["mm_lod"] = n["index"], name, motion_role(n), "source"
        parts.append({"id": obj["mm_part_id"], "object": obj.name, "assemblyId": assembly, "serviceKey": key,
            "sourceNodeIndex": n["index"], "sourceName": name, "sourcePath": text,
            "sourceTriangles": triangles(mesh), "motionRole": obj["mm_motion_role"], "centerM": list(center),
            "serviceBoundary": "sealed-bearing" if "inafag" in text else "sealed-shock" if assembly == "shock" else "source-service-group"})
        objects.append(obj)
    if len(objects) != 307 or len(excluded) != 7: raise ValueError(f"Unexpected pinned inventory {len(objects)} excluded {len(excluded)}")
    for o in source_objects.values(): bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.orphans_purge(do_recursive=True)
    # Orphan glTF materials/images are removed, so no authored logo images leak
    # into the distributable .blend either. Original GLB remains in ignored cache.
    for mat in list(bpy.data.materials):
        if mat.users == 0: bpy.data.materials.remove(mat)
    for img in list(bpy.data.images):
        if img.users == 0: bpy.data.images.remove(img)
    anchors = {}
    for key, position in anchor_positions.items():
        anchor = bpy.data.objects.new("MM_carbon_rig_" + key, None)
        bpy.context.collection.objects.link(anchor)
        anchor.location = position
        anchors[key] = anchor
    rig = {"schemaVersion": 1, "modelId": MODEL_ID, "cycleSeconds": 4,
        "rearMaximumDegrees": 8, "forkCompressionM": .045, "shockEyeDistanceM": (a - b).length,
        **{key: o.name for key, o in anchors.items()},
        "bindings": [{"objectName": p["object"], "role": p["motionRole"]} for p in parts],
        "limitations": ["Prescribed fixed-frame suspension kinematics, not riding/contact/force simulation.",
            "Shock slide is solved from the moving lower mount and fixed upper mount; no damping or pressure simulation.",
            "The source chain stays visible; runtime mesh deformation anchors its front and follows the rear swingarm. No individual link or exact chain-length simulation; flexible lines remain hidden.",
            "Original steering angle and asymmetric wheel geometry are retained; no hardtail drivetrain assumptions."]}
    counts = write_content(parts)
    write_json(ROOT / "Assets/Resources/MechanicalCatalog/CarbonFrameBikeMotionRig.json", rig)
    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE))
    runtime_objects = []
    for obj in objects:
        clone = obj.copy()
        # Already optimized realtime meshes; do not damage teeth/spokes at LOD0.
        clone.data = obj.data.copy()
        clone["mm_lod"] = "lod0"
        original_name = obj.name
        obj.name += "_source"
        clone.name = original_name
        bpy.context.collection.objects.link(clone)
        runtime_objects.append(clone)
    modules = []
    for assembly in ASSEMBLIES:
        members = [o for o in runtime_objects if o["mm_assembly_id"] == assembly]
        path = ASSETS / "Modules" / f"{assembly}_LOD0.fbx"
        if assembly == "frame":
            select(members + list(anchors.values()))
            bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={"MESH", "EMPTY"},
                apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                add_leaf_bones=False, bake_anim=False, mesh_smooth_type="FACE", path_mode="STRIP", use_custom_props=True)
        else: export(path, members)
        modules.append({"assemblyId": assembly, "partObjects": len(members), "triangles": sum(triangles(o.data) for o in members),
            "resourcePath": f"Models/CarbonFrameBike/Modules/{assembly}_LOD0", "bytes": path.stat().st_size})
    whole = []
    for assembly in ASSEMBLIES:
        members = []
        for o in runtime_objects:
            if o["mm_assembly_id"] != assembly: continue
            clone = o.copy()
            clone.data = o.data.copy()
            bpy.context.collection.objects.link(clone)
            members.append(clone)
        select(members)
        bpy.ops.object.join()
        merged = members[0]
        merged.name = "MM_CarbonBike_overview_" + assembly
        merged.data = simplify(merged.data, max(300, int(triangles(merged.data) * .55)))
        whole.append(merged)
    export(ASSETS / "CarbonFrameBike_LOD1.fbx", whole)
    lod1 = sum(triangles(o.data) for o in whole)
    select(whole)
    bpy.ops.object.join()
    merged = whole[0]
    merged.name = "MM_CarbonBike_mobile_overview"
    merged.data = simplify(merged.data, 22000)
    export(ASSETS / "CarbonFrameBike_LOD2.fbx", [merged])
    lod2 = triangles(merged.data)
    bpy.data.objects.remove(merged, do_unlink=True)
    for o in objects: bpy.data.objects.remove(o, do_unlink=True)
    write_json(DATA / "carbon_frame_bike_model_manifest.json", {"schemaVersion": 1, "modelId": MODEL_ID,
        "sourceCommit": COMMIT, "glbSha256": source_sha, "units": "m", "sourcePoseFrame": 0,
        "sourceNodeCount": len(nodes), "sourceMeshCount": len(data["meshes"]), "partObjectCount": len(parts),
        "parts": parts, "excludedReferences": excluded, "authoredImagesRedistributed": False,
        "materialPolicy": "All source maps replaced with neutral, texture-free PBR; separate visible mark geometry omitted."})
    write_json(DATA / "carbon_frame_bike_runtime_assets.json", {"schemaVersion": 1, "modelId": MODEL_ID,
        "modules": modules, "interactionSteps": counts, "sourceTriangles": sum(p["sourceTriangles"] for p in parts),
        "lod0Triangles": sum(m["triangles"] for m in modules), "lod1Triangles": lod1, "lod2Triangles": lod2})
    write_json(DATA / "carbon_frame_bike_engineering.json", {"schemaVersion": 1, "modelId": MODEL_ID, "units": "m",
        "license": "CC-BY-SA-4.0", "shockEyeDistanceM": (a - b).length,
        "shockSourceDesignation": "200-57; 200 mm eye distance verified, 57 mm is authored designation, not measured usable travel",
        "wheelSourceDesignations": {"front": "Reifen_584x75 / Felge_584x24_32Loch", "rear": "Reifen_507x100 / Felge_507x46_32Loch_Kabra"},
        "materials": [{"name": m.name, "metallic": next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Metallic"].default_value,
            "roughness": next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Roughness"].default_value} for m in materials.values()],
        "limitations": rig["limitations"] + ["Source CAD labels may disagree with mesh dimensions; wheel/tire labels are provenance, not validated sizing specifications.",
            "This custom bike is not a Santa Cruz V10 and has no VPP linkage added."]})
    notices = DATA / "CarbonFrameBike"
    notices.mkdir(exist_ok=True)
    for filename in ("README.md", "CC-BY-SA-4.0-LICENSE.txt"): shutil.copyfile(CACHE / "official" / filename, notices / ("AUTHOR_README.md" if filename == "README.md" else filename))
    shutil.copyfile(CACHE / "source_manifest.json", notices / "source_manifest.json")
    (notices / "ATTRIBUTION.txt").write_text(
        "Carbon Frame Bike — Robert Schweier (RobertS Bikes)\n"
        "Realtime version and animation: Felix Herbst / prefrontal cortex; additional support: Needle\n"
        f"Source: https://github.com/prefrontalcortex/glTF-Sample-Models/tree/{COMMIT}/2.0/CarbonFrameBike\n"
        "Creative Commons Attribution-ShareAlike 4.0 International: https://creativecommons.org/licenses/by-sa/4.0/\n\n"
        "Mech.Master modifications (2026-09-29): baked authored closed pose and skinned hoses, retained metre geometry and node identity, "
        "removed separate visible mark meshes and authored image maps, replaced display materials, Chinese service groups, "
        "runtime LODs and source-axis suspension demonstration. These adapted model assets and previews are CC BY-SA 4.0.\n"
        "No endorsement is implied. This notice does not relicense independent application code or other models.\n"
        "Known upstream branding review: https://github.com/KhronosGroup/glTF-Sample-Assets/issues/83\n"
        "Removed visible marks do not represent a resolution of that upstream issue or a separate trademark grant.\n", encoding="utf-8")
    render(scene, runtime_objects, rig, anchors)
    print("CARBON_IMPORT_OK", counts, "objects", len(parts), "LOD0", sum(m["triangles"] for m in modules), "LOD1", lod1, "LOD2", lod2, flush=True)


if __name__ == "__main__": main()
