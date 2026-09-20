"""Validate source dimensions, part identities, and generated runtime assets."""

import json
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector
from mathutils.geometry import interpolate_bezier


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Art/Models/Source/BicycleEngineeringSource.blend"
MODEL_MANIFEST = ROOT / "Assets/StreamingAssets/MechanicalCatalog/bicycle_model_manifest.json"
RUNTIME_MANIFEST = ROOT / "Assets/StreamingAssets/MechanicalCatalog/bicycle_runtime_assets.json"
INTERACTION = ROOT / "Assets/Resources/MechanicalCatalog/BicycleInteractionCatalog.json"

EXPECTED_COUNTS = {
    "frame": 22, "cockpit_headset": 24, "fork": 28, "wheel_front": 90,
    "wheel_rear": 120, "brake_front": 37, "brake_rear": 37,
    "crank_bottom_bracket": 25, "front_derailleur": 14,
    "rear_derailleur": 26, "chain": 112, "pedals": 32,
    "saddle_seatpost": 12, "controls_cables": 16,
}


def close(label, actual, expected, tolerance):
    if abs(actual - expected) > tolerance:
        raise RuntimeError(f"{label}: expected {expected}±{tolerance}, got {actual}")
    print(f"PASS {label}: {actual:.4f}")


def bounds(objects):
    points = []
    for obj in objects:
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    low = Vector((min(p.x for p in points), min(p.y for p in points), min(p.z for p in points)))
    high = Vector((max(p.x for p in points), max(p.y for p in points), max(p.z for p in points)))
    return low, high


def by_name(name):
    obj = bpy.data.objects.get(name)
    if obj is None:
        raise RuntimeError("Missing source object " + name)
    return obj


def validate_source():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE))
    parts = [obj for obj in bpy.context.scene.objects if "mm_part_id" in obj]
    if len(parts) != 595:
        raise RuntimeError(f"Expected 595 source parts, got {len(parts)}")
    ids = [obj["mm_part_id"] for obj in parts]
    if len(ids) != len(set(ids)):
        raise RuntimeError("Source part IDs are not unique")
    counts = {}
    for obj in parts:
        assembly = obj["mm_assembly_id"]
        counts[assembly] = counts.get(assembly, 0) + 1
    if counts != EXPECTED_COUNTS:
        raise RuntimeError(f"Assembly counts differ: {counts}")
    print("PASS source objects: 595 unique IDs in 14 assemblies")

    front_tire, rear_tire = by_name("MM_wheel_front_tire"), by_name("MM_wheel_rear_tire")
    front_low, front_high = bounds([front_tire])
    rear_low, rear_high = bounds([rear_tire])
    close("front wheel outer diameter X", front_high.x - front_low.x, .698, .012)
    close("front wheel outer diameter Z", front_high.z - front_low.z, .698, .012)
    close("rear wheel outer diameter X", rear_high.x - rear_low.x, .698, .012)
    close("wheelbase", front_tire.location.x - rear_tire.location.x, 1.120, .001)

    front_rotor = by_name("MM_wheel_front_rotor")
    rear_rotor = by_name("MM_wheel_rear_rotor")
    low, high = bounds([front_rotor])
    close("front rotor diameter", max(high.x-low.x, high.z-low.z), .180, .004)
    low, high = bounds([rear_rotor])
    close("rear rotor diameter", max(high.x-low.x, high.z-low.z), .160, .004)

    front_caps = [by_name("MM_wheel_front_hub_end_cap_1"), by_name("MM_wheel_front_hub_end_cap_2")]
    rear_caps = [by_name("MM_wheel_rear_hub_end_cap_1"), by_name("MM_wheel_rear_hub_end_cap_2")]
    low, high = bounds(front_caps)
    close("front hub spacing", high.y-low.y, .110, .001)
    low, high = bounds(rear_caps)
    close("rear hub spacing", high.y-low.y, .148, .001)

    meshes = [obj for obj in parts if obj.type == "MESH"]
    vertices = sum(len(obj.data.vertices) for obj in meshes)
    polygons = sum(len(obj.data.polygons) for obj in meshes)
    if vertices < 100000 or polygons < 90000:
        raise RuntimeError(f"Source detail unexpectedly low: {vertices} vertices, {polygons} polygons")
    print(f"PASS source detail: {vertices} vertices, {polygons} polygons")
    validate_mounts_routes_and_saddle()
    validate_requested_mechanical_geometry()
    return vertices, polygons


