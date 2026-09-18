"""Export module LOD0 files and consolidated whole-bike LODs from the source blend."""

import json
from pathlib import Path

import bpy


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Art/Models/Source/BicycleEngineeringSource.blend"
MODULE_DIR = ROOT / "Assets/Resources/Models/Bicycle/Modules"
WHOLE_DIR = ROOT / "Assets/Resources/Models/Bicycle"
MANIFEST = ROOT / "Assets/StreamingAssets/MechanicalCatalog/bicycle_runtime_assets.json"

ASSEMBLIES = (
    "frame", "cockpit_headset", "fork", "wheel_front", "wheel_rear",
    "brake_front", "brake_rear", "crank_bottom_bracket", "front_derailleur",
    "rear_derailleur", "chain", "pedals", "saddle_seatpost", "controls_cables",
)

INTERNAL_TOKENS = (
    "bearing", "bushing", "seal", "foam_ring", "spring", "piston", "diaphragm",
    "olive", "connector_insert", "washer", "star_nut", "inner_tube", "rim_tape",
    "cable_end_cap", "ferrule", "valve_core", "pawl", "crush_washer",
)

LOD2_SKIP = INTERNAL_TOKENS + (
    "bolt", "screw", "nut", "nipple", "spoke", "chain_link", "quick_link",
    "pad", "pin", "clip", "cable", "hose", "rotor_lock", "traction_pin",
    "cassette_spacer", "bar_end_plug", "guide", "cap", "ring",
)


def source_parts():
    return [obj for obj in bpy.context.scene.objects if "mm_part_id" in obj]


def select_only(objects):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.select_set(True)
    if objects:
        bpy.context.view_layer.objects.active = objects[0]


def export_fbx(path, objects):
    select_only(objects)
    bpy.ops.export_scene.fbx(
        filepath=str(path), use_selection=True, object_types={"MESH", "OTHER"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", add_leaf_bones=False, bake_anim=False,
        mesh_smooth_type="FACE", path_mode="STRIP", use_custom_props=True,
    )


def polygon_count(objects):
    return sum(len(obj.data.polygons) for obj in objects if obj.type == "MESH")


def duplicate_as_mesh(objects, collection):
    copies = []
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for source in objects:
        if source.type == "MESH":
            obj = source.copy()
            obj.data = source.data.copy()
        else:
            mesh = bpy.data.meshes.new_from_object(source.evaluated_get(depsgraph))
            obj = bpy.data.objects.new(source.name + "_mesh", mesh)
            obj.matrix_world = source.matrix_world.copy()
            for key in source.keys():
                obj[key] = source[key]
        collection.objects.link(obj)
        copies.append(obj)
    return copies


def consolidate_by_assembly(objects, collection, decimate_ratio):
    results = []
    groups = {assembly: [] for assembly in ASSEMBLIES}
    for obj in objects:
        assembly = obj.get("mm_assembly_id")
        if assembly in groups:
            groups[assembly].append(obj)
    for assembly in ASSEMBLIES:
        members = groups[assembly]
        if not members:
            continue
        select_only(members)
        bpy.context.view_layer.objects.active = members[0]
        bpy.ops.object.join()
        merged = members[0]
        merged.name = "MM_LOD1_" + assembly
        merged["mm_assembly_id"] = assembly
        for key in ("mm_part_id", "mm_service_boundary", "mm_lod"):
            if key in merged:
                del merged[key]
        merged["mm_lod"] = "lod1"
        if decimate_ratio < 1.0 and len(merged.data.polygons) > 200:
            modifier = merged.modifiers.new("Runtime decimation", "DECIMATE")
            modifier.ratio = decimate_ratio
            bpy.context.view_layer.objects.active = merged
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        results.append(merged)
    return results


def export_modules(parts):
    records = []
    for assembly in ASSEMBLIES:
        objects = [obj for obj in parts if obj["mm_assembly_id"] == assembly]
        path = MODULE_DIR / f"{assembly}_LOD0.fbx"
        export_fbx(path, objects)
        records.append({
            "assemblyId": assembly,
            "resourcePath": f"Models/Bicycle/Modules/{assembly}_LOD0",
            "partObjects": len(objects),
            "sourcePolygons": polygon_count(objects),
            "bytes": path.stat().st_size,
        })
    return records


def export_lod1(parts):
    visible = [obj for obj in parts if not any(token in obj["mm_part_id"] for token in INTERNAL_TOKENS)]
    collection = bpy.data.collections.new("MM_EXPORT_LOD1")
    bpy.context.scene.collection.children.link(collection)
    copies = duplicate_as_mesh(visible, collection)
    merged = consolidate_by_assembly(copies, collection, 0.58)
    path = WHOLE_DIR / "BicycleEngineering_LOD1.fbx"
    export_fbx(path, merged)
    return {
        "resourcePath": "Models/Bicycle/BicycleEngineering_LOD1",
        "objects": len(merged), "polygons": polygon_count(merged), "bytes": path.stat().st_size,
    }, collection


def export_lod2(parts):
    visible = [obj for obj in parts if not any(token in obj["mm_part_id"] for token in LOD2_SKIP)]
    collection = bpy.data.collections.new("MM_EXPORT_LOD2")
    bpy.context.scene.collection.children.link(collection)
    copies = duplicate_as_mesh(visible, collection)
    select_only(copies)
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    merged = copies[0]
    merged.name = "MM_LOD2_Bicycle"
    for key in list(merged.keys()):
        if key.startswith("mm_"):
            del merged[key]
    merged["mm_lod"] = "lod2"
    modifier = merged.modifiers.new("Thumbnail decimation", "DECIMATE")
    modifier.ratio = 0.30
    bpy.context.view_layer.objects.active = merged
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    path = WHOLE_DIR / "BicycleEngineering_LOD2.fbx"
    export_fbx(path, [merged])
    return {
        "resourcePath": "Models/Bicycle/BicycleEngineering_LOD2",
        "objects": 1, "polygons": polygon_count([merged]), "bytes": path.stat().st_size,
    }, collection


def main():
    MODULE_DIR.mkdir(parents=True, exist_ok=True)
    WHOLE_DIR.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    source_curves = [
        obj for obj in bpy.context.scene.objects
        if obj.type == "CURVE" and "mm_part_id" in obj
    ]
    if source_curves:
        select_only(source_curves)
        bpy.ops.object.convert(target="MESH")
    parts = source_parts()
    if len(parts) != 595:
        raise RuntimeError(f"Expected 595 source parts, got {len(parts)}")
    modules = export_modules(parts)
    lod1, lod1_collection = export_lod1(parts)
    lod2, lod2_collection = export_lod2(parts)
    manifest = {
        "schemaVersion": 1,
        "moduleId": "bike.hardtail.27_5.2x10.v1",
        "sourcePartObjects": len(parts),
        "moduleLod0": modules,
        "wholeBikeLod1": lod1,
        "wholeBikeLod2": lod2,
    }
    MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print("RUNTIME_ASSETS", json.dumps(manifest, ensure_ascii=False))
    bpy.data.collections.remove(lod1_collection)
    bpy.data.collections.remove(lod2_collection)


if __name__ == "__main__":
    main()