def validate_mounts_routes_and_saddle():
    bb, head = Vector((-.105, 0, .365)), Vector((.305, 0, .585))
    axis = (head - bb).normalized()
    bolts = [by_name(f"MM_frame_bottle_boss_bolt_{i:02d}") for i in (1, 2)]
    for obj in bolts:
        relative = obj.location - bb
        radial = relative - axis * relative.dot(axis)
        close(obj.name + " shaft / tube radius", radial.length, .0305, .0005)
        if not 0 < relative.dot(axis) < (head-bb).length:
            raise RuntimeError("Bottle bolt is beyond down tube")
    close("bottle mounting pitch", abs((bolts[1].location - bolts[0].location).dot(axis)), .064, .0005)

    # Sample the actual Bezier path, not just its endpoints: AUTO handles must not
    # bow a nominally attached cable into the open frame triangle.
    for name, first in (("MM_brake_rear_hose", 6),
                        ("MM_controls_housing_1", 5), ("MM_controls_housing_2", 5)):
        obj = by_name(name)
        points = obj.data.splines[0].bezier_points
        if len(points) < 13:
            raise RuntimeError("Missing frame-following route: " + name)
        for i in range(first, first + 3):
            a, b = points[i], points[i+1]
            for p in interpolate_bezier(a.co, a.handle_right, b.handle_left, b.co, 24):
                relative = p - bb
                distance = (relative - axis * relative.dot(axis)).length
                if not .038 < distance < .052:
                    raise RuntimeError(f"{name} floats away from / cuts into down tube: {distance}")
    front = by_name("MM_brake_front_hose").data.splines[0].bezier_points
    if len(front) < 7 or any(p.co.y > -.07 for p in front):
        raise RuntimeError("Front hose must follow outside of left fork")
    print("PASS brake / shift routes: sampled down-tube clearance and outside-fork route")

    for name in ("shell", "padding", "cover"):
        obj = by_name("MM_saddle_" + name)
        mesh = bmesh.new()
        mesh.from_mesh(obj.data)
        closed = all(edge.is_manifold for edge in mesh.edges)
        volume = mesh.calc_volume(signed=True)
        mesh.free()
        if not closed or volume <= 0:
            raise RuntimeError("Saddle layer must be a closed outward-facing volume: " + name)
    cover = by_name("MM_saddle_cover")
    vertices = [v.co for v in cover.data.vertices]
    rear_width = max(abs(v.y) for v in vertices if v.x < -.035) * 2
    nose_width = max(abs(v.y) for v in vertices if v.x > .065) * 2
    if rear_width < nose_width * 2.5:
        raise RuntimeError("Saddle must have wide rear support and a narrow nose")
    stride, top = 33, 81 * 33
    center = vertices[top + 40 * stride + 16]
    shoulder = vertices[top + 40 * stride + 8]
    if shoulder.z - center.z < .003:
        raise RuntimeError("Saddle pressure-relief channel is missing")
    print("PASS saddle: 3 closed layers, tapered nose, wide rear, relief channel")


def distance_to_axis(point, start, end):
    axis = end - start
    projection = max(0.0, min(1.0, (point-start).dot(axis) / axis.length_squared))
    return (point - start.lerp(end, projection)).length


def validate_requested_mechanical_geometry():
    rear, front = Vector((-.560, -.043, .349)), Vector((-.105, -.070, .365))
    chain_links = [by_name(f"MM_chain_link_{i:03d}") for i in range(1, 111)]
    chain_low, chain_high = bounds(chain_links)
    if chain_low.x > rear.x-.045 or chain_high.x < front.x+.068:
        raise RuntimeError("Chain does not wrap around both sprocket ends")
    if any(len(obj.data.polygons) < 28 for obj in chain_links):
        raise RuntimeError("Chain links must use rounded capsule plates, not rectangular blocks")
    print("PASS chain: rounded plates wrap both front and rear sprockets")

    outer, inner = by_name("MM_crank_chainring_1"), by_name("MM_crank_chainring_2")
    outer_low, outer_high = bounds([outer])
    inner_low, inner_high = bounds([inner])
    close("36T outer chainring diameter", max(outer_high.x-outer_low.x, outer_high.z-outer_low.z), .152, .004)
    close("22T inner chainring diameter", max(inner_high.x-inner_low.x, inner_high.z-inner_low.z), .096, .004)
    if abs(outer.matrix_world.translation.y-inner.matrix_world.translation.y) < .010:
        raise RuntimeError("2x drivetrain chainrings are not laterally separated")
    print("PASS front drivetrain: visibly separate 36T / 22T chainrings")

    crown, dropout = Vector((.324, 0, .620)), Vector((.560, 0, .349))
    for side_index, side in enumerate((-1, 1), 1):
        axis_top = crown + Vector((0, side*.054, 0))
        axis_bottom = dropout + Vector((0, side*.055, 0))
        for bushing_index in (1, 2):
            obj = by_name(f"MM_fork_guide_bushing_{side_index}_{bushing_index}")
            radial = distance_to_axis(obj.matrix_world.translation, axis_top, axis_bottom)
            if radial > .002:
                raise RuntimeError(f"{obj.name} is not concentric with its fork leg: {radial}")
    damper = by_name("MM_fork_damper_cartridge")
    damper_radial = distance_to_axis(damper.matrix_world.translation,
                                      crown+Vector((0,.054,0)), dropout+Vector((0,.055,0)))
    if damper_radial > .002:
        raise RuntimeError(f"Damper cartridge is suspended outside its fork leg: {damper_radial}")
    print("PASS fork: guide bushings and damper are concentric with lower legs")

    for which in ("front", "rear"):
        blade = by_name(f"MM_brake_{which}_lever_blade")
        points = blade.data.splines[0].bezier_points
        root, tip = points[0].co, points[-1].co
        if tip.x >= root.x or abs(tip.y) >= abs(root.y):
            raise RuntimeError(f"{which} brake lever still points forward / away from the grip")
    print("PASS brake levers: both blades sweep rearward and toward the grip area")

    for side in (1, 2):
        body = by_name(f"MM_pedal_body_{side}")
        if not body.get("mm_open_platform") or len(body.data.polygons) < 80:
            raise RuntimeError(f"Pedal {side} is not an open, braced platform")
    print("PASS pedals: joined open cages replace solid slabs")


def validate_manifests(source_polygons):
    model = json.loads(MODEL_MANIFEST.read_text(encoding="utf-8"))
    if model["partObjects"] != 595 or len(model["parts"]) != 595:
        raise RuntimeError("Model manifest does not contain 595 parts")
    runtime = json.loads(RUNTIME_MANIFEST.read_text(encoding="utf-8"))
    modules = runtime["moduleLod0"]
    if len(modules) != 14 or sum(item["partObjects"] for item in modules) != 595:
        raise RuntimeError("Runtime module manifest is incomplete")
    for item in modules:
        path = ROOT / "Assets/Resources" / (item["resourcePath"] + ".fbx")
        if not path.exists() or path.stat().st_size != item["bytes"]:
            raise RuntimeError("Runtime FBX missing or size changed: " + str(path))
    lod1 = runtime["wholeBikeLod1"]
    lod2 = runtime["wholeBikeLod2"]
    if not (lod2["polygons"] < lod1["polygons"] < source_polygons):
        raise RuntimeError("LOD polygon budgets are not strictly descending")
    if lod1["objects"] != 14 or lod2["objects"] != 1:
        raise RuntimeError("Whole-bike LOD consolidation differs")
    print(f"PASS runtime LODs: source={source_polygons}, lod1={lod1['polygons']}, lod2={lod2['polygons']}")

    expected_names = {part["object"] for part in model["parts"]}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for item in modules:
        path = ROOT / "Assets/Resources" / (item["resourcePath"] + ".fbx")
        bpy.ops.import_scene.fbx(filepath=str(path))
    imported_names = {obj.name for obj in bpy.context.scene.objects}
    missing_names = sorted(expected_names - imported_names)
    if missing_names:
        raise RuntimeError("FBX round-trip lost object names: " + ", ".join(missing_names[:12]))
    print("PASS FBX round-trip bindings: 595 object names preserved")

    interaction = json.loads(INTERACTION.read_text(encoding="utf-8"))
    counts = {plan["difficulty"]: len(plan["steps"]) for plan in interaction["plans"]}
    if counts != {"Simple": 15, "Standard": 30, "Advanced": 45}:
        raise RuntimeError(f"Interaction counts differ: {counts}")
    print("PASS interaction plans: Simple=15, Standard=30, Advanced=45")


def main():
    _, polygons = validate_source()
    validate_manifests(polygons)
    print("ALL ENGINEERING BICYCLE VALIDATIONS PASSED")


if __name__ == "__main__":
    main()
